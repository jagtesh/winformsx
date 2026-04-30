// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using global::Windows.Win32.UI.Input.KeyboardAndMouse;
using global::Windows.Win32.UI.Input.Ime;

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
    bool GetPhysicalCursorPos(out System.Drawing.Point lpPoint);
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
    uint SendInput(ReadOnlySpan<INPUT> inputs, int cbSize);
    uint MapVirtualKey(uint uCode, uint uMapType);
    int ToUnicode(uint wVirtKey, uint wScanCode, byte* lpKeyState, char* pwszBuff, int cchBuff, uint wFlags);
    nint GetKeyboardLayout(uint idThread);
    int GetKeyboardLayoutList(int nBuff, nint* lpList);
    nint ActivateKeyboardLayout(nint hkl, uint flags);

    // ─── IME ───────────────────────────────────────────────────────────

    HIMC ImmAssociateContext(HWND hWnd, HIMC hIMC);
    HIMC ImmCreateContext();
    HIMC ImmGetContext(HWND hWnd);
    bool ImmGetConversionStatus(HIMC hIMC, IME_CONVERSION_MODE* lpfdwConversion, IME_SENTENCE_MODE* lpfdwSentence);
    bool ImmGetOpenStatus(HIMC hIMC);
    bool ImmNotifyIME(HIMC hIMC, NOTIFY_IME_ACTION dwAction, NOTIFY_IME_INDEX dwIndex, uint dwValue);
    bool ImmReleaseContext(HWND hWnd, HIMC hIMC);
    bool ImmSetConversionStatus(HIMC hIMC, IME_CONVERSION_MODE fdwConversion, IME_SENTENCE_MODE fdwSentence);
    bool ImmSetOpenStatus(HIMC hIMC, bool fOpen);

    // ─── Mouse ──────────────────────────────────────────────────────────

    uint GetDoubleClickTime();
    bool SwapMouseButton(bool fSwap);
    bool DragDetect(HWND hwnd, System.Drawing.Point pt);
    bool TrackMouseEvent(HWND hwnd, uint dwFlags, uint dwHoverTime);
}
