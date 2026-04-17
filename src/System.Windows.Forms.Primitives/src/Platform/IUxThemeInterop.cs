namespace System.Windows.Forms.Platform;

/// <summary>
///  Defines the contract for UxTheme OS queries, allowing substitution of visual themes.
/// </summary>
internal interface IUxThemeInterop
{
    BOOL IsAppThemed();
}
