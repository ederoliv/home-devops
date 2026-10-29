using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeDevops.Models;

namespace HomeDevops.Services;

public sealed record TransferReport(int Count, IReadOnlyList<string> Skipped);

/// <summary>Backup (zip com todos os .hdp) e importação/exportação de projetos avulsos.</summary>
public sealed class ProjectTransfer(ProjectStore store)
{
    /// <summary>
    /// Copia .hdp escolhidos pelo usuário para a pasta de projetos. Nunca sobrescreve: se o id já existe,
    /// o projeto entra como uma cópia com id novo.
    /// </summary>
    public TransferReport Import(IEnumerable<string> paths)
    {
        store.Flush();
        var ids = ExistingIds();
        var imported = 0;
        var skipped = new List<string>();

        foreach (var path in paths)
        {
            var label = Path.GetFileName(path);
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path));
                if (ProjectStore.Validate(node) is { } error)
                {
                    skipped.Add($"{label}: {error}");
                    continue;
                }

                var project = (JsonObject)node!;
                var id = project["id"]!.GetValue<string>();
                if (ids.ContainsKey(id))
                {
                    id = Guid.NewGuid().ToString();
                    project["id"] = id;
                }

                var target = FileNames.UniquePath(store.Folder, FileNames.Sanitize(project["name"]!.GetValue<string>()));
                ProjectStore.WriteAtomic(target, project.ToJsonString(HdpJson.Options));
                ids[id] = target;
                imported++;
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{label}: {e.Message}");
            }
        }

        return new TransferReport(imported, skipped);
    }

    /// <summary>Copia o .hdp do projeto para a pasta escolhida, sem sobrescrever. Retorna o nome criado.</summary>
    public string Export(Project project, string destinationFolder)
    {
        store.Flush();
        if (project.FileName is null)
        {
            throw new InvalidOperationException("O projeto ainda não foi salvo.");
        }

        if (!Directory.Exists(destinationFolder))
        {
            throw new DirectoryNotFoundException("A pasta de destino não existe.");
        }

        var target = FileNames.UniquePath(destinationFolder, Path.GetFileNameWithoutExtension(project.FileName));
        File.Copy(Path.Combine(store.Folder, project.FileName), target);
        return Path.GetFileName(target);
    }

    /// <summary>Zipa todos os .hdp (na raiz do zip). Retorna quantos entraram.</summary>
    public int CreateBackup(string zipPath)
    {
        store.Flush();
        var files = store.EnumerateProjectFiles().ToList();

        using var stream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            zip.CreateEntryFromFile(file, Path.GetFileName(file));
        }

        return files.Count;
    }

    /// <summary>
    /// Move os .hdp do zip para a pasta de projetos. Projeto com id já existente substitui o atual
    /// (mantendo o nome do arquivo dele); os demais entram como arquivos novos.
    /// </summary>
    public TransferReport RestoreBackup(string zipPath)
    {
        store.Flush();
        var ids = ExistingIds();
        var restored = 0;
        var skipped = new List<string>();

        using var zip = ZipFile.OpenRead(zipPath);
        foreach (var entry in zip.Entries)
        {
            // Name é só o último trecho do caminho: qualquer subpasta dentro do zip é descartada
            if (entry.Name.Length == 0 || !FileNames.IsProjectFile(entry.Name))
            {
                continue;
            }

            try
            {
                string text;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
                {
                    text = reader.ReadToEnd();
                }

                var node = JsonNode.Parse(text);
                if (ProjectStore.Validate(node) is { } error)
                {
                    skipped.Add($"{entry.Name}: {error}");
                    continue;
                }

                var id = node!["id"]!.GetValue<string>();
                var target = ids.TryGetValue(id, out var existing)
                    ? existing
                    : FileNames.UniquePath(store.Folder, FileNames.Sanitize(Path.GetFileNameWithoutExtension(entry.Name)));
                ProjectStore.WriteAtomic(target, text);
                ids[id] = target;
                restored++;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException or UnauthorizedAccessException)
            {
                skipped.Add($"{entry.Name}: {e.Message}");
            }
        }

        return new TransferReport(restored, skipped);
    }

    /// <summary>id do projeto -> arquivo onde ele está hoje.</summary>
    private Dictionary<string, string> ExistingIds()
    {
        var map = new Dictionary<string, string>();
        foreach (var path in store.EnumerateProjectFiles())
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(path));
                if (ProjectStore.Validate(node) is null)
                {
                    map[node!["id"]!.GetValue<string>()] = path;
                }
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                // arquivo ilegível: não conta como projeto existente
            }
        }

        return map;
    }
}
