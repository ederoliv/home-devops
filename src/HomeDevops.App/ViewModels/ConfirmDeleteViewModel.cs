using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.ViewModels;

/// <summary>Confirmação de exclusão: só libera o botão quando o nome é digitado igual.</summary>
public partial class ConfirmDeleteViewModel(string kind, string name, string warning) : ObservableObject
{
    public string Heading { get; } = $"Excluir {kind}";

    public string Prompt { get; } = $"Para confirmar, digite o nome da {kind}:";

    public string Name { get; } = name;

    public string Warning { get; } = warning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanConfirm))]
    private string _typed = "";

    public bool CanConfirm => Typed.Trim() == Name;
}
