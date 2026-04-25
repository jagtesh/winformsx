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
    private readonly ConcurrentQueue<ImpellerMessage> _messageQueue = new();
    private readonly Dictionary<string, uint> _registeredMessages = [];
    private uint _nextRegisteredMessage = 0xC000; // WM_APP range

    public LRESULT SendMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // Synchronous dispatch — look up the NativeWindow for this HWND and invoke
        // its WndProc directly (wxWidgets-style synthetic message dispatch).
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
            spin.SpinOnce();
        }

        lpMsg = im.ToMSG();
        return lpMsg.message != PInvoke.WM_QUIT;
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

