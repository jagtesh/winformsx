// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Input.Ime;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL ImmReleaseContext(HWND hWnd, HIMC hIMC)
    {
        return PlatformApi.Input.ImmReleaseContext(hWnd, hIMC);
    }

    /// <inheritdoc cref="ImmReleaseContext(HWND, HIMC)"/>
    public static BOOL ImmReleaseContext<T>(T hWnd, HIMC hIMC) where T : IHandle<HWND>
    {
        BOOL result = ImmReleaseContext(hWnd.Handle, hIMC);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}
