// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms.VisualStyles;

/// <summary>
///  API-compatible visual style renderer facade. Native UxTheme rendering is not supported by the Impeller backend.
/// </summary>
public sealed class VisualStyleRenderer : IHandle<HTHEME>
{
    private const int UnsupportedHResult = unchecked((int)0x80004001);

    private int _lastHResult = UnsupportedHResult;

    public static bool IsSupported => false;

    public static bool IsElementDefined(VisualStyleElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return false;
    }

    internal static bool IsCombinationDefined(string className, int part) => false;

    public VisualStyleRenderer(VisualStyleElement element) : this(element.ClassName, element.Part, element.State)
    {
    }

    public VisualStyleRenderer(string className, int part, int state)
    {
        ArgumentNullException.ThrowIfNull(className);
        throw new InvalidOperationException(VisualStyleInformation.IsEnabledByUser
            ? SR.VisualStylesDisabledInClientArea
            : SR.VisualStyleNotActive);
    }

    public string Class { get; private set; } = string.Empty;

    public int Part { get; private set; }

    public int State { get; private set; }

    public IntPtr Handle => throw new InvalidOperationException(SR.VisualStyleNotActive);

    HTHEME IHandle<HTHEME>.Handle => HTHEME.Null;

    internal HTHEME HTHEME => HTHEME.Null;

    public void SetParameters(VisualStyleElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        SetParameters(element.ClassName, element.Part, element.State);
    }

    public void SetParameters(string className, int part, int state)
    {
        ArgumentNullException.ThrowIfNull(className);
        throw new InvalidOperationException(SR.VisualStyleNotActive);
    }

    public void DrawBackground(IDeviceContext dc, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(dc);
        FillFallbackBackground(dc, bounds);
    }

    internal void DrawBackground(HDC dc, Rectangle bounds, HWND hwnd = default)
    {
        if (bounds.Width < 0 || bounds.Height < 0)
        {
            return;
        }

        using Graphics graphics = Graphics.FromHdcInternal((IntPtr)dc);
        using Brush brush = SystemBrushes.Control;
        graphics.FillRectangle(brush, bounds);
    }

    public void DrawBackground(IDeviceContext dc, Rectangle bounds, Rectangle clipRectangle)
    {
        ArgumentNullException.ThrowIfNull(dc);
        FillFallbackBackground(dc, Rectangle.Intersect(bounds, clipRectangle));
    }

    internal void DrawBackground(HDC dc, Rectangle bounds, Rectangle clipRectangle, HWND hwnd)
        => DrawBackground(dc, Rectangle.Intersect(bounds, clipRectangle), hwnd);

    public Rectangle DrawEdge(IDeviceContext dc, Rectangle bounds, Edges edges, EdgeStyle style, EdgeEffects effects)
    {
        ArgumentNullException.ThrowIfNull(dc);
        Graphics? graphics = dc.TryGetGraphics(create: true);
        if (graphics is not null)
        {
            ControlPaint.DrawBorder(graphics, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        return Rectangle.Inflate(bounds, -1, -1);
    }

    internal Rectangle DrawEdge(HDC dc, Rectangle bounds, Edges edges, EdgeStyle style, EdgeEffects effects)
    {
        using Graphics graphics = Graphics.FromHdcInternal((IntPtr)dc);
        ControlPaint.DrawBorder(graphics, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
        return Rectangle.Inflate(bounds, -1, -1);
    }

    public void DrawImage(Graphics g, Rectangle bounds, Image image)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(image);

        if (bounds.Width >= 0 && bounds.Height >= 0)
        {
            g.DrawImage(image, bounds);
        }
    }

    public void DrawImage(Graphics g, Rectangle bounds, ImageList imageList, int imageIndex)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(imageList);
        ArgumentOutOfRangeException.ThrowIfNegative(imageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(imageIndex, imageList.Images.Count);

        if (bounds.Width >= 0 && bounds.Height >= 0)
        {
            g.DrawImage(imageList.Images[imageIndex], bounds);
        }
    }

    public void DrawParentBackground(IDeviceContext dc, Rectangle bounds, Control childControl)
    {
        ArgumentNullException.ThrowIfNull(dc);
        ArgumentNullException.ThrowIfNull(childControl);
    }

    public void DrawText(IDeviceContext dc, Rectangle bounds, string? textToDraw)
        => DrawText(dc, bounds, textToDraw, drawDisabled: false);

    public void DrawText(IDeviceContext dc, Rectangle bounds, string? textToDraw, bool drawDisabled)
        => DrawText(dc, bounds, textToDraw, drawDisabled, TextFormatFlags.HorizontalCenter);

    public void DrawText(IDeviceContext dc, Rectangle bounds, string? textToDraw, bool drawDisabled, TextFormatFlags flags)
    {
        ArgumentNullException.ThrowIfNull(dc);
        TextRenderer.DrawText(dc, textToDraw, SystemFonts.DefaultFont, bounds, SystemColors.ControlText, flags);
    }

    internal void DrawText(HDC dc, Rectangle bounds, string? textToDraw, bool drawDisabled, TextFormatFlags flags)
    {
        using Graphics graphics = Graphics.FromHdcInternal((IntPtr)dc);
        TextRenderer.DrawText(graphics, textToDraw, SystemFonts.DefaultFont, bounds, SystemColors.ControlText, flags);
    }

    public Rectangle GetBackgroundContentRectangle(IDeviceContext dc, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return bounds.Width < 0 || bounds.Height < 0 ? Rectangle.Empty : Rectangle.Inflate(bounds, -2, -2);
    }

    internal Rectangle GetBackgroundContentRectangle(HDC dc, Rectangle bounds)
        => bounds.Width < 0 || bounds.Height < 0 ? Rectangle.Empty : Rectangle.Inflate(bounds, -2, -2);

    public Rectangle GetBackgroundExtent(IDeviceContext dc, Rectangle contentBounds)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return contentBounds.Width < 0 || contentBounds.Height < 0 ? Rectangle.Empty : Rectangle.Inflate(contentBounds, 2, 2);
    }

    public Region? GetBackgroundRegion(IDeviceContext dc, Rectangle bounds)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return bounds.Width < 0 || bounds.Height < 0 ? null : new Region(bounds);
    }

