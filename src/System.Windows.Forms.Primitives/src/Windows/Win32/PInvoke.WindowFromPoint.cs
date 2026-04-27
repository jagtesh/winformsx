// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the window at a screen point.
    /// TODO: Implement hit-testing against virtual window tree in Iteration 2.</summary>
    public static HWND WindowFromPoint(global::System.Drawing.Point Point)
        => HWND.Null;
}
