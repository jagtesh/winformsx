// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="GetSystemMenu(HWND, BOOL)"/>
    public static HMENU GetSystemMenu<T>(T hwnd, BOOL bRevert) where T : IHandle<HWND>
    {
        // In Impeller mode, no system menu exists. Return null handle directly
        // to avoid recursion (HWND : IHandle<HWND> causes overload resolution
        // to select this generic method when calling GetSystemMenu(HWND, BOOL)).
        GC.KeepAlive(hwnd.Wrapper);
        return HMENU.Null;
    }
}
