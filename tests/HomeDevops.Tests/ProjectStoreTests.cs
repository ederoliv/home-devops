using System.Text.Json.Nodes;
using HomeDevops.Models;
using HomeDevops.Services;

namespace HomeDevops.Tests;

public class ProjectStoreTests
{
    // Arquivo exatamente como o app anterior (Tauri) gravava, para garantir compatibilidade.
    private const string LegacyFile = """
        {
          "version": 1,
          "id": "3f2c-legacy",
          "name": "Teste",
          "description": "Projeto antigo",
          "createdAt": "2026-10-29T02:09:57.123Z",
          "items": [
            {
              "id": "a1",
              "type": "feature",
              "title": "Login",
              "description": "",
              "state": "doing",
              "createdAt": "2026-10-29T02:10:30.000Z",
              "tasks": [ { "id": "t1", "title": "Login", "done": true }, { "id": "t2", "title": "Tela", "done": false } ]
            },
            { "id": "b1", "type": "bug", "title": "Erro", "description": "x", "state": "done", "createdAt": "2026-10-29T02:11:00.000Z", "tasks": [] }
          ]
        }
        """;

    private static Project NewProject(string name) => new() { Name = name, Description = "d" };

    [Fact]
    public void Save_creates_a_file_named_after_the_project_and_roundtrips()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var project = NewProject("Meu App");
        var item = new Item { Type = ItemType.Bug, Title = "Crash", State = ItemState.Doing };
        item.AddTask("Crash").Done = false;
        project.Items.Add(item);
        project.Image = "data:image/png;base64,AAAA";

        store.Save(project);

