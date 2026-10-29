using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeDevops.Models;

/// <summary>Uma feature ou um bug de um projeto.</summary>
public partial class Item : TrackedObject
{
    public Item()
    {
        Tasks.CollectionChanged += OnTasksCollectionChanged;
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public ItemType Type { get; set; }

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private ItemState _state = ItemState.Todo;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ObservableCollection<TaskItem> Tasks { get; } = [];

    [JsonIgnore]
    public int TaskCount => Tasks.Count;

    [JsonIgnore]
    public int DoneCount => Tasks.Count(t => t.Done);

    // estado só de tela, não vai para o arquivo
    [ObservableProperty]
    [property: JsonIgnore]
    private bool _isExpanded;

    [ObservableProperty]
    [property: JsonIgnore]
    private string _newTaskTitle = "";

    partial void OnTitleChanged(string value) => RaiseChanged();

    partial void OnDescriptionChanged(string value) => RaiseChanged();

    partial void OnStateChanged(ItemState value) => RaiseChanged();

    public TaskItem AddTask(string title)
    {
        var task = new TaskItem { Title = title };
        Tasks.Add(task);
        if (State == ItemState.Done)
        {
            State = ItemState.Doing;
        }

        return task;
    }

    /// <summary>Remover uma tarefa não recalcula o estado do item.</summary>
    [RelayCommand]
    public void RemoveTask(TaskItem? task)
    {
        if (task is not null)
        {
            Tasks.Remove(task);
        }
    }

    [RelayCommand]
    private void SubmitTask()
    {
        var title = NewTaskTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        AddTask(title);
        NewTaskTitle = "";
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (TaskItem task in e.OldItems ?? Array.Empty<TaskItem>())
        {
            task.Changed -= OnTaskChanged;
            task.DoneToggled -= OnTaskDoneToggled;
        }

        foreach (TaskItem task in e.NewItems ?? Array.Empty<TaskItem>())
        {
            task.Changed += OnTaskChanged;
            task.DoneToggled += OnTaskDoneToggled;
        }

        NotifyProgress();
        RaiseChanged();
    }

    private void OnTaskChanged(object? sender, EventArgs e) => RaiseChanged();

    /// <summary>
    /// Todas marcadas -> Concluído. Senão, um item concluído volta para Em andamento, e um item
    /// A fazer vai para Em andamento quando uma tarefa é marcada. Desmarcar nunca volta para A fazer.
    /// </summary>
    private void OnTaskDoneToggled(TaskItem task)
    {
        if (Tasks.All(t => t.Done))
        {
            State = ItemState.Done;
        }
        else if (State == ItemState.Done || (State == ItemState.Todo && task.Done))
        {
            State = ItemState.Doing;
        }

        NotifyProgress();
    }

    private void NotifyProgress()
    {
        OnPropertyChanged(nameof(TaskCount));
        OnPropertyChanged(nameof(DoneCount));
    }
}
