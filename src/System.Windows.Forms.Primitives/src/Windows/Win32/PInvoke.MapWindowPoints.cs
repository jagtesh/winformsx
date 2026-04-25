// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    // -- RECT variants ---------------------------------------------------
    public static int MapWindowPoints(HWND hWndFrom, HWND hWndTo, ref RECT lpRect)
        => PlatformApi.Window.MapWindowPoints(hWndFrom, hWndTo, ref lpRect);

    public static int MapWindowPoints<TFrom>(TFrom hWndFrom, HWND hWndTo, ref RECT lpRect) where TFrom : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom.Handle, hWndTo, ref lpRect); GC.KeepAlive(hWndFrom.Wrapper); return r; }

    public static int MapWindowPoints<TTo>(HWND hWndFrom, TTo hWndTo, ref RECT lpRect) where TTo : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom, hWndTo.Handle, ref lpRect); GC.KeepAlive(hWndTo.Wrapper); return r; }

    public static int MapWindowPoints<TFrom, TTo>(TFrom hWndFrom, TTo hWndTo, ref RECT lpRect)
        where TFrom : IHandle<HWND> where TTo : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom.Handle, hWndTo.Handle, ref lpRect); GC.KeepAlive(hWndFrom.Wrapper); GC.KeepAlive(hWndTo.Wrapper); return r; }

    // -- Point variants (3-arg convenience — maps 1 point) ------------
    public static int MapWindowPoints(HWND hWndFrom, HWND hWndTo, ref global::System.Drawing.Point lpPoint)
    {
        var r = new RECT { left = lpPoint.X, top = lpPoint.Y, right = lpPoint.X, bottom = lpPoint.Y };
        int result = MapWindowPoints(hWndFrom, hWndTo, ref r);
        lpPoint = new global::System.Drawing.Point(r.left, r.top);
        return result;
    }

    public static int MapWindowPoints<TFrom>(TFrom hWndFrom, HWND hWndTo, ref global::System.Drawing.Point lpPoint) where TFrom : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom.Handle, hWndTo, ref lpPoint); GC.KeepAlive(hWndFrom.Wrapper); return r; }

    public static int MapWindowPoints<TTo>(HWND hWndFrom, TTo hWndTo, ref global::System.Drawing.Point lpPoint) where TTo : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom, hWndTo.Handle, ref lpPoint); GC.KeepAlive(hWndTo.Wrapper); return r; }

    public static int MapWindowPoints<TFrom, TTo>(TFrom hWndFrom, TTo hWndTo, ref global::System.Drawing.Point lpPoint)
        where TFrom : IHandle<HWND> where TTo : IHandle<HWND>
    { int r = MapWindowPoints(hWndFrom.Handle, hWndTo.Handle, ref lpPoint); GC.KeepAlive(hWndFrom.Wrapper); GC.KeepAlive(hWndTo.Wrapper); return r; }
}