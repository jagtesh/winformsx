// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Accessibility;
using System.Windows.Forms.Platform;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe HRESULT UiaDisconnectProvider(IRawElementProviderSimple* provider)
        => PlatformApi.Accessibility.UiaDisconnectProvider((nint)provider) ? HRESULT.S_OK : HRESULT.E_FAIL;

    /// <inheritdoc cref="UiaDisconnectProvider(IRawElementProviderSimple*)"/>
    public static unsafe void UiaDisconnectProvider(IRawElementProviderSimple.Interface? provider, bool skipOSCheck = false)
    {
        if (provider is not null && (skipOSCheck || OsVersion.IsWindows8OrGreater()))
        {
            using var providerScope = ComHelpers.GetComScope<IRawElementProviderSimple>(provider);
            HRESULT result = UiaDisconnectProvider(providerScope);
            if (result.Failed)
            {
                Debug.WriteLine($"UiaDisconnectProvider failed with {result}");
            }

            Debug.Assert(result == HRESULT.S_OK || result == HRESULT.E_INVALIDARG, $"UiaDisconnectProvider failed with {result}");
        }
    }
}
