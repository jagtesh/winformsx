// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static unsafe partial class PInvoke
{
    private const int LetterPaperWidthTenthsOfMillimeter = 2159;
    private const int LetterPaperHeightTenthsOfMillimeter = 2794;

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
        _ = pDeviceName;
        _ = fMode;

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

        devmode->dmOrientation = 1;
        devmode->dmPaperSize = 1;
        devmode->dmPaperLength = LetterPaperHeightTenthsOfMillimeter;
        devmode->dmPaperWidth = LetterPaperWidthTenthsOfMillimeter;
        devmode->dmCopies = 1;
        devmode->dmDefaultSource = 7;
        devmode->dmPrintQuality = -4;
        devmode->dmYResolution = 300;
        devmode->dmColor = DEVMODE_COLOR.DMCOLOR_COLOR;
        devmode->dmDuplex = DEVMODE_DUPLEX.DMDUP_SIMPLEX;
        devmode->dmCollate = DEVMODE_COLLATE.DMCOLLATE_FALSE;
    }
}
