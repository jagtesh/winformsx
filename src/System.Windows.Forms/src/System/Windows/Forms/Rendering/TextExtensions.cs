// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;
using System.Windows.Forms.Internal;

namespace System.Windows.Forms;

internal static class TextExtensions
{
    // The value of the ItalicPaddingFactor comes from several tests using different fonts & drawing
    // flags and some benchmarking with GDI+.
    private const float ItalicPaddingFactor = 1 / 2f;

    // Used to clear TextRenderer specific flags from TextFormatFlags
    internal const int GdiUnsupportedFlagMask = unchecked((int)0xFF000000);

    [Conditional("DEBUG")]
    private static void ValidateFlags(DRAW_TEXT_FORMAT flags)
    {
        Debug.Assert(((uint)flags & GdiUnsupportedFlagMask) == 0,
            "Some custom flags were left over and are not GDI compliant!");
    }

    private static (DRAW_TEXT_FORMAT Flags, TextPaddingOptions Padding) SplitTextFormatFlags(TextFormatFlags flags)
    {
        if (((uint)flags & GdiUnsupportedFlagMask) == 0)
        {
            return ((DRAW_TEXT_FORMAT)flags, TextPaddingOptions.GlyphOverhangPadding);
        }

        // Clear TextRenderer custom flags.
        DRAW_TEXT_FORMAT windowsGraphicsSupportedFlags = (DRAW_TEXT_FORMAT)((uint)flags & ~GdiUnsupportedFlagMask);

        TextPaddingOptions padding = flags.HasFlag(TextFormatFlags.LeftAndRightPadding)
            ? TextPaddingOptions.LeftAndRightPadding
            : flags.HasFlag(TextFormatFlags.NoPadding)
                ? TextPaddingOptions.NoPadding
                : TextPaddingOptions.GlyphOverhangPadding;

        return (windowsGraphicsSupportedFlags, padding);
    }

    /// <summary>
    ///  Draws the given <paramref name="text"/> text in the given <paramref name="hdc"/>.
    /// </summary>
    /// <param name="backColor">If <see cref="Color.Empty"/>, the hdc current background color is used.</param>
    /// <param name="foreColor">If <see cref="Color.Empty"/>, the hdc current foreground color is used.</param>
    public static unsafe void DrawText(
        this HDC hdc,
        ReadOnlySpan<char> text,
        FontCache.Scope font,
        Rectangle bounds,
        Color foreColor,
        TextFormatFlags flags,
        Color backColor = default)
    {
        if (text.IsEmpty || foreColor == Color.Transparent)
        {
            return;
        }

        (DRAW_TEXT_FORMAT dt, TextPaddingOptions padding) = SplitTextFormatFlags(flags);

        DRAWTEXTPARAMS dtparams = GetTextMargins(font, padding);

        bounds = AdjustForVerticalAlignment(hdc, text, bounds, dt, &dtparams);

        // Adjust unbounded rect to avoid overflow.
        if (bounds.Width == int.MaxValue)
        {
            bounds.Width -= bounds.X;
        }

        if (bounds.Height == int.MaxValue)
        {
            bounds.Height -= bounds.Y;
        }

        using Graphics graphics = hdc.CreateGraphics();
        if (!backColor.IsEmpty && backColor != Color.Transparent)
        {
            using SolidBrush backgroundBrush = new(backColor);
            graphics.FillRectangle(backgroundBrush, bounds);
        }

        using SolidBrush textBrush = new(foreColor.IsEmpty ? SystemColors.ControlText : foreColor);
        using StringFormat stringFormat = CreateStringFormat(dt);
        graphics.DrawString(text.ToString(), font.Data.Font.TryGetTarget(out Font? targetFont) ? targetFont : SystemFonts.DefaultFont, textBrush, bounds, stringFormat);
    }

    /// <summary>
    ///  Get the bounding box internal text padding to be used when drawing text.
    /// </summary>
    public static DRAWTEXTPARAMS GetTextMargins(
        this FontCache.Scope font,
        TextPaddingOptions padding = default)
    {
        // DrawText(Ex) adds a small space at the beginning of the text bounding box but not at the end,
        // this is more noticeable when the font has the italic style.  We compensate with this factor.

        int leftMargin = 0;
        int rightMargin = 0;
        float overhangPadding;

        switch (padding)
        {
            case TextPaddingOptions.GlyphOverhangPadding:
                // [overhang padding][Text][overhang padding][italic padding]
                overhangPadding = font.Data.Height / 6f;
                leftMargin = (int)Math.Ceiling(overhangPadding);
                rightMargin = (int)Math.Ceiling(overhangPadding * (1 + ItalicPaddingFactor));
                break;

            case TextPaddingOptions.LeftAndRightPadding:
                // [2 * overhang padding][Text][2 * overhang padding][italic padding]
                overhangPadding = font.Data.Height / 6f;
                leftMargin = (int)Math.Ceiling(2 * overhangPadding);
                rightMargin = (int)Math.Ceiling(overhangPadding * (2 + ItalicPaddingFactor));
                break;

            case TextPaddingOptions.NoPadding:
            default:
                break;
        }

        return new DRAWTEXTPARAMS
        {
            iLeftMargin = leftMargin,
            iRightMargin = rightMargin
        };
    }

