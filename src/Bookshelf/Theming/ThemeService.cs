using Bookshelf.Services.Storage;

namespace Bookshelf.Theming;

/// <summary>Holds the selected theme and remembers it on this device.</summary>
public sealed class ThemeService(ILocalStore store)
{
    internal const string SettingKey = "ui.theme";

    public AppTheme Current { get; private set; } = ThemeRegistry.Default;

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var theme = ThemeRegistry.Get(await store.GetSettingAsync(SettingKey));
        if (theme != Current)
        {
            Current = theme;
            Changed?.Invoke();
        }
    }

    public async Task SetThemeAsync(string key)
    {
        var theme = ThemeRegistry.Get(key);
        await store.SetSettingAsync(SettingKey, theme.Key);
        if (theme != Current)
        {
            Current = theme;
            Changed?.Invoke();
        }
    }
}
