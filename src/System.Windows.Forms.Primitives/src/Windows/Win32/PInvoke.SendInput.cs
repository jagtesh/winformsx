// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe uint SendInput(uint cInputs, INPUT* pInputs, int cbSize)
    {
        if (pInputs is null)
        {
            return 0;
        }

        return PlatformApi.Input.SendInput(new ReadOnlySpan<INPUT>(pInputs, checked((int)cInputs)), cbSize);
    }

    public static uint SendInput(ReadOnlySpan<INPUT> pInputs, int cbSize)
        => PlatformApi.Input.SendInput(pInputs, cbSize);
}
