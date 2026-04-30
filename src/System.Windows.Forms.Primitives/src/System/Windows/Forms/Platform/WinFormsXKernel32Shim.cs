// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.System.ApplicationInstallationAndServicing;
using Windows.Win32.System.Diagnostics.Debug;
using Windows.Win32.System.Threading;

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
            LoadLibraryEx = &LoadLibraryEx,
            FreeLibrary = &FreeLibrary,
            GetProcAddress = &GetProcAddress,
            FindResource = &FindResource,
            FindResourceEx = &FindResourceEx,
            LoadResource = &LoadResource,
            LockResource = &LockResource,
            SizeofResource = &SizeofResource,
            FreeResource = &FreeResource,
            GetLastError = &GetLastError,
            SetLastError = &SetLastError,
            CloseHandle = &CloseHandle,
            DuplicateHandle = &DuplicateHandle,
            FormatMessage = &FormatMessage,
            GetExitCodeThread = &GetExitCodeThread,
            GetLocaleInfoEx = &GetLocaleInfoEx,
            GetStartupInfo = &GetStartupInfo,
            GetThreadLocale = &GetThreadLocale,
            GetTickCount = &GetTickCount,
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
            CreateActCtx = &CreateActCtx,
            ActivateActCtx = &ActivateActCtx,
            DeactivateActCtx = &DeactivateActCtx,
            GetCurrentActCtx = &GetCurrentActCtx,
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
    private static nint LoadLibraryEx(char* fileName, nint file, uint flags)
    {
        try
        {
            _ = file;
            return (nint)PlatformApi.System.LoadLibraryEx(fileName is null ? null : new string(fileName), flags);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int FreeLibrary(nint module)
    {
        try
        {
            return PlatformApi.System.FreeLibrary((HINSTANCE)module) ? 1 : 0;
        }
        catch
        {
            return module == 0 ? 0 : 1;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetProcAddress(nint module, byte* procName)
    {
        try
        {
            return PlatformApi.System.GetProcAddress((HMODULE)module, (PCSTR)procName);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint FindResource(nint module, char* name, char* type)
    {
        try
        {
            return (nint)PlatformApi.System.FindResource((HMODULE)module, (PCWSTR)name, (PCWSTR)type).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint FindResourceEx(nint module, char* type, char* name, ushort language)
    {
        try
        {
            return (nint)PlatformApi.System.FindResourceEx((HMODULE)module, (PCWSTR)type, (PCWSTR)name, language).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint LoadResource(nint module, nint resourceInfo)
    {
        try
        {
            return (nint)PlatformApi.System.LoadResource((HMODULE)module, (HRSRC)resourceInfo).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void* LockResource(nint resourceData)
    {
        try
        {
            return PlatformApi.System.LockResource((HGLOBAL)resourceData);
        }
        catch
        {
            return null;
        }
    }

    [UnmanagedCallersOnly]
    private static uint SizeofResource(nint module, nint resourceInfo)
    {
        try
        {
            return PlatformApi.System.SizeofResource((HMODULE)module, (HRSRC)resourceInfo);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int FreeResource(nint resourceData)
    {
        try
        {
            return PlatformApi.System.FreeResource((HGLOBAL)resourceData) ? 1 : 0;
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
    private static int CloseHandle(nint handle)
    {
        try
        {
            return PlatformApi.System.CloseHandle((HANDLE)handle) ? 1 : 0;
        }
        catch
        {
            return 1;
        }
    }

    [UnmanagedCallersOnly]
    private static int DuplicateHandle(
        nint sourceProcessHandle,
        nint sourceHandle,
        nint targetProcessHandle,
        nint* targetHandle,
        uint desiredAccess,
        int inheritHandle,
        uint options)
    {
        try
        {
            return PlatformApi.System.DuplicateHandle(
                (HANDLE)sourceProcessHandle,
                (HANDLE)sourceHandle,
                (HANDLE)targetProcessHandle,
                (HANDLE*)targetHandle,
                desiredAccess,
                (BOOL)inheritHandle,
                options) ? 1 : 0;
        }
        catch
        {
            if (targetHandle is not null)
            {
                *targetHandle = sourceHandle;
                return 1;
            }

            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint FormatMessage(uint flags, void* source, uint messageId, uint languageId, char* buffer, uint size, void* arguments)
    {
        try
        {
            return PlatformApi.System.FormatMessage((FORMAT_MESSAGE_OPTIONS)flags, source, messageId, languageId, buffer, size, arguments);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetExitCodeThread(nint thread, uint* exitCode)
    {
        try
        {
            return PlatformApi.System.GetExitCodeThread((HANDLE)thread, exitCode) ? 1 : 0;
        }
        catch
        {
            if (exitCode is not null)
            {
                *exitCode = 259;
                return 1;
            }

            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetLocaleInfoEx(char* localeName, uint lcType, char* data, int dataLength)
    {
        try
        {
            return PlatformApi.System.GetLocaleInfoEx(localeName is null ? string.Empty : new string(localeName), lcType, data, dataLength);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void GetStartupInfo(STARTUPINFOW* startupInfo)
    {
        if (startupInfo is null)
        {
            return;
        }

        try
        {
            PlatformApi.System.GetStartupInfo(out *startupInfo);
        }
        catch
        {
            *startupInfo = default;
            startupInfo->cb = (uint)sizeof(STARTUPINFOW);
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetThreadLocale()
    {
        try
        {
            return PlatformApi.System.GetThreadLocale();
        }
        catch
        {
            return 0x0409;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetTickCount()
    {
        try
        {
            return PlatformApi.System.GetTickCount();
        }
        catch
        {
            return (uint)Environment.TickCount64;
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

    [UnmanagedCallersOnly]
    private static nint CreateActCtx(ACTCTXW* actCtx)
    {
        try
        {
            return (nint)PlatformApi.System.CreateActCtx(actCtx);
        }
        catch
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly]
    private static int ActivateActCtx(nint actCtx, nuint* cookie)
    {
        try
        {
            return PlatformApi.System.ActivateActCtx((HANDLE)actCtx, cookie) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeactivateActCtx(uint flags, nuint cookie)
    {
        try
        {
            return PlatformApi.System.DeactivateActCtx(flags, cookie) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetCurrentActCtx(nint* actCtx)
    {
        try
        {
            return PlatformApi.System.GetCurrentActCtx((HANDLE*)actCtx) ? 1 : 0;
        }
        catch
        {
            return 0;
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
        public delegate* unmanaged<char*, nint, uint, nint> LoadLibraryEx;
        public delegate* unmanaged<nint, int> FreeLibrary;
        public delegate* unmanaged<nint, byte*, nint> GetProcAddress;
        public delegate* unmanaged<nint, char*, char*, nint> FindResource;
        public delegate* unmanaged<nint, char*, char*, ushort, nint> FindResourceEx;
        public delegate* unmanaged<nint, nint, nint> LoadResource;
        public delegate* unmanaged<nint, void*> LockResource;
        public delegate* unmanaged<nint, nint, uint> SizeofResource;
        public delegate* unmanaged<nint, int> FreeResource;
        public delegate* unmanaged<uint> GetLastError;
        public delegate* unmanaged<uint, void> SetLastError;
        public delegate* unmanaged<nint, int> CloseHandle;
        public delegate* unmanaged<nint, nint, nint, nint*, uint, int, uint, int> DuplicateHandle;
        public delegate* unmanaged<uint, void*, uint, uint, char*, uint, void*, uint> FormatMessage;
        public delegate* unmanaged<nint, uint*, int> GetExitCodeThread;
        public delegate* unmanaged<char*, uint, char*, int, int> GetLocaleInfoEx;
        public delegate* unmanaged<STARTUPINFOW*, void> GetStartupInfo;
        public delegate* unmanaged<uint> GetThreadLocale;
        public delegate* unmanaged<uint> GetTickCount;
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
        public delegate* unmanaged<ACTCTXW*, nint> CreateActCtx;
        public delegate* unmanaged<nint, nuint*, int> ActivateActCtx;
        public delegate* unmanaged<uint, nuint, int> DeactivateActCtx;
        public delegate* unmanaged<nint*, int> GetCurrentActCtx;
    }
}
