// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing;

namespace System.Windows.Forms.VisualStyles;

/// <summary>
///  Provides information about the current visual style.
///
///  NOTE:
///
///  1) These properties (except SupportByOS, which is always meaningful) are meaningful only
///  if visual styles are supported and have currently been applied by the user.
///  2) A subset of these use VisualStyleRenderer objects, so they are
///  not meaningful unless VisualStyleRenderer.IsSupported is true.
/// </summary>
public static class VisualStyleInformation
{
    /// <summary>
    ///  Used to find whether visual styles are supported by the current OS. Same as
    ///  using the OSFeature class to see if themes are supported.
    ///  This is always supported on platforms that .NET Core supports.
    /// </summary>
    public static bool IsSupportedByOS => false;

    /// <summary>
    ///  Returns true if a visual style has currently been applied by the user, else false.
    /// </summary>
    public static bool IsEnabledByUser => false;

    internal static string ThemeFilename => string.Empty;

    /// <summary>
    ///  The current visual style's color scheme name.
    /// </summary>
    public static string ColorScheme => string.Empty;

    /// <summary>
    ///  The current visual style's size name.
    /// </summary>
    public static string Size => string.Empty;

    /// <summary>
    ///  The current visual style's display name.
    /// </summary>
    public static string DisplayName => string.Empty;

    /// <summary>
    ///  The current visual style's company.
    /// </summary>
    public static string Company => string.Empty;

    /// <summary>
    ///  The name of the current visual style's author.
    /// </summary>
    public static string Author => string.Empty;

    /// <summary>
    ///  The current visual style's copyright information.
    /// </summary>
    public static string Copyright => string.Empty;

    /// <summary>
    ///  The current visual style's url.
    /// </summary>
    public static string Url => string.Empty;

    /// <summary>
    ///  The current visual style's version.
    /// </summary>
    public static string Version => string.Empty;

    /// <summary>
    ///  The current visual style's description.
    /// </summary>
    public static string Description => string.Empty;

    /// <summary>
    ///  Returns true if the current theme supports flat menus, else false.
    /// </summary>
    public static bool SupportsFlatMenus => false;

    /// <summary>
    ///  The minimum color depth supported by the current visual style.
    /// </summary>
    public static int MinimumColorDepth
    {
        get
        {
            return 0;
        }
    }

    /// <summary>
    ///  Border Color that Windows renders for controls like TextBox and ComboBox.
    /// </summary>
    public static Color TextControlBorder => SystemColors.WindowFrame;

    /// <summary>
    ///  This is the color buttons and tab pages are highlighted with when they are moused over on themed OS.
    /// </summary>
    public static Color ControlHighlightHot => SystemColors.ButtonHighlight;
}
