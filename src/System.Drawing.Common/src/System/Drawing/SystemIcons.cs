// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing;

public static class SystemIcons
{
    private static Icon? s_application;
    private static Icon? s_asterisk;
    private static Icon? s_error;
    private static Icon? s_exclamation;
    private static Icon? s_hand;
    private static Icon? s_information;
    private static Icon? s_question;
    private static Icon? s_warning;
    private static Icon? s_winlogo;
    private static Icon? s_shield;

    public static Icon Application => GetIcon(ref s_application, SystemIconKind.Application);

    public static Icon Asterisk => GetIcon(ref s_asterisk, SystemIconKind.Asterisk);

    public static Icon Error => GetIcon(ref s_error, SystemIconKind.Error);

    public static Icon Exclamation => GetIcon(ref s_exclamation, SystemIconKind.Exclamation);

    public static Icon Hand => GetIcon(ref s_hand, SystemIconKind.Hand);

    public static Icon Information => GetIcon(ref s_information, SystemIconKind.Information);

    public static Icon Question => GetIcon(ref s_question, SystemIconKind.Question);

    public static Icon Warning => GetIcon(ref s_warning, SystemIconKind.Warning);

    public static Icon WinLogo => GetIcon(ref s_winlogo, SystemIconKind.WinLogo);

    public static Icon Shield
    {
        get
        {
            if (s_shield is null)
            {
                s_shield = new Icon(typeof(SystemIcons), "ShieldIcon.ico");
                Debug.Assert(s_shield is not null, "ShieldIcon.ico must be present as an embedded resource in System.Drawing.Common.");
            }

            return s_shield;
        }
    }

    private static Icon GetIcon(ref Icon? icon, SystemIconKind kind) =>
        icon ??= kind switch
        {
            SystemIconKind.Application => CreateManagedIcon(Color.SlateGray, "A"),
            SystemIconKind.Asterisk => CreateManagedIcon(Color.RoyalBlue, "*"),
            SystemIconKind.Error => CreateManagedIcon(Color.Firebrick, "X"),
            SystemIconKind.Exclamation => CreateManagedIcon(Color.DarkGoldenrod, "!"),
            SystemIconKind.Hand => CreateManagedIcon(Color.Firebrick, "X"),
            SystemIconKind.Information => CreateManagedIcon(Color.RoyalBlue, "i"),
            SystemIconKind.Question => CreateManagedIcon(Color.MediumPurple, "?"),
            SystemIconKind.Warning => CreateManagedIcon(Color.DarkGoldenrod, "!"),
            SystemIconKind.WinLogo => CreateManagedIcon(Color.SeaGreen, "W"),
            _ => CreateManagedIcon(Color.SteelBlue, string.Empty),
        };

