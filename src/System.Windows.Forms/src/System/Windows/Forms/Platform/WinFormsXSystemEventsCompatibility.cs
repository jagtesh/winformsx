// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace System.Windows.Forms.Platform;

internal static class WinFormsXSystemEventsCompatibility
{
    private const string WindowClassName = "WinFormsX.SystemEvents";
    private const nint HWND_MESSAGE = -3;
    private const int WM_QUERYENDSESSION = 0x0011;
    private const int WM_ENDSESSION = 0x0016;
    private const int WM_POWERBROADCAST = 0x0218;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int PBT_APMSUSPEND = 0x0004;
    private const int PBT_APMRESUMESUSPEND = 0x0007;
    private const int PBT_APMPOWERSTATUSCHANGE = 0x000A;
    private const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    private const int WTS_CONSOLE_CONNECT = 0x1;
    private const int WTS_CONSOLE_DISCONNECT = 0x2;
    private const int WTS_REMOTE_CONNECT = 0x3;
    private const int WTS_REMOTE_DISCONNECT = 0x4;
    private const int WTS_SESSION_LOGON = 0x5;
    private const int WTS_SESSION_LOGOFF = 0x6;
    private const int WTS_SESSION_LOCK = 0x7;
    private const int WTS_SESSION_UNLOCK = 0x8;
    private const int WTS_SESSION_REMOTE_CONTROL = 0x9;
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

    internal static void RaisePowerModeChanged(global::Microsoft.Win32.PowerModes mode)
    {
        global::Microsoft.Win32.PowerModeChangedEventArgs e = new(mode);
        PalEvents.RaisePowerModeChanged(e);
        global::Microsoft.Win32.SystemEvents.RaisePowerModeChanged(mode);
    }

    internal static void RaiseSessionEnded(global::Microsoft.Win32.SessionEndReasons reason)
    {
        global::Microsoft.Win32.SessionEndedEventArgs e = new(reason);
        PalEvents.RaiseSessionEnded(e);
        global::Microsoft.Win32.SystemEvents.RaiseSessionEnded(reason);
    }

    internal static void RaiseSessionEnding(global::Microsoft.Win32.SessionEndingEventArgs e)
    {
        PalEvents.RaiseSessionEnding(e);
        global::Microsoft.Win32.SystemEvents.RaiseSessionEnding(e);
    }

    internal static void RaiseSessionSwitch(global::Microsoft.Win32.SessionSwitchReason reason)
    {
        global::Microsoft.Win32.SessionSwitchEventArgs e = new(reason);
        PalEvents.RaiseSessionSwitch(e);
        global::Microsoft.Win32.SystemEvents.RaiseSessionSwitch(reason);
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

            if (m.MsgInternal == WM_POWERBROADCAST)
            {
                RaisePowerModeChanged(MapPowerMode((int)m.WParamInternal));
                base.WndProc(ref m);
                return;
            }

            if (m.MsgInternal == WM_QUERYENDSESSION)
            {
                RaiseSessionEnding(new global::Microsoft.Win32.SessionEndingEventArgs(global::Microsoft.Win32.SessionEndReasons.SystemShutdown));
                base.WndProc(ref m);
                return;
            }

            if (m.MsgInternal == WM_ENDSESSION)
            {
                if (m.WParamInternal != 0)
                {
                    RaiseSessionEnded(global::Microsoft.Win32.SessionEndReasons.SystemShutdown);
                }

                base.WndProc(ref m);
                return;
            }

            if (m.MsgInternal == WM_WTSSESSION_CHANGE)
            {
                if (TryMapSessionSwitchReason((int)m.WParamInternal, out global::Microsoft.Win32.SessionSwitchReason reason))
                {
                    RaiseSessionSwitch(reason);
                }

                base.WndProc(ref m);
                return;
            }

            base.WndProc(ref m);
        }

        private static global::Microsoft.Win32.PowerModes MapPowerMode(int wParam)
        {
            return wParam switch
            {
                PBT_APMSUSPEND => global::Microsoft.Win32.PowerModes.Suspend,
                PBT_APMRESUMEAUTOMATIC or PBT_APMRESUMESUSPEND => global::Microsoft.Win32.PowerModes.Resume,
                PBT_APMPOWERSTATUSCHANGE => global::Microsoft.Win32.PowerModes.StatusChange,
                _ => global::Microsoft.Win32.PowerModes.StatusChange,
            };
        }

        private static bool TryMapSessionSwitchReason(int wParam, out global::Microsoft.Win32.SessionSwitchReason reason)
        {
            switch (wParam)
            {
                case WTS_CONSOLE_CONNECT:
                    reason = global::Microsoft.Win32.SessionSwitchReason.ConsoleConnect;
                    return true;
                case WTS_CONSOLE_DISCONNECT:
                    reason = global::Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect;
                    return true;
                case WTS_REMOTE_CONNECT:
                    reason = global::Microsoft.Win32.SessionSwitchReason.RemoteConnect;
                    return true;
                case WTS_REMOTE_DISCONNECT:
                    reason = global::Microsoft.Win32.SessionSwitchReason.RemoteDisconnect;
                    return true;
                case WTS_SESSION_LOGON:
                    reason = global::Microsoft.Win32.SessionSwitchReason.SessionLogon;
                    return true;
                case WTS_SESSION_LOGOFF:
                    reason = global::Microsoft.Win32.SessionSwitchReason.SessionLogoff;
                    return true;
                case WTS_SESSION_LOCK:
                    reason = global::Microsoft.Win32.SessionSwitchReason.SessionLock;
                    return true;
                case WTS_SESSION_UNLOCK:
                    reason = global::Microsoft.Win32.SessionSwitchReason.SessionUnlock;
                    return true;
                case WTS_SESSION_REMOTE_CONTROL:
                    reason = global::Microsoft.Win32.SessionSwitchReason.SessionRemoteControl;
                    return true;
                default:
                    reason = default;
                    return false;
            }
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
