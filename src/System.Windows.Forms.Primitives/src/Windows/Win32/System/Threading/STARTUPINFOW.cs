// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Windows.Win32.System.Threading;

internal unsafe struct STARTUPINFOW
{
    public uint cb;
    public char* lpReserved;
    public char* lpDesktop;
    public char* lpTitle;
    public uint dwX;
    public uint dwY;
    public uint dwXSize;
    public uint dwYSize;
    public uint dwXCountChars;
    public uint dwYCountChars;
    public uint dwFillAttribute;
    public STARTUPINFOW_FLAGS dwFlags;
    public ushort wShowWindow;
    public ushort cbReserved2;
    public byte* lpReserved2;
    public HANDLE hStdInput;
    public HANDLE hStdOutput;
    public HANDLE hStdError;
}
