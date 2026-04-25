// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL GetWindowRect(HWND hWnd, out RECT lpRect)
        => PlatformApi.Window.GetWindowRect(hWnd, out lpRect);

    public static unsafe BOOL GetWindowRect(HWND hWnd, RECT* lpRect)
    {
        BOOL result = GetWindowRect(hWnd, out RECT rect);
        if (lpRect is not null) *lpRect = rect;
        return result;
    }

    public static BOOL GetWindowRect<T>(T hWnd, out RECT lpRect) where T : IHandle<HWND>
    { BOOL r = GetWindowRect(hWnd.Handle, out lpRect); GC.KeepAlive(hWnd.Wrapper); return r; }

    public static unsafe BOOL GetWindowRect<T>(T hWnd, RECT* lpRect) where T : IHandle<HWND>
    { BOOL r = GetWindowRect(hWnd.Handle, lpRect); GC.KeepAlive(hWnd.Wrapper); return r; }
}