// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller message interop — internal message queue replacing the Win32 message pump.
/// Messages are dispatched through a managed queue rather than the OS.
/// </summary>
internal sealed class ImpellerMessageInterop : IMessageInterop
{
    private const uint WM_QUIT = 0x0012;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_ERASEBKGND = 0x0014;
    private const uint EM_GETSEL = 0x00B0;
    private const uint EM_SETSEL = 0x00B1;
    private const uint EM_SETMODIFY = 0x00B9;
    private const uint EM_REPLACESEL = 0x00C2;
    private const uint EM_LIMITTEXT = 0x00C5;
    private const uint EM_SETREADONLY = 0x00CF;

    private readonly ConcurrentQueue<ImpellerMessage> _messageQueue = new();
    private readonly Dictionary<string, uint> _registeredMessages = [];
    private readonly Dictionary<HWND, TextSelectionState> _textSelections = [];
    private uint _nextRegisteredMessage = 0xC000; // WM_APP range

    public LRESULT SendMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.MessageDispatch,
            $"SendMessage hwnd=0x{(nint)hWnd:X} msg=0x{msg:X}");

        if (TryHandleTextBoxMessage(hWnd, msg, wParam, lParam, out LRESULT textResult))
        {
            return textResult;
        }

        // Synchronous dispatch — look up the NativeWindow for this HWND and invoke
        // its WndProc directly (wxWidgets-style synthetic message dispatch).
        // WM_PAINT flows through the standard WinForms WmPaint pipeline:
        //   BeginPaintScope → ImpellerGdiInterop.BeginPaint (acquires Impeller surface)
        //   → PaintEventArgs → Graphics.FromHdcInternal (creates backend-backed Graphics)
        //   → OnPaint → EndPaint (presents frame)
        if (NativeWindow.DispatchMessageDirect(hWnd, msg, wParam, lParam, out LRESULT result))
        {
            return result;
        }

        return (LRESULT)0;
    }

    public LRESULT SendMessage(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam)
        => SendMessage(hWnd, (uint)msg, wParam, lParam);

    public bool PostMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        _messageQueue.Enqueue(new ImpellerMessage(hWnd, msg, wParam, lParam));
        return true;
    }

    public bool PeekMessage(out MSG lpMsg, HWND hWnd, uint filterMin, uint filterMax, PEEK_MESSAGE_REMOVE_TYPE flags)
    {
        if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
        {
            windowInterop.PumpEvents();
        }

        if (_messageQueue.TryPeek(out var im))
        {
            lpMsg = im.ToMSG();
            if (flags.HasFlag(PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
            {
                _messageQueue.TryDequeue(out _);
            }

            return true;
        }

        lpMsg = default;
        return false;
    }

    public bool GetMessage(out MSG lpMsg, HWND hWnd, uint filterMin, uint filterMax)
    {
        // Blocking — spin until a message arrives
        // TODO: Use a proper wait handle for efficiency
        ImpellerMessage im;
        SpinWait spin = default;
        while (!_messageQueue.TryDequeue(out im))
        {
            if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
            {
                windowInterop.PumpEvents();
            }

            spin.SpinOnce();
        }

        lpMsg = im.ToMSG();
        return lpMsg.message != WM_QUIT;
    }

    public bool TranslateMessage(in MSG lpMsg) => true; // No-op in Impeller
    public LRESULT DispatchMessage(in MSG lpMsg) => SendMessage(lpMsg.hwnd, lpMsg.message, lpMsg.wParam, lpMsg.lParam);

    public unsafe bool SendMessageTimeout(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam,
        SEND_MESSAGE_TIMEOUT_FLAGS flags, uint timeout, nuint* result)
    {
        var r = SendMessage(hWnd, msg, wParam, lParam);
        if (result != null)
            *result = (nuint)(nint)r;
        return true;
    }

    public bool SendMessageCallback(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, Action callback)
    {
        SendMessage(hWnd, msg, wParam, lParam);
        callback();
        return true;
    }

    public uint RegisterWindowMessage(string name)
    {
        if (!_registeredMessages.TryGetValue(name, out var id))
        {
            id = _nextRegisteredMessage++;
            _registeredMessages[name] = id;
        }

        return id;
    }

    /// <summary>
    /// Drain and dispatch all pending synthetic WinForms messages.
    /// Called from Silk.NET's Update callback.
    /// </summary>
    internal void ProcessPendingMessages()
    {
        using var guard = WinFormsXExecutionGuard.Enter(
            WinFormsXExecutionKind.MessageDispatch,
            "ProcessPendingMessages");

        // Process a bounded batch to avoid blocking the render loop
        int maxMessages = 64;
        while (maxMessages-- > 0 && _messageQueue.TryDequeue(out var im))
        {
            var msg = im.ToMSG();
            if (msg.message == WM_QUIT)
            {
                // Close the Silk.NET window, which causes Run() to return
                if (PlatformApi.Window is ImpellerWindowInterop windowInterop)
                {
                    foreach (var state in windowInterop.GetAllWindows())
                    {
                        state.SilkWindow?.Close();
                    }
                }

                return;
            }

            // Skip paint messages — the Render callback is the sole painter.
            // Processing WM_PAINT here would race with the GPU surface.
            if (msg.message is WM_PAINT or WM_ERASEBKGND)
            {
                continue;
            }

            // Dispatch through WinForms NativeWindow WndProc
            NativeWindow.DispatchMessageDirect(msg.hwnd, msg.message, msg.wParam, msg.lParam, out _);
        }
    }

    private unsafe bool TryHandleTextBoxMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, out LRESULT result)
    {
        result = (LRESULT)0;
        if (Control.FromHandle(hWnd) is not TextBoxBase textBox)
        {
            return false;
        }

        switch (msg)
        {
            case EM_GETSEL:
            {
                GetTextSelection(hWnd, textBox, out int start, out int length);
                int end = Math.Min(textBox.Text.Length, start + length);
                if ((nint)wParam != 0)
                {
                    *(int*)(nint)wParam = start;
                }

                if ((nint)lParam != 0)
                {
                    *(int*)(nint)lParam = end;
                }

                uint packed = unchecked((uint)((start & 0xFFFF) | ((end & 0xFFFF) << 16)));
                result = (LRESULT)(nint)packed;
                return true;
            }

            case EM_SETSEL:
            {
                int textLength = textBox.Text.Length;
                int start = (int)(nint)wParam;
                int end = (int)(nint)lParam;
                if (start < 0)
                {
                    start = textLength;
                }

                if (end < 0)
                {
                    end = textLength;
                }

                start = Math.Clamp(start, 0, textLength);
                end = Math.Clamp(end, 0, textLength);
                if (end < start)
                {
                    (start, end) = (end, start);
                }

                _textSelections[hWnd] = new TextSelectionState(start, end - start);
                return true;
            }

            case EM_REPLACESEL:
            {
                if (textBox.ReadOnly)
                {
                    return true;
                }

                string replacement = (nint)lParam == 0 ? string.Empty : new string((char*)(nint)lParam);
                GetTextSelection(hWnd, textBox, out int start, out int length);
                string oldText = textBox.Text;
                start = Math.Clamp(start, 0, oldText.Length);
                length = Math.Clamp(length, 0, oldText.Length - start);

                int allowed = Math.Max(0, textBox.MaxLength - (oldText.Length - length));
                if (replacement.Length > allowed)
                {
                    replacement = replacement[..allowed];
                }

                textBox.Text = string.Concat(oldText.AsSpan(0, start), replacement, oldText.AsSpan(start + length));
                _textSelections[hWnd] = new TextSelectionState(start + replacement.Length, 0);
                return true;
            }

            case EM_LIMITTEXT:
            case EM_SETMODIFY:
            case EM_SETREADONLY:
                return true;
        }

        return false;
    }

    private void GetTextSelection(HWND hWnd, TextBoxBase textBox, out int start, out int length)
    {
        if (!_textSelections.TryGetValue(hWnd, out TextSelectionState selection))
        {
            selection = new TextSelectionState(0, 0);
        }

        int textLength = textBox.Text.Length;
        start = Math.Clamp(selection.Start, 0, textLength);
        length = Math.Clamp(selection.Length, 0, textLength - start);
    }

    private readonly record struct TextSelectionState(int Start, int Length);
}

/// <summary>
/// Internal message representation for the Impeller message queue.
/// </summary>
internal readonly record struct ImpellerMessage(HWND HWnd, uint Msg, WPARAM WParam, LPARAM LParam)
{
    public MSG ToMSG() => new()
    {
        hwnd = HWnd,
        message = Msg,
        wParam = WParam,
        lParam = LParam,
    };
}
