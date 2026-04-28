// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.Gdi;

internal readonly partial struct HRGN
{
    public unsafe RECT[] GetRegionRects()
    {
        return [];
    }
}
