using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeDevops.Models;

namespace HomeDevops.Services;

public sealed record LoadResult(IReadOnlyList<Project> Projects, IReadOnlyList<string> Skipped);

/// <summary>
/// Guarda cada projeto em um arquivo .hdp (JSON) dentro de uma única pasta.
/// As gravações pedidas por <see cref="MarkDirty"/> são agrupadas: várias mudanças seguidas no mesmo
/// projeto viram uma única escrita, feita logo depois no contexto de UI (ou na hora, sem contexto).
/// </summary>
public sealed class ProjectStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly object _gate = new();
    private readonly HashSet<Project> _dirty = [];
    private readonly SynchronizationContext? _ui;
    private bool _flushScheduled;

    public ProjectStore(string folder, SynchronizationContext? ui = null)
    {
        Folder = folder;
        _ui = ui;
        Directory.CreateDirectory(folder);
    }

    public string Folder { get; }

    /// <summary>Falha ao gravar um projeto (disco cheio, arquivo bloqueado...).</summary>
    public event Action<string>? SaveFailed;

    /// <summary>Lê todos os .hdp da pasta. Arquivos inválidos ou com id repetido são ignorados e reportados.</summary>
    public LoadResult LoadAll()
    {
        var projects = new List<Project>();
        var skipped = new List<string>();
        var idToFile = new Dictionary<string, string>();

        foreach (var path in EnumerateProjectFiles())
        {
            var file = Path.GetFileName(path);
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path));
                if (Validate(node) is { } error)
                {
                    skipped.Add($"{file}: {error}");
                    continue;
                }

                var id = node!["id"]!.GetValue<string>();
                if (idToFile.TryGetValue(id, out var first))
                {
                    skipped.Add($"{file}: mesmo id de {first}");
                    continue;
                }

                var project = node.Deserialize<Project>(HdpJson.Options)!;
                project.FileName = file;
                idToFile[id] = file;
                projects.Add(project);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                skipped.Add($"{file}: {e.Message}");
            }
        }

        return new LoadResult(projects.OrderBy(p => p.CreatedAt).ToList(), skipped);
    }

    /// <summary>Marca o projeto para ser gravado em breve.</summary>
    public void MarkDirty(Project project)
    {
        lock (_gate)
        {
            _dirty.Add(project);
            if (_flushScheduled)
            {
                return;
            }

            _flushScheduled = true;
        }

        if (_ui is null)
        {
            Flush();
        }
        else
        {
            _ui.Post(_ => Flush(), null);
        }
    }

    /// <summary>Grava agora tudo o que está pendente.</summary>
    public void Flush()
    {
        Project[] batch;
        lock (_gate)
        {
            batch = [.. _dirty];
            _dirty.Clear();
            _flushScheduled = false;
        }

        foreach (var project in batch)
        {
            try
            {
                Save(project);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                SaveFailed?.Invoke($"Erro ao salvar \"{project.Name}\": {e.Message}");
            }
        }
    }

    /// <summary>
    /// Grava o projeto. O arquivo leva o nome do projeto; se o nome mudou, o arquivo é renomeado.
    /// </summary>
    public void Save(Project project)
    {
        var baseName = FileNames.Sanitize(project.Name);
        var target = project.FileName is { } current && FileNames.StemMatches(current, baseName)
            ? Path.Combine(Folder, current)
            : FileNames.UniquePath(Folder, baseName);

        WriteAtomic(target, System.Text.Json.JsonSerializer.Serialize(project, HdpJson.Options));

        var previous = project.FileName;
        project.FileName = Path.GetFileName(target);
        if (previous is not null && !previous.Equals(project.FileName, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(Path.Combine(Folder, previous));
        }
    }

    public void Delete(Project project)
    {
        lock (_gate)
        {
            _dirty.Remove(project);
        }

        if (project.FileName is not null)
        {
            File.Delete(Path.Combine(Folder, project.FileName));
        }
    }

    internal IEnumerable<string> EnumerateProjectFiles() =>
        Directory.EnumerateFiles(Folder)
            .Where(FileNames.IsProjectFile)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase);

    /// <summary>Um projeto válido tem id (texto), name (texto) e items (lista). Retorna o motivo se não tiver.</summary>
    internal static string? Validate(JsonNode? node)
    {
        var ok = node is JsonObject obj
            && obj["id"] is JsonValue id && id.TryGetValue<string>(out _)
            && obj["name"] is JsonValue name && name.TryGetValue<string>(out _)
            && obj["items"] is JsonArray;
        return ok ? null : "não parece um projeto (faltam id, name ou items)";
    }

    /// <summary>Grava em um arquivo temporário e renomeia, para nunca deixar um arquivo pela metade.</summary>
    internal static void WriteAtomic(string path, string content)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, content, Utf8NoBom);
        File.Move(temp, path, overwrite: true);
    }
}
