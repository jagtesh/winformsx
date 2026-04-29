// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    // Matches Win32 pseudo-handle semantics for current thread.
    public static HANDLE GetCurrentThread() => (HANDLE)(nint)(-2);
}
