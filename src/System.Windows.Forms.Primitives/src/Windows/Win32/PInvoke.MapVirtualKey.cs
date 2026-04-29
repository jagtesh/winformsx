// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

using global::System.Windows.Forms.Platform;
using Windows.Win32.UI.Input.KeyboardAndMouse;

internal static partial class PInvoke
{
    public static uint MapVirtualKey(uint uCode, MAP_VIRTUAL_KEY_TYPE uMapType)
        => PlatformApi.Input.MapVirtualKey(uCode, (uint)uMapType);
}
