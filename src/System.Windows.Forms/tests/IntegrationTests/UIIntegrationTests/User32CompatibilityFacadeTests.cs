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
    [Fact]
    public void DirectDllImports_RouteToWinFormsXPal()
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
    }

    private static partial class NativeUser32
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
    }
}
