// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXWinSpoolShim
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

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXWinSpoolRegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            EnumPrinters = &EnumPrinters,
            DeviceCapabilities = &DeviceCapabilities,
            DocumentProperties = &DocumentProperties,
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

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXWinSpoolShim).Assembly.Location) ?? string.Empty;
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
            "winspool.drv.dylib",
            "libwinspool.drv.dylib",
            "libwinspool.drv.so",
            "winspool.drv.so",
            "winspool.drv",
            "winspool",
            "libwinspool.dylib",
            "libwinspool.so"
        ];
    }

    [UnmanagedCallersOnly]
    private static int EnumPrinters(uint flags, char* name, uint level, byte* printerEnum, uint bufferSize, uint* needed, uint* returned)
    {
        try
        {
            return WinFormsXPrintSpoolerInterop.EnumPrinters(flags, name, level, printerEnum, bufferSize, needed, returned) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeviceCapabilities(char* device, char* port, uint capability, void* output, DEVMODEW* devMode)
    {
        try
        {
            return WinFormsXPrintSpoolerInterop.DeviceCapabilities(device, port, capability, output, devMode);
        }
        catch
        {
            return -1;
        }
    }

    [UnmanagedCallersOnly]
    private static int DocumentProperties(HWND hwnd, HANDLE printer, char* deviceName, DEVMODEW* devModeOutput, DEVMODEW* devModeInput, uint mode)
    {
        try
        {
            return WinFormsXPrintSpoolerInterop.DocumentProperties(hwnd, printer, deviceName, devModeOutput, devModeInput, mode);
        }
        catch
        {
            return -1;
        }
    }

    private struct DispatchTable
    {
        public uint Version;
        public uint Size;
        public delegate* unmanaged<uint, char*, uint, byte*, uint, uint*, uint*, int> EnumPrinters;
        public delegate* unmanaged<char*, char*, uint, void*, DEVMODEW*, int> DeviceCapabilities;
        public delegate* unmanaged<HWND, HANDLE, char*, DEVMODEW*, DEVMODEW*, uint, int> DocumentProperties;
    }
}
