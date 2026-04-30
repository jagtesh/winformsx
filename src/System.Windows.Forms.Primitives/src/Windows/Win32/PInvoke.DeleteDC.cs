// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Deletes a synthetic device context through the WinFormsX GDI PAL.</summary>
    public static BOOL DeleteDC(HDC hdc)
        => PlatformApi.Gdi.DeleteDC(hdc);
}
