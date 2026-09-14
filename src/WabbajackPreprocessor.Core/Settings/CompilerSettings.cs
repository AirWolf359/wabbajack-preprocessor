using System.Text.Json;
using System.Text.Json.Serialization;

namespace WabbajackPreprocessor.Core.Settings;

/// <summary>
/// DTO mirroring Wabbajack 3.x/4.x's <c>CompilerSettings</c> class
/// (Wabbajack.Compiler/CompilerSettings.cs). Property names and declaration order match
/// what Wabbajack serializes (PascalCase, no naming policy, every property written).
/// Path-typed properties are kept as strings so unusual values round-trip untouched;
/// unknown properties from newer Wabbajack versions are preserved via
/// <see cref="ExtraProperties"/>. See docs/wabbajack-formats.md.
/// </summary>
public class CompilerSettings
{
    public bool ModlistIsNSFW { get; set; }
    public string? Source { get; set; } = "";
    public string? Downloads { get; set; } = "";
    public string? Game { get; set; }
    public string? OutputFile { get; set; } = "";
    public string? ModListImage { get; set; } = "";
    public bool UseGamePaths { get; set; }
    public bool UseTextureRecompression { get; set; }
    public List<string> OtherGames { get; set; } = [];
    public string? MaxVerificationTime { get; set; } = "00:01:00";
    public string? ModListName { get; set; } = "";
    public string? ModListAuthor { get; set; } = "";
    public string? ModListDescription { get; set; } = "";
    public string? ModListReadme { get; set; } = "";
    public string? ModListWebsite { get; set; } = "";
    public string? ModListCommunity { get; set; } = "";
    public string? ModlistVersion { get; set; } = "0.0.1.0";
    public bool PublishUpdate { get; set; }
    public string? MachineUrl { get; set; } = "";
    public bool AutoGenerateReport { get; set; }
    public string? Profile { get; set; } = "";
    public List<string> AdditionalProfiles { get; set; } = [];
    public List<string> NoMatchInclude { get; set; } = [];
    public List<string> Include { get; set; } = [];
    public List<string> Ignore { get; set; } = [];
    public List<string> AlwaysEnabled { get; set; } = [];
    public string? Version { get; set; }
    public string? Description { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraProperties { get; set; }

    [JsonIgnore]
    public IEnumerable<string> AllProfiles =>
        AdditionalProfiles.Append(Profile ?? "").Where(p => !string.IsNullOrWhiteSpace(p));

    public List<string> GetTagList(TagList list) => list switch
    {
        TagList.NoMatchInclude => NoMatchInclude,
        TagList.Include => Include,
        TagList.Ignore => Ignore,
        TagList.AlwaysEnabled => AlwaysEnabled,
        _ => throw new ArgumentOutOfRangeException(nameof(list)),
    };
}

/// <summary>The four mod/path tag lists in a compiler settings file.</summary>
public enum TagList
{
    NoMatchInclude,
    Include,
    Ignore,
    AlwaysEnabled,
}
