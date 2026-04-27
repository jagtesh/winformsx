// Raw P/Invoke declarations for the Impeller C API (v1.2.0 — Flutter 3.41.0).
// Uses [LibraryImport] for source-generated, NativeAOT-compatible marshalling.
// Hot-path draw calls use [SuppressGCTransition] for near-native performance.

#pragma warning disable IDE0005
using System.Runtime.CompilerServices;
#pragma warning disable IDE0005
using System.Runtime.InteropServices;

namespace System.Drawing.Impeller;

/// <summary>
/// Raw P/Invoke bindings to the Impeller C API (impeller.dll).
/// Aligned with Flutter 3.41.0 engine commit 3452d735bd.
/// </summary>
public static partial class NativeMethods
{
    private const string ImpellerLib = "impeller";

    // ─── Version ──────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerGetVersion")]
    public static partial uint ImpellerGetVersion();

    // ─── Context ──────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerContextCreateOpenGLESNew")]
    public static partial nint ImpellerContextCreateOpenGLESNew(
        uint version,
        nint glProcAddressCallback,
        nint userData);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerContextCreateVulkanNew")]
    public static partial nint ImpellerContextCreateVulkanNew(
        uint version,
        ref ImpellerContextVulkanSettings settings);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerContextGetVulkanInfo")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ImpellerContextGetVulkanInfo(
        nint context,
        out ImpellerContextVulkanInfo outVulkanInfo);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerContextRetain")]
    public static partial void ImpellerContextRetain(nint context);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerContextRelease")]
    public static partial void ImpellerContextRelease(nint context);

    // ─── Vulkan Swapchain ─────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerVulkanSwapchainCreateNew")]
    public static partial nint ImpellerVulkanSwapchainCreateNew(nint context, nint vulkanSurfaceKHR);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerVulkanSwapchainAcquireNextSurfaceNew")]
    public static partial nint ImpellerVulkanSwapchainAcquireNextSurfaceNew(nint swapchain);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerVulkanSwapchainRetain")]
    public static partial void ImpellerVulkanSwapchainRetain(nint swapchain);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerVulkanSwapchainRelease")]
    public static partial void ImpellerVulkanSwapchainRelease(nint swapchain);

    // ─── Surface ──────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerSurfaceCreateWrappedFBONew")]
    public static partial nint ImpellerSurfaceCreateWrappedFBONew(
        nint context,
        ulong fbo,
        uint format,
        ref ImpellerISize size);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerSurfaceRetain")]
    public static partial void ImpellerSurfaceRetain(nint surface);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerSurfaceRelease")]
    public static partial void ImpellerSurfaceRelease(nint surface);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerSurfaceDrawDisplayList")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ImpellerSurfaceDrawDisplayList(
        nint surface,
        nint displayList);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerSurfacePresent")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ImpellerSurfacePresent(nint surface);

    // ─── DisplayListBuilder ───────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderNew")]
    public static partial nint ImpellerDisplayListBuilderNew(ref ImpellerRect cullRect);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderNew")]
    public static partial nint ImpellerDisplayListBuilderNewNoCull(nint nullCullRect);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderRetain")]
    public static partial void ImpellerDisplayListBuilderRetain(nint builder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderRelease")]
    public static partial void ImpellerDisplayListBuilderRelease(nint builder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderCreateDisplayListNew")]
    public static partial nint ImpellerDisplayListBuilderCreateDisplayListNew(nint builder);

    // Save/Restore
    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderSave")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderSave(nint builder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderRestore")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderRestore(nint builder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderGetSaveCount")]
    [SuppressGCTransition]
    public static partial uint ImpellerDisplayListBuilderGetSaveCount(nint builder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderRestoreToCount")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderRestoreToCount(nint builder, uint count);

    // Transforms
    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderTranslate")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderTranslate(
        nint builder, float tx, float ty);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderScale")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderScale(
        nint builder, float sx, float sy);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderRotate")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderRotate(
        nint builder, float angleDegrees);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderSetTransform")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderSetTransform(
        nint builder, ref ImpellerMatrix matrix);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderResetTransform")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderResetTransform(nint builder);

    // Clipping
    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderClipRect")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderClipRect(
        nint builder, ref ImpellerRect rect, ImpellerClipOp op);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderClipOval")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderClipOval(
        nint builder, ref ImpellerRect ovalBounds, ImpellerClipOp op);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderClipPath")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderClipPath(
        nint builder, nint path, ImpellerClipOp op);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderClipRoundedRect")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderClipRoundedRect(
        nint builder, ref ImpellerRoundedRect roundedRect, ImpellerClipOp op);

    // Drawing commands
    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawPaint")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawPaint(
        nint builder, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawRect")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawRect(
        nint builder, ref ImpellerRect rect, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawOval")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawOval(
        nint builder, ref ImpellerRect ovalBounds, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawRoundedRect")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawRoundedRect(
        nint builder, ref ImpellerRoundedRect roundedRect, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawLine")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawLine(
        nint builder, ref ImpellerPoint from, ref ImpellerPoint to, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawPath")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawPath(
        nint builder, nint path, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawTexture")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawTexture(
        nint builder, nint texture, ref ImpellerPoint point, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawTextureRect")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawTextureRect(
        nint builder, nint texture, ref ImpellerRect srcRect, ref ImpellerRect dstRect, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawDisplayList")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawDisplayList(
        nint builder, nint displayList, float opacity);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderDrawParagraph")]
    [SuppressGCTransition]
    public static partial void ImpellerDisplayListBuilderDrawParagraph(
        nint builder, nint paragraph, ref ImpellerPoint point);

    // SaveLayer for compositing effects
    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListBuilderSaveLayer")]
    public static partial void ImpellerDisplayListBuilderSaveLayer(
        nint builder, ref ImpellerRect bounds, nint paint, nint backdrop);

    // ─── DisplayList ──────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListRetain")]
    public static partial void ImpellerDisplayListRetain(nint displayList);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerDisplayListRelease")]
    public static partial void ImpellerDisplayListRelease(nint displayList);

    // ─── Paint ────────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintNew")]
    public static partial nint ImpellerPaintNew();

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintRetain")]
    public static partial void ImpellerPaintRetain(nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintRelease")]
    public static partial void ImpellerPaintRelease(nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetColor")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetColor(nint paint, ref ImpellerColor color);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetDrawStyle")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetDrawStyle(nint paint, ImpellerDrawStyle style);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetStrokeWidth")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetStrokeWidth(nint paint, float width);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetStrokeCap")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetStrokeCap(nint paint, ImpellerStrokeCap cap);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetStrokeJoin")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetStrokeJoin(nint paint, ImpellerStrokeJoin join);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetStrokeMiter")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetStrokeMiter(nint paint, float miter);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPaintSetBlendMode")]
    [SuppressGCTransition]
    public static partial void ImpellerPaintSetBlendMode(nint paint, ImpellerBlendMode mode);

    // ─── Path & PathBuilder ───────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderNew")]
    public static partial nint ImpellerPathBuilderNew();

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderRetain")]
    public static partial void ImpellerPathBuilderRetain(nint pathBuilder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderRelease")]
    public static partial void ImpellerPathBuilderRelease(nint pathBuilder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderMoveTo")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderMoveTo(nint pathBuilder, ref ImpellerPoint point);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderLineTo")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderLineTo(nint pathBuilder, ref ImpellerPoint point);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderQuadraticCurveTo")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderQuadraticCurveTo(
        nint pathBuilder, ref ImpellerPoint cp, ref ImpellerPoint endPoint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderCubicCurveTo")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderCubicCurveTo(
        nint pathBuilder, ref ImpellerPoint cp1, ref ImpellerPoint cp2, ref ImpellerPoint endPoint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderClose")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderClose(nint pathBuilder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderAddRect")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderAddRect(nint pathBuilder, ref ImpellerRect rect);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderAddOval")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderAddOval(nint pathBuilder, ref ImpellerRect rect);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderAddRoundedRect")]
    [SuppressGCTransition]
    public static partial void ImpellerPathBuilderAddRoundedRect(
        nint pathBuilder, ref ImpellerRoundedRect roundedRect);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderTakePathNew")]
    public static partial nint ImpellerPathBuilderTakePathNew(nint pathBuilder, ImpellerFillType fillType);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathBuilderCopyPathNew")]
    public static partial nint ImpellerPathBuilderCopyPathNew(nint pathBuilder, ImpellerFillType fillType);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathRetain")]
    public static partial void ImpellerPathRetain(nint path);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerPathRelease")]
    public static partial void ImpellerPathRelease(nint path);

    // ─── Texture ──────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTextureCreateWithContentsNew")]
    public static partial nint ImpellerTextureCreateWithContentsNew(
        nint context,
        ref ImpellerTextureDescriptor descriptor,
        ref ImpellerMapping contents,
        nint contentsOnReleaseUserData);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTextureRetain")]
    public static partial void ImpellerTextureRetain(nint texture);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTextureRelease")]
    public static partial void ImpellerTextureRelease(nint texture);

    // ─── Typography Context ────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTypographyContextNew")]
    public static partial nint ImpellerTypographyContextNew();

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTypographyContextRetain")]
    public static partial void ImpellerTypographyContextRetain(nint context);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTypographyContextRelease")]
    public static partial void ImpellerTypographyContextRelease(nint context);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTypographyContextRegisterFont")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ImpellerTypographyContextRegisterFont(
        nint context,
        ref ImpellerMapping contents,
        nint contentsOnReleaseUserData,
        nint familyNameAlias);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerTypographyContextRegisterFont",
        StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool ImpellerTypographyContextRegisterFontWithAlias(
        nint context,
        ref ImpellerMapping contents,
        nint contentsOnReleaseUserData,
        string familyNameAlias);

    // ─── Paragraph Style ──────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleNew")]
    public static partial nint ImpellerParagraphStyleNew();

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleRelease")]
    public static partial void ImpellerParagraphStyleRelease(nint paragraphStyle);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetForeground")]
    public static partial void ImpellerParagraphStyleSetForeground(nint paragraphStyle, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetBackground")]
    public static partial void ImpellerParagraphStyleSetBackground(nint paragraphStyle, nint paint);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetFontWeight")]
    public static partial void ImpellerParagraphStyleSetFontWeight(nint paragraphStyle, ImpellerFontWeight weight);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetFontStyle")]
    public static partial void ImpellerParagraphStyleSetFontStyle(nint paragraphStyle, ImpellerFontStyle style);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetFontFamily",
        StringMarshalling = StringMarshalling.Utf8)]
    public static partial void ImpellerParagraphStyleSetFontFamily(nint paragraphStyle, string familyName);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetFontSize")]
    public static partial void ImpellerParagraphStyleSetFontSize(nint paragraphStyle, float size);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetHeight")]
    public static partial void ImpellerParagraphStyleSetHeight(nint paragraphStyle, float height);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetTextAlignment")]
    public static partial void ImpellerParagraphStyleSetTextAlignment(nint paragraphStyle, ImpellerTextAlignment align);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetTextDirection")]
    public static partial void ImpellerParagraphStyleSetTextDirection(nint paragraphStyle, ImpellerTextDirection direction);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphStyleSetMaxLines")]
    public static partial void ImpellerParagraphStyleSetMaxLines(nint paragraphStyle, uint maxLines);

    // ─── Paragraph Builder ────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderNew")]
    public static partial nint ImpellerParagraphBuilderNew(nint typographyContext);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderRelease")]
    public static partial void ImpellerParagraphBuilderRelease(nint paragraphBuilder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderPushStyle")]
    public static partial void ImpellerParagraphBuilderPushStyle(nint paragraphBuilder, nint style);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderPopStyle")]
    public static partial void ImpellerParagraphBuilderPopStyle(nint paragraphBuilder);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderAddText")]
    public static partial void ImpellerParagraphBuilderAddText(nint paragraphBuilder, nint data, uint length);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphBuilderBuildParagraphNew")]
    public static partial nint ImpellerParagraphBuilderBuildParagraphNew(nint paragraphBuilder, float width);

    // ─── Paragraph ────────────────────────────────────────────────────────

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphRetain")]
    public static partial void ImpellerParagraphRetain(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphRelease")]
    public static partial void ImpellerParagraphRelease(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphGetHeight")]
    public static partial float ImpellerParagraphGetHeight(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphGetMaxWidth")]
    public static partial float ImpellerParagraphGetMaxWidth(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphGetAlphabeticBaseline")]
    public static partial float ImpellerParagraphGetAlphabeticBaseline(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphGetLongestLineWidth")]
    public static partial float ImpellerParagraphGetLongestLineWidth(nint paragraph);

    [LibraryImport(ImpellerLib, EntryPoint = "ImpellerParagraphGetMaxIntrinsicWidth")]
    public static partial float ImpellerParagraphGetMaxIntrinsicWidth(nint paragraph);
}
