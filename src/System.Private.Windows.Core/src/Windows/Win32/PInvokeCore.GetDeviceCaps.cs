// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

using Windows.Win32.Graphics.Gdi;

internal static partial class PInvokeCore
{
    public static int GetDeviceCaps(HDC hdc, GET_DEVICE_CAPS_INDEX index) => index switch
    {
        GET_DEVICE_CAPS_INDEX.LOGPIXELSX => 96,
        GET_DEVICE_CAPS_INDEX.LOGPIXELSY => 96,
        GET_DEVICE_CAPS_INDEX.BITSPIXEL => 32,
        GET_DEVICE_CAPS_INDEX.PLANES => 1,
        GET_DEVICE_CAPS_INDEX.HORZRES => 1920,
        GET_DEVICE_CAPS_INDEX.VERTRES => 1080,
        _ => 0,
    };
}
