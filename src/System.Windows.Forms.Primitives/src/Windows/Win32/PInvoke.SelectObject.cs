// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Selects a GDI object into a device context via PAL.</summary>
    public static HGDIOBJ SelectObject(HDC hdc, HGDIOBJ h)
        => PlatformApi.Gdi.SelectObject(hdc, h);
}
