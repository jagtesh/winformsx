// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL SetForegroundWindow(HWND hWnd)
        => PlatformApi.Window.SetForegroundWindow(hWnd);

    /// <inheritdoc cref="SetForegroundWindow(HWND)"/>
    public static BOOL SetForegroundWindow<T>(T hWnd) where T : IHandle<HWND>
    {
        BOOL result = SetForegroundWindow(hWnd.Handle);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}