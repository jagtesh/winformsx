// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL ChooseFont(CHOOSEFONTW* lpcf)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.ChooseFont",
            "The stock Windows font picker is not implemented in WinFormsX yet; ChooseFont returned false.");
        _ = lpcf;
        return BOOL.FALSE;
    }
}
