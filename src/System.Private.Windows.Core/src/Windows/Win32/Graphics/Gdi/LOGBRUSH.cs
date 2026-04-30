// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.Gdi;

internal struct LOGBRUSH
{
    public BRUSH_STYLE lbStyle;
    public uint lbColor;
    public nuint lbHatch;
}
