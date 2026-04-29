// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL HideCaret(HWND hWnd) => BOOL.TRUE;

    public static BOOL ShowCaret(HWND hWnd) => BOOL.TRUE;
}
