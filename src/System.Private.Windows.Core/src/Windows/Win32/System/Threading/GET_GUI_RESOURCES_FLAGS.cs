// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.System.Threading;

[Flags]
internal enum GET_GUI_RESOURCES_FLAGS : uint
{
    GR_GDIOBJECTS = 0,
    GR_USEROBJECTS = 1,
    GR_GDIOBJECTS_PEAK = 2,
    GR_USEROBJECTS_PEAK = 4,
}
