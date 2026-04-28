// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Drawing.Drawing2D;

public sealed unsafe class HatchBrush : Brush
{
    private readonly HatchStyle _hatchStyle;
    private readonly Color _foreColor;
    private readonly Color _backColor;

    public HatchBrush(HatchStyle hatchstyle, Color foreColor)
        : this(hatchstyle, foreColor, Color.FromArgb(unchecked((int)0xff000000)))
    {
    }

    public HatchBrush(HatchStyle hatchstyle, Color foreColor, Color backColor)
    {
        if (hatchstyle is < HatchStyle.Min or > HatchStyle.SolidDiamond)
        {
            throw new ArgumentException(SR.Format(SR.InvalidEnumArgument, nameof(hatchstyle), hatchstyle, nameof(HatchStyle)), nameof(hatchstyle));
        }

        _hatchStyle = hatchstyle;
        _foreColor = foreColor;
        _backColor = backColor;
    }

    internal HatchBrush(GpHatch* nativeBrush)
    {
        Debug.Assert(nativeBrush is not null, "Initializing native brush with null.");
        _hatchStyle = HatchStyle.Horizontal;
        _foreColor = Color.Black;
        _backColor = Color.White;
    }

    public override object Clone() => new HatchBrush(_hatchStyle, _foreColor, _backColor);

    public HatchStyle HatchStyle => _hatchStyle;

    public Color ForegroundColor => _foreColor;

    public Color BackgroundColor => _backColor;
}
