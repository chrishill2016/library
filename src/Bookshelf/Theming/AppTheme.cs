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
    MudTheme Theme);
