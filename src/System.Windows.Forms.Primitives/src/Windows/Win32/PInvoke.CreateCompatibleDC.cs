// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Creates a synthetic compatible device context through the WinFormsX GDI PAL.</summary>
    public static HDC CreateCompatibleDC(HDC hdc)
        => PlatformApi.Gdi.CreateCompatibleDC(hdc);
}
