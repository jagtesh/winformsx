// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com;

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    public static unsafe HRESULT CoCreateInstance<T>(
        in Guid rclsid,
        IUnknown* pUnkOuter,
        CLSCTX dwClsContext,
        out T* ppv)
        where T : unmanaged, IComIID
    {
        _ = rclsid;
        _ = pUnkOuter;
        _ = dwClsContext;

        ppv = null;
        return HRESULT.REGDB_E_CLASSNOTREG;
    }

    public static unsafe HRESULT CoCreateInstance(
        Guid* rclsid,
        IUnknown* pUnkOuter,
        CLSCTX dwClsContext,
        Guid* riid,
        void** ppv)
    {
        _ = rclsid;
        _ = pUnkOuter;
        _ = dwClsContext;
        _ = riid;

        if (ppv is not null)
        {
            *ppv = null;
        }

        return HRESULT.REGDB_E_CLASSNOTREG;
    }
}
