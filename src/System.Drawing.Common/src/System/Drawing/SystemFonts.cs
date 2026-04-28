// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Interop;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Drawing;

public static class SystemFonts
{
    public static Font? GetFontByName(string systemFontName)
    {
        if (nameof(CaptionFont).Equals(systemFontName))
        {
            return CaptionFont;
        }
        else if (nameof(DefaultFont).Equals(systemFontName))
        {
            return DefaultFont;
        }
        else if (nameof(DialogFont).Equals(systemFontName))
        {
            return DialogFont;
        }
        else if (nameof(IconTitleFont).Equals(systemFontName))
        {
            return IconTitleFont;
        }
        else if (nameof(MenuFont).Equals(systemFontName))
        {
            return MenuFont;
        }
        else if (nameof(MessageBoxFont).Equals(systemFontName))
        {
            return MessageBoxFont;
        }
        else if (nameof(SmallCaptionFont).Equals(systemFontName))
        {
            return SmallCaptionFont;
        }
        else if (nameof(StatusFont).Equals(systemFontName))
        {
            return StatusFont;
        }

        return null;
    }

    private static unsafe bool GetNonClientMetrics(out NONCLIENTMETRICSW metrics)
    {
        metrics = new NONCLIENTMETRICSW { cbSize = (uint)sizeof(NONCLIENTMETRICSW) };
        return false;
    }

    public static Font? CaptionFont
    {
        get
        {
            Font? captionFont = null;

            if (GetNonClientMetrics(out NONCLIENTMETRICSW metrics))
            {
                captionFont = GetFontFromData(in metrics.lfCaptionFont);
                captionFont.SetSystemFontName(nameof(CaptionFont));
            }

            return captionFont;
        }
    }

    public static Font? SmallCaptionFont
    {
        get
        {
            Font? smcaptionFont = null;

            if (GetNonClientMetrics(out NONCLIENTMETRICSW metrics))
            {
                smcaptionFont = GetFontFromData(in metrics.lfSmCaptionFont);
                smcaptionFont.SetSystemFontName(nameof(SmallCaptionFont));
            }

            return smcaptionFont;
        }
    }

    public static Font? MenuFont
    {
        get
        {
            Font? menuFont = null;

            if (GetNonClientMetrics(out NONCLIENTMETRICSW metrics))
            {
                menuFont = GetFontFromData(metrics.lfMenuFont);
                menuFont.SetSystemFontName(nameof(MenuFont));
            }

            return menuFont;
        }
    }

    public static Font? StatusFont
    {
        get
        {
            Font? statusFont = null;

            if (GetNonClientMetrics(out NONCLIENTMETRICSW metrics))
            {
                statusFont = GetFontFromData(metrics.lfStatusFont);
                statusFont.SetSystemFontName(nameof(StatusFont));
            }

            return statusFont;
        }
    }

    public static Font? MessageBoxFont
    {
        get
        {
            Font? messageBoxFont = null;

            if (GetNonClientMetrics(out NONCLIENTMETRICSW metrics))
            {
                messageBoxFont = GetFontFromData(metrics.lfMessageFont);
                messageBoxFont.SetSystemFontName(nameof(MessageBoxFont));
            }

            return messageBoxFont ?? DefaultFont;
        }
    }

    private static bool IsCriticalFontException(Exception ex) =>
        // In any of these cases we'll handle the exception.
        ex is not (ExternalException
            or ArgumentException
            // GDI+ throws this one for many reasons other than actual OOM.
            or OutOfMemoryException
            or InvalidOperationException
            or NotImplementedException
            or FileNotFoundException);

    public static Font? IconTitleFont
    {
        get
        {
            Font iconTitleFont = FontInPoints(DefaultFont);
            iconTitleFont.SetSystemFontName(nameof(IconTitleFont));
            return iconTitleFont;
        }
    }

    public static Font DefaultFont
    {
        get
        {
            Font? defaultFont = null;

            defaultFont = new Font(FontFamily.GenericSansSerif, 8);

            if (defaultFont.Unit != GraphicsUnit.Point)
            {
                defaultFont = FontInPoints(defaultFont);
            }

            Debug.Assert(defaultFont is not null, "defaultFont wasn't set.");

            defaultFont.SetSystemFontName(nameof(DefaultFont));
            return defaultFont;
        }
    }

    public static Font DialogFont
    {
        get
        {
            Font? dialogFont = null;

            try
            {
                // Preserve WinForms' logical dialog font contract without querying host Windows APIs.
                dialogFont = new Font("MS Shell Dlg 2", 8);
            }
            catch (ArgumentException)
            {
            }

            if (dialogFont is null)
            {
                dialogFont = DefaultFont;
            }
            else if (dialogFont.Unit != GraphicsUnit.Point)
            {
                dialogFont = FontInPoints(dialogFont);
            }

            // For Japanese cultures, SystemFonts.DefaultFont returns a new Font object every time it is invoked.
            // So for Japanese we return the DefaultFont with its SystemFontName set to DialogFont.
            dialogFont!.SetSystemFontName(nameof(DialogFont));
            return dialogFont;
        }
    }

    private static Font FontInPoints(Font font)
    {
        return new Font(font.FontFamily, font.SizeInPoints, font.Style, GraphicsUnit.Point, font.GdiCharSet, font.GdiVerticalFont);
    }

    private static Font GetFontFromData(in LOGFONTW logFont) =>
        GetFontFromData(Unsafe.As<LOGFONTW, LOGFONT>(ref Unsafe.AsRef(in logFont)));

    private static Font GetFontFromData(in LOGFONT logFont)
    {
        Font? font = null;
        try
        {
            font = Font.FromLogFont(in logFont);
        }
        catch (Exception ex) when (!IsCriticalFontException(ex)) { }

        return font is null
            ? DefaultFont
            : font.Unit != GraphicsUnit.Point ? FontInPoints(font) : font;
    }
}
