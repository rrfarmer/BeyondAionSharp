using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;

namespace Aion.GameServer.Dataholders.RetailNpc;

public enum RetailNpcScalarView { Signed32, Unsigned32, Unsigned16, Signed16, GeneratedZero }
public enum RetailNpcScalarOperation { PercentAndFlat, Copy, FlatOnly }
public enum RetailNpcModifierInputOrigin { FixedMathematicalFixture, ExternalActorSnapshot }

public sealed record RetailNpcScalarSpecification(string Field, string? TemplateOffset, string ActorOffset,
    RetailNpcScalarView View, RetailNpcScalarOperation Operation, int? ModifierIndex, long? Minimum, long? Maximum)
{
    public string? ModifierAuditName => ModifierIndex is int index ? RetailNpcOrdinaryScalarProjection.ModifierNames[index] : null;
}
public sealed record RetailNpcScalarSeedStep(string Stage, string Origin, string Rule, long? Input, long Result);

/// <summary>Source/template history only. No effect, epoch or observed actor is implied.</summary>
public sealed class RetailNpcOrdinaryScalarBase
{
    internal RetailNpcOrdinaryScalarBase(RetailNpcScalarSpecification specification, long value,
        RetailNpcSourcePresence presence, ImmutableArray<string> lexemes, string path,
        RetailNpcTemplateSourceReceipt source, ImmutableArray<RetailNpcScalarSeedStep> steps)
    { Specification = specification; Value = value; SourcePresence = presence; SourceLexemes = lexemes;
        SourceRecordPath = path; SourceReceipt = source; Steps = steps; }
    public RetailNpcScalarSpecification Specification { get; }
    public long Value { get; }
    public RetailNpcSourcePresence SourcePresence { get; }
    public ImmutableArray<string> SourceLexemes { get; }
    public string SourceRecordPath { get; }
    public RetailNpcTemplateSourceReceipt SourceReceipt { get; }
    public ImmutableArray<RetailNpcScalarSeedStep> Steps { get; }
}

/// <summary>Original ordered data entry, including inert nameless zero placeholders.</summary>
public sealed class RetailNpcAbnormalSourceMerge
{
    internal RetailNpcAbnormalSourceMerge(string group, int ordinal, string? name, ushort value,
        uint mask, bool ignored, JsonElement raw)
    { Group = group; Ordinal = ordinal; Name = name; Value = value; Mask = mask;
        IgnoredNamelessZero = ignored; RawNode = raw.Clone(); }
    public string Group { get; }
    public int Ordinal { get; }
    public string? Name { get; }
    public ushort Value { get; }
    public uint Mask { get; }
    public bool IgnoredNamelessZero { get; }
    public JsonElement RawNode { get; }
}

public sealed record RetailNpcOrdinaryScalarProjectionContext(RetailNpcOrdinarySeedDomain ExpectedDomain);

public sealed class RetailNpcOrdinaryScalarInputs
{
    internal RetailNpcOrdinaryScalarInputs(RetailNpcOrdinaryCoreSeeds core,
        IDictionary<string, RetailNpcOrdinaryScalarBase> fields, IEnumerable<RetailNpcAbnormalSourceMerge> merges)
    { Core = core; Fields = fields.ToFrozenDictionary(StringComparer.Ordinal); AbnormalMerges = merges.ToImmutableArray(); }
    public RetailNpcOrdinaryCoreSeeds Core { get; }
    public RetailNpcTemplateSourceReceipt SourceReceipt => Core.SourceReceipt;
    public RetailNpcLevelRatioTableReceipt RatioTableReceipt => Core.RatioTableReceipt;
    public FrozenDictionary<string, RetailNpcOrdinaryScalarBase> Fields { get; }
    public ImmutableArray<RetailNpcAbnormalSourceMerge> AbnormalMerges { get; }
    public bool EffectiveActorValueVerified => false;
    public bool ActivationReady => false;
}

