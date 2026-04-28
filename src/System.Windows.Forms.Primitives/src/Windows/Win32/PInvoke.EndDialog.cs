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
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.EndDialog",
            "Native Win32 dialog lifetime is not implemented in WinFormsX; EndDialog acknowledged without closing a native dialog.");
        GC.KeepAlive(hDlg.Wrapper); return true;
    }
}

