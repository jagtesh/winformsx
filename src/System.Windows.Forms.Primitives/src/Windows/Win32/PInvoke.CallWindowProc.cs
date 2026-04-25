// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe LRESULT CallWindowProc(void* lpPrevWndFunc, HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
        => PlatformApi.Window.CallWindowProc(lpPrevWndFunc, hWnd, Msg, wParam, lParam);
}