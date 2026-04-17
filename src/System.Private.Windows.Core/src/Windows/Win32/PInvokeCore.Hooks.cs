// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static Func<HDC, int, int, int, int, HDC, int, int, ROP_CODE, BOOL>? BitBltCallback { get; set; }
    public static Func<HDC, HDC>? CreateCompatibleDCCallback { get; set; }

    [DllImport("GDI32.dll", ExactSpelling = true, EntryPoint = "BitBlt")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern BOOL BitBlt_Native(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop);

    public static BOOL BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop)
    {
        if (BitBltCallback is object)
        {
            return BitBltCallback(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
        }

        return BitBlt_Native(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
    }

    [DllImport("GDI32.dll", ExactSpelling = true, EntryPoint = "CreateCompatibleDC")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern HDC CreateCompatibleDC_Native(HDC hdc);

    public static HDC CreateCompatibleDC(HDC hdc)
    {
        if (CreateCompatibleDCCallback is object)
        {
            return CreateCompatibleDCCallback(hdc);
        }

        return CreateCompatibleDC_Native(hdc);
    }
}
