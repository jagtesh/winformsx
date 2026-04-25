// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="GetDlgItem(HWND, int)"/>
    public static HWND GetDlgItem<T>(T hDlg, int nIDDlgItem)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 dialogs
        GC.KeepAlive(hDlg.Wrapper); return HWND.Null;
    }
}


