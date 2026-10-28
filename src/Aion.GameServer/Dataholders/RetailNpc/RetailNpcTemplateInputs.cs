using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace Aion.GameServer.Dataholders.RetailNpc;

/// <summary>Strict source lexemes, not native constructor/default/effect results.</summary>
public sealed class RetailNpcSourceScalar<T> where T : struct
{
    internal RetailNpcSourceScalar(RetailNpcSourcePresence presence, ImmutableArray<string> lexemes,
        T? parsedValue, string recordPath)
    { Presence = presence; Lexemes = lexemes; ParsedValue = parsedValue; RecordPath = recordPath; }

    public RetailNpcSourcePresence Presence { get; }
    public ImmutableArray<string> Lexemes { get; }
    public T? ParsedValue { get; }
    public string RecordPath { get; }
    public bool DefaultApplied => false;
    public bool EffectiveValueVerified => false;
}

/// <summary>Original token spelling; no enum ordinal or case normalization.</summary>
public sealed record RetailNpcSourceToken(
    RetailNpcSourcePresence Presence, ImmutableArray<string> Lexemes, string? Value, string RecordPath);

public sealed record RetailNpcTemplateProjectionPolicy(
    string ExpectedDatasetId, string ExpectedOutputManifestSha256, string ExpectedRegion)
{
    public int MaxOrderedEntries { get; init; } = 4096;
}

/// <summary>All paths are evidence strings. Projection never opens a provenance path.</summary>
public sealed record RetailNpcTemplateSourceReceipt(
    string DatasetId, string OutputManifestSha256, int NpcId, string Devname,
    string Region, string SelectionReason, string SelectedDetailFilename,
    string SourcePath, long SourceFileBytes, string SourceFileSha256,
    long ByteStart, long ByteEndExclusive, int Line, int RecordIndex,
    string RawRecordSha256, string SemanticRecordSha256, string MainSha256, string NpcSha256);

public sealed record RetailNpcProjectionIssue(string Code, string RecordPath, string Message);

