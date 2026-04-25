// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL UpdateWindow(HWND hWnd)
        => PlatformApi.Window.UpdateWindow(hWnd);

    /// <inheritdoc cref="UpdateWindow(HWND)"/>
    public static BOOL UpdateWindow<T>(T hWnd) where T : IHandle<HWND>
    {
        BOOL result = UpdateWindow(hWnd.Handle);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}