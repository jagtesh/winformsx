// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContext()
        => PlatformApi.System.GetThreadDpiAwarenessContext();

    /// <summary>Internal alias used by ScaleHelper.</summary>
    public static DPI_AWARENESS_CONTEXT GetThreadDpiAwarenessContextInternal()
        => GetThreadDpiAwarenessContext();
}