using System.Text.Json;
using WabbajackPreprocessor.Core.Settings;

namespace WabbajackPreprocessor.Core.Tests;

/// <summary>
/// Validates the parser against real compiler settings files the user drops into the
/// repo's git-ignored samples\ folder. Passes trivially when no samples are present.
/// </summary>
public class SampleFileTests
{
    private static string? FindSamplesDir()
    {
        for (var dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "WabbajackPreprocessor.sln")))
            {
                var samples = Path.Combine(dir, "samples");
                return Directory.Exists(samples) ? samples : null;
            }
        }
        return null;
    }

    [Fact]
    public void Samples_RoundTrip_Unchanged()
    {
        var samples = FindSamplesDir();
        if (samples is null)
            return;

        foreach (var path in Directory.EnumerateFiles(samples, "*.compiler_settings"))
        {
            var settings = CompilerSettingsFile.Load(path);
            Assert.False(string.IsNullOrEmpty(settings.Profile), $"{path}: Profile is empty");

            // Re-serializing an untouched file must reproduce it exactly (modulo
            // newline style and a trailing newline).
            var written = JsonSerializer.Serialize(settings, CompilerSettingsFile.Options);
            var original = File.ReadAllText(path);
            Assert.Equal(NormalizeLineEndings(original), NormalizeLineEndings(written));
        }
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n');
}
