// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Creates a solid brush via PAL.</summary>
    public static HBRUSH CreateSolidBrush(COLORREF color)
        => PlatformApi.Gdi.CreateSolidBrush(color);

    /// <summary>Creates a pen via PAL.</summary>
    public static HPEN CreatePen(PEN_STYLE iStyle, int cWidth, COLORREF color)
        => PlatformApi.Gdi.CreatePen(iStyle, cWidth, color);
}
