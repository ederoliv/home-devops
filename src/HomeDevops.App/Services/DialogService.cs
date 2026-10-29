using System.Windows;
using System.Windows.Interop;
using HomeDevops.ViewModels;
using HomeDevops.Views;
using Microsoft.Win32;

namespace HomeDevops.Services;

public sealed class DialogService : IDialogService
{
    private const string AppName = "Home DevOps";

    private static Window? Owner =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;

    public bool Confirm(string message)
    {
        var result = Owner is { } owner
            ? MessageBox.Show(owner, message, AppName, MessageBoxButton.YesNo, MessageBoxImage.Question)
            : MessageBox.Show(message, AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public EditResult? Edit(EditRequest request)
    {
        var viewModel = new EditDialogViewModel(request, this);
        var window = new EditDialog { DataContext = viewModel, Owner = Owner };
        return window.ShowDialog() == true ? viewModel.ToResult() : null;
    }

    public void ShowSettings(SettingsViewModel viewModel) =>
        new SettingsWindow { DataContext = viewModel, Owner = Owner }.ShowDialog();

    public string? PickSaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = defaultFileName, OverwritePrompt = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public string? PickOpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public IReadOnlyList<string> PickOpenFiles(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true, Multiselect = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileNames : [];
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog(Owner) == true ? dialog.FolderName : null;
    }

    public string? PickColor(string currentHex)
    {
        var start = ThemePalette.IsValidHex(currentHex) ? currentHex : AppearanceSettings.DefaultAccent;
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.ColorTranslator.FromHtml(start),
        };

        var result = Owner is { } owner
            ? dialog.ShowDialog(new Win32Owner(new WindowInteropHelper(owner).Handle))
            : dialog.ShowDialog();
        return result == System.Windows.Forms.DialogResult.OK
            ? $"#{dialog.Color.R:x2}{dialog.Color.G:x2}{dialog.Color.B:x2}"
            : null;
    }

    public string? PickLogo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolher logo",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico;*.webp|Todos os arquivos|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(Owner) != true)
        {
            return null;
        }

        var logo = ImageService.LoadAsLogoDataUrl(dialog.FileName);
        if (logo is null)
        {
            MessageBox.Show(Owner!, "Não foi possível ler essa imagem.", AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        return logo;
    }

    private sealed class Win32Owner(IntPtr handle) : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle => handle;
    }
}
