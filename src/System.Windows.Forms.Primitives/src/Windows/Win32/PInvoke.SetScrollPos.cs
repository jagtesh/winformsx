// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="SetScrollPos(HWND, SCROLLBAR_CONSTANTS, int, BOOL)"/>
    public static int SetScrollPos<T>(T hWnd, SCROLLBAR_CONSTANTS nBar, int nPos, BOOL bRedraw)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 scrollbars
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.SetScrollPos",
            "Native Win32 scrollbar state is ignored in WinFormsX; scrollbars must be represented by managed controls/PAL state.");
        GC.KeepAlive(hWnd.Wrapper); return 0;
    }
}

