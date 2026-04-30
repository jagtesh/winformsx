// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;
using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static COMMON_DLG_ERRORS CommDlgExtendedError()
    {
        return WinFormsXCommonDialogInterop.CommDlgExtendedError();
    }
}
