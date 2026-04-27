// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Stub — DrawFrameControl is used for scrollbar/button chrome. Returns true (success).</summary>
    public static BOOL DrawFrameControl(HDC hdc, ref RECT lprc, DFC_TYPE uType, DFCS_STATE uState)
        => true;
}
