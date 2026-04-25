// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL IsChild(HWND hWndParent, HWND hWnd)
        => PlatformApi.Window.IsChild(hWndParent, hWnd);

    /// <inheritdoc cref="IsChild(HWND, HWND)"/>
    public static BOOL IsChild<T>(HWND hWndParent, T hWnd) where T : IHandle<HWND>
    {
        BOOL result = IsChild(hWndParent, hWnd.Handle);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }

    /// <inheritdoc cref="IsChild(HWND, HWND)"/>
    public static BOOL IsChild<TParent, TChild>(TParent hWndParent, TChild hWnd)
        where TParent : IHandle<HWND> where TChild : IHandle<HWND>
    {
        BOOL result = IsChild(hWndParent.Handle, hWnd.Handle);
        GC.KeepAlive(hWndParent.Wrapper);
        GC.KeepAlive(hWnd.Wrapper);
        return result;
    }
}