using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>
/// Inactive finite ordinary NPC physical target check and calculation. Computes raw hit/cache,
/// selected Begin chance, natural NPC defenses, lazy relation global and exact damage records.
/// It does not implement AddSkillEffect target admission, SkillPhyAttack emission/delay, skill
/// completion, HP ownership or a player defender. Current declarations are not observations.
/// </summary>
internal static class RetailNpcPhysicalInstantCalculation
{
    internal const ulong NpcVtable = 0x1412b8470;
    internal const ulong NoneAttenuation = 0x140b60ec0;
    internal enum Origin { Unknown, MathematicalFixture, SourceBoundDeclaration, ObservedOriginal }
    internal enum Entry { TargetCheckThenBeginAndCalculate, CalculatorCall }
    internal enum CallKind { Draw, CounterClock }
    internal enum Stage { Dodge, BeginChance, Base, BaseRandom, Additional, Block, Parry, Critical, DodgeClock, BlockClock, ParryClock }
    internal enum ConditionCopyKind { Unknown, SkillPhyAttackEffect0StackCopy }
    internal enum SpanRole { Attacker, Defender, AttackerTemplate, DefenderTemplate, Skill, Manager, Effect, Reference, Timer, Header, Cache, Record, Condition }
    internal sealed record Evidence(string MainSha256, string PackageContext, string Operation, string Epoch, Origin Origin, string Receipt);
    internal sealed record Binding(ulong Address, string Identity, Evidence Evidence);
    internal sealed record Floating(bool? RoundToNearestEven, bool? MaskedExceptions, bool? NormalReturn);
    internal sealed record RawActor
    {
        internal int? Type144 { get; init; }
        internal ulong? Vtable { get; init; }
        internal uint? Kind1940 { get; init; }
        internal uint? Master188c { get; init; }
        internal uint? Related1894 { get; init; }
        internal ulong? Template1848 { get; init; }
        internal ulong? VirtualProfile1850 { get; init; }
        internal ulong? Difficulty1bf8 { get; init; }
        internal uint? Pose13c { get; init; }
        internal uint? Abnormal1250 { get; init; }
        internal int? Current914 { get; init; }
        internal uint? TemplateFlags434 { get; init; }
        internal byte? TemplateFlags3f0 { get; init; }
        internal byte? TemplateExclusive450 { get; init; }
        internal int? TemplateFixedPhysical3f4 { get; init; }
        internal ushort? Level1492 { get; init; }
        internal uint? Ratio1514 { get; init; }
        internal ushort? Minimum14dc { get; init; }
        internal ushort? Maximum14e0 { get; init; }
        internal uint? AdditiveRate8dcBits { get; init; }
        internal int? Flat14d4 { get; init; }
        internal ushort? Accuracy14aa { get; init; }
        internal ushort? Evasion14ae { get; init; }
        internal ushort? Parry14b4 { get; init; }
        internal ushort? Block14b6 { get; init; }
        internal uint? Defense1494 { get; init; }
        internal ushort? Critical14bc { get; init; }
        internal ushort? CriticalReduce14c6 { get; init; }
        internal ushort? CriticalDamageReduce14ca { get; init; }
        internal ulong? OneTime200 { get; init; }
        internal ulong? OneTime208 { get; init; }
        internal byte? Movement189 { get; init; }
        internal int? StateIndex13d0 { get; init; }
        internal int? Heading12c { get; init; }
        internal uint? X120Bits { get; init; }
        internal uint? Y124Bits { get; init; }
    }
    internal sealed record Manager(Binding Binding, ulong? CurrentPointer158, int? Force43e0, int? Force43e8, int? Force43f0);
    internal sealed record Actor(Binding Current, Binding Template, int NpcId, string LifeGeneration, RawActor Raw, Manager Manager);
    internal sealed record LoadedRow(Binding Current, int Index, ImmutableArray<int> Values);
    internal sealed record LoadedTables(LoadedRow AttackerMovement, LoadedRow DefenderMovement,
        LoadedRow RandomType3, Binding HeadingTableA, Binding HeadingTableB,
        ImmutableArray<uint> HeadingABits, ImmutableArray<uint> HeadingBBits,
        Binding ParryTable, ImmutableArray<int> ParryRows, Binding DodgeRow, int? DodgeValue);
    internal sealed record ConditionProducer(ConditionCopyKind Kind, Binding CallerFrameRbp, ulong SourceSkillAddress,
        ulong SourceDescriptorAddress, ulong DestinationAddress, ulong FirstReadSite, ulong LastWriteSite,
        ulong ArgumentLeaSite, ulong ArgumentStoreSite, ulong CalculatorCallSite);
    internal sealed record CurrentSkill(Binding Current, ImmutableDictionary<string, long?> Words,
        Binding ConditionCopy, ImmutableArray<byte> Condition480, ulong? AttenuationFunction,
        byte? ExclusiveEc4, int? RunInfo58, ushort? Sharing90, bool? CurrentRecipeAndCopyBound, ConditionProducer ConditionProducer);
    internal sealed record Globals(Binding RaceMaskBinding, Binding RaceLimitBinding, ulong? RaceMask154827468, int? RaceLimit14f523f78);
    internal sealed record Span(SpanRole Role, ulong Address, int Bytes, string AllocationIdentity, Evidence Evidence);
    internal sealed record RawStorage(Binding Header, Binding Cache, Binding Record,
        ImmutableArray<byte> HeaderBytes, ImmutableArray<byte> CacheBytes, ImmutableArray<byte> RecordBytes,
        ImmutableArray<Span> Spans, bool? OriginalCallerLayoutAndInitializationBound, bool? SerializedNoOtherWriter);
    internal sealed record OriginalCall(CallKind Kind, Stage Stage, ulong CallSite, int First, int Second,
        uint Returned, bool InclusiveAdapterInvoked, Evidence Evidence);
    internal sealed record Input(Entry Entry, Evidence Context, P.SelectedPhysical Selected, Actor Attacker, Actor Defender,
        CurrentSkill Skill, LoadedTables Tables, Globals Globals, RawStorage Storage, Floating Floating,
        ImmutableArray<uint> DefenderCounterDeadlines, ImmutableArray<OriginalCall> OriginalCalls,
        bool? DiagnosticsDisabled, bool? NormalReturnNoInlineGameWriter);
    internal sealed record Diagnostic(string Code, string Path, string Message, bool UnsupportedProgram);
    internal sealed record Write(int Sequence, ulong NativeSite, SpanRole Role, int Offset, string BeforeHex, string AfterHex);
    internal sealed record GlobalWrite(int Sequence, ulong NativeSite, ulong Before, ulong After);
    internal sealed record CounterWrite(int Sequence, ulong NativeSite, int Offset, uint Before, uint After, uint OriginalTick);
    internal sealed record Trace(int Sequence, string Stage, ulong NativeSite, long Value);
    internal sealed record Result(bool CheckReached, bool Hit, bool BeginInvoked, int? BeginFallback,
        int? PreliminaryReturn, int? FinalRecordDamage, ImmutableArray<byte> HeaderAfter,
        ImmutableArray<byte> CacheAfter, ImmutableArray<byte> RecordAfter, ulong? RaceMaskAfter,
        ImmutableArray<uint> CounterDeadlinesAfter, ImmutableArray<Write> Writes,
        ImmutableArray<GlobalWrite> GlobalWrites, ImmutableArray<CounterWrite> CounterWrites,
        ImmutableArray<Trace> Trace, ImmutableArray<OriginalCall> ConsumedCalls,
        ImmutableDictionary<string, uint> FloatBits)
    {
        internal int? HpResult => null;
        internal int? ApplicationCell => null;
        internal bool ActivationReady => false;
        internal bool NativeOracle => false;
    }
    internal sealed record Outcome(Result? Value, Diagnostic? Diagnostic)
    {
        [MemberNotNullWhen(true, nameof(Value))]
        internal bool Success => Value is not null && Diagnostic is null;
    }

