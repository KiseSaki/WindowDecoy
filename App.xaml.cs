using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowDecoy.Interop;
using WindowDecoy.Models;
using WindowDecoy.Services;
using WindowDecoy.Views;
using WindowDecoy.ViewModels;

namespace WindowDecoy;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--test"))
        {
            NativeMethods.AttachConsole(unchecked((uint)-1));
            RunSmokeTests();
            Shutdown(0);
            return;
        }

        // Fail-safe protection: System session ending (Logoff / Shutdown)
        SystemEvents.SessionEnding += OnSessionEnding;

        // Fail-safe protection: AppDomain Unhandled Exception
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Fail-safe protection: Dispatcher Unhandled Exception
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e)
    {
        TriggerEmergencyRestore();
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        TriggerEmergencyRestore();
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show($"WindowDecoy 发生未捕获异常:\n{ex.Message}\n\n堆栈:\n{ex.StackTrace}", "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true; // Prevent app from crashing silently!
        TriggerEmergencyRestore();
        MessageBox.Show($"WindowDecoy 发生异常:\n{e.Exception.Message}\n\n堆栈:\n{e.Exception.StackTrace}", "错误提示", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TriggerEmergencyRestore();
        SystemEvents.SessionEnding -= OnSessionEnding;
        base.OnExit(e);
    }

    private void TriggerEmergencyRestore()
    {
        try
        {
            if (MainWindow is MainWindow mainWindow && mainWindow.DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
        }
        catch
        {
            // Ignore during emergency shutdown
        }
    }

    private void RunSmokeTests()
    {
        Console.WriteLine("========================================");
        Console.WriteLine("[WindowDecoy Smoke Verification Suite]");
        Console.WriteLine("========================================");

        // 1. Hotkey Parsing Tests
        Console.WriteLine("[Test 1] Testing Hotkey Parsing...");
        bool qOk = HotkeyManager.ParseHotkey("Ctrl+Alt+Q", out uint qMod, out uint qVk);
        Console.WriteLine($"  Ctrl+Alt+Q => Success: {qOk}, Mod: 0x{qMod:X}, VK: 0x{qVk:X}");

        bool tildeOk = HotkeyManager.ParseHotkey("Ctrl+Alt+`", out uint tMod, out uint tVk);
        Console.WriteLine($"  Ctrl+Alt+` => Success: {tildeOk}, Mod: 0x{tMod:X}, VK: 0x{tVk:X}");

        if (!qOk || !tildeOk)
        {
            Console.WriteLine("[FAIL] Hotkey parsing failed!");
            return;
        }

        // 2. Profile Service Tests
        Console.WriteLine("[Test 2] Testing Profile Service...");
        var defaults = ProfileService.GetDefaultProfiles();
        Console.WriteLine($"  Default profiles loaded: {defaults.Count}");
        if (defaults.Count == 0 || string.IsNullOrEmpty(defaults[0].FakeTitle))
        {
            Console.WriteLine("[FAIL] Default profile invalid!");
            return;
        }

        // 3. Window Enumerator Tests
        Console.WriteLine("[Test 3] Testing Window Enumeration...");
        var windows = WindowEnumerator.GetTopLevelWindows();
        Console.WriteLine($"  Discovered {windows.Count} visible top-level windows.");
        foreach (var w in windows.Take(5))
        {
            Console.WriteLine($"    HWND: 0x{w.Hwnd:X8} | PID: {w.ProcessId,6} | {w.ProcessName,-15} | {w.Title}");
        }

        // 4. DecoyManager and State Machine
        Console.WriteLine("[Test 4] Testing DecoyManager and State Machine...");
        var manager = new DecoyManager();
        var session = new DecoySession
        {
            Profile = defaults[0]
        };
        Console.WriteLine($"  Initial session state: {session.State} (Status: {session.StatusText})");
        manager.Sessions.Add(session);

        // 5. WindowController Hide & Restore Verification (if a window exists)
        if (windows.Count > 0)
        {
            var target = windows[0];
            Console.WriteLine($"[Test 5] Testing Hide & Restore on HWND 0x{target.Hwnd:X8} ({target.ProcessName})...");

            bool initiallyVisible = NativeMethods.IsWindowVisible(target.Hwnd);
            Console.WriteLine($"  Initially visible: {initiallyVisible}");

            // Capture state
            var saved = WindowController.CaptureWindowState(target.Hwnd, target.ProcessId, target.ProcessName);
            Console.WriteLine($"  State captured successfully: {saved != null}");

            // Hide
            bool hideSuccess = WindowController.HideWindow(target.Hwnd);
            bool visibleAfterHide = NativeMethods.IsWindowVisible(target.Hwnd);
            Console.WriteLine($"  HideWindow called => Success: {hideSuccess}, IsWindowVisible now: {visibleAfterHide}");

            // Restore
            bool restoreSuccess = false;
            if (saved != null)
            {
                restoreSuccess = WindowController.RestoreWindow(saved);
            }
            bool visibleAfterRestore = NativeMethods.IsWindowVisible(target.Hwnd);
            Console.WriteLine($"  RestoreWindow called => Success: {restoreSuccess}, IsWindowVisible now: {visibleAfterRestore}");

            if (visibleAfterHide || !visibleAfterRestore)
            {
                Console.WriteLine("[FAIL] Hide or Restore visibility check failed!");
                return;
            }
        }

        Console.WriteLine("========================================");
        Console.WriteLine("[ALL VERIFICATIONS PASSED SUCCESSFULLY]");
        Console.WriteLine("========================================");
    }
}

