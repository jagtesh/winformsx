// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the window at a screen point.</summary>
    public static HWND WindowFromPoint(global::System.Drawing.Point Point)
        => PlatformApi.Window.WindowFromPoint(Point);
}
