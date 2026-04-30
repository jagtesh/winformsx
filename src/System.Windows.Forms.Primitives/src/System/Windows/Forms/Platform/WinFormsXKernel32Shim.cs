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
    }
}
