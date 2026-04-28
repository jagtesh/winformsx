// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Stub — DrawEdge is used for 3D border rendering. Returns true (success).</summary>
    public static unsafe BOOL DrawEdge(HDC hdc, RECT* qrc, DRAWEDGE_FLAGS edge, DRAW_EDGE_FLAGS grfFlags)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.DrawEdge",
            "Native DrawEdge border rendering is ignored in WinFormsX; borders must be rendered through the drawing PAL.");
        return true;
    }

    /// <summary>Managed ref overload.</summary>
    public static BOOL DrawEdge(HDC hdc, ref RECT qrc, DRAWEDGE_FLAGS edge, DRAW_EDGE_FLAGS grfFlags)
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.DrawEdge",
            "Native DrawEdge border rendering is ignored in WinFormsX; borders must be rendered through the drawing PAL.");
        return true;
    }
}
