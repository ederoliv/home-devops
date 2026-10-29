using System.Text.Json.Nodes;
using HomeDevops.Services;

namespace HomeDevops.Tests;

public class ConfigAndThemeTests
{
    [Fact]
    public void Missing_config_returns_defaults_and_does_not_create_anything()
    {
        using var temp = new TempFolder();
        var folder = Path.Combine(temp.Path, ".homeDevops");

        var appearance = new ConfigService(folder).Get<AppearanceSettings>(ConfigSections.Appearance);

        Assert.Equal(AppearanceSettings.DefaultBg, appearance.Bg);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void Set_writes_one_key_per_section_and_the_folder_is_hidden_on_windows()
    {
        using var temp = new TempFolder();
        var folder = Path.Combine(temp.Path, ".homeDevops");
        var config = new ConfigService(folder);

        config.Set(ConfigSections.Appearance, new AppearanceSettings { Bg = "#ffffff", Text = "#000000", Accent = "#000000" });

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "config.json")))!;
        Assert.Equal("#ffffff", json["appearance"]!["bg"]!.GetValue<string>());
        Assert.Equal("#000000", json["appearance"]!["accent"]!.GetValue<string>());
        Assert.Null(json["sidebar"]);
        if (OperatingSystem.IsWindows())
        {
            Assert.True(File.GetAttributes(folder).HasFlag(FileAttributes.Hidden));
        }

        Assert.Equal("#ffffff", new ConfigService(folder).Get<AppearanceSettings>(ConfigSections.Appearance).Bg);
    }

    [Fact]
    public void Writing_a_section_preserves_the_others_including_unknown_ones()
    {
        using var temp = new TempFolder();
        var folder = temp.Sub(".homeDevops");
        File.WriteAllText(Path.Combine(folder, "config.json"), """{ "futura": { "x": 1 }, "sidebar": { "collapsed": true } }""");
        var config = new ConfigService(folder);

        config.Set(ConfigSections.Appearance, new AppearanceSettings());

        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(folder, "config.json")))!;
        Assert.Equal(1, json["futura"]!["x"]!.GetValue<int>());
        Assert.True(json["sidebar"]!["collapsed"]!.GetValue<bool>());
        Assert.NotNull(json["appearance"]);
    }

    [Fact]
    public void Reads_the_config_written_by_the_previous_app()
    {
        using var temp = new TempFolder();
        var folder = temp.Sub(".homeDevops");
        File.WriteAllText(Path.Combine(folder, "config.json"), """{"appearance":{"accent":"#000000","bg":"#ffffff","text":"#000000"}}""");

        var appearance = new ConfigService(folder).Get<AppearanceSettings>(ConfigSections.Appearance);

        Assert.Equal(("#ffffff", "#000000", "#000000"), (appearance.Bg, appearance.Text, appearance.Accent));
    }

    [Fact]
    public void A_corrupt_config_is_kept_as_a_copy_and_treated_as_empty()
    {
        using var temp = new TempFolder();
        var folder = temp.Sub(".homeDevops");
        File.WriteAllText(Path.Combine(folder, "config.json"), "{ quebrado");

        var config = new ConfigService(folder);

        Assert.False(config.Get<SidebarSettings>(ConfigSections.Sidebar).Collapsed);
        Assert.Equal("{ quebrado", File.ReadAllText(Path.Combine(folder, "config.json.corrupt")));
    }

    [Fact]
    public void Default_palette_matches_the_documented_colors()
    {
        var palette = ThemePalette.From(new AppearanceSettings());

        Assert.Equal(("#14161b", "#e6e8ee", "#5b8def"), (palette.Bg, palette.Text, palette.Accent));
        Assert.True(palette.IsDark);
        Assert.Equal("#ffffff", palette.OnAccent);
    }

    [Fact]
    public void Derived_colors_mix_the_text_over_the_background()
    {
        var palette = ThemePalette.From(new AppearanceSettings { Bg = "#000000", Text = "#ffffff", Accent = "#ffffff" });

        Assert.Equal("#0d0d0d", palette.Panel);   // 5%
        Assert.Equal("#171717", palette.Panel2);  // 9%
        Assert.Equal("#292929", palette.Line);    // 16%
        Assert.Equal("#8c8c8c", palette.Muted);   // 55%
    }

    [Theory]
    [InlineData("#ffffff", "#111111")]
    [InlineData("#000000", "#ffffff")]
    [InlineData("#5b8def", "#ffffff")]
    [InlineData("#ffd54a", "#111111")]
    public void Text_on_accent_flips_with_the_accent_brightness(string accent, string expected) =>
        Assert.Equal(expected, ThemePalette.From(new AppearanceSettings { Accent = accent }).OnAccent);

    [Fact]
    public void Light_background_is_not_dark_and_invalid_colors_fall_back_to_defaults()
    {
        Assert.False(ThemePalette.From(new AppearanceSettings { Bg = "#ffffff" }).IsDark);

        var palette = ThemePalette.From(new AppearanceSettings { Bg = "azul", Text = "#12", Accent = "" });

        Assert.Equal(("#14161b", "#e6e8ee", "#5b8def"), (palette.Bg, palette.Text, palette.Accent));
    }

    [Theory]
    [InlineData("#abcdef", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("abcdef", false)]
    [InlineData("#abcde", false)]
    [InlineData("#gggggg", false)]
    [InlineData(null, false)]
    public void IsValidHex_accepts_only_full_six_digit_colors(string? value, bool expected) =>
        Assert.Equal(expected, ThemePalette.IsValidHex(value));
}
