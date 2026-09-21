using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowDecoy.Interop;

namespace WindowDecoy.Services;

public static class IconService
{
    public static ImageSource? GetWindowIcon(nint hwnd, string? fallbackFilePath = null)
    {
        if (hwnd != 0)
        {
            try
            {
                nint hIcon = 0;
                // Try WM_GETICON (small2, small, big) with timeout to prevent hanging
                NativeMethods.SendMessageTimeout(hwnd, NativeConstants.WM_GETICON, NativeConstants.ICON_SMALL2, 0, NativeConstants.SMTO_ABORTIFHUNG, 50, out hIcon);
                if (hIcon == 0)
                {
                    NativeMethods.SendMessageTimeout(hwnd, NativeConstants.WM_GETICON, NativeConstants.ICON_SMALL, 0, NativeConstants.SMTO_ABORTIFHUNG, 50, out hIcon);
                }
                if (hIcon == 0)
                {
                    NativeMethods.SendMessageTimeout(hwnd, NativeConstants.WM_GETICON, NativeConstants.ICON_BIG, 0, NativeConstants.SMTO_ABORTIFHUNG, 50, out hIcon);
                }
                if (hIcon == 0)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hwnd, NativeConstants.GCLP_HICONSM);
                }
                if (hIcon == 0)
                {
                    hIcon = NativeMethods.GetClassLongPtr(hwnd, NativeConstants.GCLP_HICON);
                }

                if (hIcon != 0)
                {
                    var img = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    img.Freeze();
                    return img;
                }
            }
            catch
            {
                // Ignore icon read error from HWND
            }
        }

        if (!string.IsNullOrEmpty(fallbackFilePath))
        {
            return ExtractIconFromFile(fallbackFilePath);
        }

        return null;
    }

    public static ImageSource? ExtractIconFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            if (!File.Exists(filePath))
                return null;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".ico")
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }

            if (ext == ".exe" || ext == ".dll")
            {
                nint hLarge = 0;
                nint hSmall = 0;
                uint count = NativeMethods.ExtractIconEx(filePath, 0, out hLarge, out hSmall, 1);

                nint iconHandle = hSmall != 0 ? hSmall : hLarge;
                if (iconHandle != 0)
                {
                    try
                    {
                        var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                            iconHandle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());

                        imageSource.Freeze();
                        return imageSource;
                    }
                    finally
                    {
                        if (hLarge != 0) NativeMethods.DestroyIcon(hLarge);
                        if (hSmall != 0 && hSmall != hLarge) NativeMethods.DestroyIcon(hSmall);
                    }
                }
            }
        }
        catch
        {
            // Silently handle icon extraction errors
        }

        return null;
    }

    public static nint GetHIconFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return 0;

        try
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".exe" || ext == ".dll")
            {
                nint hLarge = 0;
                nint hSmall = 0;
                NativeMethods.ExtractIconEx(filePath, 0, out hLarge, out hSmall, 1);
                if (hLarge != 0)
                {
                    if (hSmall != 0) NativeMethods.DestroyIcon(hSmall);
                    return hLarge;
                }
                return hSmall;
            }

            if (ext == ".ico")
            {
                return LoadIconFromIcoFile(filePath);
            }
        }
        catch
        {
            // Ignore
        }

        return 0;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern nint LoadImage(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;

    private static nint LoadIconFromIcoFile(string path)
    {
        return LoadImage(0, path, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
    }
}
