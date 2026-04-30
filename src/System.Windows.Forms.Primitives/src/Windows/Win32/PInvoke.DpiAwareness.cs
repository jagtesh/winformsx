// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static bool AreDpiAwarenessContextsEqual(DPI_AWARENESS_CONTEXT dpiContextA, DPI_AWARENESS_CONTEXT dpiContextB)
        => PlatformApi.System.AreDpiAwarenessContextsEqual(dpiContextA, dpiContextB);

    public static DPI_AWARENESS GetAwarenessFromDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext)
        => PlatformApi.System.GetAwarenessFromDpiAwarenessContext(dpiContext);

    public static uint GetDpiForSystem()
        => PlatformApi.System.GetDpiForSystem();

    public static HRESULT GetProcessDpiAwareness(HANDLE hprocess, out PROCESS_DPI_AWARENESS value)
        => PlatformApi.System.GetProcessDpiAwareness(hprocess, out value);

    public static DPI_HOSTING_BEHAVIOR GetThreadDpiHostingBehavior()
        => PlatformApi.System.GetThreadDpiHostingBehavior();

    public static bool IsValidDpiAwarenessContext(DPI_AWARENESS_CONTEXT dpiContext)
        => PlatformApi.System.IsValidDpiAwarenessContext(dpiContext);

    public static bool IsProcessDPIAware()
    {
        HRESULT result = PlatformApi.System.GetProcessDpiAwareness(HANDLE.Null, out PROCESS_DPI_AWARENESS dpiAwareness);
        return result.Succeeded && dpiAwareness != PROCESS_DPI_AWARENESS.PROCESS_DPI_UNAWARE;
    }

    public static bool SetProcessDPIAware()
        => PlatformApi.System.SetProcessDPIAware();

    public static HRESULT SetProcessDpiAwareness(PROCESS_DPI_AWARENESS value)
        => PlatformApi.System.SetProcessDpiAwareness(value);

    public static bool SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT value)
        => PlatformApi.System.SetProcessDpiAwarenessContext(value);

    public static DPI_HOSTING_BEHAVIOR SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR value)
        => PlatformApi.System.SetThreadDpiHostingBehavior(value);
}
