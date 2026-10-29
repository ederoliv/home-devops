using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.Models;

/// <summary>Um projeto. É exatamente o que vai dentro de um arquivo .hdp.</summary>
public partial class Project : TrackedObject
{
    public Project()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    public int Version { get; set; } = 1;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    /// <summary>Logo como data URL (PNG em base64), ou nulo.</summary>
    [ObservableProperty]
    private string? _image;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ObservableCollection<Item> Items { get; } = [];

    /// <summary>Nome do arquivo .hdp onde o projeto está salvo (controlado pelo <c>ProjectStore</c>).</summary>
    [JsonIgnore]
    public string? FileName { get; set; }

    [JsonIgnore]
    public int ItemCount => Items.Count;

    [JsonIgnore]
    public int FeaturesDone => Items.Count(i => i.Type == ItemType.Feature && i.State == ItemState.Done);

    [JsonIgnore]
    public int FeaturesTotal => Items.Count(i => i.Type == ItemType.Feature);

    [JsonIgnore]
    public int BugsDone => Items.Count(i => i.Type == ItemType.Bug && i.State == ItemState.Done);

    [JsonIgnore]
    public int BugsTotal => Items.Count(i => i.Type == ItemType.Bug);

    partial void OnNameChanged(string value) => RaiseChanged();

    partial void OnDescriptionChanged(string value) => RaiseChanged();

    partial void OnImageChanged(string? value) => RaiseChanged();

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (Item item in e.OldItems ?? Array.Empty<Item>())
        {
            item.Changed -= OnItemChanged;
        }

        foreach (Item item in e.NewItems ?? Array.Empty<Item>())
        {
            item.Changed += OnItemChanged;
        }

        NotifyStats();
        RaiseChanged();
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        NotifyStats();
        RaiseChanged();
    }

    private void NotifyStats()
    {
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(FeaturesDone));
        OnPropertyChanged(nameof(FeaturesTotal));
        OnPropertyChanged(nameof(BugsDone));
        OnPropertyChanged(nameof(BugsTotal));
    }
}
