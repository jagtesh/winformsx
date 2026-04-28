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
    private StringFormatFlags _managedFormatFlags;
    private StringAlignment _managedAlignment = StringAlignment.Near;
    private StringAlignment _managedLineAlignment = StringAlignment.Near;
    private HotkeyPrefix _managedHotkeyPrefix = HotkeyPrefix.None;
    private StringTrimming _managedTrimming = StringTrimming.Character;
    private float _managedFirstTabOffset;
    private float[] _managedTabStops = [];
    private StringDigitSubstitute _managedDigitSubstitute = StringDigitSubstitute.User;
    private int _managedDigitLanguage;
    private CharacterRange[] _managedMeasurableCharacterRanges = [];

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
        _managedDigitLanguage = language;
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
        _managedMeasurableCharacterRanges = (CharacterRange[])format._managedMeasurableCharacterRanges.Clone();
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
        _nativeFormat = null;
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
            return _managedFormatFlags;
        }
        set
        {
            _managedFormatFlags = value;
        }
    }

    /// <summary>
    ///  Sets the measure of characters to the specified range.
    /// </summary>
    public void SetMeasurableCharacterRanges(CharacterRange[] ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        _managedMeasurableCharacterRanges = (CharacterRange[])ranges.Clone();
    }

    /// <summary>
    ///  Specifies text alignment information.
    /// </summary>
    public StringAlignment Alignment
    {
        get
        {
            return _managedAlignment;
        }
        set
        {
            if (value is < StringAlignment.Near or > StringAlignment.Far)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringAlignment));
            }

            _managedAlignment = value;
        }
    }

    /// <summary>
    ///  Gets or sets the line alignment.
    /// </summary>
    public StringAlignment LineAlignment
    {
        get
        {
            return _managedLineAlignment;
        }
        set
        {
            if (value is < 0 or > StringAlignment.Far)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringAlignment));
            }

            _managedLineAlignment = value;
        }
    }

    /// <summary>
    ///  Gets or sets the <see cref='HotkeyPrefix'/> for this <see cref='StringFormat'/> .
    /// </summary>
    public HotkeyPrefix HotkeyPrefix
    {
        get
        {
            return _managedHotkeyPrefix;
        }
        set
        {
            if (value is < HotkeyPrefix.None or > HotkeyPrefix.Hide)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(HotkeyPrefix));
            }

            _managedHotkeyPrefix = value;
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
    }

    /// <summary>
    ///  Gets the tab stops for this <see cref='StringFormat'/>.
    /// </summary>
    public float[] GetTabStops(out float firstTabOffset)
    {
        firstTabOffset = _managedFirstTabOffset;
        return (float[])_managedTabStops.Clone();
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
            return _managedTrimming;
        }
        set
        {
            if (value is < StringTrimming.None or > StringTrimming.EllipsisPath)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(StringTrimming));
            }

            _managedTrimming = value;
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
            return new StringFormat();
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
            StringFormat f = new(StringFormatFlags.LineLimit | StringFormatFlags.NoClip | StringFormatFlags.FitBlackBox);
            f.Trimming = StringTrimming.None;
            return f;
        }
    }

    public void SetDigitSubstitution(int language, StringDigitSubstitute substitute)
    {
        _managedDigitLanguage = language;
        _managedDigitSubstitute = substitute;
    }

    /// <summary>
    ///  Gets the <see cref='StringDigitSubstitute'/> for this <see cref='StringFormat'/>.
    /// </summary>
    public StringDigitSubstitute DigitSubstitutionMethod
    {
        get
        {
            return _managedDigitSubstitute;
        }
    }

    /// <summary>
    ///  Gets the language of <see cref='StringDigitSubstitute'/> for this <see cref='StringFormat'/>.
    /// </summary>
    public int DigitSubstitutionLanguage
    {
        get
        {
            return _managedDigitLanguage;
        }
    }

    internal int GetMeasurableCharacterRangeCount()
    {
        return _managedMeasurableCharacterRanges.Length;
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
