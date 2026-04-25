// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller input interop — keyboard, mouse, cursor, focus, capture.
/// </summary>
internal sealed unsafe class ImpellerInputInterop : IInputInterop
{
    private HWND _focusWindow;
    private HWND _activeWindow;
    private HWND _captureWindow;
    private System.Drawing.Point _cursorPos;

    // --- Focus ----------------------------------------------------------

    public HWND GetFocus() => _focusWindow;
    public HWND SetFocus(HWND hWnd) { var old = _focusWindow; _focusWindow = hWnd; return old; }
    public HWND GetActiveWindow() => _activeWindow;
    public HWND SetActiveWindow(HWND hWnd) { var old = _activeWindow; _activeWindow = hWnd; return old; }
    public HWND GetForegroundWindow() => _activeWindow;
    public bool SetForegroundWindow(HWND hWnd) { _activeWindow = hWnd; return true; }

    // --- Capture --------------------------------------------------------

    public HWND GetCapture() => _captureWindow;
    public HWND SetCapture(HWND hWnd) { var old = _captureWindow; _captureWindow = hWnd; return old; }
    public bool ReleaseCapture() { _captureWindow = HWND.Null; return true; }

    // --- Cursor ---------------------------------------------------------

    public bool GetCursorPos(out System.Drawing.Point pt) { pt = _cursorPos; return true; }
    public bool SetCursorPos(int x, int y) { _cursorPos = new System.Drawing.Point(x, y); return true; }
    public HCURSOR SetCursor(HCURSOR hCursor) => hCursor;
    public HCURSOR LoadCursor(HINSTANCE hInstance, PCWSTR lpCursorName) => (HCURSOR)(nint)1;
    public bool ClipCursor(RECT* lpRect) => true;
    public bool ShowCursor(bool bShow) => true;
    public HCURSOR DestroyCursor(HCURSOR hCursor) => hCursor;

    // --- Keyboard -------------------------------------------------------

    public short GetKeyState(int vKey) => 0;
    public short GetAsyncKeyState(int vKey) => 0;
    public bool GetKeyboardState(byte* lpKeyState) => true;
    public uint MapVirtualKey(uint code, uint type) => 0;
    public int ToUnicode(uint vKey, uint scanCode, byte* keyState, char* buf, int bufLen, uint flags) => 0;
    public nint GetKeyboardLayout(uint idThread) => 0;
    public nint ActivateKeyboardLayout(nint hkl, uint flags) => hkl;

    // --- Mouse ----------------------------------------------------------

    public uint GetDoubleClickTime() => 500;
    public bool SwapMouseButton(bool fSwap) => false;
    public bool DragDetect(HWND hwnd, System.Drawing.Point pt) => false;
    public bool TrackMouseEvent(HWND hwnd, uint dwFlags, uint dwHoverTime) => true;
}
