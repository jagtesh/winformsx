// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.UI.Controls.Dialogs;
using Windows.Win32.System.Memory;

namespace Windows.Win32;

internal static partial class PInvokeCore
{
    private const string VirtualPrinterDriver = "winformsx";
    private const string VirtualPrinterName = "WinFormsX Virtual Printer";
    private const string VirtualPrinterPort = "FILE:";

    internal static unsafe HRESULT PrintDlgEx(PRINTDLGEXW* pPD)
    {
        if (pPD is null)
        {
            return HRESULT.E_POINTER;
        }

        if (pPD->Flags.HasFlag(PRINTDLGEX_FLAGS.PD_RETURNDEFAULT))
        {
            pPD->hDevNames = CreateDevNames();
        }

        pPD->dwResultAction = 0;
        return HRESULT.S_OK;
    }

    private static unsafe HGLOBAL CreateDevNames()
    {
        int offsetInChars = sizeof(DEVNAMES) / sizeof(char);
        int sizeInChars = checked(
            offsetInChars
            + VirtualPrinterDriver.Length + 1
            + VirtualPrinterName.Length + 1
            + VirtualPrinterPort.Length + 1
            + 1);

        HGLOBAL handle = GlobalAlloc(
            GLOBAL_ALLOC_FLAGS.GMEM_MOVEABLE | GLOBAL_ALLOC_FLAGS.GMEM_ZEROINIT,
            (uint)(sizeof(char) * sizeInChars));
        if (handle.IsNull)
        {
            return HGLOBAL.Null;
        }

        DEVNAMES* devnames = (DEVNAMES*)GlobalLock(handle);
        if (devnames is null)
        {
            GlobalFree(handle);
            return HGLOBAL.Null;
        }

        Span<char> names = new((char*)devnames, sizeInChars);

        devnames->wDriverOffset = checked((ushort)offsetInChars);
        VirtualPrinterDriver.AsSpan().CopyTo(names.Slice(offsetInChars, VirtualPrinterDriver.Length));
        offsetInChars += VirtualPrinterDriver.Length + 1;

        devnames->wDeviceOffset = checked((ushort)offsetInChars);
        VirtualPrinterName.AsSpan().CopyTo(names.Slice(offsetInChars, VirtualPrinterName.Length));
        offsetInChars += VirtualPrinterName.Length + 1;

        devnames->wOutputOffset = checked((ushort)offsetInChars);
        VirtualPrinterPort.AsSpan().CopyTo(names.Slice(offsetInChars, VirtualPrinterPort.Length));
        offsetInChars += VirtualPrinterPort.Length + 1;

        devnames->wDefault = checked((ushort)offsetInChars);
        GlobalUnlock(handle);
        return handle;
    }
}
