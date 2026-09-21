using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WindowDecoy.Models;
using WindowDecoy.Services;
using WindowDecoy.Views;

namespace WindowDecoy.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly DecoyManager _decoyManager;
    private DecoySession? _selectedSession;
    private HotkeyManager? _hotkeyManager;
    private nint _mainWindowHwnd;

    private string _masterActivateHotkey = "Ctrl+Alt+Q";
    private string _masterRestoreHotkey = "Ctrl+Alt+`";
    private string _statusSummary = "就绪";

    public DecoyManager DecoyManager => _decoyManager;
    public ObservableCollection<DecoySession> Sessions => _decoyManager.Sessions;

    public DecoySession? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                OnPropertyChanged(nameof(FakeIconPreview));
                OnPropertyChanged(nameof(FakeScreenshotPreview));
                OnPropertyChanged(nameof(HasSelectedSession));
                UpdateCommandStates();
            }
        }
    }

    public bool HasSelectedSession => SelectedSession != null;

    public string MasterActivateHotkey
    {
        get => _masterActivateHotkey;
        set
        {
            if (SetProperty(ref _masterActivateHotkey, value))
            {
                RebindHotkeys();
            }
        }
    }

    public string MasterRestoreHotkey
    {
        get => _masterRestoreHotkey;
        set
        {
            if (SetProperty(ref _masterRestoreHotkey, value))
            {
                RebindHotkeys();
            }
        }
    }

    public string StatusSummary
    {
        get => _statusSummary;
        private set => SetProperty(ref _statusSummary, value);
    }

    private bool _minimizeToTrayOnClose = true;
    public bool MinimizeToTrayOnClose
    {
        get => _minimizeToTrayOnClose;
        set => SetProperty(ref _minimizeToTrayOnClose, value);
    }

    public event Action? RequestHideToTray;

    public ImageSource? FakeIconPreview
    {
        get
        {
            if (SelectedSession != null && !string.IsNullOrWhiteSpace(SelectedSession.Profile.FakeIconPath))
            {
                return IconService.ExtractIconFromFile(SelectedSession.Profile.FakeIconPath);
            }
            return null;
        }
    }

    public ImageSource? FakeScreenshotPreview
    {
        get
        {
            if (SelectedSession != null && !string.IsNullOrWhiteSpace(SelectedSession.Profile.FakeScreenshotPath) && File.Exists(SelectedSession.Profile.FakeScreenshotPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(SelectedSession.Profile.FakeScreenshotPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 320;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }

    // Commands
    public ICommand AddSessionCommand { get; }
    public ICommand RemoveSessionCommand { get; }
    public ICommand PickProcessCommand { get; }
    public ICommand BrowseIconCommand { get; }
    public ICommand BrowseScreenshotCommand { get; }
    public ICommand ActivateSelectedCommand { get; }
    public ICommand RestoreSelectedCommand { get; }
    public ICommand ActivateAllCommand { get; }
    public ICommand RestoreAllCommand { get; }
    public ICommand SaveProfilesCommand { get; }
    public ICommand RestartAsAdminCommand { get; }
    public ICommand MinimizeToTrayCommand { get; }

    public bool IsElevated { get; } = CheckElevation();
    public bool IsNotElevated => !IsElevated;

    private static bool CheckElevation()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void RestartAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(psi);
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"以管理员身份重启失败:\n{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public MainViewModel()
    {
        _decoyManager = new DecoyManager();
        _decoyManager.SessionStateChanged += _ => UpdateStatusSummary();

        AddSessionCommand = new RelayCommand(AddSession);
        RemoveSessionCommand = new RelayCommand(RemoveSelectedSession, () => SelectedSession != null);
        PickProcessCommand = new RelayCommand(PickProcess, () => SelectedSession != null);
        BrowseIconCommand = new RelayCommand(BrowseIcon, () => SelectedSession != null);
        BrowseScreenshotCommand = new RelayCommand(BrowseScreenshot, () => SelectedSession != null);
        ActivateSelectedCommand = new RelayCommand(ActivateSelected, () => SelectedSession != null && SelectedSession.CanActivate && SelectedSession.TargetHwnd != 0);
        RestoreSelectedCommand = new RelayCommand(RestoreSelected, () => SelectedSession != null && SelectedSession.CanRestore);
        ActivateAllCommand = new RelayCommand(() => _decoyManager.ActivateAll());
        RestoreAllCommand = new RelayCommand(() => _decoyManager.RestoreAll());
        SaveProfilesCommand = new RelayCommand(SaveProfiles);
        RestartAsAdminCommand = new RelayCommand(RestartAsAdmin);
        MinimizeToTrayCommand = new RelayCommand(() => RequestHideToTray?.Invoke());

        LoadProfiles();
    }

    public void InitializeHotkeys(nint hwnd)
    {
        _mainWindowHwnd = hwnd;
        RebindHotkeys();
    }

    public void RebindHotkeys()
    {
        if (_mainWindowHwnd == 0)
            return;

        _hotkeyManager?.Dispose();
        _hotkeyManager = new HotkeyManager(_mainWindowHwnd);

        // 1. Master hotkeys
        if (!string.IsNullOrWhiteSpace(MasterActivateHotkey))
        {
            _hotkeyManager.Register(MasterActivateHotkey, () =>
            {
                Application.Current.Dispatcher.Invoke(() => _decoyManager.ActivateAll());
            });
        }

        if (!string.IsNullOrWhiteSpace(MasterRestoreHotkey))
        {
            _hotkeyManager.Register(MasterRestoreHotkey, () =>
            {
                Application.Current.Dispatcher.Invoke(() => _decoyManager.RestoreAll());
            });
        }

        // 2. Individual session hotkeys
        foreach (var session in Sessions)
        {
            var s = session;
            if (!string.IsNullOrWhiteSpace(s.Profile.Hotkey))
            {
                _hotkeyManager.Register(s.Profile.Hotkey, () =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (s.CanActivate && s.TargetHwnd != 0)
                            _decoyManager.ActivateSession(s);
                        else if (s.CanRestore)
                            _decoyManager.RestoreSession(s);
                    });
                });
            }

            if (!string.IsNullOrWhiteSpace(s.Profile.RestoreHotkey) && s.Profile.RestoreHotkey != s.Profile.Hotkey)
            {
                _hotkeyManager.Register(s.Profile.RestoreHotkey, () =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (s.CanRestore)
                            _decoyManager.RestoreSession(s);
                    });
                });
            }
        }
    }

    private void AddSession()
    {
        var session = new DecoySession
        {
            Profile = new DecoyProfile
            {
                Name = $"任务 #{Sessions.Count + 1}",
                FakeTitle = "UserMapper.java - 小程序 - Visual Studio Code",
                Hotkey = "",
                RestoreHotkey = ""
            }
        };

        Sessions.Add(session);
        SelectedSession = session;
        UpdateStatusSummary();
        SaveProfiles();
    }

    private void RemoveSelectedSession()
    {
        if (SelectedSession == null)
            return;

        if (SelectedSession.IsActive)
        {
            _decoyManager.RestoreSession(SelectedSession);
        }

        var toRemove = SelectedSession;
        int index = Sessions.IndexOf(toRemove);
        Sessions.Remove(toRemove);

        if (Sessions.Count > 0)
        {
            SelectedSession = Sessions[Math.Clamp(index, 0, Sessions.Count - 1)];
        }
        else
        {
            SelectedSession = null;
        }

        UpdateStatusSummary();
        SaveProfiles();
        RebindHotkeys();
    }

    private void PickProcess()
    {
        if (SelectedSession == null)
            return;

        var picker = new ProcessPickerWindow
        {
            Owner = Application.Current.MainWindow
        };

        if (picker.ShowDialog() == true && picker.SelectedWindow != null)
        {
            var win = picker.SelectedWindow;
            SelectedSession.TargetHwnd = win.Hwnd;
            SelectedSession.TargetProcessId = win.ProcessId;
            SelectedSession.TargetProcessName = win.ProcessName;
            SelectedSession.TargetTitle = win.Title;
            SelectedSession.TargetIcon = win.Icon;
            SelectedSession.Profile.TargetProcessName = win.ProcessName;
            SelectedSession.Profile.TargetProcessId = win.ProcessId;
            SelectedSession.Profile.Name = $"{win.ProcessName} 伪装";
            SelectedSession.State = DecoyState.TargetSelected;

            UpdateStatusSummary();
            UpdateCommandStates();
            SaveProfiles();
        }
    }

    private void BrowseIcon()
    {
        if (SelectedSession == null) return;

        var dlg = new OpenFileDialog
        {
            Title = "选择假图标文件 (.ico, .exe, .dll)",
            Filter = "图标或可执行文件 (*.ico;*.exe;*.dll)|*.ico;*.exe;*.dll|所有文件 (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            SelectedSession.Profile.FakeIconPath = dlg.FileName;
            OnPropertyChanged(nameof(FakeIconPreview));
            SaveProfiles();
        }
    }

    private void BrowseScreenshotCommandExecute()
    {
        BrowseScreenshot();
    }

    private void BrowseScreenshot()
    {
        if (SelectedSession == null) return;

        var dlg = new OpenFileDialog
        {
            Title = "选择假工作界面截图",
            Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|所有文件 (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            SelectedSession.Profile.FakeScreenshotPath = dlg.FileName;
            OnPropertyChanged(nameof(FakeScreenshotPreview));
            SaveProfiles();
        }
    }

    private void ActivateSelected()
    {
        if (SelectedSession != null && SelectedSession.CanActivate)
        {
            _decoyManager.ActivateSession(SelectedSession);
            UpdateCommandStates();
        }
    }

    private void RestoreSelected()
    {
        if (SelectedSession != null && SelectedSession.CanRestore)
        {
            _decoyManager.RestoreSession(SelectedSession);
            UpdateCommandStates();
        }
    }

    public void NotifyProfilePropertyChanged()
    {
        OnPropertyChanged(nameof(FakeIconPreview));
        OnPropertyChanged(nameof(FakeScreenshotPreview));
        SaveProfiles();
        RebindHotkeys();
    }

    private void UpdateCommandStates()
    {
        (ActivateSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestoreSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveSessionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PickProcessCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BrowseIconCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BrowseScreenshotCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateStatusSummary()
    {
        int total = Sessions.Count;
        int active = Sessions.Count(s => s.IsActive);
        StatusSummary = $"共 {total} 个任务，{active} 个伪装运行中";
        UpdateCommandStates();
    }

    public void SaveProfiles()
    {
        var profiles = Sessions.Select(s => s.Profile).ToList();
        ProfileService.SaveProfiles(profiles);
    }

    private void LoadProfiles()
    {
        var profiles = ProfileService.LoadProfiles();
        Sessions.Clear();
        foreach (var profile in profiles)
        {
            var session = new DecoySession
            {
                Profile = profile,
                TargetProcessName = profile.TargetProcessName,
                TargetProcessId = profile.TargetProcessId
            };

            // Try to auto-locate window if process is currently running
            if (!string.IsNullOrEmpty(session.TargetProcessName))
            {
                nint hwnd = WindowLocator.FindTargetWindow(0, session.TargetProcessId, session.TargetProcessName);
                if (hwnd != 0)
                {
                    session.TargetHwnd = hwnd;
                    session.State = DecoyState.TargetSelected;
                }
            }

            Sessions.Add(session);
        }

        if (Sessions.Count > 0)
        {
            SelectedSession = Sessions[0];
        }

        UpdateStatusSummary();
    }

    public void Cleanup()
    {
        _hotkeyManager?.Dispose();
        _decoyManager.Dispose();
        _decoyManager.EmergencyRestoreAll();
    }
}

