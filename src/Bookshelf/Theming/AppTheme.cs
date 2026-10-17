using MudBlazor;

namespace Bookshelf.Theming;

/// <param name="Key">Stable id saved in settings and used as a CSS class (<c>theme-{Key}</c>).</param>
/// <param name="Name">Shown in the theme picker.</param>
/// <param name="Description">One line for the theme picker.</param>
/// <param name="IsDark">Whether MudBlazor should use <see cref="MudTheme.PaletteDark"/>.</param>
/// <param name="Swatches">A few colours previewed in the picker.</param>
/// <param name="Theme">The MudBlazor theme.</param>
public sealed record AppTheme(
    string Key,
    string Name,
    string Description,
    bool IsDark,
    IReadOnlyList<string> Swatches,
    MudTheme Theme)
{
    /// <summary>The app bar colour: also used for the phone's status bar.</summary>
    public string BarColor => Hex(IsDark ? Theme.PaletteDark.AppbarBackground : Theme.PaletteLight.AppbarBackground);

    /// <summary>The page colour: shown behind the app (overscroll, start-up).</summary>
    public string PageColor => Hex(IsDark ? Theme.PaletteDark.Background : Theme.PaletteLight.Background);

    private static string Hex(MudBlazor.Utilities.MudColor colour) => colour.ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex);
}
