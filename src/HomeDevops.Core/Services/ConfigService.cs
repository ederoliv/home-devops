using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HomeDevops.Models;

namespace HomeDevops.Services;

/// <summary>
/// Configurações do app em <c>~/.homeDevops/config.json</c>: uma chave por seção, e só existe a chave
/// quando a seção precisa guardar algo. Seções desconhecidas são preservadas ao gravar.
/// </summary>
public sealed class ConfigService
{
    public const string FileName = "config.json";

    private readonly string _folder;
    private readonly string _path;
    private JsonObject _root = [];

    public ConfigService(string folder)
    {
        _folder = folder;
        _path = Path.Combine(folder, FileName);
        Load();
    }

    /// <summary>Lê a seção; se não existir (ou estiver inválida), devolve os valores padrão do tipo.</summary>
    public T Get<T>(string section)
        where T : new()
    {
        try
        {
            var node = _root[section];
            return node is null ? new T() : node.Deserialize<T>(HdpJson.Options) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public void Set<T>(string section, T value)
    {
        _root[section] = JsonSerializer.SerializeToNode(value, HdpJson.Options);
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            _root = JsonNode.Parse(File.ReadAllText(_path)) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            // arquivo corrompido: guarda uma cópia para não perder nada e recomeça vazio
            File.Copy(_path, _path + ".corrupt", overwrite: true);
        }
    }

    private void Save()
    {
        if (!Directory.Exists(_folder))
        {
            Directory.CreateDirectory(_folder);
            if (OperatingSystem.IsWindows())
            {
                File.SetAttributes(_folder, File.GetAttributes(_folder) | FileAttributes.Hidden);
            }
        }

        var temp = _path + ".tmp";
        File.WriteAllText(temp, _root.ToJsonString(HdpJson.Options), new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }
}

/// <summary>Nomes das seções do config.json.</summary>
public static class ConfigSections
{
    public const string Appearance = "appearance";
    public const string Sidebar = "sidebar";
}
