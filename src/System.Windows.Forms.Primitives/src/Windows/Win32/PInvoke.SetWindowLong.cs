// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static nint SetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
        => PlatformApi.Window.SetWindowLong(hWnd, nIndex, dwNewLong);

    public static nint SetWindowLong<T>(T hWnd, WINDOW_LONG_PTR_INDEX nIndex, nint dwNewLong)
        where T : IHandle<HWND>
    { nint r = SetWindowLong(hWnd.Handle, nIndex, dwNewLong); GC.KeepAlive(hWnd.Wrapper); return r; }

    public static nint SetWindowLong<THwnd, TValue>(THwnd hWnd, WINDOW_LONG_PTR_INDEX nIndex, TValue dwNewLong)
        where THwnd : IHandle<HWND> where TValue : IHandle<HWND>
    { nint r = SetWindowLong(hWnd.Handle, nIndex, dwNewLong.Handle); GC.KeepAlive(hWnd.Wrapper); GC.KeepAlive(dwNewLong.Wrapper); return r; }

    public static nint SetWindowLong<T>(T hWnd, WINDOW_LONG_PTR_INDEX nIndex, WNDPROC newProc)
        where T : IHandle<HWND>
    {
        nint r = SetWindowLong(hWnd.Handle, nIndex, (nint)global::System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(newProc));
        GC.KeepAlive(hWnd.Wrapper);
        return r;
    }

    public static unsafe nint SetWindowLong<T>(T hWnd, WINDOW_LONG_PTR_INDEX nIndex, void* newProc)
        where T : IHandle<HWND>
    { nint r = SetWindowLong(hWnd.Handle, nIndex, (nint)newProc); GC.KeepAlive(hWnd.Wrapper); return r; }

    public static nint SetWindowLong(HWND hWnd, WINDOW_LONG_PTR_INDEX nIndex, WNDPROC newProc)
        => SetWindowLong(hWnd, nIndex, (nint)global::System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(newProc));
}