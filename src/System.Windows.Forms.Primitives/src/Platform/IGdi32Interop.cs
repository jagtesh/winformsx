// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.



namespace System.Windows.Forms.Platform
{
    internal interface IGdi32Interop
    {
        HDC GetDC(HWND hWnd);
        HDC GetDCEx(HWND hWnd, HRGN hrgnClip, GET_DCX_FLAGS flags);
        int ReleaseDC(HWND hWnd, HDC hDC);
        BOOL BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop);
        HDC CreateCompatibleDC(HDC hdc);
    }
}
