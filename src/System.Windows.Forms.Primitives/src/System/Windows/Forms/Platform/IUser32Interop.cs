// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Legacy User32 interop surface — used by existing <see cref="NativeWindow"/>
/// call sites via <c>PlatformApi.User32</c>. This is a migration shim;
/// new code should use <see cref="IWindowInterop"/> or <see cref="IMessageInterop"/> directly.
/// </summary>
internal interface IUser32Interop
{
    HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle, string? lpClassName, string? lpWindowName,
        WINDOW_STYLE dwStyle, int x, int y, int nWidth, int nHeight,
        HWND hWndParent, HMENU hMenu, HINSTANCE hInstance, object? lpParam);

    LRESULT DefWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
    bool DestroyWindow(HWND hWnd);
    bool PostMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
}
