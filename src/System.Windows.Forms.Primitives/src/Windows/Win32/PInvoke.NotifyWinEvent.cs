// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static void NotifyWinEvent(uint winEvent, HWND hwnd, int idObject, int idChild)
        => PlatformApi.Accessibility.NotifyWinEvent(winEvent, hwnd, idObject, idChild);

    /// <inheritdoc cref="NotifyWinEvent(uint, HWND, int, int)"/>
    public static void NotifyWinEvent<T>(uint winEvent, T hwnd, int idObject, int idChild) where T : IHandle<HWND>
    {
        NotifyWinEvent(winEvent, hwnd.Handle, idObject, idChild);
        GC.KeepAlive(hwnd.Wrapper);
    }
}