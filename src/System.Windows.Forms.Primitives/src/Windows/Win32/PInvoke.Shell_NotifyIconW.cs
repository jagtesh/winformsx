// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

// https://github.com/microsoft/CsWin32/issues/882
internal static partial class PInvoke
{
    public static BOOL Shell_NotifyIconW(NOTIFY_ICON_MESSAGE dwMessage, ref NOTIFYICONDATAW lpData) => true;
}
