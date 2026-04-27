// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Retrieves a module handle via PAL. Returns a synthetic handle.</summary>
    public static HMODULE GetModuleHandle(string? lpModuleName)
        => PlatformApi.System.GetModuleHandle(lpModuleName);

    /// <summary>PCWSTR overload used by NativeWindow.DefaultWindowProc.</summary>
    public static unsafe HMODULE GetModuleHandle(PCWSTR lpModuleName)
        => PlatformApi.System.GetModuleHandle(null);
}