public sealed class RetailNpcTemplateProjectionResult
{
    internal RetailNpcTemplateProjectionResult(RetailNpcTemplateInputs? inputs,
        ImmutableArray<RetailNpcProjectionIssue> issues)
    { Inputs = inputs; Issues = issues; }
    public RetailNpcTemplateInputs? Inputs { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Inputs is not null && Issues.IsEmpty;
}

/// <summary>Container presence and original entry order are independent of field presence.</summary>
public sealed class RetailNpcOrderedSource<T>
{
    internal RetailNpcOrderedSource(RetailNpcSourcePresence presence, ImmutableArray<T> entries,
        JsonElement? rawGroup)
    { Presence = presence; Entries = entries; RawGroup = rawGroup?.Clone(); }
    public RetailNpcSourcePresence Presence { get; }
    public ImmutableArray<T> Entries { get; }
    public JsonElement? RawGroup { get; }
}

public sealed class RetailNpcAttackTemplateInputs
{
    internal RetailNpcAttackTemplateInputs(int ordinal, RetailNpcSourceScalar<int> weight,
        RetailNpcSourceScalar<int> modifier, RetailNpcSourceScalar<float> range,
        RetailNpcSourceToken type, JsonElement raw)
    { Ordinal = ordinal; Weight = weight; Modifier = modifier; Range = range; Type = type; RawNode = raw.Clone(); }
    public int Ordinal { get; }
    public RetailNpcSourceScalar<int> Weight { get; }
    public RetailNpcSourceScalar<int> Modifier { get; }
    public RetailNpcSourceScalar<float> Range { get; }
    public RetailNpcSourceToken Type { get; }
    public JsonElement RawNode { get; }
}

/// <summary>Static identity binding only; the raw entry retains probability/timing/unknown fields.</summary>
public sealed class RetailNpcSkillTemplateInputs
{
    internal RetailNpcSkillTemplateInputs(int ordinal, RetailNpcSourceToken name, int supported48Id,
        JsonElement binding, JsonElement raw)
    { Ordinal = ordinal; Name = name; Supported48Id = supported48Id; Binding = binding.Clone(); RawNode = raw.Clone(); }
    public int Ordinal { get; }
    public RetailNpcSourceToken Name { get; }
    public int Supported48Id { get; }
    public JsonElement Binding { get; }
    public JsonElement RawNode { get; }
    public bool EffectSemanticsVerified => false;
}

/// <summary>No named-group expansion or invented status name for an unnamed placeholder.</summary>
public sealed class RetailNpcAbnormalTemplateInputs
{
    internal RetailNpcAbnormalTemplateInputs(int ordinal, RetailNpcSourceToken name,
        RetailNpcSourceScalar<ushort> value, JsonElement raw)
    { Ordinal = ordinal; Name = name; Value = value; RawNode = raw.Clone(); }
    public int Ordinal { get; }
    public RetailNpcSourceToken Name { get; }
    public RetailNpcSourceScalar<ushort> Value { get; }
    public JsonElement RawNode { get; }
    public bool NativeSlotsResolved => false;
}

public sealed class RetailNpcFirstLevelTemplateInputs
{
    internal RetailNpcFirstLevelTemplateInputs(RetailNpcSourcePresence presence,
        IDictionary<string, RetailNpcSourceScalar<ushort>> primaries,
        IDictionary<string, RetailNpcSourceScalar<short>> elements, JsonElement? raw)
    { Presence = presence; Primaries = primaries.ToFrozenDictionary(StringComparer.Ordinal);
        Elements = elements.ToFrozenDictionary(StringComparer.Ordinal); RawGroup = raw?.Clone(); }
    public RetailNpcSourcePresence Presence { get; }
    public FrozenDictionary<string, RetailNpcSourceScalar<ushort>> Primaries { get; }
    public FrozenDictionary<string, RetailNpcSourceScalar<short>> Elements { get; }
    public JsonElement? RawGroup { get; }
}

public sealed class RetailNpcBoundsTemplateInputs
{
    internal RetailNpcBoundsTemplateInputs(RetailNpcSourcePresence presence,
        IDictionary<string, RetailNpcSourceScalar<float>> axes, JsonElement? raw)
    { Presence = presence; Axes = axes.ToFrozenDictionary(StringComparer.Ordinal); RawGroup = raw?.Clone(); }
    public RetailNpcSourcePresence Presence { get; }
    public FrozenDictionary<string, RetailNpcSourceScalar<float>> Axes { get; }
    public JsonElement? RawGroup { get; }
    public bool VisualScaleApplied => false;
}

/// <summary>
/// Immutable typed view of an admitted schema1 source record. It is not an NpcTemplate,
/// ordinary stat seed or effective profile. Original nodes retain unknown fields/order.
/// Supported lexical widths are checked without reproducing permissive native malformed-input
/// conversion, constructor zeroing, narrowing, defaults, effects, units or activation.
/// </summary>
public sealed class RetailNpcTemplateInputs
{
    internal RetailNpcTemplateInputs(RetailNpcSourceProfile source, RetailNpcTemplateSourceReceipt receipt,
        IDictionary<string, RetailNpcSourceScalar<int>> integers,
        IDictionary<string, RetailNpcSourceScalar<float>> speeds,
        RetailNpcFirstLevelTemplateInputs firstLevel, RetailNpcBoundsTemplateInputs bounds,
        RetailNpcOrderedSource<RetailNpcAttackTemplateInputs> attacks,
        RetailNpcOrderedSource<RetailNpcSkillTemplateInputs> skills,
        RetailNpcOrderedSource<RetailNpcAbnormalTemplateInputs> resists,
        RetailNpcOrderedSource<RetailNpcAbnormalTemplateInputs> penetrations)
    {
        Source = source; Receipt = receipt;
        Integers = integers.ToFrozenDictionary(StringComparer.Ordinal);
        Speeds = speeds.ToFrozenDictionary(StringComparer.Ordinal);
        FirstLevel = firstLevel; Bounds = bounds; Attacks = attacks; Skills = skills;
        AbnormalResists = resists; AbnormalPenetrations = penetrations;
    }

    public RetailNpcSourceProfile Source { get; }
    public RetailNpcTemplateSourceReceipt Receipt { get; }
    public int NpcId => Receipt.NpcId;
    public string Devname => Receipt.Devname;
    public FrozenDictionary<string, RetailNpcSourceScalar<int>> Integers { get; }
    public FrozenDictionary<string, RetailNpcSourceScalar<float>> Speeds { get; }
    public RetailNpcFirstLevelTemplateInputs FirstLevel { get; }
    public RetailNpcBoundsTemplateInputs Bounds { get; }
    public RetailNpcOrderedSource<RetailNpcAttackTemplateInputs> Attacks { get; }
    public RetailNpcOrderedSource<RetailNpcSkillTemplateInputs> Skills { get; }
    public RetailNpcOrderedSource<RetailNpcAbnormalTemplateInputs> AbnormalResists { get; }
    public RetailNpcOrderedSource<RetailNpcAbnormalTemplateInputs> AbnormalPenetrations { get; }
    public RetailNpcSourceScalar<int> StatRatio => Integers["stat_ratio"];
    public RetailNpcSourceScalar<int> VisualScale => Integers["scale"];
    public JsonElement RawRecord => Source.RawRecord;
    // Main ignores these raw fields for normal damage. Do not parse them as damage seeds.
    public RetailNpcSourceField RawPhysicalAttack => Source.GetRawRootField("physical_attack");
    public RetailNpcSourceField RawMagicalAttack => Source.GetRawRootField("magical_attack");
    public bool SourceProjected => true;
    public bool DefaultsResolved => false;
    public bool EffectiveInputsVerified => false;
    public bool FixtureVerified => false;
    public bool ActivationReady => false;
}
