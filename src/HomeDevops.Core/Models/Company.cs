using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeDevops.Models;

/// <summary>Uma empresa. Todo projeto pertence a uma (<see cref="Project.CompanyId"/>).</summary>
public partial class Company : TrackedObject
{
    /// <summary>Nome da empresa que recebe os projetos que não apontam para nenhuma empresa existente.</summary>
    public const string DefaultName = "Sem empresa";

    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    /// <summary>Logo como data URL (PNG em base64), ou nulo.</summary>
    [ObservableProperty]
    private string? _image;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // calculado pelo MainViewModel; não vai para o arquivo
    [ObservableProperty]
    [property: JsonIgnore]
    private int _projectCount;

    partial void OnNameChanged(string value) => RaiseChanged();

    partial void OnDescriptionChanged(string value) => RaiseChanged();

    partial void OnImageChanged(string? value) => RaiseChanged();
}
