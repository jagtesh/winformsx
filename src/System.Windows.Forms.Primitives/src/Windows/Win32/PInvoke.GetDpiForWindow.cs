// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static uint GetDpiForWindow(HWND hwnd)
        => PlatformApi.System.GetDpiForWindow(hwnd);

    /// <inheritdoc cref="GetDpiForWindow(HWND)"/>
    public static uint GetDpiForWindow<T>(T hwnd) where T : IHandle<HWND>
    {
        uint result = GetDpiForWindow(hwnd.Handle);
        GC.KeepAlive(hwnd.Wrapper);
        return result;
    }
}