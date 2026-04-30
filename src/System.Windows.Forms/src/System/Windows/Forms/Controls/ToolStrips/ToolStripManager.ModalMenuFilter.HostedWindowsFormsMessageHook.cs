// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms;

public static partial class ToolStripManager
{
    internal partial class ModalMenuFilter
    {
        private class HostedWindowsFormsMessageHook
        {
            private HHOOK _messageHookHandle;
            private bool _isHooked;
            private HOOKPROC? _callBack;
            private Timer? _hostedMessagePumpTimer;
            private const int HostedPumpIntervalMs = 15;
            private const int MaxPumpBatchSize = 128;

            public HostedWindowsFormsMessageHook()
            {
#if DEBUG
                _callingStack = Environment.StackTrace;
#endif
            }

#if DEBUG
            private readonly string _callingStack;

#pragma warning disable CA1821 // Remove empty Finalizers
            ~HostedWindowsFormsMessageHook()
            {
                Debug.Assert(
                    _messageHookHandle == IntPtr.Zero,
                    $"Finalizing an active mouse hook. This will crash the process. Calling stack: {_callingStack}");
            }
#pragma warning restore CA1821
#endif

            public bool HookMessages
            {
                get => !_messageHookHandle.IsNull;
                set
                {
                    if (value)
                    {
                        InstallMessageHook();
                    }
                    else
                    {
                        UninstallMessageHook();
                    }
                }
            }

            private unsafe void InstallMessageHook()
            {
                lock (this)
                {
                    if (!_messageHookHandle.IsNull)
                    {
                        return;
                    }

                    _callBack = MessageHookProc;
                    IntPtr hook = Marshal.GetFunctionPointerForDelegate(_callBack);
                    _messageHookHandle = PInvoke.SetWindowsHookEx(
                        WINDOWS_HOOK_ID.WH_GETMESSAGE,
                        (delegate* unmanaged[Stdcall]<int, WPARAM, LPARAM, LRESULT>)hook,
                        (HINSTANCE)0,
                        PInvoke.GetCurrentThreadId());

                    if (_messageHookHandle != IntPtr.Zero)
                    {
                        _isHooked = true;
                    }

                    EnsureHostedMessagePump(enabled: true);
                    Debug.Assert(_messageHookHandle != IntPtr.Zero, "Failed to install mouse hook");
                }
            }

            private unsafe LRESULT MessageHookProc(int nCode, WPARAM wparam, LPARAM lparam)
            {
                if (nCode == PInvoke.HC_ACTION && _isHooked
                    && (PEEK_MESSAGE_REMOVE_TYPE)(nuint)wparam == PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE)
                {
                    // Only process messages we've pulled off the queue.
                    MSG* msg = (MSG*)(nint)lparam;
                    if (msg is not null)
                    {
                        // Call pretranslate on the message to execute the message filters and preprocess message.
                        if (Application.ThreadContext.FromCurrent().PreTranslateMessage(ref *msg))
                        {
                            msg->message = PInvoke.WM_NULL;
                        }
                    }
                }

                return PInvoke.CallNextHookEx(_messageHookHandle, nCode, wparam, lparam);
            }

            private void UninstallMessageHook()
            {
                lock (this)
                {
                    EnsureHostedMessagePump(enabled: false);

                    if (!_messageHookHandle.IsNull)
                    {
                        PInvoke.UnhookWindowsHookEx(_messageHookHandle);
                        _messageHookHandle = default;
                    }

                    _isHooked = false;
                }
            }

            private void EnsureHostedMessagePump(bool enabled)
            {
                if (enabled)
                {
                    _hostedMessagePumpTimer ??= CreateHostedMessagePumpTimer();
                    _hostedMessagePumpTimer.Enabled = true;
                    return;
                }

                if (_hostedMessagePumpTimer is not null)
                {
                    _hostedMessagePumpTimer.Enabled = false;
                    _hostedMessagePumpTimer.Dispose();
                    _hostedMessagePumpTimer = null;
                }
            }

            private Timer CreateHostedMessagePumpTimer()
            {
                Timer timer = new()
                {
                    Interval = HostedPumpIntervalMs
                };

                timer.Tick += (_, _) => PumpHostedMessages();
                return timer;
            }

            private unsafe void PumpHostedMessages()
            {
                if (!_isHooked)
                {
                    return;
                }

                for (int i = 0; i < MaxPumpBatchSize; i++)
                {
                    MSG msg = default;
                    if (!PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
                    {
                        break;
                    }

                    if (msg.message == PInvoke.WM_NULL)
                    {
                        continue;
                    }

                    if (Application.ThreadContext.FromCurrent().PreTranslateMessage(ref msg))
                    {
                        continue;
                    }

                    PInvoke.TranslateMessage(&msg);
                    PInvoke.DispatchMessage(&msg);
                }
            }
        }
    }
}
