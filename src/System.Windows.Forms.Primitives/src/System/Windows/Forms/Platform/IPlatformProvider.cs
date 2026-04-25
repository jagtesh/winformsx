// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// The platform provider contract. Combines all interop interfaces
/// under a single provider identity. The sole implementation is
/// <see cref="ImpellerPlatformProvider"/>.
/// </summary>
internal interface IPlatformProvider
{
    /// <summary>Name of this platform provider.</summary>
    string Name { get; }

    /// <summary>Window management — create, destroy, show, move, resize, styles.</summary>
    IWindowInterop Window { get; }

    /// <summary>Message pump — SendMessage, PostMessage, PeekMessage, message loop.</summary>
    IMessageInterop Message { get; }

    /// <summary>GDI — device contexts, drawing, brushes, pens, text, clipping.</summary>
    IGdiInterop Gdi { get; }

    /// <summary>Input — keyboard, mouse, cursor, focus, capture.</summary>
    IInputInterop Input { get; }

    /// <summary>System — timers, metrics, DPI, clipboard, shell, memory.</summary>
    ISystemInterop System { get; }

    /// <summary>Common dialogs — file, color, font, folder, print.</summary>
    IDialogInterop Dialog { get; }

    /// <summary>Common controls — ImageList, menus, scrollbars, icons.</summary>
    IControlInterop Control { get; }

    /// <summary>Accessibility — UIA, MSAA.</summary>
    IAccessibilityInterop Accessibility { get; }
}
