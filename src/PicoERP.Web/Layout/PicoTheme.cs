using MudBlazor;

namespace PicoERP.Web.Layout;

/// <summary>
/// Premium Persian enterprise MudBlazor theme for PicoERP
/// </summary>
public static class PicoTheme
{
    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565C0",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#5C6BC0",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#00897B",
            TertiaryContrastText = "#FFFFFF",
            Success = "#2E7D32",
            Warning = "#E65100",
            Error = "#C62828",
            Info = "#0277BD",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1A237E",
            AppbarBackground = "#1565C0",
            AppbarText = "#FFFFFF",
            TableLines = "#E3E8EE",
            TableStriped = "#F5F7FF",
            TextPrimary = "#1A1A2E",
            TextSecondary = "#5A6374",
            ActionDefault = "#6E7B8A",
            Divider = "#E3E8EE",
            OverlayLight = "rgba(255,255,255,0.88)",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#5C9CE6",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#7986CB",
            Background = "#121212",
            Surface = "#1E1E2E",
            DrawerBackground = "#1A1A2E",
            AppbarBackground = "#0D0D1A",
            TextPrimary = "#E8EAF6",
            TextSecondary = "#9FA8DA",
            Divider = "#2D2D44",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Vazirmatn", "Tahoma", "Arial", "sans-serif" },
                FontSize = "14px",
                FontWeight = "400",
                LineHeight = "1.6",
                LetterSpacing = "0"
            },
            H1 = new H1Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "2rem", FontWeight = "700" },
            H2 = new H2Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "1.75rem", FontWeight = "700" },
            H3 = new H3Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "1.5rem", FontWeight = "600" },
            H4 = new H4Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "1.25rem", FontWeight = "600" },
            H5 = new H5Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "1.1rem", FontWeight = "600" },
            H6 = new H6Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "1rem", FontWeight = "600" },
            Body1 = new Body1Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "0.9rem" },
            Body2 = new Body2Typography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "0.82rem" },
            Caption = new CaptionTypography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "0.75rem" },
            Button = new ButtonTypography { FontFamily = new[] { "Vazirmatn", "Tahoma", "sans-serif" }, FontSize = "0.875rem", FontWeight = "600", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthRight = "260px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "64px",
        },
    };
}
