// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Retrieves a system color from the WinFormsX system PAL.</summary>
    public static COLORREF GetSysColor(SYS_COLOR_INDEX nIndex)
        => PlatformApi.System.GetSysColor(nIndex);
}
