// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

// https://github.com/microsoft/win32metadata/issues/1300
internal static partial class PInvoke
{
    public static unsafe BOOL GetOpenFileName(OPENFILENAME* param0)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.GetOpenFileName",
            "The stock Windows open-file dialog is not implemented in WinFormsX yet; GetOpenFileName returned cancel.");
        return WinFormsXCommonDialogInterop.GetOpenFileName(param0);
    }

    public static unsafe BOOL GetSaveFileName(OPENFILENAME* param0)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.GetSaveFileName",
            "The stock Windows save-file dialog is not implemented in WinFormsX yet; GetSaveFileName returned cancel.");
        return WinFormsXCommonDialogInterop.GetSaveFileName(param0);
    }
}
