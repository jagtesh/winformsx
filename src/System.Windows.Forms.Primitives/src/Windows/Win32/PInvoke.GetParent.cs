// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static HWND GetParent(HWND hWnd)
        => PlatformApi.Window.GetParent(hWnd);

    /// <inheritdoc cref="GetParent(HWND)"/>
    public static HWND GetParent<T>(T hWnd) where T : IHandle<HWND>
    {
        HWND result = GetParent(hWnd.Handle);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}