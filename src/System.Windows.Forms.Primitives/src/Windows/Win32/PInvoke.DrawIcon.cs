// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static new BOOL DrawIcon<T>(HDC hDC, int x, int y, T hIcon)
        where T : IHandle<HICON>
    {
        GC.KeepAlive(hIcon.Wrapper);
        return true;
    }
}
