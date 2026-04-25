// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Accessibility abstraction — UI Automation (UIA) and MSAA.
/// Stubbed initially; will be implemented using platform-native
/// accessibility APIs (AT-SPI on Linux, NSAccessibility on macOS).
/// </summary>
internal unsafe interface IAccessibilityInterop
{
    // ─── UIA Provider ───────────────────────────────────────────────────

    bool UiaDisconnectProvider(nint provider);
    HRESULT UiaReturnRawElementProvider(HWND hwnd, WPARAM wParam, LPARAM lParam, nint el);
    LRESULT UiaDefWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
    bool UiaDisconnectAllProviders();
    HRESULT UiaHostProviderFromHwnd(HWND hwnd, out nint ppProvider);
    HRESULT UiaRaiseAutomationEvent(nint provider, int eventId);
    HRESULT UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId, object? oldValue, object? newValue);
    HRESULT UiaRaiseStructureChangedEvent(nint provider, int structureChangeType, int* pRuntimeId, int cRuntimeIdLen);

    // ─── MSAA ───────────────────────────────────────────────────────────

    HRESULT CreateStdAccessibleObject(HWND hwnd, int idObject, in Guid riid, out nint ppvObject);
    LRESULT LresultFromObject(in Guid riid, WPARAM wParam, nint pUnk);
    uint NotifyWinEvent(uint eventId, HWND hwnd, int idObject, int idChild);
}
