using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeDevops.Models;
using HomeDevops.Services;

namespace HomeDevops.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const double AvatarCollapsed = 40;
    private const double AvatarExpanded = 28;

    private readonly ProjectStore _store;
    private readonly ProjectTransfer _transfer;
    private readonly ConfigService _config;
    private readonly ThemeService _theme;
    private readonly IDialogService _dialogs;
    private bool _isSidebarCollapsed;

    public MainViewModel(
        ProjectStore store,
        ProjectTransfer transfer,
        ConfigService config,
        ThemeService theme,
        IDialogService dialogs)
    {
        _store = store;
        _transfer = transfer;
        _config = config;
        _theme = theme;
        _dialogs = dialogs;
        _isSidebarCollapsed = config.Get<SidebarSettings>(ConfigSections.Sidebar).Collapsed;
    }

    public ObservableCollection<Project> Projects { get; } = [];

    [ObservableProperty]
    private Project? _selectedProject;

    [ObservableProperty]
    private ItemFilter _filter = ItemFilter.All;

    /// <summary>Itens do projeto selecionado, já filtrados por <see cref="Filter"/>.</summary>
    [ObservableProperty]
    private ICollectionView? _itemsView;

    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (!SetProperty(ref _isSidebarCollapsed, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsSidebarExpanded));
            OnPropertyChanged(nameof(AvatarSize));
            _config.Set(ConfigSections.Sidebar, new SidebarSettings { Collapsed = value });
        }
    }

    public bool IsSidebarExpanded => !_isSidebarCollapsed;

    public double AvatarSize => _isSidebarCollapsed ? AvatarCollapsed : AvatarExpanded;

    /// <summary>Carrega os projetos da pasta (na abertura do app).</summary>
    public void Load() => Reload();

    /// <summary>Relê a pasta de projetos, mantendo a seleção. Devolve os arquivos ignorados.</summary>
    public IReadOnlyList<string> Reload()
    {
        _store.Flush();
        var result = _store.LoadAll();
        var selectedId = SelectedProject?.Id;

        Projects.Clear();
        foreach (var project in result.Projects)
        {
            Hook(project);
            Projects.Add(project);
        }

        SelectedProject = Projects.FirstOrDefault(p => p.Id == selectedId) ?? Projects.FirstOrDefault();

        if (result.Skipped.Count > 0)
        {
            ShowToast($"Arquivos ignorados: {string.Join("; ", result.Skipped)}");
        }

        return result.Skipped;
    }

    partial void OnSelectedProjectChanged(Project? value) => RebuildItemsView();

    partial void OnFilterChanged(ItemFilter value) => ItemsView?.Refresh();

    private void Hook(Project project) => project.Changed += (_, _) => _store.MarkDirty(project);

    private void RebuildItemsView()
    {
        if (SelectedProject is null)
        {
            ItemsView = null;
            return;
        }

        var view = CollectionViewSource.GetDefaultView(SelectedProject.Items);
        view.Filter = Matches;
        if (view is ICollectionViewLiveShaping { CanChangeLiveFiltering: true } live)
        {
            // um item que muda de estado entra/sai do filtro "Concluídos" na hora
            live.LiveFilteringProperties.Clear();
            live.LiveFilteringProperties.Add(nameof(Item.State));
            live.IsLiveFiltering = true;
        }

        ItemsView = view;
    }

    private bool Matches(object candidate) =>
        candidate is Item item
        && Filter switch
        {
            ItemFilter.Feature => item.Type == ItemType.Feature,
            ItemFilter.Bug => item.Type == ItemType.Bug,
            ItemFilter.Done => item.State == ItemState.Done,
            _ => true,
        };

    [RelayCommand]
    private void NewProject()
    {
        var result = _dialogs.Edit(new EditRequest("Novo projeto", "Nome", ShowImage: true));
        if (result is null)
        {
            return;
        }

        var project = new Project { Name = result.Name, Description = result.Description, Image = result.Image };
        Hook(project);
        Projects.Add(project);
        SelectedProject = project;
        _store.MarkDirty(project);
    }

    [RelayCommand]
    private void EditProject()
    {
        if (SelectedProject is not { } project)
        {
            return;
        }

        var result = _dialogs.Edit(new EditRequest("Editar projeto", "Nome", project.Name, project.Description, project.Image, ShowImage: true));
        if (result is null)
        {
            return;
        }

        project.Name = result.Name;
        project.Description = result.Description;
        project.Image = result.Image;
    }

    [RelayCommand]
    private void DeleteProject()
    {
        if (SelectedProject is not { } project
            || !_dialogs.Confirm($"Excluir o projeto \"{project.Name}\" e tudo dentro dele? O arquivo .hdp será apagado."))
        {
            return;
        }

        Projects.Remove(project);
        SelectedProject = Projects.FirstOrDefault();
        try
        {
            _store.Delete(project);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"Erro ao apagar o arquivo: {e.Message}");
        }
    }

    [RelayCommand]
    private void NewFeature() => NewItem(ItemType.Feature);

    [RelayCommand]
    private void NewBug() => NewItem(ItemType.Bug);

    private void NewItem(ItemType type)
    {
        if (SelectedProject is not { } project)
        {
            return;
        }

        var label = type == ItemType.Feature ? "Nova feature" : "Novo bug";
        var result = _dialogs.Edit(new EditRequest(label, "Título", ShowDefaultTask: true));
        if (result is null)
        {
            return;
        }

        var item = new Item { Type = type, Title = result.Name, Description = result.Description, IsExpanded = true };
        if (result.CreateDefaultTask)
        {
            item.AddTask(result.Name);
        }

        project.Items.Insert(0, item);
    }

    [RelayCommand]
    private void EditItem(Item? item)
    {
        if (item is null)
        {
            return;
        }

        var label = item.Type == ItemType.Feature ? "Editar feature" : "Editar bug";
        var result = _dialogs.Edit(new EditRequest(label, "Título", item.Title, item.Description));
        if (result is null)
        {
            return;
        }

        item.Title = result.Name;
        item.Description = result.Description;
    }

    [RelayCommand]
    private void DeleteItem(Item? item)
    {
        if (item is not null && SelectedProject is { } project && _dialogs.Confirm("Excluir este item?"))
        {
            project.Items.Remove(item);
        }
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void OpenSettings() =>
        _dialogs.ShowSettings(new SettingsViewModel(this, _store, _transfer, _config, _theme, _dialogs));
}
