// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL InvalidateRect(HWND hWnd, RECT* lpRect, BOOL bErase)
        => PlatformApi.Window.InvalidateRect(hWnd, lpRect is not null ? *lpRect : null, bErase);

    public static unsafe BOOL InvalidateRect<T>(T hWnd, RECT* lpRect, BOOL bErase) where T : IHandle<HWND>
    { BOOL r = InvalidateRect(hWnd.Handle, lpRect, bErase); GC.KeepAlive(hWnd.Wrapper); return r; }

    public static BOOL InvalidateRect(HWND hWnd, BOOL bErase)
        => PlatformApi.Window.InvalidateRect(hWnd, null, bErase);
}