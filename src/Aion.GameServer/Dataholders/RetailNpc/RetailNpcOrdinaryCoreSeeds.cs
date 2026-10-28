using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Aion.GameServer.Dataholders.RetailNpc;

public enum RetailNpcCoreSeedStage
{
    TemplateConstructor,
    TemplateParser,
    TemplatePostparse,
    FreshCreatureInitialization,
    OrdinarySet,
    OrdinarySecondLevelSeed
}

public enum RetailNpcCoreSeedOrigin
{
    ConstructorDefault,
    SourceValue,
    ParserClamp,
    PostparseClamp,
    LevelTableDefault,
    TemplateCopy,
    RuntimeNarrowing,
    NativeZeroInitialization
}

/// <summary>One instruction-derived operation, not an observed live actor transition.</summary>
public sealed record RetailNpcCoreSeedStep(RetailNpcCoreSeedStage Stage, RetailNpcCoreSeedOrigin Origin,
    string Rule, long? Input, long Result);

/// <summary>Source evidence and all construction steps survive copies, defaults and narrowing.</summary>
public sealed class RetailNpcCoreSeedValue<T> where T : struct
{
    internal RetailNpcCoreSeedValue(T value, RetailNpcSourcePresence presence,
        ImmutableArray<string> lexemes, string path, RetailNpcTemplateSourceReceipt source,
        ImmutableArray<RetailNpcCoreSeedStep> steps, bool sourceValueUsed,
        RetailNpcLevelRatioTableReceipt? table = null)
    {
        Value = value; SourcePresence = presence; SourceLexemes = lexemes; SourceRecordPath = path;
        SourceReceipt = source; Steps = steps; SourceValueUsed = sourceValueUsed; RatioTableReceipt = table;
    }

    public T Value { get; }
    public RetailNpcSourcePresence SourcePresence { get; }
    public ImmutableArray<string> SourceLexemes { get; }
    public string SourceRecordPath { get; }
    public RetailNpcTemplateSourceReceipt SourceReceipt { get; }
    public ImmutableArray<RetailNpcCoreSeedStep> Steps { get; }
    public RetailNpcCoreSeedOrigin Origin => Steps[^1].Origin;
    public RetailNpcLevelRatioTableReceipt? RatioTableReceipt { get; }
    public bool SourceValueUsed { get; }
    public bool NativeDefaultApplied => SourcePresence == RetailNpcSourcePresence.Absent
        && Steps.Any(x => x.Origin is RetailNpcCoreSeedOrigin.ConstructorDefault or RetailNpcCoreSeedOrigin.LevelTableDefault);
    public bool EffectiveActorValueVerified => false;
}

/// <summary>
/// Explicit static assertions. Null is unresolved. They never certify a live actor or package.
/// ExpectedSource pins every source receipt field, including selected file and record span.
/// MasterObjectId (+188c) and SourceRelatedObjectId (+1894) must independently be known
/// zero; absent virtual/profile scaling (+1850) cannot establish either ID's absence.
/// </summary>
public sealed record RetailNpcOrdinarySeedDomain(
    RetailNpcTemplateSourceReceipt ExpectedSource,
    string ExpectedRatioTableSha256,
    bool? FreshUniqueTemplate,
    bool? FreshActor,
    int? NativeKind,
    uint? MasterObjectId,
    uint? SourceRelatedObjectId,
    bool? VirtualProfileRecordPresent,
    bool? DifficultyRecordPresent,
    bool? DualWeaponFlagPresent)
{
    // The common seed/scalar stages precede the native dual split. This does
    // not admit dual attack emission or claim those final damage ranges.
    public bool StatisticsBeforeDualSplit { get; init; }
}

