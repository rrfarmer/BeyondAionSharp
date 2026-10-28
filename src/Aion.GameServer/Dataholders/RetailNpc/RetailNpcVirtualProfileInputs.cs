using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Aion.GameServer.Dataholders.RetailNpc;

public enum RetailNpcVirtualTableSelection { ProvidedBase, RegionalMissingBaseFallback, RegionalOverride }
public sealed record RetailNpcVirtualTablePolicy(string DatasetId, string OutputManifestSha256, string Region,
    string PackageContextSha256, string ExpectedSourceSha256, int ExpectedSourceBytes, string SourceIdentity,
    string SelectedRelativePath, RetailNpcVirtualTableSelection Selection)
{
    public int MaximumBytes { get; init; } = 4 * 1024 * 1024;
    public int MaximumCharacters { get; init; } = 2 * 1024 * 1024;
    public int MaximumEntityCharacters { get; init; } = 65536;
}
public sealed record RetailNpcVirtualTableReceipt(string DatasetId, string OutputManifestSha256, string Region,
    string MainSha256, string PackageContextSha256, string SourceSha256, int SourceBytes,
    string SourceIdentity, string SelectedRelativePath, RetailNpcVirtualTableSelection Selection);
public sealed record RetailNpcVirtualPair(int Flat, int Percent);
public sealed record RetailNpcVirtualOccurrence(int Ordinal, string Name, string Lexeme, string RawXml,
    int? DestinationOffset, bool Ignored, bool Percent, long? ParsedValue);

/// <summary>Defined descriptor fields, never a claim about allocation padding or a loaded pointer.</summary>
public sealed class RetailNpcVirtualDescriptor
{
    internal RetailNpcVirtualDescriptor(int ordinal, string name, byte rank, int freshId, string rawXml,
        IDictionary<int, RetailNpcVirtualPair> pairs, long expFlat, int expPercent,
        IEnumerable<RetailNpcVirtualOccurrence> occurrences, byte[] definedImage, string hash)
    {
        Ordinal = ordinal; Name = name; Rank = rank; FreshRegistrationId = freshId; RawXml = rawXml;
        Pairs = pairs.ToFrozenDictionary(); ExpFlat = expFlat; ExpPercent = expPercent;
        Occurrences = occurrences.ToImmutableArray(); DefinedImageWithZeroPaddingPlaceholders = definedImage.ToImmutableArray();
        SemanticSha256 = hash;
    }
    public int Ordinal { get; }
    public string Name { get; }
    public byte Rank { get; }
    public int FreshRegistrationId { get; }
    public string RawXml { get; }
    public FrozenDictionary<int, RetailNpcVirtualPair> Pairs { get; }
    public long ExpFlat { get; }
    public int ExpPercent { get; }
    public ImmutableArray<RetailNpcVirtualOccurrence> Occurrences { get; }
    public ImmutableArray<byte> DefinedImageWithZeroPaddingPlaceholders { get; }
    public ImmutableArray<int> UnknownPaddingOffsets => [0xbc, 0xbd, 0xbe, 0xbf];
    public string SemanticSha256 { get; }
    public bool RuntimeObservationVerified => false;
}
public sealed record RetailNpcVirtualRegistryEntry(string Name, int NativeNameId);
public sealed record RetailNpcVirtualRegistryEvidence(ImmutableArray<RetailNpcVirtualRegistryEntry> Entries,
    string ContentSha256, string Epoch, RetailNpcModifierInputOrigin Origin, string? ActorObjectIdentity, string Evidence);
