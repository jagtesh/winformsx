// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe BOOL GetKeyboardState(byte* lpKeyState)
    {
        if (lpKeyState is null)
        {
            return false;
        }

        for (int i = 0; i < 256; i++)
        {
            lpKeyState[i] = 0;
        }

        return true;
    }
}
