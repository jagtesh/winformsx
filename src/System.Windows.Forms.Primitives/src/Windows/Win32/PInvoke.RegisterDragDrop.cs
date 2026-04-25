// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Windows.Win32.System.Ole;

namespace Windows.Win32;

internal static partial class PInvoke
{
    /// <inheritdoc cref="RegisterDragDrop(HWND, IDropTarget*)"/>
    public static unsafe HRESULT RegisterDragDrop<T>(T hwnd, IDropTarget.Interface pDropTarget)
        where T : IHandle<HWND>
    {
        // Impeller: drag and drop not yet supported
        GC.KeepAlive(hwnd.Wrapper);
        return HRESULT.S_OK;
    }
}
