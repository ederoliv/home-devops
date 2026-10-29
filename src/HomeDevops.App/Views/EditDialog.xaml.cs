using System.Windows;
using HomeDevops.ViewModels;

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

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (((EditDialogViewModel)DataContext).RequestDelete())
        {
            DialogResult = true;
        }
    }
}
