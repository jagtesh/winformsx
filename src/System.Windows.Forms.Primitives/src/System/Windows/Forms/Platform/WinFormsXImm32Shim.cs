// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.UI.Input.Ime;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXImm32Shim
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

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXImm32RegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            ImmAssociateContext = &ImmAssociateContext,
            ImmCreateContext = &ImmCreateContext,
            ImmGetContext = &ImmGetContext,
            ImmGetConversionStatus = &ImmGetConversionStatus,
            ImmGetOpenStatus = &ImmGetOpenStatus,
            ImmNotifyIME = &ImmNotifyIME,
            ImmReleaseContext = &ImmReleaseContext,
            ImmSetConversionStatus = &ImmSetConversionStatus,
            ImmSetOpenStatus = &ImmSetOpenStatus,
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

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXImm32Shim).Assembly.Location) ?? string.Empty;
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
            "IMM32.dll.dylib",
            "libIMM32.dll.dylib",
            "libIMM32.dll.so",
            "IMM32.dll.so",
            "IMM32.dll",
            "imm32.dll",
            "imm32",
            "libimm32.dylib",
            "libimm32.so"
        ];
    }

    [UnmanagedCallersOnly]
    private static nint ImmAssociateContext(nint hwnd, nint himc)
    {
        try
        {
            return (nint)PlatformApi.Input.ImmAssociateContext((HWND)hwnd, (HIMC)himc);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint ImmCreateContext()
    {
        try
        {
            return (nint)PlatformApi.Input.ImmCreateContext();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint ImmGetContext(nint hwnd)
    {
        try
        {
            return (nint)PlatformApi.Input.ImmGetContext((HWND)hwnd);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmGetConversionStatus(nint himc, uint* conversion, uint* sentence)
    {
        if (conversion is null || sentence is null)
        {
            return 0;
        }

        try
        {
            IME_CONVERSION_MODE managedConversion;
            IME_SENTENCE_MODE managedSentence;
            bool result = PlatformApi.Input.ImmGetConversionStatus(
                (HIMC)himc,
                &managedConversion,
                &managedSentence);
            *conversion = (uint)managedConversion;
            *sentence = (uint)managedSentence;
            return result ? 1 : 0;
        }
        catch
        {
            *conversion = 0;
            *sentence = 0;
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmGetOpenStatus(nint himc)
    {
        try
        {
            return PlatformApi.Input.ImmGetOpenStatus((HIMC)himc) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmNotifyIME(nint himc, uint action, uint index, uint value)
    {
        try
        {
            return PlatformApi.Input.ImmNotifyIME(
                (HIMC)himc,
                (NOTIFY_IME_ACTION)action,
                (NOTIFY_IME_INDEX)index,
                value)
                ? 1
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmReleaseContext(nint hwnd, nint himc)
    {
        try
        {
            return PlatformApi.Input.ImmReleaseContext((HWND)hwnd, (HIMC)himc) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmSetConversionStatus(nint himc, uint conversion, uint sentence)
    {
        try
        {
            return PlatformApi.Input.ImmSetConversionStatus(
                (HIMC)himc,
                (IME_CONVERSION_MODE)conversion,
                (IME_SENTENCE_MODE)sentence)
                ? 1
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ImmSetOpenStatus(nint himc, int open)
    {
        try
        {
            return PlatformApi.Input.ImmSetOpenStatus((HIMC)himc, open != 0) ? 1 : 0;
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
        public delegate* unmanaged<nint, nint, nint> ImmAssociateContext;
        public delegate* unmanaged<nint> ImmCreateContext;
        public delegate* unmanaged<nint, nint> ImmGetContext;
        public delegate* unmanaged<nint, uint*, uint*, int> ImmGetConversionStatus;
        public delegate* unmanaged<nint, int> ImmGetOpenStatus;
        public delegate* unmanaged<nint, uint, uint, uint, int> ImmNotifyIME;
        public delegate* unmanaged<nint, nint, int> ImmReleaseContext;
        public delegate* unmanaged<nint, uint, uint, int> ImmSetConversionStatus;
        public delegate* unmanaged<nint, int, int> ImmSetOpenStatus;
    }
}
