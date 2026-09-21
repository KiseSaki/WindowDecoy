using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using WindowDecoy.Interop;
using WindowDecoy.Services;
using WindowDecoy.ViewModels;

namespace WindowDecoy.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private TrayIconService? _trayService;
    private bool _isExplicitExit = false;
    private bool _hasShownTrayNotice = false;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.RequestHideToTray += HideToTray;
        DataContext = _viewModel;

        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        nint hwnd = new WindowInteropHelper(this).Handle;
        _viewModel.InitializeHotkeys(hwnd);

        _trayService = new TrayIconService(
            hwnd: hwnd,
            onShowRequested: ShowAndActivate,
            onRestoreAllRequested: () => _viewModel.RestoreAllCommand.Execute(null),
            onExitRequested: ExitApplication);

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_trayService != null && _trayService.HandleMessage(hwnd, msg, wParam, lParam))
        {
            handled = true;
            return 0;
        }
        return 0;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            HideToTray();
            _viewModel.SaveProfiles();
            return;
        }

        _viewModel.SaveProfiles();
        _viewModel.Cleanup();
        _trayService?.Dispose();
    }

    public void HideToTray()
    {
        Hide();
        if (!_hasShownTrayNotice)
        {
            _hasShownTrayNotice = true;
            _trayService?.ShowBalloonNotification(
                "WindowDecoy 已进入后台运行",
                "程序已最小化到系统托盘，所有伪装及热键持续生效中。双击托盘图标即可唤出主界面。");
        }
    }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        nint hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public void ExitApplication()
    {
        _isExplicitExit = true;
        Close();
        Application.Current.Shutdown();
    }
}

