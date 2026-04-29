// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static HINSTANCE LoadLibraryEx(string lpLibFileName, int dwFlags)
    {
        _ = dwFlags;

        if (string.IsNullOrWhiteSpace(lpLibFileName))
        {
            return HINSTANCE.Null;
        }

        HMODULE module = PlatformApi.System.GetModuleHandle(lpLibFileName);
        if (!module.IsNull)
        {
            return (HINSTANCE)(nint)module;
        }

        if (NativeLibrary.TryLoad(lpLibFileName, out nint handle))
        {
            return (HINSTANCE)handle;
        }

        return HINSTANCE.Null;
    }
}
