// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

namespace Microsoft.Win32;

public sealed class SystemEvents
{
    private static long s_nextTimerId;
    private static readonly ConcurrentDictionary<IntPtr, Timer> s_timers = [];

    private SystemEvents()
    {
    }

    public static event EventHandler? DisplaySettingsChanged;

    public static event EventHandler? DisplaySettingsChanging;

    public static event EventHandler? EventsThreadShutdown;

    public static event EventHandler? InstalledFontsChanged;

    public static event EventHandler? LowMemory;

    public static event EventHandler? PaletteChanged;

    public static event PowerModeChangedEventHandler? PowerModeChanged;

    public static event SessionEndedEventHandler? SessionEnded;

    public static event SessionEndingEventHandler? SessionEnding;

    public static event SessionSwitchEventHandler? SessionSwitch;

    public static event EventHandler? TimeChanged;

    public static event TimerElapsedEventHandler? TimerElapsed;

    public static event UserPreferenceChangedEventHandler? UserPreferenceChanged;

    public static event UserPreferenceChangingEventHandler? UserPreferenceChanging;

    public static IntPtr CreateTimer(int interval)
    {
        if (interval <= 0)
        {
            throw new ArgumentException("Timer interval must be greater than zero.", nameof(interval));
        }

        IntPtr timerId = new(Interlocked.Increment(ref s_nextTimerId));
        Timer timer = new(
            static state =>
            {
                IntPtr id = (IntPtr)state!;
                TimerElapsed?.Invoke(null!, new TimerElapsedEventArgs(id));
            },
            timerId,
            interval,
            interval);

        s_timers[timerId] = timer;
        return timerId;
    }

    public static void InvokeOnEventsThread(Delegate method)
    {
        ArgumentNullException.ThrowIfNull(method);

        method.DynamicInvoke();
    }

    public static void KillTimer(IntPtr timerId)
    {
        if (s_timers.TryRemove(timerId, out Timer? timer))
        {
            timer.Dispose();
        }
    }

    internal static void RaiseDisplaySettingsChanged()
        => DisplaySettingsChanged?.Invoke(null, EventArgs.Empty);

    internal static void RaiseDisplaySettingsChanging()
        => DisplaySettingsChanging?.Invoke(null, EventArgs.Empty);

    internal static void RaiseEventsThreadShutdown()
        => EventsThreadShutdown?.Invoke(null, EventArgs.Empty);

    internal static void RaiseInstalledFontsChanged()
        => InstalledFontsChanged?.Invoke(null, EventArgs.Empty);

    internal static void RaiseLowMemory()
        => LowMemory?.Invoke(null, EventArgs.Empty);

    internal static void RaisePaletteChanged()
        => PaletteChanged?.Invoke(null, EventArgs.Empty);

    internal static void RaisePowerModeChanged(PowerModes mode)
        => PowerModeChanged?.Invoke(null!, new PowerModeChangedEventArgs(mode));

    internal static void RaiseSessionEnded(SessionEndReasons reason)
        => SessionEnded?.Invoke(null!, new SessionEndedEventArgs(reason));

    internal static void RaiseSessionEnding(SessionEndingEventArgs e)
        => SessionEnding?.Invoke(null!, e);

    internal static void RaiseSessionSwitch(SessionSwitchReason reason)
        => SessionSwitch?.Invoke(null!, new SessionSwitchEventArgs(reason));

    internal static void RaiseTimeChanged()
        => TimeChanged?.Invoke(null, EventArgs.Empty);

    internal static void RaiseUserPreferenceChanged(UserPreferenceCategory category)
        => UserPreferenceChanged?.Invoke(null!, new UserPreferenceChangedEventArgs(category));

    internal static void RaiseUserPreferenceChanging(UserPreferenceCategory category)
        => UserPreferenceChanging?.Invoke(null!, new UserPreferenceChangingEventArgs(category));
}

public sealed class PowerModeChangedEventArgs : EventArgs
{
    public PowerModeChangedEventArgs(PowerModes mode) => Mode = mode;

    public PowerModes Mode { get; }
}

public delegate void PowerModeChangedEventHandler(object sender, PowerModeChangedEventArgs e);

public enum PowerModes
{
    Resume = 1,
    StatusChange = 2,
    Suspend = 3,
}

public sealed class SessionEndedEventArgs : EventArgs
{
    public SessionEndedEventArgs(SessionEndReasons reason) => Reason = reason;

    public SessionEndReasons Reason { get; }
}

public delegate void SessionEndedEventHandler(object sender, SessionEndedEventArgs e);

public sealed class SessionEndingEventArgs : EventArgs
{
    public SessionEndingEventArgs(SessionEndReasons reason) => Reason = reason;

    public bool Cancel { get; set; }

    public SessionEndReasons Reason { get; }
}

public delegate void SessionEndingEventHandler(object sender, SessionEndingEventArgs e);

public enum SessionEndReasons
{
    Logoff = 1,
    SystemShutdown = 2,
}

public sealed class SessionSwitchEventArgs : EventArgs
{
    public SessionSwitchEventArgs(SessionSwitchReason reason) => Reason = reason;

    public SessionSwitchReason Reason { get; }
}

public delegate void SessionSwitchEventHandler(object sender, SessionSwitchEventArgs e);

public enum SessionSwitchReason
{
    ConsoleConnect = 1,
    ConsoleDisconnect = 2,
    RemoteConnect = 3,
    RemoteDisconnect = 4,
    SessionLogon = 5,
    SessionLogoff = 6,
    SessionLock = 7,
    SessionUnlock = 8,
    SessionRemoteControl = 9,
}

public sealed class TimerElapsedEventArgs : EventArgs
{
    public TimerElapsedEventArgs(IntPtr timerId) => TimerId = timerId;

    public IntPtr TimerId { get; }
}

public delegate void TimerElapsedEventHandler(object sender, TimerElapsedEventArgs e);

public enum UserPreferenceCategory
{
    Accessibility = 1,
    Color = 2,
    Desktop = 3,
    General = 4,
    Icon = 5,
    Keyboard = 6,
    Menu = 7,
    Mouse = 8,
    Policy = 9,
    Power = 10,
    Screensaver = 11,
    Window = 12,
    Locale = 13,
    VisualStyle = 14,
}

public sealed class UserPreferenceChangedEventArgs : EventArgs
{
    public UserPreferenceChangedEventArgs(UserPreferenceCategory category) => Category = category;

    public UserPreferenceCategory Category { get; }
}

public delegate void UserPreferenceChangedEventHandler(object sender, UserPreferenceChangedEventArgs e);

public sealed class UserPreferenceChangingEventArgs : EventArgs
{
    public UserPreferenceChangingEventArgs(UserPreferenceCategory category) => Category = category;

    public UserPreferenceCategory Category { get; }
}

public delegate void UserPreferenceChangingEventHandler(object sender, UserPreferenceChangingEventArgs e);
