// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static nint GetProcAddress(HMODULE hModule, PCSTR lpProcName)
        => PlatformApi.System.GetProcAddress(hModule, lpProcName);
}
