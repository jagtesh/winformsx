// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform
{
    internal interface IPlatformProvider
    {
        IUser32Interop User32 { get; }
        IGdi32Interop Gdi32 { get; }
        IUxThemeInterop UxTheme { get; }
    }
}
