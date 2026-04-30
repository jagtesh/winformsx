// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.System.Diagnostics.Debug;

[Flags]
internal enum FORMAT_MESSAGE_OPTIONS : uint
{
    FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200,
    FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000,
    FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000,
}
