// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Retrieves device capability data from the WinFormsX GDI PAL.</summary>
    public static int GetDeviceCaps(HDC hdc, GET_DEVICE_CAPS_INDEX index)
        => PlatformApi.Gdi.GetDeviceCaps(hdc, index);
}
