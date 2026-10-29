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
    private readonly CompanyStore _companyStore;
    private readonly ProjectTransfer _transfer;
    private readonly ConfigService _config;
    private readonly ThemeService _theme;
    private readonly IDialogService _dialogs;
    private bool _isSidebarCollapsed;

    public MainViewModel(
        ProjectStore store,
        CompanyStore companyStore,
        ProjectTransfer transfer,
        ConfigService config,
        ThemeService theme,
        IDialogService dialogs)
    {
        _store = store;
        _companyStore = companyStore;
        _transfer = transfer;
        _config = config;
        _theme = theme;
        _dialogs = dialogs;
        _isSidebarCollapsed = config.Get<SidebarSettings>(ConfigSections.Sidebar).Collapsed;

        // visão própria (e não a padrão da coleção, que a janela de configurações também usa)
        CompanyProjects = new CollectionViewSource { Source = Projects }.View;
        CompanyProjects.Filter = candidate => candidate is Project p && SelectedCompany is { } c && p.CompanyId == c.Id;
        Projects.CollectionChanged += (_, _) => RefreshProjectCounts();
    }

    public ObservableCollection<Company> Companies { get; } = [];

    /// <summary>Todos os projetos, de todas as empresas.</summary>
    public ObservableCollection<Project> Projects { get; } = [];

    /// <summary>Projetos da empresa selecionada.</summary>
    public ICollectionView CompanyProjects { get; }

    [ObservableProperty]
    private Company? _selectedCompany;

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
        var companies = _companyStore.Load().OrderBy(c => c.CreatedAt).ToList();
        var result = _store.LoadAll();
        var selectedCompanyId = SelectedCompany?.Id;
        var selectedId = SelectedProject?.Id;

        // todo projeto precisa de uma empresa: os sem empresa (arquivos antigos ou importados) vão para a padrão
        var orphans = result.Projects.Where(p => companies.All(c => c.Id != p.CompanyId)).ToList();
        var companiesChanged = false;
        if (orphans.Count > 0)
        {
            var fallback = companies.FirstOrDefault(c => c.Name == Company.DefaultName);
            if (fallback is null)
            {
                fallback = new Company { Name = Company.DefaultName };
                companies.Add(fallback);
                companiesChanged = true;
            }

            foreach (var project in orphans)
            {
                project.CompanyId = fallback.Id;
            }
        }

        Projects.Clear();
        Companies.Clear();
        foreach (var company in companies)
        {
            Hook(company);
            Companies.Add(company);
        }

        foreach (var project in result.Projects)
        {
            Hook(project);
            Projects.Add(project);
        }

        if (companiesChanged)
        {
            SaveCompanies();
        }

        foreach (var project in orphans)
        {
            _store.MarkDirty(project);
        }

        SelectedCompany = Companies.FirstOrDefault(c => c.Id == selectedCompanyId) ?? Companies.FirstOrDefault();
        SelectedProject = CompanyProjects.Cast<Project>().FirstOrDefault(p => p.Id == selectedId)
            ?? CompanyProjects.Cast<Project>().FirstOrDefault();

        if (result.Skipped.Count > 0)
        {
            ShowToast($"Arquivos ignorados: {string.Join("; ", result.Skipped)}");
        }

        return result.Skipped;
    }

    partial void OnSelectedCompanyChanged(Company? value)
    {
        CompanyProjects.Refresh();
        SelectedProject = CompanyProjects.Cast<Project>().FirstOrDefault();
    }

    partial void OnSelectedProjectChanged(Project? value) => RebuildItemsView();

    partial void OnFilterChanged(ItemFilter value) => ItemsView?.Refresh();

    private void Hook(Project project) => project.Changed += (_, _) => _store.MarkDirty(project);

    private void Hook(Company company) => company.Changed += (_, _) => SaveCompanies();

    private void SaveCompanies()
    {
        try
        {
            _companyStore.Save(Companies);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"Erro ao salvar as empresas: {e.Message}");
        }
    }

    private void RefreshProjectCounts()
    {
        foreach (var company in Companies)
        {
            company.ProjectCount = Projects.Count(p => p.CompanyId == company.Id);
        }
    }

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
    private void NewCompany()
    {
        var result = _dialogs.Edit(new EditRequest("Nova empresa", "Nome", ShowImage: true));
        if (result is null)
        {
            return;
        }

        var company = new Company { Name = result.Name, Description = result.Description, Image = result.Image };
        Hook(company);
        Companies.Add(company);
        SaveCompanies();
        SelectedCompany = company;
    }

    [RelayCommand]
    private void EditCompany()
    {
        if (SelectedCompany is not { } company)
        {
            return;
        }

        var result = _dialogs.Edit(new EditRequest("Editar empresa", "Nome", company.Name, company.Description, company.Image, ShowImage: true));
        if (result is null)
        {
            return;
        }

        company.Name = result.Name;
        company.Description = result.Description;
        company.Image = result.Image;
    }

    [RelayCommand]
    private void DeleteCompany()
    {
        if (SelectedCompany is not { } company)
        {
            return;
        }

        var owned = Projects.Where(p => p.CompanyId == company.Id).ToList();
        var warning = owned.Count > 0
            ? $"Excluir a empresa \"{company.Name}\" e seus {owned.Count} projeto(s), com tudo dentro? Os arquivos .hdp serão apagados."
            : $"Excluir a empresa \"{company.Name}\"?";
        if (!_dialogs.Confirm(warning))
        {
            return;
        }

        foreach (var project in owned)
        {
            Projects.Remove(project);
            try
            {
                _store.Delete(project);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                ShowToast($"Erro ao apagar o arquivo: {e.Message}");
            }
        }

        Companies.Remove(company);
        SaveCompanies();
        SelectedCompany = Companies.FirstOrDefault();
    }

    [RelayCommand]
    private void NewProject()
    {
        if (SelectedCompany is not { } company)
        {
            return;
        }

        var result = _dialogs.Edit(new EditRequest("Novo projeto", "Nome", ShowImage: true));
        if (result is null)
        {
            return;
        }

        var project = new Project
        {
            CompanyId = company.Id,
            Name = result.Name,
            Description = result.Description,
            Image = result.Image,
        };
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
        SelectedProject = CompanyProjects.Cast<Project>().FirstOrDefault();
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