public sealed class RetailNpcOrdinaryScalarProjectionResult
{
    internal RetailNpcOrdinaryScalarProjectionResult(RetailNpcOrdinaryScalarInputs? inputs,
        ImmutableArray<RetailNpcProjectionIssue> issues) { Inputs = inputs; Issues = issues; }
    public RetailNpcOrdinaryScalarInputs? Inputs { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Inputs is not null && Issues.IsEmpty;
}

public sealed record RetailNpcModifierPairInput(int Index, int Percent, int Flat);

/// <summary>
/// Caller-supplied evidence. Hashing proves the admitted bytes, not the truth of a runtime
/// observation. Paths/identities remain strings and are never opened. Epoch has no default.
/// </summary>
public sealed record RetailNpcModifierAggregateReceipt(RetailNpcModifierInputOrigin Origin,
    RetailNpcTemplateSourceReceipt Source, string RatioTableSha256, string PackageContextSha256,
    string ContentSha256, int ContentBytes, string Epoch, string? ActorObjectIdentity, string EvidenceReference);

public sealed class RetailNpcModifierAggregate
{
    internal RetailNpcModifierAggregate(ImmutableArray<RetailNpcModifierPairInput> pairs,
        ImmutableArray<byte> bytes, RetailNpcModifierAggregateReceipt receipt)
    { Pairs = pairs; CanonicalBytes = bytes; Receipt = receipt; }
    public ImmutableArray<RetailNpcModifierPairInput> Pairs { get; }
    public ImmutableArray<byte> CanonicalBytes { get; }
    public RetailNpcModifierAggregateReceipt Receipt { get; }
    public bool RuntimeObservationVerified => false;
    public bool ActivationReady => false;
}

public sealed class RetailNpcModifierAggregateAdmissionResult
{
    internal RetailNpcModifierAggregateAdmissionResult(RetailNpcModifierAggregate? aggregate,
        ImmutableArray<RetailNpcProjectionIssue> issues) { Aggregate = aggregate; Issues = issues; }
    public RetailNpcModifierAggregate? Aggregate { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Aggregate is not null && Issues.IsEmpty;
}

/// <summary>All pins and current eligibility assertions are required; null is unresolved.
/// IDs are actual independent current-state assertions, not deductions from profile absence.</summary>
public sealed record RetailNpcOrdinaryScalarEvaluationContext(RetailNpcTemplateSourceReceipt ExpectedSource,
    string ExpectedRatioTableSha256, string ExpectedPackageContextSha256, string ExpectedModifierContentSha256,
    string ExpectedEpoch, RetailNpcModifierInputOrigin ExpectedOrigin, string? ExpectedActorObjectIdentity,
    int? NativeKind, uint? MasterObjectId, uint? SourceRelatedObjectId,
    bool? VirtualProfileRecordPresent, bool? DifficultyRecordPresent, bool? DualWeaponFlagPresent);

public sealed record RetailNpcScalarOperationHistory(long Base, int? Percent, int? Flat,
    long? Product, long? PercentContribution, long Unclamped, long? Minimum, long? Maximum, long Result);

public sealed class RetailNpcOrdinaryScalarValue
{
    internal RetailNpcOrdinaryScalarValue(RetailNpcOrdinaryScalarBase source,
        RetailNpcScalarOperationHistory history) { Source = source; History = history; }
    public RetailNpcOrdinaryScalarBase Source { get; }
    public RetailNpcScalarOperationHistory History { get; }
    public long Value => History.Result;
}

public sealed record RetailNpcConsumedModifierComponent(int Index, bool Percent, bool Flat);

/// <summary>
/// Complete conditional scalar result for supplied inputs. No current HP mutation, MP policy,
/// active-effect inference, native object or activation API. Per-hand/MP diagnostics retain
/// their original seed-stage names in Core; this profile cannot make them live actor values.
/// </summary>
public sealed class RetailNpcOrdinaryScalarProfile
{
    internal RetailNpcOrdinaryScalarProfile(RetailNpcOrdinaryScalarInputs inputs,
        RetailNpcModifierAggregate modifiers, RetailNpcOrdinaryScalarEvaluationContext context,
        IDictionary<string, RetailNpcOrdinaryScalarValue> values,
        IEnumerable<RetailNpcConsumedModifierComponent> consumed)
    {
        Inputs = inputs; Modifiers = modifiers; Context = context;
        Fields = values.ToFrozenDictionary(StringComparer.Ordinal); ConsumedComponents = consumed.ToImmutableArray();
        ElementalDefenses = RetailNpcTemplateProjection.ElementFields.Select(x => checked((short)Fields[x].Value)).ToImmutableArray();
        Primaries = RetailNpcTemplateProjection.PrimaryFields.Select(x => checked((ushort)Fields[x].Value)).ToImmutableArray();
        AbnormalResists = Enumerable.Range(0, 25).Select(i => checked((ushort)Fields[$"abnormal_resist[{i}]"].Value)).ToImmutableArray();
        AbnormalPenetrations = Enumerable.Range(0, 25).Select(i => checked((ushort)Fields[$"abnormal_penetration[{i}]"].Value)).ToImmutableArray();
    }
    public RetailNpcOrdinaryScalarInputs Inputs { get; }
    public RetailNpcOrdinaryCoreSeeds Core => Inputs.Core;
    public RetailNpcModifierAggregate Modifiers { get; }
    public RetailNpcOrdinaryScalarEvaluationContext Context { get; }
    public FrozenDictionary<string, RetailNpcOrdinaryScalarValue> Fields { get; }
    public ImmutableArray<RetailNpcConsumedModifierComponent> ConsumedComponents { get; }
    public ImmutableArray<short> ElementalDefenses { get; }
    public ImmutableArray<ushort> Primaries { get; }
    public ImmutableArray<ushort> AbnormalResists { get; }
    public ImmutableArray<ushort> AbnormalPenetrations { get; }
    public int MaximumHp => checked((int)Fields["max_hp"].Value);
    public int HpRegeneration => checked((int)Fields["hp_regen"].Value);
    public uint StatRatio => checked((uint)Fields["stat_ratio"].Value);
    public uint PhysicalDefense => checked((uint)Fields["physical_defend"].Value);
    public uint MagicalDefense => checked((uint)Fields["magical_defend"].Value);
    public ushort MagicalResistance => Word("magical_resist");
    public ushort PhysicalAccuracy => Word("hit_accuracy");
    public ushort MagicalAccuracy => Word("magical_hit_accuracy");
    public ushort Dodge => Word("dodge");
    public ushort Parry => Word("parry");
    public ushort CombinedParry => Parry; // Main:14035a115, offhand seed0 in the admitted nondual domain.
    public ushort Block => Word("block");
    public ushort PhysicalCritical => Word("critical");
    public ushort MagicalCritical => Word("magical_critical");
    public ushort Concentration => Word("concentration");
    public ushort MagicalSkillBoost => Word("magical_skill_boost");
    public ushort MagicalSkillBoostResist => Word("magical_skill_boost_resist");
    public ushort PhysicalCriticalReduceRate => Word("physical_critical_reduce_rate");
    public ushort MagicalCriticalReduceRate => Word("magical_critical_reduce_rate");
    public ushort PhysicalCriticalDamageReduce => Word("physical_critical_damage_reduce");
    public ushort MagicalCriticalDamageReduce => Word("magical_critical_damage_reduce");
    public ushort ProcReduceRate => Word("proc_reduce_rate");
    public short HealSkillBoost => checked((short)Fields["heal_skill_boost"].Value);
    public short MpHealSkillBoost => checked((short)Fields["mp_heal_skill_boost"].Value);
    private ushort Word(string name) => checked((ushort)Fields[name].Value);
    public bool ScalarRulesApplied => true;
    public bool EffectiveActorValueVerified => false;
    public bool FixtureVerified => false;
    public bool ActivationReady => false;
}

public sealed class RetailNpcOrdinaryScalarEvaluationResult
{
    internal RetailNpcOrdinaryScalarEvaluationResult(RetailNpcOrdinaryScalarProfile? profile,
        ImmutableArray<RetailNpcProjectionIssue> issues) { Profile = profile; Issues = issues; }
    public RetailNpcOrdinaryScalarProfile? Profile { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Profile is not null && Issues.IsEmpty;
}
