// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL ClientToScreen(HWND hWnd, ref global::System.Drawing.Point lpPoint)
        => PlatformApi.Window.ClientToScreen(hWnd, ref lpPoint);

    /// <inheritdoc cref="ClientToScreen(HWND, ref global::System.Drawing.Point)"/>
    public static unsafe BOOL ClientToScreen<T>(T hWnd, ref global::System.Drawing.Point lpPoint) where T : IHandle<HWND>
    {
        BOOL result = PlatformApi.Window.ClientToScreen(hWnd.Handle, ref lpPoint);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}