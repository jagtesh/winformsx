// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the HWND that has mouse capture.
    /// TODO: Wire to managed capture state in Iteration 2.</summary>
    public static HWND GetCapture()
        => HWND.Null; // No capture — stub until input pipeline wired.

    /// <summary>Releases mouse capture.
    /// TODO: Wire to managed capture state in Iteration 2.</summary>
    public static new BOOL ReleaseCapture()
        => true; // Always succeeds — stub until input pipeline wired.
}
