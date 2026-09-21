using System;
using System.IO;
using System.Runtime.InteropServices;
using WindowDecoy.Interop;

namespace WindowDecoy.Services;

public class TrayIconService : IDisposable
{
    private const uint TRAY_ICON_ID = 1;
    private const uint MENU_SHOW = 1001;
    private const uint MENU_RESTORE_ALL = 1002;
    private const uint MENU_EXIT = 1003;

    private readonly nint _hwnd;
    private readonly Action _onShowRequested;
    private readonly Action _onRestoreAllRequested;
    private readonly Action _onExitRequested;
    private readonly uint _taskbarCreatedMsg;

    private nint _hIcon;
    private bool _isCustomIcon;
    private bool _isIconAdded;
    private bool _disposed;

    public TrayIconService(
        nint hwnd,
        Action onShowRequested,
        Action onRestoreAllRequested,
        Action onExitRequested)
    {
        _hwnd = hwnd;
        _onShowRequested = onShowRequested;
        _onRestoreAllRequested = onRestoreAllRequested;
        _onExitRequested = onExitRequested;

        _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");
        LoadIcon();
        AddTrayIcon();
    }

    private void LoadIcon()
    {
        try
        {
            // 1. Try Assets/app.ico
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(appDir, "Assets", "app.ico");
            if (File.Exists(icoPath))
            {
                _hIcon = IconService.GetHIconFromFile(icoPath);
                if (_hIcon != 0)
                {
                    _isCustomIcon = true;
                    return;
                }
            }

            // 2. Try process executable
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                NativeMethods.ExtractIconEx(exePath, 0, out _, out _hIcon, 1);
                if (_hIcon != 0)
                {
                    _isCustomIcon = true;
                    return;
                }
            }

            // 3. Fallback to system shield or application icon
            _hIcon = NativeMethods.LoadIcon(0, (nint)NativeConstants.IDI_SHIELD);
            if (_hIcon == 0)
            {
                _hIcon = NativeMethods.LoadIcon(0, (nint)NativeConstants.IDI_APPLICATION);
            }
        }
        catch
        {
            _hIcon = NativeMethods.LoadIcon(0, (nint)NativeConstants.IDI_APPLICATION);
        }
    }

    public void AddTrayIcon()
    {
        if (_hwnd == 0)
            return;

        var nid = NOTIFYICONDATA.Create();
        nid.hWnd = _hwnd;
        nid.uID = TRAY_ICON_ID;
        nid.uFlags = NativeConstants.NIF_MESSAGE | NativeConstants.NIF_ICON | NativeConstants.NIF_TIP;
        nid.uCallbackMessage = NativeConstants.WM_TRAYICON;
        nid.hIcon = _hIcon;
        nid.szTip = "WindowDecoy - 隐形替身伪装 (后台运行中)";

        _isIconAdded = NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_ADD, ref nid);
    }

    public void RemoveTrayIcon()
    {
        if (!_isIconAdded || _hwnd == 0)
            return;

        var nid = NOTIFYICONDATA.Create();
        nid.hWnd = _hwnd;
        nid.uID = TRAY_ICON_ID;
        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_DELETE, ref nid);
        _isIconAdded = false;
    }

    public void ShowBalloonNotification(string title, string text)
    {
        if (!_isIconAdded || _hwnd == 0)
            return;

        var nid = NOTIFYICONDATA.Create();
        nid.hWnd = _hwnd;
        nid.uID = TRAY_ICON_ID;
        nid.uFlags = NativeConstants.NIF_INFO;
        nid.szInfoTitle = title;
        nid.szInfo = text;
        nid.dwInfoFlags = NativeConstants.NIIF_INFO;

        NativeMethods.Shell_NotifyIcon(NativeConstants.NIM_MODIFY, ref nid);
    }

    public bool HandleMessage(nint hwnd, int msg, nint wParam, nint lParam)
    {
        if (msg == _taskbarCreatedMsg)
        {
            AddTrayIcon();
            return true;
        }

        if (msg == (int)NativeConstants.WM_TRAYICON)
        {
            int eventType = (int)lParam;
            switch (eventType)
            {
                case NativeConstants.WM_LBUTTONUP:
                case NativeConstants.WM_LBUTTONDBLCLK:
                case NativeConstants.NIN_BALLOONUSERCLICK:
                    _onShowRequested();
                    return true;

                case NativeConstants.WM_RBUTTONUP:
                case NativeConstants.WM_CONTEXTMENU:
                    ShowContextMenu();
                    return true;
            }
        }

        return false;
    }

    private void ShowContextMenu()
    {
        nint hMenu = NativeMethods.CreatePopupMenu();
        if (hMenu == 0)
            return;

        try
        {
            NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, (nint)MENU_SHOW, "📌 显示主界面");
            NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, (nint)MENU_RESTORE_ALL, "🔄 一键全恢复 (Ctrl+Alt+`)");
            NativeMethods.AppendMenu(hMenu, NativeConstants.MF_SEPARATOR, 0, string.Empty);
            NativeMethods.AppendMenu(hMenu, NativeConstants.MF_STRING, (nint)MENU_EXIT, "❌ 退出 WindowDecoy");

            NativeMethods.SetForegroundWindow(_hwnd);
            NativeMethods.GetCursorPos(out POINT pt);

            uint cmd = NativeMethods.TrackPopupMenuEx(
                hMenu,
                NativeConstants.TPM_RETURNCMD | NativeConstants.TPM_RIGHTBUTTON,
                pt.X,
                pt.Y,
                _hwnd,
                0);

            // Per Microsoft documentation (KB135788), post WM_NULL to force task switch / menu dismiss
            NativeMethods.PostMessage(_hwnd, 0, 0, 0);

            if (cmd == MENU_SHOW)
            {
                _onShowRequested();
            }
            else if (cmd == MENU_RESTORE_ALL)
            {
                _onRestoreAllRequested();
            }
            else if (cmd == MENU_EXIT)
            {
                _onExitRequested();
            }
        }
        finally
        {
            NativeMethods.DestroyMenu(hMenu);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        RemoveTrayIcon();

        if (_isCustomIcon && _hIcon != 0)
        {
            try
            {
                NativeMethods.DestroyIcon(_hIcon);
            }
            catch
            {
                // Ignore
            }
            _hIcon = 0;
        }
    }
}
