using MudBlazor;

namespace Bookshelf.Theming;

/// <summary>
/// All available themes ("bindings"). To add one, write a factory method like
/// <see cref="LeatherAndGilt"/>, add it to <see cref="All"/>, and add
/// <c>.theme-{key}</c> texture rules to <c>app.css</c>.
/// </summary>
public static class ThemeRegistry
{
    public const string DefaultKey = "leather-gilt";

    private static readonly string[] SerifBody = ["EB Garamond", "Georgia", "Times New Roman", "serif"];
    private static readonly string[] SerifDisplay = ["Cormorant Garamond", "EB Garamond", "Georgia", "serif"];

    public static IReadOnlyList<AppTheme> All { get; } =
        [LeatherAndGilt(), ClothBoundGreen(), LibraryAtNight(), Parchment(), MarbledEndpapers()];

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
        Build(dark: new PaletteDark
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
        }));

    private static AppTheme ClothBoundGreen() => new(
        "cloth-green",
        "Cloth-bound Green",
        "Bottle-green buckram with gold blocking, like an old library edition.",
        IsDark: true,
        ["#1f3d2b", "#14261c", "#d4b25a", "#efe6cf"],
        Build(dark: new PaletteDark
        {
            Primary = "#d4b25a",
            PrimaryContrastText = "#14261c",
            Secondary = "#b5493a",
            SecondaryContrastText = "#f5ecd6",
            Tertiary = "#8aa06a",
            Info = "#86a9c0",
            Success = "#9cc27a",
            Warning = "#e0a64a",
            Error = "#e06a58",
            Black = "#0b150f",
            Background = "#14261c",
            BackgroundGray = "#102018",
            Surface = "#1c3326",
            AppbarBackground = "#1f3d2b",
            AppbarText = "#e6cc7e",
            DrawerBackground = "#18301f",
            DrawerText = "#efe6cf",
            DrawerIcon = "#d4b25a",
            TextPrimary = "#efe6cf",
            TextSecondary = "#c3b896",
            TextDisabled = "rgba(239,230,207,0.4)",
            ActionDefault = "#c3b896",
            ActionDisabled = "rgba(239,230,207,0.3)",
            ActionDisabledBackground = "rgba(239,230,207,0.12)",
            LinesDefault = "rgba(212,178,90,0.28)",
            LinesInputs = "rgba(212,178,90,0.5)",
            Divider = "rgba(212,178,90,0.28)",
            DividerLight = "rgba(212,178,90,0.14)",
            TableLines = "rgba(212,178,90,0.2)",
            Skeleton = "rgba(212,178,90,0.12)",
            OverlayDark = "rgba(6,14,9,0.7)",
        }));

    private static AppTheme LibraryAtNight() => new(
        "library-night",
        "Library at Night",
        "A dark reading room by lamplight: navy, walnut and warm amber.",
        IsDark: true,
        ["#2a1d14", "#0f1420", "#e0a650", "#e9e1d0"],
        Build(dark: new PaletteDark
        {
            Primary = "#e0a650",
            PrimaryContrastText = "#1a1206",
            Secondary = "#7c9cc4",
            SecondaryContrastText = "#0f1420",
            Tertiary = "#9a8466",
            Info = "#7c9cc4",
            Success = "#8fb57a",
            Warning = "#e0a650",
            Error = "#e07264",
            Black = "#07090f",
            Background = "#0f1420",
            BackgroundGray = "#0b0f18",
            Surface = "#181f2e",
            AppbarBackground = "#2a1d14",
            AppbarText = "#f0c27a",
            DrawerBackground = "#141a27",
            DrawerText = "#e9e1d0",
            DrawerIcon = "#e0a650",
            TextPrimary = "#e9e1d0",
            TextSecondary = "#a9a291",
            TextDisabled = "rgba(233,225,208,0.4)",
            ActionDefault = "#a9a291",
            ActionDisabled = "rgba(233,225,208,0.3)",
            ActionDisabledBackground = "rgba(233,225,208,0.1)",
            LinesDefault = "rgba(224,166,80,0.24)",
            LinesInputs = "rgba(224,166,80,0.45)",
            Divider = "rgba(224,166,80,0.24)",
            DividerLight = "rgba(224,166,80,0.12)",
            TableLines = "rgba(224,166,80,0.18)",
            Skeleton = "rgba(224,166,80,0.1)",
            OverlayDark = "rgba(4,6,12,0.75)",
        }));

    private static AppTheme Parchment() => new(
        "parchment",
        "Parchment",
        "Aged cream pages, sepia ink and rubric red. Light, for bright days.",
        IsDark: false,
        ["#5b3a1e", "#f3e9d2", "#9b3222", "#3b2a1a"],
        Build(light: new PaletteLight
        {
            Primary = "#9b3222",
            PrimaryContrastText = "#fbf4e2",
            Secondary = "#5b3a1e",
            SecondaryContrastText = "#fbf4e2",
            Tertiary = "#6f7a45",
            Info = "#3f6684",
            Success = "#4f7a35",
            Warning = "#a8650f",
            Error = "#b3261e",
            Black = "#2a1d10",
            Background = "#f3e9d2",
            BackgroundGray = "#ece0c4",
            Surface = "#faf3e0",
            AppbarBackground = "#5b3a1e",
            AppbarText = "#f6e7c1",
            DrawerBackground = "#f6edd8",
            DrawerText = "#3b2a1a",
            DrawerIcon = "#9b3222",
            TextPrimary = "#3b2a1a",
            TextSecondary = "#6e5a43",
            TextDisabled = "rgba(59,42,26,0.4)",
            ActionDefault = "#6e5a43",
            ActionDisabled = "rgba(59,42,26,0.3)",
            ActionDisabledBackground = "rgba(59,42,26,0.1)",
            LinesDefault = "rgba(91,58,30,0.28)",
            LinesInputs = "rgba(91,58,30,0.5)",
            Divider = "rgba(91,58,30,0.24)",
            DividerLight = "rgba(91,58,30,0.12)",
            TableLines = "rgba(91,58,30,0.18)",
            Skeleton = "rgba(91,58,30,0.1)",
            OverlayLight = "rgba(243,233,210,0.7)",
        }));

    private static AppTheme MarbledEndpapers() => new(
        "marbled",
        "Marbled Endpapers",
        "Crisp white pages framed with blue and ochre marbled paper.",
        IsDark: false,
        ["#24476b", "#f7f4ec", "#c08a32", "#23262b"],
        Build(light: new PaletteLight
        {
            Primary = "#1f4e79",
            PrimaryContrastText = "#ffffff",
            Secondary = "#a8702a",
            SecondaryContrastText = "#ffffff",
            Tertiary = "#6a7f4a",
            Info = "#2f6d9a",
            Success = "#3f7a3a",
            Warning = "#a8650f",
            Error = "#b3261e",
            Black = "#15181c",
            Background = "#f7f4ec",
            BackgroundGray = "#efebe0",
            Surface = "#ffffff",
            AppbarBackground = "#24476b",
            AppbarText = "#fbf6e9",
            DrawerBackground = "#ffffff",
            DrawerText = "#23262b",
            DrawerIcon = "#1f4e79",
            TextPrimary = "#23262b",
            TextSecondary = "#5b6068",
            TextDisabled = "rgba(35,38,43,0.4)",
            ActionDefault = "#5b6068",
            ActionDisabled = "rgba(35,38,43,0.3)",
            ActionDisabledBackground = "rgba(35,38,43,0.08)",
            LinesDefault = "rgba(31,78,121,0.2)",
            LinesInputs = "rgba(31,78,121,0.45)",
            Divider = "rgba(31,78,121,0.18)",
            DividerLight = "rgba(31,78,121,0.1)",
            TableLines = "rgba(31,78,121,0.15)",
            Skeleton = "rgba(31,78,121,0.08)",
            OverlayLight = "rgba(247,244,236,0.7)",
        }));

    /// <summary>Every binding shares the old-book typography; only colours (and CSS textures) differ.</summary>
    private static MudTheme Build(PaletteDark? dark = null, PaletteLight? light = null)
    {
        var theme = new MudTheme
        {
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
        };
        if (dark is not null)
        {
            theme.PaletteDark = dark;
        }

        if (light is not null)
        {
            theme.PaletteLight = light;
        }

        return theme;
    }
}
