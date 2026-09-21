using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using WindowDecoy.Interop;
using WindowDecoy.Models;

namespace WindowDecoy.Services;

public static class WindowEnumerator
{
    public static List<WindowInfo> GetTopLevelWindows()
    {
        var result = new List<WindowInfo>();
        uint currentPid = (uint)Process.GetCurrentProcess().Id;

        try
        {
            NativeMethods.EnumWindows((hwnd, lParam) =>
            {
                try
                {
                    if (hwnd == 0 || !NativeMethods.IsWindowVisible(hwnd))
                        return true;

                    int length = NativeMethods.GetWindowTextLength(hwnd);
                    if (length <= 0 || length > 1024)
                        return true;

                    var sb = new StringBuilder(length + 2);
                    NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
                    string title = sb.ToString().Trim();
                    if (string.IsNullOrEmpty(title))
                        return true;

                    if (!NativeMethods.GetWindowRect(hwnd, out RECT rect))
                        return true;

                    if (rect.Width <= 0 || rect.Height <= 0)
                        return true;

                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == 0 || pid > int.MaxValue || pid == currentPid)
                        return true;

                    string processName = string.Empty;
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        processName = proc.ProcessName;
                    }
                    catch
                    {
                        // Process may have exited or cannot be opened
                        return true;
                    }

                    // Skip known desktop wallpaper / system surfaces
                    if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                        (title == "Program Manager" || title == "Windows 输入体验" || title == "Search"))
                    {
                        return true;
                    }

                    if (processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase))
                        return true;

                    var info = new WindowInfo
                    {
                        Hwnd = hwnd,
                        ProcessId = pid,
                        ProcessName = processName,
                        Title = title,
                        Rect = rect,
                        Icon = IconService.GetWindowIcon(hwnd)
                    };

                    result.Add(info);
                }
                catch
                {
                    // Prevent any exception from escaping across the native callback boundary
                }

                return true;
            }, 0);
        }
        catch
        {
            // Fallback gracefully
        }

        return result;
    }
}
