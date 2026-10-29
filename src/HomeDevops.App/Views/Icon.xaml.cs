using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HomeDevops.Views;

/// <summary>Desenha um ícone do Lucide na cor do <c>Foreground</c> herdado (ex.: o do botão que o contém).</summary>
public partial class Icon : UserControl
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data),
        typeof(Geometry),
        typeof(Icon),
        new PropertyMetadata(null, (d, e) => ((Icon)d).Glyph.Data = (Geometry?)e.NewValue));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size),
        typeof(double),
        typeof(Icon),
        new PropertyMetadata(16.0, (d, e) => ((Icon)d).Width = ((Icon)d).Height = (double)e.NewValue));

    public Icon()
    {
        InitializeComponent();
        Width = Height = Size;
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
