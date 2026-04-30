// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static uint GetGuiResources(Windows.Win32.Foundation.HANDLE hProcess, Windows.Win32.System.Threading.GET_GUI_RESOURCES_FLAGS uiFlags)
    {
        _ = hProcess;
        _ = uiFlags;

        // WinFormsX does not expose process-global native GUI handle counters.
        // Return a stable zero so callers can continue with deterministic behavior.
        return 0;
    }
}
