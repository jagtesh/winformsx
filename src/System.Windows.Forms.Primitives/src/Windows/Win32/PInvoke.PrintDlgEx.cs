// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    internal static unsafe HRESULT PrintDlgEx(PRINTDLGEXW* pPD)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.PrintDlgEx",
            "The stock Windows extended print dialog is not implemented in WinFormsX yet; PrintDlgEx returned cancel.");
        return WinFormsXCommonDialogInterop.PrintDlgEx(pPD);
    }
}
