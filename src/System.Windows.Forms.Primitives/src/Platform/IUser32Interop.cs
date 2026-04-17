// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform
{
    internal interface IUser32Interop
    {
        HWND CreateWindowEx(WINDOW_EX_STYLE dwExStyle, string lpClassName, string lpWindowName, WINDOW_STYLE dwStyle, int X, int Y, int nWidth, int nHeight, HWND hWndParent, global::Windows.Win32.UI.WindowsAndMessaging.HMENU hMenu, HINSTANCE hInstance, object? lpParam);

        bool DestroyWindow(HWND hWnd);

        LRESULT DefWindowProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam);

        LRESULT SendMessage(HWND hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default);

        bool PostMessage(HWND hWnd, uint Msg, WPARAM wParam = default, LPARAM lParam = default);

        BOOL GetWindowRect(HWND hWnd, out RECT lpRect);

        BOOL SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int X, int Y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags);
    }
}
