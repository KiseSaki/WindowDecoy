using System.Windows.Media;
using WindowDecoy.Interop;

namespace WindowDecoy.Models;

public class WindowInfo
{
    public nint Hwnd { get; set; }
    public uint ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public RECT Rect { get; set; }
    public ImageSource? Icon { get; set; }

    public string DisplayText => $"{ProcessName}.exe (PID: {ProcessId}) - {Title}";

    public override string ToString() => DisplayText;
}

