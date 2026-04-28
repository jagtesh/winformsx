// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET9_0_OR_GREATER
using System.ComponentModel;
#endif

namespace System.Drawing.Imaging;

/// <summary>
///  Contains information about how image colors are manipulated during rendering.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public sealed unsafe class ImageAttributes : ICloneable, IDisposable
{
    private const int ColorMapStackSpace = 32;
    private readonly Dictionary<ColorAdjustType, AdjustmentState> _states = [];
    private bool _disposed;

    internal GpImageAttributes* _nativeImageAttributes => null;

    internal void SetNativeImageAttributes(GpImageAttributes* handle)
    {
        if (handle is null)
        {
            throw new ArgumentNullException(nameof(handle));
        }

        WinFormsXCompatibilityWarning.Once(
            "System.Drawing.Imaging.ImageAttributes.NativeGdiPlusHandle",
            "Native GDI+ image-attribute handles are ignored in WinFormsX; managed ImageAttributes state is used instead.");
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='ImageAttributes'/> class.
    /// </summary>
    public ImageAttributes()
    {
    }

    internal ImageAttributes(GpImageAttributes* newNativeImageAttributes)
        => SetNativeImageAttributes(newNativeImageAttributes);

    private ImageAttributes(ImageAttributes source)
    {
        foreach ((ColorAdjustType type, AdjustmentState state) in source._states)
        {
            _states[type] = state.Clone();
        }
    }

    /// <summary>
    ///  Cleans up resources for this <see cref='ImageAttributes'/>.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _states.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///  Cleans up resources for this <see cref='ImageAttributes'/>.
    /// </summary>
    ~ImageAttributes() => Dispose();

    /// <summary>
    ///  Creates an exact copy of this <see cref='ImageAttributes'/>.
    /// </summary>
    public object Clone()
    {
        ThrowIfDisposed();
        return new ImageAttributes(this);
    }

    internal ColorMatrix? GetColorMatrix(ColorAdjustType type)
        => _states.TryGetValue(type, out AdjustmentState? state) ? state.ColorMatrix : null;

    public void SetColorMatrix(ColorMatrix newColorMatrix) =>
        SetColorMatrix(newColorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag flags) =>
        SetColorMatrix(newColorMatrix, flags, ColorAdjustType.Default);

    public void SetColorMatrix(ColorMatrix newColorMatrix, ColorMatrixFlag mode, ColorAdjustType type) =>
        SetColorMatrices(newColorMatrix, null, mode, type);

    public void ClearColorMatrix() => ClearColorMatrix(ColorAdjustType.Default);

    public void ClearColorMatrix(ColorAdjustType type)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        GetOrCreateState(type).ColorMatrix = null;
        GetOrCreateState(type).GrayMatrix = null;
    }

    public void SetColorMatrices(ColorMatrix newColorMatrix, ColorMatrix? grayMatrix) =>
        SetColorMatrices(newColorMatrix, grayMatrix, ColorMatrixFlag.Default, ColorAdjustType.Default);

    public void SetColorMatrices(ColorMatrix newColorMatrix, ColorMatrix? grayMatrix, ColorMatrixFlag flags) =>
        SetColorMatrices(newColorMatrix, grayMatrix, flags, ColorAdjustType.Default);

    public void SetColorMatrices(
        ColorMatrix newColorMatrix,
        ColorMatrix? grayMatrix,
        ColorMatrixFlag mode,
        ColorAdjustType type)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(newColorMatrix);
        ValidateColorAdjustType(type);
        ValidateColorMatrixFlag(mode, allowAltGrays: grayMatrix is not null);

        AdjustmentState state = GetOrCreateState(type);
        state.ColorMatrix = CloneColorMatrix(newColorMatrix);
        state.GrayMatrix = grayMatrix is null ? null : CloneColorMatrix(grayMatrix);
        state.ColorMatrixFlag = mode;
    }

    public void SetThreshold(float threshold) => SetThreshold(threshold, ColorAdjustType.Default);

    public void SetThreshold(float threshold, ColorAdjustType type) => SetThreshold(threshold, type, enableFlag: true);

    public void ClearThreshold() => ClearThreshold(ColorAdjustType.Default);

    public void ClearThreshold(ColorAdjustType type) => SetThreshold(0.0f, type, enableFlag: false);

    private void SetThreshold(float threshold, ColorAdjustType type, bool enableFlag)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        AdjustmentState state = GetOrCreateState(type);
        state.Threshold = enableFlag ? threshold : null;
    }

    public void SetGamma(float gamma) => SetGamma(gamma, ColorAdjustType.Default);

    public void SetGamma(float gamma, ColorAdjustType type) => SetGamma(gamma, type, enableFlag: true);

    public void ClearGamma() => ClearGamma(ColorAdjustType.Default);

    public void ClearGamma(ColorAdjustType type) => SetGamma(0.0f, type, enableFlag: false);

    private void SetGamma(float gamma, ColorAdjustType type, bool enableFlag)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        AdjustmentState state = GetOrCreateState(type);
        state.Gamma = enableFlag ? gamma : null;
    }

    public void SetNoOp() => SetNoOp(ColorAdjustType.Default);

    public void SetNoOp(ColorAdjustType type) => SetNoOp(type, enableFlag: true);

    public void ClearNoOp() => ClearNoOp(ColorAdjustType.Default);

    public void ClearNoOp(ColorAdjustType type) => SetNoOp(type, enableFlag: false);

    private void SetNoOp(ColorAdjustType type, bool enableFlag)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        GetOrCreateState(type).NoOp = enableFlag;
    }

    public void SetColorKey(Color colorLow, Color colorHigh) =>
        SetColorKey(colorLow, colorHigh, ColorAdjustType.Default);

    public void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type) =>
        SetColorKey(colorLow, colorHigh, type, enableFlag: true);

    public void ClearColorKey() => ClearColorKey(ColorAdjustType.Default);

    public void ClearColorKey(ColorAdjustType type) => SetColorKey(Color.Empty, Color.Empty, type, enableFlag: false);

    private void SetColorKey(Color colorLow, Color colorHigh, ColorAdjustType type, bool enableFlag)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        AdjustmentState state = GetOrCreateState(type);
        state.ColorKey = enableFlag ? (colorLow, colorHigh) : null;
    }

    public void SetOutputChannel(ColorChannelFlag flags) => SetOutputChannel(flags, ColorAdjustType.Default);

    public void SetOutputChannel(ColorChannelFlag flags, ColorAdjustType type) =>
        SetOutputChannel(type, flags, enableFlag: true);

    public void ClearOutputChannel() => ClearOutputChannel(ColorAdjustType.Default);

    public void ClearOutputChannel(ColorAdjustType type) =>
        SetOutputChannel(type, ColorChannelFlag.ColorChannelLast, enableFlag: false);

    private void SetOutputChannel(ColorAdjustType type, ColorChannelFlag flags, bool enableFlag)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        ValidateColorChannelFlag(flags);
        AdjustmentState state = GetOrCreateState(type);
        state.OutputChannel = enableFlag ? flags : null;
    }

    public void SetOutputChannelColorProfile(string colorProfileFilename) =>
        SetOutputChannelColorProfile(colorProfileFilename, ColorAdjustType.Default);

    public void SetOutputChannelColorProfile(string colorProfileFilename, ColorAdjustType type)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);

        string fullPath = Path.GetFullPath(colorProfileFilename);
        GetOrCreateState(type).OutputChannelColorProfile = fullPath;
    }

    public void ClearOutputChannelColorProfile() => ClearOutputChannel(ColorAdjustType.Default);

    public void ClearOutputChannelColorProfile(ColorAdjustType type)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        GetOrCreateState(type).OutputChannelColorProfile = null;
    }

    /// <inheritdoc cref="SetRemapTable(ColorMap[], ColorAdjustType)"/>
    public void SetRemapTable(params ColorMap[] map) => SetRemapTable(map, ColorAdjustType.Default);

    /// <summary>
    ///  Sets the default color-remap table.
    /// </summary>
    /// <inheritdoc cref="SetRemapTable(ColorAdjustType, ReadOnlySpan{ColorMap})"/>
