// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform
{
    internal class WindowsPlatformProvider : IPlatformProvider
    {
        public IUser32Interop User32 { get; } = new WindowsUser32Interop();
        public IGdi32Interop Gdi32 { get; } = new WindowsGdi32Interop();

        private class WindowsUser32Interop : IUser32Interop
        {
            public unsafe HWND CreateWindowEx(WINDOW_EX_STYLE dwExStyle, string lpClassName, string lpWindowName, WINDOW_STYLE dwStyle, int X, int Y, int nWidth, int nHeight, HWND hWndParent, global::Windows.Win32.UI.WindowsAndMessaging.HMENU hMenu, HINSTANCE hInstance, nint lpParam)
            {
                fixed (char* cn = lpClassName)
                fixed (char* wn = lpWindowName)
                {
                    return PInvoke.CreateWindowEx(dwExStyle, cn, wn, dwStyle, X, Y, nWidth, nHeight, hWndParent, hMenu, hInstance, (void*)lpParam);
                }
            }

            public bool DestroyWindow(HWND hWnd) => PInvoke.DestroyWindow(hWnd);

            public LRESULT DefWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => PInvoke.DefWindowProc(hWnd, Msg, wParam, lParam);

            public LRESULT SendMessage(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => PInvoke.SendMessage(hWnd, Msg, wParam, lParam);

            public bool PostMessage(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam) => PInvoke.PostMessage(hWnd, Msg, wParam, lParam);
        }

        private class WindowsGdi32Interop : IGdi32Interop
        {
            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern HDC GetDC(HWND hWnd);

            [DllImport("user32.dll", ExactSpelling = true)]
            public static extern int ReleaseDC(HWND hWnd, HDC hDC);

            HDC IGdi32Interop.GetDC(HWND hWnd) => GetDC(hWnd);
            int IGdi32Interop.ReleaseDC(HWND hWnd, HDC hDC) => ReleaseDC(hWnd, hDC);
        }
    }
}
