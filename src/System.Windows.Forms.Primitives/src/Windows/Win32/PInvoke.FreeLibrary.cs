// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static BOOL FreeLibrary(HINSTANCE hLibModule)
    {
        if (hLibModule.IsNull)
        {
            return BOOL.FALSE;
        }

        // Only free handles that were loaded by our managed fallback path.
        if (!TryUntrackLoadedLibrary(hLibModule))
        {
            return BOOL.TRUE;
        }

        NativeLibrary.Free((nint)hLibModule);
        return BOOL.TRUE;
    }
}
