// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    // Matches Win32 pseudo-handle semantics for current process.
    public static HANDLE GetCurrentProcess() => (HANDLE)(nint)(-1);
}
