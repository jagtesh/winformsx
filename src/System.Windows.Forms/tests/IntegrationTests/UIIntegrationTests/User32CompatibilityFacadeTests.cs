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

                Assert.True(NativeComCtl32.ImageList_GetImageInfo(imageList, 0, out NativeComCtl32.IMAGEINFO imageInfo));
                Assert.NotEqual(nint.Zero, imageInfo._hbmImage);
                Assert.Equal(16, imageInfo._rcImage._right);
                Assert.Equal(20, imageInfo._rcImage._bottom);

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

            nint loadedModule = NativeKernel32.LoadLibraryEx("WinFormsX.LoaderSmoke.dll", nint.Zero, 0);
            Assert.NotEqual(nint.Zero, loadedModule);
            Assert.Equal((nint)PInvoke.LoadLibraryEx("WinFormsX.LoaderSmoke.dll", 0), loadedModule);
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

        nint dataObject = 0x12345678;
        Assert.Equal(S_OK, NativeOle32.OleSetClipboard(dataObject));

        nint actualDataObject;
        Assert.Equal(S_OK, NativeOle32.OleGetClipboard(&actualDataObject));
        Assert.Equal(dataObject, actualDataObject);
        Assert.Equal(S_OK, NativeOle32.OleFlushClipboard());

        nint hwnd = 0x2468;
        nint dropTarget = 0x1357;
        Assert.Equal(S_OK, NativeOle32.RegisterDragDrop(hwnd, dropTarget));
        Assert.Equal(DRAGDROP_E_ALREADYREGISTERED, NativeOle32.RegisterDragDrop(hwnd, dropTarget));

        uint effect = 1;
        Assert.Equal(DRAGDROP_S_CANCEL, NativeOle32.DoDragDrop(dataObject, 0, 1, &effect));
        Assert.Equal(0u, effect);

        Assert.Equal(S_OK, NativeOle32.RevokeDragDrop(hwnd));
        NativeOle32.OleUninitialize();
    }

    [Fact]
    public unsafe void DirectOleAut32DllImports_ResolveToWinFormsXFacade()
    {
        const int S_OK = 0;
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

        Assert.Equal(0, (nint)NativeOleAut32.SafeArrayCreate(0, 1, null));
        Assert.Equal(S_OK, NativeOleAut32.SafeArrayDestroy(null));
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
        internal static extern int OleSetClipboard(nint dataObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleGetClipboard(nint* dataObject);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int OleFlushClipboard();

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int RegisterDragDrop(nint hwnd, nint dropTarget);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int RevokeDragDrop(nint hwnd);

        [DllImport(Ole32, ExactSpelling = true)]
        internal static extern int DoDragDrop(nint dataObject, nint dropSource, uint allowedEffects, uint* effect);
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
}
