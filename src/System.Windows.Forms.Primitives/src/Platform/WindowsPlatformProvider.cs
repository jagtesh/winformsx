// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform
{
    internal class WindowsPlatformProvider : IPlatformProvider
    {
        private readonly WindowsUser32Interop _user32 = new();
        private readonly WindowsGdi32Interop _gdi32 = new();
        private readonly WindowsUxThemeInterop _uxTheme = new();

        public IUser32Interop User32 => _user32;
        public IGdi32Interop Gdi32 => _gdi32;
        public IUxThemeInterop UxTheme => _uxTheme;

        private class WindowsUser32Interop : IUser32Interop
        {
            public unsafe HWND CreateWindowEx(WINDOW_EX_STYLE dwExStyle, string lpClassName, string lpWindowName, WINDOW_STYLE dwStyle, int X, int Y, int nWidth, int nHeight, HWND hWndParent, global::Windows.Win32.UI.WindowsAndMessaging.HMENU hMenu, HINSTANCE hInstance, object? lpParam)
            {
                return global::Windows.Win32.PInvoke.CreateWindowEx(dwExStyle, lpClassName, lpWindowName, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
            }



            public LRESULT DefWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => global::Windows.Win32.PInvoke.DefWindowProc(hWnd, Msg, wParam, lParam);

            public LRESULT SendMessage(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => global::Windows.Win32.PInvoke.SendMessage(hWnd, Msg, wParam, lParam);

            public bool PostMessage(HWND hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default) => global::Windows.Win32.PInvoke.PostMessage(hWnd, Msg, wParam, lParam);

            public unsafe BOOL GetWindowRect(HWND hWnd, out RECT lpRect)
            {
                fixed (RECT* lpRectLocal = &lpRect)
                {
                    return global::Windows.Win32.PInvoke.GetWindowRect_Native(hWnd, lpRectLocal);
                }
            }

            public BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags)
            {
                return global::Windows.Win32.PInvoke.SetWindowPos_Native(hWnd, hWndInsertAfter, X, Y, cx, cy, uFlags);
            }

            public bool DestroyWindow(HWND hWnd) => global::Windows.Win32.PInvoke.DestroyWindow(hWnd);
        }

        private class WindowsGdi32Interop : IGdi32Interop
        {
            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern HDC GetDC(HWND hWnd);

            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern HDC GetDCEx(HWND hWnd, HRGN hrgnClip, GET_DCX_FLAGS flags);

            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern int ReleaseDC(HWND hWnd, HDC hDC);

            HDC IGdi32Interop.GetDC(HWND hWnd) => GetDC(hWnd);
            HDC IGdi32Interop.GetDCEx(HWND hWnd, HRGN hrgnClip, GET_DCX_FLAGS flags) => GetDCEx(hWnd, hrgnClip, flags);
            int IGdi32Interop.ReleaseDC(HWND hWnd, HDC hDC) => ReleaseDC(hWnd, hDC);
            public BOOL BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop) => global::Windows.Win32.PInvokeCore.BitBlt_Native(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
            public HDC CreateCompatibleDC(HDC hdc) => global::Windows.Win32.PInvokeCore.CreateCompatibleDC_Native(hdc);
        }

        private class WindowsUxThemeInterop : IUxThemeInterop
        {
            public BOOL IsAppThemed() => global::Windows.Win32.PInvoke.IsAppThemed_Native();
        }
    }
}
