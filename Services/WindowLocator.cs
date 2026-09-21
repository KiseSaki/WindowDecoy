using System;
using System.Diagnostics;
using System.Text;
using WindowDecoy.Interop;

namespace WindowDecoy.Services;

public static class WindowLocator
{
    public static nint FindTargetWindow(nint knownHwnd, uint targetPid, string targetProcessName, bool mustBeVisible = false)
    {
        // 1. Check if known HWND is still valid and belongs to the same process
        if (knownHwnd != 0 && NativeMethods.IsWindow(knownHwnd))
        {
            NativeMethods.GetWindowThreadProcessId(knownHwnd, out uint pid);
            if (pid == targetPid)
            {
                return knownHwnd;
            }
        }

        // 2. Search windows by PID
        if (targetPid != 0)
        {
            nint foundHwnd = FindLargestWindowByPid(targetPid, mustBeVisible);
            if (foundHwnd != 0)
                return foundHwnd;
        }

        // 3. Search by process name if process restarted with a new PID
        if (!string.IsNullOrWhiteSpace(targetProcessName))
        {
            try
            {
                var processes = Process.GetProcessesByName(targetProcessName);
                foreach (var proc in processes)
                {
                    try
                    {
                        nint hwnd = FindLargestWindowByPid((uint)proc.Id, mustBeVisible);
                        if (hwnd != 0)
                            return hwnd;
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        return 0;
    }

    private static nint FindLargestWindowByPid(uint pid, bool mustBeVisible)
    {
        nint bestHwnd = 0;
        long maxArea = 0;

        NativeMethods.EnumWindows((hwnd, lParam) =>
        {
            try
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowPid);
                if (windowPid != pid)
                    return true;

                if (mustBeVisible && !NativeMethods.IsWindowVisible(hwnd))
                    return true;

                int len = NativeMethods.GetWindowTextLength(hwnd);
                if (len <= 0)
                    return true;

                if (NativeMethods.GetWindowRect(hwnd, out RECT rect))
                {
                    long area = (long)rect.Width * rect.Height;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        bestHwnd = hwnd;
                    }
                }
            }
            catch { }

            return true;
        }, 0);

        return bestHwnd;
    }
}