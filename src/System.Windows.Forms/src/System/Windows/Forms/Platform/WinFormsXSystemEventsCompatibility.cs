// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace System.Windows.Forms.Platform;

internal static class WinFormsXSystemEventsCompatibility
{
    private const string WindowClassName = "WinFormsX.SystemEvents";
    private const nint HWND_MESSAGE = -3;
    private const uint SPI_SETHIGHCONTRAST = 0x0047;
    private const uint SPI_SETDESKWALLPAPER = 0x0014;
    private const uint SPI_ICONHORIZONTALSPACING = 0x000D;
    private const uint SPI_SETDOUBLECLICKTIME = 0x0020;
    private const uint SPI_SETKEYBOARDDELAY = 0x000A;
    private const uint SPI_SETMENUDROPALIGNMENT = 0x001B;
    private static NativeSystemEventsWindow? s_window;

    public static void Initialize()
    {
        if (s_window is not null)
        {
            return;
        }

        s_window = new NativeSystemEventsWindow(WindowClassName);
    }

    private sealed class NativeSystemEventsWindow : NativeWindow
    {
        public NativeSystemEventsWindow(string className)
        {
            CreateHandle(
                new CreateParams()
                {
                    ClassName = className,
                    X = 0,
                    Y = 0,
                    Width = 0,
                    Height = 0,
                    Style = 0,
                    ExStyle = 0,
                    Parent = (HWND)HWND_MESSAGE,
                });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.MsgInternal == global::Windows.Win32.PInvoke.WM_SYSCOLORCHANGE)
            {
                PalEvents.RaiseDisplaySettingsChanging();
                PalEvents.RaiseUserPreferenceChanged(new global::Microsoft.Win32.UserPreferenceChangedEventArgs(global::Microsoft.Win32.UserPreferenceCategory.Color));
                base.WndProc(ref m);
                return;
            }

            if (m.MsgInternal == global::Windows.Win32.PInvoke.WM_SETTINGCHANGE)
            {
                global::Microsoft.Win32.UserPreferenceCategory category = MapSettingChangeCategory((uint)m.WParamInternal);
                PalEvents.RaiseUserPreferenceChanging(new global::Microsoft.Win32.UserPreferenceChangingEventArgs(category));
                PalEvents.RaiseDisplaySettingsChanging();
                PalEvents.RaiseUserPreferenceChanged(new global::Microsoft.Win32.UserPreferenceChangedEventArgs(category));
                base.WndProc(ref m);
                return;
            }

            base.WndProc(ref m);
        }

        private static global::Microsoft.Win32.UserPreferenceCategory MapSettingChangeCategory(uint wParam)
        {
            return wParam switch
            {
                SPI_SETHIGHCONTRAST => global::Microsoft.Win32.UserPreferenceCategory.Accessibility,
                SPI_SETDESKWALLPAPER => global::Microsoft.Win32.UserPreferenceCategory.Desktop,
                SPI_ICONHORIZONTALSPACING => global::Microsoft.Win32.UserPreferenceCategory.Icon,
                SPI_SETDOUBLECLICKTIME => global::Microsoft.Win32.UserPreferenceCategory.Mouse,
                SPI_SETKEYBOARDDELAY => global::Microsoft.Win32.UserPreferenceCategory.Keyboard,
                SPI_SETMENUDROPALIGNMENT => global::Microsoft.Win32.UserPreferenceCategory.Menu,
                _ => global::Microsoft.Win32.UserPreferenceCategory.General,
            };
        }
    }
}
