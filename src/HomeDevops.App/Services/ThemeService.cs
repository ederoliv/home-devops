using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using HomeDevops.Services;

namespace HomeDevops.Services;

/// <summary>Aplica as cores escolhidas: troca os pincéis do tema e pinta a barra de título das janelas.</summary>
public sealed class ThemeService
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    private ThemePalette _palette = ThemePalette.From(new AppearanceSettings());

    public void Apply(AppearanceSettings settings)
    {
        _palette = ThemePalette.From(settings);

        var resources = Application.Current.Resources;
        resources["BgBrush"] = Brush(_palette.Bg);
        resources["TextBrush"] = Brush(_palette.Text);
        resources["AccentBrush"] = Brush(_palette.Accent);
        resources["OnAccentBrush"] = Brush(_palette.OnAccent);
        resources["PanelBrush"] = Brush(_palette.Panel);
        resources["Panel2Brush"] = Brush(_palette.Panel2);
        resources["LineBrush"] = Brush(_palette.Line);
        resources["MutedBrush"] = Brush(_palette.Muted);

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }
    }

    /// <summary>Pinta a barra de título com as cores do tema (Windows 11; em versões antigas é ignorado).</summary>
    public void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        SetAttribute(handle, DwmUseImmersiveDarkMode, _palette.IsDark ? 1 : 0);
        SetAttribute(handle, DwmCaptionColor, ColorRef(_palette.Bg));
        SetAttribute(handle, DwmTextColor, ColorRef(_palette.Text));
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    /// <summary>COLORREF do Windows: 0x00BBGGRR.</summary>
    private static int ColorRef(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        return color.R | (color.G << 8) | (color.B << 16);
    }

    private static void SetAttribute(IntPtr handle, int attribute, int value) =>
        _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
