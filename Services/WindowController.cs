using System;
using System.Collections.Generic;
using WindowDecoy.Interop;
using WindowDecoy.Models;

namespace WindowDecoy.Services;

public static class WindowController
{
    public static SavedWindowState? CaptureWindowState(nint hwnd, uint pid, string processName)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            return null;

        NativeMethods.GetWindowRect(hwnd, out RECT rect);
        nint monitor = NativeMethods.MonitorFromWindow(hwnd, NativeConstants.MONITOR_DEFAULTTONEAREST);
        nint style = NativeMethods.GetWindowLongPtr(hwnd, NativeConstants.GWL_STYLE);
        nint exStyle = NativeMethods.GetWindowLongPtr(hwnd, NativeConstants.GWL_EXSTYLE);

        var placement = WINDOWPLACEMENT.Create();
        bool gotPlacement = NativeMethods.GetWindowPlacement(hwnd, ref placement);
        if (!gotPlacement)
        {
            placement.showCmd = NativeConstants.SW_SHOWNORMAL;
            placement.rcNormalPosition = rect;
        }

        return new SavedWindowState
        {
            Hwnd = hwnd,
            ProcessId = pid,
            ProcessName = processName,
            Placement = placement,
            WindowRect = rect,
            MonitorHandle = monitor,
            OriginalStyle = style,
            OriginalExStyle = exStyle
        };
    }

    public static bool HideWindow(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            return false;

        NativeMethods.ShowWindow(hwnd, NativeConstants.SW_HIDE);
        NativeMethods.SetWindowPos(hwnd, 0, 0, 0, 0, 0,
            NativeConstants.SWP_HIDEWINDOW | NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);

        return true;
    }

    public static List<nint> HideAllProcessWindows(uint pid, nint mainHwnd)
    {
        var hiddenHwnds = new List<nint>();

        if (mainHwnd != 0 && NativeMethods.IsWindow(mainHwnd))
        {
            HideWindow(mainHwnd);
            hiddenHwnds.Add(mainHwnd);

            // Check if window is still visible after hide!
            if (NativeMethods.IsWindowVisible(mainHwnd))
            {
                throw new InvalidOperationException(
                    "未能隐藏目标窗口！\n\n该目标程序很可能正在以【管理员身份】运行，而 WindowDecoy 当前为普通权限。\n受 Windows 界面特权隔离 (UIPI) 限制，普通权限无法操作管理员窗口。\n\n请退出当前程序，右键选择【以管理员身份运行】WindowDecoy 后重试！");
            }
        }

        if (pid != 0)
        {
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                try
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowPid);
                    if (windowPid == pid && NativeMethods.IsWindowVisible(hwnd))
                    {
                        HideWindow(hwnd);
                        if (!hiddenHwnds.Contains(hwnd))
                        {
                            hiddenHwnds.Add(hwnd);
                        }
                    }
                }
                catch { }
                return true;
            }, 0);
        }

        return hiddenHwnds;
    }

    public static bool RestoreWindow(SavedWindowState state)
    {
        if (state == null)
            return false;

        // 1. Restore all windows hidden during activation
        if (state.AllHiddenHwnds != null)
        {
            foreach (var h in state.AllHiddenHwnds)
            {
                try
                {
                    if (h != 0 && NativeMethods.IsWindow(h))
                    {
                        NativeMethods.ShowWindow(h, NativeConstants.SW_SHOW);
                        NativeMethods.SetWindowPos(h, 0, 0, 0, 0, 0,
                            NativeConstants.SWP_SHOWWINDOW | NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
                    }
                }
                catch { }
            }
        }

        // 2. Restore placement of primary window
        nint hwnd = WindowLocator.FindTargetWindow(state.Hwnd, state.ProcessId, state.ProcessName, mustBeVisible: false);
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            return false;

        var placement = state.Placement;
        if (placement.showCmd == NativeConstants.SW_HIDE || 
            placement.showCmd == NativeConstants.SW_SHOWMINIMIZED || 
            placement.showCmd == NativeConstants.SW_MINIMIZE)
        {
            placement.showCmd = NativeConstants.SW_SHOWNORMAL;
        }

        NativeMethods.SetWindowPlacement(hwnd, ref placement);

        int showCmd = placement.showCmd != 0 ? placement.showCmd : NativeConstants.SW_SHOWNORMAL;
        NativeMethods.ShowWindow(hwnd, showCmd);

        BringToForeground(hwnd);
        return true;
    }

    public static void EmergencyShowWindow(nint hwnd)
    {
        if (hwnd != 0 && NativeMethods.IsWindow(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_SHOW);
            NativeMethods.SetWindowPos(hwnd, 0, 0, 0, 0, 0,
                NativeConstants.SWP_SHOWWINDOW | NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOZORDER | NativeConstants.SWP_NOACTIVATE);
            BringToForeground(hwnd);
        }
    }

    public static bool BringToForeground(nint hwnd)
    {
        if (hwnd == 0 || !NativeMethods.IsWindow(hwnd))
            return false;

        if (NativeMethods.IsIconic(hwnd))
        {
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_RESTORE);
        }
        else
        {
            NativeMethods.ShowWindow(hwnd, NativeConstants.SW_SHOW);
        }

        NativeMethods.AllowSetForegroundWindow(NativeConstants.ASFW_ANY);

        nint foregroundHwnd = NativeMethods.GetForegroundWindow();
        if (foregroundHwnd == hwnd)
            return true;

        // Simulate Alt press to bypass Windows 10/11 foreground lock
        NativeMethods.keybd_event(NativeConstants.VK_MENU, 0, 0, 0);
        NativeMethods.keybd_event(NativeConstants.VK_MENU, 0, NativeConstants.KEYEVENTF_KEYUP, 0);

        uint currentThread = NativeMethods.GetCurrentThreadId();
        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out _);

        bool attached = false;
        if (foregroundThread != 0 && currentThread != foregroundThread)
        {
            attached = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        }

        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            bool success = NativeMethods.SetForegroundWindow(hwnd);
            if (!success)
            {
                NativeMethods.SwitchToThisWindow(hwnd, true);
            }
            return true;
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}