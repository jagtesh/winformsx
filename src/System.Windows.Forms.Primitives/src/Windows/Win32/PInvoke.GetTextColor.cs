// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Gets the text color for a DC from the WinFormsX GDI PAL.</summary>
    public static COLORREF GetTextColor(HDC hdc)
        => PlatformApi.Gdi.GetTextColor(hdc);
}
