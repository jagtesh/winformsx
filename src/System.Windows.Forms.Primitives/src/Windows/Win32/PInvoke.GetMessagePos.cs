// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32;

using global::System.Drawing;
using global::System.Windows.Forms.Platform;

internal static partial class PInvoke
{
    public static uint GetMessagePos()
    {
        if (!PlatformApi.Input.GetCursorPos(out Point point))
        {
            return 0;
        }

        return (uint)((ushort)point.X | ((ushort)point.Y << 16));
    }
}
