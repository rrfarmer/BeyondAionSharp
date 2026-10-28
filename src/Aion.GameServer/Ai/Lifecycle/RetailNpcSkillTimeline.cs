using System.Collections.Immutable;

namespace Aion.GameServer.Ai.Lifecycle;

/// <summary>
/// Inactive, side-effect-free reconstruction of NPC skill eligibility/use/cancel
/// and their clock writes. Java SkillAttackManager/NpcSkillList were read first.
/// Native instruction receipts: implementation/phase3/skill-timeline. Raw ticks
/// and masks deliberately do not use the legacy skill scheduler or player enums.
/// </summary>
internal static class RetailNpcSkillTimeline
{
    internal readonly record struct Point(float X, float Y, float Z);
    internal sealed record Skill(int Id, int RawKind84, uint DelayA0, uint ReuseAc,
        ushort ImpactDelayB4, float ProjectileSpeedB8);
    internal sealed record Entry(int SkillId, uint LastStartTick, int RemainingCount);
    internal sealed record TemplateEntry(int? SkillId, uint Count24);
    internal sealed record Table(string Evidence, ImmutableArray<Entry> Entries);
    internal sealed record ClockRead(uint Tick, string Evidence);
    internal sealed record Context(string SourceNpcSha256, string ActorIdentity, string Evidence);
    internal sealed record Timers(uint Attack728, uint Movement72c, uint Timeout738,
        uint NextUpdate754, uint TemplateTimeout16cc);

    internal enum ActionKind { WriteSkillEntry, SendUseSkill, WriteCastState, ExtendTimers,
        RestoreCancelledCount, ClearCastState, ReplaceTimers, QueueUpdate,
        ResetAttackMotion, SendMotionAction, ConsumeAnimationDraw, SendNormalAttack }
    internal sealed record Action(ActionKind Kind, int? Index = null, Entry? Entry = null,
        int? SkillId = null, uint? TargetId = null, ushort? Level = null, ushort? ImpactDelay = null,
        uint? Tick = null, uint? OtherTick = null, int? Reason = null);
    internal sealed record Result(int? NativeResult, ImmutableArray<Entry> Entries, Timers Timers,
        int? CastState, int? CastingSkillId, uint? CastDeadline,
        ImmutableArray<Action> Actions, ImmutableArray<string> ConsumedEvidence)
    {
        public bool RuntimeVerified => false;
        public bool ActivationReady => false;
    }

    // NPC::IsValidSkillUseAbnormalState, 0x14007e700. Raw kind1 and all
    // other kinds have different masks. No guessed enum-name translation.
    internal static bool AbnormalAllows(Skill? skill, uint mask)
    {
        if (skill is null) return false;
        if (skill.RawKind84 == 1)
            return (mask & 0x100u) == 0 && (mask & 0x0c000108u) == 0 && (mask & 0x0649f04cu) == 0;
        return (mask & 0x200u) == 0 && (mask & 0x100000u) == 0 &&
            (mask & 0x14100008u) == 0 && (mask & 0x0649f04cu) == 0;
    }

    internal static bool CountAllows(Context context, Table table, int skillId, bool checkCount)
    {
        Validate(context, table);
        if (!checkCount) return true;
        var found = table.Entries.FirstOrDefault(x => x.SkillId == skillId);
        return found is null || found.RemainingCount != 0;
    }

    internal static bool ReuseAllows(Context context, Table table, Skill? skill, uint now, uint overrideDelay)
    {
        Validate(context, table);
        if (skill is null) return false;
        var found = table.Entries.FirstOrDefault(x => x.SkillId == skill.Id);
        // Strict unsigned absolute comparison and wrapped addition, not elapsed
        // subtraction. Explicit override0 selects the skill's reuse field.
        return found is null || unchecked(found.LastStartTick +
            (overrideDelay == 0 ? skill.ReuseAc : overrideDelay)) < now;
    }

    internal static bool CanUse(Context context, Table table, Skill? skill, uint abnormalMask,
        uint now, bool checkCount, uint overrideDelay)
    {
        Validate(context, table);
        if (!AbnormalAllows(skill, abnormalMask)) return false;
        var found = table.Entries.FirstOrDefault(x => x.SkillId == skill!.Id);
        return found is null || ((!checkCount || found.RemainingCount != 0) &&
            unchecked(found.LastStartTick + (overrideDelay == 0 ? skill!.ReuseAc : overrideDelay)) < now);
    }

