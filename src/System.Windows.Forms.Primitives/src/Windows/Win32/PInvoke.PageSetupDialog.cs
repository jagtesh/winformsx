// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL PageSetupDlg(PAGESETUPDLGW* param0)
    {
        _ = param0;
        return BOOL.FALSE;
    }
}
