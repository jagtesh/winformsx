// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="SetScrollInfo(HWND, SCROLLBAR_CONSTANTS, in SCROLLINFO, BOOL)"/>
    public static int SetScrollInfo<T>(T hWnd, SCROLLBAR_CONSTANTS nBar, ref SCROLLINFO lpsi, BOOL redraw)
        where T : IHandle<HWND>
    {
        _ = redraw;
        int result = SetSyntheticScrollInfo(hWnd.Handle, nBar, lpsi);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}
