// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Com;

namespace Windows.Win32;

internal static partial class PInvoke
{
    public static unsafe HRESULT OleSetClipboard(IDataObject* pDataObj)
    {
        _ = pDataObj;

        WinFormsXCompatibilityWarning.Once(
            "PInvoke.OleSetClipboard",
            "Native OLE clipboard registration is not implemented in WinFormsX yet; OleSetClipboard acknowledged without OS integration.");

        return HRESULT.S_OK;
    }

    public static HRESULT OleFlushClipboard()
    {
        WinFormsXCompatibilityWarning.Once(
            "PInvoke.OleFlushClipboard",
            "Native OLE clipboard flushing is not implemented in WinFormsX yet; OleFlushClipboard acknowledged without OS integration.");

        return HRESULT.S_OK;
    }

    public static unsafe HRESULT OleGetClipboard(IDataObject** ppDataObj)
    {
        if (ppDataObj is not null)
        {
            *ppDataObj = null;
        }

        WinFormsXCompatibilityWarning.Once(
            "PInvoke.OleGetClipboard",
            "Native OLE clipboard retrieval is not implemented in WinFormsX yet; OleGetClipboard returned CLIPBRD_E_BAD_DATA.");

        return HRESULT.CLIPBRD_E_BAD_DATA;
    }
}
