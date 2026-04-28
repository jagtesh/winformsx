// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Drawing.Text;

namespace System.Drawing;

/// <summary>
///  Encapsulates text layout information (such as alignment and linespacing), display manipulations (such as
///  ellipsis insertion and national digit substitution) and OpenType features.
/// </summary>
public sealed unsafe class StringFormat : MarshalByRefObject, ICloneable, IDisposable
{
    internal GpStringFormat* _nativeFormat;
    private readonly bool _backendOnly = Graphics.IsBackendActive;
    private StringFormatFlags _managedFormatFlags;
    private StringAlignment _managedAlignment = StringAlignment.Near;
    private StringAlignment _managedLineAlignment = StringAlignment.Near;
    private HotkeyPrefix _managedHotkeyPrefix = HotkeyPrefix.None;
    private StringTrimming _managedTrimming = StringTrimming.Character;
    private float _managedFirstTabOffset;
    private float[] _managedTabStops = [];
    private StringDigitSubstitute _managedDigitSubstitute = StringDigitSubstitute.User;
    private int _managedDigitLanguage;

    private StringFormat(GpStringFormat* format) => _nativeFormat = format;

    /// <summary>
    ///  Initializes a new instance of the <see cref='StringFormat'/> class.
    /// </summary>
    public StringFormat() : this(0, 0)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='StringFormat'/> class with the specified <see cref='StringFormatFlags'/>.
    /// </summary>
    public StringFormat(StringFormatFlags options) : this(options, 0)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='StringFormat'/> class with the specified
    ///  <see cref='StringFormatFlags'/> and language.
    /// </summary>
    public StringFormat(StringFormatFlags options, int language)
    {
        _managedFormatFlags = options;
        if (!_backendOnly)
        {
            GpStringFormat* format;
            PInvoke.GdipCreateStringFormat((int)options, (ushort)language, &format).ThrowIfFailed();
            _nativeFormat = format;
        }
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref='StringFormat'/> class from the specified
    ///  existing <see cref='StringFormat'/>.
    /// </summary>
    public StringFormat(StringFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        _managedFormatFlags = format._managedFormatFlags;
        _managedAlignment = format._managedAlignment;
        _managedLineAlignment = format._managedLineAlignment;
        _managedHotkeyPrefix = format._managedHotkeyPrefix;
        _managedTrimming = format._managedTrimming;
        _managedFirstTabOffset = format._managedFirstTabOffset;
        _managedTabStops = (float[])format._managedTabStops.Clone();
        _managedDigitSubstitute = format._managedDigitSubstitute;
        _managedDigitLanguage = format._managedDigitLanguage;
        if (!format._backendOnly && format._nativeFormat is not null)
        {
            GpStringFormat* newFormat;
            PInvoke.GdipCloneStringFormat(format._nativeFormat, &newFormat).ThrowIfFailed();
            _nativeFormat = newFormat;
            GC.KeepAlive(format);
        }
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='StringFormat'/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_nativeFormat is not null)
        {
            try
            {
#if DEBUG
                Status status = !Gdip.Initialized ? Status.Ok :
#endif
                PInvoke.GdipDeleteStringFormat(_nativeFormat);
#if DEBUG
                Debug.Assert(status == Status.Ok, $"GDI+ returned an error status: {status}");
#endif
            }
            catch (Exception ex)
            {
                if (ClientUtils.IsCriticalException(ex))
                {
                    throw;
                }

                Debug.Fail($"Exception thrown during Dispose: {ex}");
            }
            finally
            {
                _nativeFormat = null;
            }
        }
    }

    /// <summary>
    ///  Creates an exact copy of this <see cref='StringFormat'/>.
    /// </summary>
    public object Clone() => new StringFormat(this);

