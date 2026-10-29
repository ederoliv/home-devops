using System.Windows;

namespace HomeDevops.Views;

public partial class EditDialog : Window
{
    public EditDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
