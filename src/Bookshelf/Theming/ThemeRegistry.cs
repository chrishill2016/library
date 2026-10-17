using MudBlazor;

namespace Bookshelf.Theming;

/// <summary>
/// All available themes. To add one, write a factory method like
/// <see cref="LeatherAndGilt"/>, add it to <see cref="All"/>, and
/// optionally add <c>.theme-{key}</c> texture rules to <c>app.css</c>.
/// </summary>
public static class ThemeRegistry
{
    public const string DefaultKey = "leather-gilt";

    private static readonly string[] SerifBody = ["EB Garamond", "Georgia", "Times New Roman", "serif"];
    private static readonly string[] SerifDisplay = ["Cormorant Garamond", "EB Garamond", "Georgia", "serif"];

    public static IReadOnlyList<AppTheme> All { get; } = [LeatherAndGilt()];

    public static AppTheme Default => Get(DefaultKey);

    /// <summary>The theme with <paramref name="key"/>, or the default when unknown.</summary>
    public static AppTheme Get(string? key) =>
        All.FirstOrDefault(t => t.Key == key) ?? All.First(t => t.Key == DefaultKey);

    private static AppTheme LeatherAndGilt() => new(
        DefaultKey,
        "Leather & Gilt",
        "Oxblood leather binding with gold-leaf tooling.",
        IsDark: true,
        ["#4a1a17", "#2b1310", "#c9a44c", "#f1e4c8"],
        new MudTheme
        {
            PaletteDark = new PaletteDark
            {
                Primary = "#c9a44c",
                PrimaryContrastText = "#2b1310",
                Secondary = "#a8443c",
                SecondaryContrastText = "#f1e4c8",
                Tertiary = "#7c8b5a",
                Info = "#7fa0b8",
                Success = "#8fae6b",
                Warning = "#d99a3d",
                Error = "#d9604f",
                Black = "#1a0b09",
                Background = "#2b1310",
                BackgroundGray = "#241009",
                Surface = "#3a1b16",
                AppbarBackground = "#4a1a17",
                AppbarText = "#e8cf8a",
                DrawerBackground = "#331713",
                DrawerText = "#f1e4c8",
                DrawerIcon = "#c9a44c",
                TextPrimary = "#f1e4c8",
                TextSecondary = "#cdb894",
                TextDisabled = "rgba(241,228,200,0.4)",
                ActionDefault = "#cdb894",
                ActionDisabled = "rgba(241,228,200,0.3)",
                ActionDisabledBackground = "rgba(241,228,200,0.12)",
                LinesDefault = "rgba(201,164,76,0.28)",
                LinesInputs = "rgba(201,164,76,0.5)",
                Divider = "rgba(201,164,76,0.28)",
                DividerLight = "rgba(201,164,76,0.14)",
                TableLines = "rgba(201,164,76,0.2)",
                Skeleton = "rgba(201,164,76,0.12)",
                OverlayDark = "rgba(20,8,6,0.7)",
            },
            Typography = new Typography
            {
                Default = new DefaultTypography { FontFamily = SerifBody, FontSize = "1.0625rem", LetterSpacing = "normal" },
                H1 = new H1Typography { FontFamily = SerifDisplay, FontWeight = "700" },
                H2 = new H2Typography { FontFamily = SerifDisplay, FontWeight = "700" },
                H3 = new H3Typography { FontFamily = SerifDisplay, FontWeight = "700" },
                H4 = new H4Typography { FontFamily = SerifDisplay, FontWeight = "700", LetterSpacing = "0.02em" },
                H5 = new H5Typography { FontFamily = SerifDisplay, FontWeight = "600", LetterSpacing = "0.02em" },
                H6 = new H6Typography { FontFamily = SerifDisplay, FontWeight = "600", LetterSpacing = "0.04em" },
                Subtitle1 = new Subtitle1Typography { FontFamily = SerifBody },
                Subtitle2 = new Subtitle2Typography { FontFamily = SerifBody, FontWeight = "600" },
                Body1 = new Body1Typography { FontFamily = SerifBody, FontSize = "1.0625rem" },
                Body2 = new Body2Typography { FontFamily = SerifBody, FontSize = "0.95rem" },
                Button = new ButtonTypography { FontFamily = SerifBody, FontWeight = "600", LetterSpacing = "0.06em", TextTransform = "uppercase" },
                Caption = new CaptionTypography { FontFamily = SerifBody, FontSize = "0.85rem" },
                Overline = new OverlineTypography { FontFamily = SerifBody, LetterSpacing = "0.18em" },
            },
            LayoutProperties = new LayoutProperties { DefaultBorderRadius = "3px" },
        });
}
