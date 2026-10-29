using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeDevops.Models;
using HomeDevops.Services;

namespace HomeDevops.ViewModels;

public enum SettingsSection
{
    Appearance,
    Backup,
    Transfer,
}

public partial class SettingsViewModel : ViewModelBase
{
    private readonly MainViewModel _main;
    private readonly ProjectTransfer _transfer;
    private readonly ConfigService _config;
    private readonly ThemeService _theme;
    private readonly IDialogService _dialogs;

    public SettingsViewModel(
        MainViewModel main,
        ProjectStore store,
        ProjectTransfer transfer,
        ConfigService config,
        ThemeService theme,
        IDialogService dialogs)
    {
        _main = main;
        _transfer = transfer;
        _config = config;
        _theme = theme;
        _dialogs = dialogs;
        ProjectsFolder = store.Folder;

        var appearance = config.Get<AppearanceSettings>(ConfigSections.Appearance);
        _bg = appearance.Bg;
        _text = appearance.Text;
        _accent = appearance.Accent;
        _exportProject = main.SelectedProject;
    }

    public string ProjectsFolder { get; }

    public ObservableCollection<Project> Projects => _main.Projects;

    public bool HasProjects => Projects.Count > 0;

    [ObservableProperty]
    private SettingsSection _section = SettingsSection.Appearance;

    // ----- Aparência: cada cor vale assim que for um hexadecimal completo (#rrggbb) -----

    [ObservableProperty]
    private string _bg;

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private string _accent;

    partial void OnBgChanged(string value) => ApplyAppearance();

    partial void OnTextChanged(string value) => ApplyAppearance();

    partial void OnAccentChanged(string value) => ApplyAppearance();

    private void ApplyAppearance()
    {
        if (!ThemePalette.IsValidHex(Bg) || !ThemePalette.IsValidHex(Text) || !ThemePalette.IsValidHex(Accent))
        {
            return;
        }

        var settings = new AppearanceSettings { Bg = Bg, Text = Text, Accent = Accent };
        _theme.Apply(settings);
        Guard(() => _config.Set(ConfigSections.Appearance, settings));
    }

    [RelayCommand]
    private void PickColor(string which)
    {
        var current = which switch
        {
            nameof(Bg) => Bg,
            nameof(Text) => Text,
            _ => Accent,
        };

        if (_dialogs.PickColor(current) is not { } picked)
        {
            return;
        }

        switch (which)
        {
            case nameof(Bg): Bg = picked; break;
            case nameof(Text): Text = picked; break;
            default: Accent = picked; break;
        }
    }

    [RelayCommand]
    private void ResetAppearance()
    {
        Bg = AppearanceSettings.DefaultBg;
        Text = AppearanceSettings.DefaultText;
        Accent = AppearanceSettings.DefaultAccent;
    }

    // ----- Backup -----

    [RelayCommand]
    private void CreateBackup()
    {
        var file = _dialogs.PickSaveFile("Gerar backup", "ZIP|*.zip", $"HomeDevops-backup-{DateTime.Now:yyyy-MM-dd}.zip");
        if (file is null)
        {
            return;
        }

        Guard(() => ShowToast($"Backup gerado com {_transfer.CreateBackup(file)} projeto(s)"));
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        var file = _dialogs.PickOpenFile("Importar backup", "ZIP|*.zip");
        if (file is null
            || !_dialogs.Confirm("Projetos do backup que já existem aqui (mesmo id) vão SUBSTITUIR os atuais; os demais serão adicionados. Continuar?"))
        {
            return;
        }

        Guard(() =>
        {
            var report = _transfer.RestoreBackup(file);
            ReloadProjects();
            ShowReport("restaurado(s)", report);
        });
    }

    // ----- Importar/Exportar projeto -----

    [ObservableProperty]
    private Project? _exportProject;

    [RelayCommand]
    private void ImportProjects()
    {
        var files = _dialogs.PickOpenFiles("Importar projeto", "Home Devops Project|*.hdp");
        if (files.Count == 0)
        {
            return;
        }

        Guard(() =>
        {
            var report = _transfer.Import(files);
            ReloadProjects();
            ShowReport("importado(s)", report);
        });
    }

    [RelayCommand]
    private void ExportSelectedProject()
    {
        if (ExportProject is not { } project || _dialogs.PickFolder("Pasta de destino") is not { } folder)
        {
            return;
        }

        Guard(() => ShowToast($"Exportado como {_transfer.Export(project, folder)}"));
    }

    /// <summary>Relê a pasta e mantém no combo de exportação o mesmo projeto (agora uma nova instância).</summary>
    private void ReloadProjects()
    {
        var exportId = ExportProject?.Id;
        _main.Reload();
        OnPropertyChanged(nameof(HasProjects));
        ExportProject = Projects.FirstOrDefault(p => p.Id == exportId) ?? _main.SelectedProject;
    }

    private void ShowReport(string verb, TransferReport report)
    {
        var skipped = report.Skipped.Count > 0 ? $" · ignorados: {string.Join("; ", report.Skipped)}" : "";
        ShowToast($"{report.Count} projeto(s) {verb}{skipped}");
    }

    /// <summary>Erros de arquivo viram aviso na tela em vez de derrubar o app.</summary>
    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException
                                      or InvalidOperationException or JsonException)
        {
            ShowToast($"Erro: {e.Message}");
        }
    }
}
