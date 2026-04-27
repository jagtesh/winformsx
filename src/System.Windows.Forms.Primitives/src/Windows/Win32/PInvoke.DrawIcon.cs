// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static new BOOL DrawIcon<T>(HDC hDC, int x, int y, T hIcon)
        where T : IHandle<HICON>
    {
        BOOL result = DrawIconEx(hDC, x, y, hIcon, 0, 0, DI_FLAGS.DI_NORMAL | DI_FLAGS.DI_DEFAULTSIZE);
        GC.KeepAlive(hIcon.Wrapper);
        return result;
    }
}
