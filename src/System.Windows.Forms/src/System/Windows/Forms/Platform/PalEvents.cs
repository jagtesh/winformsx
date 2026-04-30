// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace System.Windows.Forms.Platform;

internal static class PalEvents
{
    public static event EventHandler? DisplaySettingsChanging;

    public static event PowerModeChangedEventHandler? PowerModeChanged;

    public static event SessionEndedEventHandler? SessionEnded;

    public static event SessionEndingEventHandler? SessionEnding;

    public static event SessionSwitchEventHandler? SessionSwitch;

    public static event UserPreferenceChangedEventHandler? UserPreferenceChanged;

    public static event UserPreferenceChangingEventHandler? UserPreferenceChanging;

    internal static void RaiseDisplaySettingsChanging()
        => DisplaySettingsChanging?.Invoke(null, EventArgs.Empty);

    internal static void RaisePowerModeChanged(PowerModeChangedEventArgs e)
        => PowerModeChanged?.Invoke(null!, e);

    internal static void RaiseSessionEnded(SessionEndedEventArgs e)
        => SessionEnded?.Invoke(null!, e);

    internal static void RaiseSessionEnding(SessionEndingEventArgs e)
        => SessionEnding?.Invoke(null!, e);

    internal static void RaiseSessionSwitch(SessionSwitchEventArgs e)
        => SessionSwitch?.Invoke(null!, e);

    internal static void RaiseUserPreferenceChanged(UserPreferenceChangedEventArgs e)
        => UserPreferenceChanged?.Invoke(null!, e);

    internal static void RaiseUserPreferenceChanging(UserPreferenceChangingEventArgs e)
        => UserPreferenceChanging?.Invoke(null!, e);
}
