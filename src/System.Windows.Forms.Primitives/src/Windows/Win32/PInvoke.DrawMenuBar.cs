// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="DrawMenuBar(HWND)"/>
    public static BOOL DrawMenuBar<T>(T hWnd)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 menu bar
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.DrawMenuBar",
            "Native Win32 menu-bar repaint is ignored in WinFormsX; menu rendering is owned by the managed PAL.");
        GC.KeepAlive(hWnd.Wrapper); return true;
    }
}

