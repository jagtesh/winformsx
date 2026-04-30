// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Gdi32Tests;

public class GdiCompatibilityTests
{
    public GdiCompatibilityTests()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(typeof(Application).Module.ModuleHandle);
    }

    [Fact]
    public void GetDeviceCaps_ReturnsDeterministicDisplayDefaults()
    {
        Assert.Equal(96, PInvokeCore.GetDeviceCaps(default, GET_DEVICE_CAPS_INDEX.LOGPIXELSX));
        Assert.Equal(96, PInvokeCore.GetDeviceCaps(default, GET_DEVICE_CAPS_INDEX.LOGPIXELSY));
        Assert.Equal(32, PInvokeCore.GetDeviceCaps(default, GET_DEVICE_CAPS_INDEX.BITSPIXEL));
        Assert.Equal(1, PInvokeCore.GetDeviceCaps(default, GET_DEVICE_CAPS_INDEX.PLANES));
    }

    [Fact]
    public void GetSysColor_ReturnsPaletteBackedColors()
    {
        Assert.Equal((uint)0x00FFFFFF, PInvoke.GetSysColor(SYS_COLOR_INDEX.COLOR_WINDOW).Value);
        Assert.Equal((uint)0x00000000, PInvoke.GetSysColor(SYS_COLOR_INDEX.COLOR_WINDOWTEXT).Value);
        Assert.Equal((uint)0x00F0F0F0, PInvoke.GetSysColor(SYS_COLOR_INDEX.COLOR_BTNFACE).Value);
    }

    [Fact]
    public void DeviceContextColors_ReturnPreviousAndCurrentState()
    {
        HDC hdc = PInvoke.CreateCompatibleDC(default);
        try
        {
            COLORREF red = new(0x000000FF);
            COLORREF blue = new(0x00FF0000);

            Assert.Equal((uint)0x00000000, PInvoke.GetTextColor(hdc).Value);
            Assert.Equal((uint)0x00000000, PInvoke.SetTextColor(hdc, red).Value);
            Assert.Equal(red, PInvoke.GetTextColor(hdc));
            Assert.Equal(red, PInvoke.SetTextColor(hdc, blue));
            Assert.Equal(blue, PInvoke.GetTextColor(hdc));

            COLORREF yellow = new(0x0000FFFF);
            Assert.Equal((uint)0x00FFFFFF, PInvoke.GetBkColor(hdc).Value);
            Assert.Equal((uint)0x00FFFFFF, PInvoke.SetBkColor(hdc, yellow).Value);
            Assert.Equal(yellow, PInvoke.GetBkColor(hdc));
        }
        finally
        {
            PInvoke.DeleteDC(hdc);
        }
    }
}
