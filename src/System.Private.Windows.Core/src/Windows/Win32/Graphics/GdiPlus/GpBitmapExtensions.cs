// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace Windows.Win32.Graphics.GdiPlus;

internal static unsafe class GpBitmapExtensions
{
    public static void LockBits(
        this IPointer<GpBitmap> bitmap,
        Rectangle rect,
        ImageLockMode flags,
        PixelFormat format,
        ref BitmapData data)
    {
        throw new NotSupportedException("Native GDI+ bitmap handles are not supported by the managed drawing PAL.");
    }

    public static void UnlockBits(this IPointer<GpBitmap> bitmap, ref BitmapData data)
    {
        throw new NotSupportedException("Native GDI+ bitmap handles are not supported by the managed drawing PAL.");
    }

    public static HBITMAP GetHBITMAP(this IPointer<GpBitmap> bitmap) => bitmap.GetHBITMAP(Color.LightGray);

    public static HBITMAP GetHBITMAP(this IPointer<GpBitmap> bitmap, Color background)
    {
        throw new NotSupportedException("Native GDI+ bitmap handles are not supported by the managed drawing PAL.");
    }
}
