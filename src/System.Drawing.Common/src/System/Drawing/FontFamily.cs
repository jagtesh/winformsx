// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Text;
using System.Globalization;

namespace System.Drawing;

/// <summary>
///  Abstracts a group of type faces having a similar basic design but having certain variation in styles.
/// </summary>
public sealed unsafe class FontFamily : MarshalByRefObject, IDisposable
{
    private const int NeutralLanguage = 0;
    private readonly string _name;

    internal FontFamily(GpFontFamily* family)
    {
        Debug.Assert(family is not null, "Initializing native font family with null.");
        _name = "SansSerif";
    }

    internal FontFamily(string name, bool createDefaultOnFail)
    {
        _name = string.IsNullOrWhiteSpace(name) && createDefaultOnFail ? "SansSerif" : name;
    }

    public FontFamily(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    public FontFamily(string name, FontCollection? fontCollection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        GC.KeepAlive(fontCollection);
    }

    public FontFamily(GenericFontFamilies genericFamily)
    {
        _name = genericFamily switch
        {
            GenericFontFamilies.Serif => "Serif",
            GenericFontFamilies.Monospace => "Monospace",
            _ => "SansSerif",
        };
    }

    ~FontFamily() => Dispose(disposing: false);

    internal GpFontFamily* NativeFamily => null;

    public override string ToString() => $"[{GetType().Name}: Name={Name}]";

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FontFamily otherFamily
            && string.Equals(otherFamily.Name, Name, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(GetName(NeutralLanguage));

    private static int CurrentLanguage => CultureInfo.CurrentUICulture.LCID;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
    }

    public string Name => _name;

    public string GetName(int language) => _name;

    public static FontFamily[] Families => new InstalledFontCollection().Families;

    public static FontFamily GenericSansSerif => new(GenericFontFamilies.SansSerif);

    public static FontFamily GenericSerif => new(GenericFontFamilies.Serif);

    public static FontFamily GenericMonospace => new(GenericFontFamilies.Monospace);

    [Obsolete("FontFamily.GetFamilies has been deprecated. Use Families instead.")]
    public static FontFamily[] GetFamilies(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        return new InstalledFontCollection().Families;
    }

    public bool IsStyleAvailable(FontStyle style) => true;

    public int GetEmHeight(FontStyle style) => 2048;

    public int GetCellAscent(FontStyle style) => 1854;

    public int GetCellDescent(FontStyle style) => 434;

    public int GetLineSpacing(FontStyle style) => 2355;
}
