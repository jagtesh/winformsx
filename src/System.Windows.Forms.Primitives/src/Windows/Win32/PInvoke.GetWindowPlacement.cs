// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL GetWindowPlacement(HWND hWnd, WINDOWPLACEMENT* lpwndpl)
        => PlatformApi.Window.GetWindowPlacement(hWnd, lpwndpl);

    public static unsafe BOOL SetWindowPlacement(HWND hWnd, WINDOWPLACEMENT* lpwndpl)
        => PlatformApi.Window.SetWindowPlacement(hWnd, lpwndpl);
}
