// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace Windows.Win32;

internal static partial class PInvoke
{
    static PInvoke()
    {
        // WinFormsX routes drawing through the Drawing PAL and Impeller.
        // The GDI+ entrypoint declarations remain only while public surface
        // area is migrated; touching them must not initialize gdiplus.dll.
        Debug.Assert(!Gdip.Initialized);
    }
}
