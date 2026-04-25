// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="SetMenu(HWND, HMENU)"/>
    public static BOOL SetMenu<T>(T hWnd, HMENU hMenu)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 menus
        GC.KeepAlive(hWnd.Wrapper);
        return true;
    }

    /// <inheritdoc cref="SetMenu(HWND, HMENU)"/>
    public static BOOL SetMenu<T1, T2>(T1 hWnd, T2 hMenu)
        where T1 : IHandle<HWND>
        where T2 : IHandle<HMENU>
    {
        // Impeller: no Win32 menus
        GC.KeepAlive(hWnd.Wrapper);
        GC.KeepAlive(hMenu.Wrapper);
        return true;
    }
}
