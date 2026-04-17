// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public delegate BOOL GetWindowRectDelegate(HWND hWnd, out RECT lpRect);
    public static GetWindowRectDelegate? GetWindowRectCallback { get; set; }
    public static Func<HWND, HWND, int, int, int, int, SET_WINDOW_POS_FLAGS, BOOL>? SetWindowPosCallback { get; set; }

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "GetWindowRect")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows5.0")]
    internal static unsafe extern BOOL GetWindowRect_Native(HWND hWnd, RECT* lpRect);

    [SupportedOSPlatform("windows5.0")]
    public static unsafe BOOL GetWindowRect(HWND hWnd, RECT* lpRect)
    {
        if (GetWindowRectCallback is object)
        {
            BOOL result = GetWindowRectCallback(hWnd, out RECT rect);
            if (lpRect != null)
            {
                *lpRect = rect;
            }

            return result;
        }

        return GetWindowRect_Native(hWnd, lpRect);
    }

    [SupportedOSPlatform("windows5.0")]
    public static unsafe BOOL GetWindowRect(HWND hWnd, out RECT lpRect)
    {
        if (GetWindowRectCallback is object)
        {
            return GetWindowRectCallback(hWnd, out lpRect);
        }

        fixed (RECT* lpRectLocal = &lpRect)
        {
            return GetWindowRect_Native(hWnd, lpRectLocal);
        }
    }

    [DllImport("USER32.dll", ExactSpelling = true, EntryPoint = "SetWindowPos")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows5.0")]
    internal static extern BOOL SetWindowPos_Native(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags);

    [SupportedOSPlatform("windows5.0")]
    public static BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
    {
        if (SetWindowPosCallback is object)
        {
            return SetWindowPosCallback(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);
        }

        return SetWindowPos_Native(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);
    }

    // ── UxTheme Methods ──

    public delegate BOOL IsAppThemedDelegate();
    public static IsAppThemedDelegate? IsAppThemedCallback { get; set; }

    [DllImport("uxtheme.dll", ExactSpelling = true, EntryPoint = "IsAppThemed")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern BOOL IsAppThemed_Native();

    public static BOOL IsAppThemed()
    {
        if (IsAppThemedCallback is object)
            return IsAppThemedCallback();
        return IsAppThemed_Native();
    }
}
