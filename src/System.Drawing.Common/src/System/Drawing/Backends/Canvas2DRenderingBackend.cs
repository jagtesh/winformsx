// Canvas2D rendering backend for WASM — records drawing commands as JavaScript
// and executes them on the HTML5 Canvas 2D context.

#if BROWSER
namespace System.Drawing;

/// <summary>
/// WASM rendering backend. Records Canvas2D JavaScript commands during PaintTree
/// and executes them via eval() on EndFrame.
/// </summary>
internal sealed class Canvas2DRenderingBackend : IRenderingBackend
{
    private WasmDrawCommandBuffer? _buffer;
    private uint _saveCount;

    // ─── Frame Lifecycle ────────────────────────────────────────────────

    public bool BeginFrame(int width, int height)
    {
        _buffer = new WasmDrawCommandBuffer();
        WasmDrawCommandBuffer.Current = _buffer;
        _saveCount = 0;
        return true;
    }

    public void EndFrame(int width, int height)
    {
        WasmDrawCommandBuffer.Current = null;
        if (_buffer is null) return;
        WasmJsBridge.ExecuteScript(_buffer, width, height);
        _buffer = null;
    }

    // ─── State Stack ────────────────────────────────────────────────────

    public void Save() { _saveCount++; _buffer?.Save(); }
    public void Restore() { if (_saveCount > 0) _saveCount--; _buffer?.Restore(); }
    public void RestoreToCount(uint count) { _saveCount = count; }
    public uint SaveCount => _saveCount;

    // ─── Transforms ─────────────────────────────────────────────────────

    public void Translate(float dx, float dy) => _buffer?.Translate(dx, dy);
    public void Scale(float sx, float sy) => _buffer?.Scale(sx, sy);
    public void Rotate(float radians) => _buffer?.Rotate(radians);
    public void ResetTransform() => _buffer?.ResetTransform();

    // ─── Clipping ───────────────────────────────────────────────────────

    public void ClipRect(float x, float y, float w, float h) => _buffer?.ClipRect(x, y, w, h);

    // ─── Fill Operations ────────────────────────────────────────────────

    public void Clear(Color color) => _buffer?.Clear(color);
    public void FillRect(float x, float y, float w, float h, Color color) => _buffer?.FillRect(x, y, w, h, color);
    public void FillEllipse(float x, float y, float w, float h, Color color) => _buffer?.FillEllipse(x, y, w, h, color);
    public void FillPolygon(Point[] points, Color color) => _buffer?.FillPolygon(points, color);

    // ─── Stroke Operations ──────────────────────────────────────────────

    public void StrokeRect(float x, float y, float w, float h, Color color, float lineWidth) => _buffer?.StrokeRect(x, y, w, h, color, lineWidth);
    public void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth) => _buffer?.StrokeEllipse(x, y, w, h, color, lineWidth);
    public void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth) => _buffer?.DrawLine(x1, y1, x2, y2, color, lineWidth);
    public void StrokePolygon(Point[] points, Color color, float lineWidth) => _buffer?.StrokePolygon(points, color, lineWidth);

    // ─── Text ───────────────────────────────────────────────────────────

    public void DrawString(string text, float x, float y, Color color,
                           string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        float lineHeight = fontSize * 1.25f;
        for (int i = 0; i < lines.Length; i++)
        {
            _buffer?.FillText(lines[i], x, y + (i * lineHeight), color, fontFamily, fontSize, bold, italic);
        }
    }

    public void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment,
                                  Color color, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text)) return;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        float lineHeight = fontSize * 1.25f;
        
        // Alignment via MeasureString approximation
        float x = bounds.X, y = bounds.Y;
        var measured = MeasureString(text, fontFamily, fontSize, bold, italic);
        switch (alignment)
        {
            case ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter:
                x = bounds.X + (bounds.Width - measured.Width) / 2f; break;
            case ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight:
                x = bounds.X + bounds.Width - measured.Width; break;
        }
        switch (alignment)
        {
            case ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight:
                y = bounds.Y + (bounds.Height - measured.Height) / 2f; break;
            case ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight:
                y = bounds.Y + bounds.Height - measured.Height; break;
        }
        
        for (int i = 0; i < lines.Length; i++)
        {
            _buffer?.FillText(lines[i], x, y + (i * lineHeight), color, fontFamily, fontSize, bold, italic);
        }
    }

    public SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic)
    {
        if (string.IsNullOrEmpty(text)) return SizeF.Empty;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int lineCount = Math.Max(1, lines.Length);
        int maxLength = 0;
        foreach (var line in lines) if (line.Length > maxLength) maxLength = line.Length;

        // Approximate text measurement using font metrics
        float charWidth = fontSize * 0.6f;
        float height = fontSize * 1.25f * lineCount;
        return new SizeF(maxLength * charWidth, height);
    }

    // ─── Paths (not supported in Canvas2D — no-ops) ─────────────────────

    public void DrawBezier(float x1, float y1, float cx1, float cy1,
                           float cx2, float cy2, float x2, float y2, Color color, float lineWidth) { }
    public void DrawPath(nint pathHandle, Color color, float lineWidth) { }
    public void FillPath(nint pathHandle, Color color) { }

    // ─── Images (not yet supported in Canvas2D — no-ops) ────────────────

    public void DrawImage(Image image, float x, float y) { }
    public void DrawImageRect(Image image, float sx, float sy, float sw, float sh,
                              float dx, float dy, float dw, float dh) { }
}
#endif
