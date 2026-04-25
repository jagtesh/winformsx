// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static new BOOL GetClientRect(HWND hWnd, out RECT lpRect)
        => PlatformApi.Window.GetClientRect(hWnd, out lpRect);

    public static new BOOL GetClientRect<T>(T hWnd, out RECT lpRect) where T : IHandle<HWND>
    { BOOL r = GetClientRect(hWnd.Handle, out lpRect); GC.KeepAlive(hWnd.Wrapper); return r; }
}