#if NET9_0_OR_GREATER
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public void SetRemapTable(ColorMap[] map, ColorAdjustType type)
    {
        ArgumentNullException.ThrowIfNull(map);
        SetRemapTable(type, map);
    }

#if NET9_0_OR_GREATER
    /// <inheritdoc cref="SetRemapTable(ColorMap[], ColorAdjustType)"/>
    public void SetRemapTable(params ReadOnlySpan<ColorMap> map) => SetRemapTable(ColorAdjustType.Default, map);

    /// <inheritdoc cref="SetRemapTable(ColorMap[], ColorAdjustType)"/>
    public void SetRemapTable(params ReadOnlySpan<(Color OldColor, Color NewColor)> map) => SetRemapTable(ColorAdjustType.Default, map);
#endif

#if NET9_0_OR_GREATER
    public
#else
    private
#endif
    void SetRemapTable(ColorAdjustType type, params ReadOnlySpan<ColorMap> map)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);

        ColorMap[] copy = new ColorMap[map.Length];
        map.CopyTo(copy);
        GetOrCreateState(type).RemapTable = copy;
    }

#if NET9_0_OR_GREATER
    public void SetRemapTable(ColorAdjustType type, params ReadOnlySpan<(Color OldColor, Color NewColor)> map)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);

        ColorMap[] copy = new ColorMap[map.Length];
        for (int i = 0; i < map.Length; i++)
        {
            copy[i] = new ColorMap { OldColor = map[i].OldColor, NewColor = map[i].NewColor };
        }

        GetOrCreateState(type).RemapTable = copy;
    }
