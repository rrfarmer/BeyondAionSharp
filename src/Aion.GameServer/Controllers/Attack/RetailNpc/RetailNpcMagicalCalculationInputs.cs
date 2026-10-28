using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive source/current/original-call boundaries. A declaration is not a native observation.</summary>
internal static class RetailNpcMagicalCalculationInputs
{
    internal enum Origin { Unknown, MathematicalFixture, ExternalSourceAssertion, ObservedOriginal }
    internal enum Rounding { Unknown, NearestEven }
    internal enum SpanRole
    {
        Unknown, Attacker, Defender, AttackerTemplate, DefenderTemplate, Skill,
        AttackerManager, DefenderManager, EffectStorage, ReferenceStorage, TimerStorage, LoadedTables
    }
    internal enum CallStage { Unknown, OrdinaryResist, NpcBase, OrdinaryCritical }
    internal sealed record Evidence(string MainSha256, string PackageContext, string Operation,
        string Epoch, Origin Origin, string Reference);
    internal sealed record Binding(ulong Address, string Identity, Evidence Evidence);
    internal sealed record FloatContract(Rounding Rounding, bool? MaskedExceptions, bool? NormalReturn);

    // Nullable raw reads distinguish unknown from a known zero/null pointer. Int32 carriers for
    // native words are checked before narrowing; no field receives a missing-value default.
    internal sealed record ActorRaw
    {
        internal int? Type144 { get; init; }
        internal ulong? VtableAddress { get; init; }
        internal uint? Kind1940 { get; init; }
        internal uint? Master188c { get; init; }
        internal uint? Related1894 { get; init; }
        internal ulong? VirtualProfile1850 { get; init; }
        internal ulong? Difficulty1bf8 { get; init; }
        internal uint? TemplateFlags434 { get; init; }
        internal int? CurrentDual914 { get; init; }
        internal int? EffectiveLevel1492 { get; init; }
        internal uint? StatRatio1514 { get; init; }
        internal int? MovementGetterByte { get; init; }
        internal int? MinimumDamage14dc { get; init; }
        internal int? MaximumDamage14e0 { get; init; }
        internal int? MagicalAccuracy14c4 { get; init; }
        internal int? MagicalCritical14c0 { get; init; }
        internal int? MagicalBoost1502 { get; init; }
        internal int? MagicalResistance149c { get; init; }
        internal int? MagicalSuppression1504 { get; init; }
        internal int? MagicalDefense1498 { get; init; }
        internal int? SelectedElementalWordSigned { get; init; }
        internal int? MagicalCriticalReduce14c8 { get; init; }
        internal int? MagicalCriticalDamageReduce14cc { get; init; }
        internal int? RawPoseLowNibble { get; init; }
        internal int? ImmunityMask27c { get; init; }
        internal int? TemplateFixedMagical3f8 { get; init; }
        internal int? TemplateExclusive450 { get; init; }
        internal int? OrdinaryMagicalPercent11c4 { get; init; }
        internal int? OrdinaryMagicalFlat11c8 { get; init; }
        internal int? SelectedSubtypePenetration { get; init; }
        internal int? SelectedSubtypePenetrationOffset { get; init; }
        internal int? SelectedElementalWordOffset { get; init; }
    }
    internal sealed record Manager(Binding? LoadedManager, bool? Present,
        int? Counter4400, uint? BoostFlags4444, uint? CriticalFlags4454);
    internal sealed record AttackerOptions(ulong? OneTimeBoostPointer200, ulong? OneTimeCriticalPointer208);
    internal sealed record Actor(Binding Current, uint ObjectId, string LifeIdentity,
        Binding LoadedTemplate, string LoadedTemplateRecipeRawSha256, RetailNpcSpellInstantParameters.NpcRecipe TemplateRecipe,
        ActorRaw Raw, Manager Manager);
    internal sealed record LoadedSkill(Binding Current, string RecipeRawSha256, int? SkillLevel,
        ImmutableDictionary<string, long?> SelectedFields);
    internal sealed record Invocation(Evidence Context, Actor Attacker, Actor Defender,
        LoadedSkill Skill, AttackerOptions AttackerOptions, FloatContract FloatingPoint,
        uint? LoadedCapSelector145a43940, int? ForceCompareInput);

