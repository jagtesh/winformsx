// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the state of a virtual key via PAL.
    /// </summary>
    public static short GetKeyState(int nVirtKey)
        => global::System.Windows.Forms.Platform.PlatformApi.Input.GetKeyState(nVirtKey);
}