    private static Icon CreateManagedIcon(Color accent, string glyph, int size = 32)
    {
        size = Math.Max(16, size);
        Bitmap bitmap = new(size, size, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using SolidBrush brush = new(accent);
        int inset = Math.Max(2, size / 8);
        int diameter = size - (inset * 2);
        graphics.FillEllipse(brush, inset, inset, diameter, diameter);
        using Pen pen = new(Color.White, 2);
        graphics.DrawEllipse(pen, inset, inset, diameter, diameter);
        if (!string.IsNullOrEmpty(glyph))
        {
            DrawManagedGlyph(graphics, glyph, size);
        }

        return new Icon(bitmap);
    }

    private static void DrawManagedGlyph(Graphics graphics, string glyph, int size)
    {
        float scale = size / 32f;
        using Pen pen = new(Color.White, Math.Max(2f, 3f * scale));
        using SolidBrush brush = new(Color.White);

        switch (glyph)
        {
            case "X":
                graphics.DrawLine(pen, 11 * scale, 11 * scale, 21 * scale, 21 * scale);
                graphics.DrawLine(pen, 21 * scale, 11 * scale, 11 * scale, 21 * scale);
                break;
            case "!":
                graphics.DrawLine(pen, 16 * scale, 9 * scale, 16 * scale, 19 * scale);
                graphics.FillEllipse(brush, 14 * scale, 22 * scale, 4 * scale, 4 * scale);
                break;
            case "?":
                graphics.DrawLine(pen, 12 * scale, 11 * scale, 15 * scale, 8 * scale);
                graphics.DrawLine(pen, 15 * scale, 8 * scale, 20 * scale, 10 * scale);
                graphics.DrawLine(pen, 20 * scale, 10 * scale, 20 * scale, 15 * scale);
                graphics.DrawLine(pen, 20 * scale, 15 * scale, 16 * scale, 18 * scale);
                graphics.DrawLine(pen, 16 * scale, 19 * scale, 16 * scale, 20 * scale);
                graphics.FillEllipse(brush, 14 * scale, 23 * scale, 4 * scale, 4 * scale);
                break;
            case "i":
                graphics.FillEllipse(brush, 14 * scale, 8 * scale, 4 * scale, 4 * scale);
                graphics.DrawLine(pen, 16 * scale, 14 * scale, 16 * scale, 24 * scale);
                break;
            case "L":
                graphics.DrawRectangle(pen, 11 * scale, 15 * scale, 10 * scale, 8 * scale);
                graphics.DrawLine(pen, 12 * scale, 15 * scale, 12 * scale, 11 * scale);
                graphics.DrawLine(pen, 12 * scale, 11 * scale, 20 * scale, 11 * scale);
                graphics.DrawLine(pen, 20 * scale, 11 * scale, 20 * scale, 15 * scale);
                break;
            default:
                graphics.DrawRectangle(pen, 11 * scale, 10 * scale, 10 * scale, 12 * scale);
                break;
        }
    }

    private enum SystemIconKind
    {
        Application,
        Asterisk,
        Error,
        Exclamation,
        Hand,
        Information,
        Question,
        Warning,
        WinLogo
    }

#if NET8_0_OR_GREATER
    /// <summary>
    ///  Gets the specified Windows shell stock icon.
    /// </summary>
    /// <param name="stockIcon">The stock icon to retrieve.</param>
    /// <param name="options">Options for retrieving the icon.</param>
    /// <returns>The requested <see cref="Icon"/>.</returns>
    /// <remarks>
    ///  <para>
    ///   Unlike the static icon properties in <see cref="SystemIcons"/>, this API returns icons that are themed
    ///   for the running version of Windows. Additionally, the returned <see cref="Icon"/> is not cached and
    ///   should be disposed when no longer needed.
    ///  </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="stockIcon"/> is an invalid <see cref="StockIconId"/>.</exception>
    public static Icon GetStockIcon(StockIconId stockIcon, StockIconOptions options = StockIconOptions.Default)
    {
        ValidateStockIcon(stockIcon);
        return CreateManagedIcon(GetStockIconColor(stockIcon), GetStockIconGlyph(stockIcon));
    }

    /// <inheritdoc cref="GetStockIcon(StockIconId, StockIconOptions)"/>
    /// <param name="size">
    ///  The desired size. If the specified size does not exist, an existing size will be resampled to give the
    ///  requested size.
    /// </param>
    public static Icon GetStockIcon(StockIconId stockIcon, int size)
    {
        ValidateStockIcon(stockIcon);
        return CreateManagedIcon(GetStockIconColor(stockIcon), GetStockIconGlyph(stockIcon), size);
    }

    private static void ValidateStockIcon(StockIconId stockIcon)
    {
        if (!Enum.IsDefined(typeof(StockIconId), stockIcon))
        {
            throw new ArgumentException(null, nameof(stockIcon));
        }
    }

    private static Color GetStockIconColor(StockIconId stockIcon) =>
        stockIcon switch
        {
            StockIconId.Application => Color.SlateGray,
            StockIconId.Folder or StockIconId.FolderOpen => Color.DarkGoldenrod,
            StockIconId.Printer => Color.DimGray,
            StockIconId.Help => Color.MediumPurple,
            StockIconId.Warning => Color.DarkGoldenrod,
            StockIconId.Error => Color.Firebrick,
            StockIconId.Info => Color.RoyalBlue,
            StockIconId.Shield or StockIconId.Lock => Color.SeaGreen,
            _ => Color.FromArgb(unchecked((int)(0xFF336699u + ((uint)stockIcon * 7919u)))),
        };

    private static string GetStockIconGlyph(StockIconId stockIcon) =>
        stockIcon switch
        {
            StockIconId.Application => "A",
            StockIconId.Folder or StockIconId.FolderOpen => "F",
            StockIconId.Printer => "P",
            StockIconId.Help => "?",
            StockIconId.Warning => "!",
            StockIconId.Error => "X",
            StockIconId.Info => "i",
            StockIconId.Shield => "S",
            StockIconId.Lock => "L",
            _ => string.Empty,
        };
#endif
}
