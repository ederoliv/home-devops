using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeDevops.ViewModels;

public partial class EditDialogViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly string _deleteKind;
    private readonly string _deleteWarning;
    private readonly string _originalName;
    private bool _deleteRequested;

    public EditDialogViewModel(EditRequest request, IDialogService dialogs)
    {
        _dialogs = dialogs;
        Title = request.Title;
        NameLabel = request.NameLabel;
        ShowImage = request.ShowImage;
        ShowDefaultTask = request.ShowDefaultTask;
        ShowDelete = request.ShowDelete;
        _deleteKind = request.DeleteKind;
        _deleteWarning = request.DeleteWarning;
        _originalName = request.Name;
        _name = request.Name;
        _description = request.Description;
        _image = request.Image;
    }

    public string Title { get; }

    public string NameLabel { get; }

    public bool ShowImage { get; }

    public bool ShowDefaultTask { get; }

    public bool ShowDelete { get; }

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

    public EditResult ToResult() =>
        new(Name.Trim(), Description.Trim(), Image, ShowDefaultTask && CreateDefaultTask, _deleteRequested);

    /// <summary>Pede a confirmação (digitar o nome original). Se confirmada, o resultado passa a ser "excluir".</summary>
    public bool RequestDelete()
    {
        _deleteRequested = _dialogs.ConfirmDelete(_deleteKind, _originalName, _deleteWarning);
        return _deleteRequested;
    }

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
