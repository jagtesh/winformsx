// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the HWND that has mouse capture.
    /// TODO: Wire to managed capture state in Iteration 2.</summary>
    public static HWND GetCapture()
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.GetCapture",
            "Mouse capture state is not fully wired in WinFormsX yet; GetCapture returned a null handle.");
        return HWND.Null;
    }

    /// <summary>Releases mouse capture.
    /// TODO: Wire to managed capture state in Iteration 2.</summary>
    public static new BOOL ReleaseCapture()
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.ReleaseCapture",
            "Mouse capture release is not fully wired in WinFormsX yet; ReleaseCapture acknowledged without native work.");
        return true;
    }
}
