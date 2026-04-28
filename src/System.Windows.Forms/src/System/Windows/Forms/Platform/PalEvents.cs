// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Win32;

namespace System.Windows.Forms.Platform;

internal static class PalEvents
{
    public static event EventHandler? DisplaySettingsChanging;

    public static event UserPreferenceChangedEventHandler? UserPreferenceChanged;

    public static event UserPreferenceChangingEventHandler? UserPreferenceChanging;

    internal static void RaiseDisplaySettingsChanging()
        => DisplaySettingsChanging?.Invoke(null, EventArgs.Empty);

    internal static void RaiseUserPreferenceChanged(UserPreferenceChangedEventArgs e)
        => UserPreferenceChanged?.Invoke(null, e);

    internal static void RaiseUserPreferenceChanging(UserPreferenceChangingEventArgs e)
        => UserPreferenceChanging?.Invoke(null, e);
}
