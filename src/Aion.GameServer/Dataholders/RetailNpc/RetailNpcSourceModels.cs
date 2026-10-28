using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>Source presence only; no native defaults, units or effective stat calculation.</summary>
public enum RetailNpcSourcePresence { Absent, ExplicitZero, ExplicitValue, AmbiguousDuplicate }

public sealed record RetailNpcSourceField(
    RetailNpcSourcePresence Presence,
    ImmutableArray<string> RawValues,
    string Projection)
{
    public bool DefaultApplied => false;
}

/// <summary>Limits apply before any dataset is returned. Only requested raw profiles are retained.</summary>
public sealed record RetailNpcDatasetLimits
{
    public long MaxWholeInputBytes { get; init; } = 4L * 1024 * 1024 * 1024;
    public int MaxMetadataFileBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxProfileLineBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxJsonDepth { get; init; } = 384;
    public int MaxCoverageRows { get; init; } = 200_000;
    public int MaxRetainedProfiles { get; init; } = 1024;
    public long MaxRetainedProfileBytes { get; init; } = 32 * 1024 * 1024;
    public int MaxCsvRecordCharacters { get; init; } = 1024 * 1024;
}

/// <summary>Caller pins are independent trust anchors, not hashes accepted from the dataset itself.</summary>
public sealed record RetailNpcDatasetRequest(
    string Directory,
    string ExpectedDatasetId,
    string ExpectedOutputManifestSha256,
    IReadOnlyDictionary<int, string> RequiredNpcIdDevnames)
{
    public RetailNpcDatasetLimits Limits { get; init; } = new();
    /// <summary>Explicit optional retained-ID exception. Null keeps exact-name admission.</summary>
    public RetailNpcReviewedIdentityBindings? ReviewedIdentityBindings { get; init; }
}

public sealed record RetailNpcArtifactReceipt(string File, long Bytes, string Sha256);

/// <summary>
/// Immutable, source-extracted evidence. The cloned JsonElement owns its backing document,
/// including ordered unknown raw nodes. This is not an NpcTemplate or an effective profile.
/// </summary>
public sealed class RetailNpcSourceProfile
{
    private readonly JsonElement document;
    private static readonly Regex ZeroLexeme = new(@"^[+-]?0+(?:\.0*)?(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant);

    internal RetailNpcSourceProfile(JsonElement source, RetailNpcReviewedIdentityReceipt? reviewedIdentity = null)
    {
        document = source.Clone();
        NpcId = document.GetProperty("npc_id").GetInt32();
        Devname = document.GetProperty("devname").GetString()!;
        ReviewedIdentityReceipt = reviewedIdentity;
        Client48Devname = reviewedIdentity?.Client48Devname ?? Devname;
        DatasetId = document.GetProperty("dataset_id").GetString()!;
        Blockers = document.GetProperty("blockers").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray();
        ScalarFields = document.GetProperty("scalar_fields").EnumerateObject().ToFrozenDictionary(
            x => x.Name,
            x => Field(x.Value), StringComparer.Ordinal);
    }

    public int NpcId { get; }
    public string Devname { get; }
    public string Client48Devname { get; }
    public RetailNpcReviewedIdentityReceipt? ReviewedIdentityReceipt { get; }
    public string DatasetId { get; }
    public ImmutableArray<string> Blockers { get; }
    public FrozenDictionary<string, RetailNpcSourceField> ScalarFields { get; }
    public JsonElement RawRecord => document.GetProperty("raw_record");
    public JsonElement OrderedLists => document.GetProperty("ordered_lists");
    public JsonElement SkillBindings => document.GetProperty("skill_bindings");
    public JsonElement SourceProvenance => document.GetProperty("source");
    public JsonElement Binding48 => document.GetProperty("binding48");
    public JsonElement AggregateCrosscheck => document.GetProperty("aggregate_crosscheck");
    public JsonElement CompleteSourceProfile => document;
    public bool FixtureVerified => false;
    public bool ActivationReady => false;

    /// <summary>
    /// Reads a direct raw source field, including fields absent from the frozen typed registry
    /// such as stat_ratio. Zero is a lexical fact only; no default or native conversion is applied.
    /// </summary>
    public RetailNpcSourceField GetRawRootField(string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        var values = RawChildren(RawRecord).Where(x => x[0].GetString() == tag)
            .Select(RawText).ToImmutableArray();
        return new(Presence(values, numeric: true), values, "raw_lexeme_unresolved");
    }

    public ImmutableArray<JsonElement> GetRawRootChildren(string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);
        return RawChildren(RawRecord).Where(x => x[0].GetString() == tag).ToImmutableArray();
    }

