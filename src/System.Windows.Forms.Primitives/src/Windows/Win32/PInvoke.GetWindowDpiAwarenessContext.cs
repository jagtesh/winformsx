// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="GetWindowDpiAwarenessContext(HWND)"/>
    public static DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext(HWND hwnd)
        => PlatformApi.System.GetWindowDpiAwarenessContext(hwnd);

    /// <inheritdoc cref="GetWindowDpiAwarenessContext(HWND)"/>
    public static DPI_AWARENESS_CONTEXT GetWindowDpiAwarenessContext<T>(T hwnd) where T : IHandle<HWND>
    {
        DPI_AWARENESS_CONTEXT result = GetWindowDpiAwarenessContext(hwnd.Handle);
        GC.KeepAlive(hwnd.Wrapper);
        return result;
    }
}
