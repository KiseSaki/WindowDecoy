using System;
using System.Windows.Media;
using WindowDecoy.ViewModels;
using WindowDecoy.Views;

namespace WindowDecoy.Models;

public class DecoySession : ViewModelBase
{
    private DecoyProfile _profile = new();
    private nint _targetHwnd;
    private uint _targetProcessId;
    private string _targetProcessName = string.Empty;
    private string _targetTitle = string.Empty;
    private ImageSource? _targetIcon;
    private DecoyState _state = DecoyState.Idle;
    private SavedWindowState? _savedState;
    private DecoyWindow? _decoyWindow;
    private string _errorMessage = string.Empty;
    private DateTime _lastHandoverTime = DateTime.MinValue;

    public string Id => Profile.Id;

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public DecoyProfile Profile
    {
        get => _profile;
        set => SetProperty(ref _profile, value);
    }

    public nint TargetHwnd
    {
        get => _targetHwnd;
        set => SetProperty(ref _targetHwnd, value);
    }

    public uint TargetProcessId
    {
        get => _targetProcessId;
        set => SetProperty(ref _targetProcessId, value);
    }

    public string TargetProcessName
    {
        get => _targetProcessName;
        set => SetProperty(ref _targetProcessName, value);
    }

    public string TargetTitle
    {
        get => _targetTitle;
        set => SetProperty(ref _targetTitle, value);
    }

    public ImageSource? TargetIcon
    {
        get => _targetIcon;
        set => SetProperty(ref _targetIcon, value);
    }

    public DecoyState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(IsActive));
                OnPropertyChanged(nameof(CanActivate));
                OnPropertyChanged(nameof(CanRestore));
            }
        }
    }

    public SavedWindowState? SavedState
    {
        get => _savedState;
        set => SetProperty(ref _savedState, value);
    }

    public DecoyWindow? DecoyWindow
    {
        get => _decoyWindow;
        set => SetProperty(ref _decoyWindow, value);
    }

    public DateTime LastHandoverTime
    {
        get => _lastHandoverTime;
        set => SetProperty(ref _lastHandoverTime, value);
    }

    public bool IsActive => State == DecoyState.Active || State == DecoyState.TargetRevealed;
    public bool CanActivate => State == DecoyState.TargetSelected || State == DecoyState.Idle || State == DecoyState.Error;
    public bool CanRestore => State == DecoyState.Active || State == DecoyState.TargetRevealed;

    public string StatusText => State switch
    {
        DecoyState.Idle => "未绑定目标",
        DecoyState.TargetSelected => "待命中 (就绪)",
        DecoyState.Activating => "正在进入伪装...",
        DecoyState.Active => "伪装生效中 (替身呈现)",
        DecoyState.TargetRevealed => "原程序运行中 (失焦将自动切回替身)",
        DecoyState.Restoring => "正在还原...",
        DecoyState.Error => string.IsNullOrWhiteSpace(ErrorMessage) ? "异常 (点击重试)" : $"异常: {ErrorMessage}",
        _ => "未知"
    };

    public Brush StatusColor => State switch
    {
        DecoyState.Active => new SolidColorBrush(Color.FromRgb(34, 197, 94)),          // Green
        DecoyState.TargetRevealed => new SolidColorBrush(Color.FromRgb(14, 165, 233)),  // Cyan
        DecoyState.TargetSelected => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  // Blue
        DecoyState.Activating or DecoyState.Restoring => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
        DecoyState.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),            // Red
        _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))                          // Gray
    };
}