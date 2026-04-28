// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;

namespace System.Drawing;

internal sealed class ManagedBitmapRenderingBackend(Bitmap bitmap) : IRenderingBackend
{
    private readonly Stack<Matrix3x2> _transforms = new();
    private Matrix3x2 _transform = Matrix3x2.Identity;

    public uint SaveCount => (uint)_transforms.Count;

    public bool BeginFrame(int width, int height) => true;

    public void EndFrame(int width, int height)
    {
    }

    public void Save() => _transforms.Push(_transform);

    public void Restore()
    {
        if (_transforms.TryPop(out Matrix3x2 transform))
        {
            _transform = transform;
        }
    }

    public void RestoreToCount(uint count)
    {
        while (_transforms.Count > count)
        {
            Restore();
        }
    }

    public void Translate(float dx, float dy) =>
        _transform = Matrix3x2.Multiply(_transform, Matrix3x2.CreateTranslation(dx, dy));

    public void Scale(float sx, float sy) =>
        _transform = Matrix3x2.Multiply(_transform, Matrix3x2.CreateScale(sx, sy));

    public void Rotate(float radians) =>
        _transform = Matrix3x2.Multiply(_transform, Matrix3x2.CreateRotation(radians));

    public void ResetTransform() => _transform = Matrix3x2.Identity;

    public void ClipRect(float x, float y, float w, float h)
    {
    }

    public void Clear(Color color)
    {
        Span<int> pixels = bitmap.ManagedPixelBuffer;
        pixels.Fill(color.ToArgb());
    }

    public void FillRect(float x, float y, float w, float h, Color color)
    {
        Rectangle rect = ToDeviceRect(x, y, w, h);
        FillDeviceRect(rect, color.ToArgb());
    }

    public void FillEllipse(float x, float y, float w, float h, Color color) => FillRect(x, y, w, h, color);

    public void FillPolygon(Point[] points, Color color)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0)
        {
            return;
        }

        int left = points[0].X;
        int top = points[0].Y;
        int right = points[0].X;
        int bottom = points[0].Y;
        for (int i = 1; i < points.Length; i++)
        {
            left = Math.Min(left, points[i].X);
            top = Math.Min(top, points[i].Y);
            right = Math.Max(right, points[i].X);
            bottom = Math.Max(bottom, points[i].Y);
        }

        FillRect(left, top, right - left + 1, bottom - top + 1, color);
    }

    public void StrokeRect(float x, float y, float w, float h, Color color, float lineWidth)
    {
        int width = Math.Max(1, (int)MathF.Ceiling(lineWidth));
        FillRect(x, y, w, width, color);
        FillRect(x, y + h - width, w, width, color);
        FillRect(x, y, width, h, color);
        FillRect(x + w - width, y, width, h, color);
    }

    public void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth) =>
        StrokeRect(x, y, w, h, color, lineWidth);

    public void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth)
    {
        Point p1 = ToDevicePoint(x1, y1);
        Point p2 = ToDevicePoint(x2, y2);
        DrawDeviceLine(p1.X, p1.Y, p2.X, p2.Y, color.ToArgb(), Math.Max(1, (int)MathF.Ceiling(lineWidth)));
    }

    public void StrokePolygon(Point[] points, Color color, float lineWidth)
    {
        ArgumentNullException.ThrowIfNull(points);
        for (int i = 1; i < points.Length; i++)
        {
            StrokeLine(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y, color, lineWidth);
        }

        if (points.Length > 1)
        {
            StrokeLine(points[^1].X, points[^1].Y, points[0].X, points[0].Y, color, lineWidth);
        }
    }

    public void DrawString(string text, float x, float y, Color color, string fontFamily, float fontSize, bool bold, bool italic)
    {
    }

    public void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment, Color color, string fontFamily, float fontSize, bool bold, bool italic)
    {
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic) =>
        string.IsNullOrEmpty(text) ? SizeF.Empty : new SizeF(text.Length * fontSize * 0.55f, fontSize * 1.25f);

    public void DrawBezier(float x1, float y1, float cx1, float cy1, float cx2, float cy2, float x2, float y2, Color color, float lineWidth) =>
        StrokeLine(x1, y1, x2, y2, color, lineWidth);

    public void DrawPath(nint pathHandle, Color color, float lineWidth)
    {
    }

    public void FillPath(nint pathHandle, Color color)
    {
    }

    public void DrawImage(Image image, float x, float y) =>
        DrawImageRect(image, 0, 0, image.Width, image.Height, x, y, image.Width, image.Height);

    public void DrawImageRect(Image image, float sx, float sy, float sw, float sh, float dx, float dy, float dw, float dh)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image is not Bitmap source)
        {
            return;
        }

        ReadOnlySpan<int> sourcePixels = source.ManagedPixels;
        if (sourcePixels.IsEmpty)
        {
            return;
        }

        Rectangle dest = ToDeviceRect(dx, dy, dw, dh);
        int srcLeft = Math.Clamp((int)MathF.Floor(sx), 0, source.Width);
        int srcTop = Math.Clamp((int)MathF.Floor(sy), 0, source.Height);
        int srcWidth = Math.Clamp((int)MathF.Ceiling(sw), 0, source.Width - srcLeft);
        int srcHeight = Math.Clamp((int)MathF.Ceiling(sh), 0, source.Height - srcTop);
        if (dest.Width <= 0 || dest.Height <= 0 || srcWidth <= 0 || srcHeight <= 0)
        {
            return;
        }

        Span<int> targetPixels = bitmap.ManagedPixelBuffer;
        int left = Math.Max(0, dest.Left);
        int top = Math.Max(0, dest.Top);
        int right = Math.Min(bitmap.Width, dest.Right);
        int bottom = Math.Min(bitmap.Height, dest.Bottom);

        for (int y = top; y < bottom; y++)
        {
            int srcY = srcTop + ((y - dest.Top) * srcHeight / dest.Height);
            for (int x = left; x < right; x++)
            {
                int srcX = srcLeft + ((x - dest.Left) * srcWidth / dest.Width);
                targetPixels[(y * bitmap.Width) + x] = sourcePixels[(srcY * source.Width) + srcX];
            }
        }
    }

    private Rectangle ToDeviceRect(float x, float y, float w, float h)
    {
        Point p1 = ToDevicePoint(x, y);
        Point p2 = ToDevicePoint(x + w, y + h);
        int left = Math.Min(p1.X, p2.X);
        int top = Math.Min(p1.Y, p2.Y);
        int right = Math.Max(p1.X, p2.X);
        int bottom = Math.Max(p1.Y, p2.Y);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private Point ToDevicePoint(float x, float y)
    {
        Vector2 point = Vector2.Transform(new Vector2(x, y), _transform);
        return new Point((int)MathF.Round(point.X), (int)MathF.Round(point.Y));
    }

    private void FillDeviceRect(Rectangle rect, int argb)
    {
        Span<int> pixels = bitmap.ManagedPixelBuffer;
        int left = Math.Max(0, rect.Left);
        int top = Math.Max(0, rect.Top);
        int right = Math.Min(bitmap.Width, rect.Right);
        int bottom = Math.Min(bitmap.Height, rect.Bottom);
        for (int y = top; y < bottom; y++)
        {
            pixels.Slice((y * bitmap.Width) + left, right - left).Fill(argb);
        }
    }

    private void DrawDeviceLine(int x0, int y0, int x1, int y1, int argb, int lineWidth)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            FillDeviceRect(new Rectangle(x0, y0, lineWidth, lineWidth), argb);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
