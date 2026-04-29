// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Windows.Forms.Platform;
using System.Collections.Concurrent;

namespace Windows.Win32;

internal static partial class PInvoke
{
    private static readonly ConcurrentDictionary<nint, byte> s_loadedLibraries = new();

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
            s_loadedLibraries.TryAdd(handle, 0);
            return (HINSTANCE)handle;
        }

        return HINSTANCE.Null;
    }

    internal static bool TryUntrackLoadedLibrary(HINSTANCE module)
        => s_loadedLibraries.TryRemove((nint)module, out _);
}
