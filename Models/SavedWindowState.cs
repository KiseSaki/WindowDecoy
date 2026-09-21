using WindowDecoy.Interop;

namespace WindowDecoy.Models;

public class SavedWindowState
{
    public nint Hwnd { get; set; }
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public WINDOWPLACEMENT Placement { get; set; }
    public RECT WindowRect { get; set; }
    public nint MonitorHandle { get; set; }
    public nint OriginalStyle { get; set; }
    public nint OriginalExStyle { get; set; }
    public List<nint> AllHiddenHwnds { get; set; } = new();
}

