using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.Models;

public partial class TaskItem : TrackedObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _done;

    /// <summary>Disparado quando <see cref="Done"/> muda, para o <see cref="Item"/> recalcular o estado.</summary>
    internal event Action<TaskItem>? DoneToggled;

    partial void OnTitleChanged(string value) => RaiseChanged();

    partial void OnDoneChanged(bool value)
    {
        RaiseChanged();
        DoneToggled?.Invoke(this);
    }
}
