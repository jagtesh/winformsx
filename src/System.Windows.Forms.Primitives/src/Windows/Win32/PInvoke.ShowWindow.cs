// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL ShowWindow(HWND hWnd, SHOW_WINDOW_CMD nCmdShow)
        => PlatformApi.Window.ShowWindow(hWnd, nCmdShow);

    /// <inheritdoc cref="ShowWindow(HWND, SHOW_WINDOW_CMD)"/>
    public static BOOL ShowWindow<T>(T hWnd, SHOW_WINDOW_CMD nCmdShow) where T : IHandle<HWND>
    {
        BOOL result = ShowWindow(hWnd.Handle, nCmdShow);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}