public sealed class RetailNpcVirtualSelection
{
    internal RetailNpcVirtualSelection(RetailNpcVirtualProfileTable table, RetailNpcVirtualDescriptor descriptor,
        RetailNpcVirtualRegistryEvidence registry, int nativeId)
    { Table = table; Descriptor = descriptor; Registry = registry; NativeNameId = nativeId; }
    public RetailNpcVirtualProfileTable Table { get; }
    public RetailNpcVirtualDescriptor Descriptor { get; }
    public RetailNpcVirtualRegistryEvidence Registry { get; }
    public int NativeNameId { get; }
    public byte NativeRank => Descriptor.Rank;
    public bool RuntimeObservationVerified => false;
}
public sealed record RetailNpcVirtualDomain(bool? FreshUniqueTemplate, bool? FreshActor, int? NativeKind,
    uint? MasterObjectId188c, uint? SourceRelatedObjectId1894, bool? VirtualProfileRecordPresent,
    bool? DifficultyRecordPresent, bool? DualWeaponFlagPresent);
public sealed record RetailNpcVirtualContext(RetailNpcTemplateSourceReceipt ExpectedSource,
    RetailNpcLevelRatioTableReceipt ExpectedRatioTable, RetailNpcVirtualTableReceipt ExpectedTable,
    string ExpectedDescriptorSha256, int ExpectedNativeNameId, byte ExpectedRank, string ExpectedRegistrySha256,
    string ExpectedPackageContextSha256, string ExpectedModifierSha256, string ExpectedEpoch,
    RetailNpcModifierInputOrigin ExpectedOrigin, string? ExpectedActorObjectIdentity,
    RetailNpcVirtualDomain SeedDomain, RetailNpcVirtualDomain CurrentDomain);
public sealed record RetailNpcVirtualStep(string Stage, string Rule, long Input, long Result);
public sealed record RetailNpcVirtualSourceValue(string Field, RetailNpcSourcePresence Presence,
    ImmutableArray<string> Lexemes, long ConstructorDefault, long TemplateValue,
    ImmutableArray<RetailNpcVirtualStep> History);
public sealed record RetailNpcVirtualHelperValue(string Field, int DescriptorOffset, long NativeOperand,
    int Percent, int Flat, int DividedPercent, long Product, long Unclamped, long FullResult,
    int ActorWidth, long ActorBaseline);
public sealed record RetailNpcVirtualScalarValue(RetailNpcScalarSpecification Specification, long Baseline,
    long PercentOperand, int? Percent, int? Flat, long? Product, long? Contribution, long Unclamped, long Value);
public sealed record RetailNpcVirtualDelay(long OriginalWord, int Percent, int Flat, long Product,
    long Contribution, long Unclamped, long AbsoluteClamped, long HalfLimited, long DoubledWord, long Value);
public sealed record RetailNpcVirtualCount(long Baseline, long PercentOperand, int Percent, int Flat,
    long Product, long Contribution, long Unclamped, long Value);
public sealed record RetailNpcVirtualWrite(string Stage, string Field, string ActorOffset, string Storage, long Value);

