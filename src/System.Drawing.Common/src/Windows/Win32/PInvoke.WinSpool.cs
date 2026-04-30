// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;

namespace Windows.Win32;

internal static unsafe partial class PInvoke
{
    private const string VirtualPrinterName = "WinFormsX Virtual Printer";
    private const short DmOrientPortrait = 1;
    private const short DmPaperLetter = 1;
    private const short DmBinAuto = 7;
    private const short DmResHigh = -4;
    private const int LetterPaperWidthTenthsOfMillimeter = 2159;
    private const int LetterPaperHeightTenthsOfMillimeter = 2794;

    internal static BOOL EnumPrinters(
        uint Flags,
        PCWSTR Name,
        uint Level,
        byte* pPrinterEnum,
        uint cbBuf,
        uint* pcbNeeded,
        uint* pcReturned)
    {
        _ = Flags;
        _ = Name;
        _ = Level;
        _ = pPrinterEnum;
        _ = cbBuf;

        if (pcbNeeded is not null)
        {
            *pcbNeeded = 0;
        }

        if (pcReturned is not null)
        {
            *pcReturned = 0;
        }

        Marshal.SetLastPInvokeError((int)WIN32_ERROR.ERROR_SUCCESS);
        return BOOL.TRUE;
    }

    internal static int DeviceCapabilities(
        char* pDevice,
        char* pPort,
        PRINTER_DEVICE_CAPABILITIES fwCapability,
        PWSTR pOutput,
        DEVMODEW* pDevMode)
    {
        _ = pPort;
        _ = pDevMode;

        if (!IsVirtualPrinter(pDevice))
        {
            return -1;
        }

        void* output = pOutput.Value;
        return fwCapability switch
        {
            PRINTER_DEVICE_CAPABILITIES.DC_COPIES => 1,
            PRINTER_DEVICE_CAPABILITIES.DC_COLORDEVICE => 1,
            PRINTER_DEVICE_CAPABILITIES.DC_DUPLEX => 0,
            PRINTER_DEVICE_CAPABILITIES.DC_ORIENTATION => 0,
            PRINTER_DEVICE_CAPABILITIES.DC_PAPERNAMES => WriteFixedString(output, 64, "Letter"),
            PRINTER_DEVICE_CAPABILITIES.DC_PAPERS => WriteUInt16(output, DmPaperLetter),
            PRINTER_DEVICE_CAPABILITIES.DC_PAPERSIZE => WriteSize(output, LetterPaperWidthTenthsOfMillimeter, LetterPaperHeightTenthsOfMillimeter),
            PRINTER_DEVICE_CAPABILITIES.DC_BINNAMES => WriteFixedString(output, 24, "Automatically Select"),
            PRINTER_DEVICE_CAPABILITIES.DC_BINS => WriteUInt16(output, DmBinAuto),
            _ => -1
        };
    }

    internal static int DocumentProperties(
        HWND hWnd,
        HANDLE hPrinter,
        char* pDeviceName,
        DEVMODEW* pDevModeOutput,
        DEVMODEW* pDevModeInput,
        uint fMode)
    {
        _ = hWnd;
        _ = hPrinter;
        _ = fMode;

        if (!IsVirtualPrinter(pDeviceName))
        {
            return -1;
        }

        if (pDevModeOutput is null)
        {
            return sizeof(DEVMODEW);
        }

        if (pDevModeInput is not null && pDevModeInput != pDevModeOutput)
        {
            *pDevModeOutput = *pDevModeInput;
        }
        else
        {
            *pDevModeOutput = default;
        }

        FillDefaultDevMode(pDevModeOutput);
        return 1;
    }

    private static void FillDefaultDevMode(DEVMODEW* devmode)
    {
        devmode->dmSize = (ushort)sizeof(DEVMODEW);
        devmode->dmFields =
            DEVMODE_FIELD_FLAGS.DM_ORIENTATION
            | DEVMODE_FIELD_FLAGS.DM_PAPERSIZE
            | DEVMODE_FIELD_FLAGS.DM_PAPERLENGTH
            | DEVMODE_FIELD_FLAGS.DM_PAPERWIDTH
            | DEVMODE_FIELD_FLAGS.DM_COPIES
            | DEVMODE_FIELD_FLAGS.DM_DEFAULTSOURCE
            | DEVMODE_FIELD_FLAGS.DM_PRINTQUALITY
            | DEVMODE_FIELD_FLAGS.DM_COLOR
            | DEVMODE_FIELD_FLAGS.DM_DUPLEX
            | DEVMODE_FIELD_FLAGS.DM_YRESOLUTION
            | DEVMODE_FIELD_FLAGS.DM_COLLATE;

        devmode->dmOrientation = DmOrientPortrait;
        devmode->dmPaperSize = DmPaperLetter;
        devmode->dmPaperLength = LetterPaperHeightTenthsOfMillimeter;
        devmode->dmPaperWidth = LetterPaperWidthTenthsOfMillimeter;
        devmode->dmCopies = 1;
        devmode->dmDefaultSource = DmBinAuto;
        devmode->dmPrintQuality = DmResHigh;
        devmode->dmYResolution = 300;
        devmode->dmColor = DEVMODE_COLOR.DMCOLOR_COLOR;
        devmode->dmDuplex = DEVMODE_DUPLEX.DMDUP_SIMPLEX;
        devmode->dmCollate = DEVMODE_COLLATE.DMCOLLATE_FALSE;
    }

    private static bool IsVirtualPrinter(char* printerName)
    {
        if (printerName is null || printerName[0] == '\0')
        {
            return true;
        }

        return VirtualPrinterName.AsSpan().Equals(new string(printerName), StringComparison.OrdinalIgnoreCase);
    }

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
            *(Size*)output = new Size(width, height);
        }

        return 1;
    }
}
