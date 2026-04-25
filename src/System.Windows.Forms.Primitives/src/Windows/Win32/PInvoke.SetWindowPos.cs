// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        => PlatformApi.Window.SetWindowPos(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);

    /// <inheritdoc cref="SetWindowPos(HWND, HWND, int, int, int, int, SET_WINDOW_POS_FLAGS)"/>
    public static BOOL SetWindowPos<T>(T hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
        where T : IHandle<HWND>
    {
        BOOL result = SetWindowPos(hWnd.Handle, hWndInsertAfter, X, Y, cx, cy, uFlags);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}