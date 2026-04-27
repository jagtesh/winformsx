// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <summary>Returns the state of a virtual key via PAL.
    /// TODO: Wire to Silk.NET keyboard state in Iteration 2.</summary>
    public static short GetKeyState(int nVirtKey)
        => 0; // No key pressed — stub until input pipeline wired.
}
