// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform
{
    internal static class PlatformApi
    {
        private static IPlatformProvider? s_provider;

        [MemberNotNull(nameof(s_provider))]
        private static IPlatformProvider Provider
        {
            get
            {
                if (s_provider is null)
                {
                    throw new InvalidOperationException("PlatformApi must be initialized before use.");
                }

                return s_provider;
            }
        }

        public static IUser32Interop User32 => Provider.User32;
        public static IGdi32Interop Gdi32 => Provider.Gdi32;

        public static void Initialize(IPlatformProvider provider)
        {
            s_provider = provider;

            // Inject GDI abstraction into GetDcScope
            global::Windows.Win32.Graphics.Gdi.GetDcScope.GetDCCallback = hwnd => Gdi32.GetDC(hwnd);
            global::Windows.Win32.Graphics.Gdi.GetDcScope.GetDCExCallback = (hwnd, hrgn, flags) => Gdi32.GetDCEx(hwnd, hrgn, flags);
            global::Windows.Win32.Graphics.Gdi.GetDcScope.ReleaseDCCallback = (hwnd, hdc) => Gdi32.ReleaseDC(hwnd, hdc);
        }
    }
}
