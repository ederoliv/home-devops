using System.IO.Compression;
using HomeDevops.Models;
using HomeDevops.Services;

namespace HomeDevops.Tests;

public class CompanyTests
{
    [Fact]
    public void Load_without_a_file_returns_no_companies()
    {
        using var temp = new TempFolder();

        Assert.Empty(new CompanyStore(temp.Path).Load());
    }

    [Fact]
    public void Save_then_load_roundtrips_the_companies()
    {
        using var temp = new TempFolder();
        var store = new CompanyStore(temp.Path);
        var acme = new Company { Name = "Acme", Description = "Cliente", Image = "data:image/png;base64,AAAA" };

        store.Save([acme, new Company { Name = "Beta" }]);

        var loaded = store.Load();
        Assert.Equal(["Acme", "Beta"], loaded.Select(c => c.Name));
        Assert.Equal(acme.Id, loaded[0].Id);
        Assert.Equal("Cliente", loaded[0].Description);
        Assert.Equal("data:image/png;base64,AAAA", loaded[0].Image);
    }

    [Fact]
    public void Project_count_is_not_written_to_the_file()
    {
        using var temp = new TempFolder();
        var store = new CompanyStore(temp.Path);

        store.Save([new Company { Name = "Acme", ProjectCount = 7 }]);

        Assert.DoesNotContain("projectCount", File.ReadAllText(store.Path));
    }

    [Fact]
    public void Corrupt_file_is_kept_aside_and_loads_empty()
    {
        using var temp = new TempFolder();
        var store = new CompanyStore(temp.Path);
        File.WriteAllText(store.Path, "{ não é json");

        Assert.Empty(store.Load());
        Assert.True(File.Exists(store.Path + ".corrupt"));
    }

    [Fact]
    public void Companies_without_name_or_with_repeated_id_are_dropped()
    {
        var json = """
            { "version": 1, "companies": [
              { "id": "a", "name": "Acme" },
              { "id": "a", "name": "Repetida" },
              { "id": "b", "name": "  " }
            ] }
            """;

        using var temp = new TempFolder();
        var store = new CompanyStore(temp.Path);
        File.WriteAllText(store.Path, json);

        Assert.Equal(["Acme"], store.Load().Select(c => c.Name));
    }

    [Fact]
    public void Project_keeps_its_company_id_and_old_files_have_none()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        store.Save(new Project { Name = "Novo", CompanyId = "c1" });
        File.WriteAllText(
            Path.Combine(temp.Path, "Antigo.hdp"),
            """{ "version": 1, "id": "old", "name": "Antigo", "items": [] }""");

        var loaded = store.LoadAll().Projects.ToDictionary(p => p.Name);

        Assert.Equal("c1", loaded["Novo"].CompanyId);
        Assert.Null(loaded["Antigo"].CompanyId);
    }

    [Fact]
    public void Companies_file_is_not_mistaken_for_a_project()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        new CompanyStore(temp.Path).Save([new Company { Name = "Acme" }]);

        var result = store.LoadAll();

        Assert.Empty(result.Projects);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void Backup_includes_companies_and_restore_merges_them_by_id()
    {
        using var temp = new TempFolder();
        var source = temp.Sub("source");
        var acme = new Company { Name = "Acme" };
        new CompanyStore(source).Save([acme, new Company { Name = "Beta" }]);
        var projects = new ProjectStore(source);
        projects.Save(new Project { Name = "Um", CompanyId = acme.Id });
        var zipPath = Path.Combine(temp.Path, "backup.zip");

        var count = new ProjectTransfer(projects).CreateBackup(zipPath);

        Assert.Equal(1, count);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            Assert.Contains(zip.Entries, e => e.FullName == CompanyStore.FileName);
        }

        var target = temp.Sub("target");
        var targetCompanies = new CompanyStore(target);
        targetCompanies.Save([new Company { Id = acme.Id, Name = "Nome velho" }, new Company { Name = "Local" }]);

        var report = new ProjectTransfer(new ProjectStore(target)).RestoreBackup(zipPath);

        Assert.Equal(1, report.Count);
        Assert.Empty(report.Skipped);
        Assert.Equal(["Acme", "Local", "Beta"], targetCompanies.Load().Select(c => c.Name));
    }
}
