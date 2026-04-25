// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe LRESULT DispatchMessage(MSG* msg)
    {
        if (msg is null) return default;
        return PlatformApi.Message.DispatchMessage(in *msg);
    }

    /// <summary>ANSI alias for DispatchMessage.</summary>
    public static unsafe LRESULT DispatchMessageA(MSG* msg) => DispatchMessage(msg);
}