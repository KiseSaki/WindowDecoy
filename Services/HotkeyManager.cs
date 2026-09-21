using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;
using WindowDecoy.Interop;

namespace WindowDecoy.Services;

public class HotkeyManager : IDisposable
{
    private readonly nint _windowHandle;
    private readonly HwndSource? _hwndSource;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _currentId = 9000;
    private bool _disposed;

    public HotkeyManager(nint windowHandle)
    {
        _windowHandle = windowHandle;
        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(HwndHook);
    }

    public int Register(string hotkeyString, Action handler)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return 0;

        if (!ParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            return 0;

        int id = ++_currentId;
        // MOD_NOREPEAT (0x4000) prevents repeated triggers while holding the key down
        bool success = NativeMethods.RegisterHotKey(_windowHandle, id, modifiers | NativeConstants.MOD_NOREPEAT, vk);
        if (!success)
        {
            // Retry without MOD_NOREPEAT in case of compatibility
            success = NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, vk);
        }

        if (success)
        {
            _handlers[id] = handler;
            return id;
        }

        return 0;
    }

    public void Unregister(int id)
    {
        if (id != 0 && _handlers.ContainsKey(id))
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
            _handlers.Remove(id);
        }
    }

    public void UnregisterAll()
    {
        foreach (int id in _handlers.Keys)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        }
        _handlers.Clear();
    }

    private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeConstants.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var action))
            {
                action?.Invoke();
                handled = true;
            }
        }
        return 0;
    }

    public static bool ParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = NativeConstants.MOD_NONE;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        string[] parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        string? keyPart = null;

        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeConstants.MOD_CONTROL;
            }
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeConstants.MOD_ALT;
            }
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeConstants.MOD_SHIFT;
            }
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= NativeConstants.MOD_WIN;
            }
            else
            {
                keyPart = part;
            }
        }

        if (string.IsNullOrEmpty(keyPart))
            return false;

        // Parse key
        if (keyPart == "`" || keyPart == "~")
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(Key.OemTilde);
        }
        else if (Enum.TryParse<Key>(keyPart, true, out var key))
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        }
        else if (keyPart.Length == 1)
        {
            char c = char.ToUpperInvariant(keyPart[0]);
            if (c >= 'A' && c <= 'Z')
                vk = (uint)c;
            else if (c >= '0' && c <= '9')
                vk = (uint)c;
        }

        return vk != 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            UnregisterAll();
            _hwndSource?.RemoveHook(HwndHook);
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

