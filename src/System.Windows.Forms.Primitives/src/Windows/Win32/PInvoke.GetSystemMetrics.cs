// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Retrieves the specified system metric value via PAL.</summary>
    public static int GetCurrentSystemMetrics(SYSTEM_METRICS_INDEX nIndex)
        => PlatformApi.System.GetSystemMetrics(nIndex);

    /// <summary>DPI-aware system metrics — currently ignores DPI and returns standard metric.</summary>
    public static int GetCurrentSystemMetrics(SYSTEM_METRICS_INDEX nIndex, uint dpi)
        => PlatformApi.System.GetSystemMetrics(nIndex);
}