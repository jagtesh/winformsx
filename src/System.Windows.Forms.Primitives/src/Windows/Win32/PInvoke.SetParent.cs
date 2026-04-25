// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static HWND SetParent(HWND hWndChild, HWND hWndNewParent)
        => PlatformApi.Window.SetParent(hWndChild, hWndNewParent);

    /// <inheritdoc cref="SetParent(HWND, HWND)"/>
    public static HWND SetParent<T>(T hWndChild, HWND hWndNewParent) where T : IHandle<HWND>
    {
        HWND result = SetParent(hWndChild.Handle, hWndNewParent);
        GC.KeepAlive(hWndChild.Wrapper);
        return result;
    }

    /// <inheritdoc cref="SetParent(HWND, HWND)"/>
    public static HWND SetParent<T>(HWND hWndChild, T hWndNewParent) where T : IHandle<HWND>
    {
        HWND result = SetParent(hWndChild, hWndNewParent.Handle);
        GC.KeepAlive(hWndNewParent.Wrapper);
        return result;
    }

    /// <inheritdoc cref="SetParent(HWND, HWND)"/>
    public static HWND SetParent<T>(T hWndChild, IHandle<HWND> hWndNewParent) where T : IHandle<HWND>
    {
        HWND result = SetParent(hWndChild.Handle, hWndNewParent.Handle);
        GC.KeepAlive(hWndChild.Wrapper);
        GC.KeepAlive(hWndNewParent.Wrapper);
        return result;
    }

    /// <inheritdoc cref="SetParent(HWND, HWND)"/>
    public static HWND SetParent<TChild, TParent>(TChild hWndChild, TParent hWndNewParent)
        where TChild : IHandle<HWND> where TParent : IHandle<HWND>
    {
        HWND result = SetParent(hWndChild.Handle, hWndNewParent.Handle);
        GC.KeepAlive(hWndChild.Wrapper);
        GC.KeepAlive(hWndNewParent.Wrapper);
        return result;
    }
}