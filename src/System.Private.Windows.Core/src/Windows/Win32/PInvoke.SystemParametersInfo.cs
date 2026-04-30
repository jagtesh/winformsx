// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Accessibility;

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe bool SystemParametersInfo<T>(SYSTEM_PARAMETERS_INFO_ACTION uiAction, ref T value)
        where T : unmanaged
    {
        fixed (void* p = &value)
        {
            return SystemParametersInfoManaged(uiAction, 0, p);
        }
    }

    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe int SystemParametersInfoInt(SYSTEM_PARAMETERS_INFO_ACTION uiAction)
    {
        int value = 0;
        SystemParametersInfo(uiAction, ref value);
        return value;
    }

    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe bool SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION uiAction, ref bool value, uint fWinIni = 0)
    {
        BOOL nativeBool = value;
        bool result = SystemParametersInfoManaged(uiAction, 0, &nativeBool);
        value = nativeBool;
        return result;
    }

    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe bool SystemParametersInfoBool(SYSTEM_PARAMETERS_INFO_ACTION uiAction)
    {
        bool value = false;
        SystemParametersInfo(uiAction, ref value);
        return value;
    }

    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe bool SystemParametersInfo(ref HIGHCONTRASTW highContrast)
    {
        fixed (void* p = &highContrast)
        {
            // Note that the documentation for HIGHCONTRASTW says that the lpszDefaultScheme member needs to be
            // freed, but this is incorrect. No internal users ever free the pointer and the pointer never changes.
            highContrast.cbSize = (uint)sizeof(HIGHCONTRASTW);
            return SystemParametersInfoManaged(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST,
                highContrast.cbSize,
                p);
        }
    }

    /// <inheritdoc cref="SystemParametersInfo(SYSTEM_PARAMETERS_INFO_ACTION, uint, void*, SYSTEM_PARAMETERS_INFO_UPDATE_FLAGS)"/>
    public static unsafe bool SystemParametersInfo(ref NONCLIENTMETRICSW metrics)
    {
        fixed (void* p = &metrics)
        {
            metrics.cbSize = (uint)sizeof(NONCLIENTMETRICSW);
            return SystemParametersInfoManaged(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETNONCLIENTMETRICS,
                metrics.cbSize,
                p);
        }
    }

    /// <summary>
    ///  Tries to get system parameter info for the dpi. dpi is ignored if "SystemParametersInfoForDpi()" API
    ///  is not available on the OS that this application is running.
    /// </summary>
    public static unsafe bool TrySystemParametersInfoForDpi(ref NONCLIENTMETRICSW metrics, uint dpi)
    {
        fixed (void* p = &metrics)
        {
            metrics.cbSize = (uint)sizeof(NONCLIENTMETRICSW);
            return SystemParametersInfoManaged(
                SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETNONCLIENTMETRICS,
                metrics.cbSize,
                p);
        }
    }

    private static unsafe bool SystemParametersInfoManaged(
        SYSTEM_PARAMETERS_INFO_ACTION uiAction,
        uint uiParam,
        void* pvParam)
    {
        if (pvParam is null)
        {
            return true;
        }

        switch (uiAction)
        {
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETCLIENTAREAANIMATION:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETDRAGFULLWINDOWS:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETFONTSMOOTHING:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETICONTITLEWRAP:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDCUES:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETKEYBOARDPREF:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETLISTBOXSMOOTHSCROLLING:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETTOOLTIPANIMATION:
            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETUIEFFECTS:
                *(BOOL*)pvParam = true;
                return true;

            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETCLIENTAREAANIMATION:
                return true;

            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETWORKAREA:
                *(RECT*)pvParam = new RECT(0, 0, 1920, 1040);
                return true;

            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETNONCLIENTMETRICS:
                ((NONCLIENTMETRICSW*)pvParam)->cbSize = uiParam == 0
                    ? (uint)sizeof(NONCLIENTMETRICSW)
                    : uiParam;
                return true;

            case SYSTEM_PARAMETERS_INFO_ACTION.SPI_GETHIGHCONTRAST:
                ((HIGHCONTRASTW*)pvParam)->cbSize = uiParam == 0
                    ? (uint)sizeof(HIGHCONTRASTW)
                    : uiParam;
                ((HIGHCONTRASTW*)pvParam)->dwFlags = 0;
                return true;

            default:
                return true;
        }
    }
}
