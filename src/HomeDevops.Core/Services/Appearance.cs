using System.Globalization;
using System.Text.RegularExpressions;

namespace HomeDevops.Services;

/// <summary>Seção "appearance" do config.json.</summary>
public sealed class AppearanceSettings
{
    public const string DefaultBg = "#14161b";
    public const string DefaultText = "#e6e8ee";
    public const string DefaultAccent = "#5b8def";

    public string Bg { get; set; } = DefaultBg;

    public string Text { get; set; } = DefaultText;

    public string Accent { get; set; } = DefaultAccent;
}

/// <summary>Seção "sidebar" do config.json.</summary>
public sealed class SidebarSettings
{
    public bool Collapsed { get; set; }
}

/// <summary>
/// Todas as cores da interface, calculadas a partir das três escolhidas pelo usuário
/// (fundo, fonte principal e destaque). Valores inválidos caem no padrão.
/// </summary>
public sealed partial record ThemePalette(
    string Bg,
    string Text,
    string Accent,
    string OnAccent,
    string Panel,
    string Panel2,
    string Line,
    string Muted,
    bool IsDark)
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexPattern();

    public static bool IsValidHex(string? value) => value is not null && HexPattern().IsMatch(value);

    public static ThemePalette From(AppearanceSettings settings)
    {
        var bg = Parse(settings.Bg, AppearanceSettings.DefaultBg);
        var text = Parse(settings.Text, AppearanceSettings.DefaultText);
        var accent = Parse(settings.Accent, AppearanceSettings.DefaultAccent);

        return new ThemePalette(
            Bg: ToHex(bg),
            Text: ToHex(text),
            Accent: ToHex(accent),
            OnAccent: Brightness(accent) > 0.6 ? "#111111" : "#ffffff",
            Panel: ToHex(Mix(text, bg, 0.05)),
            Panel2: ToHex(Mix(text, bg, 0.09)),
            Line: ToHex(Mix(text, bg, 0.16)),
            Muted: ToHex(Mix(text, bg, 0.55)),
            IsDark: Brightness(bg) <= 0.5);
    }

    /// <summary>Brilho percebido, de 0 (preto) a 1 (branco).</summary>
    public static double Brightness((byte R, byte G, byte B) c) => ((0.299 * c.R) + (0.587 * c.G) + (0.114 * c.B)) / 255;

    /// <summary>Mistura <paramref name="amount"/> de <paramref name="fg"/> sobre <paramref name="bg"/>.</summary>
    public static (byte R, byte G, byte B) Mix((byte R, byte G, byte B) fg, (byte R, byte G, byte B) bg, double amount) =>
    (
        Blend(fg.R, bg.R, amount),
        Blend(fg.G, bg.G, amount),
        Blend(fg.B, bg.B, amount)
    );

    private static byte Blend(byte fg, byte bg, double amount) =>
        (byte)Math.Round((fg * amount) + (bg * (1 - amount)));

    private static (byte R, byte G, byte B) Parse(string? hex, string fallback)
    {
        var value = IsValidHex(hex) ? hex! : fallback;
        var n = int.Parse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(n >> 16), (byte)((n >> 8) & 0xFF), (byte)(n & 0xFF));
    }

    private static string ToHex((byte R, byte G, byte B) c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";
}
