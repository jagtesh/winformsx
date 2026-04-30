// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXKernel32Shim
{
    private const uint DispatchVersion = 1;

    private static nint s_libraryHandle;
    private static bool s_registered;

    public static void Register()
    {
        if (s_registered)
        {
            return;
        }

        string? libraryPath = FindLibraryPath();
        if (libraryPath is null || !NativeLibrary.TryLoad(libraryPath, out s_libraryHandle) || s_libraryHandle == nint.Zero)
        {
            return;
        }

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXKernel32RegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            GetCurrentProcess = &GetCurrentProcess,
            GetCurrentProcessId = &GetCurrentProcessId,
            GetCurrentThreadId = &GetCurrentThreadId,
            GetModuleHandle = &GetModuleHandle,
            GetModuleFileName = &GetModuleFileName,
            GetLastError = &GetLastError,
            SetLastError = &SetLastError,
            GlobalAlloc = &GlobalAlloc,
            GlobalReAlloc = &GlobalReAlloc,
            GlobalLock = &GlobalLock,
            GlobalUnlock = &GlobalUnlock,
            GlobalSize = &GlobalSize,
            GlobalFree = &GlobalFree,
            LocalAlloc = &LocalAlloc,
            LocalReAlloc = &LocalReAlloc,
            LocalLock = &LocalLock,
            LocalUnlock = &LocalUnlock,
            LocalSize = &LocalSize,
            LocalFree = &LocalFree,
        };

        delegate* unmanaged<DispatchTable*, int> register = (delegate* unmanaged<DispatchTable*, int>)registerExport;
        s_registered = register(&dispatch) != 0;
    }

    private static string? FindLibraryPath()
    {
        foreach (string name in GetLibraryNames())
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            if (File.Exists(path))
            {
                return path;
            }

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXKernel32Shim).Assembly.Location) ?? string.Empty;
            path = Path.Combine(assemblyDirectory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string[] GetLibraryNames()
    {
        return
        [
            "KERNEL32.dll.dylib",
            "libKERNEL32.dll.dylib",
            "libKERNEL32.dll.so",
            "KERNEL32.dll.so",
            "KERNEL32.dll",
            "kernel32.dll",
            "kernel32",
            "libkernel32.dylib",
            "libkernel32.so"
        ];
    }

    [UnmanagedCallersOnly]
    private static nint GetCurrentProcess() => -1;

    [UnmanagedCallersOnly]
    private static uint GetCurrentProcessId()
    {
        try
        {
            return PlatformApi.System.GetCurrentProcessId();
        }
        catch
        {
            return (uint)Environment.ProcessId;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetCurrentThreadId()
    {
        try
        {
            return PlatformApi.System.GetCurrentThreadId();
        }
        catch
        {
            return (uint)Environment.CurrentManagedThreadId;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetModuleHandle(char* moduleName)
    {
        try
        {
            return (nint)PlatformApi.System.GetModuleHandle(moduleName is null ? null : new string(moduleName));
        }
        catch
        {
            return 0x400000;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetModuleFileName(nint module, char* filename, uint size)
    {
        try
        {
            if (filename is null || size == 0)
            {
                return 0;
            }

            int length = size > int.MaxValue ? int.MaxValue : (int)size;
            return PlatformApi.System.GetModuleFileName((HMODULE)module, new Span<char>(filename, length));
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetLastError()
    {
        try
        {
            return PlatformApi.System.GetLastError();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void SetLastError(uint error)
    {
        try
        {
            PlatformApi.System.SetLastError(error);
        }
        catch
        {
        }
    }

    [UnmanagedCallersOnly]
    private static nint GlobalAlloc(uint flags, nuint bytes)
    {
        try
        {
            return (nint)PlatformApi.System.GlobalAlloc(flags, bytes).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GlobalReAlloc(nint handle, nuint bytes, uint flags)
    {
        try
        {
            return (nint)PlatformApi.System.GlobalReAlloc((HGLOBAL)handle, bytes, flags).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void* GlobalLock(nint handle)
    {
        try
        {
            return PlatformApi.System.GlobalLock((HGLOBAL)handle);
        }
        catch
        {
            return null;
        }
    }

    [UnmanagedCallersOnly]
    private static int GlobalUnlock(nint handle)
    {
        try
        {
            return PlatformApi.System.GlobalUnlock((HGLOBAL)handle) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nuint GlobalSize(nint handle)
    {
        try
        {
            return PlatformApi.System.GlobalSize((HGLOBAL)handle);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GlobalFree(nint handle)
    {
        try
        {
            return (nint)PlatformApi.System.GlobalFree((HGLOBAL)handle).Value;
        }
        catch
        {
            return handle;
        }
    }

    [UnmanagedCallersOnly]
    private static nint LocalAlloc(uint flags, nuint bytes)
    {
        try
        {
            return PlatformApi.System.LocalAlloc(flags, bytes);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint LocalReAlloc(nint handle, nuint bytes, uint flags)
    {
        try
        {
            return PlatformApi.System.LocalReAlloc(handle, bytes, flags);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void* LocalLock(nint handle)
    {
        try
        {
            return PlatformApi.System.LocalLock(handle);
        }
        catch
        {
            return null;
        }
    }

    [UnmanagedCallersOnly]
    private static int LocalUnlock(nint handle)
    {
        try
        {
            return PlatformApi.System.LocalUnlock(handle) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nuint LocalSize(nint handle)
    {
        try
        {
            return PlatformApi.System.LocalSize(handle);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint LocalFree(nint handle)
    {
        try
        {
            return PlatformApi.System.LocalFree(handle);
        }
        catch
        {
            return handle;
        }
    }

    private struct DispatchTable
    {
        public uint Version;
        public uint Size;
        public delegate* unmanaged<nint> GetCurrentProcess;
        public delegate* unmanaged<uint> GetCurrentProcessId;
        public delegate* unmanaged<uint> GetCurrentThreadId;
        public delegate* unmanaged<char*, nint> GetModuleHandle;
        public delegate* unmanaged<nint, char*, uint, uint> GetModuleFileName;
        public delegate* unmanaged<uint> GetLastError;
        public delegate* unmanaged<uint, void> SetLastError;
        public delegate* unmanaged<uint, nuint, nint> GlobalAlloc;
        public delegate* unmanaged<nint, nuint, uint, nint> GlobalReAlloc;
        public delegate* unmanaged<nint, void*> GlobalLock;
        public delegate* unmanaged<nint, int> GlobalUnlock;
        public delegate* unmanaged<nint, nuint> GlobalSize;
        public delegate* unmanaged<nint, nint> GlobalFree;
        public delegate* unmanaged<uint, nuint, nint> LocalAlloc;
        public delegate* unmanaged<nint, nuint, uint, nint> LocalReAlloc;
        public delegate* unmanaged<nint, void*> LocalLock;
        public delegate* unmanaged<nint, int> LocalUnlock;
        public delegate* unmanaged<nint, nuint> LocalSize;
        public delegate* unmanaged<nint, nint> LocalFree;
    }
}
