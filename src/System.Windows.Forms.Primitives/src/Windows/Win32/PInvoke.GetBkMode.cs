// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static int GetBkMode(HDC hdc)
        => PlatformApi.Gdi.GetBkMode(hdc);
}
