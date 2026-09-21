namespace WindowDecoy.Interop;

public static class NativeConstants
{
    // ShowWindow Commands
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const int SW_SHOWNA = 8;
    public const int SW_RESTORE = 9;

    // SetWindowPos Flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;

    // Window Messages
    public const int WM_ACTIVATE = 0x0006;
    public const int WM_SETFOCUS = 0x0007;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int WM_CLOSE = 0x0010;
    public const int WM_GETICON = 0x007F;
    public const int WM_SETICON = 0x0080;
    public const int WM_HOTKEY = 0x0312;

    // WM_ACTIVATE States
    public const int WA_INACTIVE = 0;
    public const int WA_ACTIVE = 1;
    public const int WA_CLICKACTIVE = 2;

    // Foreground Permission
    public const uint ASFW_ANY = 0xFFFFFFFF;

    // Virtual Keys and Flags
    public const byte VK_MENU = 0x12; // Alt key
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // WinEvent Constants
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    // WM_SETICON Flags
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;

    // Class Long Indices
    public const int GCLP_HICON = -14;
    public const int GCLP_HICONSM = -34;

    // SendMessageTimeout Flags
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // Hotkey Modifiers
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Window Long Indices
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;

    // Window Styles
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_MINIMIZE = 0x20000000;
    public const uint WS_MAXIMIZE = 0x01000000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_APPWINDOW = 0x00040000;

    // Monitor Constants
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    // System Parameters
    public const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;

    // Shell_NotifyIcon Messages
    public const uint NIM_ADD = 0x00000000;
    public const uint NIM_MODIFY = 0x00000001;
    public const uint NIM_DELETE = 0x00000002;
    public const uint NIM_SETVERSION = 0x00000004;

    // NOTIFYICONDATA Flags
    public const uint NIF_MESSAGE = 0x00000001;
    public const uint NIF_ICON = 0x00000002;
    public const uint NIF_TIP = 0x00000004;
    public const uint NIF_STATE = 0x00000008;
    public const uint NIF_INFO = 0x00000010;
    public const uint NIF_GUID = 0x00000020;
    public const uint NIF_SHOWTIP = 0x00000040;

    // Balloon Flags
    public const uint NIIF_NONE = 0x00000000;
    public const uint NIIF_INFO = 0x00000001;
    public const uint NIIF_WARNING = 0x00000002;
    public const uint NIIF_ERROR = 0x00000003;

    // Tray Messages
    public const uint WM_TRAYICON = 0x8000 + 101; // WM_APP + 101
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_CONTEXTMENU = 0x007B;
    public const int NIN_BALLOONUSERCLICK = 0x0405;

    // Win32 Menu Flags
    public const uint MF_STRING = 0x00000000;
    public const uint MF_SEPARATOR = 0x00000800;
    public const uint MF_GRAYED = 0x00000001;
    public const uint MF_DISABLED = 0x00000002;

    public const uint TPM_RETURNCMD = 0x0100;
    public const uint TPM_RIGHTBUTTON = 0x0002;
    public const uint TPM_LEFTALIGN = 0x0000;

    // Standard Icons
    public const int IDI_APPLICATION = 32512;
    public const int IDI_SHIELD = 32518;
}