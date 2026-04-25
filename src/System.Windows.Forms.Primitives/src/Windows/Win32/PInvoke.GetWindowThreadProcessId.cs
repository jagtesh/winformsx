// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static uint GetWindowThreadProcessId(HWND hWnd, out uint lpdwProcessId)
    {
        lpdwProcessId = 0;
        return PlatformApi.System.GetWindowThreadProcessId(hWnd, out lpdwProcessId);
    }

    /// <inheritdoc cref="GetWindowThreadProcessId(HWND, out uint)"/>
    public static unsafe uint GetWindowThreadProcessId(HWND hWnd, uint* lpdwProcessId)
    {
        uint pid = 0;
        uint result = PlatformApi.System.GetWindowThreadProcessId(hWnd, out pid);
        if (lpdwProcessId is not null)
            *lpdwProcessId = pid;
        return result;
    }

    /// <inheritdoc cref="GetWindowThreadProcessId(HWND, out uint)"/>
    public static uint GetWindowThreadProcessId<T>(T hWnd, out uint lpdwProcessId) where T : IHandle<HWND>
    {
        uint result = GetWindowThreadProcessId(hWnd.Handle, out lpdwProcessId);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}