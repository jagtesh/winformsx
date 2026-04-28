// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>No-op via PAL — no comctl32 in Impeller.</summary>
    public static BOOL InitCommonControlsEx(in INITCOMMONCONTROLSEX picce)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.InitCommonControlsEx",
            "Native comctl32 common controls are not loaded in WinFormsX; InitCommonControlsEx acknowledged without native work.");
        return true;
    }
}
