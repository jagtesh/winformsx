// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXPrintSpoolerInterop
{
    private const int DcPapers = 2;
    private const int DcPaperSize = 3;
    private const int DcBins = 6;
    private const int DcDuplex = 7;
    private const int DcBinNames = 12;
    private const int DcPaperNames = 16;
    private const int DcOrientation = 17;
    private const int DcCopies = 18;
    private const int DcColorDevice = 32;
    private const short DmPaperLetter = 1;
    private const short DmBinAuto = 7;
    private const int LetterPaperWidthTenthsOfMillimeter = 2159;
    private const int LetterPaperHeightTenthsOfMillimeter = 2794;

    public static bool EnumPrinters(
        uint flags,
        char* name,
        uint level,
        byte* printerEnum,
        uint bufferSize,
        uint* needed,
        uint* returned)
    {
        _ = flags;
        _ = name;
        _ = level;
        _ = printerEnum;
        _ = bufferSize;

        if (needed is not null)
        {
            *needed = 0;
        }

        if (returned is not null)
        {
            *returned = 0;
        }

        Marshal.SetLastPInvokeError(0);
        return true;
    }

    public static int DeviceCapabilities(char* device, char* port, uint capability, void* output, DEVMODEW* devMode)
    {
        _ = device;
        _ = port;
        _ = devMode;

        return capability switch
        {
            DcCopies => 1,
            DcColorDevice => 1,
            DcDuplex => 0,
            DcOrientation => 0,
            DcPaperNames => WriteFixedString(output, 64, "Letter"),
            DcPapers => WriteUInt16(output, DmPaperLetter),
            DcPaperSize => WriteSize(output, LetterPaperWidthTenthsOfMillimeter, LetterPaperHeightTenthsOfMillimeter),
            DcBinNames => WriteFixedString(output, 24, "Automatically Select"),
            DcBins => WriteUInt16(output, DmBinAuto),
            _ => -1
        };
    }

    public static int DocumentProperties(
        HWND hwnd,
        HANDLE printer,
        char* deviceName,
        DEVMODEW* devModeOutput,
        DEVMODEW* devModeInput,
        uint mode)
        => global::Windows.Win32.PInvoke.DocumentProperties(hwnd, printer, deviceName, devModeOutput, devModeInput, mode);

    private static int WriteFixedString(void* output, int length, string value)
    {
        if (output is null)
        {
            return 1;
        }

        Span<char> destination = new(output, length);
        destination.Clear();
        value.AsSpan(0, Math.Min(value.Length, length - 1)).CopyTo(destination);
        return 1;
    }

    private static int WriteUInt16(void* output, int value)
    {
        if (output is not null)
        {
            *(ushort*)output = checked((ushort)value);
        }

        return 1;
    }

    private static int WriteSize(void* output, int width, int height)
    {
        if (output is not null)
        {
            int* values = (int*)output;
            values[0] = width;
            values[1] = height;
        }

        return 1;
    }
}
