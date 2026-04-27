// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Sets the text (foreground) color for a DC via PAL.</summary>
    public static COLORREF SetTextColor(HDC hdc, COLORREF color)
        => PlatformApi.Gdi.SetTextColor(hdc, color);
}
