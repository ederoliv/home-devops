using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeDevops.ViewModels;

public partial class EditDialogViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;

    public EditDialogViewModel(EditRequest request, IDialogService dialogs)
    {
        _dialogs = dialogs;
        Title = request.Title;
        NameLabel = request.NameLabel;
        ShowImage = request.ShowImage;
        ShowDefaultTask = request.ShowDefaultTask;
        _name = request.Name;
        _description = request.Description;
        _image = request.Image;
    }

    public string Title { get; }

    public string NameLabel { get; }

    public bool ShowImage { get; }

    public bool ShowDefaultTask { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string? _image;

    /// <summary>Só vale quando <see cref="ShowDefaultTask"/>; começa marcada.</summary>
    [ObservableProperty]
    private bool _createDefaultTask = true;

    public bool CanSave => !string.IsNullOrWhiteSpace(Name);

    public EditResult ToResult() => new(Name.Trim(), Description.Trim(), Image, ShowDefaultTask && CreateDefaultTask);

    [RelayCommand]
    private void ChooseImage()
    {
        if (_dialogs.PickLogo() is { } logo)
        {
            Image = logo;
        }
    }

    [RelayCommand]
    private void RemoveImage() => Image = null;
}
