// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Office;

namespace System.Windows.Forms;

public sealed partial class Application
{
    /// <summary>
    ///  Lighter weight <see cref="ThreadContext"/> that doesn't support <see cref="IMsoComponent"/>.
    /// </summary>
    internal sealed unsafe class LightThreadContext : ThreadContext
    {
        protected override bool? GetMessageLoopInternal(bool mustBeActive, int loopCount)
        {
            // If we are already running a loop, we're fine.
            if (loopCount > 0)
            {
                return true;
            }

            return null;
        }

        protected override bool RunMessageLoop(msoloop reason, bool fullModal) => FPushMessageLoop(reason);

        public BOOL FContinueMessageLoop(
            msoloop uReason,
            MSG* pMsgPeeked)
        {
            Debug.Assert(uReason != msoloop.FocusWait);

            bool continueLoop = true;

            // If we get a null message, and we have previously posted the WM_QUIT message,
            // then someone ate the message.
            if (pMsgPeeked is null && PostedQuit)
            {
                continueLoop = false;
            }
            else
            {
                switch (uReason)
                {
                    case msoloop.ModalAlert:
                    case msoloop.ModalForm:

                        // For modal forms, check to see if the current active form has been
                        // dismissed.  If there is no active form, then it is an error that
                        // we got into here, so we terminate the loop.

                        if (CurrentForm is not { } form || form.CheckCloseDialog(closingOnly: false))
                        {
                            continueLoop = false;
                        }

                        break;

                    case msoloop.DoEvents:
                    case msoloop.DoEventsModal:
                        // For DoEvents, just see if there are more messages on the queue.
                        MSG temp = default;
                        if (!PInvoke.PeekMessage(&temp, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE))
                        {
                            continueLoop = false;
                        }

                        break;
                }
            }

            return continueLoop;
        }

        private BOOL FPushMessageLoop(msoloop uReason)
        {
            // For the main message loop in Impeller mode, delegate to Silk.NET's
            // native event loop. This properly handles window move, resize, close,
            // and all OS interactions. WinForms synthetic messages are processed
            // from the Update callback.
            if (uReason == msoloop.Main
                && Platform.PlatformApi.Window is Platform.ImpellerWindowInterop windowInterop)
            {
                windowInterop.RunMainLoop();
                FromCurrent().DisposeThreadWindows();
                return true;
            }

            // Fallback: manual loop for DoEvents, modal forms, etc.
            BOOL continueLoop = true;
            MSG msg = default;

            while (true)
            {
                if (PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE))
                {
                    if (!FContinueMessageLoop(uReason, &msg))
                    {
                        return true;
                    }

                    // If the component wants us to process the message, do it.
                    PInvoke.GetMessage(&msg, HWND.Null, 0, 0);

                    if (msg.message == PInvoke.WM_QUIT)
                    {
                        FromCurrent().DisposeThreadWindows();

                        if (uReason != msoloop.Main)
                        {
                            PInvoke.PostQuitMessage((int)msg.wParam);
                        }

                        return true;
                    }

                    // Now translate and dispatch the message.
                    if (!PreTranslateMessage(ref msg))
                    {
                        PInvoke.TranslateMessage(msg);
                        PInvoke.DispatchMessage(&msg);
                    }
                }
                else
                {
                    // If this is a DoEvents loop, then get out. There's nothing left for us to do.
                    if (uReason is msoloop.DoEvents or msoloop.DoEventsModal)
                    {
                        break;
                    }

                    // Nothing is on the message queue. Perform idle processing.
                    _idleHandler?.Invoke(Thread.CurrentThread, EventArgs.Empty);

                    // Give the component one more chance to terminate the message loop.
                    if (!FContinueMessageLoop(uReason, pMsgPeeked: null))
                    {
                        return true;
                    }

                    if (!PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE))
                    {
                        Thread.Sleep(1);
                    }
                }
            }

            return !continueLoop;
        }
    }
}
