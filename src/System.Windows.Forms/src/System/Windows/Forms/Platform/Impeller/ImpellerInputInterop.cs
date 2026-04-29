// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

using global::Windows.Win32.UI.Input.KeyboardAndMouse;

/// <summary>
/// Impeller input interop — keyboard, mouse, cursor, focus, capture.
/// </summary>
internal sealed unsafe class ImpellerInputInterop : IInputInterop
{
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_RBUTTONDOWN = 0x0204;
    private const uint WM_RBUTTONUP = 0x0205;
    private const uint WM_MBUTTONDOWN = 0x0207;
    private const uint WM_MBUTTONUP = 0x0208;
    private const nint MK_LBUTTON = 0x0001;
    private const nint MK_RBUTTON = 0x0002;
    private const nint MK_SHIFT = 0x0004;
    private const nint MK_CONTROL = 0x0008;
    private const nint MK_MBUTTON = 0x0010;
    private const nint MK_ALT = 0x0020;

    private HWND _focusWindow;
    private HWND _activeWindow;
    private HWND _captureWindow;
    private System.Drawing.Point _cursorPos;
    private readonly HashSet<int> _pressedKeys = [];
    private readonly object _inputStateLock = new();

    // --- Focus ----------------------------------------------------------

    public HWND GetFocus() => _focusWindow;
    public HWND SetFocus(HWND hWnd) { var old = _focusWindow; _focusWindow = hWnd; if (hWnd != HWND.Null) _activeWindow = hWnd; return old; }
    public HWND GetActiveWindow() => _activeWindow;
    public HWND SetActiveWindow(HWND hWnd) { var old = _activeWindow; _activeWindow = hWnd; return old; }
    public HWND GetForegroundWindow() => _activeWindow;
    public bool SetForegroundWindow(HWND hWnd) { _activeWindow = hWnd; return true; }

    // --- Capture --------------------------------------------------------

    public HWND GetCapture() => _captureWindow;
    public HWND SetCapture(HWND hWnd) { var old = _captureWindow; _captureWindow = hWnd; return old; }
    public bool ReleaseCapture() { _captureWindow = HWND.Null; return true; }

    // --- Cursor ---------------------------------------------------------

    public bool GetCursorPos(out System.Drawing.Point pt)
    {
        lock (_inputStateLock)
        {
            pt = _cursorPos;
        }

        return true;
    }

    public bool GetPhysicalCursorPos(out System.Drawing.Point pt)
    {
        lock (_inputStateLock)
        {
            pt = _cursorPos;
        }

        return true;
    }

    public bool SetCursorPos(int x, int y)
    {
        lock (_inputStateLock)
        {
            _cursorPos = new System.Drawing.Point(x, y);
        }

        return true;
    }

    public HCURSOR SetCursor(HCURSOR hCursor) => hCursor;
    public HCURSOR LoadCursor(HINSTANCE hInstance, PCWSTR lpCursorName) => (HCURSOR)(nint)1;
    public bool ClipCursor(RECT* lpRect) => true;
    public bool ShowCursor(bool bShow) => true;
    public HCURSOR DestroyCursor(HCURSOR hCursor) => hCursor;

    // --- Keyboard -------------------------------------------------------

    public short GetKeyState(int vKey)
    {
        lock (_inputStateLock)
        {
            return _pressedKeys.Contains(vKey) ? unchecked((short)0x8000) : (short)0;
        }
    }

    public short GetAsyncKeyState(int vKey) => GetKeyState(vKey);
    public bool GetKeyboardState(byte* lpKeyState)
    {
        if (lpKeyState is null)
        {
            return false;
        }

        lock (_inputStateLock)
        {
            for (int i = 0; i < 256; i++)
            {
                lpKeyState[i] = _pressedKeys.Contains(i) ? (byte)0x80 : (byte)0;
            }
        }

        return true;
    }

