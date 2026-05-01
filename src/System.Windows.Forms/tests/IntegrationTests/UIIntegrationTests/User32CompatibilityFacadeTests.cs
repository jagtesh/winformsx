// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows.Forms.Platform;
using System.Windows.Forms.UITests.Input;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input.Ime;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace System.Windows.Forms.UITests;

public class User32CompatibilityFacadeTests
{
    private const uint SPIF_SENDCHANGE = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    [Fact]
    public void CommonSystemMetrics_ResolveConsistentlyForManagedAndNativeUser32Facade()
    {
        SYSTEM_METRICS_INDEX[] metrics =
        [
            SYSTEM_METRICS_INDEX.SM_CXSCREEN,
            SYSTEM_METRICS_INDEX.SM_CYSCREEN,
            SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN,
            SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN,
            SYSTEM_METRICS_INDEX.SM_CXICONSPACING,
            SYSTEM_METRICS_INDEX.SM_CYICONSPACING,
            SYSTEM_METRICS_INDEX.SM_CXDRAG,
            SYSTEM_METRICS_INDEX.SM_CYDRAG,
            SYSTEM_METRICS_INDEX.SM_CXMINTRACK,
            SYSTEM_METRICS_INDEX.SM_CYMINTRACK,
            SYSTEM_METRICS_INDEX.SM_CMOUSEBUTTONS,
            SYSTEM_METRICS_INDEX.SM_CMONITORS,
        ];

        foreach (SYSTEM_METRICS_INDEX metric in metrics)
        {
            int managed = PInvoke.GetSystemMetrics(metric);
            int native = NativeUser32.GetSystemMetrics((int)metric);
            Assert.Equal(managed, native);
            Assert.True(managed > 0, $"Expected a positive metric value for {metric}, got {managed}.");
        }
    }

