using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Aion.GameServer.Dataholders.RetailNpc;

public sealed record RetailNpcDifficultyFieldContract(string Field, string TemplateOffset, string ActorOffset,
    string? CommonTailCopyOffset, int Minimum, int Maximum, string OperandView);
public sealed record RetailNpcDifficultyFloatStep(string Operation, uint Binary32Bits)
{
    public float DisplayValue => BitConverter.UInt32BitsToSingle(Binary32Bits);
}
public sealed record RetailNpcDifficultyPredicate(bool RecordPresent, int Counter, int StatScalingRatio)
{
    public bool ApplySecondLevel => RecordPresent && Counter != 0 && StatScalingRatio > 0;
}
public sealed record RetailNpcDifficultySourceOperand(RetailNpcSourcePresence Presence, ImmutableArray<string> Lexemes,
    string RecordPath, int ParsedSigned32, long ConstructorValue, long NativeOperand, string View)
{
    public bool NativeConstructorDefaultApplied => Presence == RetailNpcSourcePresence.Absent;
    public bool EffectiveActorValueVerified => false;
}

/// <summary>One named construction stage, not an effective or live actor scalar.</summary>
public sealed class RetailNpcDifficultyFieldPlan
{
    internal RetailNpcDifficultyFieldPlan(RetailNpcDifficultyFieldContract contract, RetailNpcDifficultySourceOperand operand,
        long priorWrite, bool applied, IEnumerable<RetailNpcDifficultyFloatStep> steps, int? conversion, long value)
    { Contract = contract; Source = operand; PriorOrdinaryWrite = priorWrite; Applied = applied;
        FloatSteps = steps.ToImmutableArray(); NativeCvttSigned32 = conversion; Value = value; }
    public RetailNpcDifficultyFieldContract Contract { get; }
    public RetailNpcDifficultySourceOperand Source { get; }
    public long PriorOrdinaryWrite { get; }
    public bool Applied { get; }
    public ImmutableArray<RetailNpcDifficultyFloatStep> FloatSteps { get; }
    public int? NativeCvttSigned32 { get; }
    public long Value { get; }
    public bool ClampApplied => Applied && Value != NativeCvttSigned32;
}

public sealed record RetailNpcDifficultyWrite(string Stage, string Field, string ActorOffset,
    string Rule, int WidthBits, long Value);

/// <summary>
/// Complete own-domain difficulty plan. Set initialHP, second-level bases and common-tail
/// copies are distinct stages. No effects, currentHP mutation, MP lifecycle or actor binding.
/// </summary>
public sealed class RetailNpcDifficultyPlan
{
    internal RetailNpcDifficultyPlan(RetailNpcTemplateInputs source, RetailNpcLevelRatioTableReceipt levelTable,
        RetailNpcDifficultyTableReceipt table, RetailNpcDifficultyRecordInputs record, RetailNpcDifficultyContext context,
        RetailNpcDifficultyPredicate predicate, RetailNpcDifficultyFieldPlan initialHp,
        IDictionary<string, RetailNpcDifficultyFieldPlan> fields, IEnumerable<RetailNpcDifficultyWrite> writes,
        IDictionary<string, RetailNpcDifficultySourceOperand> boosts)
    {
        Source = source; LevelRatioTableReceipt = levelTable; DifficultyTableReceipt = table; Record = record; Context = context;
        Predicate = predicate; InitialHpOnSet = initialHp; Fields = fields.ToFrozenDictionary(StringComparer.Ordinal);
        OrderedWrites = writes.ToImmutableArray(); CommonTailBoostSources = boosts.ToFrozenDictionary(StringComparer.Ordinal);
    }
    public RetailNpcTemplateInputs Source { get; }
    public RetailNpcTemplateSourceReceipt SourceReceipt => Source.Receipt;
    public RetailNpcLevelRatioTableReceipt LevelRatioTableReceipt { get; }
    public RetailNpcDifficultyTableReceipt DifficultyTableReceipt { get; }
    public RetailNpcDifficultyRecordInputs Record { get; }
    public RetailNpcDifficultyContext Context { get; }
    public RetailNpcDifficultyPredicate Predicate { get; }
    public RetailNpcDifficultyFieldPlan InitialHpOnSet { get; }
    public FrozenDictionary<string, RetailNpcDifficultyFieldPlan> Fields { get; }
    public ImmutableArray<RetailNpcDifficultyWrite> OrderedWrites { get; }
    public FrozenDictionary<string, RetailNpcDifficultySourceOperand> CommonTailBoostSources { get; }
    public ImmutableArray<string> UntouchedDomains => ["hp_regen", "stat_ratio", "dodge", "parry", "block", "concentration",
        "proc_reduce_rate", "first_level_primaries", "elemental_defenses", "abnormal_resists", "abnormal_penetrations",
        "attack_delay", "hit_count", "movement_speeds"];
    public bool RuntimeObservationVerified => false;
    public bool EffectiveActorValueVerified => false;
    public bool ActivationReady => false;
}

public sealed class RetailNpcDifficultyConstructionResult
{
    internal RetailNpcDifficultyConstructionResult(RetailNpcDifficultyPlan? plan, ImmutableArray<RetailNpcProjectionIssue> issues)
    { Plan = plan; Issues = issues; }
    public RetailNpcDifficultyPlan? Plan { get; }
    public ImmutableArray<RetailNpcProjectionIssue> Issues { get; }
    public bool Success => Plan is not null && Issues.IsEmpty;
}