#endif

    [InlineArray(ColorMapStackSpace)]
    private struct StackBuffer
    {
        internal (ARGB, ARGB) _element0;
    }

    public void ClearRemapTable() => ClearRemapTable(ColorAdjustType.Default);

    public void ClearRemapTable(ColorAdjustType type)
    {
        ThrowIfDisposed();
        ValidateColorAdjustType(type);
        GetOrCreateState(type).RemapTable = null;
    }

    public void SetBrushRemapTable(params ColorMap[] map) => SetRemapTable(map, ColorAdjustType.Brush);

#if NET9_0_OR_GREATER
    public void SetBrushRemapTable(params ReadOnlySpan<ColorMap> map) => SetRemapTable(ColorAdjustType.Brush, map);

    public void SetBrushRemapTable(params ReadOnlySpan<(Color OldColor, Color NewColor)> map) => SetRemapTable(ColorAdjustType.Brush, map);
#endif

    public void ClearBrushRemapTable() => ClearRemapTable(ColorAdjustType.Brush);

    public void SetWrapMode(Drawing2D.WrapMode mode) => SetWrapMode(mode, default, clamp: false);

    public void SetWrapMode(Drawing2D.WrapMode mode, Color color) => SetWrapMode(mode, color, clamp: false);

    public void SetWrapMode(Drawing2D.WrapMode mode, Color color, bool clamp)
    {
        ThrowIfDisposed();
        ValidateWrapMode(mode);

        AdjustmentState state = GetOrCreateState(ColorAdjustType.Default);
        state.WrapMode = mode;
        state.WrapColor = color;
        state.Clamp = clamp;
    }

    public void GetAdjustedPalette(ColorPalette palette, ColorAdjustType type)
    {
        ThrowIfDisposed();

        // Match GDI+ null behavior for this API.
        _ = palette.Entries;
        ValidateColorAdjustType(type);
    }

    private AdjustmentState GetOrCreateState(ColorAdjustType type)
    {
        if (!_states.TryGetValue(type, out AdjustmentState? state))
        {
            state = new AdjustmentState();
            _states[type] = state;
        }

        return state;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static void ValidateColorAdjustType(ColorAdjustType type)
    {
        if (type < ColorAdjustType.Default || type > ColorAdjustType.Text)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static void ValidateColorMatrixFlag(ColorMatrixFlag flag, bool allowAltGrays)
    {
        if (flag < ColorMatrixFlag.Default || flag > ColorMatrixFlag.AltGrays || (!allowAltGrays && flag == ColorMatrixFlag.AltGrays))
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static void ValidateColorChannelFlag(ColorChannelFlag flag)
    {
        if (flag < ColorChannelFlag.ColorChannelC || flag > ColorChannelFlag.ColorChannelLast)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static void ValidateWrapMode(Drawing2D.WrapMode mode)
    {
        if (mode < Drawing2D.WrapMode.Tile || mode > Drawing2D.WrapMode.Clamp)
        {
            throw Status.InvalidParameter.GetException();
        }
    }

    private static ColorMatrix CloneColorMatrix(ColorMatrix matrix)
    {
        float[][] values = new float[5][];
        for (int row = 0; row < 5; row++)
        {
            values[row] = new float[5];
            for (int column = 0; column < 5; column++)
            {
                values[row][column] = matrix[row, column];
            }
        }

        return new ColorMatrix(values);
    }

    private sealed class AdjustmentState
    {
        public ColorMatrix? ColorMatrix { get; set; }
        public ColorMatrix? GrayMatrix { get; set; }
        public ColorMatrixFlag ColorMatrixFlag { get; set; }
        public float? Threshold { get; set; }
        public float? Gamma { get; set; }
        public bool NoOp { get; set; }
        public (Color Low, Color High)? ColorKey { get; set; }
        public ColorChannelFlag? OutputChannel { get; set; }
        public string? OutputChannelColorProfile { get; set; }
        public ColorMap[]? RemapTable { get; set; }
        public Drawing2D.WrapMode WrapMode { get; set; }
        public Color WrapColor { get; set; }
        public bool Clamp { get; set; }

        public AdjustmentState Clone()
            => new()
            {
                ColorMatrix = ColorMatrix is null ? null : CloneColorMatrix(ColorMatrix),
                GrayMatrix = GrayMatrix is null ? null : CloneColorMatrix(GrayMatrix),
                ColorMatrixFlag = ColorMatrixFlag,
                Threshold = Threshold,
                Gamma = Gamma,
                NoOp = NoOp,
                ColorKey = ColorKey,
                OutputChannel = OutputChannel,
                OutputChannelColorProfile = OutputChannelColorProfile,
                RemapTable = RemapTable is null ? null : (ColorMap[])RemapTable.Clone(),
                WrapMode = WrapMode,
                WrapColor = WrapColor,
                Clamp = Clamp,
            };
    }
}
