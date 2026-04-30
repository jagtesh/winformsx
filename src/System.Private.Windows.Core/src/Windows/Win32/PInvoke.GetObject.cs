// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static unsafe int GetObject(HGDIOBJ h, int c, void* pv)
    {
        if (pv is null || c <= 0)
        {
            return 0;
        }

        if (c >= sizeof(LOGBRUSH) && TryGetStockBrush(h, out LOGBRUSH brush))
        {
            *(LOGBRUSH*)pv = brush;
            return sizeof(LOGBRUSH);
        }

        return 0;
    }

    /// <inheritdoc cref="GetObject(HGDIOBJ,int,void*)"/>
    public static unsafe bool GetObject<T>(HGDIOBJ h, out T @object) where T : unmanaged
    {
        // HGDIOBJ isn't technically correct, but close enough to filter out bigger mistakes (HWND, etc.).

        @object = default;
        fixed (void* pv = &@object)
        {
            return GetObject(h, sizeof(T), pv) != 0;
        }
    }

    private static bool TryGetStockBrush(HGDIOBJ h, out LOGBRUSH brush)
    {
        if (!TryGetStockObject(h, out GET_STOCK_OBJECT_FLAGS stockObject))
        {
            brush = default;
            return false;
        }

        brush = stockObject switch
        {
            GET_STOCK_OBJECT_FLAGS.WHITE_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00FFFFFF),
            GET_STOCK_OBJECT_FLAGS.LTGRAY_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00C0C0C0),
            GET_STOCK_OBJECT_FLAGS.GRAY_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00808080),
            GET_STOCK_OBJECT_FLAGS.DKGRAY_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00404040),
            GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00000000),
            GET_STOCK_OBJECT_FLAGS.NULL_BRUSH => CreateBrush(BRUSH_STYLE.BS_HOLLOW, 0x00000000),
            GET_STOCK_OBJECT_FLAGS.DC_BRUSH => CreateBrush(BRUSH_STYLE.BS_SOLID, 0x00FFFFFF),
            _ => default,
        };

        return stockObject is >= GET_STOCK_OBJECT_FLAGS.WHITE_BRUSH and <= GET_STOCK_OBJECT_FLAGS.NULL_BRUSH
            or GET_STOCK_OBJECT_FLAGS.DC_BRUSH;
    }

    private static LOGBRUSH CreateBrush(BRUSH_STYLE style, uint color) => new()
    {
        lbStyle = style,
        lbColor = color,
    };
}
