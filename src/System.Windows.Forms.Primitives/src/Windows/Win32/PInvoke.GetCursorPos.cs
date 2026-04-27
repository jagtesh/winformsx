// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns cursor position via PAL.
    /// TODO: Wire to Silk.NET mouse position when input pipeline is connected.</summary>
    public static unsafe BOOL GetCursorPos(out global::System.Drawing.Point lpPoint)
    {
        lpPoint = default;
        return true;
    }
}
