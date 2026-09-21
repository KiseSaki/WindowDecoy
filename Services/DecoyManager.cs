using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using WindowDecoy.Interop;
using WindowDecoy.Models;
using WindowDecoy.Views;

namespace WindowDecoy.Services;

public class DecoyManager : IDisposable
{
    private readonly DispatcherTimer _watchdogTimer;
    private readonly object _lock = new();
    private readonly WinEventProc _winEventDelegate;
    private nint _winEventHook;

    public ObservableCollection<DecoySession> Sessions { get; } = new();

    public event Action<DecoySession>? SessionStateChanged;

    public DecoyManager()
    {
        _watchdogTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _watchdogTimer.Tick += OnWatchdogTick;
        _watchdogTimer.Start();

        // Install WinEvent hook to detect foreground window change in real time
        _winEventDelegate = OnWinEvent;
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_SYSTEM_FOREGROUND,
            NativeConstants.EVENT_SYSTEM_FOREGROUND,
            0,
            _winEventDelegate,
            0,
            0,
            NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS);
    }

    private void OnWinEvent(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (eventType != NativeConstants.EVENT_SYSTEM_FOREGROUND)
            return;

        CheckTargetLostFocus(hwnd);
    }

    private void CheckTargetLostFocus(nint newFgHwnd)
    {
        uint newFgPid = 0;
        if (newFgHwnd != 0 && NativeMethods.IsWindow(newFgHwnd))
        {
            NativeMethods.GetWindowThreadProcessId(newFgHwnd, out newFgPid);
        }

        uint myPid = (uint)Environment.ProcessId;

        // If newly foregrounded window belongs to WindowDecoy UI, don''t hide game
        if (newFgPid != 0 && newFgPid == myPid)
            return;

        foreach (var session in Sessions.ToList())
        {
            if (session.State == DecoyState.TargetRevealed && session.Profile.AutoDisguiseOnLostFocus)
            {
                // Debounce handover grace period
                if ((DateTime.UtcNow - session.LastHandoverTime).TotalMilliseconds < 500)
                    continue;

                // If newly foregrounded window does not belong to the target game, target has lost focus!
                if (newFgPid != session.TargetProcessId)
                {
                    AutoReDisguise(session);
                    AutoReDisguise(session, startMinimized: true);
                }
            }
        }
    }

    public bool ActivateSession(DecoySession session)
    {
        lock (_lock)
        {
            if (session.State == DecoyState.Active || session.State == DecoyState.Activating || session.State == DecoyState.Restoring)
                return false;

            if (session.State == DecoyState.TargetRevealed)
            {
                return AutoReDisguise(session, startMinimized: false);
            }

            session.State = DecoyState.Activating;
            session.ErrorMessage = string.Empty;
            SessionStateChanged?.Invoke(session);

            try
            {
                // 1. Locate window
                nint hwnd = session.TargetHwnd;
                if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
                {
                    hwnd = WindowLocator.FindTargetWindow(session.TargetHwnd, session.TargetProcessId, session.TargetProcessName);
                }

                if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
                {
                    session.ErrorMessage = $"找不到目标窗口 ({session.TargetProcessName}, PID: {session.TargetProcessId})";
                    session.State = DecoyState.Error;
                    SessionStateChanged?.Invoke(session);
                    MessageBox.Show(session.ErrorMessage, "伪装启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                session.TargetHwnd = hwnd;

                // 2. Capture state
                var savedState = WindowController.CaptureWindowState(hwnd, session.TargetProcessId, session.TargetProcessName);
                if (savedState == null)
                {
                    session.ErrorMessage = "未能读取目标窗口状态";
                    session.State = DecoyState.Error;
                    SessionStateChanged?.Invoke(session);
                    MessageBox.Show(session.ErrorMessage, "伪装启动失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                session.SavedState = savedState;

                // 3. Hide all windows belonging to target process
                var hiddenHwnds = WindowController.HideAllProcessWindows(session.TargetProcessId, hwnd);
                savedState.AllHiddenHwnds = hiddenHwnds;

                // 4. Create and Show Decoy Window on UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var decoy = new DecoyWindow(session, s => RevealTarget(s));
                    session.DecoyWindow = decoy;
                    decoy.Show();
                    decoy.Activate();
                });

                session.State = DecoyState.Active;
                SessionStateChanged?.Invoke(session);
                return true;
            }
            catch (Exception ex)
            {
                session.ErrorMessage = ex.Message;
                session.State = DecoyState.Error;
                SessionStateChanged?.Invoke(session);
                MessageBox.Show($"启动伪装时发生异常:\n{ex.Message}", "伪装启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }

    public bool AutoReDisguise(DecoySession session, bool startMinimized = true)
    {
        lock (_lock)
        {
            if (session.State != DecoyState.TargetRevealed)
                return false;

            session.State = DecoyState.Activating;
            SessionStateChanged?.Invoke(session);

            try
            {
                // 1. Hide all windows belonging to target process
                var hiddenHwnds = WindowController.HideAllProcessWindows(session.TargetProcessId, session.TargetHwnd);
                if (session.SavedState != null)
                {
                    session.SavedState.AllHiddenHwnds = hiddenHwnds;
                }

                // 2. Create and show Decoy Window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (session.DecoyWindow != null)
                    {
                        session.DecoyWindow.CloseProgrammatically();
                        session.DecoyWindow = null;
                    }

                    var decoy = new DecoyWindow(session, s => RevealTarget(s), startMinimized: startMinimized);
                    session.DecoyWindow = decoy;
                    if (startMinimized)
                    {
                        decoy.WindowState = WindowState.Minimized;
                        decoy.ShowActivated = false;
                        decoy.Show();
                    }
                    else
                    {
                        decoy.Show();
                        decoy.Activate();
                    }
                });

                session.State = DecoyState.Active;
                session.ErrorMessage = string.Empty;
                SessionStateChanged?.Invoke(session);
                return true;
            }
            catch (Exception ex)
            {
                session.ErrorMessage = ex.Message;
                session.State = DecoyState.Error;
                SessionStateChanged?.Invoke(session);
                return false;
            }
        }
    }

    public bool RevealTarget(DecoySession session)
    {
        lock (_lock)
        {
            if (session.State != DecoyState.Active)
                return false;

            session.State = DecoyState.Restoring;
            SessionStateChanged?.Invoke(session);

            try
            {
                // 1. Restore Target Window and bring it to foreground FIRST while Decoy Window is still active foreground!
                if (session.SavedState != null)
                {
                    WindowController.RestoreWindow(session.SavedState);
                }
                else if (session.TargetHwnd != 0)
                {
                    WindowController.EmergencyShowWindow(session.TargetHwnd);
                }

                // 2. Close Decoy Window after target window is already restored and foregrounded
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (session.DecoyWindow != null)
                    {
                        session.DecoyWindow.CloseProgrammatically();
                        session.DecoyWindow = null;
                    }
                });

                session.LastHandoverTime = DateTime.UtcNow;

                // Enter TargetRevealed loop if Proxy mode and AutoDisguise is enabled
                if (session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy && session.Profile.AutoDisguiseOnLostFocus)
                {
                    session.State = DecoyState.TargetRevealed;
                }
                else
                {
                    session.State = DecoyState.TargetSelected;
                }

                session.ErrorMessage = string.Empty;
                SessionStateChanged?.Invoke(session);
                return true;
            }
            catch (Exception ex)
            {
                session.ErrorMessage = ex.Message;
                session.State = DecoyState.Error;
                SessionStateChanged?.Invoke(session);
                return false;
            }
        }
    }

    public bool RestoreSession(DecoySession session)
    {
        lock (_lock)
        {
            if (session.State != DecoyState.Active && session.State != DecoyState.TargetRevealed)
                return false;

            session.State = DecoyState.Restoring;
            SessionStateChanged?.Invoke(session);

            try
            {
                // 1. Restore Target Window
                if (session.SavedState != null)
                {
                    WindowController.RestoreWindow(session.SavedState);
                }
                else if (session.TargetHwnd != 0)
                {
                    WindowController.EmergencyShowWindow(session.TargetHwnd);
                }

                // 2. Close Decoy Window
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (session.DecoyWindow != null)
                    {
                        session.DecoyWindow.CloseProgrammatically();
                        session.DecoyWindow = null;
                    }
                });

                // Explicit restore stops disguise loop completely
                session.State = DecoyState.TargetSelected;
                session.ErrorMessage = string.Empty;
                SessionStateChanged?.Invoke(session);
                return true;
            }
            catch (Exception ex)
            {
                session.ErrorMessage = ex.Message;
                session.State = DecoyState.Error;
                SessionStateChanged?.Invoke(session);
                return false;
            }
        }
    }

    public void ActivateAll()
    {
        foreach (var session in Sessions.ToList())
        {
            if (session.CanActivate && (session.TargetHwnd != 0 || session.TargetProcessId != 0))
            {
                ActivateSession(session);
            }
            else if (session.State == DecoyState.TargetRevealed)
            {
                AutoReDisguise(session, startMinimized: false);
            }
        }
    }

    public void RestoreAll()
    {
        foreach (var session in Sessions.ToList())
        {
            if (session.IsActive)
            {
                RestoreSession(session);
            }
        }
    }

    private void OnWatchdogTick(object? sender, EventArgs e)
    {
        foreach (var session in Sessions.ToList())
        {
            if (session.State == DecoyState.Active || session.State == DecoyState.TargetRevealed)
            {
                bool targetDead = false;
                try
                {
                    using var proc = Process.GetProcessById((int)session.TargetProcessId);
                    if (proc.HasExited)
                    {
                        targetDead = true;
                    }
                }
                catch
                {
                    targetDead = true;
                }

                if (targetDead)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        session.DecoyWindow?.CloseProgrammatically();
                        session.DecoyWindow = null;
                        session.SavedState = null;
                        session.TargetHwnd = 0;
                        session.State = DecoyState.Idle;
                        SessionStateChanged?.Invoke(session);
                    });
                    continue;
                }
            }

            // Fallback watchdog check for TargetRevealed losing focus
            if (session.State == DecoyState.TargetRevealed && session.Profile.AutoDisguiseOnLostFocus)
            {
                if ((DateTime.UtcNow - session.LastHandoverTime).TotalMilliseconds > 600)
                {
                    nint fg = NativeMethods.GetForegroundWindow();
                    uint fgPid = 0;
                    if (fg != 0)
                    {
                        NativeMethods.GetWindowThreadProcessId(fg, out fgPid);
                    }

                    if (fgPid != session.TargetProcessId && fgPid != (uint)Environment.ProcessId)
                    {
                        AutoReDisguise(session, startMinimized: true);
                    }
                }
            }
        }
    }

    public void EmergencyRestoreAll()
    {
        foreach (var session in Sessions)
        {
            if (session.State == DecoyState.Active || session.State == DecoyState.TargetRevealed)
            {
                try
                {
                    if (session.SavedState != null && session.SavedState.Hwnd != 0)
                    {
                        WindowController.EmergencyShowWindow(session.SavedState.Hwnd);
                    }
                    else if (session.TargetHwnd != 0)
                    {
                        WindowController.EmergencyShowWindow(session.TargetHwnd);
                    }
                }
                catch
                {
                    // Ignore on emergency exit
                }
            }
        }
    }

    public void Dispose()
    {
        _watchdogTimer.Stop();
        if (_winEventHook != 0)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = 0;
        }
    }
}