// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.Graphics.GdiPlus;

internal static unsafe class GpMetafileExtensions
{
    public static HENHMETAFILE GetHENHMETAFILE(this IPointer<GpMetafile> metafile)
    {
        throw new NotSupportedException("Metafile handles are not supported by the managed drawing PAL.");
    }
}
