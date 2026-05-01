// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.System.Memory;

namespace Windows.Win32;

internal static unsafe partial class PInvokeCore
{
    private static readonly object s_globalMemoryLock = new();
    private static readonly Dictionary<nint, nuint> s_globalMemorySizes = [];

    internal static HGLOBAL WinFormsXGlobalAlloc(GLOBAL_ALLOC_FLAGS uFlags, nuint dwBytes)
    {
        if (dwBytes == 0)
        {
            return HGLOBAL.Null;
        }

        nint handle = Marshal.AllocHGlobal(checked((nint)dwBytes));
        if (handle == 0)
        {
            return HGLOBAL.Null;
        }

        if (uFlags.HasFlag(GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT))
        {
            new Span<byte>((void*)handle, checked((int)dwBytes)).Clear();
        }

        lock (s_globalMemoryLock)
        {
            s_globalMemorySizes[handle] = dwBytes;
        }

        return (HGLOBAL)handle;
    }

    internal static HGLOBAL WinFormsXGlobalFree(HGLOBAL hMem)
    {
        nint handle = (nint)hMem.Value;
        if (handle == 0)
        {
            return HGLOBAL.Null;
        }

        lock (s_globalMemoryLock)
        {
            s_globalMemorySizes.Remove(handle);
        }

        Marshal.FreeHGlobal(handle);
        return HGLOBAL.Null;
    }

    internal static void* WinFormsXGlobalLock(HGLOBAL hMem) => hMem.Value;

    internal static HGLOBAL WinFormsXGlobalReAlloc(HGLOBAL hMem, nuint dwBytes, GLOBAL_ALLOC_FLAGS uFlags)
    {
        if (hMem.IsNull)
        {
            return WinFormsXGlobalAlloc(uFlags, dwBytes);
        }

        if (dwBytes == 0)
        {
            WinFormsXGlobalFree(hMem);
            return HGLOBAL.Null;
        }

        nint oldHandle = (nint)hMem.Value;
        nuint oldSize;
        lock (s_globalMemoryLock)
        {
            s_globalMemorySizes.TryGetValue(oldHandle, out oldSize);
        }

        nint newHandle = Marshal.ReAllocHGlobal(oldHandle, checked((nint)dwBytes));
        if (newHandle == 0)
        {
            return HGLOBAL.Null;
        }

        if (uFlags.HasFlag(GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT) && dwBytes > oldSize)
        {
            byte* start = (byte*)newHandle + checked((nint)oldSize);
            new Span<byte>(start, checked((int)(dwBytes - oldSize))).Clear();
        }

        lock (s_globalMemoryLock)
        {
            s_globalMemorySizes.Remove(oldHandle);
            s_globalMemorySizes[newHandle] = dwBytes;
        }

        return (HGLOBAL)newHandle;
    }

    internal static nuint WinFormsXGlobalSize(HGLOBAL hMem)
    {
        lock (s_globalMemoryLock)
        {
            return s_globalMemorySizes.TryGetValue((nint)hMem.Value, out nuint size) ? size : 0;
        }
    }

    internal static bool WinFormsXGlobalUnlock(HGLOBAL hMem)
    {
        _ = hMem;
        return false;
    }
}
