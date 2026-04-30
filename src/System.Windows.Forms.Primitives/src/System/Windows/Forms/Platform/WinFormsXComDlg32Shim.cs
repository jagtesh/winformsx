// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Windows.Win32.UI.Controls.Dialogs;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXComDlg32Shim
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

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXComDlg32RegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            GetOpenFileName = &GetOpenFileName,
            GetSaveFileName = &GetSaveFileName,
            ChooseColor = &ChooseColor,
            ChooseFont = &ChooseFont,
            PrintDlg = &PrintDlg,
            PrintDlgEx = &PrintDlgEx,
            PageSetupDlg = &PageSetupDlg,
            CommDlgExtendedError = &CommDlgExtendedError,
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

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXComDlg32Shim).Assembly.Location) ?? string.Empty;
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
            "COMDLG32.dll.dylib",
            "libCOMDLG32.dll.dylib",
            "libCOMDLG32.dll.so",
            "COMDLG32.dll.so",
            "COMDLG32.dll",
            "comdlg32.dll",
            "comdlg32",
            "libcomdlg32.dylib",
            "libcomdlg32.so"
        ];
    }

    [UnmanagedCallersOnly]
    private static int GetOpenFileName(void* ofn)
    {
        try
        {
            return WinFormsXCommonDialogInterop.GetOpenFileName((OPENFILENAME*)ofn) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetSaveFileName(void* ofn)
    {
        try
        {
            return WinFormsXCommonDialogInterop.GetSaveFileName((OPENFILENAME*)ofn) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ChooseColor(void* chooseColor)
    {
        try
        {
            return WinFormsXCommonDialogInterop.ChooseColor((CHOOSECOLORW*)chooseColor) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ChooseFont(void* chooseFont)
    {
        try
        {
            return WinFormsXCommonDialogInterop.ChooseFont((CHOOSEFONTW*)chooseFont) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int PrintDlg(void* printDlg)
    {
        try
        {
            return WinFormsXCommonDialogInterop.PrintDlg(printDlg) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int PrintDlgEx(void* printDlgEx)
    {
        try
        {
            return WinFormsXCommonDialogInterop.PrintDlgEx((PRINTDLGEXW*)printDlgEx).Value;
        }
        catch
        {
            return HRESULT.E_FAIL.Value;
        }
    }

    [UnmanagedCallersOnly]
    private static int PageSetupDlg(void* pageSetup)
    {
        try
        {
            return WinFormsXCommonDialogInterop.PageSetupDlg((PAGESETUPDLGW*)pageSetup) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint CommDlgExtendedError()
    {
        try
        {
            return (uint)WinFormsXCommonDialogInterop.CommDlgExtendedError();
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
        public delegate* unmanaged<void*, int> GetOpenFileName;
        public delegate* unmanaged<void*, int> GetSaveFileName;
        public delegate* unmanaged<void*, int> ChooseColor;
        public delegate* unmanaged<void*, int> ChooseFont;
        public delegate* unmanaged<void*, int> PrintDlg;
        public delegate* unmanaged<void*, int> PrintDlgEx;
        public delegate* unmanaged<void*, int> PageSetupDlg;
        public delegate* unmanaged<uint> CommDlgExtendedError;
    }
}
