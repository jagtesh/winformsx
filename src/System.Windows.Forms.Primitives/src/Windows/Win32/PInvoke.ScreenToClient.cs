// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL ScreenToClient(HWND hWnd, ref global::System.Drawing.Point lpPoint)
        => PlatformApi.Window.ScreenToClient(hWnd, ref lpPoint);

    /// <inheritdoc cref="ScreenToClient(HWND, ref global::System.Drawing.Point)"/>
    public static unsafe BOOL ScreenToClient<T>(T hWnd, ref global::System.Drawing.Point lpPoint) where T : IHandle<HWND>
    {
        BOOL result = PlatformApi.Window.ScreenToClient(hWnd.Handle, ref lpPoint);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}