    [Fact]
    public void DirectDllImports_RouteToWinFormsXPal()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            Assert.True(NativeUser32.SetCursorPos(321, 654));
            Assert.True(NativeUser32.GetCursorPos(out Point cursor));
            Assert.Equal(new Point(321, 654), cursor);
            Assert.Equal(PInvoke.GetMessagePos(), NativeUser32.GetMessagePos());
            unsafe
            {
                uint managedWait = PInvoke.MsgWaitForMultipleObjectsEx(
                    0,
                    null,
                    0,
                    QUEUE_STATUS_FLAGS.QS_ALLINPUT,
                    MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
                uint nativeWait = NativeUser32.MsgWaitForMultipleObjectsEx(
                    0,
                    null,
                    0,
                    (uint)QUEUE_STATUS_FLAGS.QS_ALLINPUT,
                    (uint)MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
                Assert.Equal(managedWait, nativeWait);
            }

            const string registeredMessageName = "WinFormsX.User32.MessageQueueSlice";
            uint registeredWindowMessage = NativeUser32.RegisterWindowMessageW(registeredMessageName);
            Assert.True(registeredWindowMessage >= 0xC000);
            Assert.Equal(registeredWindowMessage, NativeUser32.RegisterWindowMessageW(registeredMessageName));
            Assert.Equal(registeredWindowMessage, NativeUser32.RegisterWindowMessageA(registeredMessageName));

            const uint PM_REMOVE = 0x0001;
            for (int i = 0; i < 16; i++)
            {
                if (!NativeUser32.PeekMessageW(out _, nint.Zero, 0, 0, PM_REMOVE))
                {
                    break;
                }
            }

            Assert.Equal(
                NativeUser32.SendMessageW(nint.Zero, registeredWindowMessage, (nint)0x11, (nint)0x22),
                NativeUser32.SendMessageA(nint.Zero, registeredWindowMessage, (nint)0x11, (nint)0x22));

            Assert.True(NativeUser32.PostMessageW(nint.Zero, registeredWindowMessage, (nint)0x44, (nint)0x55));
            Assert.True(NativeUser32.PeekMessageW(out NativeUser32.MSG peekW, nint.Zero, 0, 0, PM_REMOVE));
            Assert.Equal(registeredWindowMessage, peekW._message);
            Assert.Equal((nuint)0x44, peekW._wParam);
            Assert.Equal((nint)0x55, peekW._lParam);

            Assert.True(NativeUser32.PostMessageA(nint.Zero, registeredWindowMessage, (nint)0x66, (nint)0x77));
            Assert.True(NativeUser32.PeekMessageA(out NativeUser32.MSG peekA, nint.Zero, 0, 0, PM_REMOVE));
            Assert.Equal(registeredWindowMessage, peekA._message);
            Assert.Equal((nuint)0x66, peekA._wParam);
            Assert.Equal((nint)0x77, peekA._lParam);

            Assert.True(NativeUser32.PostMessageW(nint.Zero, registeredWindowMessage, (nint)0x88, (nint)0x99));
            Assert.True(NativeUser32.GetMessageW(out NativeUser32.MSG getW, nint.Zero, 0, 0));
            Assert.Equal(registeredWindowMessage, getW._message);
            Assert.Equal((nuint)0x88, getW._wParam);
            Assert.Equal((nint)0x99, getW._lParam);

            Assert.True(NativeUser32.PostMessageA(nint.Zero, registeredWindowMessage, (nint)0xAA, (nint)0xBB));
            Assert.True(NativeUser32.GetMessageA(out NativeUser32.MSG getA, nint.Zero, 0, 0));
            Assert.Equal(registeredWindowMessage, getA._message);
            Assert.Equal((nuint)0xAA, getA._wParam);
            Assert.Equal((nint)0xBB, getA._lParam);

            NativeUser32.MSG nativeDispatchMessage = new()
            {
                _hwnd = nint.Zero,
                _message = registeredWindowMessage,
                _wParam = (nuint)0xA1,
                _lParam = (nint)0xB2,
                _time = 1,
                _pt = new Point(7, 9),
            };
            MSG managedDispatchMessage = new()
            {
                hwnd = HWND.Null,
                message = registeredWindowMessage,
                wParam = (WPARAM)(nuint)0xA1,
                lParam = (LPARAM)(nint)0xB2,
                time = 1,
                pt = new Point(7, 9),
            };

            Assert.Equal((bool)PInvoke.TranslateMessage(managedDispatchMessage), NativeUser32.TranslateMessage(ref nativeDispatchMessage));

            unsafe
            {
                MSG managedDispatchCopy = managedDispatchMessage;
                nint managedDispatchResult = (nint)PInvoke.DispatchMessage(&managedDispatchCopy);
                Assert.Equal(managedDispatchResult, NativeUser32.DispatchMessageW(ref nativeDispatchMessage));
                Assert.Equal(managedDispatchResult, NativeUser32.DispatchMessageA(ref nativeDispatchMessage));
            }

            uint virtualKey = (uint)VIRTUAL_KEY.VK_RETURN;
            uint nativeScan = NativeUser32.MapVirtualKey(virtualKey, (uint)MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
            uint managedScan = PInvoke.MapVirtualKey(virtualKey, MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
            Assert.Equal(managedScan, nativeScan);

            int nativeWidth = NativeUser32.GetSystemMetrics((int)SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            int managedWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            Assert.Equal(managedWidth, nativeWidth);

            Assert.NotEqual(nint.Zero, NativeUser32.GetDesktopWindow());

            nint first = 0x1234;
            nint second = 0x5678;
            _ = NativeUser32.SetFocus(first);
            Assert.Equal(first, NativeUser32.GetFocus());
            Assert.Equal(first, NativeUser32.SetFocus(second));
            Assert.Equal(second, NativeUser32.GetFocus());

            unsafe
            {
                Span<INPUT> inputs =
                [
                    InputBuilder.KeyDown(VIRTUAL_KEY.VK_RETURN),
                    InputBuilder.KeyUp(VIRTUAL_KEY.VK_RETURN),
                ];

                fixed (INPUT* input = inputs)
                {
                    Assert.Equal((uint)inputs.Length, NativeUser32.SendInput((uint)inputs.Length, input, Marshal.SizeOf<INPUT>()));
                }
            }

            Assert.Equal(PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RETURN), NativeUser32.GetAsyncKeyState((int)VIRTUAL_KEY.VK_RETURN));
            Assert.Equal(PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_RETURN), NativeUser32.GetKeyState((int)VIRTUAL_KEY.VK_RETURN));

            const string clipboardFormatName = "WinFormsX.User32CompatibilityFacadeTests";
            uint registeredFormat = NativeUser32.RegisterClipboardFormat(clipboardFormatName);
            Assert.NotEqual(0u, registeredFormat);
            Assert.Equal(registeredFormat, NativeUser32.RegisterClipboardFormat(clipboardFormatName.ToUpperInvariant()));

            char[] clipboardFormatNameBuffer = new char[128];
            int clipboardFormatNameLength = NativeUser32.GetClipboardFormatName(
                registeredFormat,
                clipboardFormatNameBuffer,
                clipboardFormatNameBuffer.Length);
            Assert.Equal(clipboardFormatName.Length, clipboardFormatNameLength);
            Assert.Equal(clipboardFormatName, new string(clipboardFormatNameBuffer, 0, clipboardFormatNameLength));

            nint clipboardText = Marshal.StringToHGlobalUni("WinFormsX clipboard text");
            try
            {
                uint unicodeTextFormat = CF_UNICODETEXT;
                Assert.True(NativeUser32.OpenClipboard(nint.Zero));
                Assert.True(NativeUser32.EmptyClipboard());
                Assert.False(NativeUser32.IsClipboardFormatAvailable(unicodeTextFormat));
                Assert.Equal(clipboardText, NativeUser32.SetClipboardData(unicodeTextFormat, clipboardText));
                Assert.True(NativeUser32.IsClipboardFormatAvailable(unicodeTextFormat));
                Assert.Equal(clipboardText, NativeUser32.GetClipboardData(unicodeTextFormat));
                Assert.True(NativeUser32.CloseClipboard());
            }
            finally
            {
                Marshal.FreeHGlobal(clipboardText);
            }

            byte[] managedKeyState = new byte[256];
            byte[] nativeKeyState = new byte[256];
            unsafe
            {
                fixed (byte* managed = managedKeyState)
                fixed (byte* native = nativeKeyState)
                {
                    managedKeyState.AsSpan().Fill(0xFF);
                    nativeKeyState.AsSpan().Fill(0xFF);

                    Assert.Equal((bool)PInvoke.GetKeyboardState(managed), NativeUser32.GetKeyboardState(native));
                    Assert.Equal(managedKeyState, nativeKeyState);
                }

                nint managedLayout = (nint)PInvoke.GetKeyboardLayout(0);
                nint nativeLayout = NativeUser32.GetKeyboardLayout(0);
                Assert.Equal(managedLayout, nativeLayout);
                Assert.NotEqual(nint.Zero, nativeLayout);

                HKL* managedLayouts = stackalloc HKL[4];
                nint* nativeLayouts = stackalloc nint[4];
                int managedLayoutCount = PInvoke.GetKeyboardLayoutList(4, managedLayouts);
                int nativeLayoutCount = NativeUser32.GetKeyboardLayoutList(4, nativeLayouts);
                Assert.Equal(managedLayoutCount, nativeLayoutCount);
                Assert.True(nativeLayoutCount > 0);
                Assert.Equal((nint)managedLayouts[0], nativeLayouts[0]);
            }

            using Form form = new();
            using Button child = new();
            form.Controls.Add(child);
            form.CreateControl();
            child.CreateControl();

            nint formHandle = form.Handle;
            nint childHandle = child.Handle;

            nint originalCapture = NativeUser32.GetCapture();
            Assert.Equal(nint.Zero, NativeUser32.SetCapture(formHandle));
            Assert.Equal(formHandle, NativeUser32.GetCapture());
            Assert.Equal(formHandle, NativeUser32.SetCapture(formHandle));
            Assert.Equal(formHandle, NativeUser32.GetCapture());
            Assert.True(NativeUser32.ReleaseCapture());
            Assert.Equal(nint.Zero, NativeUser32.GetCapture());

            if (originalCapture != nint.Zero)
            {
                _ = NativeUser32.SetCapture(originalCapture);
                Assert.Equal(originalCapture, NativeUser32.GetCapture());
                Assert.True(NativeUser32.ReleaseCapture());
            }

            RECT rect;
            RECT clientRect;
            Assert.True(NativeUser32.GetWindowRect(formHandle, out rect));
            Assert.True(NativeUser32.GetClientRect(formHandle, out clientRect));
            Assert.True(PInvoke.GetWindowRect((HWND)formHandle, out RECT managedWindowRect));
            Assert.True(PInvoke.GetClientRect((HWND)formHandle, out RECT managedClientRect));
            Assert.True(NativeUser32.GetWindowRect(formHandle, out rect));
            Assert.True(NativeUser32.GetClientRect(formHandle, out clientRect));
            Assert.Equal(managedWindowRect.left, rect.left);
            Assert.Equal(managedWindowRect.top, rect.top);
            Assert.Equal(managedClientRect.left, clientRect.left);
            Assert.Equal(managedClientRect.top, clientRect.top);
            Assert.Equal((bool)PInvoke.GetWindowRect((HWND)formHandle, out managedWindowRect), (bool)NativeUser32.GetWindowRect(formHandle, out rect));
            Assert.Equal((bool)PInvoke.GetClientRect((HWND)formHandle, out managedClientRect), (bool)NativeUser32.GetClientRect(formHandle, out clientRect));
            unsafe
            {
                WINDOWPLACEMENT managedPlacement = new()
                {
                    length = (uint)sizeof(WINDOWPLACEMENT),
                };
                WINDOWPLACEMENT nativePlacement = new()
                {
                    length = (uint)sizeof(WINDOWPLACEMENT),
                };

                Assert.True(PInvoke.GetWindowPlacement((HWND)formHandle, &managedPlacement));
                Assert.True(NativeUser32.GetWindowPlacement(formHandle, &nativePlacement));
                Assert.Equal(managedPlacement.showCmd, nativePlacement.showCmd);
                Assert.Equal(managedPlacement.rcNormalPosition.left, nativePlacement.rcNormalPosition.left);
                Assert.Equal(managedPlacement.rcNormalPosition.top, nativePlacement.rcNormalPosition.top);

                nativePlacement.flags = WINDOWPLACEMENT_FLAGS.WPF_SETMINPOSITION;
                nativePlacement.ptMinPosition.X = 17;
                nativePlacement.ptMinPosition.Y = 19;
                Assert.True(NativeUser32.SetWindowPlacement(childHandle, &nativePlacement));

                WINDOWPLACEMENT afterNativeSet = new()
                {
                    length = (uint)sizeof(WINDOWPLACEMENT),
                };
                Assert.True(PInvoke.GetWindowPlacement((HWND)childHandle, &afterNativeSet));
                Assert.Equal(17, afterNativeSet.ptMinPosition.X);
                Assert.Equal(19, afterNativeSet.ptMinPosition.Y);
            }

            Point point = new(10, 10);
            Point managedPoint = point;
            Assert.Equal((bool)PInvoke.ClientToScreen(form, ref managedPoint), NativeUser32.ClientToScreen(formHandle, ref point));
            Assert.Equal(managedPoint, point);

            Point screenPoint = managedPoint;
            Point managedScreenPoint = screenPoint;
            Assert.Equal((bool)PInvoke.ScreenToClient(form, ref managedScreenPoint), NativeUser32.ScreenToClient(formHandle, ref screenPoint));
            Assert.Equal(managedScreenPoint, screenPoint);

            var mappedPoint = new Point(1, 2);
            var mappedPointExpected = mappedPoint;
            int managedMap = PInvoke.MapWindowPoints(form, HWND.Null, ref mappedPoint);
            int nativeMap = NativeUser32.MapWindowPoints(formHandle, nint.Zero, ref mappedPointExpected, 1);
            Assert.Equal(managedMap, nativeMap);
            Assert.Equal(mappedPoint, mappedPointExpected);

            Point testPoint = form.PointToScreen(new Point(5, 5));
            nint managedWindowFromPoint = (nint)PInvoke.WindowFromPoint(testPoint);
            nint nativeWindowFromPoint = NativeUser32.WindowFromPoint(testPoint);
            Assert.Equal(managedWindowFromPoint, nativeWindowFromPoint);

            nint managedChildFromPoint = (nint)PInvoke.ChildWindowFromPointEx(form, new Point(5, 5), CWP_FLAGS.CWP_SKIPINVISIBLE);
            nint nativeChildFromPoint = NativeUser32.ChildWindowFromPointEx(formHandle, new Point(5, 5), (uint)CWP_FLAGS.CWP_SKIPINVISIBLE);
            Assert.Equal(managedChildFromPoint, nativeChildFromPoint);
            Assert.Equal((nint)PInvoke.GetMenu(form), NativeUser32.GetMenu(formHandle));
            Assert.Equal((nint)PInvoke.GetSystemMenu(form, bRevert: false), NativeUser32.GetSystemMenu(formHandle, false));
            Assert.Equal(PInvoke.GetMenuItemCount(HMENU.Null), NativeUser32.GetMenuItemCount((nint)HMENU.Null));

            MENUITEMINFOW menuItemInfo = new()
            {
                cbSize = (uint)Marshal.SizeOf<MENUITEMINFOW>(),
            };

            Assert.Equal((bool)PInvoke.EnableMenuItem(HMENU.Null, 0, MENU_ITEM_FLAGS.MF_BYPOSITION), NativeUser32.EnableMenuItem((nint)HMENU.Null, 0, (uint)MENU_ITEM_FLAGS.MF_BYPOSITION));
            Assert.Equal((bool)PInvoke.GetMenuItemInfo(HMENU.Null, 0, fByPosition: false, ref menuItemInfo), NativeUser32.GetMenuItemInfo((nint)HMENU.Null, 0, fByPosition: false, ref menuItemInfo));
            Assert.Equal((bool)PInvoke.SetMenu(form, HMENU.Null), NativeUser32.SetMenu(formHandle, (nint)HMENU.Null));
            Assert.True(PInvoke.DrawMenuBar(form));
            Assert.True(NativeUser32.DrawMenuBar(formHandle));

            Assert.Equal((nint)PInvoke.GetWindowLong((HWND)formHandle, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT), NativeUser32.GetParent(formHandle));
            nint originalParent = NativeUser32.GetParent(childHandle);
            nint oldParent = NativeUser32.SetParent(childHandle, formHandle);
            Assert.Equal(originalParent, oldParent);
            Assert.Equal(formHandle, NativeUser32.GetParent(childHandle));
            _ = NativeUser32.SetParent(childHandle, oldParent);
            Assert.Equal(originalParent, NativeUser32.GetParent(childHandle));

            Assert.Equal((nint)PInvoke.GetAncestor((HWND)formHandle, GET_ANCESTOR_FLAGS.GA_ROOT), NativeUser32.GetAncestor(formHandle, (uint)GET_ANCESTOR_FLAGS.GA_ROOT));
            Assert.Equal((nint)PInvoke.GetWindow((HWND)childHandle, GET_WINDOW_CMD.GW_OWNER), NativeUser32.GetWindow(childHandle, (uint)GET_WINDOW_CMD.GW_OWNER));
            Assert.Equal((bool)PInvoke.IsChild((HWND)formHandle, (HWND)childHandle), NativeUser32.IsChild(formHandle, childHandle));

            Assert.True(NativeUser32.UpdateWindow(formHandle));
            Assert.True(NativeUser32.InvalidateRect(formHandle, nint.Zero, true));
            Assert.True(NativeUser32.InvalidateRect(formHandle, ref rect, true));
            Assert.True(NativeUser32.ValidateRect(formHandle, nint.Zero));
            Assert.True(NativeUser32.ValidateRect(formHandle, ref clientRect));

            nint icon = NativeUser32.LoadIcon(nint.Zero, (nint)32512);
            Assert.NotEqual(nint.Zero, icon);
            Assert.True(NativeUser32.GetIconInfo(icon, out NativeUser32.ICONINFO iconInfo));
            Assert.True(NativeUser32.DrawIcon(nint.Zero, 0, 0, icon));
            Assert.True(NativeUser32.DrawIconEx(nint.Zero, 0, 0, icon, 16, 16, 0, nint.Zero, 3));
            nint iconCopy = NativeUser32.CopyIcon(icon);
            Assert.NotEqual(nint.Zero, iconCopy);
            nint imageCopy = NativeUser32.CopyImage(icon, 1, 16, 16, 0);
            Assert.NotEqual(nint.Zero, imageCopy);
            Assert.True(NativeUser32.DestroyIcon(imageCopy));
            Assert.True(NativeUser32.DestroyIcon(iconCopy));
            Assert.True(NativeUser32.DestroyIcon(icon));

            nint cursorHandle = NativeUser32.LoadCursor(nint.Zero, (nint)32512);
            Assert.NotEqual(nint.Zero, cursorHandle);
            nint cursorCopy = NativeUser32.CopyCursor(cursorHandle);
            Assert.NotEqual(nint.Zero, cursorCopy);
            Assert.True(NativeUser32.DestroyCursor(cursorCopy));
            Assert.True(NativeUser32.DestroyCursor(cursorHandle));

            unsafe
            {
                byte marker = 0;
                nint resourceIcon = NativeUser32.CreateIconFromResourceEx(&marker, 1, true, 0x00030000, 16, 16, 0);
                Assert.NotEqual(nint.Zero, resourceIcon);
                Assert.True(NativeUser32.DestroyIcon(resourceIcon));
            }

            unsafe
            {
                bool managedClientAreaAnimation = true;
                Assert.True(PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, ref managedClientAreaAnimation));

                int nativeClientAreaAnimation = managedClientAreaAnimation ? 1 : 0;
                Assert.Equal(1, NativeUser32.SystemParametersInfo((uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, 0, &nativeClientAreaAnimation, 0));
                Assert.Equal(managedClientAreaAnimation ? 1 : 0, nativeClientAreaAnimation);

                int disabled = 0;
                Assert.Equal(1, NativeUser32.SystemParametersInfo((uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, 0, &disabled, SPIF_SENDCHANGE));

                bool afterSet = true;
                Assert.True(PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION, ref afterSet));
                Assert.False(afterSet);

                bool restore = managedClientAreaAnimation;
                Assert.True(PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION, ref restore, SPIF_SENDCHANGE));

                uint nativeWindowDpi = NativeUser32.GetDpiForWindow(formHandle);
                Assert.Equal(PInvoke.GetDpiForWindow(form), nativeWindowDpi);
                Assert.Equal(PInvoke.GetDpiForSystem(), NativeUser32.GetDpiForSystem());

                nint managedInputLanguage = 0;
                Assert.True(PInvoke.SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDEFAULTINPUTLANG, ref managedInputLanguage));

                nint nativeInputLanguage = 0;
                Assert.Equal(1, NativeUser32.SystemParametersInfo(
                    (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDEFAULTINPUTLANG,
                    0,
                    &nativeInputLanguage,
                    0));
                Assert.Equal(managedInputLanguage, nativeInputLanguage);
                Assert.NotEqual(nint.Zero, nativeInputLanguage);

                var managedMetrics = new NONCLIENTMETRICSW();
                managedMetrics.cbSize = (uint)sizeof(NONCLIENTMETRICSW);
                Assert.True(PInvoke.TrySystemParametersInfoForDpi(ref managedMetrics, nativeWindowDpi));

                var nativeMetrics = new NONCLIENTMETRICSW();
                Assert.Equal(1, NativeUser32.SystemParametersInfoForDpi(
                    (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETNONCLIENTMETRICS,
                    (uint)sizeof(NONCLIENTMETRICSW),
                    &nativeMetrics,
                    0,
                    nativeWindowDpi));
            }
        }
    }

    [Fact]
    public unsafe void DirectImm32DllImports_RouteToWinFormsXPal()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            using Form form = new();
            form.CreateControl();
            nint handle = form.Handle;

            nint nativeContext = NativeImm32.ImmGetContext(handle);
            Assert.NotEqual(nint.Zero, nativeContext);
            Assert.Equal(nativeContext, (nint)PInvoke.ImmGetContext((HWND)handle));

            Assert.False(NativeImm32.ImmGetOpenStatus(nativeContext));
            Assert.True(NativeImm32.ImmSetOpenStatus(nativeContext, true));
            Assert.True(PInvoke.ImmGetOpenStatus((HIMC)nativeContext));

            uint nativeConversion = 0;
            uint nativeSentence = 0;
            Assert.True(NativeImm32.ImmGetConversionStatus(nativeContext, &nativeConversion, &nativeSentence));
            Assert.Equal(0u, nativeConversion);
            Assert.Equal(0u, nativeSentence);

            Assert.True(NativeImm32.ImmSetConversionStatus(nativeContext, 0x0001, 0x0008));
            IME_CONVERSION_MODE managedConversion;
            IME_SENTENCE_MODE managedSentence;
            Assert.True(PInvoke.ImmGetConversionStatus((HIMC)nativeContext, &managedConversion, &managedSentence));
            Assert.Equal(0x0001u, (uint)managedConversion);
            Assert.Equal(0x0008u, (uint)managedSentence);

            Assert.True(NativeImm32.ImmNotifyIME(nativeContext, 0x0015, 0x0001, 0));
            Assert.True(NativeImm32.ImmReleaseContext(handle, nativeContext));

            nint createdContext = NativeImm32.ImmCreateContext();
            Assert.NotEqual(nint.Zero, createdContext);
            Assert.Equal(nativeContext, NativeImm32.ImmAssociateContext(handle, createdContext));
            Assert.Equal(createdContext, (nint)PInvoke.ImmGetContext((HWND)handle));
            Assert.Equal(createdContext, NativeImm32.ImmAssociateContext(handle, nint.Zero));
            Assert.Equal(nint.Zero, NativeImm32.ImmAssociateContext(handle, nativeContext));
        }
    }

    [Fact]
    public void DirectComDlg32DllImports_RouteToWinFormsXPal()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            Assert.False(NativeComDlg32.GetOpenFileName(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.False(NativeComDlg32.GetSaveFileName(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.False(NativeComDlg32.ChooseColor(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.False(NativeComDlg32.ChooseFont(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.False(NativeComDlg32.PrintDlg(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.Equal(0, NativeComDlg32.PrintDlgEx(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
            Assert.False(NativeComDlg32.PageSetupDlg(nint.Zero));
            Assert.Equal(0u, NativeComDlg32.CommDlgExtendedError());
        }
    }

    [Fact]
    public void DirectComCtl32DllImports_ResolveCommonControlsInit()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            NativeComCtl32.InitCommonControls();
            Assert.Equal(0x000000FFu, NativeComCtl32.GetInitializedClasses() & 0x000000FFu);

            var icc = new NativeComCtl32.INITCOMMONCONTROLSEX
            {
                _dwSize = (uint)Marshal.SizeOf<NativeComCtl32.INITCOMMONCONTROLSEX>(),
                _dwICC = 0x00000001u | 0x00000002u | 0x00000100u
            };

            Assert.True(NativeComCtl32.InitCommonControlsEx(ref icc));
            Assert.Equal(icc._dwICC, NativeComCtl32.GetInitializedClasses() & icc._dwICC);

            icc._dwSize = 0;
            Assert.False(NativeComCtl32.InitCommonControlsEx(ref icc));
        }
    }

    [Fact]
    public void DirectComCtl32DllImports_ResolveImageListState()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            nint imageList = NativeComCtl32.ImageList_Create(16, 20, 0x00000020u, 0, 4);
            Assert.NotEqual(nint.Zero, imageList);
            try
            {
                Assert.True(NativeComCtl32.ImageList_GetIconSize(imageList, out int width, out int height));
                Assert.Equal(16, width);
                Assert.Equal(20, height);
                Assert.Equal(0, NativeComCtl32.ImageList_GetImageCount(imageList));

                Assert.Equal(0, NativeComCtl32.ImageList_Add(imageList, nint.Zero, nint.Zero));
                Assert.Equal(1, NativeComCtl32.ImageList_ReplaceIcon(imageList, -1, nint.Zero));
                Assert.Equal(2, NativeComCtl32.ImageList_GetImageCount(imageList));
                Assert.Equal(2, NativeComCtl32.ImageList_AddMasked(imageList, nint.Zero, 0x0000FF00u));
                Assert.Equal(3, NativeComCtl32.ImageList_GetImageCount(imageList));

                Assert.True(NativeComCtl32.ImageList_Replace(imageList, 0, nint.Zero, nint.Zero));
                Assert.False(NativeComCtl32.ImageList_Replace(imageList, 9, nint.Zero, nint.Zero));

                Assert.True(NativeComCtl32.ImageList_GetImageInfo(imageList, 0, out NativeComCtl32.IMAGEINFO imageInfo));
                Assert.NotEqual(nint.Zero, imageInfo._hbmImage);
                Assert.Equal(16, imageInfo._rcImage._right);
                Assert.Equal(20, imageInfo._rcImage._bottom);

                Assert.True(NativeComCtl32.ImageList_Draw(imageList, 0, nint.Zero, 1, 2, 0));
                Assert.True(NativeComCtl32.ImageList_DrawEx(imageList, 1, nint.Zero, 3, 4, 16, 20, 0xFFFFFFFFu, 0x00000000u, 0));
                Assert.False(NativeComCtl32.ImageList_Draw(imageList, 12, nint.Zero, 0, 0, 0));
                Assert.False(NativeComCtl32.ImageList_DrawEx(imageList, -1, nint.Zero, 0, 0, 16, 20, 0, 0, 0));

                nint iconHandle = NativeComCtl32.ImageList_GetIcon(imageList, 1, 0);
                Assert.NotEqual(nint.Zero, iconHandle);
                Assert.Equal(nint.Zero, NativeComCtl32.ImageList_GetIcon(imageList, 99, 0));

                Assert.Equal(0xFFFFFFFFu, NativeComCtl32.ImageList_SetBkColor(imageList, 0x000000FFu));
                Assert.Equal(0x000000FFu, NativeComCtl32.ImageList_SetBkColor(imageList, 0xFFFFFFFFu));

                Assert.True(NativeComCtl32.ImageList_Write(imageList, nint.Zero));
                Assert.Equal(0, NativeComCtl32.ImageList_WriteEx(imageList, 0, nint.Zero));

                Assert.True(NativeComCtl32.ImageList_SetIconSize(imageList, 24, 28));
                Assert.Equal(0, NativeComCtl32.ImageList_GetImageCount(imageList));
                Assert.False(NativeComCtl32.ImageList_GetImageInfo(imageList, 0, out _));
                Assert.Equal(0, NativeComCtl32.ImageList_Add(imageList, nint.Zero, nint.Zero));
                Assert.True(NativeComCtl32.ImageList_GetImageInfo(imageList, 0, out imageInfo));
                Assert.Equal(24, imageInfo._rcImage._right);
                Assert.Equal(28, imageInfo._rcImage._bottom);

                Assert.False(NativeComCtl32.ImageList_Remove(imageList, 3));
                Assert.True(NativeComCtl32.ImageList_Remove(imageList, -1));
                Assert.Equal(0, NativeComCtl32.ImageList_GetImageCount(imageList));
            }
            finally
            {
                Assert.True(NativeComCtl32.ImageList_Destroy(imageList));
            }
        }
    }

    [Fact]
    public void DirectWinSpoolDllImports_RouteToWinFormsXPal()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            Assert.True(NativeWinSpool.EnumPrinters(0, nint.Zero, 4, nint.Zero, 0, out uint needed, out uint returned));
            Assert.Equal(0u, needed);
            Assert.Equal(0u, returned);
            Assert.Equal(1, NativeWinSpool.DeviceCapabilities(nint.Zero, nint.Zero, 18, nint.Zero, nint.Zero));
            Assert.True(NativeWinSpool.DocumentProperties(nint.Zero, nint.Zero, nint.Zero, nint.Zero, nint.Zero, 0) > 0);
        }
    }

    [Fact]
    public unsafe void DirectKernel32DllImports_RouteToWinFormsXPal()
    {
        using (new EnvironmentOverride("WINFORMSX_SUPPRESS_HIDDEN_BACKEND", "1"))
        {
            Application.EnableVisualStyles();

            Assert.Equal((uint)Environment.ProcessId, NativeKernel32.GetCurrentProcessId());
            Assert.Equal(PInvoke.GetCurrentThreadId(), NativeKernel32.GetCurrentThreadId());
            Assert.Equal((nint)PInvoke.GetCurrentProcess(), NativeKernel32.GetCurrentProcess());
            Assert.Equal((nint)PInvoke.GetCurrentThread(), NativeKernel32.GetCurrentThread());

            nint nativeModule = NativeKernel32.GetModuleHandle(null);
            Assert.NotEqual(nint.Zero, nativeModule);
            Assert.Equal((nint)PInvoke.GetModuleHandle((string?)null), nativeModule);

            Span<char> managedPath = stackalloc char[512];
            Span<char> nativePath = stackalloc char[512];
            fixed (char* managedPathPointer = managedPath)
            fixed (char* nativePathPointer = nativePath)
            {
                uint managedLength = PInvoke.GetModuleFileName(HINSTANCE.Null, managedPathPointer, (uint)managedPath.Length);
                uint nativeLength = NativeKernel32.GetModuleFileName(nint.Zero, nativePathPointer, (uint)nativePath.Length);
                Assert.Equal(managedLength, nativeLength);
                Assert.True(nativeLength > 0);
            Assert.Equal(
                new string(managedPathPointer, 0, (int)managedLength),
                new string(nativePathPointer, 0, (int)nativeLength));
            }

            char* commandLineW = NativeKernel32.GetCommandLineW();
            Assert.NotEqual(nint.Zero, (nint)commandLineW);
            Assert.Contains("WinFormsX", new string(commandLineW));

            nint commandLineA = NativeKernel32.GetCommandLineA();
            Assert.NotEqual(nint.Zero, commandLineA);
            Assert.Contains("WinFormsX", Marshal.PtrToStringAnsi(commandLineA));

            string environmentName = $"WINFORMSX_KERNEL32_DIRECT_ENV_{Environment.ProcessId}";
            Assert.True(NativeKernel32.SetEnvironmentVariableW(environmentName, null));
            try
            {
                Span<char> environmentBuffer = stackalloc char[128];
                fixed (char* environmentBufferPointer = environmentBuffer)
                {
                    Assert.Equal(0u, NativeKernel32.GetEnvironmentVariableW(
                        environmentName,
                        environmentBufferPointer,
                        (uint)environmentBuffer.Length));

                    Assert.True(NativeKernel32.SetEnvironmentVariableW(environmentName, "AlphaBeta"));
                    Assert.Equal(10u, NativeKernel32.GetEnvironmentVariableW(environmentName, environmentBufferPointer, 4));
                    Assert.Equal('\0', environmentBuffer[0]);

                    uint environmentLength = NativeKernel32.GetEnvironmentVariableW(
                        environmentName,
                        environmentBufferPointer,
                        (uint)environmentBuffer.Length);
                    Assert.Equal(9u, environmentLength);
                    Assert.Equal("AlphaBeta", new string(environmentBufferPointer, 0, (int)environmentLength));

                    string expandSource = $"Value=%{environmentName}%; Missing=%WINFORMSX_KERNEL32_MISSING_ENV%";
                    string expectedExpansion = "Value=AlphaBeta; Missing=%WINFORMSX_KERNEL32_MISSING_ENV%";
                    Assert.True(NativeKernel32.ExpandEnvironmentStringsW(expandSource, environmentBufferPointer, 8) > 8);
                    uint expandedLength = NativeKernel32.ExpandEnvironmentStringsW(
                        expandSource,
                        environmentBufferPointer,
                        (uint)environmentBuffer.Length);
                    Assert.Equal((uint)expectedExpansion.Length + 1u, expandedLength);
                    Assert.Equal(expectedExpansion, new string(environmentBufferPointer, 0, (int)expandedLength - 1));
                }

                byte* ansiEnvironmentBuffer = stackalloc byte[64];
                Assert.True(NativeKernel32.SetEnvironmentVariableA(environmentName, "Gamma"));
                Assert.Equal(6u, NativeKernel32.GetEnvironmentVariableA(environmentName, ansiEnvironmentBuffer, 3));

                uint ansiEnvironmentLength = NativeKernel32.GetEnvironmentVariableA(environmentName, ansiEnvironmentBuffer, 64);
                Assert.Equal(5u, ansiEnvironmentLength);
                Assert.Equal("Gamma", Marshal.PtrToStringAnsi((nint)ansiEnvironmentBuffer, (int)ansiEnvironmentLength));

                byte* ansiExpansionBuffer = stackalloc byte[128];
                string expectedAnsiExpansion = "A=Gamma";
                uint ansiExpandedLength = NativeKernel32.ExpandEnvironmentStringsA(
                    $"A=%{environmentName}%",
                    ansiExpansionBuffer,
                    128);
                Assert.Equal((uint)expectedAnsiExpansion.Length + 1u, ansiExpandedLength);
                Assert.Equal(expectedAnsiExpansion, Marshal.PtrToStringAnsi((nint)ansiExpansionBuffer, (int)ansiExpandedLength - 1));

                Assert.True(NativeKernel32.SetEnvironmentVariableA(environmentName, null));
                Assert.Equal(0u, NativeKernel32.GetEnvironmentVariableA(environmentName, ansiEnvironmentBuffer, 64));
            }
            finally
            {
                NativeKernel32.SetEnvironmentVariableW(environmentName, null);
            }

            nint loadedModule = NativeKernel32.LoadLibraryEx("WinFormsX.LoaderSmoke.dll", nint.Zero, 0);
            Assert.NotEqual(nint.Zero, loadedModule);
            Assert.Equal((nint)PInvoke.LoadLibraryEx("WinFormsX.LoaderSmoke.dll", 0), loadedModule);

            nint kernel32Module = NativeKernel32.LoadLibraryEx("KERNEL32.dll", nint.Zero, 0);
            Assert.NotEqual(nint.Zero, kernel32Module);
            Assert.Equal(NativeKernel32.GetModuleHandle("KERNEL32.dll"), kernel32Module);

            nint getTickCount64 = NativeKernel32.GetProcAddress(kernel32Module, "GetTickCount64");
            Assert.NotEqual(nint.Zero, getTickCount64);
            Assert.Equal(getTickCount64, NativeKernel32.GetProcAddress(kernel32Module, "GetTickCount64"));
            Assert.NotEqual(getTickCount64, NativeKernel32.GetProcAddress(kernel32Module, "GetTickCount"));
            Assert.NotEqual(nint.Zero, NativeKernel32.GetProcAddress(kernel32Module, "GetSystemTimeAsFileTime"));
            Assert.NotEqual(nint.Zero, NativeKernel32.GetProcAddress(kernel32Module, "GetProcAddress"));
            Assert.Equal(nint.Zero, NativeKernel32.GetProcAddress(kernel32Module, "MissingExport"));
            Assert.Equal(nint.Zero, NativeKernel32.GetProcAddress(loadedModule, "GetTickCount64"));
            Assert.True(NativeKernel32.FreeLibrary(kernel32Module));

            Assert.True(NativeKernel32.FreeLibrary(loadedModule));
            Assert.Equal(nint.Zero, NativeKernel32.GetProcAddress(loadedModule, "MissingExport"));

            fixed (char* resourceName = "MissingResource")
            fixed (char* resourceType = "CUSTOM")
            {
                HRSRC managedResource = PInvoke.FindResource((HMODULE)loadedModule, (PCWSTR)resourceName, (PCWSTR)resourceType);
                nint nativeResource = NativeKernel32.FindResource(loadedModule, "MissingResource", "CUSTOM");
                Assert.Equal((nint)managedResource.Value, nativeResource);
                Assert.Equal(nint.Zero, nativeResource);

                managedResource = PInvoke.FindResourceEx((HMODULE)loadedModule, (PCWSTR)resourceType, (PCWSTR)resourceName, 0);
                nativeResource = NativeKernel32.FindResourceEx(loadedModule, "CUSTOM", "MissingResource", 0);
                Assert.Equal((nint)managedResource.Value, nativeResource);
                Assert.Equal(nint.Zero, nativeResource);

                Assert.Equal(nint.Zero, NativeKernel32.LoadResource(loadedModule, nativeResource));
                Assert.Equal(0u, NativeKernel32.SizeofResource(loadedModule, nativeResource));
                Assert.Equal(nint.Zero, NativeKernel32.LockResource(nint.Zero));
                Assert.False(NativeKernel32.FreeResource(nint.Zero));
            }

            PInvoke.SetLastError(0);
            NativeKernel32.SetLastError(0x2Au);
            Assert.Equal(0x2Au, NativeKernel32.GetLastError());
            Assert.Equal(0x2Au, PInvoke.GetLastError());

            PInvoke.SetLastError(0x33u);
            Assert.Equal(0x33u, NativeKernel32.GetLastError());

            Assert.True(NativeKernel32.CloseHandle((nint)0x1234));
            nint duplicateHandle;
            Assert.True(NativeKernel32.DuplicateHandle(
                NativeKernel32.GetCurrentProcess(),
                (nint)0x5678,
                NativeKernel32.GetCurrentProcess(),
                &duplicateHandle,
                0,
                false,
                0));
            Assert.Equal((nint)0x5678, duplicateHandle);

            char* message = stackalloc char[128];
            uint messageLength = NativeKernel32.FormatMessage(0x1200, nint.Zero, 5, NativeKernel32.GetThreadLocale(), message, 128, nint.Zero);
            Assert.True(messageLength > 0);
            Assert.False(string.IsNullOrWhiteSpace(new string(message, 0, (int)messageLength)));

            char* localeData = stackalloc char[8];
            int localeLength = NativeKernel32.GetLocaleInfoEx(PInvoke.LOCALE_NAME_SYSTEM_DEFAULT, PInvoke.LOCALE_IMEASURE, localeData, 8);
            Assert.True(localeLength > 0);
            Assert.True(localeData[0] is '0' or '1');

            NativeKernel32.STARTUPINFOW startupInfo;
            NativeKernel32.GetStartupInfo(&startupInfo);
            Assert.Equal((uint)sizeof(NativeKernel32.STARTUPINFOW), startupInfo.cb);

            uint tickCount = NativeKernel32.GetTickCount();
            Assert.True(tickCount > 0);
            ulong tickCount64 = NativeKernel32.GetTickCount64();
            Assert.True(tickCount64 >= tickCount);

            long performanceFrequency;
            Assert.True(NativeKernel32.QueryPerformanceFrequency(&performanceFrequency));
            Assert.Equal(10_000_000L, performanceFrequency);

            long performanceCounter1;
            long performanceCounter2;
            Assert.True(NativeKernel32.QueryPerformanceCounter(&performanceCounter1));
            Assert.True(NativeKernel32.QueryPerformanceCounter(&performanceCounter2));
            Assert.True(performanceCounter2 > performanceCounter1);

            Assert.Equal(1252u, NativeKernel32.GetACP());
            Assert.Equal(437u, NativeKernel32.GetOEMCP());
            Assert.Equal(0x0409u, NativeKernel32.GetSystemDefaultLCID());
            Assert.Equal(0x0409u, NativeKernel32.GetUserDefaultLCID());
            Assert.Equal(NativeKernel32.GetSystemDefaultLCID(), NativeKernel32.GetUserDefaultLCID());

            NativeKernel32.FILETIME systemFileTime;
            NativeKernel32.GetSystemTimeAsFileTime(&systemFileTime);

            NativeKernel32.SYSTEMTIME convertedSystemTime;
            Assert.True(NativeKernel32.FileTimeToSystemTime(&systemFileTime, &convertedSystemTime));

            NativeKernel32.FILETIME convertedFileTime;
            Assert.True(NativeKernel32.SystemTimeToFileTime(&convertedSystemTime, &convertedFileTime));
            Assert.Equal(systemFileTime.dwLowDateTime, convertedFileTime.dwLowDateTime);
            Assert.Equal(systemFileTime.dwHighDateTime, convertedFileTime.dwHighDateTime);

            NativeKernel32.SYSTEMTIME utcSystemTime;
            NativeKernel32.SYSTEMTIME localSystemTime;
            NativeKernel32.GetSystemTime(&utcSystemTime);
            NativeKernel32.GetLocalTime(&localSystemTime);

            NativeKernel32.FILETIME utcFileTime;
            NativeKernel32.FILETIME localFileTime;
            Assert.True(NativeKernel32.SystemTimeToFileTime(&utcSystemTime, &utcFileTime));
            Assert.True(NativeKernel32.SystemTimeToFileTime(&localSystemTime, &localFileTime));

            ulong utcTicks = ((ulong)utcFileTime.dwHighDateTime << 32) | utcFileTime.dwLowDateTime;
            ulong localTicks = ((ulong)localFileTime.dwHighDateTime << 32) | localFileTime.dwLowDateTime;
            Assert.True(utcTicks > localTicks);
            ulong offsetTicks = utcTicks - localTicks;
            Assert.InRange(offsetTicks, 143_900_000_000UL, 144_100_000_000UL);

            NativeKernel32.SYSTEMTIME invalidSystemTime = new()
            {
                wYear = 2026,
                wMonth = 2,
                wDay = 30
            };

            Assert.False(NativeKernel32.SystemTimeToFileTime(&invalidSystemTime, &utcFileTime));
            Assert.False(NativeKernel32.FileTimeToSystemTime(null, &utcSystemTime));
            Assert.False(NativeKernel32.FileTimeToSystemTime(&systemFileTime, null));
            Assert.False(NativeKernel32.SystemTimeToFileTime(null, &utcFileTime));
            Assert.False(NativeKernel32.SystemTimeToFileTime(&utcSystemTime, null));

            uint exitCode;
            Assert.True(NativeKernel32.GetExitCodeThread((nint)(-2), &exitCode));
            Assert.Equal(259u, exitCode);

            const uint ZeroInit = 0x40;
            nint globalMemory = NativeKernel32.GlobalAlloc(ZeroInit, 8);
            Assert.NotEqual(nint.Zero, globalMemory);
            Assert.Equal((nuint)8, NativeKernel32.GlobalSize(globalMemory));

            byte* globalBytes = (byte*)NativeKernel32.GlobalLock(globalMemory);
            Assert.NotEqual(nint.Zero, (nint)globalBytes);
            Assert.True(globalBytes[0] == 0 && globalBytes[7] == 0);
            globalBytes[0] = 0x12;
            Assert.False(NativeKernel32.GlobalUnlock(globalMemory));

            globalMemory = NativeKernel32.GlobalReAlloc(globalMemory, 12, ZeroInit);
            Assert.NotEqual(nint.Zero, globalMemory);
            Assert.Equal((nuint)12, NativeKernel32.GlobalSize(globalMemory));
            globalBytes = (byte*)NativeKernel32.GlobalLock(globalMemory);
            Assert.Equal(0x12, globalBytes[0]);
            Assert.Equal(0, globalBytes[11]);
            Assert.Equal(nint.Zero, NativeKernel32.GlobalFree(globalMemory));

            nint localMemory = NativeKernel32.LocalAlloc(ZeroInit, 4);
            Assert.NotEqual(nint.Zero, localMemory);
            Assert.Equal((nuint)4, NativeKernel32.LocalSize(localMemory));

            byte* localBytes = (byte*)NativeKernel32.LocalLock(localMemory);
            Assert.NotEqual(nint.Zero, (nint)localBytes);
            localBytes[0] = 0x34;
            Assert.False(NativeKernel32.LocalUnlock(localMemory));

            localMemory = NativeKernel32.LocalReAlloc(localMemory, 6, ZeroInit);
            Assert.NotEqual(nint.Zero, localMemory);
            Assert.Equal((nuint)6, NativeKernel32.LocalSize(localMemory));
            localBytes = (byte*)NativeKernel32.LocalLock(localMemory);
            Assert.Equal(0x34, localBytes[0]);
            Assert.Equal(0, localBytes[5]);
            Assert.Equal(nint.Zero, NativeKernel32.LocalFree(localMemory));

            NativeKernel32.ACTCTXW actCtx = new()
            {
                cbSize = (uint)sizeof(NativeKernel32.ACTCTXW)
            };

            nint activationContext = NativeKernel32.CreateActCtx(&actCtx);
            Assert.NotEqual((nint)(-1), activationContext);
            Assert.NotEqual(nint.Zero, activationContext);

            nint currentActivationContext;
            Assert.False(NativeKernel32.GetCurrentActCtx(&currentActivationContext));

            nuint activationCookie;
            Assert.True(NativeKernel32.ActivateActCtx(activationContext, &activationCookie));
            Assert.NotEqual((nuint)0, activationCookie);
            Assert.True(NativeKernel32.GetCurrentActCtx(&currentActivationContext));
            Assert.Equal(activationContext, currentActivationContext);

            Assert.True(NativeKernel32.DeactivateActCtx(0, activationCookie));
        }
    }

    [Fact]
    public unsafe void ManagedDwmWindowAttributeWrappers_RouteToWinFormsXState()
    {
        HWND hwnd = (HWND)(nint)0x123456;

        BOOL darkMode = true;
        Assert.Equal(
            HRESULT.S_OK,
            PInvoke.DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                &darkMode,
                (uint)sizeof(BOOL)));

        BOOL actualDarkMode;
        Assert.Equal(
            HRESULT.S_OK,
            PInvoke.DwmGetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
                &actualDarkMode,
                (uint)sizeof(BOOL)));
        Assert.True(actualDarkMode);

        DWM_WINDOW_CORNER_PREFERENCE cornerPreference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
        Assert.Equal(
            HRESULT.S_OK,
            PInvoke.DwmSetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &cornerPreference,
                (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)));

        DWM_WINDOW_CORNER_PREFERENCE actualCornerPreference;
        Assert.Equal(
            HRESULT.S_OK,
            PInvoke.DwmGetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                &actualCornerPreference,
                (uint)sizeof(DWM_WINDOW_CORNER_PREFERENCE)));
        Assert.Equal(cornerPreference, actualCornerPreference);
    }

    [Fact]
    public unsafe void DirectDwmApiDllImports_ResolveToWinFormsXFacade()
    {
        nint hwnd = 0x654321;
        BOOL darkMode = true;

        Assert.Equal(0, NativeDwmApi.DwmSetWindowAttribute(
            hwnd,
            (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            &darkMode,
            (uint)sizeof(BOOL)));

        BOOL actualDarkMode;
        Assert.Equal(0, NativeDwmApi.DwmGetWindowAttribute(
            hwnd,
            (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE,
            &actualDarkMode,
            (uint)sizeof(BOOL)));
        Assert.True(actualDarkMode);
    }

    [Fact]
    public unsafe void DirectUxThemeDllImports_ResolveToWinFormsXFacade()
    {
        nint theme = NativeUxTheme.OpenThemeData(0x1234, "Button");
        Assert.NotEqual(0, theme);

        NativeUxTheme.NativeSize size;
        Assert.Equal(0, NativeUxTheme.GetThemePartSize(theme, 0, 1, 1, null, 0, &size));
        Assert.Equal(13, size.cx);
        Assert.Equal(13, size.cy);

        NativeUxTheme.NativeRect bounds = new() { left = 0, top = 0, right = 20, bottom = 20 };
        NativeUxTheme.NativeMargins margins;
        Assert.Equal(0, NativeUxTheme.GetThemeMargins(theme, 0, 1, 1, 0, &bounds, &margins));
        Assert.Equal(0, margins.cxLeftWidth);
        Assert.Equal(0, margins.cxRightWidth);
        Assert.Equal(0, margins.cyTopHeight);
        Assert.Equal(0, margins.cyBottomHeight);

        Assert.Equal(0, NativeUxTheme.SetWindowTheme(0x1234, "Explorer", null));
        Assert.False(NativeUxTheme.IsThemePartDefined(theme, 1, 1));
        Assert.Equal(0, NativeUxTheme.CloseThemeData(theme));
    }

    [Fact]
    public void SystemEventsPowerAndSessionBridge_RaisesPublicAndPalEvents()
    {
        List<PowerModes> publicPowerModes = [];
        List<PowerModes> palPowerModes = [];
        List<SessionSwitchReason> publicSessionReasons = [];
        List<SessionSwitchReason> palSessionReasons = [];

        PowerModeChangedEventHandler publicPowerHandler = (_, e) => publicPowerModes.Add(e.Mode);
        PowerModeChangedEventHandler palPowerHandler = (_, e) => palPowerModes.Add(e.Mode);
        SessionSwitchEventHandler publicSessionHandler = (_, e) => publicSessionReasons.Add(e.Reason);
        SessionSwitchEventHandler palSessionHandler = (_, e) => palSessionReasons.Add(e.Reason);

        SystemEvents.PowerModeChanged += publicPowerHandler;
        PalEvents.PowerModeChanged += palPowerHandler;
        SystemEvents.SessionSwitch += publicSessionHandler;
        PalEvents.SessionSwitch += palSessionHandler;

        try
        {
            WinFormsXSystemEventsCompatibility.RaisePowerModeChanged(PowerModes.Suspend);
            WinFormsXSystemEventsCompatibility.RaiseSessionSwitch(SessionSwitchReason.SessionUnlock);
        }
        finally
        {
            SystemEvents.PowerModeChanged -= publicPowerHandler;
            PalEvents.PowerModeChanged -= palPowerHandler;
            SystemEvents.SessionSwitch -= publicSessionHandler;
            PalEvents.SessionSwitch -= palSessionHandler;
        }

        Assert.Equal([PowerModes.Suspend], publicPowerModes);
        Assert.Equal([PowerModes.Suspend], palPowerModes);
        Assert.Equal([SessionSwitchReason.SessionUnlock], publicSessionReasons);
        Assert.Equal([SessionSwitchReason.SessionUnlock], palSessionReasons);
    }

    [Fact]
    public unsafe void DirectOle32DllImports_ResolveToWinFormsXFacade()
    {
        const int S_OK = 0;
        const int S_FALSE = 1;
        const int E_INVALIDARG = unchecked((int)0x80070057);
        const int E_NOINTERFACE = unchecked((int)0x80004002);
        const int E_NOTIMPL = unchecked((int)0x80004001);
        const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);
        const int DRAGDROP_S_CANCEL = 0x00040101;
        const int DRAGDROP_E_ALREADYREGISTERED = unchecked((int)0x80040101);

        int initializeResult = NativeOle32.OleInitialize(null);
        Assert.True(initializeResult is S_OK or S_FALSE);

        nint createdObject;
        Assert.Equal(
            REGDB_E_CLASSNOTREG,
            NativeOle32.CoCreateInstance(null, 0, 0, null, &createdObject));
        Assert.Equal(0, createdObject);

        byte* buffer = (byte*)NativeOle32.CoTaskMemAlloc(4);
        try
        {
            Assert.NotEqual(0, (nint)buffer);
            buffer[0] = 0x11;
            buffer[1] = 0x22;
            buffer[2] = 0x33;
            buffer[3] = 0x44;

            nint resizedBuffer = NativeOle32.CoTaskMemRealloc((nint)buffer, 8);
            Assert.NotEqual(0, resizedBuffer);
            buffer = (byte*)resizedBuffer;
            Assert.NotEqual(0, (nint)buffer);
            Assert.Equal((byte)0x11, buffer[0]);
            Assert.Equal((byte)0x22, buffer[1]);
            Assert.Equal((byte)0x33, buffer[2]);
            Assert.Equal((byte)0x44, buffer[3]);

            buffer[4] = 0x55;
            buffer[5] = 0x66;
            buffer[6] = 0x77;
            buffer[7] = 0x88;

            resizedBuffer = NativeOle32.CoTaskMemRealloc((nint)buffer, 4);
            Assert.NotEqual(0, resizedBuffer);
            buffer = (byte*)resizedBuffer;
            Assert.NotEqual(0, (nint)buffer);
            Assert.Equal((byte)0x11, buffer[0]);
            Assert.Equal((byte)0x22, buffer[1]);
            Assert.Equal((byte)0x33, buffer[2]);
            Assert.Equal((byte)0x44, buffer[3]);
        }
        finally
        {
            NativeOle32.CoTaskMemFree((nint)buffer);
            NativeOle32.CoTaskMemFree(0);
        }

        nint mallocObject = (nint)0x1234;
        Assert.Equal(E_NOTIMPL, NativeOle32.CoGetMalloc(1, &mallocObject));
        Assert.Equal(0, mallocObject);
        Assert.Equal(E_INVALIDARG, NativeOle32.CoGetMalloc(1, null));

        Guid firstGuid;
        Guid secondGuid;
        Assert.Equal(S_OK, NativeOle32.CoCreateGuid(&firstGuid));
        Assert.Equal(S_OK, NativeOle32.CoCreateGuid(&secondGuid));
        Assert.NotEqual(Guid.Empty, firstGuid);
        Assert.NotEqual(Guid.Empty, secondGuid);
        Assert.NotEqual(firstGuid, secondGuid);
        Assert.Equal(E_INVALIDARG, NativeOle32.CoCreateGuid(null));

        nint previousFilter;
        Assert.Equal(S_OK, NativeOle32.CoRegisterMessageFilter((nint)0x1111, &previousFilter));
        Assert.Equal(0, previousFilter);
        Assert.Equal(S_OK, NativeOle32.CoRegisterMessageFilter((nint)0x2222, &previousFilter));
        Assert.Equal((nint)0x1111, previousFilter);
        Assert.Equal(S_OK, NativeOle32.CoRegisterMessageFilter(0, &previousFilter));
        Assert.Equal((nint)0x2222, previousFilter);

        nint lockBytes;
        Assert.Equal(S_OK, NativeOle32.CreateILockBytesOnHGlobal((nint)0x3210, true, &lockBytes));
        Assert.NotEqual(0, lockBytes);

        Assert.Equal(E_INVALIDARG, NativeOle32.CreateOleAdviseHolder(null));
        Assert.Equal(E_INVALIDARG, NativeOle32.CreateDataAdviseHolder(null));

        nint oleAdviseHolder;
        Assert.Equal(S_OK, NativeOle32.CreateOleAdviseHolder(&oleAdviseHolder));
        Assert.NotEqual(0, oleAdviseHolder);

        nint secondOleAdviseHolder;
        Assert.Equal(S_OK, NativeOle32.CreateOleAdviseHolder(&secondOleAdviseHolder));
        Assert.NotEqual(0, secondOleAdviseHolder);
        Assert.NotEqual(oleAdviseHolder, secondOleAdviseHolder);

        nint dataAdviseHolder;
        Assert.Equal(S_OK, NativeOle32.CreateDataAdviseHolder(&dataAdviseHolder));
        Assert.NotEqual(0, dataAdviseHolder);

        nint stream;
        Assert.Equal(S_OK, NativeOle32.CreateStreamOnHGlobal((nint)0x9876, false, &stream));
        Assert.NotEqual(0, stream);

        nint streamHGlobal;
        Assert.Equal(S_OK, NativeOle32.GetHGlobalFromStream(stream, &streamHGlobal));
        Assert.Equal((nint)0x9876, streamHGlobal);
        Assert.Equal(E_INVALIDARG, NativeOle32.GetHGlobalFromStream((nint)0x4321, &streamHGlobal));
        Assert.Equal(0, streamHGlobal);

        nint dataObject = 0x12345678;
        Assert.Equal(S_OK, NativeOle32.OleSetClipboard(dataObject));

        nint actualDataObject;
        Assert.Equal(S_OK, NativeOle32.OleGetClipboard(&actualDataObject));
        Assert.Equal(dataObject, actualDataObject);
        Assert.Equal(S_OK, NativeOle32.OleFlushClipboard());
        Assert.Equal(S_OK, NativeOle32.OleIsCurrentClipboard(dataObject));
        Assert.Equal(S_FALSE, NativeOle32.OleIsCurrentClipboard((nint)0x7F7F7F7F));

        nint hwnd = 0x2468;
        nint dropTarget = 0x1357;
        Assert.Equal(S_OK, NativeOle32.RegisterDragDrop(hwnd, dropTarget));
        Assert.Equal(DRAGDROP_E_ALREADYREGISTERED, NativeOle32.RegisterDragDrop(hwnd, dropTarget));

        uint effect = 1;
        Assert.Equal(DRAGDROP_S_CANCEL, NativeOle32.DoDragDrop(dataObject, 0, 1, &effect));
        Assert.Equal(0u, effect);

        nint pictureObject;
        Assert.Equal(E_INVALIDARG, NativeOle32.OleCreatePictureIndirect(null, null, false, &pictureObject));
        Assert.Equal(0, pictureObject);
        Assert.Equal(E_NOINTERFACE, NativeOle32.OleCreatePictureIndirect((void*)0x1111, (void*)0x2222, false, &pictureObject));
        Assert.Equal(0, pictureObject);

        NativeOle32.NativeStgMedium medium = new()
        {
            tymed = 4u,
            unionMember = stream,
            pUnkForRelease = 0,
        };

        NativeOle32.ReleaseStgMedium(&medium);
        Assert.Equal(0u, medium.tymed);
        Assert.Equal(0, medium.unionMember);
        Assert.Equal(0, medium.pUnkForRelease);

        nint postReleaseHGlobal;
        Assert.Equal(E_INVALIDARG, NativeOle32.GetHGlobalFromStream(stream, &postReleaseHGlobal));
        Assert.Equal(0, postReleaseHGlobal);

        Assert.Equal(S_OK, NativeOle32.RevokeDragDrop(hwnd));
        NativeOle32.OleUninitialize();
    }

    [Fact]
    public unsafe void DirectOleAut32DllImports_ResolveToWinFormsXFacade()
    {
        const int S_OK = 0;
        const int E_INVALIDARG = unchecked((int)0x80070057);
        const int DISP_E_ARRAYISLOCKED = unchecked((int)0x8002000D);
        const int TYPE_E_CANTLOADLIBRARY = unchecked((int)0x80029C4A);

        nint bstr = NativeOleAut32.SysAllocString("WinFormsX");
        Assert.NotEqual(0, bstr);
        Assert.Equal(9u, NativeOleAut32.SysStringLen(bstr));
        Assert.Equal(18u, NativeOleAut32.SysStringByteLen(bstr));
        NativeOleAut32.SysFreeString(bstr);

        nint typeLib;
        Assert.Equal(TYPE_E_CANTLOADLIBRARY, NativeOleAut32.LoadRegTypeLib(null, 1, 0, 0, &typeLib));
        Assert.Equal(0, typeLib);

        NativeOleAut32.NativeVariant variant = default;
        Assert.Equal(S_OK, NativeOleAut32.VariantClear(&variant));
        Assert.Equal(0, variant.vt);

        Assert.Equal(S_OK, NativeOleAut32.SafeArrayDestroy(null));

        NativeOleAut32.SAFEARRAYBOUND bound = new()
        {
            cElements = 3,
            lLbound = 2
        };
        void* safeArray = NativeOleAut32.SafeArrayCreate(3, 1, &bound);
        Assert.NotEqual(0, (nint)safeArray);
        Assert.Equal(1u, NativeOleAut32.SafeArrayGetDim(safeArray));
        ushort vt;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetVartype(safeArray, &vt));
        Assert.Equal(3, vt);

        int lowerBound;
        int upperBound;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetLBound(safeArray, 1, &lowerBound));
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetUBound(safeArray, 1, &upperBound));
        Assert.Equal(2, lowerBound);
        Assert.Equal(4, upperBound);

        int index = 3;
        int value = 42;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayPutElement(safeArray, &index, &value));

        int actualValue = 0;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetElement(safeArray, &index, &actualValue));
        Assert.Equal(value, actualValue);

        void* elementPointer;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayPtrOfIndex(safeArray, &index, &elementPointer));
        Assert.NotEqual(0, (nint)elementPointer);
        Assert.Equal(value, *(int*)elementPointer);

        void* data;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayAccessData(safeArray, &data));
        Assert.NotEqual(0, (nint)data);
        NativeOleAut32.SAFEARRAYBOUND expandedBound = new()
        {
            cElements = 5,
            lLbound = 2
        };
        Assert.Equal(DISP_E_ARRAYISLOCKED, NativeOleAut32.SafeArrayRedim(safeArray, &expandedBound));
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayUnaccessData(safeArray));

        Assert.Equal(S_OK, NativeOleAut32.SafeArrayRedim(safeArray, &expandedBound));
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetUBound(safeArray, 1, &upperBound));
        Assert.Equal(6, upperBound);

        actualValue = 0;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetElement(safeArray, &index, &actualValue));
        Assert.Equal(value, actualValue);

        index = 6;
        value = 84;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayPutElement(safeArray, &index, &value));
        actualValue = 0;
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayGetElement(safeArray, &index, &actualValue));
        Assert.Equal(value, actualValue);

        index = 7;
        elementPointer = (void*)0x1234;
        Assert.Equal(E_INVALIDARG, NativeOleAut32.SafeArrayPtrOfIndex(safeArray, &index, &elementPointer));
        Assert.Equal(0, (nint)elementPointer);

        Assert.Equal(S_OK, NativeOleAut32.SafeArrayDestroy(safeArray));
    }

    [Fact]
    public void DirectGdi32DllImports_ResolveToWinFormsXFacade()
    {
        const int LOGPIXELSX = 88;
        const int TRANSPARENT = 1;
        const int OPAQUE = 2;

        nint dc = NativeGdi32.CreateCompatibleDC(0);
        Assert.NotEqual(0, dc);
        Assert.Equal(96, NativeGdi32.GetDeviceCaps(dc, LOGPIXELSX));

        Assert.Equal(0u, NativeGdi32.SetTextColor(dc, 0x000000AA));
        Assert.Equal(0x000000AAu, NativeGdi32.GetTextColor(dc));
        Assert.Equal(0x00FFFFFFu, NativeGdi32.SetBkColor(dc, 0x0000AA00));
        Assert.Equal(0x0000AA00u, NativeGdi32.GetBkColor(dc));
        Assert.Equal(OPAQUE, NativeGdi32.SetBkMode(dc, TRANSPARENT));
        Assert.Equal(TRANSPARENT, NativeGdi32.GetBkMode(dc));

        nint brush = NativeGdi32.CreateSolidBrush(0x0000FF00);
        Assert.NotEqual(0, brush);
        Assert.Equal(brush, NativeGdi32.SelectObject(dc, brush));
        Assert.True(NativeGdi32.DeleteObject(brush));

        NativeGdi32.LOGBRUSH logBrush = new()
        {
            lbStyle = 0,
            lbColor = 0x000000CC,
        };
        nint indirectBrush = NativeGdi32.CreateBrushIndirect(ref logBrush);
        Assert.NotEqual(0, indirectBrush);
        Assert.True(NativeGdi32.DeleteObject(indirectBrush));

        nint pen = NativeGdi32.CreatePen(0, 1, 0x00FF0000);
        Assert.NotEqual(0, pen);
        Assert.True(NativeGdi32.DeleteObject(pen));

        nint bitmap = NativeGdi32.CreateCompatibleBitmap(dc, 8, 8);
        Assert.NotEqual(0, bitmap);
        nint patternBrush = NativeGdi32.CreatePatternBrush(bitmap);
        Assert.NotEqual(0, patternBrush);
        Assert.True(NativeGdi32.DeleteObject(patternBrush));
        Assert.True(NativeGdi32.DeleteObject(bitmap));

        nint dib = NativeGdi32.CreateDIBSection(dc, 0, 0, out nint bits, 0, 0);
        Assert.NotEqual(0, dib);
        Assert.Equal(0, bits);
        Assert.True(NativeGdi32.DeleteObject(dib));

        nint font = NativeGdi32.CreateFontIndirectW(0);
        Assert.NotEqual(0, font);
        Assert.True(NativeGdi32.DeleteObject(font));

        nint region = NativeGdi32.CreateRectRgn(0, 0, 8, 8);
        Assert.NotEqual(0, region);
        Assert.Equal(1, NativeGdi32.CombineRgn(region, region, 0, 1));
        Assert.True(NativeGdi32.SelectClipRgn(dc, region) > 0);
        Assert.True(NativeGdi32.IntersectClipRect(dc, 0, 0, 8, 8) > 0);
        Assert.True(NativeGdi32.GetClipBox(dc, out NativeGdi32.RECT clipBox) > 0);
        Assert.True(clipBox.right > clipBox.left);
        Assert.True(NativeGdi32.DeleteObject(region));

        nint palette = NativeGdi32.CreateHalftonePalette(dc);
        Assert.NotEqual(0, palette);
        Assert.Equal(palette, NativeGdi32.SelectPalette(dc, palette, false));
        Assert.Equal(0u, NativeGdi32.RealizePalette(dc));
        NativeGdi32.PALETTEENTRY[] entries = new NativeGdi32.PALETTEENTRY[2];
        Assert.Equal(2u, NativeGdi32.GetPaletteEntries(palette, 0, (uint)entries.Length, entries));
        Assert.True(NativeGdi32.DeleteObject(palette));

        Assert.True(NativeGdi32.PatBlt(dc, 0, 0, 8, 8, 0x00F00021));
        Assert.True(NativeGdi32.BitBlt(dc, 0, 0, 8, 8, dc, 0, 0, 0x00CC0020));

        nint printerDc = NativeGdi32.CreateDCW("DISPLAY", null, null, 0);
        Assert.NotEqual(0, printerDc);
        NativeGdi32.DOCINFOW docInfo = new()
        {
            cbSize = Marshal.SizeOf<NativeGdi32.DOCINFOW>(),
            lpszDocName = "WinFormsX direct GDI32 smoke"
        };
        Assert.True(NativeGdi32.StartDocW(printerDc, ref docInfo) > 0);
        Assert.True(NativeGdi32.StartPage(printerDc) > 0);
        Assert.True(NativeGdi32.EndPage(printerDc) > 0);
        Assert.True(NativeGdi32.EndDoc(printerDc) > 0);
        Assert.Equal(0, NativeGdi32.ExtEscape(printerDc, 0, 0, 0, 0, 0));
        Assert.True(NativeGdi32.DeleteDC(printerDc));

        nint infoDc = NativeGdi32.CreateICW("DISPLAY", null, null, 0);
        Assert.NotEqual(0, infoDc);
        Assert.True(NativeGdi32.AbortDoc(infoDc) > 0);
        Assert.True(NativeGdi32.DeleteDC(infoDc));

        Assert.True(NativeGdi32.DeleteDC(dc));
    }

    [Fact]
    public void DirectGdiPlusDllImports_ResolveToWinFormsXFacade()
    {
        const int Ok = 0;
        const int InvalidParameter = 2;
        const int NotImplemented = 6;
        const int PixelFormat32bppArgb = 0x0026200A;

        NativeGdiPlus.GdiplusStartupInput startupInput = new()
        {
            GdiplusVersion = 1,
            DebugEventCallback = 0,
            SuppressBackgroundThread = false,
            SuppressExternalCodecs = false,
        };

        Assert.Equal(
            Ok,
            NativeGdiPlus.GdiplusStartup(
                out nuint token,
                ref startupInput,
                out NativeGdiPlus.GdiplusStartupOutput startupOutput));
        Assert.NotEqual(0u, token);
        Assert.Equal(0, startupOutput.NotificationHook);
        Assert.Equal(0, startupOutput.NotificationUnhook);

        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageDecodersSize(out uint numberOfDecoders, out uint decoderBufferSize));
        Assert.Equal(0u, numberOfDecoders);
        Assert.Equal(0u, decoderBufferSize);

        Assert.Equal(
            NotImplemented,
            NativeGdiPlus.GdipCreateBitmapFromScan0(
                8,
                8,
                0,
                0,
                0,
                out nint bitmap));
        Assert.Equal(0, bitmap);

        Assert.Equal(
            InvalidParameter,
            NativeGdiPlus.GdipCreateBitmapFromScan0(
                0,
                8,
                0,
                0,
                0,
                out bitmap));

        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipCreateBitmapFromHBITMAP(0, 0, out nint hbitmapImage));
        Assert.Equal(0, hbitmapImage);

        Assert.Equal(Ok, NativeGdiPlus.GdipCreateBitmapFromHBITMAP((nint)0x7100, 0, out hbitmapImage));
        Assert.NotEqual(0, hbitmapImage);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageWidth(hbitmapImage, out uint imageWidth));
        Assert.Equal(16u, imageWidth);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageHeight(hbitmapImage, out uint imageHeight));
        Assert.Equal(16u, imageHeight);

        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipCreateBitmapFromHICON(0, out nint hiconImage));
        Assert.Equal(0, hiconImage);

        Assert.Equal(Ok, NativeGdiPlus.GdipCreateBitmapFromHICON((nint)0x7200, out hiconImage));
        Assert.NotEqual(0, hiconImage);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageWidth(hiconImage, out imageWidth));
        Assert.Equal(32u, imageWidth);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageHeight(hiconImage, out imageHeight));
        Assert.Equal(32u, imageHeight);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageFlags(hiconImage, out uint imageFlags));
        Assert.Equal(0u, imageFlags);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImagePixelFormat(hiconImage, out int pixelFormat));
        Assert.Equal(PixelFormat32bppArgb, pixelFormat);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageHorizontalResolution(hiconImage, out float horizontalResolution));
        Assert.Equal(96.0f, horizontalResolution);
        Assert.Equal(Ok, NativeGdiPlus.GdipGetImageVerticalResolution(hiconImage, out float verticalResolution));
        Assert.Equal(96.0f, verticalResolution);

        string imagePath = global::System.IO.Path.GetTempFileName();
        try
        {
            global::System.IO.File.WriteAllBytes(imagePath, [0x42]);

            Assert.Equal(Ok, NativeGdiPlus.GdipLoadImageFromFile(imagePath, out nint fileImage));
            Assert.NotEqual(0, fileImage);
            Assert.Equal(Ok, NativeGdiPlus.GdipGetImageWidth(fileImage, out imageWidth));
            Assert.Equal(64u, imageWidth);
            Assert.Equal(Ok, NativeGdiPlus.GdipGetImageHeight(fileImage, out imageHeight));
            Assert.Equal(64u, imageHeight);
            Assert.Equal(Ok, NativeGdiPlus.GdipGetImagePixelFormat(fileImage, out pixelFormat));
            Assert.Equal(PixelFormat32bppArgb, pixelFormat);
            Assert.Equal(Ok, NativeGdiPlus.GdipDisposeImage(fileImage));

            Assert.Equal(Ok, NativeGdiPlus.GdipLoadImageFromFileICM(imagePath, out nint icmFileImage));
            Assert.NotEqual(0, icmFileImage);
            Assert.Equal(Ok, NativeGdiPlus.GdipGetImageHorizontalResolution(icmFileImage, out horizontalResolution));
            Assert.Equal(96.0f, horizontalResolution);
            Assert.Equal(Ok, NativeGdiPlus.GdipDisposeImage(icmFileImage));

            Assert.Equal(InvalidParameter, NativeGdiPlus.GdipLoadImageFromFile(imagePath + ".missing", out nint missingFileImage));
            Assert.Equal(0, missingFileImage);
        }
        finally
        {
            global::System.IO.File.Delete(imagePath);
        }

        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipLoadImageFromFile(null, out nint nullFileImage));
        Assert.Equal(0, nullFileImage);
        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipLoadImageFromFile(string.Empty, out nint emptyFileImage));
        Assert.Equal(0, emptyFileImage);
        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipLoadImageFromStream(0, out nint streamImage));
        Assert.Equal(0, streamImage);
        Assert.Equal(NotImplemented, NativeGdiPlus.GdipLoadImageFromStream((nint)0x7300, out streamImage));
        Assert.Equal(0, streamImage);
        Assert.Equal(NotImplemented, NativeGdiPlus.GdipLoadImageFromStreamICM((nint)0x7400, out nint icmStreamImage));
        Assert.Equal(0, icmStreamImage);

        Assert.Equal(Ok, NativeGdiPlus.GdipDisposeImage(hbitmapImage));
        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipGetImageWidth(hbitmapImage, out _));
        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipDisposeImage(hbitmapImage));
        Assert.Equal(InvalidParameter, NativeGdiPlus.GdipDisposeImage(0));
        Assert.Equal(Ok, NativeGdiPlus.GdipDisposeImage(hiconImage));

        startupInput.GdiplusVersion = 0;
        Assert.Equal(
            InvalidParameter,
            NativeGdiPlus.GdiplusStartup(
                out nuint invalidToken,
                ref startupInput,
                out _));
        Assert.Equal(0u, invalidToken);

        NativeGdiPlus.GdiplusShutdown(token);
    }

    [Fact]
    public unsafe void DirectShell32DllImports_ResolveToWinFormsXFacade()
    {
        const int E_NOTIMPL = unchecked((int)0x80004001);
        const int S_OK = 0;
        const uint SHGSI_SMALLICON = 0x000000001;
        const uint SHGSI_ICON = 0x000000100;
        const uint SHGFI_ICON = 0x000000100;

        Assert.True(NativeShell32.Shell_NotifyIconW(0, 0));
        NativeShell32.DragAcceptFiles(0x2468, true);
        Assert.Equal(0u, NativeShell32.DragQueryFileW(0, 0xFFFFFFFF, null, 0));

        char* path = stackalloc char[260];
        Assert.False(NativeShell32.SHGetPathFromIDListEx(0, path, 260, 0));
        Assert.Equal('\0', path[0]);
        Assert.Equal(0, NativeShell32.SHBrowseForFolderW(0));

        nint shellItem;
        Assert.Equal(E_NOTIMPL, NativeShell32.SHCreateShellItem(0, 0, 0, &shellItem));
        Assert.Equal(0, shellItem);

        Assert.True(NativeShell32.ShellExecuteW(0, null, "https://example.invalid", null, null, 1) > 32);

        NativeShell32.SHSTOCKICONINFOW stockIconInfo = new()
        {
            _cbSize = (uint)Marshal.SizeOf<NativeShell32.SHSTOCKICONINFOW>(),
            _szPath = string.Empty,
        };
        Assert.Equal(S_OK, NativeShell32.SHGetStockIconInfo(4, SHGSI_ICON | SHGSI_SMALLICON, ref stockIconInfo));
        Assert.NotEqual(0, stockIconInfo._hIcon);
        Assert.Equal(4, stockIconInfo._iSysImageIndex);
        Assert.Equal(4, stockIconInfo._iIcon);
        Assert.NotNull(stockIconInfo._szPath);
        Assert.Contains("WinFormsX\\StockIcon\\", stockIconInfo._szPath);

        char* associatedPathW = stackalloc char[260];
        associatedPathW[0] = '\0';
        ushort associatedIconIndexW = 7;
        nint associatedIconW = NativeShell32.ExtractAssociatedIconW(0, associatedPathW, &associatedIconIndexW);
        Assert.NotEqual(0, associatedIconW);
        Assert.Equal((ushort)7, associatedIconIndexW);
        Assert.Contains("WinFormsX.ico", new string(associatedPathW));

        byte* associatedPathA = stackalloc byte[260];
        associatedPathA[0] = 0;
        ushort associatedIconIndexA = 9;
        nint associatedIconA = NativeShell32.ExtractAssociatedIconA(0, associatedPathA, &associatedIconIndexA);
        Assert.NotEqual(0, associatedIconA);
        Assert.Equal((ushort)9, associatedIconIndexA);
        string? associatedPathAnsi = Marshal.PtrToStringAnsi((nint)associatedPathA);
        Assert.NotNull(associatedPathAnsi);
        Assert.Contains("WinFormsX.ico", associatedPathAnsi);

        nint* largeIcons = stackalloc nint[2];
        nint* smallIcons = stackalloc nint[2];
        Assert.Equal(2u, NativeShell32.ExtractIconExW("sample.txt", 3, largeIcons, smallIcons, 2));
        Assert.NotEqual(0, largeIcons[0]);
        Assert.NotEqual(0, smallIcons[0]);
        Assert.NotEqual(largeIcons[0], smallIcons[0]);

        nint* largeIconsA = stackalloc nint[2];
        nint* smallIconsA = stackalloc nint[2];
        Assert.Equal(2u, NativeShell32.ExtractIconExA("sample.txt", 5, largeIconsA, smallIconsA, 2));
        Assert.NotEqual(0, largeIconsA[1]);
        Assert.NotEqual(0, smallIconsA[1]);

        NativeShell32.SHFILEINFOW shellFileInfoW = new()
        {
            _szDisplayName = string.Empty,
            _szTypeName = string.Empty,
        };
        Assert.NotEqual(
            0,
            NativeShell32.SHGetFileInfoW(
                "sample.txt",
                0x00000080,
                ref shellFileInfoW,
                (uint)Marshal.SizeOf<NativeShell32.SHFILEINFOW>(),
                SHGFI_ICON));
        Assert.NotEqual(0, shellFileInfoW._hIcon);
        Assert.Equal(0x00000080u, shellFileInfoW._dwAttributes);
        Assert.NotNull(shellFileInfoW._szDisplayName);
        Assert.NotNull(shellFileInfoW._szTypeName);
        Assert.Contains("WinFormsX File", shellFileInfoW._szDisplayName);
        Assert.Contains("WinFormsX Type", shellFileInfoW._szTypeName);

        NativeShell32.SHFILEINFOA shellFileInfoA = new()
        {
            _szDisplayName = string.Empty,
            _szTypeName = string.Empty,
        };
        Assert.NotEqual(
            0,
            NativeShell32.SHGetFileInfoA(
                "sample.txt",
                0x00000020,
                ref shellFileInfoA,
                (uint)Marshal.SizeOf<NativeShell32.SHFILEINFOA>(),
                SHGFI_ICON));
        Assert.NotEqual(0, shellFileInfoA._hIcon);
        Assert.Equal(0x00000020u, shellFileInfoA._dwAttributes);
        Assert.NotNull(shellFileInfoA._szDisplayName);
        Assert.NotNull(shellFileInfoA._szTypeName);
        Assert.Contains("WinFormsX File", shellFileInfoA._szDisplayName);
        Assert.Contains("WinFormsX Type", shellFileInfoA._szTypeName);
    }

    [Fact]
    public void DirectShlwApiDllImports_ResolveToWinFormsXFacade()
    {
        Assert.True(NativeShlwApi.PathFileExistsW(AppContext.BaseDirectory));
        Assert.False(NativeShlwApi.PathFileExistsW(Path.Combine(AppContext.BaseDirectory, "missing-winformsx-file.tmp")));
        Assert.False(NativeShlwApi.PathIsRelativeW(AppContext.BaseDirectory));
        Assert.True(NativeShlwApi.PathIsRelativeW("relative.txt"));
    }

    private sealed class EnvironmentOverride : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentOverride(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }

    private static unsafe partial class NativeUser32
    {
        private const string User32 = "USER32.dll";

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSG
        {
            internal nint _hwnd;
            internal uint _message;
            internal nuint _wParam;
            internal nint _lParam;
            internal uint _time;
            internal Point _pt;
        }

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out Point point);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern uint GetMessagePos();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern unsafe uint MsgWaitForMultipleObjectsEx(
            uint nCount,
            nint* pHandles,
            uint dwMilliseconds,
            uint dwWakeMask,
            uint dwFlags);

        [DllImport(User32, EntryPoint = "RegisterWindowMessageW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern uint RegisterWindowMessageW(string value);

        [DllImport(User32, EntryPoint = "RegisterWindowMessageA", CharSet = CharSet.Ansi, ExactSpelling = true)]
        internal static extern uint RegisterWindowMessageA(string value);

        [DllImport(User32, EntryPoint = "PostMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport(User32, EntryPoint = "PostMessageA", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessageA(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport(User32, EntryPoint = "SendMessageW", ExactSpelling = true)]
        internal static extern nint SendMessageW(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport(User32, EntryPoint = "SendMessageA", ExactSpelling = true)]
        internal static extern nint SendMessageA(nint hwnd, uint msg, nint wParam, nint lParam);

        [DllImport(User32, EntryPoint = "PeekMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessageW(out MSG msg, nint hwnd, uint filterMin, uint filterMax, uint removeFlags);

        [DllImport(User32, EntryPoint = "PeekMessageA", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekMessageA(out MSG msg, nint hwnd, uint filterMin, uint filterMax, uint removeFlags);

        [DllImport(User32, EntryPoint = "GetMessageW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMessageW(out MSG msg, nint hwnd, uint filterMin, uint filterMax);

        [DllImport(User32, EntryPoint = "GetMessageA", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMessageA(out MSG msg, nint hwnd, uint filterMin, uint filterMax);

        [DllImport(User32, EntryPoint = "TranslateMessage", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TranslateMessage(ref MSG msg);

        [DllImport(User32, EntryPoint = "DispatchMessageW", ExactSpelling = true)]
        internal static extern nint DispatchMessageW(ref MSG msg);

        [DllImport(User32, EntryPoint = "DispatchMessageA", ExactSpelling = true)]
        internal static extern nint DispatchMessageA(ref MSG msg);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern short GetAsyncKeyState(int vkey);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern short GetKeyState(int vkey);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool GetKeyboardState(byte* lpKeyState);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetKeyboardLayout(uint idThread);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern unsafe int GetKeyboardLayoutList(int nBuff, nint* lpList);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetCapture();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint SetCapture(nint hwnd);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReleaseCapture();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenClipboard(nint hWndNewOwner);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseClipboard();

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EmptyClipboard();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint SetClipboardData(uint format, nint data);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetClipboardData(uint format);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport(User32, EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport(User32, EntryPoint = "GetClipboardFormatNameW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int GetClipboardFormatName(uint format, [Out] char[] lpszFormatName, int cchMaxCount);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern unsafe uint SendInput(uint count, INPUT* inputs, int cbSize);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetDesktopWindow();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetFocus();

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint SetFocus(nint hwnd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int GetSystemMetrics(int index);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(nint hwnd, out RECT lpRect);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(nint hwnd, out RECT lpRect);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool GetWindowPlacement(nint hwnd, WINDOWPLACEMENT* placement);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool SetWindowPlacement(nint hwnd, WINDOWPLACEMENT* placement);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int MapWindowPoints(nint hWndFrom, nint hWndTo, ref Point point, uint cPoints);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(nint hwnd, ref Point lpPoint);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ScreenToClient(nint hwnd, ref Point lpPoint);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetParent(nint hwnd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint SetParent(nint child, nint parent);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetWindow(nint hwnd, uint uCmd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetAncestor(nint hwnd, uint gaFlags);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsChild(nint hWndParent, nint hWnd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint WindowFromPoint(Point point);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint ChildWindowFromPointEx(nint hwndParent, Point point, uint flags);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetMenu(nint hWnd, nint hMenu);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetMenu(nint hWnd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint GetSystemMenu(nint hWnd, bool bRevert);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnableMenuItem(nint hMenu, uint uIdEnableItem, uint uEnable);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int GetMenuItemCount(nint hMenu);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMenuItemInfo(nint hMenu, uint item, bool fByPosition, ref MENUITEMINFOW lpMenuItemInfo);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DrawMenuBar(nint hWnd);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateWindow(nint hwnd);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InvalidateRect(nint hwnd, nint lpRect, bool erase);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InvalidateRect(nint hwnd, ref RECT lpRect, bool erase);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ValidateRect(nint hwnd, nint lpRect);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ValidateRect(nint hwnd, ref RECT lpRect);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int SystemParametersInfo(uint uiAction, uint uiParam, void* pvParam, uint fWinIni);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern int SystemParametersInfoForDpi(uint uiAction, uint uiParam, void* pvParam, uint fWinIni, uint dpi);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern uint GetDpiForWindow(nint hwnd);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern uint GetDpiForSystem();

        [DllImport(User32, EntryPoint = "LoadIconW", ExactSpelling = true)]
        internal static extern nint LoadIcon(nint instance, nint iconName);

        [DllImport(User32, EntryPoint = "LoadCursorW", ExactSpelling = true)]
        internal static extern nint LoadCursor(nint instance, nint cursorName);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(nint icon);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyCursor(nint cursor);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint CopyIcon(nint icon);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint CopyCursor(nint cursor);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern nint CopyImage(nint image, uint imageType, int width, int height, uint flags);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetIconInfo(nint icon, out ICONINFO iconInfo);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DrawIcon(nint hdc, int x, int y, nint icon);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DrawIconEx(nint hdc, int x, int y, nint icon, int width, int height, uint step, nint brush, uint flags);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern unsafe nint CreateIconFromResourceEx(byte* bits, uint size, [MarshalAs(UnmanagedType.Bool)] bool icon, uint version, int width, int height, uint flags);

        internal struct ICONINFO
        {
            public int fIcon;
            public uint xHotspot;
            public uint yHotspot;
            public nint hbmMask;
            public nint hbmColor;
        }
    }

    private static unsafe partial class NativeImm32
    {
        private const string Imm32 = "IMM32.dll";

        [DllImport(Imm32, ExactSpelling = true)]
        internal static extern nint ImmAssociateContext(nint hwnd, nint himc);

        [DllImport(Imm32, ExactSpelling = true)]
        internal static extern nint ImmCreateContext();

        [DllImport(Imm32, ExactSpelling = true)]
        internal static extern nint ImmGetContext(nint hwnd);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmGetConversionStatus(nint himc, uint* conversion, uint* sentence);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmGetOpenStatus(nint himc);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmNotifyIME(nint himc, uint action, uint index, uint value);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmReleaseContext(nint hwnd, nint himc);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmSetConversionStatus(nint himc, uint conversion, uint sentence);

        [DllImport(Imm32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmSetOpenStatus(nint himc, bool open);
    }

    private static partial class NativeComDlg32
    {
        private const string ComDlg32 = "COMDLG32.dll";

        [DllImport(ComDlg32, EntryPoint = "GetOpenFileNameW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetOpenFileName(nint ofn);

        [DllImport(ComDlg32, EntryPoint = "GetSaveFileNameW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetSaveFileName(nint ofn);

        [DllImport(ComDlg32, EntryPoint = "ChooseColorW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChooseColor(nint chooseColor);

        [DllImport(ComDlg32, EntryPoint = "ChooseFontW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChooseFont(nint chooseFont);

        [DllImport(ComDlg32, EntryPoint = "PrintDlgW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PrintDlg(nint printDlg);

        [DllImport(ComDlg32, EntryPoint = "PrintDlgExW", ExactSpelling = true)]
        internal static extern int PrintDlgEx(nint printDlgEx);

        [DllImport(ComDlg32, EntryPoint = "PageSetupDlgW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PageSetupDlg(nint pageSetup);

        [DllImport(ComDlg32, ExactSpelling = true)]
        internal static extern uint CommDlgExtendedError();
    }

    private static partial class NativeComCtl32
    {
        private const string ComCtl32 = "COMCTL32.dll";

        [StructLayout(LayoutKind.Sequential)]
        internal struct INITCOMMONCONTROLSEX
        {
            internal uint _dwSize;
            internal uint _dwICC;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            internal int _left;
            internal int _top;
            internal int _right;
            internal int _bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IMAGEINFO
        {
            internal nint _hbmImage;
            internal nint _hbmMask;
            internal int _unused1;
            internal int _unused2;
            internal RECT _rcImage;
        }

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern void InitCommonControls();

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitCommonControlsEx(ref INITCOMMONCONTROLSEX icc);

        [DllImport(ComCtl32, EntryPoint = "WinFormsXComCtl32GetInitializedClasses", ExactSpelling = true)]
        internal static extern uint GetInitializedClasses();

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern nint ImageList_Create(int cx, int cy, uint flags, int cInitial, int cGrow);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_Destroy(nint himl);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern int ImageList_Add(nint himl, nint hbmImage, nint hbmMask);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern int ImageList_ReplaceIcon(nint himl, int i, nint hicon);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_Replace(nint himl, int i, nint hbmImage, nint hbmMask);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern int ImageList_AddMasked(nint himl, nint hbmImage, uint crMask);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern nint ImageList_GetIcon(nint himl, int i, uint flags);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_Remove(nint himl, int i);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern int ImageList_GetImageCount(nint himl);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_GetIconSize(nint himl, out int cx, out int cy);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_SetIconSize(nint himl, int cx, int cy);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_GetImageInfo(nint himl, int i, out IMAGEINFO imageInfo);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern uint ImageList_SetBkColor(nint himl, uint clrBk);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_Write(nint himl, nint pstm);

        [DllImport(ComCtl32, ExactSpelling = true)]
        internal static extern int ImageList_WriteEx(nint himl, uint flags, nint pstm);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_Draw(nint himl, int i, nint hdcDst, int x, int y, uint style);

        [DllImport(ComCtl32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImageList_DrawEx(
            nint himl,
            int i,
            nint hdcDst,
            int x,
            int y,
            int dx,
            int dy,
            uint rgbBk,
            uint rgbFg,
            uint style);
    }

    private static partial class NativeWinSpool
    {
        private const string WinSpool = "winspool.drv";

        [DllImport(WinSpool, EntryPoint = "EnumPrintersW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumPrinters(
            uint flags,
            nint name,
            uint level,
            nint printerEnum,
            uint bufferSize,
            out uint needed,
            out uint returned);

        [DllImport(WinSpool, EntryPoint = "DeviceCapabilitiesW", ExactSpelling = true)]
        internal static extern int DeviceCapabilities(nint device, nint port, ushort capability, nint output, nint devMode);

        [DllImport(WinSpool, EntryPoint = "DocumentPropertiesW", ExactSpelling = true)]
        internal static extern int DocumentProperties(nint hwnd, nint printer, nint deviceName, nint devModeOutput, nint devModeInput, uint mode);
    }

    private static unsafe partial class NativeKernel32
    {
        private const string Kernel32 = "KERNEL32.dll";

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GetCurrentProcess();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GetCurrentThread();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetCurrentProcessId();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetCurrentThreadId();

        [DllImport(Kernel32, EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern nint GetModuleHandle(string? lpModuleName);

        [DllImport(Kernel32, EntryPoint = "GetModuleFileNameW", ExactSpelling = true)]
        internal static extern uint GetModuleFileName(nint hModule, char* lpFilename, uint nSize);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern char* GetCommandLineW();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GetCommandLineA();

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetEnvironmentVariableW(string lpName, char* lpBuffer, uint nSize);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal static extern uint GetEnvironmentVariableA(string lpName, byte* lpBuffer, uint nSize);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetEnvironmentVariableW(string lpName, string? lpValue);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetEnvironmentVariableA(string lpName, string? lpValue);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern uint ExpandEnvironmentStringsW(string lpSrc, char* lpDst, uint nSize);

        [DllImport(Kernel32, ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal static extern uint ExpandEnvironmentStringsA(string lpSrc, byte* lpDst, uint nSize);

        [DllImport(Kernel32, EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern nint LoadLibraryEx(string lpLibFileName, nint hFile, uint dwFlags);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool FreeLibrary(nint hLibModule);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GetProcAddress(nint hModule, string lpProcName);

        [DllImport(Kernel32, EntryPoint = "FindResourceW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern nint FindResource(nint hModule, string lpName, string lpType);

        [DllImport(Kernel32, EntryPoint = "FindResourceExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern nint FindResourceEx(nint hModule, string lpType, string lpName, ushort wLanguage);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LoadResource(nint hModule, nint hResInfo);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LockResource(nint hResData);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint SizeofResource(nint hModule, nint hResInfo);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool FreeResource(nint hResData);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetLastError();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern void SetLastError(uint dwErrCode);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool CloseHandle(nint hObject);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool DuplicateHandle(
            nint hSourceProcessHandle,
            nint hSourceHandle,
            nint hTargetProcessHandle,
            nint* lpTargetHandle,
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwOptions);

        [DllImport(Kernel32, EntryPoint = "FormatMessageW", ExactSpelling = true)]
        internal static extern uint FormatMessage(
            uint dwFlags,
            nint lpSource,
            uint dwMessageId,
            uint dwLanguageId,
            char* lpBuffer,
            uint nSize,
            nint Arguments);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool GetExitCodeThread(nint hThread, uint* lpExitCode);

        [DllImport(Kernel32, CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal static extern int GetLocaleInfoEx(string lpLocaleName, uint LCType, char* lpLCData, int cchData);

        [DllImport(Kernel32, EntryPoint = "GetStartupInfoW", ExactSpelling = true)]
        internal static extern void GetStartupInfo(STARTUPINFOW* lpStartupInfo);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetThreadLocale();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetTickCount();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern ulong GetTickCount64();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool QueryPerformanceFrequency(long* lpFrequency);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool QueryPerformanceCounter(long* lpPerformanceCount);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetACP();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetOEMCP();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetSystemDefaultLCID();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern uint GetUserDefaultLCID();

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern void GetSystemTimeAsFileTime(FILETIME* lpSystemTimeAsFileTime);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern void GetSystemTime(SYSTEMTIME* lpSystemTime);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern void GetLocalTime(SYSTEMTIME* lpSystemTime);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool FileTimeToSystemTime(FILETIME* lpFileTime, SYSTEMTIME* lpSystemTime);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool SystemTimeToFileTime(SYSTEMTIME* lpSystemTime, FILETIME* lpFileTime);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GlobalAlloc(uint uFlags, nuint dwBytes);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GlobalReAlloc(nint hMem, nuint dwBytes, uint uFlags);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GlobalLock(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool GlobalUnlock(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nuint GlobalSize(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint GlobalFree(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LocalAlloc(uint uFlags, nuint uBytes);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LocalReAlloc(nint hMem, nuint uBytes, uint uFlags);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LocalLock(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool LocalUnlock(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nuint LocalSize(nint hMem);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern nint LocalFree(nint hMem);

        [DllImport(Kernel32, EntryPoint = "CreateActCtxW", ExactSpelling = true)]
        internal static extern nint CreateActCtx(ACTCTXW* pActCtx);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool ActivateActCtx(nint hActCtx, nuint* lpCookie);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool DeactivateActCtx(uint dwFlags, nuint ulCookie);

        [DllImport(Kernel32, ExactSpelling = true)]
        internal static extern bool GetCurrentActCtx(nint* lphActCtx);

        internal struct ACTCTXW
        {
            public uint cbSize;
            public uint dwFlags;
            public char* lpSource;
            public ushort wProcessorArchitecture;
            public ushort wLangId;
            public char* lpAssemblyDirectory;
            public char* lpResourceName;
            public char* lpApplicationName;
            public nint hModule;
        }

        internal struct STARTUPINFOW
        {
            public uint cb;
            public char* lpReserved;
            public char* lpDesktop;
            public char* lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public byte* lpReserved2;
            public nint hStdInput;
            public nint hStdOutput;
            public nint hStdError;
        }

        internal struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        internal struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }
    }

    private static unsafe partial class NativeDwmApi
    {
        private const string DwmApi = "DWMAPI.dll";

        [DllImport(DwmApi, ExactSpelling = true)]
        internal static extern int DwmSetWindowAttribute(nint hwnd, uint dwAttribute, void* pvAttribute, uint cbAttribute);

        [DllImport(DwmApi, ExactSpelling = true)]
        internal static extern int DwmGetWindowAttribute(nint hwnd, uint dwAttribute, void* pvAttribute, uint cbAttribute);
    }

    private static unsafe partial class NativeUxTheme
    {
        private const string UxTheme = "UXTHEME.dll";

        [DllImport(UxTheme, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint OpenThemeData(nint hwnd, string classList);

        [DllImport(UxTheme, ExactSpelling = true)]
        internal static extern int CloseThemeData(nint theme);

        [DllImport(UxTheme, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int SetWindowTheme(nint hwnd, string? subAppName, string? subIdList);

        [DllImport(UxTheme, ExactSpelling = true)]
        internal static extern bool IsThemePartDefined(nint theme, int partId, int stateId);

        [DllImport(UxTheme, ExactSpelling = true)]
        internal static extern int GetThemePartSize(nint theme, nint hdc, int partId, int stateId, NativeRect* rect, int sizeType, NativeSize* size);

        [DllImport(UxTheme, ExactSpelling = true)]
        internal static extern int GetThemeMargins(nint theme, nint hdc, int partId, int stateId, int propId, NativeRect* rect, NativeMargins* margins);

        internal struct NativeRect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        internal struct NativeSize
        {
            public int cx;
            public int cy;
        }

        internal struct NativeMargins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }
    }

    private static unsafe partial class NativeOle32
    {
        private const string Ole32 = "OLE32.dll";

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleInitialize(void* reserved);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern void OleUninitialize();

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CoCreateInstance(void* clsid, nint outer, uint clsContext, void* iid, nint* createdObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern nint CoTaskMemAlloc(nuint size);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern nint CoTaskMemRealloc(nint block, nuint size);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern void CoTaskMemFree(nint block);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CoGetMalloc(uint memContext, nint* mallocObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CoCreateGuid(Guid* guid);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CoRegisterMessageFilter(nint messageFilter, nint* previousMessageFilter);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CreateILockBytesOnHGlobal(nint hGlobal, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, nint* lockBytes);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CreateOleAdviseHolder(nint* adviseHolder);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CreateDataAdviseHolder(nint* adviseHolder);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int CreateStreamOnHGlobal(nint hGlobal, [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease, nint* stream);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int GetHGlobalFromStream(nint stream, nint* hGlobal);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleSetClipboard(nint dataObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleGetClipboard(nint* dataObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleFlushClipboard();

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleIsCurrentClipboard(nint dataObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int RegisterDragDrop(nint hwnd, nint dropTarget);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int RevokeDragDrop(nint hwnd);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int DoDragDrop(nint dataObject, nint dropSource, uint allowedEffects, uint* effect);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern void ReleaseStgMedium(NativeStgMedium* medium);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleCreatePictureIndirect(void* pictureDescription, void* iid, [MarshalAs(UnmanagedType.Bool)] bool ownsHandle, nint* pictureObject);

        internal struct NativeStgMedium
        {
            public uint tymed;
            public nint unionMember;
            public nint pUnkForRelease;
        }
    }

    private static unsafe partial class NativeOleAut32
    {
        private const string OleAut32 = "OLEAUT32.dll";

        [DllImport(OleAut32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint SysAllocString(string value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern void SysFreeString(nint value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern uint SysStringLen(nint value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern uint SysStringByteLen(nint value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int VariantClear(NativeVariant* variant);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int LoadRegTypeLib(void* libId, ushort majorVersion, ushort minorVersion, uint lcid, nint* typeLib);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern void* SafeArrayCreate(ushort vt, uint dimensions, void* bounds);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayDestroy(void* array);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern uint SafeArrayGetDim(void* array);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayGetLBound(void* array, uint dimension, int* lowerBound);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayGetUBound(void* array, uint dimension, int* upperBound);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayGetVartype(void* array, ushort* vt);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayAccessData(void* array, void** data);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayUnaccessData(void* array);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayPtrOfIndex(void* array, int* indices, void** data);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayPutElement(void* array, int* indices, void* value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayGetElement(void* array, int* indices, void* value);

        [DllImport(OleAut32, ExactSpelling = true)]
        internal static extern int SafeArrayRedim(void* array, SAFEARRAYBOUND* bound);

        internal struct SAFEARRAYBOUND
        {
            public uint cElements;
            public int lLbound;
        }

        internal struct NativeVariant
        {
            public ushort vt;
            public ushort reserved1;
            public ushort reserved2;
            public ushort reserved3;
            public nint data1;
            public nint data2;
        }
    }

    private static partial class NativeGdi32
    {
        private const string Gdi32 = "GDI32.dll";

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateCompatibleDC(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int GetDeviceCaps(nint hdc, int index);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint SetTextColor(nint hdc, uint color);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint GetTextColor(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint SetBkColor(nint hdc, uint color);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint GetBkColor(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int SetBkMode(nint hdc, int mode);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int GetBkMode(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateSolidBrush(uint color);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreatePen(int style, int width, uint color);

        [DllImport(Gdi32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(nint obj);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint SelectObject(nint hdc, nint obj);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateBrushIndirect(ref LOGBRUSH brush);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreatePatternBrush(nint bitmap);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateCompatibleBitmap(nint hdc, int width, int height);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateDIBSection(nint hdc, nint bitmapInfo, uint usage, out nint bits, nint section, uint offset);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateFontIndirectW(nint logFont);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int CombineRgn(nint destination, nint source1, nint source2, int mode);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int SelectClipRgn(nint hdc, nint region);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int IntersectClipRect(nint hdc, int left, int top, int right, int bottom);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int GetClipBox(nint hdc, out RECT rect);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint CreateHalftonePalette(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern nint SelectPalette(nint hdc, nint palette, [MarshalAs(UnmanagedType.Bool)] bool forceBackground);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint RealizePalette(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern uint GetPaletteEntries(nint palette, uint start, uint count, [Out] PALETTEENTRY[] entries);

        [DllImport(Gdi32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PatBlt(nint hdc, int x, int y, int width, int height, uint rop);

        [DllImport(Gdi32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BitBlt(nint hdc, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint rop);

        [DllImport(Gdi32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint CreateDCW(string? driver, string? device, string? output, nint initData);

        [DllImport(Gdi32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint CreateICW(string? driver, string? device, string? output, nint initData);

        [DllImport(Gdi32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern int StartDocW(nint hdc, ref DOCINFOW docInfo);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int StartPage(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int EndPage(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int EndDoc(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int AbortDoc(nint hdc);

        [DllImport(Gdi32, ExactSpelling = true)]
        internal static extern int ExtEscape(nint hdc, int escape, int inputSize, nint input, int outputSize, nint output);

        internal struct LOGBRUSH
        {
            public uint lbStyle;
            public uint lbColor;
            public nuint lbHatch;
        }

        internal struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        internal struct PALETTEENTRY
        {
            public byte peRed;
            public byte peGreen;
            public byte peBlue;
            public byte peFlags;
        }

        internal struct DOCINFOW
        {
            public int cbSize;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszDocName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszOutput;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszDatatype;
            public uint fwType;
        }
    }

    private static partial class NativeGdiPlus
    {
        private const string GdiPlus = "gdiplus.dll";

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdiplusStartup(
            out nuint token,
            ref GdiplusStartupInput input,
            out GdiplusStartupOutput output);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern void GdiplusShutdown(nuint token);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageDecodersSize(out uint numberOfDecoders, out uint size);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipCreateBitmapFromScan0(
            int width,
            int height,
            int stride,
            int pixelFormat,
            nint scan0,
            out nint bitmap);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipCreateBitmapFromHBITMAP(nint hbm, nint hpal, out nint bitmap);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipCreateBitmapFromHICON(nint hicon, out nint bitmap);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipLoadImageFromFile([MarshalAs(UnmanagedType.LPWStr)] string? filename, out nint image);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipLoadImageFromFileICM([MarshalAs(UnmanagedType.LPWStr)] string? filename, out nint image);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipLoadImageFromStream(nint stream, out nint image);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipLoadImageFromStreamICM(nint stream, out nint image);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipDisposeImage(nint image);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageWidth(nint image, out uint width);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageHeight(nint image, out uint height);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageFlags(nint image, out uint flags);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImagePixelFormat(nint image, out int pixelFormat);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageHorizontalResolution(nint image, out float resolution);

        [DllImport(GdiPlus, ExactSpelling = true)]
        internal static extern int GdipGetImageVerticalResolution(nint image, out float resolution);

        [StructLayout(LayoutKind.Sequential)]
        internal struct GdiplusStartupInput
        {
            internal uint GdiplusVersion;
            internal nint DebugEventCallback;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool SuppressBackgroundThread;
            [MarshalAs(UnmanagedType.Bool)]
            internal bool SuppressExternalCodecs;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GdiplusStartupOutput
        {
            internal nint NotificationHook;
            internal nint NotificationUnhook;
        }
    }

    private static unsafe partial class NativeShell32
    {
        private const string Shell32 = "SHELL32.dll";

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SHSTOCKICONINFOW
        {
            internal uint _cbSize;
            internal nint _hIcon;
            internal int _iSysImageIndex;
            internal int _iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string? _szPath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct SHFILEINFOW
        {
            internal nint _hIcon;
            internal int _iIcon;
            internal uint _dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string? _szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            internal string? _szTypeName;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct SHFILEINFOA
        {
            internal nint _hIcon;
            internal int _iIcon;
            internal uint _dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string? _szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            internal string? _szTypeName;
        }

        [DllImport(Shell32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Shell_NotifyIconW(uint message, nint data);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern void DragAcceptFiles(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool accept);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern uint DragQueryFileW(nint drop, uint file, char* buffer, uint count);

        [DllImport(Shell32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SHGetPathFromIDListEx(nint pidl, char* path, uint pathCount, uint flags);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern nint SHBrowseForFolderW(nint browseInfo);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern int SHCreateShellItem(nint parentPidl, nint parentFolder, nint pidl, nint* shellItem);

        [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint ShellExecuteW(nint hwnd, string? operation, string? file, string? parameters, string? directory, int showCommand);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern int SHGetStockIconInfo(int stockIconId, uint flags, ref SHSTOCKICONINFOW stockIconInfo);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern nint ExtractAssociatedIconW(nint instance, char* iconPath, ushort* iconIndex);

        [DllImport(Shell32, ExactSpelling = true)]
        internal static extern nint ExtractAssociatedIconA(nint instance, byte* iconPath, ushort* iconIndex);

        [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern uint ExtractIconExW(string? fileName, int iconIndex, nint* largeIcons, nint* smallIcons, uint iconCount);

        [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal static extern uint ExtractIconExA(string? fileName, int iconIndex, nint* largeIcons, nint* smallIcons, uint iconCount);

        [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Unicode)]
        internal static extern nint SHGetFileInfoW(string? path, uint fileAttributes, ref SHFILEINFOW shellFileInfo, uint cbFileInfo, uint flags);

        [DllImport(Shell32, ExactSpelling = true, CharSet = CharSet.Ansi)]
        internal static extern nint SHGetFileInfoA(string? path, uint fileAttributes, ref SHFILEINFOA shellFileInfo, uint cbFileInfo, uint flags);
    }

    private static partial class NativeShlwApi
    {
        private const string ShlwApi = "SHLWAPI.dll";

        [DllImport(ShlwApi, ExactSpelling = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PathFileExistsW(string path);

        [DllImport(ShlwApi, ExactSpelling = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PathIsRelativeW(string path);
    }
}
