// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static uint GetObjectType(HGDIOBJ h)
    {
        if (TryGetStockObject(h, out GET_STOCK_OBJECT_FLAGS i))
        {
            return (uint)(i switch
            {
                >= GET_STOCK_OBJECT_FLAGS.WHITE_BRUSH and <= GET_STOCK_OBJECT_FLAGS.HOLLOW_BRUSH
                    or GET_STOCK_OBJECT_FLAGS.DC_BRUSH => OBJ_TYPE.OBJ_BRUSH,
                >= GET_STOCK_OBJECT_FLAGS.WHITE_PEN and <= GET_STOCK_OBJECT_FLAGS.NULL_PEN
                    or GET_STOCK_OBJECT_FLAGS.DC_PEN => OBJ_TYPE.OBJ_PEN,
                GET_STOCK_OBJECT_FLAGS.DEFAULT_PALETTE => OBJ_TYPE.OBJ_PAL,
                _ => OBJ_TYPE.OBJ_FONT,
            });
        }

        return h.IsNull ? 0 : (uint)OBJ_TYPE.OBJ_BITMAP;
    }
}
