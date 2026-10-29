using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.ViewModels;

/// <summary>Base com o aviso temporário (~3 s) mostrado no canto da janela.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    protected ViewModelBase()
    {
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            Toast = null;
        };
    }

    [ObservableProperty]
    private string? _toast;

    public void ShowToast(string message)
    {
        Toast = message;
        _toastTimer.Stop();
        _toastTimer.Start();
    }
}
