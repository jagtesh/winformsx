// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System;

internal static class SafeArithmetic
{
    public static int CheckedIncrement(int value, string context)
    {
        try
        {
            return checked(value + 1);
        }
        catch (OverflowException ex)
        {
            throw new OverflowException($"{context} overflowed while incrementing {value}.", ex);
        }
    }

    public static int CheckedDecrement(int value, string context)
    {
        try
        {
            return checked(value - 1);
        }
        catch (OverflowException ex)
        {
            throw new OverflowException($"{context} underflowed while decrementing {value}.", ex);
        }
    }

    public static long CheckedIncrement(long value, string context)
    {
        try
        {
            return checked(value + 1L);
        }
        catch (OverflowException ex)
        {
            throw new OverflowException($"{context} overflowed while incrementing {value}.", ex);
        }
    }

    public static long CheckedDecrement(long value, string context)
    {
        try
        {
            return checked(value - 1L);
        }
        catch (OverflowException ex)
        {
            throw new OverflowException($"{context} underflowed while decrementing {value}.", ex);
        }
    }

    public static int SaturatingAdd(int left, int right)
    {
        long value = (long)left + right;
        return value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
    }

    public static int SaturatingSubtract(int left, int right)
    {
        long value = (long)left - right;
        return value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
    }

    public static int SaturatingClamp(int value, int minimum, int maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, "Minimum cannot be greater than maximum.");
        }

        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    public static int WrapInt32(uint value) => unchecked((int)value);

    public static int PackSignedLowHigh16(int low, int high)
        => unchecked((high << 16) | (low & 0xFFFF));
}
