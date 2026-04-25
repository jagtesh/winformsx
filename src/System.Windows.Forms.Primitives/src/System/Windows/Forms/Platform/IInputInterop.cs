// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Input abstraction — keyboard, mouse, cursor, focus, and capture.
/// </summary>
internal unsafe interface IInputInterop
{
    // ─── Focus ──────────────────────────────────────────────────────────

    HWND GetFocus();
    HWND SetFocus(HWND hWnd);
    HWND GetActiveWindow();
    HWND SetActiveWindow(HWND hWnd);
    HWND GetForegroundWindow();
    bool SetForegroundWindow(HWND hWnd);

    // ─── Capture ────────────────────────────────────────────────────────

    HWND GetCapture();
    HWND SetCapture(HWND hWnd);
    bool ReleaseCapture();

    // ─── Cursor ─────────────────────────────────────────────────────────

    bool GetCursorPos(out System.Drawing.Point lpPoint);
    bool SetCursorPos(int x, int y);
    HCURSOR SetCursor(HCURSOR hCursor);
    HCURSOR LoadCursor(HINSTANCE hInstance, PCWSTR lpCursorName);
    bool ClipCursor(RECT* lpRect);
    bool ShowCursor(bool bShow);
    HCURSOR DestroyCursor(HCURSOR hCursor);

    // ─── Keyboard ───────────────────────────────────────────────────────

    short GetKeyState(int nVirtKey);
    short GetAsyncKeyState(int vKey);
    bool GetKeyboardState(byte* lpKeyState);
    uint MapVirtualKey(uint uCode, uint uMapType);
    int ToUnicode(uint wVirtKey, uint wScanCode, byte* lpKeyState, char* pwszBuff, int cchBuff, uint wFlags);
    nint GetKeyboardLayout(uint idThread);
    nint ActivateKeyboardLayout(nint hkl, uint flags);

    // ─── Mouse ──────────────────────────────────────────────────────────

    uint GetDoubleClickTime();
    bool SwapMouseButton(bool fSwap);
    bool DragDetect(HWND hwnd, System.Drawing.Point pt);
    bool TrackMouseEvent(HWND hwnd, uint dwFlags, uint dwHoverTime);
}
