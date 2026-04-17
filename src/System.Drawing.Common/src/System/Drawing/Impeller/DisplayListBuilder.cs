// Managed wrapper over the Impeller DisplayListBuilder C API.
// Desktop-only — not compiled in WASM builds.
// In WASM, the Canvas2DRenderingBackend handles all drawing directly.

#if !BROWSER
#pragma warning disable IDE1006
#pragma warning disable IDE0005
using System.Runtime.InteropServices;

namespace System.Drawing.Impeller;

/// <summary>
/// A managed wrapper around Impeller's DisplayListBuilder.
/// Records drawing commands and produces an immutable DisplayList.
/// Only used by ImpellerRenderingBackend in desktop builds.
/// </summary>
public sealed class DisplayListBuilder : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public DisplayListBuilder()
    {
        _handle = NativeMethods.ImpellerDisplayListBuilderNewNoCull(nint.Zero);
        if (_handle == nint.Zero)
            throw new InvalidOperationException("Failed to create Impeller DisplayListBuilder.");
    }

    internal nint Handle => _disposed
        ? throw new ObjectDisposedException(nameof(DisplayListBuilder))
        : _handle;

    // ─── State Management ──────────────────────────────────────────────

    public void Save() => NativeMethods.ImpellerDisplayListBuilderSave(_handle);
    public void Restore() => NativeMethods.ImpellerDisplayListBuilderRestore(_handle);
    public uint SaveCount => NativeMethods.ImpellerDisplayListBuilderGetSaveCount(_handle);
    public void RestoreToCount(uint count) =>
        NativeMethods.ImpellerDisplayListBuilderRestoreToCount(_handle, count);

    // ─── Transforms ────────────────────────────────────────────────────

    public void Translate(float tx, float ty) =>
        NativeMethods.ImpellerDisplayListBuilderTranslate(_handle, tx, ty);
    public void Scale(float sx, float sy) =>
        NativeMethods.ImpellerDisplayListBuilderScale(_handle, sx, sy);
    public void Rotate(float angleDegrees) =>
        NativeMethods.ImpellerDisplayListBuilderRotate(_handle, angleDegrees);
    public void SetTransform(ref ImpellerMatrix matrix) =>
        NativeMethods.ImpellerDisplayListBuilderSetTransform(_handle, ref matrix);
    public void ResetTransform() =>
        NativeMethods.ImpellerDisplayListBuilderResetTransform(_handle);

    // ─── Clipping ──────────────────────────────────────────────────────

    public void ClipRect(ref ImpellerRect rect, ImpellerClipOp op = ImpellerClipOp.Intersect) =>
        NativeMethods.ImpellerDisplayListBuilderClipRect(_handle, ref rect, op);
    public void ClipOval(ref ImpellerRect ovalBounds, ImpellerClipOp op = ImpellerClipOp.Intersect) =>
        NativeMethods.ImpellerDisplayListBuilderClipOval(_handle, ref ovalBounds, op);
    public void ClipRoundedRect(ref ImpellerRoundedRect roundedRect, ImpellerClipOp op = ImpellerClipOp.Intersect) =>
        NativeMethods.ImpellerDisplayListBuilderClipRoundedRect(_handle, ref roundedRect, op);
    public void ClipPath(nint pathHandle, ImpellerClipOp op = ImpellerClipOp.Intersect) =>
        NativeMethods.ImpellerDisplayListBuilderClipPath(_handle, pathHandle, op);

    // ─── Drawing ───────────────────────────────────────────────────────

    public void DrawPaint(nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawPaint(_handle, paintHandle);
    public void DrawRect(ref ImpellerRect rect, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawRect(_handle, ref rect, paintHandle);
    public void DrawOval(ref ImpellerRect ovalBounds, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawOval(_handle, ref ovalBounds, paintHandle);
    public void DrawRoundedRect(ref ImpellerRoundedRect roundedRect, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawRoundedRect(_handle, ref roundedRect, paintHandle);
    public void DrawLine(ref ImpellerPoint from, ref ImpellerPoint to, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawLine(_handle, ref from, ref to, paintHandle);
    public void DrawPath(nint pathHandle, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawPath(_handle, pathHandle, paintHandle);
    public void DrawTexture(nint textureHandle, ref ImpellerPoint point, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawTexture(_handle, textureHandle, ref point, paintHandle);
    public void DrawTextureRect(nint textureHandle,
        ref ImpellerRect srcRect, ref ImpellerRect dstRect, nint paintHandle) =>
        NativeMethods.ImpellerDisplayListBuilderDrawTextureRect(
            _handle, textureHandle, ref srcRect, ref dstRect, paintHandle);
    public void DrawDisplayList(nint displayListHandle, float opacity = 1.0f) =>
        NativeMethods.ImpellerDisplayListBuilderDrawDisplayList(_handle, displayListHandle, opacity);
    public void DrawParagraph(nint paragraphHandle, ref ImpellerPoint point) =>
        NativeMethods.ImpellerDisplayListBuilderDrawParagraph(_handle, paragraphHandle, ref point);
    public void SaveLayer(ref ImpellerRect bounds, nint paintHandle, nint backdropHandle = 0) =>
        NativeMethods.ImpellerDisplayListBuilderSaveLayer(_handle, ref bounds, paintHandle, backdropHandle);

    // ─── Build ─────────────────────────────────────────────────────────

    public nint Build()
    {
        var dl = NativeMethods.ImpellerDisplayListBuilderCreateDisplayListNew(_handle);
        if (dl == nint.Zero)
            throw new InvalidOperationException("Failed to build Impeller DisplayList.");
        return dl;
    }

    // ─── Dispose ───────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != nint.Zero)
        {
            NativeMethods.ImpellerDisplayListBuilderRelease(_handle);
            _handle = nint.Zero;
        }
    }
}
#endif
