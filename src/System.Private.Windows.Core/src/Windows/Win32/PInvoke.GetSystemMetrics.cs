// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    /// <inheritdoc cref="GetSystemMetrics(SYSTEM_METRICS_INDEX)"/>
    public static int GetSystemMetrics(SYSTEM_METRICS_INDEX nIndex) => nIndex switch
    {
        SYSTEM_METRICS_INDEX.SM_CXICON => 32,
        SYSTEM_METRICS_INDEX.SM_CYICON => 32,
        SYSTEM_METRICS_INDEX.SM_CXSMICON => 16,
        SYSTEM_METRICS_INDEX.SM_CYSMICON => 16,
        SYSTEM_METRICS_INDEX.SM_CXVSCROLL => 17,
        SYSTEM_METRICS_INDEX.SM_CYHSCROLL => 17,
        _ => 0,
    };
}
