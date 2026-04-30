// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe HRESULT CoGetClassObject(
        Guid* rclsid,
        CLSCTX dwClsContext,
        void* pServerInfo,
        Guid* riid,
        void** ppv)
    {
        _ = rclsid;
        _ = dwClsContext;
        _ = pServerInfo;
        _ = riid;

        if (ppv is not null)
        {
            *ppv = null;
        }

        WinFormsXCompatibilityWarning.Once(
            "PInvoke.CoGetClassObject",
            "COM class factory activation is not implemented in WinFormsX yet; CoGetClassObject returns REGDB_E_CLASSNOTREG.");
        return HRESULT.REGDB_E_CLASSNOTREG;
    }

    public static unsafe HRESULT CoGetClassObject(
        in Guid rclsid,
        CLSCTX dwClsContext,
        void* pServerInfo,
        in Guid riid,
        ref void* ppv)
    {
        fixed (Guid* clsid = &rclsid)
        fixed (Guid* iid = &riid)
        {
            fixed (void** ppvRef = &ppv)
            {
                return CoGetClassObject(clsid, dwClsContext, pServerInfo, iid, ppvRef);
            }
        }
    }
}
