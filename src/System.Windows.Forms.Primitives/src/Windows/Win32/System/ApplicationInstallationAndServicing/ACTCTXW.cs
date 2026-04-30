// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.System.ApplicationInstallationAndServicing;

internal unsafe struct ACTCTXW
{
    public uint cbSize;
    public uint dwFlags;
    public char* lpSource;
    public ushort wProcessorArchitecture;
    public ushort wLangId;
    public char* lpAssemblyDirectory;
    public char* lpResourceName;
    public char* lpApplicationName;
    public HMODULE hModule;
}
