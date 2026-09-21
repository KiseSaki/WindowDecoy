using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowDecoy.Models;
using WindowDecoy.Services;

namespace WindowDecoy.Views;

public partial class ProcessPickerWindow : Window
{
    private List<WindowInfo> _allWindows = new();

    public WindowInfo? SelectedWindow { get; private set; }

    public ProcessPickerWindow()
    {
        InitializeComponent();
        Loaded += (s, e) => RefreshWindowList();
    }

    private void RefreshWindowList()
    {
        try
        {
            _allWindows = WindowEnumerator.GetTopLevelWindows();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"刷新窗口列表时出错:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyFilter()
    {
        try
        {
            string query = SearchTextBox?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                WindowsListBox.ItemsSource = _allWindows;
            }
            else
            {
                WindowsListBox.ItemsSource = _allWindows.Where(w =>
                    w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    w.ProcessId.ToString().Contains(query)
                ).ToList();
            }
        }
        catch
        {
            // Ignore filter errors
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void WindowsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ConfirmSelection();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        if (WindowsListBox.SelectedItem is WindowInfo selected)
        {
            SelectedWindow = selected;
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("请先从列表中选择一个窗口！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
