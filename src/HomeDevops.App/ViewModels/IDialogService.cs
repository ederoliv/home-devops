namespace HomeDevops.ViewModels;

/// <summary>Configuração do formulário de criação/edição (projeto ou item).</summary>
public sealed record EditRequest(
    string Title,
    string NameLabel,
    string Name = "",
    string Description = "",
    string? Image = null,
    bool ShowImage = false,
    bool ShowDefaultTask = false);

public sealed record EditResult(string Name, string Description, string? Image, bool CreateDefaultTask);

/// <summary>Tudo o que os view models precisam pedir à interface (janelas, seletores de arquivo...).</summary>
public interface IDialogService
{
    bool Confirm(string message);

    EditResult? Edit(EditRequest request);

    void ShowSettings(SettingsViewModel viewModel);

    string? PickSaveFile(string title, string filter, string defaultFileName);

    string? PickOpenFile(string title, string filter);

    IReadOnlyList<string> PickOpenFiles(string title, string filter);

    string? PickFolder(string title);

    /// <summary>Abre o seletor de cor. Retorna "#rrggbb" ou nulo se cancelado.</summary>
    string? PickColor(string currentHex);

    /// <summary>Escolhe uma imagem e a converte na logo (data URL), ou nulo.</summary>
    string? PickLogo();
}
