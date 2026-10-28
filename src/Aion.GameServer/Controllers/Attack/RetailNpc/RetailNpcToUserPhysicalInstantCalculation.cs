using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Aion.GameServer.Commons.Utils.RetailRandom;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserPhysicalSkillPveModifier;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive complete admitted selected physical target-check/Begin/calculator chain
/// with an actual User defender. Computes every admitted getter, shield, RNG and modifier child.
/// It does not complete SkillPhyAttack's later effects/emission/delay, apply HP or route gameplay.</summary>
internal static class RetailNpcToUserPhysicalInstantCalculation
{
    internal enum ClockRole { HitMovement, CalculatorMovement, DodgeCounter, BlockCounter, ParryCounter }
    internal sealed record Clock(ClockRole Role, ulong CallerSite, ulong TickSite, uint Value, ulong ActorAddress,
        string Operation, string Epoch, string ActorGeneration, string Receipt);
    internal sealed record Floating(bool? NearestEven, bool? MaskedExceptions, bool? FlushToZero,
        bool? DenormalsAreZero, bool? NormalReturn);
    internal sealed record Infrastructure(bool? SerializedNoOtherWriter, bool? NoInlineGameplayWriter,
        bool? NormalInfrastructureReturn, bool? UnconsumedFormattingScratchExcluded);
    internal sealed record Limits(int MaxImages, int MaxBytes, int MaxSteps, int MaxRandomOperations,
        int MaxRawDraws, long MaxRandomLogicalTraceBytes);
    internal sealed record CallBoundary(ulong SourceActor, ulong UserActor, ulong SourceTemplate,
        ulong CurrentSkill, ulong RunInfo, ulong CallerFrameRbp, ulong Cache,
        string SourceLife, string UserLife, string CallerRecipeReceipt);
    internal sealed record Input(Context Context, P.SelectedPhysical Selected, Memory Memory,
        CallBoundary Boundary, RetailRandomTransaction Random, ImmutableArray<Clock> Clocks,
        Floating Floating, Infrastructure Infrastructure, Limits Limits);
    internal sealed record RandomCall(int Sequence, string Stage, ulong CallerSite, int First, int Second,
        int ComputedReturn, ImmutableArray<RetailRandomDrawTrace> RawDraws, ulong NextGeneration);
    internal sealed record Result(bool Hit, bool BeginInvoked, int? BeginFallback, int? PreliminaryReturn,
        int? FinalRecordDamage, Memory CalculatedMemory, ImmutableArray<Step> Steps,
        ImmutableArray<Clock> ConsumedClocks, ImmutableArray<RandomCall> RandomCalls,
        RetailRandomState? RandomStateAfter, ulong RandomGenerationAfter,
        long RandomLogicalTraceBytes, M.Result? Pve, ImmutableDictionary<string, uint> FloatBits)
    {
        internal int? HpResult => null;
        internal int? ApplicationCell => null;
        internal bool CurrentActorObserved => false;
        internal bool HostEffectsExecuted => false;
        internal bool ActivationReady => false;
        internal bool GlobalNativeRngOrderProved => false;
    }
    internal sealed record Outcome(Result? Value, Failure? Failure, RetailRandomTransactionResult? DiagnosticRandomPrefix = null)
    {
        [MemberNotNullWhen(true, nameof(Value))]
        internal bool Success => Value is not null && Failure is null;
        internal bool DiagnosticPrefixCommittable => false;
    }
    internal static Outcome Calculate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Replay? replay = null;
        try { replay = new(input); return new(replay.Run(), null); }
        catch (Domain ex) { return new(null, ex.Failure, replay?.RandomFailure); }
        catch (ArithmeticException ex) { return new(null, new("arithmetic", "calculation", 0, ex.Message, true)); }
    }

    private sealed class Replay(Input input)
    {
        private Cursor c = null!;
        private int clockIndex, randomOperations, rawDraws;
        private long traceBytes;
        private readonly List<Clock> clocks = [];
        private readonly List<RandomCall> randomCalls = [];
        private readonly Dictionary<string, uint> floats = new(StringComparer.Ordinal);
        private RetailRandomState? randomState;
        private ulong randomGeneration = input.Random?.InitialGeneration ?? 0;
        internal RetailRandomTransactionResult? RandomFailure { get; private set; }
        private CallBoundary B => input.Boundary;
        private P.SelectedPhysical S => input.Selected;
        private ulong A => B.SourceActor;
        private ulong D => B.UserActor;
        private ulong Record => B.CallerFrameRbp + 0xa0;
        private ulong Header => B.CallerFrameRbp;
        private byte Pose() => (byte)(c.U32(D + 0x13c, 0x14036d2bb, "User.pose13c") & 15);
        private uint RatioA() => Ratio(A);
        private uint RatioD() => Ratio(D);
        private uint Ratio(ulong actor)
        {
            uint v = c.U32(actor + 0x1514, 0x14036d57e, "actor.stat_ratio1514");
            Need(v is >= 1000 and <= 65000, "range", "stat_ratio", 0, "Finite construction ratio1000..65000 required."); return v;
        }
        private int CurrentWord(int offset, ulong site)
        {
            uint value = c.U32(B.CurrentSkill + (ulong)offset, site, "Skill+" + offset.ToString("x"));
            if (S.Constructed.TryGetValue(offset.ToString("x"), out long expected))
                Need(value == unchecked((uint)expected), "stale-skill", offset.ToString("x"), site,
                    "Current selected word differs from the privately constructed source recipe.");
            return unchecked((int)value);
        }
        private float F(string name, float value, ulong site = 0)
        { c.Keep(name, value, site); floats.Add(name, BitConverter.SingleToUInt32Bits(value)); return value; }

        internal Result Run()
        {
            Validate();
            // CheckResist caller reads source NPC no-op first, then actual User movement.
            c.Note(0x140bb8de5, "ComputedNoOp", "source NPC slot189", value: 0);
            byte hitMovement = U.Movement(c, D, ClockValue(ClockRole.HitMovement, 0x140bb8df9, 0x140578ec9), "CheckResist.User.slot189");
            if (!CheckHit(hitMovement)) return Finish(false, false, null, null, null, null);
            int chance = unchecked(CurrentWord(0x4b8, 0x140bbd1e2) * S.SignedSourceLevel + CurrentWord(0x4bc, 0x140bbd1e9));
            int chanceSample = Draw("Begin", 0x140bbd1fe, 1, 100);
            int fallback = unchecked((uint)chanceSample) > unchecked((uint)System.Math.Min(chance, 100)) ? 1 : 0;
            Need(chance == 100 && fallback == 0, "incomplete-child", "Begin.chance", 0x140bbd226,
                "Outside the two selected chance100 recipes.", true);
            c.Note(0x140bbd226, "Computed", "Begin fallback separate from force/R7", value: fallback);
            float hit = F("wrapper_hit_mult", Mul(FromInt(unchecked(CurrentWord(0x458, 0x140bb823b) * S.SignedSourceLevel + CurrentWord(0x45c, 0x140bb8246))), Bits(0x3c23d70a)));
            int flat = unchecked(CurrentWord(0x460, 0x140bb8275) * S.SignedSourceLevel + CurrentWord(0x464, 0x140bb82a5));
            int additional = System.Math.Max(0, unchecked(CurrentWord(0x474, 0x140bb82ad) * S.SignedSourceLevel + CurrentWord(0x478, 0x140bb82d3)));
            float probability = Add(Mul(FromInt(S.SignedSourceLevel), Bits(unchecked((uint)CurrentWord(0x44c, 0x140bb82bc)))), Bits(unchecked((uint)CurrentWord(0x450, 0x140bb82c6))));
            // SkillPhyAttack owns these first-record/header stores; they are calculated here,
            // not an input assertion that an external caller initialized them.
            c.W32(Header, 0, 0x140bb8381, "wrapper.header_zero");
            c.Write(Record, new byte[53], 0x140bb8340, "wrapper.first_record_0_to_52_zero");
            byte sharingFlag = c.U8(B.CurrentSkill + 0x4d4, 0x140bb838d, "Skill.sharing_flag4d4");
            ushort sharing = sharingFlag == 0 ? (ushort)1 : c.U16(B.RunInfo + 0x90, 0x140bb8397, "RunInfo.sharing90");
            c.Note(0x140bb8388, "Computed", "wrapper.sharing", value: sharing);
            byte[] condition = c.Read(B.CurrentSkill + 0x480, 48, 0x140bb83eb, "Skill.current_condition480");
            Need(condition.AsSpan().SequenceEqual(S.Condition480.AsSpan()), "stale-skill", "condition480", 0x140bb83eb,
                "Current source condition differs from the selected parser recipe.");
            for (int i = 0; i < 3; i++)
                c.Write(B.CallerFrameRbp + 0x70 + (ulong)i * 16, condition.AsSpan(i * 16, 16),
                    i == 0 ? 0x140bb83f4ul : i == 1 ? 0x140bb8413ul : 0x140bb8429ul, "wrapper.condition_copy16_" + i);
            byte defenderMovement = U.Movement(c, D, ClockValue(ClockRole.CalculatorMovement, 0x140bb8430, 0x140578ec9), "SkillPhyAttack.User.slot189");
            c.Note(0x140bb843f, "ComputedNoOp", "source NPC slot189", value: 0);
            ValidateCalculatorSource();
            float coefficient = F("base_coefficient", Add(Add(c.F32(A + 0x8dc, 0x14036dd36, "Npc.additive8dc"), 1f), hit));
            int flatTotal = unchecked(c.I32(A + 0x14d4, 0x14036dd47, "Npc.flat14d4") + flat);
            int sample = Draw("Base", 0x14036ddc9,
                c.U16(A + 0x14dc, 0x14036d98b, "Npc.minimum14dc"), c.U16(A + 0x14e0, 0x14036d993, "Npc.maximum14e0"));
            float amount = F("base_draw_product", Mul(FromInt(sample), coefficient));
            amount = F("base_with_flat", Add(amount, FromInt(flatTotal)));
            int randomType = CurrentWord(0x46c, 0x140bb84ba), randomMask = CurrentWord(0x470, 0x140bb84ab);
            Need(randomType == 3 && randomMask == 1, "incomplete-child", "random.recipe", 0x14036ddf2,
                "Only the selected complete type3 BasicDMG recipe is admitted.", true);
            int bin = Draw("BaseRandom", 0x14036de03, 0, 99) / 5;
            amount = F("base_randomized", Mul(amount, Div(FromInt(RandomTable(bin)), 100f)));
            if (additional != 0 && Relation(condition))
            {
                int extraSample = Draw("Additional", 0x14036de81, 1, 100);
                if (extraSample < 101)
                {
                    c.W32(B.Cache + 8, 1, 0x14036de98, "cache.additional");
                    float extra = Add(Mul(amount, 0f), FromUInt(unchecked((uint)additional)));
                    amount = F("with_additional", Add(amount, extra));
                }
            }
            Need(c.U64(A + 0x200, 0x14036dedc, "Npc.one_time200") == 0,
                "incomplete-child", "source.one_time200", 0x14036dedc, "Active one-time damage owner.", true);
            Need(c.U64(B.CurrentSkill+0xe10,0x14036dfd3,"Skill.attenuation_functionE10") == 0x140b60ec0,
                "incomplete-child", "Skill.attenuation", 0x14036dfd3, "Reached attenuation function is not the complete proved None leaf.", true);
            int attenuation = Cvtt(amount); // Bound None attenuation140b60ec0.
            c.Note(0x140b60ec0, "ComputedIdentity", "None attenuation input/return", value: attenuation);
            amount = F("movement_attack", Add(Mul(Div(FromInt(Move(0, 0)), 100f), FromInt(attenuation)), FromInt(attenuation)));
            amount = F("preliminary_floor_one", amount < 1f ? 1f : amount);
            int preliminary = Cvtt(amount);
            // User type1 makes36b890 identity: do not manufacture or read actor levels.
            c.Note(0x14036b890, "ComputedIdentity", "actual User target skips level falloff");
            Debug(0x14036e0b7, 0x100);
            uint defense = Defense(c, D);
            int adjusted = Cvtt(Add(Mul(Div(FromInt(Move(defenderMovement, 1)), 100f), FromUInt(defense)), FromUInt(defense)));
            amount = F("after_pdef_and_ratio", Div(Sub(amount, Div(FromInt(adjusted), 10f)), Div(FromUInt(RatioD()), 1000f)));
            amount = F("after_defense_floor_zero", amount < 0f ? 0f : amount);
            amount = NaturalDefense(amount, defenderMovement);
            F("after_active_defense", amount);
            // Natural/no-defense User paths reach the fixed getter after the support branch;
            // the parry branch jumps directly to release. Its complete return is zero.
            if (c.Optional<byte>(Record + 0x30) != 2 && amount > 0f)
                c.Note(0x1400b47a0, "ComputedNoOp", "User fixed physical slot187 before critical", value: 0);
            int criticalSample = Draw("Critical", 0x14036e167, 1, 1000);
            int critical = Cvtt(Mul(c.U16(A + 0x14bc, 0x14036e1bc, "Npc.critical14bc"), probability));
            Need(c.U64(A + 0x208, 0x14036e1e6, "Npc.one_time208") == 0,
                "incomplete-child", "source.one_time208", 0x14036e1e6, "Active one-time critical owner.", true);
            int numerator = unchecked(critical + (Pose() == 5 ? 500 : 0) - Critical(c, D, false));
            int threshold = System.Math.Min(500, Cvtt(Div(FromInt(numerator), Div(FromUInt(RatioD()), 1000f))));
            bool isCritical = criticalSample < threshold;
            c.Note(0x14036e303, "Computed", "critical.threshold", value: threshold);
            int reduction = Critical(c, D, true); // Read even when critical is false.
            float multiplier = 1f;
            if (isCritical)
            {
                int normalized = Cvtt(Div(FromInt(reduction), Div(FromUInt(RatioA()), 1000f)));
                multiplier = Sub(1.5f, Div(FromInt(normalized), 1000f)); if (multiplier <= 1f) multiplier = 1f;
                c.W8(Record + 0x31, 12, 0x14036e472, "record.critical31");
            }
            amount = F("after_critical", Mul(amount, multiplier));
            c.W32(B.Cache + 4, isCritical ? 1u : 0u, 0x14036e481, "cache.critical4");
            if (sharing > 1) amount = Div(amount, FromUInt(sharing));
            F("after_sharing", amount);
            M.Result modifier = Pve();
            amount = F("after_pve", Mul(amount, Bits(modifier.RatioBits)));
            amount = F("final_floor_one", amount < 1f ? 1f : amount);
            Debug(0x14036e960, 0x100);
            int first = Cvtt(amount);
            // Actual User vslot187 is the complete XOR EAX,EAX;RET leaf.
            c.Note(0x1400b47a0, "ComputedNoOp", "User fixed physical slot187", value: 0);
            float finalFloat = F("final_integer_roundtrip", FromInt(first));
            byte exclusive = c.U8(B.CurrentSkill + 0xec4, 0x14036e9b2, "Skill.exclusiveEc4");
            Need(unchecked((sbyte)exclusive) < 1, "incomplete-child", "Skill.exclusive", 0x14036ea74, "Active exclusive attribute owner.", true);
            int sourceId = c.I32(A + 0x100, 0x14036ea07, "Npc.source100");
            ulong selectedClass = ClassLookup(c, sourceId);
            if (selectedClass != 0)
            {
                if (selectedClass != B.SourceTemplate) c.External(selectedClass, 0x451, "lookup-class");
                byte sourceExclusive = c.U8(selectedClass + 0x450, 0x14036ea1b, "lookup_class.exclusive450");
                Need(unchecked((sbyte)sourceExclusive) < 1, "incomplete-child", "lookup_class.exclusive", 0x14036ea74,
                    "Actual selected class enables an unclosed exclusive attribute owner.", true);
            }
            int final = Cvtt(finalFloat);
            c.W32(Record, unchecked((uint)final), 0x14036eae6, "record.damage0");
            return Finish(true, true, fallback, preliminary, final, modifier);
        }

        private bool CheckHit(byte movement)
        {
            int force = System.Math.Max(c.I32(B.RunInfo + 0x58, 0x140bb8da8, "RunInfo.force58"), CurrentWord(0x468, 0x140bb8dbf));
            Need(CurrentWord(0x438, 0x140bb8e0e) == 0 && force == 0, "incomplete-child", "force/no-resist", 0x14036d0f0,
                "Force threshold and source no-resist are separate; active consumers are not implemented.", true);
            uint abnormal = c.U32(A + 0x1250, 0x14036d11e, "Npc.abnormal1250");
            Need((abnormal & 0x20) == 0, "incomplete-child", "source.blind", 0x14036d150, "Blind draw/consumer requires its complete owner.", true);
            int state = State();
            c.External(0x145a2c630ul + (ulong)state * 4, 4, "dodge-row");
            int dodge = c.I32(0x145a2c630ul + (ulong)state * 4, 0x14036d187, "current.dodge_state_row");
            if (dodge == 0) ForceZero(0x43e0, 0x14036d1a2);
            ForceZero(0x43f0, 0x14036d1f0);
            if (ParryRow(state) == 0) ForceZero(0x43e8, 0x14036d268);
            if (Pose() != 5 && (abnormal & 0x649f04c) == 0 && dodge == 0)
            {
                Need(c.U64(A + 0x200, 0x14036d344, "Npc.hit_one_time200") == 0,
                    "incomplete-child", "source.hit_one_time200", 0x14036d344, "Active one-time accuracy owner.", true);
                int accuracy = System.Math.Clamp(unchecked(c.U16(A + 0x14aa, 0x14036d337, "Npc.accuracy14aa") +
                    unchecked(CurrentWord(0x43c, 0x140bb8dc6) * S.SignedSourceLevel + CurrentWord(0x440, 0x140bb8ddb))), 0, 9999);
                c.W32(B.Cache + 12, unchecked((uint)accuracy), 0x14036d501, "cache.accuracy12");
                ushort evasion = Status(c, D, StatusGetter.Dodge).Value;
                c.Note(0x14036b970, "ComputedIdentity", "User evasion level helper", value: evasion);
                int threshold = Chance(evasion, accuracy, Move(movement, 5), 300);
                // Both query getters are actually called before classifier short-circuiting,
                // even when their returned values compare equal. Neither is a chosen callback.
                int targetQuery=c.I32(D+0x738,0x1400d1850,"User.slot128.query738");
                int sourceQuery=c.I32(A+0x1890,0x1400bc890,"Npc.slot128.query1890");
                c.Note(0x14036d5ba,"ComputedGuard","actual source classifier0 excludes PvP bonus",value:sourceQuery==targetQuery?0:1);
                c.Note(0x14036d690, "Computed", "dodge.threshold", value: threshold);
                if (Draw("Dodge", 0x14036d690, 1, 1000) <= threshold)
                {
                    c.W32(B.Cache, 0, 0x14036d6ac, "cache.dodge0");
                    Counter(ClockRole.DodgeCounter, 0x80); return false;
                }
            }
            return true;
        }
        private float NaturalDefense(float damage, byte movement)
        {
            int cacheKind = c.I32(B.Cache, 0x14036eda9, "local_copy.cache_kind0");
            Need(cacheKind == 0, "incomplete-child", "cached_forced_status", 0x14036edac,
                "Forced1/2 and their consumers remain separate reached owners.", true);
            if (Pose() != 5 && (c.U32(A + 0x1250, 0x14036eee4, "Npc.abnormal_for_defense") & 0x649f04c) == 0)
            {
                int accuracy = c.I32(B.Cache + 12, 0x14036eed5, "cache.accuracy12");
                if (ShieldEligible(c, D))
                {
                    ushort block = Status(c, D, StatusGetter.Block).Value;
                    int threshold = Chance(block, accuracy, Move(movement, 4), 500);
                    c.Note(0x14036efd2, "Computed", "block.threshold", value: threshold);
                    if (Draw("Block", 0x14036efd2, 1, 1000) <= threshold)
                    {
                        c.Note(0x1400e1c59, "Guard", "shield source actual type2; User penetration child skipped", value: 2);
                        var shield = Shield(c, D, damage);
                        if (shield.Applied)
                        {
                            c.W8(Record + 0x30, 4, 0x14036f028, "record.block30");
                            OrHeader(0x20, 0x14036f02c); Counter(ClockRole.BlockCounter, 0x20); return shield.Damage;
                        }
                    }
                }
                if (ParryRow(State()) == 0)
                {
                    ushort parry = Status(c, D, StatusGetter.Parry).Value;
                    int threshold = Chance(parry, accuracy, Move(movement, 3), 400);
                    c.Note(0x14036f12d, "Computed", "parry.threshold", value: threshold);
                    if (Draw("Parry", 0x14036f12d, 1, 1000) <= threshold)
                    {
                        c.W8(Record + 0x30, 2, 0x14036f143, "record.parry30");
                        damage = Mul(damage, Bits(0x3f19999a)); OrHeader(0x40, 0x14036f162);
                        Counter(ClockRole.ParryCounter, 0x40); return damage;
                    }
                }
            }
            c.W8(Record + 0x30, 10, 0x14036f178, "record.normal30"); return damage;
        }
        private int Chance(ushort defense, int accuracy, int movement, int maximum) => System.Math.Clamp(
            Cvtt(Div(FromInt(unchecked(defense + unchecked(movement - accuracy))), Div(FromUInt(RatioA()), 1000f))), 0, maximum);
        private int State(ulong site = 0x14036d187)
        {
            int state = c.I32(D + 0x13d0, site, "User.raw_physical_state13d0");
            Need(state is >= 0 and < 32, "range", "User.state13d0", 0, "Supported current table index0..31 required."); return state;
        }
        private int ParryRow(int index)
        {
            c.External(0x141122d00,128,"parry-table");
            byte[] table = c.Peek(0x141122d00, 128);
            c.Note(0x14036d25e,"SourceTableAdmission","complete pinned parry table32");
            for (int i = 0; i < 32; i++) Need(BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(i * 4, 4)) == (i == 1 ? 1 : 0),
                "source-table", "parry", 0, "Parry table differs from the complete pinned PE table.");
            return c.I32(0x141122d00ul+(ulong)index*4,0x14036d25e,"pinned.parry_selected_cell");
        }
        private void ForceZero(int offset, ulong site)
        {
            ulong manager = c.U64(D + 0x158, site, "User.manager158");
            if (manager == 0) return;
            c.External(manager, 0x43f4, "manager");
            Need(c.I32(manager + (ulong)offset, site, "manager.force" + offset.ToString("x")) == 0,
                "incomplete-child", "manager.force", site, "Active force consumption needs its source-bound owner.", true);
        }
        private int Move(int row, int column)
        {
            Need(row is >= 0 and <= 7 && column is >= 0 and < 7, "range", "movement.row", 0, "Actual getter mask7 and seven-column row required.");
            c.External(0x141069ed0ul + (ulong)row * 28,28,"movement-row");
            return c.I32(0x141069ed0ul + (ulong)row * 28 + (ulong)column * 4, 0x14036d572, "loaded.current_movement_row");
        }
        private int RandomTable(int bin)
        {
            c.External(0x1428b7410ul + 3 * 80,80,"random-row");
            byte[] row = c.Peek(0x1428b7410ul+3*80,80);
            c.Note(0x14036de2b,"SourceTableAdmission","complete selected type3 source row");
            for (int i = 0; i < 20; i++) Need(BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(i * 4, 4)) == (i < 7 ? 90 : i < 13 ? 100 : 110),
                "source-table", "random.type3", 0, "Current loaded row must equal selected pinned source type3.");
            return c.I32(0x1428b7410ul+3*80+(ulong)bin*4,0x14036de2b,"loaded.type3_selected_cell");
        }
        private bool Relation(byte[] condition)
        {
            Need((condition[0x2c] & 0xfc) == 0 && condition.AsSpan(0x18, 20).IndexOfAnyExcept((byte)0) < 0,
                "incomplete-child", "relation.masks", 0x140370ca0, "Only the selected direction-only descriptor is admitted.", true);
            int heading = c.I32(D + 0x12c, 0x140370cf1, "User.heading12c");
            Need(heading is >= 0 and < 120, "range", "heading", 0, "Original heading index0..119 required.");
            c.External(0x14111ca60,480,"headingA"); c.External(0x14111cc40,480,"headingB");
            byte[] ta = c.Peek(0x14111ca60,480),tb = c.Peek(0x14111cc40,480);
            c.Note(0x140370d18,"SourceTableAdmission","both complete pinned heading tables");
            Need(Hash(ta) == "ee3f8c41695a4c1c9ec1fe9c52d82facad293638e01317f881c765789e6ea247" &&
                Hash(tb) == "5a95593d012096b1451a7b27d69e1b2e84eef60a7df51170d3e141334fcef1fb",
                "source-table", "heading", 0, "Both actual120-word heading tables must match pinned data.");
            float dx = Sub(c.F32(D + 0x120, 0x140370d00, "User.X"), c.F32(A + 0x120, 0x140370d10, "Npc.X"));
            float dy = Sub(c.F32(D + 0x124, 0x140370cf8, "User.Y"), c.F32(A + 0x124, 0x140370d08, "Npc.Y"));
            float dot = F("relation_dot", Add(Mul(dy, Bits(c.U32(0x14111ca60ul+(ulong)heading*4,0x140370d18,"headingA.selected_word"))),
                Mul(dx, Bits(c.U32(0x14111cc40ul+(ulong)heading*4,0x140370d22,"headingB.selected_word")))));
            if ((condition[0x2c] & 1) != 0 && dot > 0f || (condition[0x2c] & 2) != 0 && dot <= 0f) return true;
            c.External(0x154827468,8,"race-mask");
            ulong race = c.U64(0x154827468, 0x140370e15, "current.race_mask");
            if (race == 0)
            {
                c.External(0x14f523f78,4,"race-limit");
                int count = c.I32(0x14f523f78, 0x140370e2b, "current.race_limit");
                Need(count <= 63, "incomplete-child", "race.limit", 0x140370e40, "Finite lazy loop permits limit<=63 only.", true);
                for (int i = 0; i <= count; i++) race |= 1ul << i;
                if (count >= 0) c.W64(0x154827468, race, 0x140370e50, "race.single_publication");
            }
            // Actual User type1 still calls slot115 even when source class-mask is zero.
            int rawClass = c.I32(D + 0x3a78, 0x1400d2740, "User.slot115.raw_class3a78");
            c.Note(0x140370e88, "ComputedGetter", "actual User slot115", value: rawClass);
            ulong abnormal = c.U64(D + 0x1250, 0x140370e9b, "User.abnormal1250_qword");
            c.Note(0x140370ea2, "Computed", "selected zero class/abnormal masks", value: unchecked((long)abnormal));
            return false;
        }
        private void Counter(ClockRole role, uint mask)
        {
            uint now = ClockValue(role, 0x1400b4640, 0x1400b4650);
            int offset = mask == 0x20 ? 0x13dc : mask == 0x40 ? 0x13e0 : 0x13e4;
            c.W32(D + (ulong)offset, unchecked(now + 5000), 0x1400b469a, "User.counter_deadline");
        }
        private uint ClockValue(ClockRole role, ulong caller, ulong site)
        {
            Need(clockIndex < input.Clocks.Length, "clock", role.ToString(), site, "Missing original per-site clock observation.");
            Clock value = input.Clocks[clockIndex++];
            Need(value.Role == role && value.CallerSite == caller && value.TickSite == site && value.ActorAddress == D && value.Operation == input.Context.Operation &&
                value.Epoch == input.Context.Epoch && value.ActorGeneration == B.UserLife, "clock", role.ToString(), site,
                "Clock role/site/order/actor lifetime or epoch differs.");
            Text(value.Receipt); clocks.Add(value); c.Note(site, "OriginalClock", role.ToString(), value: value.Value); return value.Value;
        }
        private int Draw(string stage, ulong site, int first, int second)
        {
            Need(randomOperations < input.Limits.MaxRandomOperations, "budget", "rng.operations", site, "Cumulative adapter operation budget reached.", true);
            var source = input.Random;
            var ownership = source.Ownership is null ? null : source.Ownership with { InitialGeneration = randomGeneration };
            var transaction = source with
            {
                InitialGeneration = randomGeneration,
                InitialStateBytes = randomState?.Bytes ?? source.InitialStateBytes,
                InitialStateSha256 = randomState?.Sha256 ?? source.InitialStateSha256,
                Ownership = ownership,
                Operations = [new(0, input.Context.Operation + ":" + input.Context.Epoch + ":physical:" + randomOperations.ToString(CultureInfo.InvariantCulture),
                    RetailRandomOperationKind.SignedInclusive, randomGeneration, first, second)],
                Limits = new() { MaxOperations = 1, MaxRawDraws = input.Limits.MaxRawDraws - rawDraws,
                    MaxTraceBytes = input.Limits.MaxRandomLogicalTraceBytes - traceBytes }
            };
            var result = RetailRandomOwnerPlan.Calculate(transaction);
            if (!result.Completed)
            {
                RandomFailure = result;
                throw Error("rng-" + result.Status, stage, site, result.Detail, true);
            }
            var operation = result.CompletedPrefix.Single();
            Need(operation.SignedValue.HasValue && result.CompleteNextGeneration.HasValue,
                "rng", stage, site, "Complete signed operation/state generation missing.");
            rawDraws = checked(rawDraws + result.Draws.Length); traceBytes = checked(traceBytes + result.LogicalTraceBytesCharged);
            randomGeneration = result.CompleteNextGeneration.Value;
            if (result.EngineStateAdmitted) randomState = result.CompleteFinalState;
            randomOperations++;
            int value = operation.SignedValue.Value;
            c.Note(site, "ComputedRng", stage, value: value);
            randomCalls.Add(new(randomOperations, stage, site, first, second, value, result.Draws, randomGeneration)); return value;
        }
        private void Debug(ulong site, uint category)
        {
            _ = c.I32(A + 0x8f4, site, "Npc.raw_weapon_type8f4_format_argument");
            c.Note(site, "InfrastructureBoundary", "source formatter scratch/errno excluded; normal return/no gameplay writer required");
            c.External(0x14276916c,4,"debug-mode");
            uint mask = c.U32(0x14276916c, 0x14053ebd2, "current.debug_mode_mask");
            if ((mask & 1) != 0)
            {
                c.External(0x142769b90,4,"debug-category");
                Need((c.U32(0x142769b90, 0x14053ebdd, "current.debug_category0") & category) == 0,
                    "incomplete-child", "User.Hear", 0x14053ebfb, "Active gameplay-visible debug/Hear child.", true);
            }
        }
        private void OrHeader(uint mask, ulong site) => c.W32(Header, c.U32(Header, site, "header.old") | mask, site, "header.or");
        private Result Finish(bool hit, bool begin, int? fallback, int? preliminary, int? final, M.Result? pve)
        {
            Need(clockIndex == input.Clocks.Length, "clock", "tail", 0, "Unused original clock observations.");
            return new(hit, begin, fallback, preliminary, final, c.Snapshot(), c.Steps, [..clocks], [..randomCalls],
                randomState, randomGeneration, traceBytes, pve, floats.ToImmutableDictionary(StringComparer.Ordinal));
        }

        private void Validate()
        {
            CheckContext(input.Context);
            Need(input.Selected is not null && input.Memory is not null && input.Boundary is not null && input.Random is not null &&
                input.Floating is not null && input.Infrastructure is not null && input.Limits is not null && !input.Clocks.IsDefault,
                "input", "required", 0, "Missing explicit immutable input/context.");
            Limits l = input.Limits;
            Need(l.MaxImages is > 0 and <= 128 && l.MaxBytes is > 0 and <= 8_000_000 && l.MaxSteps is > 0 and <= 100_000 &&
                l.MaxRandomOperations >= 0 && l.MaxRawDraws >= 0 && l.MaxRandomLogicalTraceBytes >= 0 &&
                input.Memory.Images.Length <= l.MaxImages && input.Memory.ByteCount <= l.MaxBytes,
                "budget", "adapter", 0, "Explicit nonnegative capacities and bounded current images required.");
            c = new(input.Context, input.Memory, l.MaxSteps);
            Need(input.Floating.NearestEven == true && input.Floating.MaskedExceptions == true && input.Floating.FlushToZero == false &&
                input.Floating.DenormalsAreZero == false && input.Floating.NormalReturn == true,
                "floating", "environment", 0, "Declared RNE/masked/FTZ-DAZ-off/normal-return contract required; not observed MXCSR.");
            Need(input.Infrastructure.SerializedNoOtherWriter == true && input.Infrastructure.NoInlineGameplayWriter == true &&
                input.Infrastructure.NormalInfrastructureReturn == true && input.Infrastructure.UnconsumedFormattingScratchExcluded == true,
                "infrastructure", "interval", 0, "Explicit serialized/no-inline-gameplay-writer/normal-return/excluded-scratch contract required.");
            Text(B.SourceLife); Text(B.UserLife); Text(B.CallerRecipeReceipt);
            Need(B.SourceActor != B.UserActor && S.Binding.NpcId > 0 && S.EffectTypeIndex == 0,
                "actor", "roles", 0, "Distinct actual source NPC and User roles required.");
            Need(c.U64(A, 0x140bb8dd8, "Npc.vtable") == NpcVtable && c.I32(A + 0x144, 0x14036d301, "Npc.type144") == 2 &&
                c.U64(D, 0x140bb8df3, "User.vtable") == UserVtable && c.I32(D + 0x144, 0x14036d505, "User.type144") == 1,
                "actor", "class", 0, "Actual User type1/vtable required, never an NPC proxy.");
            Need(c.I32(A + 0x100, 0, "Npc.template_id100") == S.Binding.NpcId && c.U32(A + 0x1940, 0, "Npc.kind1940") == 1 &&
                c.U32(A + 0x188c, 0, "Npc.master188c") == 0 && c.U32(A + 0x1894, 0, "Npc.related1894") == 0 &&
                c.U64(A + 0x1850, 0, "Npc.virtual1850") == 0 && c.U64(A + 0x1bf8, 0, "Npc.difficulty1bf8") == 0 &&
                c.U64(A + 0x1848, 0, "Npc.template1848") == B.SourceTemplate,
                "source-domain", "Npc", 0, "Independent current ordinary ownerless/unscaled source fields required.");
            Need(c.BindingAt(A, 0x2000).Generation == B.SourceLife && c.BindingAt(D, 0x8010).Generation == B.UserLife,
                "generation", "actors", 0, "Actual actor storage lifetime differs.");
            Need(c.U64(B.RunInfo, 0x140bb8214, "RunInfo.Skill") == B.CurrentSkill &&
                c.U64(B.RunInfo + 0x48, 0x140bb8232, "RunInfo.source") == A &&
                c.U64(B.RunInfo + 0x50, 0x140bb821e, "RunInfo.target") == D &&
                unchecked((short)c.U16(B.RunInfo + 0x38, 0x140bb8218, "RunInfo.skill_level")) == S.SignedSourceLevel,
                "binding", "RunInfo", 0, "Current caller Skill/actors/signed16 level differ from selected source binding.");
            ulong cacheOwner = c.U64(B.RunInfo + 0x60, 0x140bb8452, "RunInfo.cache_owner60");
            Need(cacheOwner <= ulong.MaxValue - 0x3c && cacheOwner + 0x3c == B.Cache,
                "binding", "cache", 0, "Original current RunInfo+60 then+3c cache producer must match.");
            Need(c.I32(B.CurrentSkill+0x148,0,"adapter.Skill.source_id148") == S.Id &&
                c.I32(B.CurrentSkill+0x2c8,0,"adapter.Skill.effect0_class2c8") == S.EffectTypeIndex,
                "stale-skill", "current source identity", 0, "Current Skill ID/effect0 class differs from the selected private source recipe.");
            _ = c.BindingAt(B.CurrentSkill, 0x1000); _ = c.BindingAt(B.SourceTemplate, 0x500); _ = c.BindingAt(B.RunInfo, 0x92);
            Need(Header <= ulong.MaxValue - 0xd8, "range", "caller.frame", 0, "Wrapped caller frame.");
            _ = c.BindingAt(Header, 0xd8); _ = c.BindingAt(B.Cache, 16);
            Need(c.I32(B.Cache, 0x140bb8e16, "cache.initial_kind") == 0, "incomplete-child", "cache.kind", 0,
                "Initial forced cache1/2 belongs to an unclosed source consumer.", true);
            foreach (var output in new[] { (Header,4), (Header+0x70,48), (Record,56), (B.Cache,16) })
                foreach (var owner in new[] { (A,0x2000), (D,0x8010), (B.CurrentSkill,0x1000), (B.SourceTemplate,0x500), (B.RunInfo,0x92) })
                    Disjoint(output.Item1,output.Item2,owner.Item1,owner.Item2);
            Disjoint(B.Cache,16,Header,4); Disjoint(B.Cache,16,Header+0x70,48); Disjoint(B.Cache,16,Record,56);
            // One shared role ledger also covers every late snapshot/manager/program view.
            foreach (var owner in new[] { (A,0x2000), (D,0x8010), (B.CurrentSkill,0x1000), (B.SourceTemplate,0x500),
                (B.RunInfo,0x92), (Header,4), (Header+0x70,48), (Record,56), (B.Cache,16) }) c.Protect(owner.Item1,owner.Item2);
            foreach (var left in new[] { (A,0x2000), (D,0x8010), (B.CurrentSkill,0x1000), (B.SourceTemplate,0x500), (B.RunInfo,0x92) })
                foreach (var right in new[] { (A,0x2000), (D,0x8010), (B.CurrentSkill,0x1000), (B.SourceTemplate,0x500), (B.RunInfo,0x92) })
                    if (left.Item1 != right.Item1) Disjoint(left.Item1,left.Item2,right.Item1,right.Item2);
            Need(new[] { A,D,B.CurrentSkill,B.SourceTemplate,B.RunInfo }.Distinct().Count() == 5,
                "alias", "core roles", 0, "Actor/Skill/template/RunInfo role addresses must be distinct.");
            foreach (var image in input.Memory.Images)
                Disjoint(image.Binding.Address,image.Bytes.Length,RetailRandomEngine.NativeEngineAddress,5008);
            c.External(0x14275eb74,4,"telemetry");
            Need(c.I32(0x14275eb74, 0x140bb80c0, "current.telemetry_guard") == 0,
                "incomplete-child", "telemetry", 0, "Active instrumentation requires original extra clocks/TLS ownership.", true);
            Need(!input.Random.Operations.IsDefault && input.Random.Operations.IsEmpty && input.Random.Sources == new RetailRandomSourceDeclaration(P.MainSha256, RetailRandomEngine.RngEvidenceSha256) &&
                input.Random.Origin == (input.Context.Origin == Origin.ConstructedFixture ? RetailRandomInputOrigin.ConstructedFixture : RetailRandomInputOrigin.DeclaredCurrentMemory),
                "rng-source", "context", 0, "One declared same-origin shared engine with no caller operations/results required.");
        }
        private void ValidateCalculatorSource()
        {
            Need(c.I32(A + 0x914, 0x140bb84f1, "Npc.current914") == 0 &&
                (c.U32(B.SourceTemplate + 0x434, 0x14036dcf2, "Npc.template434") & 0x200000) == 0 &&
                (c.U32(A + 0x13c, 0x14036d9e4, "Npc.pose13c") & 0x80) == 0,
                "incomplete-child", "source.calculator", 0, "Combined/booster/one-time source programs require their owner.", true);
        }
        private static void Disjoint(ulong a,int al,ulong b,int bl)
        {
            Need(a <= ulong.MaxValue-(ulong)al && b <= ulong.MaxValue-(ulong)bl && (a+(ulong)al<=b || b+(ulong)bl<=a),
                "alias", "ranges", 0, "Output/actor/Skill/template/caller/RNG owned ranges overlap.");
        }
        private static float Bits(uint value) => Normal(BitConverter.UInt32BitsToSingle(value));

        private M.Result Pve()
        {
            var ctx = new M.Context(P.MainSha256, P.PackageContext, input.Context.Operation, input.Context.Epoch,
                input.Context.Origin == Origin.ConstructedFixture ? M.Origin.MathematicalFixture : M.Origin.SourceBoundDeclaration,
                input.Context.Receipt, M.ProofSha256, M.OracleAmendmentSha256, M.ReleaseEvidenceSha256);
            M.Binding Bind(ulong address, int count)
            { Binding current = c.BindingAt(address,count); return new(address,current.Identity,current.Generation,ctx); }
            var npc = new M.Npc(Bind(A,0x2000),Bind(B.SourceTemplate,0x500),S.Binding.NpcId,new()
            {
                Type144=c.Optional<int>(A+0x144),Vtable=c.Optional<ulong>(A),Kind1940=c.Optional<uint>(A+0x1940),
                Master188c=c.Optional<uint>(A+0x188c),Related1894=c.Optional<uint>(A+0x1894),Template1848=c.Optional<ulong>(A+0x1848),
                Virtual1850=c.Optional<ulong>(A+0x1850),Difficulty1bf8=c.Optional<ulong>(A+0x1bf8),
                Current1bc8=c.Optional<int>(A+0x1bc8),Current1bd8=c.Optional<int>(A+0x1bd8),Template23c=c.Optional<int>(B.SourceTemplate+0x23c)
            });
            var user = new M.User(Bind(D,0x8010),new()
            {
                Type144=c.Optional<int>(D+0x144),Vtable=c.Optional<ulong>(D),Primary6e50=c.Optional<ulong>(D+0x6e50),Backup6e38=c.Optional<ulong>(D+0x6e38),
                Current166c=c.Optional<int>(D+0x166c),CurrentCf4=c.Optional<int>(D+0xcf4),Selector738=c.Optional<int>(D+0x738),
                Maximum774=c.Optional<int>(D+0x774),Current778=c.Optional<int>(D+0x778),Program8008=c.Optional<ulong>(D+0x8008)
            });
            M.Snapshot? Snapshot(ulong? pointer)
            {
                if(pointer is null or 0)return null;
                c.External(pointer.Value,0x1cc,"snapshot");
                return new(Bind(pointer.Value,0x1cc),c.Optional<ushort>(pointer.Value),c.Optional<uint>(pointer.Value+0x1c8),c.Optional<int>(pointer.Value+0x1c4));
            }
            M.ProgramImage? program=null;
            if(user.Raw.Program8008 is >0)
            {
                ulong address=user.Raw.Program8008.Value;
                // The pure child speculates only over supplied raw bytes. Before any whole
                // candidate is exposed every actually consumed program field is checked known;
                // unknown unconsumed padding/rows are preserved, never defaulted.
                ushort tag=BinaryPrimitives.ReadUInt16LittleEndian(c.Peek(address+2,2));
                int length=tag==0x12?32:4;
                c.External(address,length,"HP2-program");
                byte[] raw=c.Raw(address,length);
                program=new(Bind(address,length),raw.ToImmutableArray());
            }
            var primary=Snapshot(user.Raw.Primary6e50);
            var backup=primary?.OccupiedWord == 0 ? Snapshot(user.Raw.Backup6e38) : null;
            var child=new M.Input(ctx,npc,user,primary,backup,program,
                c.Optional<int>(0x14275eb74),new(true,true,false,false),true,true);
            var result=M.Calculate(child);
            Need(result.Success,"modifier-"+result.Diagnostic?.Code,"PvE/HP2",0x14036e7c1,
                result.Diagnostic?.Message??"Missing whole PvE child result.",true);
            M.Result complete = result.Value ?? throw Error("modifier", "PvE", 0, "Missing complete child.");
            foreach (var read in complete.Reads)
            {
                if (program is not null && read.Operand.StartsWith("HP2",StringComparison.Ordinal))
                {
                    int offset = read.Operand switch
                    {
                        "HP2+2" => 2, "HP2+0" => 0, "HP2+6" => 6, "HP2+4" => 4,
                        _ when read.Operand.StartsWith("HP2 row",StringComparison.Ordinal) => 10 + (read.Operand[7]-'0')*8,
                        _ => throw Error("modifier-footprint",read.Operand,read.NativeSite,"Unmapped consumed child program field.")
                    };
                    _ = c.Read(program.Current.Address+(ulong)offset,offset==0?1:2,read.NativeSite,read.Operand);
                }
                else if (program is not null && read.Operand.StartsWith("selected HP2 row+",StringComparison.Ordinal))
                {
                    Need(complete.SelectedHp2Row.HasValue,"modifier-footprint",read.Operand,read.NativeSite,"Selected row missing.");
                    int relative=read.Operand[^1]-'0', offset=8+complete.SelectedHp2Row.Value*8+relative;
                    _ = c.Read(program.Current.Address+(ulong)offset,relative==0?1:2,read.NativeSite,read.Operand);
                }
                c.Note(read.NativeSite,"ComputedChildRead",read.Operand,value:read.Value);
            }
            // These values came solely from this cursor's same immutable current actor/program views.
            c.Note(0x14036beb0,"ComputedChild","PvE exact ratio",value:complete.RatioBits);
            return complete;
        }
    }
}
