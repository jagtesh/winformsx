// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms;

internal class CachedItemHdcInfo : IDisposable, IHandle<HDC>
{
    private static int s_nextSyntheticHandle = 1;

    internal CachedItemHdcInfo()
    {
    }

    private HDC _cachedItemHDC;
    private Size _cachedHDCSize = Size.Empty;
    private HBITMAP _cachedItemBitmap;

    public HDC Handle => _cachedItemHDC;

    // this DC is cached and should only be deleted on Dispose or when the size changes.

    public HDC GetCachedItemDC(HDC toolStripHDC, Size bitmapSize)
    {
        if (_cachedHDCSize.Width < bitmapSize.Width
             || _cachedHDCSize.Height < bitmapSize.Height)
        {
            if (_cachedItemHDC.IsNull)
            {
                _cachedItemHDC = CreateSyntheticHdc();
            }

            _cachedItemBitmap = CreateSyntheticBitmap();
            _cachedHDCSize = bitmapSize;
        }

        return _cachedItemHDC;
    }

    public void Dispose()
    {
        _cachedItemHDC = default;
        _cachedItemBitmap = default;
        _cachedHDCSize = Size.Empty;

        GC.SuppressFinalize(this);
    }

    ~CachedItemHdcInfo()
    {
        Dispose();
    }

    private static HDC CreateSyntheticHdc() => (HDC)(nint)Interlocked.Increment(ref s_nextSyntheticHandle);

    private static HBITMAP CreateSyntheticBitmap() => (HBITMAP)(nint)Interlocked.Increment(ref s_nextSyntheticHandle);
}