    internal static Outcome Evaluate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try { return new(new Replay(input).Run(), null); }
        catch (ContractError error) { return new(null, new(error.Code, error.Path, error.Message, error.Unsupported)); }
        catch (ArithmeticException error) { return new(null, new("arithmetic", "/calculation", error.Message, false)); }
    }

    private sealed class Replay(Input input)
    {
        private readonly List<Write> writes = [];
        private readonly List<GlobalWrite> globals = [];
        private readonly List<CounterWrite> counters = [];
        private readonly List<Trace> trace = [];
        private readonly List<OriginalCall> consumed = [];
        private readonly Dictionary<string, uint> floatBits = new(StringComparer.Ordinal);
        private byte[] header = [], cache = [], record = [];
        private uint[] deadlines = [];
        private ulong? raceMask = input.Globals.RaceMask154827468;
        private int cursor, sequence;
        private P.SelectedPhysical S => input.Selected;
        private RawActor A => input.Attacker.Raw;
        private RawActor D => input.Defender.Raw;
        private RetailNpcNormalScope scope = null!;
        private RetailNpcNormalStatusMath.Context statusContext = null!;
        private int MovementA(int index) => input.Tables.AttackerMovement.Values[index];
        private int MovementD(int index) => input.Tables.DefenderMovement.Values[index];
        private byte Pose => (byte)(K(D.Pose13c, "defender pose") & 15);
        private uint RatioA => K(A.Ratio1514, "attacker ratio");
        private uint RatioD => K(D.Ratio1514, "defender ratio");

        internal Result Run()
        {
            Validate();
            header = input.Storage.HeaderBytes.ToArray(); cache = input.Storage.CacheBytes.ToArray();
            record = input.Storage.RecordBytes.ToArray(); deadlines = input.DefenderCounterDeadlines.ToArray();
            bool checkedHit = input.Entry == Entry.TargetCheckThenBeginAndCalculate;
            if (checkedHit && !CheckHit()) return Finish(checkedHit, false, false, null, null, null);
            int? fallback = null;
            if (checkedHit)
            {
                int chance = unchecked(S.SignedSourceLevel * S.Chance4b8 + S.Chance4bc);
                int sample = Draw(Stage.BeginChance, 0x140bbd1fe, 1, 100);
                fallback = (uint)sample > unchecked((uint)Math.Min(chance, 100)) ? 1 : 0;
                Need(chance == 100 && fallback == 0, "chance", "/Begin", "Outside the two selected chance100 recipes.");
                T("BeginFallback", 0x140bbd226, fallback.Value);
            }
            int hitExpression = unchecked(S.HitRate458 * S.SignedSourceLevel + S.HitRate45c);
            float hit = F("wrapper_hit_mult", Mul(FromInt(hitExpression), Bits(0x3c23d70a)));
            int flat = unchecked(S.Flat460 * S.SignedSourceLevel + S.Flat464);
            int extra = Math.Max(0, unchecked(S.Additional474 * S.SignedSourceLevel + S.Additional478));
            float probability = Add(Mul(FromInt(S.SignedSourceLevel), Bits(S.CriticalProbability44cBits)), Bits(S.CriticalProbability450Bits));
            T("ConditionStackCopyBound", 0x140bb8429, 48);
            if (checkedHit)
            {
                // Bound actual NPC vslot+5e8 ->1400b47c0:32c0c3 (XOR AL,AL;RET).
                // These two reached preparation calls have no writers or child calls.
                T("DefenderPrepareNoOp", 0x140bb8430, 0x1400b47c0);
                T("AttackerPrepareNoOp", 0x140bb843f, 0x1400b47c0);
            }
            float coefficient = F("base_coefficient", Add(Add(Bits(K(A.AdditiveRate8dcBits, "additive rate8dc")), 1f), hit));
            int sampleBase = Draw(Stage.Base, 0x14036ddc9, K(A.Minimum14dc, "minimum14dc"), K(A.Maximum14e0, "maximum14e0"));
            float amount = F("base_draw_product", Mul(FromInt(sampleBase), coefficient));
            amount = F("base_with_flat_zero", Add(amount, FromInt(unchecked(K(A.Flat14d4, "flat14d4") + flat))));
            int random = Draw(Stage.BaseRandom, 0x14036de03, 0, 99);
            amount = F("base_randomized", Mul(amount, Div(input.Tables.RandomType3.Values[random / 5], 100f)));
            if (extra != 0 && (input.Skill.Condition480[0x2d] == 0 || Relation()))
            {
                _ = Draw(Stage.Additional, 0x14036de81, 1, 100);
                Write32(SpanRole.Cache, 8, 1, 0x14036de98);
                float additional = F("additional_after_base_times_zero", Add(Mul(amount, 0f), FromInt(extra)));
                amount = F("with_additional", Add(amount, additional));
            }
            // Source-bound CalcDamageAttenuation_None returns the exact int32 CVTT input.
            int attenuation = Cvtt(amount);
            T("NoneAttenuationInputReturn", NoneAttenuation, attenuation);
            float attenuationFloat = FromInt(attenuation);
            amount = F("movement_attack", Add(Mul(Div(MovementA(0), 100f), attenuationFloat), attenuationFloat));
            amount = F("preliminary_floor_one", amount < 1f ? 1f : amount);
            int preliminary = Cvtt(amount);
            var defense = RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope, amount,
                K(A.Level1492, "attacker level"), K(D.Level1492, "defender level"), false,
                K(D.Defense1494, "defense1494"), MovementD(1), RatioD);
            F("after_level", defense.AfterLevel); F("armor_term", defense.ArmorTerm);
            F("after_pdef_and_ratio", defense.BeforeClamp);
            amount = F("after_defense_floor_zero", defense.Damage);
            amount = NaturalDefense(amount);
            F("after_active_defense", amount);
            int criticalDraw = Draw(Stage.Critical, 0x14036e167, 1, 1000);
            int adjustedCritical = Cvtt(Mul(K(A.Critical14bc, "critical14bc"), probability));
            int numerator = unchecked(adjustedCritical + (Pose == 5 ? 500 : 0) - K(D.CriticalReduce14c6, "critical reduce14c6"));
            int threshold = Math.Min(500, Cvtt(Div(FromInt(numerator), Div(RatioD, 1000f))));
            bool critical = criticalDraw < threshold;
            T("CriticalThreshold", 0x14036e303, threshold);
            ushort criticalDamageReduction = K(D.CriticalDamageReduce14ca, "critical damage reduce14ca");
            float multiplier = 1f;
            if (critical)
            {
                int normalized = Cvtt(Div(criticalDamageReduction, Div(RatioA, 1000f)));
                multiplier = Math.Max(1f, Sub(1.5f, Div(FromInt(normalized), 1000f)));
                Write8(SpanRole.Record, 0x31, 12, 0x14036e472);
            }
            amount = F("after_critical", Mul(amount, multiplier));
            Write32(SpanRole.Cache, 4, critical ? 1 : 0, 0x14036e481);
            ushort sharing = K(input.Skill.Sharing90, "sharing90");
            if (sharing > 1) amount = Div(amount, sharing);
            F("after_sharing", amount);
            // Actual ordinary NPC classifier0/0 and fixed/exclusive zero guards were validated.
            T("Classifier0Both", 0x14036e71e, 0);
            amount = F("final_floor_one", amount < 1f ? 1f : amount);
            int firstConversion = Cvtt(amount);
            amount = F("final_integer_roundtrip", FromInt(firstConversion));
            int final = Cvtt(amount);
            Write32(SpanRole.Record, 0, final, 0x14036eae6);
            return Finish(checkedHit, true, checkedHit, fallback, preliminary, final);
        }

        private bool CheckHit()
        {
            Need(K(input.Skill.RunInfo58, "RunInfo58") <= 0 && S.ForceThreshold468 == 0, "force", "/current/threshold", "Current max(RunInfo58,R11) must be zero.", true);
            ForceZero("43e0", input.Defender.Manager.Force43e0, input.Tables.DodgeValue == 0);
            ForceZero("43f0", input.Defender.Manager.Force43f0, true);
            ForceZero("43e8", input.Defender.Manager.Force43e8, ParryRow() == 0);
            if (Pose != 5 && (K(A.Abnormal1250, "abnormal1250") & 0x649f04c) == 0 && K(input.Tables.DodgeValue, "dodge table row") == 0)
            {
                int accuracy = Math.Clamp(unchecked(K(A.Accuracy14aa, "accuracy14aa") +
                    unchecked(S.Accuracy43c * S.SignedSourceLevel + S.Accuracy440)), 0, 9999);
                Write32(SpanRole.Cache, 12, accuracy, 0x14036d501);
                int adjusted = RetailNpcNormalStatusMath.AdjustOwnerlessNpcDefense(statusContext,
                    RetailNpcNormalStatusMath.ChanceStage.Dodge, K(D.Evasion14ae, "evasion14ae"),
                    K(A.Level1492, "attacker level"), K(D.Level1492, "defender level"));
                int threshold = Probability(adjusted, accuracy, MovementD(5), 300);
                T("DodgeThreshold", 0x14036d690, threshold);
                if (Draw(Stage.Dodge, 0x14036d690, 1, 1000) <= threshold)
                {
                    Write32(SpanRole.Cache, 0, 0, 0x14036d6ac);
                    Counter(0x80, Stage.DodgeClock);
                    return false;
                }
            }
            return true;
        }

        private float NaturalDefense(float damage)
        {
            if (Pose != 5 && (K(A.Abnormal1250, "abnormal1250") & 0x649f04c) == 0)
            {
                int accuracy = BinaryPrimitives.ReadInt32LittleEndian(cache.AsSpan(12, 4));
                ushort block = K(D.Block14b6, "block14b6");
                if (block != 0)
                {
                    int adjusted = AdjustDefense(RetailNpcNormalStatusMath.ChanceStage.Block, block);
                    int threshold = Probability(adjusted, accuracy, MovementD(4), 500);
                    T("BlockThreshold", 0x14036efd2, threshold);
                    if (Draw(Stage.Block, 0x14036efd2, 1, 1000) <= threshold)
                    {
                        Write8(SpanRole.Record, 0x30, 4, 0x14036f028);
                        OrHeader(0x20, 0x14036f02c);
                        damage = Mul(damage, Bits(0x3f666666));
                        Counter(0x20, Stage.BlockClock);
                        return damage;
                    }
                }
                if (ParryRow() == 0)
                {
                    int adjusted = AdjustDefense(RetailNpcNormalStatusMath.ChanceStage.Parry, K(D.Parry14b4, "parry14b4"));
                    int threshold = Probability(adjusted, accuracy, MovementD(3), 400);
                    T("ParryThreshold", 0x14036f12d, threshold);
                    if (Draw(Stage.Parry, 0x14036f12d, 1, 1000) <= threshold)
                    {
                        Write8(SpanRole.Record, 0x30, 2, 0x14036f143);
                        damage = Mul(damage, Bits(0x3f666666));
                        OrHeader(0x40, 0x14036f162);
                        Counter(0x40, Stage.ParryClock);
                        return damage;
                    }
                }
            }
            Write8(SpanRole.Record, 0x30, 10, 0x14036f178);
            return damage;
        }

        private int AdjustDefense(RetailNpcNormalStatusMath.ChanceStage stage, ushort value) =>
            RetailNpcNormalStatusMath.AdjustOwnerlessNpcDefense(statusContext, stage, value,
                K(A.Level1492, "attacker level"), K(D.Level1492, "defender level"));
        private int Probability(int defense, int accuracy, int movement, int maximum) =>
            Math.Clamp(Cvtt(Div(FromInt(unchecked(defense + unchecked(movement - accuracy))), Div(RatioA, 1000f))), 0, maximum);
        private int ParryRow() => input.Tables.ParryRows[K(D.StateIndex13d0, "state index13d0")];
        private void ForceZero(string name, int? value, bool reached)
        {
            if (!reached) return;
            if (K(input.Defender.Manager.CurrentPointer158, "defender manager pointer") == 0) return;
            Need(K(value, "force" + name) == 0, "force", "/manager/" + name,
                "Active/negative force state requires the separately proved consume/removal owner.", true);
        }

        private bool Relation()
        {
            var bytes = input.Skill.Condition480;
            Need((bytes[0x2c] & 0xfc) == 0 && bytes.AsSpan(0x18, 20).ToArray().All(x => x == 0),
                "condition", "/condition/current", "Only the admitted direction-only half is supported.", true);
            if ((K(D.TemplateFlags3f0, "template3f0") & 4) != 0) return true;
            int heading = K(D.Heading12c, "heading12c");
            Need(heading is >= 0 and < 120, "heading", "/defender/heading", "Heading table index outside0..119.");
            float dx = Sub(Bits(K(D.X120Bits, "defender X")), Bits(K(A.X120Bits, "attacker X")));
            float dy = Sub(Bits(K(D.Y124Bits, "defender Y")), Bits(K(A.Y124Bits, "attacker Y")));
            float dot = F("relation_dot", Add(Mul(dy, Bits(input.Tables.HeadingABits[heading])), Mul(dx, Bits(input.Tables.HeadingBBits[heading]))));
            byte direction = bytes[0x2c];
            if (((direction & 1) != 0 && dot > 0f) || ((direction & 2) != 0 && dot <= 0f)) return true;
            ulong mask = K(raceMask, "current race mask154827468");
            if (mask == 0)
            {
                int limit = K(input.Globals.RaceLimit14f523f78, "current race limit14f523f78");
                Need(limit <= 63, "race-limit", "/globals/limit", "Lazy limits above63 are outside the finite loop domain.", true);
                for (int index = 0; index <= limit; index++)
                    mask |= 1ul << index;
                // Assembly370e40..e4e loops locally;370e50 publishes once AFTER the loop.
                // The saved pseudocode's apparent per-iteration store is not authoritative.
                if (limit >= 0) globals.Add(new(++sequence, 0x140370e50, 0, mask));
                raceMask = mask;
            }
            // Source/current selected half has race/class/status masks zero: no mapper/User class child.
            return false;
        }

        private int Draw(Stage stage, ulong site, int first, int second)
        {
            OriginalCall call = Next(CallKind.Draw, stage, site);
            int lower = Math.Min(first, second), upper = Math.Max(first, second);
            Need(call.First == first && call.Second == second && call.Returned <= int.MaxValue &&
                (int)call.Returned >= lower && (int)call.Returned <= upper &&
                call.InclusiveAdapterInvoked == (first != second), "original-call", "/calls",
                "Wrong original endpoints, return, adapter branch or order.");
            T("OriginalDraw:" + stage, site, call.Returned);
            return (int)call.Returned;
        }
        private OriginalCall Next(CallKind kind, Stage stage, ulong site)
        {
            Need(cursor < input.OriginalCalls.Length, "original-call", "/calls", "Missing reached original call.");
            OriginalCall call = input.OriginalCalls[cursor++]; Evidence(call.Evidence);
            Need(call.Kind == kind && call.Stage == stage && call.CallSite == site,
                "original-call", "/calls", "Wrong original stage, kind, site or order.");
            consumed.Add(call); return call;
        }
        private void Counter(uint mask, Stage stage)
        {
            OriginalCall call = Next(CallKind.CounterClock, stage, 0x1400b4650);
            Need(call.First == 0 && call.Second == 0 && !call.InclusiveAdapterInvoked,
                "original-clock", "/calls", "Counter clock receipt cannot describe an RNG request.");
            T("OriginalCounterClock:" + stage, call.CallSite, call.Returned);
            int index = mask == 0x20 ? 0 : mask == 0x40 ? 1 : 2;
            uint after = unchecked(call.Returned + 5000);
            counters.Add(new(++sequence, 0x1400b469a, 0x13dc + index * 4, deadlines[index], after, call.Returned));
            deadlines[index] = after;
        }
        private void OrHeader(uint mask, ulong site) => Write32(SpanRole.Header, 0,
            unchecked((int)(BinaryPrimitives.ReadUInt32LittleEndian(header) | mask)), site);
        private void Write32(SpanRole role, int offset, int value, ulong site)
        {
            byte[] bytes = role == SpanRole.Header ? header : role == SpanRole.Cache ? cache : record;
            string before = Convert.ToHexStringLower(bytes.AsSpan(offset, 4));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
            writes.Add(new(++sequence, site, role, offset, before, Convert.ToHexStringLower(bytes.AsSpan(offset, 4))));
        }
        private void Write8(SpanRole role, int offset, byte value, ulong site)
        {
            string before = record[offset].ToString("x2"); record[offset] = value;
            writes.Add(new(++sequence, site, role, offset, before, value.ToString("x2")));
        }
        private float F(string name, float value) { Normal(value); floatBits.Add(name, BitConverter.SingleToUInt32Bits(value)); return value; }
        private void T(string name, ulong site, long value) => trace.Add(new(++sequence, name, site, value));
        private Result Finish(bool check, bool hit, bool begin, int? fallback, int? preliminary, int? final)
        {
            Need(cursor == input.OriginalCalls.Length, "original-call", "/calls", "Extra original call receipts.");
            return new(check, hit, begin, fallback, preliminary, final, header.ToImmutableArray(), cache.ToImmutableArray(),
                record.ToImmutableArray(), raceMask, deadlines.ToImmutableArray(), writes.ToImmutableArray(), globals.ToImmutableArray(),
                counters.ToImmutableArray(), trace.ToImmutableArray(), consumed.ToImmutableArray(), floatBits.ToImmutableDictionary(StringComparer.Ordinal));
        }

        private void Validate()
        {
            Need(input.Entry is Entry.TargetCheckThenBeginAndCalculate or Entry.CalculatorCall, "entry", "/entry", "Unknown native entry.");
            Evidence(input.Context);
            Need(input.DiagnosticsDisabled == true && input.NormalReturnNoInlineGameWriter == true,
                "infrastructure", "/current", "Explicit diagnostic-off/normal-return/no-inline-game-writer interval required.");
            Need(input.Floating.RoundToNearestEven == true && input.Floating.MaskedExceptions == true && input.Floating.NormalReturn == true,
                "floating", "/floating", "Conditional RNE/masked-exception/normal-return contract required; actual MXCSR is unavailable.");
            Need(S is not null && S.Binding.NpcId == input.Attacker.NpcId && S.EffectTypeIndex == 0,
                "selected", "/selected", "Current source/binding/type mismatch.");
            Actor(input.Attacker); Actor(input.Defender);
            Need(input.Attacker.Current.Address != input.Defender.Current.Address, "actor", "/roles", "Distinct actors required.");
            scope = RetailNpcNormalScope.Create(P.MainSha256, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
                RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, input.Attacker.NpcId, input.Defender.NpcId);
            statusContext = RetailNpcNormalStatusMath.Context.Create(scope, P.MainSha256, input.Context.Receipt, false);
            Need((K(A.TemplateFlags434, "class434") & 0x200000) == 0 && K(A.Current914, "current914") == 0 &&
                (K(A.Pose13c, "attacker pose") & 0x80) == 0 && K(A.OneTime200, "one-time200") == 0 && K(A.OneTime208, "one-time208") == 0,
                "source-branch", "/attacker", "Combined/booster/one-time branches are outside this finite domain.", true);
            Need((K(A.Abnormal1250, "abnormal1250") & 0x20) == 0, "blind", "/attacker", "Blind RNG/consume branch is unsupported.", true);
            Need(K(D.TemplateFixedPhysical3f4, "physical fixed3f4") == 0 && K(A.TemplateExclusive450, "exclusive450") == 0,
                "override", "/templates", "Actual fixed/exclusive zero guards required.", true);
            Bind(input.Skill.Current); Bind(input.Skill.ConditionCopy);
            Need(input.Skill.CurrentRecipeAndCopyBound == true && input.Skill.AttenuationFunction == NoneAttenuation &&
                K(input.Skill.ExclusiveEc4, "skill exclusiveEc4") == 0, "skill", "/current/skill", "Actual recipe/copy/None/exclusive guards required.");
            foreach (var item in S.Constructed)
                Need(input.Skill.Words.TryGetValue(item.Key, out long? current) && current == item.Value,
                    "skill-word", "/current/" + item.Key, "Unknown/stale constructed selected word.");
            Need(input.Skill.Condition480.Length == 48 && input.Skill.Condition480.AsSpan().SequenceEqual(S.Condition480.AsSpan()),
                "condition", "/current/condition", "Current descriptor must independently bind the admitted constructed recipe.");
            ConditionProducer producer=input.Skill.ConditionProducer;
            Need(producer.Kind==ConditionCopyKind.SkillPhyAttackEffect0StackCopy,
                "condition-producer", "/current/condition/producer", "Unknown or unsupported original descriptor producer.");
            Bind(producer.CallerFrameRbp);
            Need(input.Skill.Current.Address<=ulong.MaxValue-0x4b0 && producer.CallerFrameRbp.Address<=ulong.MaxValue-0xd8 &&
                producer.SourceSkillAddress==input.Skill.Current.Address && producer.SourceDescriptorAddress==input.Skill.Current.Address+0x480 &&
                producer.DestinationAddress==input.Skill.ConditionCopy.Address && producer.DestinationAddress==producer.CallerFrameRbp.Address+0x70 &&
                producer.FirstReadSite==0x140bb83eb && producer.LastWriteSite==0x140bb8429 &&
                producer.ArgumentLeaSite==0x140bb84d1 && producer.ArgumentStoreSite==0x140bb84d5 && producer.CalculatorCallSite==0x140bb84fa &&
                input.Storage.Header.Address==producer.CallerFrameRbp.Address && input.Storage.Record.Address==producer.CallerFrameRbp.Address+0xa0,
                "condition-producer", "/current/condition/producer", "Actual Skill+480 -> callerRBP+70 copy, argument and record0/header layout must be bound.");
            Need(S.NoResist438 == 0 && Math.Max(K(input.Skill.RunInfo58, "RunInfo58"), S.ForceThreshold468) == 0,
                "force", "/current/threshold", "No-resist and force threshold are independent known zero inputs.", true);
            Need(S.RandomType46c == 3 && S.RandomMask470 == 1, "random-table", "/selected", "Only the complete selected type3/BasicDMG recipe is admitted.");
            _ = K(input.Skill.Sharing90, "sharing90");
            Tables();
            if(input.Entry==Entry.CalculatorCall)
            {
                // Finite call construction precondition, not a replay of earlier effect consumers.
                ForceZero("43e0",input.Defender.Manager.Force43e0,input.Tables.DodgeValue==0);
                ForceZero("43f0",input.Defender.Manager.Force43f0,true);
                ForceZero("43e8",input.Defender.Manager.Force43e8,ParryRow()==0);
            }
            Bind(input.Globals.RaceMaskBinding); Bind(input.Globals.RaceLimitBinding);
            Need(input.Globals.RaceMaskBinding.Address == 0x154827468 && input.Globals.RaceLimitBinding.Address == 0x14f523f78,
                "global-binding", "/globals", "Wrong native global addresses.");
            Need(input.DefenderCounterDeadlines.Length == 4, "counter", "/current/deadlines", "All four current counter cells required.");
            Storage();
        }
        private void Actor(Actor actor)
        {
            Bind(actor.Current); Bind(actor.Template); Bind(actor.Manager.Binding); Text(actor.LifeGeneration);
            RawActor r = actor.Raw;
            Need(r.Type144 == 2 && r.Vtable == NpcVtable && r.Kind1940 == 1 && r.Master188c == 0 && r.Related1894 == 0 &&
                r.Template1848 == actor.Template.Address && r.VirtualProfile1850 == 0 && r.Difficulty1bf8 == 0 && actor.NpcId > 0,
                "actor", "/current/actor", "Only actual ordinary ownerless NPC type2/kind1/vtable bindings are supported.");
            Need(r.Level1492 is >= 1 and <= 100 && r.Ratio1514 is >= 1000 and <= 65000,
                "scalar", "/current/actor", "Known effective level1..100 and ratio1000..65000 required.");
            ulong manager = K(actor.Manager.CurrentPointer158, "manager pointer");
            Need(actor.Manager.Binding.Address == (manager == 0 ? actor.Current.Address + 0x158 : manager),
                "manager-binding", "/current/manager", "Actual manager pointer or null pointer-cell address must match its binding.");
        }
        private void Tables()
        {
            LoadedRow a = input.Tables.AttackerMovement, d = input.Tables.DefenderMovement;
            foreach (LoadedRow row in new[] { a, d })
            {
                Bind(row.Current);
                Need(row.Index is >= 0 and <= 255 && row.Values.Length == 7 && row.Values.All(x => x is >= -10000 and <= 10000)
                    && row.Current.Address == 0x141069ed0ul + (ulong)row.Index * 0x1c,
                    "movement", "/tables", "Original loaded seven-int row/address/index required.");
            }
            Need(a.Index == K(A.Movement189, "attacker slot189") && d.Index == K(D.Movement189, "defender slot189"),
                "movement", "/current/getters", "Actual per-actor slot189 reads must bind their loaded rows.");
            Need(a.Index != d.Index || a.Values.AsSpan().SequenceEqual(d.Values.AsSpan()),
                "movement", "/tables/current", "One serialized loaded movement row cannot have two different values.");
            LoadedRow random = input.Tables.RandomType3; Bind(random.Current);
            Need(random.Index == 3 && random.Current.Address == 0x1428b7410ul + 3 * 20 * 4 &&
                random.Values.Length == 20 && random.Values.Select((v, i) => v == (i < 7 ? 90 : i < 13 ? 100 : 110)).All(x => x),
                "random-table", "/tables/type3", "Current loaded type3 table must bind the exact source row; no default row.");
            Bind(input.Tables.HeadingTableA); Bind(input.Tables.HeadingTableB); Bind(input.Tables.ParryTable); Bind(input.Tables.DodgeRow);
            Need(input.Tables.HeadingTableA.Address == 0x14111ca60 && input.Tables.HeadingTableB.Address == 0x14111cc40 &&
                input.Tables.HeadingABits.Length == 120 && input.Tables.HeadingBBits.Length == 120,
                "heading", "/tables", "Both source/current120-word heading tables required.");
            Need(WordsHash(input.Tables.HeadingABits) == "ee3f8c41695a4c1c9ec1fe9c52d82facad293638e01317f881c765789e6ea247" &&
                WordsHash(input.Tables.HeadingBBits) == "5a95593d012096b1451a7b27d69e1b2e84eef60a7df51170d3e141334fcef1fb",
                "heading", "/tables/words", "Heading values must match both complete pinned PE tables; no desired relation table.");
            int index = K(D.StateIndex13d0, "state index13d0");
            Need(index is >= 0 and < 32 && input.Tables.ParryTable.Address == 0x141122d00 && input.Tables.ParryRows.Length == 32 &&
                input.Tables.ParryRows.Select((v,i) => v == (i == 1 ? 1 : 0)).All(x=>x) &&
                input.Tables.DodgeRow.Address == 0x145a2c630ul + (ulong)index * 4,
                "state-table", "/tables", "Current raw state index, pinned parry table and original BSS row binding required.");
            _ = K(input.Tables.DodgeValue, "dodge table row");
        }
        private void Storage()
        {
            RawStorage storage = input.Storage;
            Bind(storage.Header); Bind(storage.Cache); Bind(storage.Record);
            Need(storage.OriginalCallerLayoutAndInitializationBound == true && storage.SerializedNoOtherWriter == true &&
                storage.HeaderBytes.Length == 4 && storage.CacheBytes.Length == 16 && storage.RecordBytes.Length == 0x38 &&
                BinaryPrimitives.ReadInt32LittleEndian(storage.CacheBytes.AsSpan(0,4)) == 0 && storage.RecordBytes[0x31] == 0,
                "storage", "/storage", "Original physical4/16/56-byte layout/initialization and serialized interval required.");
            Need(BinaryPrimitives.ReadUInt32LittleEndian(storage.HeaderBytes.AsSpan())==0 &&
                storage.RecordBytes.AsSpan(0,53).ToArray().All(x=>x==0),
                "storage", "/storage/initialization", "Typed first-record caller zeros header4 and record0..52; only padding53..55 is unspecified.");
            var required = new[] { (SpanRole.Header, storage.Header.Address,4), (SpanRole.Cache,storage.Cache.Address,16),
                (SpanRole.Record,storage.Record.Address,56), (SpanRole.Condition,input.Skill.ConditionCopy.Address,48),
                (SpanRole.Attacker,input.Attacker.Current.Address,0x2000), (SpanRole.Defender,input.Defender.Current.Address,0x2000),
                (SpanRole.AttackerTemplate,input.Attacker.Template.Address,0x500), (SpanRole.DefenderTemplate,input.Defender.Template.Address,0x500),
                (SpanRole.Skill,input.Skill.Current.Address,0x1000) };
            foreach (var (role, address, bytes) in required)
                Need(storage.Spans.Count(x=>x.Role==role && x.Address==address && x.Bytes>=bytes)==1,
                    "alias", "/storage/spans", "Missing/ambiguous original storage/actor coverage.");
            foreach (Actor actor in new[] {input.Attacker,input.Defender})
                if(actor.Manager.CurrentPointer158 != 0)
                    Need(storage.Spans.Count(x=>x.Role==SpanRole.Manager && x.Address==actor.Manager.CurrentPointer158 && x.Bytes>=0x4500)==1,
                        "alias", "/storage/manager", "Reached current manager's complete consumed storage must be disjoint from outputs.");
            foreach (Span span in storage.Spans) { Evidence(span.Evidence); Text(span.AllocationIdentity);
                Need(span.Address != 0 && span.Bytes > 0 && span.Address <= ulong.MaxValue-(ulong)span.Bytes,
                    "alias", "/storage/spans", "Malformed original memory range."); }
            for(int i=0;i<storage.Spans.Length;i++) for(int j=0;j<i;j++)
            {
                Span x=storage.Spans[i], y=storage.Spans[j];
                Need(x.Address+(ulong)x.Bytes<=y.Address || y.Address+(ulong)y.Bytes<=x.Address,
                    "alias", "/storage/spans", "Record/cache/header/copy and actor/effect/ref/timer storage must be disjoint.");
            }
        }
        private void Bind(Binding binding) { Need(binding.Address!=0,"binding","/binding","Null/unbound address."); Text(binding.Identity); Evidence(binding.Evidence); }
        private void Evidence(Evidence evidence)
        {
            Need(evidence.MainSha256==P.MainSha256 && evidence.PackageContext==P.PackageContext &&
                evidence.Operation==input.Context.Operation && evidence.Epoch==input.Context.Epoch &&
                evidence.Origin is Origin.MathematicalFixture or Origin.SourceBoundDeclaration or Origin.ObservedOriginal,
                "evidence", "/evidence", "Wrong or unknown Main/package/operation/epoch/provenance.");
            Text(evidence.Operation); Text(evidence.Epoch); Text(evidence.Receipt);
        }
    }
    private static T K<T>(T? value, string field) where T:struct
    { Need(value.HasValue,"unknown","/current/"+field,"Unknown reached native field."); return value.GetValueOrDefault(); }
    private static void Text(string value) => Need(!string.IsNullOrWhiteSpace(value),"identity","/identity","Missing identity/receipt.");
    private static string WordsHash(ImmutableArray<uint> words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for(int index=0;index<words.Length;index++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index*4,4), words[index]);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
    private static void Need([DoesNotReturnIf(false)] bool value,string code,string path,string message,bool unsupported=false)
    { if(!value) throw new ContractError(code,path,message,unsupported); }
    private sealed class ContractError(string code,string path,string message,bool unsupported):Exception(message)
    { internal string Code=>code; internal string Path=>path; internal bool Unsupported=>unsupported; }
    private static float Bits(uint bits) => Normal(BitConverter.UInt32BitsToSingle(bits));
    private static float Normal(float value)
    {
        uint bits=BitConverter.SingleToUInt32Bits(value)&0x7fffffff;
        Need(bits<0x7f800000 && (bits==0 || bits>=0x00800000),"floating","/intermediate","Nonfinite/subnormal consumed value or intermediate.");
        return value;
    }
    private static int Cvtt(float value)
    { Normal(value); Need(value>=-2147483648f && value<2147483648f,"conversion","/intermediate","Unrepresentable signed32 CVTT input."); return (int)value; }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float FromInt(int value)=>Normal(value);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a,float b)=>Normal(a+b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a,float b)=>Normal(a-b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a,float b)=>Normal(a*b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a,float b)=>Normal(a/b);
}
