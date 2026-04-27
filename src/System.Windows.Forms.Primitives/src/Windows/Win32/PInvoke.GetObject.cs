// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="GetObject(HGDIOBJ,int,void*)"/>
    public static unsafe bool GetObject<T>(HGDIOBJ h, out T @object) where T : unmanaged
    {
        @object = default;
        fixed (void* pv = &@object)
        {
            return GetObject(h, sizeof(T), pv) != 0;
        }
    }
}
