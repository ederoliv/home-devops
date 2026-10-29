using System.Text.RegularExpressions;

namespace HomeDevops.Services;

/// <summary>Regras de nome dos arquivos .hdp.</summary>
public static partial class FileNames
{
    public const string Extension = ".hdp";

    private const int MaxLength = 80;
    private static readonly char[] InvalidChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    [GeneratedRegex(@"^ \(\d+\)$")]
    private static partial Regex CounterSuffix();

    /// <summary>Transforma o nome do projeto em um nome de arquivo seguro (sem a extensão).</summary>
    public static string Sanitize(string? name)
    {
        var chars = (name ?? "").Select(c => char.IsControl(c) || InvalidChars.Contains(c) ? '_' : c).ToArray();
        var clean = new string(chars).Trim().TrimEnd('.').Trim();
        if (clean.Length > MaxLength)
        {
            clean = clean[..MaxLength].TrimEnd(' ', '.');
        }

        if (clean.Length == 0)
        {
            return "Projeto";
        }

        return Reserved.Contains(clean) ? "_" + clean : clean;
    }

    /// <summary><c>dir/base.hdp</c> ou, se já existir, <c>dir/base (2).hdp</c>, <c>(3)</c>...</summary>
    public static string UniquePath(string directory, string baseName)
    {
        var candidate = Path.Combine(directory, baseName + Extension);
        for (var n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(directory, $"{baseName} ({n}){Extension}");
        }

        return candidate;
    }

    /// <summary>"Nome" e "Nome (2)" contam como o mesmo nome base; assim renomear não troca o arquivo à toa.</summary>
    public static bool StemMatches(string fileName, string baseName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.Equals(baseName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
            && CounterSuffix().IsMatch(stem[baseName.Length..]);
    }

    public static bool IsProjectFile(string path) =>
        Path.GetExtension(path).Equals(Extension, StringComparison.OrdinalIgnoreCase);
}
