// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.



namespace System.Windows.Forms.Platform
{
    internal interface IGdi32Interop
    {
        HDC GetDC(HWND hWnd);
        int ReleaseDC(HWND hWnd, HDC hDC);
    }
}
