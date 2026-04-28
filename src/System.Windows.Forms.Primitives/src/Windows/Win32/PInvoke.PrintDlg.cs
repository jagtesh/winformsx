// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    internal static unsafe BOOL PrintDlg(PRINTDLGW_64* pPD)
    {
        _ = pPD;
        return BOOL.FALSE;
    }

    internal static unsafe BOOL PrintDlg(PRINTDLGW_32* pPD)
    {
        _ = pPD;
        return BOOL.FALSE;
    }
}
