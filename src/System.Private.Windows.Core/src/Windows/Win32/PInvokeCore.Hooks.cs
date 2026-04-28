// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static Func<HDC, int, int, int, int, HDC, int, int, ROP_CODE, BOOL>? BitBltCallback { get; set; }
    public static Func<HDC, HDC>? CreateCompatibleDCCallback { get; set; }

    public static BOOL BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop)
    {
        if (BitBltCallback is object)
        {
            return BitBltCallback(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
        }

        return BOOL.FALSE;
    }

    public static HDC CreateCompatibleDC(HDC hdc)
    {
        if (CreateCompatibleDCCallback is object)
        {
            return CreateCompatibleDCCallback(hdc);
        }

        return HDC.Null;
    }
}
