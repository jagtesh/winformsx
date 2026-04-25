// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller platform provider — the sole rendering/windowing backend.
/// All OS interactions are routed through this provider.
///
/// Implementation strategy:
/// - Window/Input/System: Backed by SDL3 or platform-native APIs
/// - GDI: Routed to WinFormsX IRenderingBackend / ImpellerRenderingBackend
/// - Message: Internal message queue (no Win32 message pump)
/// - Controls: Owner-drawn via Impeller (no comctl32)
/// - Dialogs: Platform-native dialogs
/// - Accessibility: Stub initially, then AT-SPI / NSAccessibility
/// </summary>
internal sealed class ImpellerPlatformProvider : IPlatformProvider
{
    public string Name => "Impeller";

    public IWindowInterop Window { get; } = new ImpellerWindowInterop();
    public IMessageInterop Message { get; } = new ImpellerMessageInterop();
    public IGdiInterop Gdi { get; } = new ImpellerGdiInterop();
    public IInputInterop Input { get; } = new ImpellerInputInterop();
    public ISystemInterop System { get; } = new ImpellerSystemInterop();
    public IDialogInterop Dialog { get; } = new ImpellerDialogInterop();
    public IControlInterop Control { get; } = new ImpellerControlInterop();
    public IAccessibilityInterop Accessibility { get; } = new ImpellerAccessibilityInterop();
}
