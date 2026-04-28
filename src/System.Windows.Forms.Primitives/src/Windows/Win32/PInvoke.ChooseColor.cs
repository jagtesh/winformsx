// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL ChooseColor(CHOOSECOLORW* param0)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.ChooseColor",
            "The stock Windows color picker is not implemented in WinFormsX yet; ChooseColor returned false.");
        _ = param0;
        return BOOL.FALSE;
    }
}
