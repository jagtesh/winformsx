// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXCommonDialogInterop
{
    [ThreadStatic]
    private static COMMON_DLG_ERRORS s_extendedError;

    public static bool GetOpenFileName(OPENFILENAME* ofn)
    {
        _ = ofn;
        return Cancel();
    }

    public static bool GetSaveFileName(OPENFILENAME* ofn)
    {
        _ = ofn;
        return Cancel();
    }

    public static bool ChooseColor(CHOOSECOLORW* chooseColor)
    {
        _ = chooseColor;
        return Cancel();
    }

    public static bool ChooseFont(CHOOSEFONTW* chooseFont)
    {
        _ = chooseFont;
        return Cancel();
    }

    public static bool PrintDlg(void* printDlg)
    {
        _ = printDlg;
        return Cancel();
    }

    public static HRESULT PrintDlgEx(PRINTDLGEXW* printDlgEx)
    {
        if (printDlgEx is not null)
        {
            printDlgEx->dwResultAction = 0;
        }

        s_extendedError = COMMON_DLG_ERRORS.CDERR_GENERALCODES;
        return HRESULT.S_OK;
    }

    public static bool PageSetupDlg(PAGESETUPDLGW* pageSetup)
    {
        _ = pageSetup;
        return Cancel();
    }

    public static COMMON_DLG_ERRORS CommDlgExtendedError() => s_extendedError;

    private static bool Cancel()
    {
        s_extendedError = COMMON_DLG_ERRORS.CDERR_GENERALCODES;
        return false;
    }
}