        Assert.Equal("Meu App.hdp", project.FileName);
        var loaded = Assert.Single(new ProjectStore(temp.Path).LoadAll().Projects);
        Assert.Equal(project.Id, loaded.Id);
        Assert.Equal("data:image/png;base64,AAAA", loaded.Image);
        var loadedItem = Assert.Single(loaded.Items);
        Assert.Equal((ItemType.Bug, ItemState.Doing, "Crash"), (loadedItem.Type, loadedItem.State, loadedItem.Title));
        Assert.Equal("Crash", Assert.Single(loadedItem.Tasks).Title);
    }

    [Fact]
    public void Saved_json_uses_lowercase_enums_and_camelCase_names()
    {
        using var temp = new TempFolder();
        var project = NewProject("P");
        project.Items.Add(new Item { Type = ItemType.Feature, State = ItemState.Done, Title = "Ação" });
        new ProjectStore(temp.Path).Save(project);

        var text = File.ReadAllText(Path.Combine(temp.Path, "P.hdp"));
        var json = JsonNode.Parse(text)!;

        Assert.Equal(1, json["version"]!.GetValue<int>());
        Assert.Equal("feature", json["items"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("done", json["items"]![0]!["state"]!.GetValue<string>());
        Assert.NotNull(json["createdAt"]);
        Assert.Null(json["image"]);
        Assert.Contains("Ação", text);
        Assert.DoesNotContain("fileName", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isExpanded", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loads_files_written_by_the_previous_app()
    {
        using var temp = new TempFolder();
        File.WriteAllText(Path.Combine(temp.Path, "Teste.hdp"), LegacyFile);

        var result = new ProjectStore(temp.Path).LoadAll();

        var project = Assert.Single(result.Projects);
        Assert.Empty(result.Skipped);
        Assert.Equal("Teste", project.Name);
        Assert.Equal("Teste.hdp", project.FileName);
        Assert.Equal(2, project.Items.Count);
        Assert.Equal(ItemState.Doing, project.Items[0].State);
        Assert.Equal(1, project.Items[0].DoneCount);
        Assert.Equal((0, 1, 1, 1), (project.FeaturesDone, project.FeaturesTotal, project.BugsDone, project.BugsTotal));
        Assert.Equal(DateTimeOffset.Parse("2026-10-29T02:09:57.123Z"), project.CreatedAt);
    }

    [Fact]
    public void Loading_does_not_trigger_state_rules_or_saves()
    {
        using var temp = new TempFolder();
        File.WriteAllText(Path.Combine(temp.Path, "Teste.hdp"), LegacyFile);

        var project = Assert.Single(new ProjectStore(temp.Path).LoadAll().Projects);

        Assert.Equal(ItemState.Doing, project.Items[0].State);
        Assert.Equal(ItemState.Done, project.Items[1].State);
    }

    [Fact]
    public void Renaming_a_project_renames_its_file()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var project = NewProject("Antigo");
        store.Save(project);

        project.Name = "Novo";
        store.Save(project);

        Assert.Equal("Novo.hdp", project.FileName);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Antigo.hdp")));
        Assert.True(File.Exists(Path.Combine(temp.Path, "Novo.hdp")));
    }

    [Fact]
    public void Projects_with_the_same_name_get_numbered_files_and_keep_them()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var a = NewProject("App");
        var b = NewProject("App");

        store.Save(a);
        store.Save(b);
        store.Save(b);

        Assert.Equal("App.hdp", a.FileName);
        Assert.Equal("App (2).hdp", b.FileName);
        Assert.Equal(2, Directory.GetFiles(temp.Path, "*.hdp").Length);
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var project = NewProject("Some");
        store.Save(project);

        store.Delete(project);

        Assert.Empty(Directory.GetFiles(temp.Path, "*.hdp"));
    }

    [Fact]
    public void Invalid_and_duplicate_files_are_skipped_and_reported_but_never_touched()
    {
        using var temp = new TempFolder();
        File.WriteAllText(Path.Combine(temp.Path, "a.hdp"), LegacyFile);
        File.WriteAllText(Path.Combine(temp.Path, "b-duplicado.hdp"), LegacyFile);
        File.WriteAllText(Path.Combine(temp.Path, "c-quebrado.hdp"), "{ isto não é json");
        File.WriteAllText(Path.Combine(temp.Path, "d-sem-items.hdp"), """{ "id": "x", "name": "Y" }""");
        File.WriteAllText(Path.Combine(temp.Path, "e.txt"), "ignorado pela extensão");

        var result = new ProjectStore(temp.Path).LoadAll();

        Assert.Single(result.Projects);
        Assert.Equal(3, result.Skipped.Count);
        Assert.Contains(result.Skipped, s => s.StartsWith("b-duplicado.hdp"));
        Assert.Contains(result.Skipped, s => s.StartsWith("c-quebrado.hdp"));
        Assert.Contains(result.Skipped, s => s.StartsWith("d-sem-items.hdp"));
        Assert.Equal("{ isto não é json", File.ReadAllText(Path.Combine(temp.Path, "c-quebrado.hdp")));
    }

    [Fact]
    public void Projects_are_ordered_by_creation_date()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var late = new Project { Name = "A", CreatedAt = DateTimeOffset.Parse("2026-03-01T00:00:00Z") };
        var early = new Project { Name = "Z", CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") };
        store.Save(late);
        store.Save(early);

        var names = store.LoadAll().Projects.Select(p => p.Name);

        Assert.Equal(["Z", "A"], names);
    }

    [Fact]
    public void MarkDirty_without_a_ui_context_saves_immediately_and_coalesces_with_one()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var project = NewProject("Auto");

        store.MarkDirty(project);

        Assert.True(File.Exists(Path.Combine(temp.Path, "Auto.hdp")));
    }

    [Fact]
    public void MarkDirty_with_a_ui_context_groups_changes_into_one_flush()
    {
        using var temp = new TempFolder();
        var context = new RecordingContext();
        var store = new ProjectStore(temp.Path, context);
        var project = NewProject("Lote");

        store.MarkDirty(project);
        store.MarkDirty(project);
        store.MarkDirty(project);

        Assert.Single(context.Posted);
        Assert.False(File.Exists(Path.Combine(temp.Path, "Lote.hdp")));
        context.RunAll();
        Assert.True(File.Exists(Path.Combine(temp.Path, "Lote.hdp")));
    }

    [Fact]
    public void A_pending_save_of_a_deleted_project_does_not_resurrect_the_file()
    {
        using var temp = new TempFolder();
        var context = new RecordingContext();
        var store = new ProjectStore(temp.Path, context);
        var project = NewProject("Fantasma");
        store.Save(project);

        store.MarkDirty(project);
        store.Delete(project);
        context.RunAll();

        Assert.Empty(Directory.GetFiles(temp.Path, "*.hdp"));
    }

    [Fact]
    public void A_failed_save_is_reported_instead_of_throwing()
    {
        using var temp = new TempFolder();
        var store = new ProjectStore(temp.Path);
        var project = NewProject("Bloqueado");
        store.Save(project);
        string? message = null;
        store.SaveFailed += m => message = m;

        using var lockHandle = new FileStream(Path.Combine(temp.Path, "Bloqueado.hdp"), FileMode.Open, FileAccess.Read, FileShare.None);
        store.MarkDirty(project);

        Assert.NotNull(message);
        Assert.Contains("Bloqueado", message);
    }

    private sealed class RecordingContext : SynchronizationContext
    {
        public List<(SendOrPostCallback Callback, object? State)> Posted { get; } = [];

        public override void Post(SendOrPostCallback d, object? state) => Posted.Add((d, state));

        public void RunAll()
        {
            foreach (var (callback, state) in Posted.ToList())
            {
                callback(state);
            }

            Posted.Clear();
        }
    }
}
