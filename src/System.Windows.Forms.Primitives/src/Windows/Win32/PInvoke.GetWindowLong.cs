// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static nint GetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex)
        => PlatformApi.Window.GetWindowLong(hWnd, nIndex);

    /// <inheritdoc cref="GetWindowLong(HWND, WINDOW_LONG_PTR_INDEX)"/>
    public static nint GetWindowLong<T>(T hWnd, WINDOW_LONG_PTR_INDEX nIndex) where T : IHandle<HWND>
    {
        nint result = GetWindowLong(hWnd.Handle, nIndex);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}