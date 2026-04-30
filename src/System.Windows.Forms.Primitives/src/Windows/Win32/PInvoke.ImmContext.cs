// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;
using Windows.Win32.UI.Input.Ime;

namespace Windows.Win32;

internal static unsafe partial class PInvoke
{
    public static HIMC ImmAssociateContext(HWND hWnd, HIMC hIMC)
    {
        return PlatformApi.Input.ImmAssociateContext(hWnd, hIMC);
    }

    public static HIMC ImmCreateContext()
    {
        return PlatformApi.Input.ImmCreateContext();
    }

    public static BOOL ImmGetConversionStatus(
        HIMC hIMC,
        IME_CONVERSION_MODE* lpfdwConversion,
        IME_SENTENCE_MODE* lpfdwSentence)
    {
        return PlatformApi.Input.ImmGetConversionStatus(hIMC, lpfdwConversion, lpfdwSentence);
    }

    public static BOOL ImmGetOpenStatus(HIMC hIMC)
    {
        return PlatformApi.Input.ImmGetOpenStatus(hIMC);
    }

    public static BOOL ImmNotifyIME(
        HIMC hIMC,
        NOTIFY_IME_ACTION dwAction,
        NOTIFY_IME_INDEX dwIndex,
        uint dwValue)
    {
        return PlatformApi.Input.ImmNotifyIME(hIMC, dwAction, dwIndex, dwValue);
    }

    public static BOOL ImmSetConversionStatus(
        HIMC hIMC,
        IME_CONVERSION_MODE fdwConversion,
        IME_SENTENCE_MODE fdwSentence)
    {
        return PlatformApi.Input.ImmSetConversionStatus(hIMC, fdwConversion, fdwSentence);
    }

    public static BOOL ImmSetOpenStatus(HIMC hIMC, BOOL fOpen)
    {
        return PlatformApi.Input.ImmSetOpenStatus(hIMC, fOpen);
    }
}
