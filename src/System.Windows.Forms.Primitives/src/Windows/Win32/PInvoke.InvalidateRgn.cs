// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL InvalidateRgn(HWND hWnd, HRGN hRgn, BOOL bErase)
        => PlatformApi.Window.InvalidateRect(hWnd, null, bErase);

    public static BOOL InvalidateRgn<T>(T hWnd, HRGN hRgn, BOOL bErase) where T : IHandle<HWND>
    { BOOL r = InvalidateRgn(hWnd.Handle, hRgn, bErase); GC.KeepAlive(hWnd.Wrapper); return r; }
}