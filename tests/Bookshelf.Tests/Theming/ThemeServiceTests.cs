using MudBlazor;
using Bookshelf.Tests.Fakes;
using Bookshelf.Theming;

namespace Bookshelf.Tests.Theming;

public class ThemeServiceTests
{
    private readonly InMemoryLocalStore _store = new();

    [Fact]
    public void Defaults_to_leather_and_gilt()
    {
        var sut = new ThemeService(_store);

        Assert.Equal("leather-gilt", sut.Current.Key);
        Assert.True(sut.Current.IsDark);
    }

    [Fact]
    public async Task Unknown_saved_theme_falls_back_to_default()
    {
        _store.Settings[ThemeService.SettingKey] = "retired-theme";
        var sut = new ThemeService(_store);

        await sut.InitializeAsync();

        Assert.Equal(ThemeRegistry.DefaultKey, sut.Current.Key);
    }

    [Fact]
    public async Task Setting_a_theme_saves_it()
    {
        var sut = new ThemeService(_store);

        await sut.SetThemeAsync(ThemeRegistry.DefaultKey);

        Assert.Equal(ThemeRegistry.DefaultKey, _store.Settings[ThemeService.SettingKey]);
    }

    [Fact]
    public void Registry_keys_are_unique_and_css_safe()
    {
        var keys = ThemeRegistry.All.Select(t => t.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.Matches("^[a-z0-9-]+$", k));
        Assert.Contains(ThemeRegistry.DefaultKey, keys);
    }

    [Fact]
    public void Every_theme_defines_the_palette_it_uses()
    {
        foreach (var theme in ThemeRegistry.All)
        {
            var palette = theme.IsDark ? (MudBlazor.Palette)theme.Theme.PaletteDark : theme.Theme.PaletteLight;
            Assert.NotNull(palette.Primary);
            Assert.NotEmpty(theme.Swatches);
        }
    }

    public static TheoryData<string> ThemeKeys => [.. ThemeRegistry.All.Select(t => t.Key)];

    [Theory]
    [MemberData(nameof(ThemeKeys))]
    public void Every_binding_has_readable_text(string key)
    {
        var theme = ThemeRegistry.Get(key);
        Palette palette = theme.IsDark ? theme.Theme.PaletteDark : theme.Theme.PaletteLight;
        string C(MudBlazor.Utilities.MudColor c) => c.ToString(MudBlazor.Utilities.MudColorOutputFormats.Hex);

        // WCAG AA: 4.5:1 for body text and button labels.
        Assert.True(Contrast(C(palette.TextPrimary), C(palette.Background)) >= 4.5, $"{key}: text on background");
        Assert.True(Contrast(C(palette.TextPrimary), C(palette.Surface)) >= 4.5, $"{key}: text on panels");
        Assert.True(Contrast(C(palette.TextSecondary), C(palette.Surface)) >= 4.5, $"{key}: secondary text");
        Assert.True(Contrast(C(palette.PrimaryContrastText), C(palette.Primary)) >= 4.5, $"{key}: button labels");
        Assert.True(Contrast(C(palette.AppbarText), C(palette.AppbarBackground)) >= 4.5, $"{key}: app bar");
        // Primary is also used for links, outlined buttons and stars on the page: 3:1 for large/UI.
        Assert.True(Contrast(C(palette.Primary), C(palette.Surface)) >= 3, $"{key}: primary on panels");
        Assert.Equal(theme.IsDark ? C(theme.Theme.PaletteDark.AppbarBackground) : C(theme.Theme.PaletteLight.AppbarBackground), theme.BarColor);
    }

    private static double Contrast(string a, string b)
    {
        static double Luminance(string hex)
        {
            double Channel(int i)
            {
                var c = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16) / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(0) + 0.7152 * Channel(1) + 0.0722 * Channel(2);
        }

        var (l1, l2) = (Luminance(a), Luminance(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }
}
