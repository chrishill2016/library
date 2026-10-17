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
}
