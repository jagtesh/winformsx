// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform
{
    internal static class PlatformApi
    {
        public static IGdi32Interop Gdi32 { get; private set; } = null!;
        public static IUser32Interop User32 { get; private set; } = null!;
        public static IUxThemeInterop UxTheme { get; private set; } = null!;

        public static void Initialize(IPlatformProvider provider)
        {
            Gdi32 = provider.Gdi32;
            User32 = provider.User32;
            UxTheme = provider.UxTheme;

            // Inject GDI abstraction into GetDcScope
            global::Windows.Win32.Graphics.Gdi.GetDcScope.GetDCCallback = hwnd => Gdi32.GetDC(hwnd);
            global::Windows.Win32.Graphics.Gdi.GetDcScope.GetDCExCallback = (hwnd, hrgn, flags) => Gdi32.GetDCEx(hwnd, hrgn, flags);
            global::Windows.Win32.Graphics.Gdi.GetDcScope.ReleaseDCCallback = (hwnd, hdc) => Gdi32.ReleaseDC(hwnd, hdc);

            // Inject Core GDI hooks
            global::Windows.Win32.PInvokeCore.BitBltCallback = (hdc, x, y, cx, cy, hdcSrc, x1, y1, rop) => Gdi32.BitBlt(hdc, x, y, cx, cy, hdcSrc, x1, y1, rop);
            global::Windows.Win32.PInvokeCore.CreateCompatibleDCCallback = hdc => Gdi32.CreateCompatibleDC(hdc);

            // Inject Primitives User32 hooks
            global::Windows.Win32.PInvoke.GetWindowRectCallback = (HWND hwnd, out RECT rect) => User32.GetWindowRect(hwnd, out rect);
            global::Windows.Win32.PInvoke.SetWindowPosCallback = (hwnd, hwndInsertAfter, x, y, cx, cy, flags) => User32.SetWindowPos(hwnd, hwndInsertAfter, x, y, cx, cy, flags);

            // Inject Primitives UxTheme hooks
            global::Windows.Win32.PInvoke.IsAppThemedCallback = () => UxTheme.IsAppThemed();
        }
    }
}
