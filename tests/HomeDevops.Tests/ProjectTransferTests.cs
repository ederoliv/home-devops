using System.IO.Compression;
using System.Text.Json.Nodes;
using HomeDevops.Models;
using HomeDevops.Services;

namespace HomeDevops.Tests;

public class ProjectTransferTests
{
    private static string ProjectJson(string id, string name, string description = "") =>
        $$"""{ "version": 1, "id": "{{id}}", "name": "{{name}}", "description": "{{description}}", "createdAt": "2026-01-01T00:00:00Z", "items": [] }""";

    private static string IdOf(string path) => JsonNode.Parse(File.ReadAllText(path))!["id"]!.GetValue<string>();

    [Fact]
    public void Import_copies_the_file_into_the_projects_folder()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        var outside = Path.Combine(temp.Sub("outside"), "qualquer-nome.hdp");
        File.WriteAllText(outside, ProjectJson("id-1", "Importado"));

        var report = new ProjectTransfer(new ProjectStore(projects)).Import([outside]);

        Assert.Equal(1, report.Count);
        Assert.Empty(report.Skipped);
        Assert.True(File.Exists(Path.Combine(projects, "Importado.hdp")));
        Assert.True(File.Exists(outside));
    }

    [Fact]
    public void Import_never_overwrites_and_gives_a_copy_a_new_id()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        File.WriteAllText(Path.Combine(projects, "Meu.hdp"), ProjectJson("same-id", "Meu", "original"));
        var incoming = Path.Combine(temp.Sub("in"), "x.hdp");
        File.WriteAllText(incoming, ProjectJson("same-id", "Meu", "vindo de fora"));

        var report = new ProjectTransfer(new ProjectStore(projects)).Import([incoming]);

        Assert.Equal(1, report.Count);
        Assert.Contains("original", File.ReadAllText(Path.Combine(projects, "Meu.hdp")));
        var copy = Path.Combine(projects, "Meu (2).hdp");
        Assert.True(File.Exists(copy));
        Assert.NotEqual("same-id", IdOf(copy));
        Assert.Equal(2, new ProjectStore(projects).LoadAll().Projects.Count);
    }

    [Fact]
    public void Import_skips_invalid_files_and_reports_them()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        var bad = Path.Combine(temp.Sub("in"), "ruim.hdp");
        File.WriteAllText(bad, "[1, 2, 3]");
        var good = Path.Combine(temp.Path, "in", "bom.hdp");
        File.WriteAllText(good, ProjectJson("ok", "Bom"));

        var report = new ProjectTransfer(new ProjectStore(projects)).Import([bad, good]);

        Assert.Equal(1, report.Count);
        Assert.StartsWith("ruim.hdp", Assert.Single(report.Skipped));
    }

    [Fact]
    public void Export_copies_with_a_unique_name_and_keeps_existing_files()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        var dest = temp.Sub("dest");
        var store = new ProjectStore(projects);
        var project = new Project { Name = "Exp" };
        store.Save(project);
        var transfer = new ProjectTransfer(store);

        var first = transfer.Export(project, dest);
        var second = transfer.Export(project, dest);

        Assert.Equal("Exp.hdp", first);
        Assert.Equal("Exp (2).hdp", second);
        Assert.Equal(project.Id, IdOf(Path.Combine(dest, "Exp.hdp")));
    }

    [Fact]
    public void Export_flushes_pending_changes_first()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        var dest = temp.Sub("dest");
        var store = new ProjectStore(projects, new HoldingContext());
        var project = new Project { Name = "Pendente" };
        store.Save(project);
        project.Description = "editado agora";
        store.MarkDirty(project);

        new ProjectTransfer(store).Export(project, dest);

        Assert.Contains("editado agora", File.ReadAllText(Path.Combine(dest, "Pendente.hdp")));
    }

    [Fact]
    public void Backup_zips_every_project_at_the_root_and_restore_roundtrips()
    {
        using var temp = new TempFolder();
        var source = temp.Sub("source");
        var store = new ProjectStore(source);
        store.Save(new Project { Name = "Um" });
        store.Save(new Project { Name = "Dois" });
        File.WriteAllText(Path.Combine(source, "notas.txt"), "não entra");
        var zipPath = Path.Combine(temp.Path, "backup.zip");

        var count = new ProjectTransfer(store).CreateBackup(zipPath);

        Assert.Equal(2, count);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Equal(["Dois.hdp", "Um.hdp"], zip.Entries.Select(e => e.FullName).Order());
        }

        var target = temp.Sub("target");
        var report = new ProjectTransfer(new ProjectStore(target)).RestoreBackup(zipPath);

        Assert.Equal(2, report.Count);
        Assert.Equal(["Dois", "Um"], new ProjectStore(target).LoadAll().Projects.Select(p => p.Name).Order());
    }

    [Fact]
    public void Restore_replaces_a_project_with_the_same_id_keeping_its_current_file_name()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        File.WriteAllText(Path.Combine(projects, "Renomeado.hdp"), ProjectJson("p1", "Renomeado", "versão nova"));
        var zipPath = Path.Combine(temp.Path, "old.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("NomeAntigo.hdp");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(ProjectJson("p1", "NomeAntigo", "versão do backup"));
        }

        var report = new ProjectTransfer(new ProjectStore(projects)).RestoreBackup(zipPath);

        Assert.Equal(1, report.Count);
        Assert.Equal(["Renomeado.hdp"], Directory.GetFiles(projects, "*.hdp").Select(Path.GetFileName));
        Assert.Contains("versão do backup", File.ReadAllText(Path.Combine(projects, "Renomeado.hdp")));
    }

    [Fact]
    public void Restore_ignores_folders_other_files_and_invalid_entries_and_flattens_paths()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        var zipPath = Path.Combine(temp.Path, "misto.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            void Add(string name, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(content);
            }

            Add("sub/dir/Aninhado.hdp", ProjectJson("n1", "Aninhado"));
            Add("..\\fora.hdp", ProjectJson("n2", "Escapou"));
            Add("leia-me.txt", "nada");
            Add("quebrado.hdp", "isso não é json");
            zip.CreateEntry("pasta/");
        }

        var report = new ProjectTransfer(new ProjectStore(projects)).RestoreBackup(zipPath);

        Assert.Equal(2, report.Count);
        Assert.StartsWith("quebrado.hdp", Assert.Single(report.Skipped));
        Assert.True(File.Exists(Path.Combine(projects, "Aninhado.hdp")));
        Assert.False(File.Exists(Path.Combine(temp.Path, "fora.hdp")));
        Assert.All(Directory.GetFiles(projects), f => Assert.Equal(projects, Path.GetDirectoryName(f)));
    }

    [Fact]
    public void Restore_of_a_file_that_is_not_a_zip_throws_and_changes_nothing()
    {
        using var temp = new TempFolder();
        var projects = temp.Sub("projects");
        File.WriteAllText(Path.Combine(projects, "Fica.hdp"), ProjectJson("k", "Fica"));
        var notZip = Path.Combine(temp.Path, "fake.zip");
        File.WriteAllText(notZip, "não sou um zip");

        Assert.ThrowsAny<InvalidDataException>(() => new ProjectTransfer(new ProjectStore(projects)).RestoreBackup(notZip));

        Assert.Equal(["Fica.hdp"], Directory.GetFiles(projects).Select(Path.GetFileName));
    }

    private sealed class HoldingContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // nunca executa: simula "a gravação ainda não aconteceu"
        }
    }
}
