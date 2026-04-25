// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe UIntPtr SetTimer(HWND hWnd, nuint nIDEvent, uint uElapse, void* lpTimerFunc)
        => (UIntPtr)(nuint)PlatformApi.System.SetTimer(hWnd, (nint)nIDEvent, uElapse, (nint)lpTimerFunc);

    public static unsafe UIntPtr SetTimer<T>(T hWnd, nuint nIDEvent, uint uElapse)
        where T : IHandle<HWND>
    {
        UIntPtr result = SetTimer(hWnd.Handle, nIDEvent, uElapse, lpTimerFunc: null);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}