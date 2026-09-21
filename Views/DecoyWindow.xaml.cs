using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WindowDecoy.Interop;
using WindowDecoy.Models;
using WindowDecoy.Services;

namespace WindowDecoy.Views;

public partial class DecoyWindow : Window
{
    private readonly DecoySession _session;
    private readonly Action<DecoySession> _restoreCallback;
    private readonly bool _startMinimized;
    private nint _hwnd;
    private HwndSource? _hwndSource;
    private nint _customHIcon;
    private bool _isInternalClosing;
    private bool _proxyReady;
    private int _isRestoring;

    public DecoyWindow(DecoySession session, Action<DecoySession> restoreCallback, bool startMinimized = false)
    {
        InitializeComponent();
        _session = session;
        _restoreCallback = restoreCallback;
        _startMinimized = startMinimized;

        Title = string.IsNullOrWhiteSpace(session.Profile.FakeTitle) ? "Decoy Window" : session.Profile.FakeTitle;

        if (_startMinimized)
        {
            WindowState = WindowState.Minimized;
            ShowActivated = false;
        }

        Loaded += OnLoaded;
        Closing += OnClosing;
        Activated += OnActivated;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd != 0)
        {
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        ApplyAppearance();
        ApplyGeometry();

        if (_startMinimized)
        {
            _proxyReady = true;
        }
        else
        {
            // Arm proxy ready after initial show settles (350ms)
            Task.Delay(350).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    _proxyReady = true;
                });
            });
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeConstants.WM_ACTIVATE:
            {
                int state = (int)(wParam.ToInt64() & 0xFFFF);
                if (state == NativeConstants.WA_INACTIVE)
                {
                    // As soon as this window loses focus (user clicked another app / desktop),
                    // proxy mode is immediately armed for the next Alt+Tab or taskbar click!
                    _proxyReady = true;
                }
                else if (state == NativeConstants.WA_ACTIVE || state == NativeConstants.WA_CLICKACTIVE)
                {
                    if (_proxyReady && _session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
                    {
                        TriggerRestore();
                    }
                }
                break;
            }

            case NativeConstants.WM_MOUSEACTIVATE:
            {
                if (_proxyReady && _session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
                {
                    TriggerRestore();
                }
                break;
            }
        }

        return 0;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
        {
            if (_proxyReady)
            {
                TriggerRestore();
                e.Handled = true;
            }
        }
        else if (_session.Profile.BehaviorMode == DecoyBehaviorMode.Safe)
        {
            // In Safe Mode, double-clicking anywhere on the Decoy window also restores the original game!
            if (e.ClickCount >= 2)
            {
                TriggerRestore();
                e.Handled = true;
            }
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_proxyReady && _session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
        {
            TriggerRestore();
            e.Handled = true;
        }
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        if (_proxyReady && _session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
        {
            TriggerRestore();
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState != WindowState.Minimized)
        {
            if (_startMinimized)
            {
                ApplyGeometry();
            }

            if (_proxyReady && _session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
            {
                TriggerRestore();
            }
        }
    }

    private void TriggerRestore()
    {
        if (_isInternalClosing) return;
        if (Interlocked.Exchange(ref _isRestoring, 1) != 0) return;

        Dispatcher.InvokeAsync(() =>
        {
            _restoreCallback(_session);
        });
    }

    private void ApplyAppearance()
    {
        try
        {
            // 1. Apply Icon
            if (!string.IsNullOrWhiteSpace(_session.Profile.FakeIconPath) && File.Exists(_session.Profile.FakeIconPath))
            {
                var iconSource = IconService.ExtractIconFromFile(_session.Profile.FakeIconPath);
                if (iconSource != null)
                {
                    Icon = iconSource;
                }

                _customHIcon = IconService.GetHIconFromFile(_session.Profile.FakeIconPath);
                if (_customHIcon != 0 && _hwnd != 0)
                {
                    NativeMethods.SendMessage(_hwnd, NativeConstants.WM_SETICON, NativeConstants.ICON_SMALL, _customHIcon);
                    NativeMethods.SendMessage(_hwnd, NativeConstants.WM_SETICON, NativeConstants.ICON_BIG, _customHIcon);
                }
            }

            // 2. Apply Screenshot
            if (!string.IsNullOrWhiteSpace(_session.Profile.FakeScreenshotPath) && File.Exists(_session.Profile.FakeScreenshotPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_session.Profile.FakeScreenshotPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                ScreenshotImage.Source = bitmap;
            }

            // 3. Set Tooltip
            if (_session.Profile.BehaviorMode == DecoyBehaviorMode.Proxy)
            {
                ToolTip = "WindowDecoy 代理直通模式：点击、按键或 Alt+Tab 切换将立即唤出原程序";
            }
            else
            {
                ToolTip = "WindowDecoy 安全防窥模式：双击或按恢复快捷键唤出原程序";
            }
        }
        catch
        {
            // Silently handle invalid image/icon
        }
    }

    private void ApplyGeometry()
    {
        try
        {
            var profile = _session.Profile;
            var savedState = _session.SavedState;

            if (_startMinimized && WindowState == WindowState.Minimized)
            {
                // Pre-configure restore dimensions without un-minimizing or forcing window display
                if (profile.SizeMode == WindowSizeMode.TargetWindow && savedState != null)
                {
                    var rect = savedState.WindowRect;
                    if (rect.Width > 50 && rect.Height > 50)
                    {
                        Left = rect.Left;
                        Top = rect.Top;
                        Width = rect.Width;
                        Height = rect.Height;
                    }
                }
                else if (profile.SizeMode == WindowSizeMode.ScreenshotSize && ScreenshotImage.Source != null)
                {
                    double w = ScreenshotImage.Source.Width;
                    double h = ScreenshotImage.Source.Height;
                    if (w > 100 && h > 100)
                    {
                        Width = w;
                        Height = h;
                    }
                }
                else
                {
                    Width = Math.Max(200, profile.CustomWidth > 0 ? profile.CustomWidth : 1280);
                    Height = Math.Max(150, profile.CustomHeight > 0 ? profile.CustomHeight : 720);
                }
                return;
            }

            if (profile.SizeMode == WindowSizeMode.TargetWindow && savedState != null)
            {
                var rect = savedState.WindowRect;
                if (rect.Width > 50 && rect.Height > 50)
                {
                    NativeMethods.SetWindowPos(_hwnd, 0, rect.Left, rect.Top, rect.Width, rect.Height, NativeConstants.SWP_NOZORDER | NativeConstants.SWP_SHOWWINDOW);

                    if (savedState.Placement.showCmd == NativeConstants.SW_SHOWMAXIMIZED)
                    {
                        WindowState = WindowState.Maximized;
                    }
                }
                else
                {
                    Width = 1280;
                    Height = 720;
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            else if (profile.SizeMode == WindowSizeMode.ScreenshotSize && ScreenshotImage.Source != null)
            {
                double w = ScreenshotImage.Source.Width;
                double h = ScreenshotImage.Source.Height;
                if (w > 100 && h > 100)
                {
                    Width = w;
                    Height = h;
                }

                if (profile.PositionMode == WindowPositionMode.ScreenCenter)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            else
            {
                Width = Math.Max(200, profile.CustomWidth > 0 ? profile.CustomWidth : 1280);
                Height = Math.Max(150, profile.CustomHeight > 0 ? profile.CustomHeight : 720);

                if (profile.PositionMode == WindowPositionMode.Custom)
                {
                    Left = profile.CustomX;
                    Top = profile.CustomY;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
        }
        catch
        {
            Width = 1280;
            Height = 720;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public void CloseProgrammatically()
    {
        _isInternalClosing = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        if (_customHIcon != 0)
        {
            NativeMethods.DestroyIcon(_customHIcon);
            _customHIcon = 0;
        }

        if (!_isInternalClosing && _session.State == DecoyState.Active)
        {
            // User closed Decoy window manually (Alt+F4 or Close button) -> fail-safe restore game!
            _restoreCallback(_session);
        }
    }
}