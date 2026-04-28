// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Window management abstraction — create, destroy, show, move, resize,
/// query/set styles, coordinate mapping, and registration.
/// Extends <see cref="IUser32Interop"/> for backward compatibility during migration.
/// </summary>
internal unsafe interface IWindowInterop : IUser32Interop
{
    // ─── Creation / Destruction ──────────────────────────────────────────

    new HWND CreateWindowEx(
        WINDOW_EX_STYLE dwExStyle, string? lpClassName, string? lpWindowName,
        WINDOW_STYLE dwStyle, int x, int y, int nWidth, int nHeight,
        HWND hWndParent, HMENU hMenu, HINSTANCE hInstance, object? lpParam);

    new bool DestroyWindow(HWND hWnd);

    ushort RegisterClass(in WNDCLASSW wc);
    bool UnregisterClass(string className, HINSTANCE hInstance);

    // ─── Visibility / State ─────────────────────────────────────────────

    bool ShowWindow(HWND hWnd, SHOW_WINDOW_CMD nCmdShow);
    bool EnableWindow(HWND hWnd, bool bEnable);
    bool IsWindow(HWND hWnd);
    bool IsWindowVisible(HWND hWnd);
    bool IsWindowEnabled(HWND hWnd);
    bool UpdateWindow(HWND hWnd);
    HWND SetActiveWindow(HWND hWnd);
    bool SetForegroundWindow(HWND hWnd);

    // ─── Geometry ───────────────────────────────────────────────────────

    bool MoveWindow(HWND hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);
    bool SetWindowPos(HWND hWnd, HWND hWndInsertAfter, int x, int y, int cx, int cy, SET_WINDOW_POS_FLAGS uFlags);
    bool GetWindowRect(HWND hWnd, out RECT lpRect);
    bool GetClientRect(HWND hWnd, out RECT lpRect);
    bool AdjustWindowRectEx(ref RECT lpRect, WINDOW_STYLE dwStyle, bool bMenu, WINDOW_EX_STYLE dwExStyle);

    // ─── Coordinate Mapping ─────────────────────────────────────────────

    int MapWindowPoints(HWND hWndFrom, HWND hWndTo, ref RECT lpRect);
    int MapWindowPoints(HWND hWndFrom, HWND hWndTo, ref System.Drawing.Point lpPoints, uint cPoints);
    bool ScreenToClient(HWND hWnd, ref System.Drawing.Point lpPoint);
    bool ClientToScreen(HWND hWnd, ref System.Drawing.Point lpPoint);

    // ─── Window Properties ──────────────────────────────────────────────

    nint GetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex);
    nint SetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong);
    nint SetClassLong(HWND hWnd, GET_CLASS_LONG_INDEX nIndex, nint dwNewLong);
    bool SetWindowText(HWND hWnd, string lpString);
    int GetWindowText(HWND hWnd, Span<char> lpString);
    int GetWindowTextLength(HWND hWnd);

    // ─── Hierarchy ──────────────────────────────────────────────────────

    HWND GetParent(HWND hWnd);
    HWND SetParent(HWND hWndChild, HWND hWndNewParent);
    HWND GetAncestor(HWND hwnd, GET_ANCESTOR_FLAGS gaFlags);
    HWND GetDesktopWindow();
    HWND WindowFromPoint(System.Drawing.Point point);
    HWND ChildWindowFromPointEx(HWND hwnd, System.Drawing.Point pt, uint flags);
    bool EnumChildWindows(HWND hWndParent, Func<HWND, bool> callback);
    bool EnumWindows(Func<HWND, bool> callback);
    bool IsChild(HWND hWndParent, HWND hWnd);
    HWND GetWindow(HWND hWnd, GET_WINDOW_CMD uCmd);

    // ─── Painting Invalidation ──────────────────────────────────────────

    bool InvalidateRect(HWND hWnd, RECT? lpRect, bool bErase);
    bool RedrawWindow(HWND hWnd, RECT? lprcUpdate, HRGN hrgnUpdate, REDRAW_WINDOW_FLAGS flags);
    bool ValidateRect(HWND hWnd, RECT? lpRect);

    // ─── Default Processing ─────────────────────────────────────────────

    new LRESULT DefWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
    LRESULT CallWindowProc(void* lpPrevWndFunc, HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
}