/// <summary>Complete conditional scalar/initialization plan. No current HP update, speed packet or actor mutation.</summary>
public sealed class RetailNpcVirtualConstructionPlan
{
    internal RetailNpcVirtualConstructionPlan(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableInputs ratio,
        RetailNpcVirtualSelection selection, RetailNpcModifierAggregate effects, RetailNpcVirtualContext context,
        IDictionary<string, RetailNpcVirtualSourceValue> templates, IDictionary<string, RetailNpcVirtualHelperValue> helpers,
        IDictionary<string, RetailNpcVirtualScalarValue> scalars, IDictionary<string, long> shadows,
        IDictionary<string, long> tail, IEnumerable<RetailNpcVirtualWrite> writes,
        IEnumerable<RetailNpcAbnormalSourceMerge> merges, long hp, long mp,
        RetailNpcVirtualDelay delay, RetailNpcVirtualCount count, ushort cancel, byte dp)
    {
        Source = source; RatioTable = ratio; Selection = selection; Effects = effects; Context = context;
        TemplateValues = templates.ToFrozenDictionary(StringComparer.Ordinal); Helpers = helpers.ToFrozenDictionary(StringComparer.Ordinal);
        Scalars = scalars.ToFrozenDictionary(StringComparer.Ordinal); PreEffectShadows = shadows.ToFrozenDictionary(StringComparer.Ordinal);
        CommonTail = tail.ToFrozenDictionary(StringComparer.Ordinal); OrderedWrites = writes.ToImmutableArray();
        AbnormalMerges = merges.ToImmutableArray(); InitialHpOnSet = hp; InitialMpOnSetDiagnostic = mp;
        Delay = delay; Count = count; CancelLevel = cancel; DpReduce = dp;
    }
    public RetailNpcTemplateInputs Source { get; }
    public RetailNpcLevelRatioTableInputs RatioTable { get; }
    public RetailNpcVirtualSelection Selection { get; }
    public RetailNpcModifierAggregate Effects { get; }
    public RetailNpcVirtualContext Context { get; }
    public FrozenDictionary<string, RetailNpcVirtualSourceValue> TemplateValues { get; }
    public FrozenDictionary<string, RetailNpcVirtualHelperValue> Helpers { get; }
    public FrozenDictionary<string, RetailNpcVirtualScalarValue> Scalars { get; }
    public FrozenDictionary<string, long> PreEffectShadows { get; }
    public FrozenDictionary<string, long> CommonTail { get; }
    public ImmutableArray<RetailNpcVirtualWrite> OrderedWrites { get; }
    public ImmutableArray<RetailNpcAbnormalSourceMerge> AbnormalMerges { get; }
    public long InitialHpOnSet { get; }
    public long InitialMpOnSetDiagnostic { get; }
    public int EffectiveMaxMpSeedDiagnostic => 0;
    public int FreshMpRegenerationDiagnostic => 0;
    public RetailNpcVirtualDelay Delay { get; }
    public RetailNpcVirtualCount Count { get; }
    public ushort CancelLevel { get; }
    public byte DpReduce { get; }
    public ushort RightMinimum => checked((ushort)Helpers["min_damage"].ActorBaseline);
    public ushort RightMaximum => checked((ushort)Helpers["max_damage"].ActorBaseline);
    public ushort OffhandMinimum => 0;
    public ushort OffhandMaximum => 0;
    public byte OffhandCount => 0;
    public bool RuntimeObservationVerified => false;
    public bool EffectiveActorValueVerified => false;
    public bool ActivationReady => false;
}
public sealed record RetailNpcVirtualResult<T>(T? Value, ImmutableArray<RetailNpcProjectionIssue> Issues) where T : class
{
    public bool Success => Value is not null && Issues.IsEmpty;
    public bool ActivationReady => false;
}

internal static class RetailNpcVirtualGuard
{
    internal static void Need([DoesNotReturnIf(false)] bool condition, string code, string message)
    { if (!condition) throw new RetailNpcVirtualError(code, message); }
    internal static void Hash(string? hash) => Need(hash is { Length: 64 } && hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'), "virtual_hash", "Expected lowercase SHA256");
    internal static bool Text(string? value, int max) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;
    internal static void Domain(RetailNpcVirtualDomain? d)
    {
        Need(d is { FreshUniqueTemplate: true, FreshActor: true, MasterObjectId188c: 0u, SourceRelatedObjectId1894: 0u,
            VirtualProfileRecordPresent: true, DifficultyRecordPresent: false, DualWeaponFlagPresent: false }
            && d.NativeKind is 1 or 8 or 0x1000 or 0x4000, "virtual_domain", "Explicit admitted profile kind, fresh context, zero independent IDs, absent difficulty and nondual required");
    }
    internal static RetailNpcVirtualResult<T> Failure<T>(Exception e) where T : class => new(null,
        [new(e is RetailNpcVirtualError v ? v.Code : "virtual_source_contract", "/virtual_profile", e.Message)]);
}
internal sealed class RetailNpcVirtualError(string code, string message) : Exception(message)
{ internal string Code { get; } = code; }