    internal static IEnumerable<JsonElement> RawChildren(JsonElement node) => node[2].EnumerateArray()
        .Where(x => x.ValueKind == JsonValueKind.Array && !x[0].GetString()!.StartsWith('#'));

    internal static string RawText(JsonElement node) => string.Concat(node[2].EnumerateArray()
        .Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()));

    internal static RetailNpcSourcePresence Presence(ImmutableArray<string> values, bool numeric) => values.Length switch
    {
        0 => RetailNpcSourcePresence.Absent,
        > 1 => RetailNpcSourcePresence.AmbiguousDuplicate,
        _ => numeric && ZeroLexeme.IsMatch(values[0].Trim()) ? RetailNpcSourcePresence.ExplicitZero : RetailNpcSourcePresence.ExplicitValue
    };

    internal static RetailNpcSourceField Field(JsonElement field) => new(
        field.GetProperty("presence").GetString() switch
        {
            "absent" => RetailNpcSourcePresence.Absent,
            "explicit_zero" => RetailNpcSourcePresence.ExplicitZero,
            "explicit_value" => RetailNpcSourcePresence.ExplicitValue,
            "ambiguous_duplicate" => RetailNpcSourcePresence.AmbiguousDuplicate,
            _ => throw new InvalidDataException("Unknown source field presence")
        }, field.GetProperty("raw_values").EnumerateArray().Select(x => x.GetString()!).ToImmutableArray(),
        field.GetProperty("projection").GetString()!);
}

/// <summary>
/// Admitted static source dataset and requested cohort. It has no activation or legacy
/// template conversion API. Runtime fixture/coherent-package approval is a separate gate.
/// </summary>
public sealed class RetailNpcStaticDataset
{
    internal RetailNpcStaticDataset(string id, string manifestSha, IDictionary<int, RetailNpcSourceProfile> profiles,
        int coverageCount, int identityBlockers, long retainedBytes, IEnumerable<RetailNpcArtifactReceipt> receipts,
        JsonElement provenance, JsonElement sourceManifest, int reviewedIdentityBindingCount = 0)
    {
        DatasetId = id;
        OutputManifestSha256 = manifestSha;
        Profiles = profiles.ToFrozenDictionary();
        CoverageCount = coverageCount;
        IdentityBlockerCount = identityBlockers;
        ReviewedIdentityBindingCount = reviewedIdentityBindingCount;
        RetainedProfileBytes = retainedBytes;
        Artifacts = receipts.ToImmutableArray();
        Provenance = provenance.Clone();
        SourceManifest = sourceManifest.Clone();
    }

    public string DatasetId { get; }
    public string OutputManifestSha256 { get; }
    public FrozenDictionary<int, RetailNpcSourceProfile> Profiles { get; }
    public int CoverageCount { get; }
    public int IdentityBlockerCount { get; }
    /// <summary>Count validated across the complete stream, including unretained rows.</summary>
    public int ReviewedIdentityBindingCount { get; }
    public long RetainedProfileBytes { get; }
    public ImmutableArray<RetailNpcArtifactReceipt> Artifacts { get; }
    public JsonElement Provenance { get; }
    public JsonElement SourceManifest { get; }
    public bool StaticSourceAdmitted => true;
    public bool FixtureVerified => false;
    public bool ActivationReady => false;
}
