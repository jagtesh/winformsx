// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Windows.Win32.Graphics.Dwm;

namespace Windows.Win32;

internal static unsafe partial class PInvoke
{
    private static readonly ConcurrentDictionary<(nint Hwnd, DWMWINDOWATTRIBUTE Attribute), byte[]> s_dwmAttributes = [];

    public static HRESULT DwmSetWindowAttribute(HWND hwnd, DWMWINDOWATTRIBUTE dwAttribute, void* pvAttribute, uint cbAttribute)
    {
        if (pvAttribute is null || cbAttribute == 0 || cbAttribute > 1024)
        {
            return HRESULT.E_INVALIDARG;
        }

        byte[] value = new byte[cbAttribute];
        fixed (byte* destination = value)
        {
            Buffer.MemoryCopy(pvAttribute, destination, cbAttribute, cbAttribute);
        }

        s_dwmAttributes[((nint)hwnd.Value, dwAttribute)] = value;
        return HRESULT.S_OK;
    }

    public static HRESULT DwmGetWindowAttribute(HWND hwnd, DWMWINDOWATTRIBUTE dwAttribute, void* pvAttribute, uint cbAttribute)
    {
        if (pvAttribute is null || cbAttribute == 0)
        {
            return HRESULT.E_INVALIDARG;
        }

        if (!s_dwmAttributes.TryGetValue(((nint)hwnd.Value, dwAttribute), out byte[]? value))
        {
            value = GetDefaultDwmAttribute(dwAttribute, cbAttribute);
        }

        uint copyLength = Math.Min(cbAttribute, (uint)value.Length);
        fixed (byte* source = value)
        {
            Buffer.MemoryCopy(source, pvAttribute, cbAttribute, copyLength);
        }

        return HRESULT.S_OK;
    }

    private static byte[] GetDefaultDwmAttribute(DWMWINDOWATTRIBUTE attribute, uint requestedSize)
    {
        byte[] value = new byte[Math.Min(requestedSize, 16)];

        if (attribute == DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE && value.Length >= sizeof(DWM_WINDOW_CORNER_PREFERENCE))
        {
            fixed (byte* pointer = value)
            {
                *(DWM_WINDOW_CORNER_PREFERENCE*)pointer = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT;
            }
        }

        return value;
    }
}
