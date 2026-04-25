// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Static accessor for the platform provider. Impeller is the sole backend —
/// there is no fallback. Initialized once at module load by
/// <see cref="Application.InitializePlatform"/>.
/// </summary>
internal static class PlatformApi
{
    private static IPlatformProvider? s_provider;

    /// <summary>The active platform provider. Throws if not initialized.</summary>
    public static IPlatformProvider Provider =>
        s_provider ?? throw new InvalidOperationException(
            "PlatformApi has not been initialized. " +
            "Ensure Application.InitializePlatform() has run.");

    // ─── Convenience Accessors ──────────────────────────────────────────

    /// <summary>Window management.</summary>
    public static IWindowInterop Window => Provider.Window;

    /// <summary>Message pump.</summary>
    public static IMessageInterop Message => Provider.Message;

    /// <summary>GDI drawing.</summary>
    public static IGdiInterop Gdi => Provider.Gdi;

    /// <summary>Input handling.</summary>
    public static IInputInterop Input => Provider.Input;

    /// <summary>System services.</summary>
    public static ISystemInterop System => Provider.System;

    /// <summary>Common dialogs.</summary>
    public static IDialogInterop Dialog => Provider.Dialog;

    /// <summary>Common controls.</summary>
    public static IControlInterop Control => Provider.Control;

    /// <summary>Accessibility.</summary>
    public static IAccessibilityInterop Accessibility => Provider.Accessibility;

    // ─── Legacy accessor (migration shim) ───────────────────────────────

    /// <summary>
    /// Legacy User32 accessor used by existing NativeWindow call sites.
    /// Routes to <see cref="Window"/> for window management and
    /// <see cref="Message"/> for messaging. Will be removed once all
    /// call sites are migrated to the granular interfaces.
    /// </summary>
    public static IUser32Interop User32 => (IUser32Interop)Provider.Window;

    /// <summary>
    /// Initialize the platform provider. Called once from
    /// <see cref="Application.InitializePlatform"/>.
    /// </summary>
    public static void Initialize(IPlatformProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        s_provider = provider;
    }
}
