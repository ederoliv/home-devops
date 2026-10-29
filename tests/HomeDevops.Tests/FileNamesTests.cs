using HomeDevops.Services;

namespace HomeDevops.Tests;

public class FileNamesTests
{
    [Theory]
    [InlineData("Meu App", "Meu App")]
    [InlineData("  Meu App  ", "Meu App")]
    [InlineData("a<b>c:d\"e/f\\g|h?i*j", "a_b_c_d_e_f_g_h_i_j")]
    [InlineData("termina com ponto...", "termina com ponto")]
    [InlineData("", "Projeto")]
    [InlineData("   ", "Projeto")]
    [InlineData("...", "Projeto")]
    [InlineData("CON", "_CON")]
    [InlineData("nul", "_nul")]
    [InlineData("Ação & Reação", "Ação & Reação")]
    public void Sanitize_produces_safe_names(string input, string expected) =>
        Assert.Equal(expected, FileNames.Sanitize(input));

    [Fact]
    public void Sanitize_limits_length_and_does_not_leave_trailing_space()
    {
        var name = FileNames.Sanitize(new string('a', 79) + " bbb");
        Assert.Equal(79, name.Length);
    }

    [Fact]
    public void UniquePath_adds_a_counter_when_the_file_exists()
    {
        using var temp = new TempFolder();
        File.WriteAllText(Path.Combine(temp.Path, "App.hdp"), "{}");
        File.WriteAllText(Path.Combine(temp.Path, "App (2).hdp"), "{}");

        Assert.Equal(Path.Combine(temp.Path, "App (3).hdp"), FileNames.UniquePath(temp.Path, "App"));
        Assert.Equal(Path.Combine(temp.Path, "Outro.hdp"), FileNames.UniquePath(temp.Path, "Outro"));
    }

    [Theory]
    [InlineData("Meu App.hdp", "Meu App", true)]
    [InlineData("meu app.hdp", "Meu App", true)]
    [InlineData("Meu App (2).hdp", "Meu App", true)]
    [InlineData("Meu App (final).hdp", "Meu App", false)]
    [InlineData("Meu App 2.hdp", "Meu App", false)]
    [InlineData("Outro.hdp", "Meu App", false)]
    public void StemMatches_treats_numbered_copies_as_the_same_name(string file, string baseName, bool expected) =>
        Assert.Equal(expected, FileNames.StemMatches(file, baseName));
}
