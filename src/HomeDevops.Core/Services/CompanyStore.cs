using System.Text.Json;
using HomeDevops.Models;

namespace HomeDevops.Services;

/// <summary>
/// Guarda todas as empresas em <c>companies.json</c>, na mesma pasta dos projetos (assim o backup leva junto).
/// </summary>
public sealed class CompanyStore
{
    public const string FileName = "companies.json";

    private sealed class CompaniesFile
    {
        public int Version { get; set; } = 1;

        public List<Company> Companies { get; set; } = [];
    }

    public CompanyStore(string folder)
    {
        Folder = folder;
        Path = System.IO.Path.Combine(folder, FileName);
        Directory.CreateDirectory(folder);
    }

    public string Folder { get; }

    public string Path { get; }

    /// <summary>Lê as empresas. Sem arquivo: lista vazia. Arquivo corrompido: guarda uma cópia e recomeça vazio.</summary>
    public IReadOnlyList<Company> Load()
    {
        if (!File.Exists(Path))
        {
            return [];
        }

        try
        {
            return Parse(File.ReadAllText(Path));
        }
        catch (JsonException)
        {
            File.Copy(Path, Path + ".corrupt", overwrite: true);
            return [];
        }
    }

    public void Save(IEnumerable<Company> companies) =>
        ProjectStore.WriteAtomic(Path, Serialize(companies));

    internal static string Serialize(IEnumerable<Company> companies) =>
        JsonSerializer.Serialize(new CompaniesFile { Companies = [.. companies] }, HdpJson.Options);

    /// <summary>Lê o conteúdo de um companies.json, ignorando empresas sem nome e ids repetidos.</summary>
    internal static IReadOnlyList<Company> Parse(string json)
    {
        var file = JsonSerializer.Deserialize<CompaniesFile>(json, HdpJson.Options) ?? new CompaniesFile();
        var seen = new HashSet<string>();
        return file.Companies
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) && seen.Add(c.Id))
            .ToList();
    }
}