    public uint SendInput(ReadOnlySpan<INPUT> inputs, int cbSize)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            DispatchInput(inputs[i]);
        }

        return (uint)inputs.Length;
    }

    public uint MapVirtualKey(uint code, uint type) => 0;
    public int ToUnicode(uint vKey, uint scanCode, byte* keyState, char* buf, int bufLen, uint flags) => 0;
    public nint GetKeyboardLayout(uint idThread) => 0;
    public nint ActivateKeyboardLayout(nint hkl, uint flags) => hkl;

    // --- Mouse ----------------------------------------------------------

    public uint GetDoubleClickTime() => 500;
    public bool SwapMouseButton(bool fSwap) => false;
    public bool DragDetect(HWND hwnd, System.Drawing.Point pt) => false;
    public bool TrackMouseEvent(HWND hwnd, uint dwFlags, uint dwHoverTime) => true;

    private void DispatchInput(in INPUT input)
    {
        switch (input.type)
        {
            case INPUT_TYPE.INPUT_KEYBOARD:
                DispatchKeyboardInput(input.Anonymous.ki);
                break;
            case INPUT_TYPE.INPUT_MOUSE:
                DispatchMouseInput(input.Anonymous.mi);
                break;
        }
    }

    private void DispatchKeyboardInput(in KEYBDINPUT input)
    {
        bool keyUp = (input.dwFlags & KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP) != 0;
        bool unicode = (input.dwFlags & KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE) != 0;
        int virtualKey = (int)input.wVk;

        if (!unicode && virtualKey != 0)
        {
            if (keyUp)
            {
                lock (_inputStateLock)
                {
                    _pressedKeys.Remove(virtualKey);
                }
            }
            else
            {
                lock (_inputStateLock)
                {
                    _pressedKeys.Add(virtualKey);
                }
            }

            PostToKeyboardTarget(keyUp ? WM_KEYUP : WM_KEYDOWN, (WPARAM)virtualKey, (LPARAM)0);
            return;
        }

        if (unicode && !keyUp && input.wScan != 0)
        {
            PostToKeyboardTarget(WM_CHAR, (WPARAM)(int)input.wScan, (LPARAM)0);
        }
    }

    private void DispatchMouseInput(in MOUSEINPUT input)
    {
        MOUSE_EVENT_FLAGS flags = input.dwFlags;
        if ((flags & MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE) != 0)
        {
            if ((flags & MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE) != 0)
            {
                lock (_inputStateLock)
                {
                    _cursorPos = FromAbsoluteMousePoint(input.dx, input.dy);
                }
            }
            else
            {
                lock (_inputStateLock)
                {
                    _cursorPos.Offset(input.dx, input.dy);
                }
            }

            PostToMouseTarget(WM_MOUSEMOVE, (WPARAM)GetMouseKeyState());
        }

        DispatchMouseButton(flags, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP, VIRTUAL_KEY.VK_LBUTTON, WM_LBUTTONDOWN, WM_LBUTTONUP);
        DispatchMouseButton(flags, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP, VIRTUAL_KEY.VK_RBUTTON, WM_RBUTTONDOWN, WM_RBUTTONUP);
        DispatchMouseButton(flags, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN, MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP, VIRTUAL_KEY.VK_MBUTTON, WM_MBUTTONDOWN, WM_MBUTTONUP);
    }

    private void DispatchMouseButton(
        MOUSE_EVENT_FLAGS flags,
        MOUSE_EVENT_FLAGS downFlag,
        MOUSE_EVENT_FLAGS upFlag,
        VIRTUAL_KEY virtualKey,
        uint downMessage,
        uint upMessage)
    {
        int key = (int)virtualKey;
        if ((flags & downFlag) != 0)
        {
            lock (_inputStateLock)
            {
                _pressedKeys.Add(key);
            }

            _captureWindow = GetMouseTarget();
            PostToMouseTarget(downMessage, (WPARAM)GetMouseKeyState());
        }

        if ((flags & upFlag) != 0)
        {
            lock (_inputStateLock)
            {
                _pressedKeys.Remove(key);
            }

            PostToMouseTarget(upMessage, (WPARAM)GetMouseKeyState());
            _captureWindow = HWND.Null;
        }
    }

    private void PostToKeyboardTarget(uint message, WPARAM wParam, LPARAM lParam)
    {
        HWND target = _focusWindow != HWND.Null ? _focusWindow : _activeWindow;
        if (target != HWND.Null)
        {
            PlatformApi.Message.PostMessage(target, message, wParam, lParam);
        }
    }

    private void PostToMouseTarget(uint message, WPARAM wParam)
    {
        HWND target = GetMouseTarget();
        if (target != HWND.Null)
        {
            PlatformApi.Message.PostMessage(target, message, wParam, MakePointLParam(_cursorPos));
        }
    }

    private nint GetMouseKeyState()
    {
        nint keyState = 0;

        lock (_inputStateLock)
        {
            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_LBUTTON))
            {
                keyState |= MK_LBUTTON;
            }

            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_RBUTTON))
            {
                keyState |= MK_RBUTTON;
            }

            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_MBUTTON))
            {
                keyState |= MK_MBUTTON;
            }

            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_SHIFT))
            {
                keyState |= MK_SHIFT;
            }

            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_CONTROL))
            {
                keyState |= MK_CONTROL;
            }

            if (_pressedKeys.Contains((int)VIRTUAL_KEY.VK_MENU))
            {
                keyState |= MK_ALT;
            }
        }

        return keyState;
    }

    private HWND GetMouseTarget()
        => _captureWindow != HWND.Null ? _captureWindow : _focusWindow != HWND.Null ? _focusWindow : _activeWindow;

    private static LPARAM MakePointLParam(System.Drawing.Point point)
        => (LPARAM)(nint)(((point.Y & 0xFFFF) << 16) | (point.X & 0xFFFF));

    private static System.Drawing.Point FromAbsoluteMousePoint(int x, int y)
    {
        System.Drawing.Size size = SystemInformation.PrimaryMonitorSize;
        int width = Math.Max(1, size.Width - 1);
        int height = Math.Max(1, size.Height - 1);
        return new System.Drawing.Point(
            (int)Math.Round((x / 65535.0) * width),
            (int)Math.Round((y / 65535.0) * height));
    }
}
