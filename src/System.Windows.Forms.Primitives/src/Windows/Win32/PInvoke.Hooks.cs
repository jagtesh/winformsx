// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    // Legacy callback delegates — kept for backward compatibility but now route through PAL.
    public delegate BOOL GetWindowRectDelegate(HWND hWnd, out RECT lpRect);
    public static GetWindowRectDelegate? GetWindowRectCallback { get; set; }
    public static Func<HWND, HWND, int, int, int, int, SET_WINDOW_POS_FLAGS, BOOL>? SetWindowPosCallback { get; set; }

    public delegate BOOL IsAppThemedDelegate();
    public static IsAppThemedDelegate? IsAppThemedCallback { get; set; }

    public static BOOL IsAppThemed()
    {
        if (IsAppThemedCallback is object)
        {
            return IsAppThemedCallback();
        }

        return true; // Impeller always supports themes — route through PlatformApi
    }
}
