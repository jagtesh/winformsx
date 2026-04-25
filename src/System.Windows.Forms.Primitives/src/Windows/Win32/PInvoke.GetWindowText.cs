// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe int GetWindowText(HWND hWnd, char* lpString, int nMaxCount)
    {
        var span = new global::System.Span<char>(lpString, nMaxCount);
        return PlatformApi.Window.GetWindowText(hWnd, span);
    }

    public static unsafe int GetWindowText<T>(T hWnd, char* lpString, int nMaxCount) where T : IHandle<HWND>
    { int r = GetWindowText(hWnd.Handle, lpString, nMaxCount); GC.KeepAlive(hWnd.Wrapper); return r; }

    public static int GetWindowText(HWND hWnd, global::System.Span<char> buffer)
        => PlatformApi.Window.GetWindowText(hWnd, buffer);

    public static string GetWindowText(HWND hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;
        global::System.Span<char> buffer = length <= 256 ? stackalloc char[length + 1] : new char[length + 1];
        int count = PlatformApi.Window.GetWindowText(hWnd, buffer);
        return buffer[..count].ToString();
    }

    public static string GetWindowText<T>(T hWnd) where T : IHandle<HWND>
    {
        string r = GetWindowText(hWnd.Handle);
        GC.KeepAlive(hWnd.Wrapper);
        return r;
    }
}