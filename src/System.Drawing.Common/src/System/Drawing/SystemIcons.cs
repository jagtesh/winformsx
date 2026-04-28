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

    public static Icon Application => GetIcon(ref s_application, PInvokeCore.IDI_APPLICATION);

    public static Icon Asterisk => GetIcon(ref s_asterisk, PInvokeCore.IDI_ASTERISK);

    public static Icon Error => GetIcon(ref s_error, PInvokeCore.IDI_ERROR);

    public static Icon Exclamation => GetIcon(ref s_exclamation, PInvokeCore.IDI_EXCLAMATION);

    public static Icon Hand => GetIcon(ref s_hand, PInvokeCore.IDI_HAND);

    public static Icon Information => GetIcon(ref s_information, PInvokeCore.IDI_INFORMATION);

    public static Icon Question => GetIcon(ref s_question, PInvokeCore.IDI_QUESTION);

    public static Icon Warning => GetIcon(ref s_warning, PInvokeCore.IDI_WARNING);

    public static Icon WinLogo => GetIcon(ref s_winlogo, PInvokeCore.IDI_WINLOGO);

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

    private static Icon GetIcon(ref Icon? icon, PCWSTR iconId) =>
        icon ??= CreateManagedIcon(Color.SteelBlue);

    private static Icon CreateManagedIcon(Color accent)
    {
        Bitmap bitmap = new(32, 32, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        using SolidBrush brush = new(accent);
        graphics.FillEllipse(brush, 4, 4, 24, 24);
        using Pen pen = new(Color.White, 2);
        graphics.DrawEllipse(pen, 4, 4, 24, 24);
        return new Icon(bitmap);
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
    public static Icon GetStockIcon(StockIconId stockIcon, StockIconOptions options = StockIconOptions.Default) =>
        CreateManagedIcon(Color.FromArgb(unchecked((int)(0xFF336699u + ((uint)stockIcon * 7919u)))));

    /// <inheritdoc cref="GetStockIcon(StockIconId, StockIconOptions)"/>
    /// <param name="size">
    ///  The desired size. If the specified size does not exist, an existing size will be resampled to give the
    ///  requested size.
    /// </param>
    public static Icon GetStockIcon(StockIconId stockIcon, int size) =>
        CreateManagedIcon(Color.FromArgb(unchecked((int)(0xFF669933u + ((uint)stockIcon * 3571u)))));
#endif
}
