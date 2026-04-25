// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// GDI device context and drawing primitive abstraction.
/// The Impeller implementation maps these to <c>IRenderingBackend</c> /
/// <c>DisplayListBuilder</c> operations.
/// </summary>
internal unsafe interface IGdiInterop
{
    // ─── Device Context ─────────────────────────────────────────────────

    HDC GetDC(HWND hWnd);
    HDC GetDCEx(HWND hWnd, HRGN hrgnClip, GET_DCX_FLAGS flags);
    int ReleaseDC(HWND hWnd, HDC hDC);
    HDC BeginPaint(HWND hWnd, out PAINTSTRUCT lpPaint);
    bool EndPaint(HWND hWnd, in PAINTSTRUCT lpPaint);
    HDC CreateCompatibleDC(HDC hdc);
    bool DeleteDC(HDC hdc);
    int SaveDC(HDC hdc);
    bool RestoreDC(HDC hdc, int nSavedDC);

    // ─── Object Management ──────────────────────────────────────────────

    HGDIOBJ SelectObject(HDC hdc, HGDIOBJ h);
    bool DeleteObject(HGDIOBJ ho);
    int GetObject(HGDIOBJ h, int c, void* pv);

    // ─── Brushes / Pens ─────────────────────────────────────────────────

    HBRUSH CreateSolidBrush(COLORREF color);
    HBRUSH CreatePatternBrush(HBITMAP hbm);
    HBRUSH CreateBrushIndirect(in LOGBRUSH plbrush);
    HPEN CreatePen(PEN_STYLE iStyle, int cWidth, COLORREF color);
    HBRUSH GetSysColorBrush(SYS_COLOR_INDEX nIndex);

    // ─── Drawing ────────────────────────────────────────────────────────

    int FillRect(HDC hDC, in RECT lprc, HBRUSH hbr);
    bool Rectangle(HDC hdc, int left, int top, int right, int bottom);
    bool Ellipse(HDC hdc, int left, int top, int right, int bottom);
    bool MoveToEx(HDC hdc, int x, int y, System.Drawing.Point* lppt);
    bool LineTo(HDC hdc, int x, int y);
    bool Polygon(HDC hdc, System.Drawing.Point* apt, int cpt);
    bool Polyline(HDC hdc, System.Drawing.Point* apt, int cpt);
    bool PatBlt(HDC hdc, int x, int y, int w, int h, ROP_CODE rop);
    bool BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC hdcSrc, int x1, int y1, ROP_CODE rop);
    int StretchDIBits(HDC hdc, int xDest, int yDest, int DestWidth, int DestHeight,
        int xSrc, int ySrc, int SrcWidth, int SrcHeight, void* lpBits,
        BITMAPINFO* lpbmi, DIB_USAGE iUsage, ROP_CODE rop);

    // ─── Text ───────────────────────────────────────────────────────────

    int DrawText(HDC hdc, string lpchText, int cchText, ref RECT lprc, DRAW_TEXT_FORMAT format);
    bool TextOut(HDC hdc, int x, int y, string lpString, int c);
    bool GetTextExtentPoint32(HDC hdc, string lpString, int c, out SIZE psizl);
    bool GetTextMetrics(HDC hdc, out TEXTMETRICW lptm);

    // ─── Color / Mode ───────────────────────────────────────────────────

    COLORREF SetBkColor(HDC hdc, COLORREF color);
    COLORREF SetTextColor(HDC hdc, COLORREF color);
    COLORREF GetBkColor(HDC hdc);
    COLORREF GetTextColor(HDC hdc);
    int SetBkMode(HDC hdc, BACKGROUND_MODE mode);
    int SetROP2(HDC hdc, R2_MODE mode);

    // ─── Clipping / Region ──────────────────────────────────────────────

    HRGN CreateRectRgn(int x1, int y1, int x2, int y2);
    int GetClipRgn(HDC hdc, HRGN hrgn);
    int SelectClipRgn(HDC hdc, HRGN hrgn);
    GDI_REGION_TYPE ExcludeClipRect(HDC hdc, int left, int top, int right, int bottom);
    int IntersectClipRect(HDC hdc, int left, int top, int right, int bottom);

    // ─── Bitmap ─────────────────────────────────────────────────────────

    HBITMAP CreateCompatibleBitmap(HDC hdc, int cx, int cy);
    HBITMAP CreateDIBSection(HDC hdc, in BITMAPINFO pbmi, DIB_USAGE usage, out void* ppvBits, HANDLE hSection, uint offset);

    // ─── Font ───────────────────────────────────────────────────────────

    HFONT CreateFontIndirect(in LOGFONTW lplf);
    int GetDeviceCaps(HDC hdc, GET_DEVICE_CAPS_INDEX index);

    // ─── Palette ────────────────────────────────────────────────────────

    HPALETTE CreateHalftonePalette(HDC hdc);
    HPALETTE SelectPalette(HDC hdc, HPALETTE hPal, bool bForceBkgd);
    uint RealizePalette(HDC hdc);
}
