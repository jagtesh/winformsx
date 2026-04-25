// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL KillTimer(HWND hWnd, nuint uIDEvent)
        => PlatformApi.System.KillTimer(hWnd, (nint)uIDEvent);

    /// <inheritdoc cref="KillTimer(HWND, nuint)"/>
    public static BOOL KillTimer<T>(T hWnd, nuint uIDEvent) where T : IHandle<HWND>
    {
        BOOL result = KillTimer(hWnd.Handle, uIDEvent);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}