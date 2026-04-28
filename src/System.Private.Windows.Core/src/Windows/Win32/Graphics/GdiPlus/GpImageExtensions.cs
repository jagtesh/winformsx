// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
namespace Windows.Win32.Graphics.GdiPlus;

internal static unsafe class GpImageExtensions
{
    internal static RectangleF GetImageBounds(this IPointer<GpImage> image)
    {
        throw new NotSupportedException("Native GDI+ image handles are not supported by the managed drawing PAL.");
    }

    internal static PixelFormat GetPixelFormat(this IPointer<GpImage> image)
    {
        throw new NotSupportedException("Native GDI+ image handles are not supported by the managed drawing PAL.");
    }
}
