// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="GetMenu(HWND)"/>
    public static HMENU GetMenu<T>(T hWnd)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 menus
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.GetMenu",
            "Native Win32 menus do not exist in WinFormsX; GetMenu returned a null handle.");
        GC.KeepAlive(hWnd.Wrapper); return HMENU.Null;
    }
}

