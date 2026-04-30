// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL PageSetupDlg(PAGESETUPDLGW* param0)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.PageSetupDlg",
            "The stock Windows page-setup dialog is not implemented in WinFormsX yet; PageSetupDlg returned cancel.");
        return WinFormsXCommonDialogInterop.PageSetupDlg(param0);
    }
}
