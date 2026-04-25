// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Creates a window via the PAL.</summary>
    public static unsafe HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle,
        string? lpClassName,
        string? lpWindowName,
        WINDOW_STYLE dwStyle,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        HWND hWndParent,
        HMENU hMenu,
        HINSTANCE hInstance,
        object? lpParam)
    {
        return PlatformApi.Window.CreateWindowEx(
            dwExStyle, lpClassName, lpWindowName,
            dwStyle, X, Y, nWidth, nHeight,
            hWndParent, hMenu, hInstance, lpParam);
    }

    /// <summary>Low-level CreateWindowEx with void* param — routes through managed overload.</summary>
    public static unsafe HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle,
        PCWSTR lpClassName,
        PCWSTR lpWindowName,
        WINDOW_STYLE dwStyle,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        HWND hWndParent,
        HMENU hMenu,
        HINSTANCE hInstance,
        void* lpParam)
    {
        string? className = lpClassName.ToString();
        string? windowName = lpWindowName.ToString();
        return PlatformApi.Window.CreateWindowEx(
            dwExStyle, className, windowName,
            dwStyle, X, Y, nWidth, nHeight,
            hWndParent, hMenu, hInstance, null);
    }
}