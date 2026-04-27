// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="DrawIconEx(HDC, int, int, HICON, int, int, uint, HBRUSH, DI_FLAGS)"/>
    public static new BOOL DrawIconEx<T>(
        HDC hDC, int xLeft, int yTop, T hIcon,
        int cxWidth, int cyWidth,
        DI_FLAGS diFlags = DI_FLAGS.DI_NORMAL)
        where T : IHandle<HICON>
    {
        BOOL result = DrawIconEx(hDC, xLeft, yTop, hIcon.Handle, cxWidth, cyWidth, 0, HBRUSH.Null, diFlags);
        GC.KeepAlive(hIcon.Wrapper);
        return result;
    }
}
