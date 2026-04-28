// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the HWND that has mouse capture.</summary>
    public static HWND GetCapture()
        => PlatformApi.Input.GetCapture();

    /// <summary>Releases mouse capture.</summary>
    public static new BOOL ReleaseCapture()
        => PlatformApi.Input.ReleaseCapture();
}
