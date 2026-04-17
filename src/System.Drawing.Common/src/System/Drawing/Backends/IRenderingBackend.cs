// Rendering backend abstraction — the contract every rendering backend must implement.
// Eliminates all #if BROWSER checks from the drawing pipeline.

namespace System.Drawing;

/// <summary>
/// Abstraction over the rendering backend. Implementations:
/// - <c>ImpellerRenderingBackend</c> — desktop GPU rendering via Impeller C API
/// - <c>Canvas2DRenderingBackend</c> — browser rendering via Canvas2D JS interop
/// </summary>
public interface IRenderingBackend
{
    // ─── Frame Lifecycle ────────────────────────────────────────────────

    /// <summary>Begin a new rendering frame. Returns false if the frame should be skipped.</summary>
    bool BeginFrame(int width, int height);

    /// <summary>Commit the frame — present to screen (Impeller) or execute JS (Canvas2D).</summary>
    void EndFrame(int width, int height);

    // ─── State Stack ────────────────────────────────────────────────────

    void Save();
    void Restore();
    void RestoreToCount(uint count);
    uint SaveCount { get; }

    // ─── Transforms ─────────────────────────────────────────────────────

    void Translate(float dx, float dy);
    void Scale(float sx, float sy);
    void Rotate(float radians);
    void ResetTransform();

    // ─── Clipping ───────────────────────────────────────────────────────

    void ClipRect(float x, float y, float w, float h);

    // ─── Fill Operations ────────────────────────────────────────────────

    void Clear(Color color);
    void FillRect(float x, float y, float w, float h, Color color);
    void FillEllipse(float x, float y, float w, float h, Color color);
    void FillPolygon(Point[] points, Color color);

    // ─── Stroke Operations ──────────────────────────────────────────────

    void StrokeRect(float x, float y, float w, float h, Color color, float lineWidth);
    void StrokeEllipse(float x, float y, float w, float h, Color color, float lineWidth);
    void StrokeLine(float x1, float y1, float x2, float y2, Color color, float lineWidth);
    void StrokePolygon(Point[] points, Color color, float lineWidth);

    // ─── Text ───────────────────────────────────────────────────────────

    void DrawString(string text, float x, float y, Color color,
                    string fontFamily, float fontSize, bool bold, bool italic);

    void DrawStringAligned(string text, RectangleF bounds, ContentAlignment alignment,
                           Color color, string fontFamily, float fontSize, bool bold, bool italic);

    SizeF MeasureString(string text, string fontFamily, float fontSize, bool bold, bool italic);

    // ─── Paths (advanced — no-op on backends that don't support native paths) ───

    void DrawBezier(float x1, float y1, float cx1, float cy1,
                    float cx2, float cy2, float x2, float y2, Color color, float lineWidth);

    void DrawPath(nint pathHandle, Color color, float lineWidth);
    void FillPath(nint pathHandle, Color color);

    // ─── Images ─────────────────────────────────────────────────────────

    void DrawImage(Image image, float x, float y);
    void DrawImageRect(Image image, float sx, float sy, float sw, float sh,
                       float dx, float dy, float dw, float dh);
}
