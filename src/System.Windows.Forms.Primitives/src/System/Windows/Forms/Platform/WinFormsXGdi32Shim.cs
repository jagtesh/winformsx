// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXGdi32Shim
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

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXGdi32RegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            CreateCompatibleDC = &CreateCompatibleDC,
            DeleteDC = &DeleteDC,
            GetDeviceCaps = &GetDeviceCaps,
            GetObject = &GetObject,
            GetObjectType = &GetObjectType,
            GetStockObject = &GetStockObject,
            CreateSolidBrush = &CreateSolidBrush,
            CreatePen = &CreatePen,
            DeleteObject = &DeleteObject,
            SetBkColor = &SetBkColor,
            SetTextColor = &SetTextColor,
            GetBkColor = &GetBkColor,
            GetTextColor = &GetTextColor,
            GetBkMode = &GetBkMode,
            SetBkMode = &SetBkMode,
            SelectObject = &SelectObject,
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

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXGdi32Shim).Assembly.Location) ?? string.Empty;
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
            "GDI32.dll.dylib",
            "libGDI32.dll.dylib",
            "libGDI32.dll.so",
            "GDI32.dll.so",
            "GDI32.dll",
            "gdi32.dll",
            "gdi32",
            "libgdi32.dylib",
            "libgdi32.so"
        ];
    }

    [UnmanagedCallersOnly]
    private static nint CreateCompatibleDC(nint hdc)
    {
        try
        {
            return (nint)PlatformApi.Gdi.CreateCompatibleDC((HDC)hdc);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeleteDC(nint hdc)
    {
        try
        {
            return PlatformApi.Gdi.DeleteDC((HDC)hdc) ? 1 : 0;
        }
        catch
        {
            return hdc == 0 ? 0 : 1;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetDeviceCaps(nint hdc, int index)
    {
        try
        {
            return PlatformApi.Gdi.GetDeviceCaps((HDC)hdc, (GET_DEVICE_CAPS_INDEX)index);
        }
        catch
        {
            return index switch
            {
                12 => 32,
                14 => 1,
                88 or 90 => 96,
                8 => 1920,
                10 => 1080,
                _ => 0,
            };
        }
    }

    [UnmanagedCallersOnly]
    private static int GetObject(nint obj, int count, void* data)
    {
        try
        {
            return PlatformApi.Gdi.GetObject((HGDIOBJ)obj, count, data);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetObjectType(nint obj)
    {
        try
        {
            return PlatformApi.Gdi.GetObjectType((HGDIOBJ)obj);
        }
        catch
        {
            return obj == 0 ? 0u : 10u;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetStockObject(int obj)
    {
        try
        {
            return (nint)PlatformApi.Gdi.GetStockObject((GET_STOCK_OBJECT_FLAGS)obj);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint CreateSolidBrush(uint color)
    {
        try
        {
            return (nint)PlatformApi.Gdi.CreateSolidBrush((COLORREF)color);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint CreatePen(int style, int width, uint color)
    {
        try
        {
            return (nint)PlatformApi.Gdi.CreatePen((PEN_STYLE)style, width, (COLORREF)color);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeleteObject(nint obj)
    {
        try
        {
            return PlatformApi.Gdi.DeleteObject((HGDIOBJ)obj) ? 1 : 0;
        }
        catch
        {
            return obj == 0 ? 0 : 1;
        }
    }

    [UnmanagedCallersOnly]
    private static uint SetBkColor(nint hdc, uint color)
    {
        try
        {
            return PlatformApi.Gdi.SetBkColor((HDC)hdc, (COLORREF)color).Value;
        }
        catch
        {
            return 0x00FFFFFF;
        }
    }

    [UnmanagedCallersOnly]
    private static uint SetTextColor(nint hdc, uint color)
    {
        try
        {
            return PlatformApi.Gdi.SetTextColor((HDC)hdc, (COLORREF)color).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetBkColor(nint hdc)
    {
        try
        {
            return PlatformApi.Gdi.GetBkColor((HDC)hdc).Value;
        }
        catch
        {
            return 0x00FFFFFF;
        }
    }

    [UnmanagedCallersOnly]
    private static uint GetTextColor(nint hdc)
    {
        try
        {
            return PlatformApi.Gdi.GetTextColor((HDC)hdc).Value;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetBkMode(nint hdc)
    {
        try
        {
            return PlatformApi.Gdi.GetBkMode((HDC)hdc);
        }
        catch
        {
            return 2;
        }
    }

    [UnmanagedCallersOnly]
    private static int SetBkMode(nint hdc, int mode)
    {
        try
        {
            return PlatformApi.Gdi.SetBkMode((HDC)hdc, (BACKGROUND_MODE)mode);
        }
        catch
        {
            return 2;
        }
    }

    [UnmanagedCallersOnly]
    private static nint SelectObject(nint hdc, nint obj)
    {
        try
        {
            return (nint)PlatformApi.Gdi.SelectObject((HDC)hdc, (HGDIOBJ)obj);
        }
        catch
        {
            return obj;
        }
    }

    private struct DispatchTable
    {
        public uint Version;
        public uint Size;
        public delegate* unmanaged<nint, nint> CreateCompatibleDC;
        public delegate* unmanaged<nint, int> DeleteDC;
        public delegate* unmanaged<nint, int, int> GetDeviceCaps;
        public delegate* unmanaged<nint, int, void*, int> GetObject;
        public delegate* unmanaged<nint, uint> GetObjectType;
        public delegate* unmanaged<int, nint> GetStockObject;
        public delegate* unmanaged<uint, nint> CreateSolidBrush;
        public delegate* unmanaged<int, int, uint, nint> CreatePen;
        public delegate* unmanaged<nint, int> DeleteObject;
        public delegate* unmanaged<nint, uint, uint> SetBkColor;
        public delegate* unmanaged<nint, uint, uint> SetTextColor;
        public delegate* unmanaged<nint, uint> GetBkColor;
        public delegate* unmanaged<nint, uint> GetTextColor;
        public delegate* unmanaged<nint, int> GetBkMode;
        public delegate* unmanaged<nint, int, int> SetBkMode;
        public delegate* unmanaged<nint, nint, nint> SelectObject;
    }
}
