// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    private const int StockObjectHandleBase = 0x51000000;

    public static HGDIOBJ GetStockObject(GET_STOCK_OBJECT_FLAGS i)
        => IsKnownStockObject(i) ? (HGDIOBJ)(nint)(StockObjectHandleBase + (int)i) : default;

    private static bool IsKnownStockObject(GET_STOCK_OBJECT_FLAGS i)
        => i is >= GET_STOCK_OBJECT_FLAGS.WHITE_BRUSH and <= GET_STOCK_OBJECT_FLAGS.NULL_PEN
            or >= GET_STOCK_OBJECT_FLAGS.OEM_FIXED_FONT and <= GET_STOCK_OBJECT_FLAGS.DC_PEN;

    private static bool TryGetStockObject(HGDIOBJ h, out GET_STOCK_OBJECT_FLAGS i)
    {
        nint value = h.Value;
        if (value < StockObjectHandleBase)
        {
            i = default;
            return false;
        }

        i = (GET_STOCK_OBJECT_FLAGS)(value - StockObjectHandleBase);
        return IsKnownStockObject(i);
    }
}
