// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Registers a custom window message name via PAL.</summary>
    public static uint RegisterWindowMessage(string lpString)
        => PlatformApi.Message.RegisterWindowMessage(lpString);
}
