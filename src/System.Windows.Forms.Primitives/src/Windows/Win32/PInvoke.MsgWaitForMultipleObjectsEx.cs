// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe uint MsgWaitForMultipleObjectsEx(
        uint nCount,
        HANDLE* pHandles,
        uint dwMilliseconds,
        QUEUE_STATUS_FLAGS dwWakeMask,
        MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS dwFlags)
        => PlatformApi.Message.MsgWaitForMultipleObjectsEx(nCount, pHandles, dwMilliseconds, dwWakeMask, dwFlags);
}