    public bool GetBoolean(BooleanProperty prop) => false;

    public Color GetColor(ColorProperty prop) => SystemColors.ControlText;

    public int GetEnumValue(EnumProperty prop) => 0;

    public string GetFilename(FilenameProperty prop) => string.Empty;

    public Font? GetFont(IDeviceContext dc, FontProperty prop)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return null;
    }

    public int GetInteger(IntegerProperty prop) => 0;

    public Size GetPartSize(IDeviceContext dc, ThemeSizeType type)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return new Size(13, 13);
    }

    internal Size GetPartSize(HDC dc, ThemeSizeType type, HWND hwnd = default) => new(13, 13);

    public Size GetPartSize(IDeviceContext dc, Rectangle bounds, ThemeSizeType type)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return bounds.Size;
    }

    public Point GetPoint(PointProperty prop) => Point.Empty;

    public Padding GetMargins(IDeviceContext dc, MarginProperty prop)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return Padding.Empty;
    }

    public string GetString(StringProperty prop) => string.Empty;

    public Rectangle GetTextExtent(IDeviceContext dc, string textToDraw, TextFormatFlags flags)
    {
        ArgumentNullException.ThrowIfNull(dc);
        textToDraw.ThrowIfNullOrEmpty();

        Size size = TextRenderer.MeasureText(dc, textToDraw, SystemFonts.DefaultFont, TextRenderer.MaxSize, flags);
        return new Rectangle(Point.Empty, size);
    }

    public Rectangle GetTextExtent(IDeviceContext dc, Rectangle bounds, string textToDraw, TextFormatFlags flags)
    {
        ArgumentNullException.ThrowIfNull(dc);
        textToDraw.ThrowIfNullOrEmpty();

        Size size = TextRenderer.MeasureText(dc, textToDraw, SystemFonts.DefaultFont, bounds.Size, flags);
        return new Rectangle(bounds.Location, size);
    }

    public TextMetrics GetTextMetrics(IDeviceContext dc)
    {
        ArgumentNullException.ThrowIfNull(dc);
        int height = SystemFonts.DefaultFont.Height;
        return new TextMetrics
        {
            Height = height,
            Ascent = height,
            AverageCharWidth = Math.Max(1, height / 2),
            MaxCharWidth = Math.Max(1, height)
        };
    }

    public HitTestCode HitTestBackground(IDeviceContext dc, Rectangle backgroundRectangle, Point pt, HitTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return backgroundRectangle.Contains(pt) ? HitTestCode.Client : HitTestCode.Nowhere;
    }

    public HitTestCode HitTestBackground(Graphics g, Rectangle backgroundRectangle, Region region, Point pt, HitTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(region);
        return backgroundRectangle.Contains(pt) ? HitTestCode.Client : HitTestCode.Nowhere;
    }

    public HitTestCode HitTestBackground(IDeviceContext dc, Rectangle backgroundRectangle, IntPtr hRgn, Point pt, HitTestOptions options)
    {
        ArgumentNullException.ThrowIfNull(dc);
        return backgroundRectangle.Contains(pt) ? HitTestCode.Client : HitTestCode.Nowhere;
    }

    public bool IsBackgroundPartiallyTransparent() => false;

    public int LastHResult => _lastHResult;

    private static void FillFallbackBackground(IDeviceContext dc, Rectangle bounds)
    {
        if (bounds.Width < 0 || bounds.Height < 0)
        {
            return;
        }

        Graphics? graphics = dc.TryGetGraphics(create: true);
        if (graphics is null)
        {
            return;
        }

        using Brush brush = SystemBrushes.Control;
        graphics.FillRectangle(brush, bounds);
    }
}
