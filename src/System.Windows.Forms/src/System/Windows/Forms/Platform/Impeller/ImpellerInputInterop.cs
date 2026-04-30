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
    private const uint WM_SYSKEYDOWN = 0x0104;
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint WM_SYSCHAR = 0x0106;
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
    private Form? _resizeForm;
    private System.Drawing.Point _resizeStartCursor;
    private System.Drawing.Size _resizeStartSize;
    private bool _resizeRightEdge;
    private bool _resizeBottomEdge;
    private nint _keyboardLayout = unchecked((nint)0x04090409);
    private System.Drawing.Point _cursorPos;
    private int? _dispatchedMouseKeyState;
    private readonly HashSet<int> _pressedKeys = [];
    private readonly object _inputStateLock = new();
    private static readonly string? s_traceFile = Environment.GetEnvironmentVariable("WINFORMSX_TRACE_FILE");

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
            if (TryGetDispatchedMouseButtonState(vKey, out bool pressed))
            {
                return pressed ? unchecked((short)0x8000) : (short)0;
            }

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
            INPUT input = inputs[i];
            if (input.type == INPUT_TYPE.INPUT_MOUSE && IsMoveOnly(input.Anonymous.mi.dwFlags))
            {
                int lastMoveIndex = i;
                while (lastMoveIndex + 1 < inputs.Length)
                {
                    INPUT next = inputs[lastMoveIndex + 1];
                    if (next.type != INPUT_TYPE.INPUT_MOUSE || !IsMoveOnly(next.Anonymous.mi.dwFlags))
                    {
                        break;
                    }

                    lastMoveIndex++;
                }

                input = inputs[lastMoveIndex];
                i = lastMoveIndex;
            }

            try
            {
                DispatchInput(input);
            }
            catch (Exception ex)
            {
                TraceInput($"[InputDispatchDrop] type={input.type} error={ex.GetType().Name}: {ex.Message}");
            }
        }

        return (uint)inputs.Length;
    }

    public uint MapVirtualKey(uint code, uint type) => 0;
    public int ToUnicode(uint vKey, uint scanCode, byte* keyState, char* buf, int bufLen, uint flags) => 0;
    public nint GetKeyboardLayout(uint idThread)
    {
        lock (_inputStateLock)
        {
            return _keyboardLayout;
        }
    }

    public nint ActivateKeyboardLayout(nint hkl, uint flags)
    {
        lock (_inputStateLock)
        {
            nint previous = _keyboardLayout;
            if (hkl != 0)
            {
                _keyboardLayout = hkl;
            }

            return previous;
        }
    }

    // --- Mouse ----------------------------------------------------------

    public uint GetDoubleClickTime() => 500;
    public bool SwapMouseButton(bool fSwap) => false;
    public bool DragDetect(HWND hwnd, System.Drawing.Point pt) => false;
    public bool TrackMouseEvent(HWND hwnd, uint dwFlags, uint dwHoverTime) => true;

    internal void SetDispatchedMouseKeyState(WPARAM wParam)
    {
        lock (_inputStateLock)
        {
            _dispatchedMouseKeyState = (int)((nint)wParam & (MK_LBUTTON | MK_RBUTTON | MK_MBUTTON));
        }
    }

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
            bool altModifierActive;
            if (keyUp)
            {
                lock (_inputStateLock)
                {
                    _pressedKeys.Remove(virtualKey);
                    altModifierActive = IsAltModifierPressedNoLock();
                }
            }
            else
            {
                lock (_inputStateLock)
                {
                    _pressedKeys.Add(virtualKey);
                    altModifierActive = IsAltModifierPressedNoLock();
                }
            }

            bool isAltKey = virtualKey is (int)VIRTUAL_KEY.VK_MENU or (int)VIRTUAL_KEY.VK_LMENU or (int)VIRTUAL_KEY.VK_RMENU;
            uint keyMessage = keyUp
                ? (altModifierActive && !isAltKey ? WM_SYSKEYUP : WM_KEYUP)
                : (altModifierActive && !isAltKey ? WM_SYSKEYDOWN : WM_KEYDOWN);
            PostToKeyboardTarget(keyMessage, (WPARAM)virtualKey, (LPARAM)0);

            if (!keyUp && altModifierActive && !isAltKey && TryMapVirtualKeyToChar(virtualKey, out char mappedChar))
            {
                PostSystemCharToMnemonicTarget((WPARAM)mappedChar);
            }

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

            ApplyFormResizeFromPointer();
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

            TryBeginFormResize();
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
            EndFormResize();
        }
    }

    private void TryBeginFormResize()
    {
        if (_resizeForm is not null)
        {
            return;
        }

        HWND target = GetMouseTarget();
        Control? control = Control.FromHandle(target);
        if (control is null)
        {
            return;
        }

        Form? form = control as Form ?? control.FindForm();
        if (form is null || !form.TopLevel || !form.Visible)
        {
            return;
        }

        System.Drawing.Point cursorPos;
        lock (_inputStateLock)
        {
            cursorPos = _cursorPos;
        }

        System.Drawing.Point client = cursorPos;
        if (!PlatformApi.Window.ScreenToClient((HWND)(nint)form.Handle, ref client))
        {
            return;
        }

        const int resizeEdgeThreshold = 4;
        bool rightEdge = Math.Abs(client.X - form.DisplayRectangle.Right) <= resizeEdgeThreshold;
        bool bottomEdge = Math.Abs(client.Y - form.DisplayRectangle.Bottom) <= resizeEdgeThreshold;
        if (!rightEdge && !bottomEdge)
        {
            return;
        }

        _resizeForm = form;
        _resizeStartCursor = cursorPos;
        _resizeStartSize = form.Size;
        _resizeRightEdge = rightEdge;
        _resizeBottomEdge = bottomEdge;
    }

    private void ApplyFormResizeFromPointer()
    {
        if (_resizeForm is null)
        {
            return;
        }

        System.Drawing.Point cursorPos;
        lock (_inputStateLock)
        {
            cursorPos = _cursorPos;
        }

        int deltaX = cursorPos.X - _resizeStartCursor.X;
        int deltaY = cursorPos.Y - _resizeStartCursor.Y;
        int width = _resizeStartSize.Width;
        int height = _resizeStartSize.Height;
        if (_resizeRightEdge)
        {
            width = Math.Max(1, _resizeStartSize.Width + deltaX);
        }

        if (_resizeBottomEdge)
        {
            height = Math.Max(1, _resizeStartSize.Height + deltaY);
        }

        _resizeForm.Size = new System.Drawing.Size(width, height);
    }

    private void EndFormResize()
    {
        _resizeForm = null;
        _resizeStartCursor = default;
        _resizeStartSize = default;
        _resizeRightEdge = false;
        _resizeBottomEdge = false;
    }

    private void PostToKeyboardTarget(uint message, WPARAM wParam, LPARAM lParam)
    {
        HWND target = _focusWindow != HWND.Null ? _focusWindow : _activeWindow;
        if (Control.FromHandle(target) is Form { ActiveControl: { IsHandleCreated: true } activeControl })
        {
            target = (HWND)(nint)activeControl.Handle;
        }

        if (target == HWND.Null)
        {
            TraceInput($"[InputKeyboardDrop] msg=0x{message:X} reason=no-target");
            return;
        }

        if (message == WM_KEYDOWN
            && Control.FromHandle(target) is ListView
            && (VIRTUAL_KEY)(uint)(nint)wParam is VIRTUAL_KEY.VK_LEFT or VIRTUAL_KEY.VK_RIGHT or VIRTUAL_KEY.VK_UP)
        {
            PlatformApi.Message.SendMessage(target, message, wParam, lParam);
            TraceInput($"[InputKeyboardSend] msg=0x{message:X} target=0x{(nint)target:X} route=listview-pal");
            return;
        }

        if (PlatformApi.Window is ImpellerWindowInterop impellerWindow
            && impellerWindow.TryDispatchInputMessage(target, _activeWindow, message, wParam, lParam))
        {
            return;
        }

        PlatformApi.Message.SendMessage(target, message, wParam, lParam);
        TraceInput($"[InputKeyboardSend] msg=0x{message:X} target=0x{(nint)target:X}");
    }

    private void PostSystemCharToMnemonicTarget(WPARAM wParam)
    {
        HWND target = _activeWindow != HWND.Null ? _activeWindow : _focusWindow;
        if (target == HWND.Null)
        {
            TraceInput("[InputKeyboardDrop] msg=0x106 reason=no-mnemonic-target");
            return;
        }

        if (PlatformApi.Window is ImpellerWindowInterop impellerWindow
            && impellerWindow.TryDispatchInputMessage(target, _activeWindow, WM_SYSCHAR, wParam, (LPARAM)0))
        {
            return;
        }

        PlatformApi.Message.SendMessage(target, WM_SYSCHAR, wParam, (LPARAM)0);
        TraceInput($"[InputKeyboardSend] msg=0x{WM_SYSCHAR:X} target=0x{(nint)target:X}");
    }

    private void PostToMouseTarget(uint message, WPARAM wParam)
    {
        if (message == WM_MOUSEMOVE && ManagedDragDrop.IsInProgress)
        {
            TraceInput("[InputMouseDrop] msg=0x200 reason=managed-drag-in-progress");
            return;
        }

        HWND target = GetMouseTarget();
        if (target == HWND.Null)
        {
            TraceInput($"[InputMouseDrop] msg=0x{message:X} reason=no-target");
            return;
        }

        if (target != HWND.Null)
        {
            System.Drawing.Point cursorPos;
            lock (_inputStateLock)
            {
                cursorPos = _cursorPos;
            }

            System.Drawing.Point clientPoint = cursorPos;
            PlatformApi.Window.ScreenToClient(target, ref clientPoint);
            LPARAM lParam = MakePointLParam(clientPoint);
            string targetType = (Control.FromHandle(target) ?? Control.FromChildHandle(target))?.GetType().Name ?? "<none>";
            if (PlatformApi.Window is ImpellerWindowInterop impellerWindow
                && impellerWindow.TryDispatchInputMessage(target, _activeWindow, message, wParam, lParam))
            {
                TraceInput($"[InputMouseDispatch] msg=0x{message:X} target=0x{(nint)target:X} type={targetType} cursor={cursorPos} client={clientPoint}");
                return;
            }

            PlatformApi.Message.SendMessage(target, message, wParam, lParam);
            TraceInput($"[InputMouseSend] msg=0x{message:X} target=0x{(nint)target:X} type={targetType} cursor={cursorPos} client={clientPoint}");
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

    private bool TryGetDispatchedMouseButtonState(int vKey, out bool pressed)
    {
        pressed = false;
        if (!_dispatchedMouseKeyState.HasValue)
        {
            return false;
        }

        nint mask = vKey switch
        {
            (int)VIRTUAL_KEY.VK_LBUTTON => MK_LBUTTON,
            (int)VIRTUAL_KEY.VK_RBUTTON => MK_RBUTTON,
            (int)VIRTUAL_KEY.VK_MBUTTON => MK_MBUTTON,
            _ => 0
        };

        if (mask == 0)
        {
            return false;
        }

        pressed = (_dispatchedMouseKeyState.Value & mask) != 0;
        return true;
    }

    private HWND GetMouseTarget()
    {
        if (_captureWindow != HWND.Null)
        {
            TraceInput($"[InputMouseTarget] capture=0x{(nint)_captureWindow:X}");
            return _captureWindow;
        }

        System.Drawing.Point cursorPos;
        lock (_inputStateLock)
        {
            cursorPos = _cursorPos;
        }

        HWND hitTarget = PlatformApi.Window.WindowFromPoint(cursorPos);
        if (hitTarget != HWND.Null)
        {
            Control? hitControl = Control.FromHandle(hitTarget);
            if (hitControl is not null && hitControl.ParentInternal is null)
            {
                System.Drawing.Point topLevelClientPoint = cursorPos;
                if (PlatformApi.Window.ScreenToClient(hitTarget, ref topLevelClientPoint))
                {
                    HWND childTarget = PlatformApi.Window.ChildWindowFromPointEx(hitTarget, topLevelClientPoint, 0);
                    if (childTarget != HWND.Null && childTarget != hitTarget)
                    {
                        TraceInput($"[InputMouseTarget] top-level-refine hit=0x{(nint)hitTarget:X} child=0x{(nint)childTarget:X} at={cursorPos}");
                        hitTarget = childTarget;
                    }
                }
            }

            TraceInput($"[InputMouseTarget] hit=0x{(nint)hitTarget:X} at={cursorPos}");
            return hitTarget;
        }

        HWND root = NormalizeMouseRoot(_focusWindow != HWND.Null ? _focusWindow : _activeWindow);
        if (root == HWND.Null)
        {
            foreach (Form form in Application.OpenForms)
            {
                if (form.Visible && form.IsHandleCreated)
                {
                    root = (HWND)(nint)form.Handle;
                    _activeWindow = root;
                    break;
                }
            }
        }

        if (root != HWND.Null)
        {
            if (TryFindDeepestMouseControl(root, cursorPos, out HWND managedChild))
            {
                TraceInput($"[InputMouseTarget] managed-child=0x{(nint)managedChild:X} root=0x{(nint)root:X} at={cursorPos}");
                return managedChild;
            }

            System.Drawing.Point clientPoint = cursorPos;
            if (PlatformApi.Window.ScreenToClient(root, ref clientPoint))
            {
                HWND child = PlatformApi.Window.ChildWindowFromPointEx(root, clientPoint, 0);
                if (child != HWND.Null)
                {
                    TraceInput($"[InputMouseTarget] child=0x{(nint)child:X} root=0x{(nint)root:X} at={cursorPos}");
                    return child;
                }
            }
        }

        TraceInput($"[InputMouseTarget] fallback=0x{(nint)root:X} at={cursorPos}");
        return root;
    }

    private static HWND NormalizeMouseRoot(HWND hwnd)
    {
        if (hwnd == HWND.Null)
        {
            return HWND.Null;
        }

        Control? control = Control.FromHandle(hwnd);
        if (control is null || control.ParentInternal is null)
        {
            return hwnd;
        }

        Control? topLevel = control.TopLevelControl;
        if (topLevel is not null && topLevel.IsHandleCreated)
        {
            return (HWND)(nint)topLevel.Handle;
        }

        Form? form = control.FindForm();
        return form is not null && form.IsHandleCreated
            ? (HWND)(nint)form.Handle
            : hwnd;
    }

    private bool TryFindDeepestMouseControl(HWND root, System.Drawing.Point screenPoint, out HWND hwnd)
    {
        hwnd = HWND.Null;
        Control? rootControl = Control.FromHandle(root);
        Control? hit = null;
        if (rootControl is not null)
        {
            System.Drawing.Point rootClientPoint = rootControl.PointToClient(screenPoint);
            hit = FindDeepestMouseControl(rootControl, rootClientPoint);
        }

        if (hit is null)
        {
            return false;
        }

        hwnd = (HWND)(nint)hit.Handle;
        return true;
    }

    private static Control? FindDeepestMouseControl(Control control, System.Drawing.Point clientPoint)
    {
        if (!control.Visible)
        {
            return null;
        }

        for (int i = 0; i < control.Controls.Count; i++)
        {
            Control childControl = control.Controls[i];
            if (!childControl.Bounds.Contains(clientPoint))
            {
                continue;
            }

            System.Drawing.Point childPoint = new(clientPoint.X - childControl.Left, clientPoint.Y - childControl.Top);
            Control? child = FindDeepestMouseControl(childControl, childPoint);
            if (child is not null)
            {
                return child;
            }
        }

        return control;
    }

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

    private static bool IsMoveOnly(MOUSE_EVENT_FLAGS flags)
    {
        if ((flags & MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE) == 0)
        {
            return false;
        }

        const MOUSE_EVENT_FLAGS moveFlags =
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE
            | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE
            | MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK
            | MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE_NOCOALESCE;

        return (flags & ~moveFlags) == 0;
    }

    private bool IsAltModifierPressedNoLock()
        => _pressedKeys.Contains((int)VIRTUAL_KEY.VK_MENU)
        || _pressedKeys.Contains((int)VIRTUAL_KEY.VK_LMENU)
        || _pressedKeys.Contains((int)VIRTUAL_KEY.VK_RMENU);

    private static bool TryMapVirtualKeyToChar(int virtualKey, out char mappedChar)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            mappedChar = (char)virtualKey;
            return true;
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            mappedChar = (char)virtualKey;
            return true;
        }

        mappedChar = '\0';
        return false;
    }

    private static void TraceInput(string message)
    {
        if (string.IsNullOrWhiteSpace(s_traceFile))
        {
            return;
        }

        try
        {
            File.AppendAllText(s_traceFile, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
