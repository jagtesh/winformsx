// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller accessibility interop — UIA and MSAA stubs.
/// Will be replaced with AT-SPI (Linux), NSAccessibility (macOS),
/// or a custom accessibility tree.
/// </summary>
internal sealed unsafe class ImpellerAccessibilityInterop : IAccessibilityInterop
{
    // --- UIA Provider ---------------------------------------------------

    public bool UiaDisconnectProvider(nint provider) => true;
    public HRESULT UiaReturnRawElementProvider(HWND hwnd, WPARAM wParam, LPARAM lParam, nint el)
        => HRESULT.S_OK;
    public LRESULT UiaDefWindowProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam)
        => (LRESULT)0;
    public bool UiaDisconnectAllProviders() => true;
    public HRESULT UiaHostProviderFromHwnd(HWND hwnd, out nint ppProvider)
    {
        ppProvider = 0;
        return HRESULT.E_NOTIMPL;
    }

    public HRESULT UiaRaiseAutomationEvent(nint provider, int eventId)
        => HRESULT.S_OK;
    public HRESULT UiaRaiseAutomationPropertyChangedEvent(nint provider, int propertyId, object? oldValue, object? newValue)
        => HRESULT.S_OK;
    public HRESULT UiaRaiseStructureChangedEvent(nint provider, int structureChangeType, int* pRuntimeId, int cRuntimeIdLen)
        => HRESULT.S_OK;

    // --- MSAA -----------------------------------------------------------

    public HRESULT CreateStdAccessibleObject(HWND hwnd, int idObject, in Guid riid, out nint ppvObject)
    {
        ppvObject = 0;
        return HRESULT.E_NOTIMPL;
    }

    public LRESULT LresultFromObject(in Guid riid, WPARAM wParam, nint pUnk)
        => (LRESULT)0;
    public uint NotifyWinEvent(uint eventId, HWND hwnd, int idObject, int idChild)
        => 0;
}