    internal sealed record UseObservation(Context Context, Table Table, Skill? Skill,
        bool TargetPresent, bool? NativeCasterVirtual22, bool? NativeTargetVirtual22,
        uint TargetId, ushort Level, ushort AdditionalImpactDelay, bool ConsumeCount,
        uint SuppliedStartTick, uint SuppliedRecovery, uint CasterRecovery5f0,
        Point CasterPosition, Point TargetPosition, Timers Timers,
        ImmutableArray<ClockRead> OriginalTickReads);

    /// <summary>
    /// Replays the complete admitted numeric use path, including all matching
    /// count rows. Observations at the original tick calls must be supplied in
    /// order. It neither sends the request nor schedules/applies the skill.
    /// Virtual22 observations are required only on reached native branches.
    /// </summary>
    internal static Result Use(UseObservation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input.Context, input.Table);
        var rows = input.Table.Entries.ToBuilder();
        var actions = ImmutableArray.CreateBuilder<Action>();
        var evidence = ImmutableArray.CreateBuilder<string>();
        int clockIndex = 0;
        uint ReadTick()
        {
            if (input.OriginalTickReads.IsDefault || clockIndex >= input.OriginalTickReads.Length)
                throw new ArgumentException("Missing original tick observation.", nameof(input));
            var read = input.OriginalTickReads[clockIndex++];
            RequireEvidence(read.Evidence);
            evidence.Add(read.Evidence);
            return read.Tick;
        }
        Result Rejected() => new(0, rows.ToImmutable(), input.Timers, null, null, null, [], []);
        if (!input.TargetPresent || input.Skill is null) return Rejected();
        if (!(input.NativeCasterVirtual22 ?? throw new ArgumentException("Missing caster virtual22 observation.")))
            return Rejected();
        if (!(input.NativeTargetVirtual22 ?? throw new ArgumentException("Missing target virtual22 observation.")))
            return Rejected();
        var skill = input.Skill;
        uint start = input.SuppliedStartTick == 0 ? ReadTick() : input.SuppliedStartTick;
        uint recovery = input.SuppliedRecovery == 0 ? 2_000u : input.SuppliedRecovery;
        uint nextStart = unchecked(start + skill.DelayA0);
        ushort impact = ImpactDelay(skill, input.AdditionalImpactDelay, input.CasterPosition, input.TargetPosition);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.SkillId != skill.Id) continue;
            int count = input.ConsumeCount && row.RemainingCount != 0 ? unchecked(row.RemainingCount - 1) : row.RemainingCount;
            rows[i] = row with { LastStartTick = nextStart, RemainingCount = count };
            actions.Add(new(ActionKind.WriteSkillEntry, Index: i, Entry: rows[i]));
        }
        actions.Add(new(ActionKind.SendUseSkill, SkillId: skill.Id, TargetId: input.TargetId,
            Level: input.Level, ImpactDelay: impact));
        uint deadline = unchecked(ReadTick() + 1_000u + impact);
        actions.Add(new(ActionKind.WriteCastState, SkillId: skill.Id, Tick: deadline, Reason: 1));
        uint attack = unchecked(nextStart + Math.Max(input.CasterRecovery5f0, recovery));
        uint movement = unchecked(nextStart + recovery);
        uint extendNow = ReadTick();
        var timers = Extend(input.Timers, attack, movement, extendNow);
        timers = timers with { NextUpdate754 = Math.Max(attack, timers.NextUpdate754) };
        actions.Add(new(ActionKind.ExtendTimers, Tick: attack, OtherTick: movement));
        if (clockIndex != input.OriginalTickReads.Length)
            throw new ArgumentException("Extra tick observations differ from the original call sequence.", nameof(input));
        return new(2, rows.ToImmutable(), timers, 1, skill.Id, deadline, actions.ToImmutable(), evidence.ToImmutable());
    }

    internal static ushort ImpactDelay(Skill skill, ushort additional, Point caster, Point target)
        => ImpactDelayCore(skill.ImpactDelayB4, additional, skill.ProjectileSpeedB8, caster, target);

    private static ushort ImpactDelayCore(ushort baseDelay, ushort additional, float speed, Point caster, Point target)
    {
        if (!float.IsFinite(speed))
            throw new ArgumentException("Non-finite projectile speed is outside the admitted domain.", nameof(speed));
        ushort result = unchecked((ushort)(baseDelay + additional));
        if (speed <= 0) return result;
        Finite(caster); Finite(target);
        float dx = target.X - caster.X, dy = target.Y - caster.Y, dz = target.Z - caster.Z;
        // Native y*y + x*x, then z*z: preserve separate binary32 operations.
        float yy = dy * dy, xx = dx * dx, zz = dz * dz;
        float sum = yy + xx;
        sum += zz;
        if (!float.IsFinite(sum)) throw new ArgumentException("Distance overflow is outside the admitted domain.");
        float distance = MathF.Sqrt(sum); // native sqrtf's finite nonnegative SQRTSS branch
        float travel = distance / speed;
        travel *= -1_000f;
        // CVTTSS2SI produces the indefinite signed value on overflow/NaN.
        int truncated = Cvtt(travel);
        return unchecked((ushort)(result - truncated));
    }

    internal sealed record NormalEntry(string Evidence, uint MovementDelay0c,
        ImmutableArray<ushort> ImpactWords2c, byte AnimationCount34, float ProjectileSpeed38);
    internal sealed record AnimationDraw(int InclusiveLower, int InclusiveUpper, int Value, string Evidence);
    internal sealed record NormalObservation(Context Context, NormalEntry Entry,
        int SelectedNormalIndex, int NormalEntryCount, uint TargetId, int OldMotion44, int MotionAction48,
        uint CapturedAttackTick, uint CasterRecovery5f0, float MovementDelayScale5f8,
        Point CasterPosition, Point TargetPosition, Timers Timers, AnimationDraw? OriginalDraw,
        ClockRead OriginalExtendTick);

    /// <summary>
    /// Complete admitted normal branch of 0x1401b2a30. The selected entry must
    /// already be a normal attack. Skill dispatch remains the separate Use path.
    /// Animation RNG is an original NPC draw receipt, never a new live draw or a
    /// MainServer damage-roll receipt. The request is internal NPC-to-Main, not
    /// a 4.8 client packet, and no packet is emitted here.
    /// </summary>
    internal static Result Normal(NormalObservation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateSource(input.Context);
        ArgumentNullException.ThrowIfNull(input.Entry);
        RequireEvidence(input.Entry.Evidence);
        if (input.NormalEntryCount is < 1 or > 23 || input.SelectedNormalIndex < 0 ||
            input.SelectedNormalIndex >= input.NormalEntryCount || input.Entry.ImpactWords2c.IsDefault ||
            input.Entry.ImpactWords2c.Length != 4 || input.Entry.AnimationCount34 > 4 ||
            !float.IsFinite(input.MovementDelayScale5f8))
            throw new ArgumentException("Complete bounded selected normal-entry inputs are required.", nameof(input));
        var actions = ImmutableArray.CreateBuilder<Action>();
        var consumed = ImmutableArray.CreateBuilder<string>();
        actions.Add(new(ActionKind.ResetAttackMotion)); // exchange +3c/44 to0, store +30=0
        if (input.OldMotion44 != 0)
            actions.Add(new(ActionKind.SendMotionAction, Reason: input.MotionAction48));
        int animation = 0;
        if (input.Entry.AnimationCount34 > 1)
        {
            var draw = input.OriginalDraw ?? throw new ArgumentException("Missing original animation draw.", nameof(input));
            RequireEvidence(draw.Evidence);
            if (draw.InclusiveLower != 0 || draw.InclusiveUpper != input.Entry.AnimationCount34 - 1 ||
                draw.Value < 0 || draw.Value > draw.InclusiveUpper)
                throw new ArgumentException("Animation receipt differs from the original wrapper bounds.", nameof(input));
            animation = draw.Value;
            consumed.Add(draw.Evidence);
            actions.Add(new(ActionKind.ConsumeAnimationDraw, Index: animation, OtherTick: (uint)draw.InclusiveUpper));
        }
        else if (input.OriginalDraw is not null)
            throw new ArgumentException("The zero/single-animation branch does not call RNG.", nameof(input));
        ushort impact = ImpactDelayCore(input.Entry.ImpactWords2c[animation], 0,
            input.Entry.ProjectileSpeed38, input.CasterPosition, input.TargetPosition);
        actions.Add(new(ActionKind.SendNormalAttack, Index: input.SelectedNormalIndex,
            TargetId: input.TargetId, ImpactDelay: impact, Reason: animation));
        uint attack = unchecked(input.CapturedAttackTick + input.CasterRecovery5f0);
        // MOV ECX zero extends the u32 field before CVTSI2SS RCX. It is not
        // a signed input, nor a double-precision multiply or nearest rounding.
        float movement = input.Entry.MovementDelay0c;
        movement *= input.MovementDelayScale5f8;
        uint movementTick = unchecked(input.CapturedAttackTick + (uint)Cvtt(movement));
        RequireEvidence(input.OriginalExtendTick.Evidence);
        consumed.Add(input.OriginalExtendTick.Evidence);
        var timers = Extend(input.Timers, attack, movementTick, input.OriginalExtendTick.Tick);
        actions.Add(new(ActionKind.ExtendTimers, Tick: attack, OtherTick: movementTick));
        return new(1, [], timers, null, null, null, actions.ToImmutable(), consumed.ToImmutable());
    }

    private static int Cvtt(float value) => value >= int.MinValue && value < 2_147_483_648f ? (int)value : int.MinValue;

    // 0x140102820 uses unsigned maxima with now, then a SIGNED comparison
    // between the two bounded candidates, then unsigned maxima with old state.
    internal static Timers Extend(Timers state, uint attack, uint movement, uint now)
    {
        uint a = Math.Max(attack, now), m = Math.Max(movement, now);
        uint signedMax = unchecked((int)a) > unchecked((int)m) ? a : m;
        return state with
        {
            Movement72c = Math.Max(state.Movement72c, m),
            Attack728 = Math.Max(state.Attack728, signedMax),
            Timeout738 = Math.Max(state.Timeout738, unchecked(signedMax + state.TemplateTimeout16cc))
        };
    }

    internal sealed record CancelObservation(Context Context, Table Table, bool TargetPresent,
        int? SkillId, ImmutableArray<TemplateEntry> OrderedTemplateSkills, Timers Timers, ClockRead? OriginalTick);

    internal static Result Cancel(CancelObservation input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input.Context, input.Table);
        var rows = input.Table.Entries.ToBuilder();
        var actions = ImmutableArray.CreateBuilder<Action>();
        var timers = input.Timers;
        var consumed = ImmutableArray.CreateBuilder<string>();
        if (input.TargetPresent)
        {
            if (input.SkillId is null || input.OrderedTemplateSkills.IsDefault ||
                input.OrderedTemplateSkills.Length != rows.Count)
                throw new ArgumentException("Cancellation requires the actual ordered template skill list.", nameof(input));
            for (int i = 0; i < input.OrderedTemplateSkills.Length; i++)
            {
                var template = input.OrderedTemplateSkills[i];
                if (template.SkillId != input.SkillId) continue;
                if (template.Count24 != 0)
                {
                    uint incremented = unchecked((uint)(rows[i].RemainingCount + 1));
                    // CMOVA chooses the larger count: this is not a cap/min.
                    rows[i] = rows[i] with { RemainingCount = unchecked((int)Math.Max(incremented, template.Count24)) };
                    actions.Add(new(ActionKind.RestoreCancelledCount, Index: i, Entry: rows[i]));
                }
                break;
            }
        }
        actions.Add(new(ActionKind.ClearCastState, SkillId: 0, Reason: 2));
        if (input.TargetPresent)
        {
            var tick = input.OriginalTick ?? throw new ArgumentException("Missing original cancellation tick.", nameof(input));
            RequireEvidence(tick.Evidence); consumed.Add(tick.Evidence);
            uint retry = unchecked(tick.Tick + 100u);
            // 0x140102790 replaces these fields. Unlike Extend, old maxima do
            // not survive. Equal arguments make its signed comparison moot.
            timers = timers with { NextUpdate754 = retry, Attack728 = retry, Movement72c = retry,
                Timeout738 = unchecked(retry + timers.TemplateTimeout16cc) };
            actions.Add(new(ActionKind.ReplaceTimers, Tick: retry, OtherTick: retry));
            actions.Add(new(ActionKind.QueueUpdate, Tick: 100, Reason: 6));
        }
        else if (input.OriginalTick is not null)
            throw new ArgumentException("Targetless cancellation does not read the clock.", nameof(input));
        // Clear changes state and skill ID only; +8d0 deadline is not written.
        return new(null, rows.ToImmutable(), timers, 2, 0, null, actions.ToImmutable(), consumed.ToImmutable());
    }

    private static void Validate(Context context, Table table)
    {
        ValidateSource(context); ArgumentNullException.ThrowIfNull(table);
        RequireEvidence(table.Evidence);
        if (table.Entries.IsDefault || table.Entries.Length > 20 || table.Entries.Any(x => x is null))
            throw new ArgumentException("Complete bounded native skill rows are required.", nameof(table));
    }

    private static void ValidateSource(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(context.SourceNpcSha256, RetailNpcRegenerationPlan.NpcBinarySha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Unverified NPC binary identity.", nameof(context));
        RequireEvidence(context.ActorIdentity); RequireEvidence(context.Evidence);
    }

    private static void RequireEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing original observation identity.");
    }
    private static void Finite(Point point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            throw new ArgumentException("Finite actor coordinates required.");
    }
}