    /// <summary>
    ///  Gets or sets a <see cref='StringFormatFlags'/> that contains formatting information.
    /// </summary>
    public StringFormatFlags FormatFlags
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedFormatFlags;
            }

            StringFormatFlags format;
            PInvoke.GdipGetStringFormatFlags(_nativeFormat, (int*)&format).ThrowIfFailed();
            GC.KeepAlive(this);
            return format;
        }
        set
        {
            _managedFormatFlags = value;
            if (_backendOnly || _nativeFormat is null)
            {
                return;
            }

            PInvoke.GdipSetStringFormatFlags(_nativeFormat, (int)value).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Sets the measure of characters to the specified range.
    /// </summary>
    public void SetMeasurableCharacterRanges(CharacterRange[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (_backendOnly || _nativeFormat is null)
        {
            return;
        }

        // Passing no count will clear the ranges, but it still requires a valid pointer. Taking the address of an
        // empty array gives a null pointer, so we need to pass a dummy value.
        GdiPlus.CharacterRange stub;
        fixed (void* r = ranges)
        {
            PInvoke.GdipSetStringFormatMeasurableCharacterRanges(
                _nativeFormat,
                ranges.Length,
                r is null ? &stub : (GdiPlus.CharacterRange*)r).ThrowIfFailed();
        }

        GC.KeepAlive(this);
    }

    /// <summary>
    ///  Specifies text alignment information.
    /// </summary>
    public StringAlignment Alignment
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedAlignment;
            }

            StringAlignment alignment;
            PInvoke.GdipGetStringFormatAlign(_nativeFormat, (GdiPlus.StringAlignment*)&alignment).ThrowIfFailed();
            GC.KeepAlive(this);
            return alignment;
        }
        set
        {
            if (value is < StringAlignment.Near or > StringAlignment.Far)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringAlignment));
            }

            _managedAlignment = value;
            if (_backendOnly || _nativeFormat is null)
            {
                return;
            }

            PInvoke.GdipSetStringFormatAlign(_nativeFormat, (GdiPlus.StringAlignment)value).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Gets or sets the line alignment.
    /// </summary>
    public StringAlignment LineAlignment
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedLineAlignment;
            }

            StringAlignment alignment;
            PInvoke.GdipGetStringFormatLineAlign(_nativeFormat, (GdiPlus.StringAlignment*)&alignment).ThrowIfFailed();
            GC.KeepAlive(this);
            return alignment;
        }
        set
        {
            if (value is < 0 or > StringAlignment.Far)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringAlignment));
            }

            _managedLineAlignment = value;
            if (_backendOnly || _nativeFormat is null)
            {
                return;
            }

            PInvoke.GdipSetStringFormatLineAlign(_nativeFormat, (GdiPlus.StringAlignment)value).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Gets or sets the <see cref='HotkeyPrefix'/> for this <see cref='StringFormat'/> .
    /// </summary>
    public HotkeyPrefix HotkeyPrefix
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedHotkeyPrefix;
            }

            HotkeyPrefix hotkeyPrefix;
            PInvoke.GdipGetStringFormatHotkeyPrefix(_nativeFormat, (int*)&hotkeyPrefix).ThrowIfFailed();
            GC.KeepAlive(this);
            return hotkeyPrefix;
        }
        set
        {
            if (value is < HotkeyPrefix.None or > HotkeyPrefix.Hide)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(HotkeyPrefix));
            }

            _managedHotkeyPrefix = value;
            if (_backendOnly || _nativeFormat is null)
            {
                return;
            }

            PInvoke.GdipSetStringFormatHotkeyPrefix(_nativeFormat, (int)value).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Sets tab stops for this <see cref='StringFormat'/>.
    /// </summary>
    public void SetTabStops(float firstTabOffset, float[] tabStops)
    {
        ArgumentNullException.ThrowIfNull(tabStops);

        if (firstTabOffset < 0)
        {
            throw new ArgumentException(SR.Format(SR.InvalidArgumentValue, nameof(firstTabOffset), firstTabOffset));
        }

        _managedFirstTabOffset = firstTabOffset;
        _managedTabStops = (float[])tabStops.Clone();
        if (_backendOnly || _nativeFormat is null)
        {
            return;
        }

        // To clear the tab stops you need to pass a count of 0 with a valid pointer. Taking the address of an
        // empty array gives a null pointer, so we need to pass a dummy value.
        float stub;
        fixed (float* ts = tabStops)
        {
            PInvoke.GdipSetStringFormatTabStops(
                _nativeFormat,
                firstTabOffset,
                tabStops.Length,
                ts is null ? &stub : ts).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Gets the tab stops for this <see cref='StringFormat'/>.
    /// </summary>
    public float[] GetTabStops(out float firstTabOffset)
    {
        if (_backendOnly || _nativeFormat is null)
        {
            firstTabOffset = _managedFirstTabOffset;
            return (float[])_managedTabStops.Clone();
        }

        int count;
        PInvoke.GdipGetStringFormatTabStopCount(_nativeFormat, &count).ThrowIfFailed();

        if (count == 0)
        {
            firstTabOffset = 0;
            return [];
        }

        float[] tabStops = new float[count];

        fixed (float* fto = &firstTabOffset)
        fixed (float* ts = tabStops)
        {
            PInvoke.GdipGetStringFormatTabStops(_nativeFormat, count, fto, ts).ThrowIfFailed();
            GC.KeepAlive(this);
            return tabStops;
        }
    }

    // String trimming. How to handle more text than can be displayed
    // in the limits available.

    /// <summary>
    ///  Gets or sets the <see cref='StringTrimming'/> for this <see cref='StringFormat'/>.
    /// </summary>
    public StringTrimming Trimming
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedTrimming;
            }

            StringTrimming trimming;
            PInvoke.GdipGetStringFormatTrimming(_nativeFormat, (GdiPlus.StringTrimming*)&trimming).ThrowIfFailed();
            GC.KeepAlive(this);
            return trimming;
        }
        set
        {
            if (value is < StringTrimming.None or > StringTrimming.EllipsisPath)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringTrimming));
            }

            _managedTrimming = value;
            if (_backendOnly || _nativeFormat is null)
            {
                return;
            }

            PInvoke.GdipSetStringFormatTrimming(_nativeFormat, (GdiPlus.StringTrimming)value).ThrowIfFailed();
            GC.KeepAlive(this);
        }
    }

    /// <summary>
    ///  Gets a generic default <see cref='StringFormat'/>.
    /// </summary>
    /// <devdoc>
    ///  Remarks from MSDN: A generic, default StringFormat object has the following characteristics:
    ///
    ///     - No string format flags are set.
    ///     - Character alignment and line alignment are set to StringAlignmentNear.
    ///     - Language ID is set to neutral language, which means that the current language associated with the calling thread is used.
    ///     - String digit substitution is set to StringDigitSubstituteUser.
    ///     - Hot key prefix is set to HotkeyPrefixNone.
    ///     - Number of tab stops is set to zero.
    ///     - String trimming is set to StringTrimmingCharacter.
    /// </devdoc>
    public static StringFormat GenericDefault
    {
        get
        {
            if (Graphics.IsBackendActive)
            {
                return new StringFormat();
            }

            GpStringFormat* format;
            PInvoke.GdipStringFormatGetGenericDefault(&format).ThrowIfFailed();
            return new StringFormat(format);
        }
    }

    /// <summary>
    ///  Gets a generic typographic <see cref='StringFormat'/>.
    /// </summary>
    /// <devdoc>
    ///  Remarks from MSDN: A generic, typographic StringFormat object has the following characteristics:
    ///
    ///     - String format flags StringFormatFlagsLineLimit, StringFormatFlagsNoClip, and StringFormatFlagsNoFitBlackBox are set.
    ///     - Character alignment and line alignment are set to StringAlignmentNear.
    ///     - Language ID is set to neutral language, which means that the current language associated with the calling thread is used.
    ///     - String digit substitution is set to StringDigitSubstituteUser.
    ///     - Hot key prefix is set to HotkeyPrefixNone.
    ///     - Number of tab stops is set to zero.
    ///     - String trimming is set to StringTrimmingNone.
    /// </devdoc>
    public static StringFormat GenericTypographic
    {
        get
        {
            if (Graphics.IsBackendActive)
            {
                StringFormat f = new(StringFormatFlags.LineLimit | StringFormatFlags.NoClip | StringFormatFlags.FitBlackBox);
                f.Trimming = StringTrimming.None;
                return f;
            }

            GpStringFormat* format;
            PInvoke.GdipStringFormatGetGenericTypographic(&format).ThrowIfFailed();
            return new StringFormat(format);
        }
    }

    public void SetDigitSubstitution(int language, StringDigitSubstitute substitute)
    {
        _managedDigitLanguage = language;
        _managedDigitSubstitute = substitute;
        if (_backendOnly || _nativeFormat is null)
        {
            return;
        }

        PInvoke.GdipSetStringFormatDigitSubstitution(
            _nativeFormat,
            (ushort)language,
            (GdiPlus.StringDigitSubstitute)substitute).ThrowIfFailed();

        GC.KeepAlive(this);
    }

    /// <summary>
    ///  Gets the <see cref='StringDigitSubstitute'/> for this <see cref='StringFormat'/>.
    /// </summary>
    public StringDigitSubstitute DigitSubstitutionMethod
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedDigitSubstitute;
            }

            StringDigitSubstitute digitSubstitute;
            PInvoke.GdipGetStringFormatDigitSubstitution(
                _nativeFormat,
                null,
                (GdiPlus.StringDigitSubstitute*)&digitSubstitute).ThrowIfFailed();

            GC.KeepAlive(this);
            return digitSubstitute;
        }
    }

    /// <summary>
    ///  Gets the language of <see cref='StringDigitSubstitute'/> for this <see cref='StringFormat'/>.
    /// </summary>
    public int DigitSubstitutionLanguage
    {
        get
        {
            if (_backendOnly || _nativeFormat is null)
            {
                return _managedDigitLanguage;
            }

            ushort language;
            PInvoke.GdipGetStringFormatDigitSubstitution(_nativeFormat, &language, null).ThrowIfFailed();
            GC.KeepAlive(this);
            return language;
        }
    }

    internal int GetMeasurableCharacterRangeCount()
    {
        if (_backendOnly || _nativeFormat is null)
        {
            return 0;
        }

        int count;
        PInvoke.GdipGetStringFormatMeasurableCharacterRangeCount(_nativeFormat, &count).ThrowIfFailed();
        GC.KeepAlive(this);
        return count;
    }

    /// <summary>
    ///  Cleans up Windows resources for this <see cref='StringFormat'/>.
    /// </summary>
    ~StringFormat() => Dispose(disposing: false);

    /// <summary>
    ///  Converts this <see cref='StringFormat'/> to a human-readable string.
    /// </summary>
    public override string ToString() => $"[StringFormat, FormatFlags={FormatFlags}]";
}
