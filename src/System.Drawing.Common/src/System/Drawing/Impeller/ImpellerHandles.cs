// ImpellerSharp — Strongly typed handles and [LibraryImport] bindings for the Impeller C API.
// This file defines the safe handle wrappers and core interop types.

#pragma warning disable IDE1006
using System.Runtime.InteropServices;

namespace System.Drawing.Impeller;

// ─── Opaque Handle Types ───────────────────────────────────────────────────────

/// <summary>
/// Base class for all Impeller native handles. Uses ref-counting via the C API.
/// </summary>
public abstract class ImpellerHandle : SafeHandle
{
    protected ImpellerHandle() : base(nint.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == nint.Zero;
}

/// <summary>
/// Handle to an Impeller rendering context (Metal, Vulkan, or GLES).
/// </summary>
public sealed class ImpellerContextHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerContextRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to a DisplayListBuilder — the primary drawing command recorder.
/// </summary>
public sealed class ImpellerDisplayListBuilderHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerDisplayListBuilderRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to a finalized DisplayList — an immutable list of drawing commands.
/// </summary>
public sealed class ImpellerDisplayListHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerDisplayListRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller Paint object (fill/stroke style, color, blending).
/// </summary>
public sealed class ImpellerPaintHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerPaintRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller Path (complex geometry).
/// </summary>
public sealed class ImpellerPathHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerPathRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller PathBuilder (used to construct paths).
/// </summary>
public sealed class ImpellerPathBuilderHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerPathBuilderRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller Surface — a renderable target bound to a window.
/// </summary>
public sealed class ImpellerSurfaceHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerSurfaceRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller Texture.
/// </summary>
public sealed class ImpellerTextureHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerTextureRelease(handle);
        return true;
    }
}

/// <summary>
/// Handle to an Impeller Paragraph (shaped + laid out text ready for rendering).
/// </summary>
public sealed class ImpellerParagraphHandle : ImpellerHandle
{
    protected override bool ReleaseHandle()
    {
        NativeMethods.ImpellerParagraphRelease(handle);
        return true;
    }
}
