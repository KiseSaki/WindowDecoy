using System;

namespace WindowDecoy.Models;

public class DecoyProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "新伪装配置";
    public string TargetProcessName { get; set; } = string.Empty;
    public uint TargetProcessId { get; set; }
    public string FakeTitle { get; set; } = "UserMapper.java - 小程序 - Visual Studio Code";
    public string FakeIconPath { get; set; } = string.Empty;
    public string FakeScreenshotPath { get; set; } = string.Empty;
    public WindowSizeMode SizeMode { get; set; } = WindowSizeMode.TargetWindow;
    public WindowPositionMode PositionMode { get; set; } = WindowPositionMode.TargetWindow;
    public double CustomWidth { get; set; } = 1280;
    public double CustomHeight { get; set; } = 720;
    public double CustomX { get; set; } = 100;
    public double CustomY { get; set; } = 100;
    public DecoyBehaviorMode BehaviorMode { get; set; } = DecoyBehaviorMode.Proxy;
    public bool AutoDisguiseOnLostFocus { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+Q";
    public string RestoreHotkey { get; set; } = "Ctrl+Alt+`";
}