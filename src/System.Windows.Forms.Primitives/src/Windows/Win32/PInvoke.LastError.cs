// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static uint GetLastError() => (uint)Marshal.GetLastPInvokeError();

    public static void SetLastError(uint dwErrCode) => Marshal.SetLastPInvokeError((int)dwErrCode);
}
