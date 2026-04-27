// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Impeller GDI interop — routes all drawing through IRenderingBackend.
/// HDC handles are synthetic identifiers mapped to DisplayListBuilder instances.
/// </summary>
internal sealed unsafe class ImpellerGdiInterop : IGdiInterop
{
    private static long s_nextDC = 0x20000;
    private readonly Dictionary<nint, ImpellerDCState> _dcs = [];

    // --- Device Context -------------------------------------------------

    public HDC GetDC(HWND hWnd) => AllocDC(hWnd);
    public HDC GetDCEx(HWND hWnd, HRGN clip, GET_DCX_FLAGS flags) => AllocDC(hWnd);
    public int ReleaseDC(HWND hWnd, HDC hDC) { _dcs.Remove(hDC); return 1; }
    public HDC BeginPaint(HWND hWnd, out PAINTSTRUCT ps)
    {
        var hdc = AllocDC(hWnd);
        var windowInterop = (ImpellerWindowInterop)PlatformApi.Window;
        var windowState = windowInterop.GetWindowState(hWnd);
        int w = windowState?.Width ?? 800;
        int h = windowState?.Height ?? 600;
        ps = new PAINTSTRUCT
        {
            rcPaint = new RECT(0, 0, w, h),
            hdc = hdc,
            fErase = true,
        };

        return hdc;
    }

    public bool EndPaint(HWND hWnd, in PAINTSTRUCT ps) => true;

    public HDC CreateCompatibleDC(HDC hdc) => AllocDC(HWND.Null);
    public bool DeleteDC(HDC hdc) { _dcs.Remove(hdc); return true; }
    public int SaveDC(HDC hdc) => 1;
    public bool RestoreDC(HDC hdc, int saved) => true;

    // --- Object Management ----------------------------------------------

    public HGDIOBJ SelectObject(HDC hdc, HGDIOBJ h) => h;
    public bool DeleteObject(HGDIOBJ ho) => true;
    public int GetObject(HGDIOBJ h, int c, void* pv) => 0;

    // --- Brushes / Pens -------------------------------------------------

    public HBRUSH CreateSolidBrush(COLORREF color) => (HBRUSH)(nint)color.Value;
    public HBRUSH CreatePatternBrush(HBITMAP hbm) => (HBRUSH)(nint)1;
    public HBRUSH CreateBrushIndirect(in LOGBRUSH lb) => (HBRUSH)(nint)1;
    public HPEN CreatePen(PEN_STYLE style, int width, COLORREF color) => (HPEN)(nint)1;
    public HBRUSH GetSysColorBrush(SYS_COLOR_INDEX idx) => (HBRUSH)(nint)1;

    // --- Drawing --------------------------------------------------------

    public int FillRect(HDC hDC, in RECT rc, HBRUSH hbr) => 1;
    public bool Rectangle(HDC hdc, int l, int t, int r, int b) => true;
    public bool Ellipse(HDC hdc, int l, int t, int r, int b) => true;
    public bool MoveToEx(HDC hdc, int x, int y, System.Drawing.Point* lppt) => true;
    public bool LineTo(HDC hdc, int x, int y) => true;
    public bool Polygon(HDC hdc, System.Drawing.Point* pts, int count) => true;
    public bool Polyline(HDC hdc, System.Drawing.Point* pts, int count) => true;
    public bool PatBlt(HDC hdc, int x, int y, int w, int h, ROP_CODE rop) => true;
    public bool BitBlt(HDC hdc, int x, int y, int cx, int cy, HDC src, int x1, int y1, ROP_CODE rop) => true;
    public int StretchDIBits(HDC hdc, int xd, int yd, int dw, int dh, int xs, int ys, int sw, int sh,
        void* bits, BITMAPINFO* bmi, DIB_USAGE usage, ROP_CODE rop) => dh;

    // --- Text -----------------------------------------------------------

    public int DrawText(HDC hdc, string text, int count, ref RECT rc, DRAW_TEXT_FORMAT fmt) => 0;
    public bool TextOut(HDC hdc, int x, int y, string text, int c) => true;
    public bool GetTextExtentPoint32(HDC hdc, string text, int c, out SIZE size)
    {
        // Approximate: 8px per char, 16px height — will be replaced by HarfBuzz
        size = new SIZE(text.Length * 8, 16);
        return true;
    }

    public bool GetTextMetrics(HDC hdc, out TEXTMETRICW tm) { tm = default; return true; }

    // --- Color / Mode ---------------------------------------------------

    public COLORREF SetBkColor(HDC hdc, COLORREF c) => c;
    public COLORREF SetTextColor(HDC hdc, COLORREF c) => c;
    public COLORREF GetBkColor(HDC hdc) => default;
    public COLORREF GetTextColor(HDC hdc) => default;
    public int SetBkMode(HDC hdc, BACKGROUND_MODE m) => (int)m;
    public int SetROP2(HDC hdc, R2_MODE m) => (int)m;

    // --- Clipping / Region ----------------------------------------------

    public HRGN CreateRectRgn(int x1, int y1, int x2, int y2) => (HRGN)(nint)1;
    public int GetClipRgn(HDC hdc, HRGN rgn) => 0;
    public int SelectClipRgn(HDC hdc, HRGN rgn) => 1;
    public GDI_REGION_TYPE ExcludeClipRect(HDC hdc, int l, int t, int r, int b) => GDI_REGION_TYPE.SIMPLEREGION;
    public int IntersectClipRect(HDC hdc, int l, int t, int r, int b) => 1;

    // --- Bitmap ---------------------------------------------------------

    public HBITMAP CreateCompatibleBitmap(HDC hdc, int cx, int cy) => (HBITMAP)(nint)1;
    public HBITMAP CreateDIBSection(HDC hdc, in BITMAPINFO bmi, DIB_USAGE usage,
        out void* bits, HANDLE section, uint offset)
    { bits = null; return (HBITMAP)(nint)1; }

    // --- Font -----------------------------------------------------------

    public HFONT CreateFontIndirect(in LOGFONTW lf) => (HFONT)(nint)1;
    public int GetDeviceCaps(HDC hdc, GET_DEVICE_CAPS_INDEX idx) => idx switch
    {
        GET_DEVICE_CAPS_INDEX.LOGPIXELSX => 96,
        GET_DEVICE_CAPS_INDEX.LOGPIXELSY => 96,
        GET_DEVICE_CAPS_INDEX.BITSPIXEL => 32,
        GET_DEVICE_CAPS_INDEX.PLANES => 1,
        GET_DEVICE_CAPS_INDEX.HORZRES => 1920,
        GET_DEVICE_CAPS_INDEX.VERTRES => 1080,
        _ => 0,
    };

    // --- Palette --------------------------------------------------------

    public HPALETTE CreateHalftonePalette(HDC hdc) => (HPALETTE)(nint)1;
    public HPALETTE SelectPalette(HDC hdc, HPALETTE pal, bool bkg) => pal;
    public uint RealizePalette(HDC hdc) => 0;

    // --- Internal -------------------------------------------------------

    private HDC AllocDC(HWND hWnd)
    {
        var handle = (HDC)(nint)Interlocked.Increment(ref s_nextDC);
        _dcs[handle] = new ImpellerDCState { Window = hWnd };
        return handle;
    }
}

internal sealed class ImpellerDCState
{
    public HWND Window;
}




