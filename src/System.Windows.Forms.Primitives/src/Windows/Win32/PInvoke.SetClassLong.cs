// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static IntPtr SetClassLong(IntPtr hWnd, GET_CLASS_LONG_INDEX nIndex, IntPtr dwNewLong)
         => (IntPtr)PlatformApi.Window.SetClassLong((HWND)hWnd, nIndex, (nint)dwNewLong);
}
