// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>No-op via PAL — no comctl32 in Impeller.</summary>
    public static BOOL InitCommonControlsEx(in INITCOMMONCONTROLSEX picce)
        => true;
}