    /// <summary>Original request endpoints before sorting. No RNG implementation or desired outcome.</summary>
    internal sealed record OriginalCall(CallStage Stage, int FirstEndpoint, int SecondEndpoint,
        int Returned, bool InclusiveAdapterInvoked, Evidence Evidence);
    internal sealed record Span(SpanRole Role, ulong Address, int Bytes, string AllocationIdentity, Evidence Evidence);
    internal sealed class RawWindow
    {
        internal const string Layout = "SpellAttack_Instant.CheckResist.header+record+cache.0x58";
        internal string LayoutIdentity { get; }
        internal Binding OriginalHeader { get; }
        internal string AllocationIdentity { get; }
        internal ulong AllocationStart { get; }
        internal int AllocationBytes { get; }
        internal bool? SerializedLifetimeAndCoverageKnown { get; }
        internal ImmutableArray<byte> Bytes { get; }
        internal ImmutableArray<Span> DisjointStorage { get; }
        internal RawWindow(string layoutIdentity, Binding originalHeader, string allocationIdentity,
            ulong allocationStart, int allocationBytes, bool? serializedLifetimeAndCoverageKnown,
            IEnumerable<byte> bytes, IEnumerable<Span> disjointStorage)
        {
            LayoutIdentity = layoutIdentity; OriginalHeader = originalHeader;
            AllocationIdentity = allocationIdentity; AllocationStart = allocationStart; AllocationBytes = allocationBytes;
            SerializedLifetimeAndCoverageKnown = serializedLifetimeAndCoverageKnown;
            // Retain at most one invalid sentinel beyond each admitted bound; Plan rejects it.
            Bytes = bytes.Take(0x59).ToImmutableArray(); DisjointStorage = disjointStorage.Take(65).ToImmutableArray();
        }
    }
    internal sealed record Diagnostic(string Code, string Path, string Message);
    internal sealed record Write(int Sequence, string Stage, int Offset, string BeforeHex, string AfterHex);
    internal sealed record CounterOwnerIntent(ulong ReceiverAddress, uint Mask, string NativeTarget)
    {
        internal uint? OriginalTick => null;
        internal uint? Deadline => null;
        internal bool Executed => false;
    }
    internal sealed class Result
    {
        internal Evidence Context { get; }
        internal int PreliminaryReturn { get; }
        internal int FinalRecordDamage { get; }
        internal bool WrapperSuccess { get; }
        internal bool HeaderWritten => WrapperSuccess;
        internal bool FinalRecordWritten => WrapperSuccess;
        internal ImmutableArray<byte> WindowBefore { get; }
        internal ImmutableArray<byte> WindowAfter { get; }
        internal ImmutableArray<Write> Writes { get; }
        internal ImmutableArray<OriginalCall> ConsumedOriginalCalls { get; }
        internal ImmutableArray<CounterOwnerIntent> CounterOwners { get; }
        internal ImmutableDictionary<string, JsonElement> Stages { get; }
        internal ImmutableDictionary<string, uint> FloatBits { get; }
        internal int? HpResult => null;
        internal bool ActivationReady => false;
        internal bool NativeOracle => false;
        internal Result(Evidence context, int preliminaryReturn, int finalRecordDamage, bool wrapperSuccess,
            IEnumerable<byte> before, IEnumerable<byte> after, IEnumerable<Write> writes,
            IEnumerable<OriginalCall> consumed, IEnumerable<CounterOwnerIntent> owners,
            IDictionary<string, JsonElement> stages, IDictionary<string, uint> floatBits)
        {
            Context = context; PreliminaryReturn = preliminaryReturn; FinalRecordDamage = finalRecordDamage;
            WrapperSuccess = wrapperSuccess; WindowBefore = before.ToImmutableArray(); WindowAfter = after.ToImmutableArray();
            Writes = writes.ToImmutableArray(); ConsumedOriginalCalls = consumed.ToImmutableArray(); CounterOwners = owners.ToImmutableArray();
            Stages = stages.ToImmutableDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal);
            FloatBits = floatBits.ToImmutableDictionary(StringComparer.Ordinal);
        }
    }
    internal sealed record Outcome(Result? Value, ImmutableArray<Diagnostic> Diagnostics)
    {
        [MemberNotNullWhen(true, nameof(Value))]
        internal bool Success => Value is not null && Diagnostics.IsEmpty;
    }
    internal sealed class ContractError(string code, string path, string message) : Exception(message)
    {
        internal string Code { get; } = code;
        internal string Path { get; } = path;
    }
    internal static void Need([DoesNotReturnIf(false)] bool condition, string code, string path, string message)
    { if (!condition) throw new ContractError(code, path, message); }
    internal static bool Text(string? value, int maximum = 4096) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum && !value.Any(char.IsControl);
}
