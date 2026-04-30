// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe HRESULT CoCreateInstance<T>(
        Guid rclsid,
        IUnknown* pUnkOuter,
        CLSCTX dwClsContext,
        out T* ppv)
        where T : unmanaged, IComIID
    {
        _ = rclsid;
        _ = pUnkOuter;
        _ = dwClsContext;

        ppv = null;
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.CoCreateInstance",
            "COM class activation is not implemented in WinFormsX yet; CoCreateInstance returns REGDB_E_CLASSNOTREG.");
        return HRESULT.REGDB_E_CLASSNOTREG;
    }

    public static unsafe HRESULT CoCreateInstance<T>(
        in Guid rclsid,
        IUnknown* pUnkOuter,
        CLSCTX dwClsContext,
        out T* ppv)
        where T : unmanaged, IComIID
        => CoCreateInstance(rclsid, pUnkOuter, dwClsContext, out ppv);

    public static unsafe HRESULT CoCreateInstance(
        in Guid rclsid,
        IUnknown* pUnkOuter,
        CLSCTX dwClsContext,
        in Guid riid,
        out void* ppv)
    {
        _ = rclsid;
        _ = pUnkOuter;
        _ = dwClsContext;
        _ = riid;

        ppv = null;
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.CoCreateInstance.Riid",
            "COM class activation is not implemented in WinFormsX yet; CoCreateInstance returns REGDB_E_CLASSNOTREG.");
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

        WinFormsXCompatibilityWarning.Once(
            "PInvoke.CoCreateInstance.Pointer",
            "COM class activation is not implemented in WinFormsX yet; CoCreateInstance returns REGDB_E_CLASSNOTREG.");
        return HRESULT.REGDB_E_CLASSNOTREG;
    }
}
