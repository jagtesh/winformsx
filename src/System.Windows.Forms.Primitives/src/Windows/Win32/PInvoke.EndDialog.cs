// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="EndDialog(HWND, nint)"/>
    public static BOOL EndDialog<T>(T hDlg, IntPtr nResult)
        where T : IHandle<HWND>
    {
        // Impeller: no Win32 dialogs
        GC.KeepAlive(hDlg.Wrapper); return true;
    }
}


