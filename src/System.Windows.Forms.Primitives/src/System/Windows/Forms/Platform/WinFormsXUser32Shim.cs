// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace System.Windows.Forms.Platform;

internal static unsafe class WinFormsXUser32Shim
{
    private const uint DispatchVersion = 1;

    private static nint s_libraryHandle;
    private static bool s_registered;

    public static void Register()
    {
        if (OperatingSystem.IsWindows() || s_registered)
        {
            return;
        }

        string? libraryPath = FindLibraryPath();
        if (libraryPath is null || !NativeLibrary.TryLoad(libraryPath, out s_libraryHandle) || s_libraryHandle == nint.Zero)
        {
            return;
        }

        if (!NativeLibrary.TryGetExport(s_libraryHandle, "WinFormsXUser32RegisterDispatch", out nint registerExport)
            || registerExport == nint.Zero)
        {
            return;
        }

        DispatchTable dispatch = new()
        {
            Version = DispatchVersion,
            Size = (uint)sizeof(DispatchTable),
            GetCursorPos = &GetCursorPos,
            SetCursorPos = &SetCursorPos,
            GetAsyncKeyState = &GetAsyncKeyState,
            MapVirtualKey = &MapVirtualKey,
            SendInput = &SendInput,
            GetFocus = &GetFocus,
            SetFocus = &SetFocus,
            GetDesktopWindow = &GetDesktopWindow,
            GetActiveWindow = &GetActiveWindow,
            SetActiveWindow = &SetActiveWindow,
            GetForegroundWindow = &GetForegroundWindow,
            SetForegroundWindow = &SetForegroundWindow,
            GetSystemMetrics = &GetSystemMetrics,
            IsWindow = &IsWindow,
            IsWindowVisible = &IsWindowVisible,
            IsWindowEnabled = &IsWindowEnabled,
            EnableWindow = &EnableWindow,
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

            string assemblyDirectory = Path.GetDirectoryName(typeof(WinFormsXUser32Shim).Assembly.Location) ?? string.Empty;
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
        if (OperatingSystem.IsMacOS())
        {
            return ["USER32.dll.dylib", "libUSER32.dll.dylib", "USER32.dll", "user32.dll", "user32", "libuser32.dylib"];
        }

        if (OperatingSystem.IsLinux())
        {
            return ["libUSER32.dll.so", "USER32.dll.so", "USER32.dll", "user32.dll", "user32", "libuser32.so"];
        }

        return ["USER32.dll"];
    }

    [UnmanagedCallersOnly]
    private static int GetCursorPos(Point* point)
    {
        if (point is null)
        {
            return 0;
        }

        try
        {
            bool result = PlatformApi.Input.GetCursorPos(out Point managedPoint);
            *point = managedPoint;
            return result ? 1 : 0;
        }
        catch
        {
            *point = default;
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int SetCursorPos(int x, int y)
    {
        try
        {
            return PlatformApi.Input.SetCursorPos(x, y) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static short GetAsyncKeyState(int vkey)
    {
        try
        {
            return PlatformApi.Input.GetAsyncKeyState(vkey);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint MapVirtualKey(uint code, uint mapType)
    {
        try
        {
            return PlatformApi.Input.MapVirtualKey(code, mapType);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static uint SendInput(uint count, void* inputs, int cbSize)
    {
        if (count > 0 && inputs is null)
        {
            return 0;
        }

        try
        {
            return PlatformApi.Input.SendInput(new ReadOnlySpan<INPUT>(inputs, checked((int)count)), cbSize);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetFocus()
    {
        try
        {
            return (nint)PlatformApi.Input.GetFocus();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint SetFocus(nint hwnd)
    {
        try
        {
            return (nint)PlatformApi.Input.SetFocus((HWND)hwnd);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetDesktopWindow()
    {
        try
        {
            return (nint)PlatformApi.Window.GetDesktopWindow();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetActiveWindow()
    {
        try
        {
            return (nint)PlatformApi.Input.GetActiveWindow();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint SetActiveWindow(nint hwnd)
    {
        try
        {
            HWND handle = (HWND)hwnd;
            HWND previous = PlatformApi.Input.SetActiveWindow(handle);
            PlatformApi.Window.SetActiveWindow(handle);
            return (nint)previous;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetForegroundWindow()
    {
        try
        {
            return (nint)PlatformApi.Input.GetForegroundWindow();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int SetForegroundWindow(nint hwnd)
    {
        try
        {
            HWND handle = (HWND)hwnd;
            BOOL windowResult = PlatformApi.Window.SetForegroundWindow(handle);
            BOOL inputResult = PlatformApi.Input.SetForegroundWindow(handle);
            return windowResult && inputResult ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetSystemMetrics(int index)
    {
        try
        {
            return PlatformApi.System.GetSystemMetrics((SYSTEM_METRICS_INDEX)index);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int IsWindow(nint hwnd)
    {
        try
        {
            return PlatformApi.Window.IsWindow((HWND)hwnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int IsWindowVisible(nint hwnd)
    {
        try
        {
            return PlatformApi.Window.IsWindowVisible((HWND)hwnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int IsWindowEnabled(nint hwnd)
    {
        try
        {
            return PlatformApi.Window.IsWindowEnabled((HWND)hwnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int EnableWindow(nint hwnd, int enable)
    {
        try
        {
            return PlatformApi.Window.EnableWindow((HWND)hwnd, enable != 0) ? 1 : 0;
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
        public delegate* unmanaged<Point*, int> GetCursorPos;
        public delegate* unmanaged<int, int, int> SetCursorPos;
        public delegate* unmanaged<int, short> GetAsyncKeyState;
        public delegate* unmanaged<uint, uint, uint> MapVirtualKey;
        public delegate* unmanaged<uint, void*, int, uint> SendInput;
        public delegate* unmanaged<nint> GetFocus;
        public delegate* unmanaged<nint, nint> SetFocus;
        public delegate* unmanaged<nint> GetDesktopWindow;
        public delegate* unmanaged<nint> GetActiveWindow;
        public delegate* unmanaged<nint, nint> SetActiveWindow;
        public delegate* unmanaged<nint> GetForegroundWindow;
        public delegate* unmanaged<nint, int> SetForegroundWindow;
        public delegate* unmanaged<int, int> GetSystemMetrics;
        public delegate* unmanaged<nint, int> IsWindow;
        public delegate* unmanaged<nint, int> IsWindowVisible;
        public delegate* unmanaged<nint, int> IsWindowEnabled;
        public delegate* unmanaged<nint, int, int> EnableWindow;
    }
}