public sealed class RetailNpcTemplateConstructionValues
{
    internal RetailNpcTemplateConstructionValues(
        RetailNpcCoreSeedValue<int> level, RetailNpcCoreSeedValue<int> maxHp,
        RetailNpcCoreSeedValue<int> initialHp, RetailNpcCoreSeedValue<int> hpRegen,
        RetailNpcCoreSeedValue<int> maxMp, RetailNpcCoreSeedValue<int> initialMp,
        RetailNpcCoreSeedValue<int> mpRegen, RetailNpcCoreSeedValue<int> minDamage,
        RetailNpcCoreSeedValue<int> maxDamage, RetailNpcCoreSeedValue<int> ratio,
        IDictionary<string, RetailNpcCoreSeedValue<ushort>> primaries,
        IDictionary<string, RetailNpcCoreSeedValue<short>> elements)
    {
        Level = level; MaxHp = maxHp; InitialHp = initialHp; HpRegeneration = hpRegen;
        MaxMp = maxMp; InitialMp = initialMp; MpRegeneration = mpRegen;
        MinDamage = minDamage; MaxDamage = maxDamage; StatRatio = ratio;
        Primaries = primaries.ToFrozenDictionary(StringComparer.Ordinal);
        Elements = elements.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public RetailNpcCoreSeedValue<int> Level { get; }
    public RetailNpcCoreSeedValue<int> MaxHp { get; }
    public RetailNpcCoreSeedValue<int> InitialHp { get; }
    public RetailNpcCoreSeedValue<int> HpRegeneration { get; }
    public RetailNpcCoreSeedValue<int> MaxMp { get; }
    public RetailNpcCoreSeedValue<int> InitialMp { get; }
    public RetailNpcCoreSeedValue<int> MpRegeneration { get; }
    public RetailNpcCoreSeedValue<int> MinDamage { get; }
    public RetailNpcCoreSeedValue<int> MaxDamage { get; }
    public RetailNpcCoreSeedValue<int> StatRatio { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<ushort>> Primaries { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<short>> Elements { get; }
}

/// <summary>Distinct MP stage facts, not a life-stat or regeneration policy.</summary>
public sealed class RetailNpcOrdinaryMpSeedDiagnostics
{
    internal RetailNpcOrdinaryMpSeedDiagnostics(RetailNpcCoreSeedValue<int> onSet,
        RetailNpcCoreSeedValue<int> commonMaximum, RetailNpcCoreSeedValue<int> freshRegen)
    { CurrentMpOnSet = onSet; MaxMpAtCommonSeedPoint = commonMaximum; MpRegenAtFreshCreatureInitialization = freshRegen; }

    public RetailNpcCoreSeedValue<int> CurrentMpOnSet { get; }
    public RetailNpcCoreSeedValue<int> MaxMpAtCommonSeedPoint { get; }
    public RetailNpcCoreSeedValue<int> MpRegenAtFreshCreatureInitialization { get; }
    public bool MpLifecycleResolved => false;
    public bool SourceMpRegenCopyVerified => false;
}

/// <summary>
/// Values at named pre-effect copy points. These fields are not one live actor snapshot:
/// Set, fresh Creature Init and second-level construction occur at different stages.
/// </summary>
public sealed class RetailNpcOrdinaryCoreSeeds
{
    internal RetailNpcOrdinaryCoreSeeds(RetailNpcTemplateInputs source,
        RetailNpcLevelRatioTableReceipt table, RetailNpcOrdinarySeedDomain domain,
        RetailNpcTemplateConstructionValues template, RetailNpcCoreSeedValue<int> currentHp,
        RetailNpcCoreSeedValue<int> maximumHp, RetailNpcCoreSeedValue<int> hpRegen,
        RetailNpcCoreSeedValue<int> ratio, RetailNpcCoreSeedValue<ushort> minimum,
        RetailNpcCoreSeedValue<ushort> maximum, RetailNpcCoreSeedValue<ushort> offMinimum,
        RetailNpcCoreSeedValue<ushort> offMaximum, RetailNpcOrdinaryMpSeedDiagnostics mp,
        IDictionary<string, RetailNpcCoreSeedValue<ushort>> primaries,
        IDictionary<string, RetailNpcCoreSeedValue<short>> elements)
    {
        Source = source; RatioTableReceipt = table; Domain = domain; Template = template;
        InitialHpOnSet = currentHp; MaxHpBeforeEffects = maximumHp; HpRegenerationBeforeEffects = hpRegen;
        StatRatioBeforeEffects = ratio; MainHandMinimumBeforeEffects = minimum; MainHandMaximumBeforeEffects = maximum;
        OffHandMinimumBeforeEffects = offMinimum; OffHandMaximumBeforeEffects = offMaximum; MpDiagnostics = mp;
        FirstLevelBasePrimaries = primaries.ToFrozenDictionary(StringComparer.Ordinal);
        FirstLevelCurrentPrimariesBeforeEffects = primaries.ToFrozenDictionary(StringComparer.Ordinal);
        FirstLevelBaseElements = elements.ToFrozenDictionary(StringComparer.Ordinal);
        FirstLevelCurrentElementsBeforeEffects = elements.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public RetailNpcTemplateInputs Source { get; }
    public RetailNpcTemplateSourceReceipt SourceReceipt => Source.Receipt;
    public RetailNpcLevelRatioTableReceipt RatioTableReceipt { get; }
    public RetailNpcOrdinarySeedDomain Domain { get; }
    public RetailNpcTemplateConstructionValues Template { get; }
    public RetailNpcCoreSeedValue<int> InitialHpOnSet { get; }
    public RetailNpcCoreSeedValue<int> MaxHpBeforeEffects { get; }
    public RetailNpcCoreSeedValue<int> HpRegenerationBeforeEffects { get; }
    public RetailNpcCoreSeedValue<int> StatRatioBeforeEffects { get; }
    public RetailNpcCoreSeedValue<ushort> MainHandMinimumBeforeEffects { get; }
    public RetailNpcCoreSeedValue<ushort> MainHandMaximumBeforeEffects { get; }
    public RetailNpcCoreSeedValue<ushort> OffHandMinimumBeforeEffects { get; }
    public RetailNpcCoreSeedValue<ushort> OffHandMaximumBeforeEffects { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<ushort>> FirstLevelBasePrimaries { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<ushort>> FirstLevelCurrentPrimariesBeforeEffects { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<short>> FirstLevelBaseElements { get; }
    public FrozenDictionary<string, RetailNpcCoreSeedValue<short>> FirstLevelCurrentElementsBeforeEffects { get; }
    public RetailNpcOrdinaryMpSeedDiagnostics MpDiagnostics { get; }
    public bool CoreConstructorAndSeedRulesApplied => true;
    public bool EffectiveInputsVerified => false;
    public bool FixtureVerified => false;
    public bool ActivationReady => false;
}

public sealed class RetailNpcOrdinarySeedProjectionResult
{
    internal RetailNpcOrdinarySeedProjectionResult(RetailNpcOrdinaryCoreSeeds? seeds,
        ImmutableArray<RetailNpcProjectionIssue> issues)
    { Seeds = seeds; Issues = issues; }
    public RetailNpcOrdinaryCoreSeeds? Seeds { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Seeds is not null && Issues.IsEmpty;
}
