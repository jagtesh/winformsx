// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms.UITests.Input;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace System.Windows.Forms.UITests;

public class User32CompatibilityFacadeTests
{
    private const uint SPIF_SENDCHANGE = 0x0002;

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
        internal static extern short GetAsyncKeyState(int vkey);

        [DllImport(User32, ExactSpelling = true)]
        internal static extern short GetKeyState(int vkey);

        [DllImport(User32, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool GetKeyboardState(byte* lpKeyState);

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
}
