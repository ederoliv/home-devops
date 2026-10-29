using System.Windows;

namespace HomeDevops.Views;

public partial class ConfirmDeleteDialog : Window
{
    public ConfirmDeleteDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TypedBox.Focus();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