    /// <summary>
    ///  Adjusts <paramref name="bounds"/> to allow for vertical alignment.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The GDI DrawText does not do multiline alignment when User32.DT.SINGLELINE is not set. This
    ///   adjustment is to workaround that limitation.
    ///  </para>
    /// </remarks>
    public static unsafe Rectangle AdjustForVerticalAlignment(
        this HDC hdc,
        ReadOnlySpan<char> text,
        Rectangle bounds,
        DRAW_TEXT_FORMAT flags,
        DRAWTEXTPARAMS* dtparams)
    {
        ValidateFlags(flags);

        // No need to do anything if TOP (Cannot test DT_TOP because it is 0), single line text or measuring text.
        bool isTop = !flags.HasFlag(DRAW_TEXT_FORMAT.DT_BOTTOM) && !flags.HasFlag(DRAW_TEXT_FORMAT.DT_VCENTER);
        if (isTop || flags.HasFlag(DRAW_TEXT_FORMAT.DT_SINGLELINE) || flags.HasFlag(DRAW_TEXT_FORMAT.DT_CALCRECT))
        {
            return bounds;
        }

        Size textSize = MeasureTextManaged(
            text,
            bounds.Size,
            FontStyle.Regular,
            flags);
        int textHeight = textSize.Height;

        // If the text does not fit inside the bounds then return the bounds that were passed in.
        // This way we paint the top of the text at the top of the bounds passed in.
        if (textHeight > bounds.Height)
        {
            return bounds;
        }

        Rectangle adjustedBounds = bounds;

        if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_VCENTER))
        {
            // Middle
            adjustedBounds.Y = adjustedBounds.Top + adjustedBounds.Height / 2 - textHeight / 2;
        }
        else
        {
            // Bottom
            adjustedBounds.Y = adjustedBounds.Bottom - textHeight;
        }

        return adjustedBounds;
    }

    /// <summary>
    ///  Returns the bounds in logical units of the given <paramref name="text"/>.
    /// </summary>
    /// <param name="proposedSize">
    ///  <para>
    ///   The desired bounds. It will be modified as follows:
    ///  </para>
    ///  <list type="bullet">
    ///   <item><description>The base is extended to fit multiple lines of text.</description></item>
    ///   <item><description>The width is extended to fit the largest word.</description></item>
    ///   <item><description>The width is reduced if the text is smaller than the requested width.</description></item>
    ///   <item><description>The width is extended to fit a single line of text.</description></item>
    ///  </list>
    /// </param>
    public static unsafe Size MeasureText(
        this HDC hdc,
        ReadOnlySpan<char> text,
        FontCache.Scope font,
        Size proposedSize,
        TextFormatFlags flags)
    {
        (DRAW_TEXT_FORMAT dt, TextPaddingOptions padding) = SplitTextFormatFlags(flags);

        if (text.IsEmpty)
        {
            return Size.Empty;
        }

        // DrawText returns a rectangle useful for aligning, but not guaranteed to encompass all
        // pixels (its not a FitBlackBox, if the text is italicized, it will overhang on the right.)
        // So we need to account for this.

        DRAWTEXTPARAMS dtparams = GetTextMargins(font, padding);

        // If Width / Height are < 0, we need to make them larger or DrawText will return
        // an unbounded measurement when we actually trying to make it very narrow.
        int minWidth = 1 + dtparams.iLeftMargin + dtparams.iRightMargin;

        if (proposedSize.Width <= minWidth)
        {
            proposedSize.Width = minWidth;
        }

        if (proposedSize.Height <= 0)
        {
            proposedSize.Height = 1;
        }

        // If proposedSize.Height == int.MaxValue it is assumed bounds are needed. If flags contain SINGLELINE and
        // VCENTER or BOTTOM options, DrawTextEx does not bind the rectangle to the actual text height since
        // it assumes the text is to be vertically aligned; we need to clear the VCENTER and BOTTOM flags to
        // get the actual text bounds.
        if (proposedSize.Height == int.MaxValue && dt.HasFlag(DRAW_TEXT_FORMAT.DT_SINGLELINE))
        {
            // Clear vertical-alignment flags.
            dt &= ~(DRAW_TEXT_FORMAT.DT_BOTTOM | DRAW_TEXT_FORMAT.DT_VCENTER);
        }

        if (proposedSize.Width == int.MaxValue)
        {
            // If there is no constraining width, there should be no need to calculate word breaks.
            dt &= ~(DRAW_TEXT_FORMAT.DT_WORDBREAK);
        }

        dt |= DRAW_TEXT_FORMAT.DT_CALCRECT;
        return MeasureTextManaged(
            text,
            proposedSize,
            font.Data.Font.TryGetTarget(out Font? targetFont) ? targetFont.Style : FontStyle.Regular,
            dt,
            dtparams.iLeftMargin + dtparams.iRightMargin);
    }

    /// <summary>
    ///  Returns the dimensions the of the given <paramref name="text"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This method is used to get the size in logical units of a line of text; it uses GetTextExtentPoint32 function
    ///   which computes the width and height of the text ignoring TAB\CR\LF characters.
    ///  </para>
    ///  <para>
    ///   A text extent is the distance between the beginning of the space and a character that will fit in the space.
    ///  </para>
    /// </remarks>
    public static unsafe Size GetTextExtent(this HDC hdc, string? text, HFONT hfont)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Empty;
        }

        return MeasureTextManaged(text, Size.Empty, SystemFonts.DefaultFont.Style, default);
    }

    private static StringFormat CreateStringFormat(DRAW_TEXT_FORMAT flags)
    {
        StringFormat stringFormat = new(StringFormat.GenericTypographic);

        if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_RIGHT))
        {
            stringFormat.Alignment = StringAlignment.Far;
        }
        else if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_CENTER))
        {
            stringFormat.Alignment = StringAlignment.Center;
        }

        if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_BOTTOM))
        {
            stringFormat.LineAlignment = StringAlignment.Far;
        }
        else if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_VCENTER))
        {
            stringFormat.LineAlignment = StringAlignment.Center;
        }

        if (!flags.HasFlag(DRAW_TEXT_FORMAT.DT_WORDBREAK) || flags.HasFlag(DRAW_TEXT_FORMAT.DT_SINGLELINE))
        {
            stringFormat.FormatFlags |= StringFormatFlags.NoWrap;
        }

        if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_END_ELLIPSIS))
        {
            stringFormat.Trimming = StringTrimming.EllipsisCharacter;
        }
        else if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_PATH_ELLIPSIS))
        {
            stringFormat.Trimming = StringTrimming.EllipsisPath;
        }
        else if (flags.HasFlag(DRAW_TEXT_FORMAT.DT_WORD_ELLIPSIS))
        {
            stringFormat.Trimming = StringTrimming.EllipsisWord;
        }

        return stringFormat;
    }

    private static Size MeasureTextManaged(
        ReadOnlySpan<char> text,
        Size proposedSize,
        FontStyle style,
        DRAW_TEXT_FORMAT flags,
        int horizontalPadding = 0)
    {
        float lineHeight = MathF.Max(1, SystemFonts.DefaultFont.GetHeight(ScaleHelper.InitialSystemDpi));
        float averageGlyphWidth = MathF.Max(1, SystemFonts.DefaultFont.SizeInPoints * ScaleHelper.InitialSystemDpi / 72f * 0.55f);
        if (style.HasFlag(FontStyle.Bold))
        {
            averageGlyphWidth *= 1.08f;
        }

        int lineCount = 1;
        int longestLineLength = 0;
        int currentLineLength = 0;

        foreach (char c in text)
        {
            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                longestLineLength = Math.Max(longestLineLength, currentLineLength);
                currentLineLength = 0;
                lineCount++;
                continue;
            }

            currentLineLength++;
        }

        longestLineLength = Math.Max(longestLineLength, currentLineLength);
        bool canWrap = flags.HasFlag(DRAW_TEXT_FORMAT.DT_WORDBREAK)
            && !flags.HasFlag(DRAW_TEXT_FORMAT.DT_SINGLELINE)
            && proposedSize.Width > 0
            && proposedSize.Width < int.MaxValue;

        if (canWrap)
        {
            int charactersPerLine = Math.Max(1, (int)MathF.Floor((proposedSize.Width - horizontalPadding) / averageGlyphWidth));
            int wrappedLineCount = (int)MathF.Ceiling((float)Math.Max(1, longestLineLength) / charactersPerLine);
            lineCount = Math.Max(lineCount, wrappedLineCount);
            longestLineLength = Math.Min(longestLineLength, charactersPerLine);
        }

        int width = (int)MathF.Ceiling(longestLineLength * averageGlyphWidth) + horizontalPadding;
        int height = (int)MathF.Ceiling(lineCount * lineHeight);

        if (proposedSize.Width > 0 && proposedSize.Width < int.MaxValue && (canWrap || flags.HasFlag(DRAW_TEXT_FORMAT.DT_END_ELLIPSIS)))
        {
            width = Math.Min(width, proposedSize.Width);
        }

        if (proposedSize.Height > 0 && proposedSize.Height < int.MaxValue && flags.HasFlag(DRAW_TEXT_FORMAT.DT_SINGLELINE))
        {
            height = Math.Min(height, proposedSize.Height);
        }

        return new Size(Math.Max(1, width), Math.Max(1, height));
    }
}
