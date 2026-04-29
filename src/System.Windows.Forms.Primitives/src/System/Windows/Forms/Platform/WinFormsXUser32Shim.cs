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
    private static readonly global::System.Collections.Generic.Dictionary<HWND, HMENU> s_windowMenus = [];

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
            GetWindowRect = &GetWindowRect,
            GetClientRect = &GetClientRect,
            MapWindowPoints = &MapWindowPoints,
            ClientToScreen = &ClientToScreen,
            ScreenToClient = &ScreenToClient,
            GetParent = &GetParent,
            SetParent = &SetParent,
            GetWindow = &GetWindow,
            GetAncestor = &GetAncestor,
            IsChild = &IsChild,
            WindowFromPoint = &WindowFromPoint,
            ChildWindowFromPointEx = &ChildWindowFromPointEx,
            SetMenu = &SetMenu,
            GetMenu = &GetMenu,
            GetSystemMenu = &GetSystemMenu,
            EnableMenuItem = &EnableMenuItem,
            GetMenuItemCount = &GetMenuItemCount,
            GetMenuItemInfo = &GetMenuItemInfo,
            DrawMenuBar = &DrawMenuBar,
            GetCapture = &GetCapture,
            SetCapture = &SetCapture,
            ReleaseCapture = &ReleaseCapture,
            GetKeyState = &GetKeyState,
            GetKeyboardState = &GetKeyboardState,
            GetKeyboardLayout = &GetKeyboardLayout,
            ActivateKeyboardLayout = &ActivateKeyboardLayout,
            UpdateWindow = &UpdateWindow,
            InvalidateRect = &InvalidateRect,
            ValidateRect = &ValidateRect,
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
    private static nint GetCapture()
    {
        try
        {
            return (nint)PlatformApi.Input.GetCapture();
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint SetCapture(nint hwnd)
    {
        try
        {
            return (nint)PlatformApi.Input.SetCapture((HWND)hwnd);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ReleaseCapture()
    {
        try
        {
            return PlatformApi.Input.ReleaseCapture() ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static short GetKeyState(int vKey)
    {
        try
        {
            return PlatformApi.Input.GetKeyState(vKey);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetKeyboardState(byte* lpKeyState)
    {
        if (lpKeyState is null)
        {
            return 0;
        }

        try
        {
            Span<byte> buffer = new(lpKeyState, 256);
            fixed (byte* pinned = buffer)
            {
                BOOL result = PlatformApi.Input.GetKeyboardState(pinned);
                return result ? 1 : 0;
            }
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetKeyboardLayout(uint idThread)
    {
        try
        {
            return PlatformApi.Input.GetKeyboardLayout(idThread);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint ActivateKeyboardLayout(nint hkl, uint flags)
    {
        try
        {
            return PlatformApi.Input.ActivateKeyboardLayout(hkl, flags);
        }
        catch
        {
            return hkl;
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

    [UnmanagedCallersOnly]
    private static int GetWindowRect(nint hwnd, WinFormsXRect* lpRect)
    {
        if (lpRect is null)
        {
            return 0;
        }

        try
        {
            return PlatformApi.Window.GetWindowRect((HWND)hwnd, out RECT managedRect) && CopyRect(managedRect, lpRect) ? 1 : 0;
        }
        catch
        {
            *lpRect = default;
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetClientRect(nint hwnd, WinFormsXRect* lpRect)
    {
        if (lpRect is null)
        {
            return 0;
        }

        try
        {
            return PlatformApi.Window.GetClientRect((HWND)hwnd, out RECT managedRect) && CopyRect(managedRect, lpRect) ? 1 : 0;
        }
        catch
        {
            *lpRect = default;
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int MapWindowPoints(nint hWndFrom, nint hWndTo, WinFormsXPoint* points, uint count)
    {
        if (points is null || count == 0)
        {
            return 0;
        }

        try
        {
            int processed = 0;
            for (int i = 0; i < count; i++)
            {
                var point = new System.Drawing.Point(points[i].x, points[i].y);
                if (PlatformApi.Window.MapWindowPoints((HWND)hWndFrom, (HWND)hWndTo, ref point, 1) != 1)
                {
                    return 0;
                }

                points[i].x = point.X;
                points[i].y = point.Y;
                processed++;
            }

            return processed;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ClientToScreen(nint hwnd, WinFormsXPoint* point)
    {
        if (point is null)
        {
            return 0;
        }

        try
        {
            var managedPoint = new System.Drawing.Point(point->x, point->y);
            if (!PlatformApi.Window.ClientToScreen((HWND)hwnd, ref managedPoint))
            {
                return 0;
            }

            point->x = managedPoint.X;
            point->y = managedPoint.Y;
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ScreenToClient(nint hwnd, WinFormsXPoint* point)
    {
        if (point is null)
        {
            return 0;
        }

        try
        {
            var managedPoint = new System.Drawing.Point(point->x, point->y);
            if (!PlatformApi.Window.ScreenToClient((HWND)hwnd, ref managedPoint))
            {
                return 0;
            }

            point->x = managedPoint.X;
            point->y = managedPoint.Y;
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetParent(nint hwnd)
    {
        try
        {
            return (nint)PlatformApi.Window.GetParent((HWND)hwnd);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint SetParent(nint child, nint newParent)
    {
        try
        {
            return (nint)PlatformApi.Window.SetParent((HWND)child, (HWND)newParent);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetWindow(nint hwnd, uint uCmd)
    {
        try
        {
            return (nint)PlatformApi.Window.GetWindow((HWND)hwnd, (GET_WINDOW_CMD)uCmd);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetAncestor(nint hwnd, uint flags)
    {
        try
        {
            return (nint)PlatformApi.Window.GetAncestor((HWND)hwnd, (GET_ANCESTOR_FLAGS)flags);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int IsChild(nint hWndParent, nint hWnd)
    {
        try
        {
            return PlatformApi.Window.IsChild((HWND)hWndParent, (HWND)hWnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WindowFromPoint(WinFormsXPoint point)
    {
        try
        {
            return (nint)PlatformApi.Window.WindowFromPoint(new System.Drawing.Point(point.x, point.y));
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint ChildWindowFromPointEx(nint parent, WinFormsXPoint point, uint flags)
    {
        try
        {
            return (nint)PlatformApi.Window.ChildWindowFromPointEx((HWND)parent, new System.Drawing.Point(point.x, point.y), flags);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int SetMenu(nint hwnd, nint hmenu)
    {
        if (hwnd == nint.Zero)
        {
            return 0;
        }

        try
        {
            if (hmenu == nint.Zero)
            {
                _ = s_windowMenus.Remove((HWND)hwnd);
            }
            else
            {
                s_windowMenus[(HWND)hwnd] = (HMENU)hmenu;
            }

            return 1;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetMenu(nint hwnd)
    {
        try
        {
            return (nint)(s_windowMenus.TryGetValue((HWND)hwnd, out HMENU hMenu) ? hMenu : HMENU.Null);
        }
        catch
        {
            return (nint)HMENU.Null;
        }
    }

    [UnmanagedCallersOnly]
    private static nint GetSystemMenu(nint hwnd, int bRevert)
    {
        _ = bRevert;
        _ = hwnd;
        return (nint)HMENU.Null;
    }

    [UnmanagedCallersOnly]
    private static int EnableMenuItem(nint hmenu, uint item, uint enableFlags)
    {
        try
        {
            return PlatformApi.Control.EnableMenuItem((HMENU)hmenu, item, (MENU_ITEM_FLAGS)enableFlags) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetMenuItemCount(nint hmenu)
    {
        try
        {
            return PlatformApi.Control.GetMenuItemCount((HMENU)hmenu);
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int GetMenuItemInfo(nint hmenu, uint item, int byPosition, void* menuItemInfo)
    {
        if (menuItemInfo is null)
        {
            return 0;
        }

        try
        {
            MENUITEMINFOW managedInfo = *(MENUITEMINFOW*)menuItemInfo;
            bool result = PlatformApi.Control.GetMenuItemInfo((HMENU)hmenu, item, byPosition != 0, ref managedInfo);
            *(MENUITEMINFOW*)menuItemInfo = managedInfo;
            return result ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int DrawMenuBar(nint hwnd)
    {
        try
        {
            return PlatformApi.Control.DrawMenuBar((HWND)hwnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int UpdateWindow(nint hwnd)
    {
        try
        {
            return PlatformApi.Window.UpdateWindow((HWND)hwnd) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int InvalidateRect(nint hwnd, WinFormsXRect* rect, int erase)
    {
        try
        {
            RECT? managedRect = rect is null ? null : new RECT(rect->left, rect->top, rect->right, rect->bottom);
            return PlatformApi.Window.InvalidateRect((HWND)hwnd, managedRect, erase != 0) ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static int ValidateRect(nint hwnd, WinFormsXRect* rect)
    {
        try
        {
            RECT? managedRect = rect is null ? null : new RECT(rect->left, rect->top, rect->right, rect->bottom);
            return PlatformApi.Window.ValidateRect((HWND)hwnd, managedRect) ? 1 : 0;
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
        public delegate* unmanaged<nint, WinFormsXRect*, int> GetWindowRect;
        public delegate* unmanaged<nint, WinFormsXRect*, int> GetClientRect;
        public delegate* unmanaged<nint, nint, WinFormsXPoint*, uint, int> MapWindowPoints;
        public delegate* unmanaged<nint, WinFormsXPoint*, int> ClientToScreen;
        public delegate* unmanaged<nint, WinFormsXPoint*, int> ScreenToClient;
        public delegate* unmanaged<nint, nint> GetParent;
        public delegate* unmanaged<nint, nint, nint> SetParent;
        public delegate* unmanaged<nint, uint, nint> GetWindow;
        public delegate* unmanaged<nint, uint, nint> GetAncestor;
        public delegate* unmanaged<nint, nint, int> IsChild;
        public delegate* unmanaged<WinFormsXPoint, nint> WindowFromPoint;
        public delegate* unmanaged<nint, WinFormsXPoint, uint, nint> ChildWindowFromPointEx;
        public delegate* unmanaged<nint, nint, int> SetMenu;
        public delegate* unmanaged<nint, nint> GetMenu;
        public delegate* unmanaged<nint, int, nint> GetSystemMenu;
        public delegate* unmanaged<nint, uint, uint, int> EnableMenuItem;
        public delegate* unmanaged<nint, int> GetMenuItemCount;
        public delegate* unmanaged<nint, uint, int, void*, int> GetMenuItemInfo;
        public delegate* unmanaged<nint, int> DrawMenuBar;
        public delegate* unmanaged<nint> GetCapture;
        public delegate* unmanaged<nint, nint> SetCapture;
        public delegate* unmanaged<int> ReleaseCapture;
        public delegate* unmanaged<int, short> GetKeyState;
        public delegate* unmanaged<byte*, int> GetKeyboardState;
        public delegate* unmanaged<uint, nint> GetKeyboardLayout;
        public delegate* unmanaged<nint, uint, nint> ActivateKeyboardLayout;
        public delegate* unmanaged<nint, int> UpdateWindow;
        public delegate* unmanaged<nint, WinFormsXRect*, int, int> InvalidateRect;
        public delegate* unmanaged<nint, WinFormsXRect*, int> ValidateRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinFormsXRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private struct WinFormsXPoint
    {
        public int x;
        public int y;
    }

    private static bool CopyRect(in RECT source, WinFormsXRect* destination)
    {
        destination->left = source.left;
        destination->top = source.top;
        destination->right = source.right;
        destination->bottom = source.bottom;
        return true;
    }
}
