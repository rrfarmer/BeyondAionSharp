using System.Collections.Immutable;
using Aion.GameServer.Ai.Lifecycle;
using Plan = Aion.GameServer.Ai.Lifecycle.RetailNpcSkillTimeline;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcSkillTimelineTests
{
    private static Plan.Context Context() => new(RetailNpcRegenerationPlan.NpcBinarySha256,
        "synthetic actor1", "analytic instruction fixture, not native execution");
    private static Plan.Skill Skill() => new(100, 1, 300, 500, 100, 10);
    private static Plan.Table Table(params Plan.Entry[] rows) => new("complete raw row fixture", rows.ToImmutableArray());
    private static Plan.Timers Timers() => new(10, 20, 30, 50, 100);
    private static Plan.UseObservation Use() => new(Context(), Table(new Plan.Entry(100, 7, 2), new(200, 8, 3)),
        Skill(), true, true, true, 500, 21, 50, true, 1_000, 0, 3_000,
        new(0, 0, 0), new(3, 4, 0), Timers(), [new(1_100, "after send"), new(1_150, "inside extend")]);
    private static Plan.CancelObservation Cancel(int count = 1, uint templateCount = 2) => new(Context(),
        Table(new Plan.Entry(100, 1300, count)), true, 100, [new(100, templateCount)], Timers(), new(2000, "cancel tick"));
    private static Plan.NormalObservation Normal() => new(Context(),
        new("selected raw entry", 2500, [100, 200, 300, 400], 3, 10),
        1, 2, 500, 1, 9, 1000, 3000, 0.5f, default, new(3, 4, 0), Timers(),
        new(0, 2, 1, "original NPC animation draw"), new(1100, "inside normal extend"));

    [Fact]
    public void NormalAttackPreservesMotionAnimationImpactAndSeparateMovementTiming()
    {
        var result = Plan.Normal(Normal());
        Assert.Equal(1, result.NativeResult);
        Assert.Equal(new[] { Plan.ActionKind.ResetAttackMotion, Plan.ActionKind.SendMotionAction,
            Plan.ActionKind.ConsumeAnimationDraw, Plan.ActionKind.SendNormalAttack,
            Plan.ActionKind.ExtendTimers }, result.Actions.Select(x => x.Kind).ToArray());
        Assert.Equal((ushort)700, result.Actions[3].ImpactDelay);
        Assert.Equal(1, result.Actions[3].Index);
        Assert.Equal(1, result.Actions[3].Reason);
        Assert.Equal(new Plan.Timers(4000, 2250, 4100, 50, 100), result.Timers);
        Assert.Null(result.CastState);
        Assert.Empty(result.Entries);
        Assert.Equal(new[] { "original NPC animation draw", "inside normal extend" }, result.ConsumedEvidence.ToArray());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void ZeroOrOneAnimationUsesSlotZeroWithoutCallingRng(byte count)
    {
        var input = Normal();
        var result = Plan.Normal(input with { Entry = input.Entry with { AnimationCount34 = count },
            OldMotion44 = 0, OriginalDraw = null });
        Assert.Equal(new[] { Plan.ActionKind.ResetAttackMotion, Plan.ActionKind.SendNormalAttack,
            Plan.ActionKind.ExtendTimers }, result.Actions.Select(x => x.Kind).ToArray());
        Assert.Equal((ushort)600, result.Actions[1].ImpactDelay);
        Assert.Single(result.ConsumedEvidence);
    }

    [Fact]
    public void NormalMovementDelayConvertsUnsignedBeforeFloatAndUsesCvttIndefinite()
    {
        var input = Normal();
        var result = Plan.Normal(input with { Entry = input.Entry with { MovementDelay0c = uint.MaxValue },
            MovementDelayScale5f8 = 1, CapturedAttackTick = 0, CasterRecovery5f0 = 0,
            Timers = new(0, 0, 0, 0, 0), OriginalExtendTick = new(0, "zero") });
        // float(uint.MaxValue)=4294967296; signed CVTT overflows to0x80000000.
        Assert.Equal(0x80000000u, result.Timers.Movement72c);
        // Signed intermediate chooses attack0 instead of the high-bit movement.
        Assert.Equal(0u, result.Timers.Attack728);
    }

    [Fact]
    public void NormalEntryAndOriginalAnimationBoundsAreRequired()
    {
        var input = Normal();
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { OriginalDraw = null }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { OriginalDraw = new(1, 3, 1, "wrong lower") }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { OriginalDraw = new(0, 2, 3, "out of range") }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { Entry = input.Entry with { AnimationCount34 = 1 } }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { Entry = input.Entry with { ImpactWords2c = [100] } }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { SelectedNormalIndex = 2 }));
        Assert.Throws<ArgumentException>(() => Plan.Normal(input with { Entry = input.Entry with { AnimationCount34 = 5 } }));
    }

    [Theory]
    [InlineData(1, 0u, true)]
    [InlineData(1, 0x100u, false)]
    [InlineData(1, 0x200u, true)]
    [InlineData(0, 0x100u, true)]
    [InlineData(0, 0x200u, false)]
    [InlineData(2, 0x100000u, false)]
    [InlineData(1, 0x100000u, true)]
    [InlineData(1, 0x4u, false)]
    [InlineData(2, 0x4u, false)]
    [InlineData(-1, 0u, true)]
    public void RawSkillKindsUseDifferentAbnormalMasks(int kind, uint mask, bool expected) =>
        Assert.Equal(expected, Plan.AbnormalAllows(Skill() with { RawKind84 = kind }, mask));

    [Fact]
    public void MissingSkillIsRejectedAndAbsentStateRowIsAllowed()
    {
        Assert.False(Plan.AbnormalAllows(null, 0));
        Assert.False(Plan.ReuseAllows(Context(), Table(), null, 1, 0));
        Assert.True(Plan.CanUse(Context(), Table(), Skill(), 0, 0, true, 0));
        Assert.True(Plan.CountAllows(Context(), Table(), 100, true));
    }

    [Theory]
    [InlineData(1499u, false)]
    [InlineData(1500u, false)]
    [InlineData(1501u, true)]
    public void ReuseEqualityIsStillBlocked(uint now, bool expected)
    {
        var table = Table(new Plan.Entry(100, 1000, 1));
        Assert.Equal(expected, Plan.ReuseAllows(Context(), table, Skill(), now, 0));
        Assert.Equal(expected, Plan.CanUse(Context(), table, Skill(), 0, now, true, 0));
    }

    [Fact]
    public void ReuseUsesWrappedAbsoluteTicksAndExplicitOverride()
    {
        var table = Table(new Plan.Entry(100, uint.MaxValue - 9, 1));
        Assert.True(Plan.ReuseAllows(Context(), table, Skill(), 1000, 20)); // wraps to10
        Assert.False(Plan.ReuseAllows(Context(), table, Skill(), 10, 20));
        Assert.True(Plan.ReuseAllows(Context(), Table(new Plan.Entry(100, 1000, 1)), Skill(), 1101, 100));
    }

    [Fact]
    public void CountChecksUseFirstMatchAndTreatNegativeAsNonzero()
    {
        var table = Table(new Plan.Entry(100, 0, 0), new(100, 0, 2));
        Assert.False(Plan.CountAllows(Context(), table, 100, true));
        Assert.True(Plan.CountAllows(Context(), table, 100, false));
        Assert.False(Plan.CanUse(Context(), table, Skill(), 0, 1000, true, 0));
        Assert.True(Plan.CanUse(Context(), table, Skill(), 0, 1000, false, 0));
        Assert.True(Plan.CountAllows(Context(), Table(new Plan.Entry(100, 0, -1)), 100, true));
    }

    [Fact]
    public void CompleteUseProducesCountClockPacketAndTimerOrder()
    {
        var result = Plan.Use(Use());
        Assert.Equal(2, result.NativeResult);
        Assert.Equal(new Plan.Entry(100, 1300, 1), result.Entries[0]);
        Assert.Equal(new Plan.Entry(200, 8, 3), result.Entries[1]);
        Assert.Equal(new Plan.Timers(4300, 3300, 4400, 4300, 100), result.Timers);
        Assert.Equal(1, result.CastState);
        Assert.Equal(100, result.CastingSkillId);
        Assert.Equal(2750u, result.CastDeadline);
        Assert.Equal(new[] { Plan.ActionKind.WriteSkillEntry, Plan.ActionKind.SendUseSkill,
            Plan.ActionKind.WriteCastState, Plan.ActionKind.ExtendTimers }, result.Actions.Select(x => x.Kind).ToArray());
        Assert.Equal((ushort)650, result.Actions[1].ImpactDelay);
        Assert.Equal((ushort)21, result.Actions[1].Level);
        Assert.Equal(500u, result.Actions[1].TargetId);
        Assert.Equal(new[] { "after send", "inside extend" }, result.ConsumedEvidence.ToArray());
        Assert.False(result.RuntimeVerified);
        Assert.False(result.ActivationReady);
    }

    [Fact]
    public void UseUpdatesAllDuplicateIdsButEligibilityOnlyReadsFirst()
    {
        var input = Use() with { Table = Table(new Plan.Entry(100, 7, 2), new(100, 8, 0), new(100, 9, -1)) };
        var result = Plan.Use(input);
        Assert.Equal(new[] { 1, 0, -2 }, result.Entries.Select(x => x.RemainingCount).ToArray());
        Assert.All(result.Entries, x => Assert.Equal(1300u, x.LastStartTick));
        Assert.Equal(new[] { 0, 1, 2 }, result.Actions.Where(x => x.Kind == Plan.ActionKind.WriteSkillEntry).Select(x => x.Index!.Value).ToArray());
    }

    [Fact]
    public void NonconsumingUseStillWritesReuseTime()
    {
        var result = Plan.Use(Use() with { ConsumeCount = false });
        Assert.Equal(2, result.Entries[0].RemainingCount);
        Assert.Equal(1300u, result.Entries[0].LastStartTick);
    }

    [Fact]
    public void ZeroStartReadsAdditionalOriginalClockAndZeroRecoverySelects2000()
    {
        var input = Use() with { SuppliedStartTick = 0, CasterRecovery5f0 = 0,
            OriginalTickReads = [new(500, "start"), new(550, "after send"), new(600, "inside extend")] };
        var result = Plan.Use(input);
        Assert.Equal(800u, result.Entries[0].LastStartTick);
        Assert.Equal(2800u, result.Timers.Attack728);
        Assert.Equal(2800u, result.Timers.Movement72c);
        Assert.Equal(2200u, result.CastDeadline);
        Assert.Equal(3, result.ConsumedEvidence.Length);
    }

    [Fact]
    public void ExplicitRecoveryAndOldTimerMaximaArePreserved()
    {
        var result = Plan.Use(Use() with { SuppliedRecovery = 4000,
            Timers = new(9000, 8000, 10000, 11000, 100) });
        Assert.Equal(new Plan.Timers(9000, 8000, 10000, 11000, 100), result.Timers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EarlyUseBranchesDoNotInventLateVirtualResultsOrClocks(int branch)
    {
        var input = Use() with { OriginalTickReads = [] };
        input = branch switch
        {
            0 => input with { TargetPresent = false, NativeCasterVirtual22 = null, NativeTargetVirtual22 = null },
            1 => input with { Skill = null, NativeCasterVirtual22 = null, NativeTargetVirtual22 = null },
            2 => input with { NativeCasterVirtual22 = false, NativeTargetVirtual22 = null },
            _ => input with { NativeTargetVirtual22 = false }
        };
        var result = Plan.Use(input);
        Assert.Equal(0, result.NativeResult);
        Assert.Empty(result.Actions);
        Assert.Null(result.CastState);
    }

    [Fact]
    public void MissingReachedVirtualOrTickAndExtraTicksFailVisibly()
    {
        Assert.Throws<ArgumentException>(() => Plan.Use(Use() with { NativeCasterVirtual22 = null }));
        Assert.Throws<ArgumentException>(() => Plan.Use(Use() with { NativeTargetVirtual22 = null }));
        Assert.Throws<ArgumentException>(() => Plan.Use(Use() with { OriginalTickReads = [new(1, "one")] }));
        Assert.Throws<ArgumentException>(() => Plan.Use(Use() with { OriginalTickReads = [new(1, "one"), new(2, "two"), new(3, "extra")] }));
    }

    [Fact]
    public void ImpactDelayPreservesLowWordAndNegativeTravelTruncation()
    {
        var skill = Skill() with { ImpactDelayB4 = 65530, ProjectileSpeedB8 = 0 };
        Assert.Equal((ushort)4, Plan.ImpactDelay(skill, 10, default, default));
        skill = skill with { ImpactDelayB4 = 100, ProjectileSpeedB8 = 3 };
        Assert.Equal((ushort)433, Plan.ImpactDelay(skill, 0, default, new(1, 0, 0)));
        Assert.Equal((ushort)650, Plan.ImpactDelay(Skill(), 50, default, new(3, 4, 0)));
    }

    [Fact]
    public void NonpositiveSpeedDoesNotReadPositionsAndOverflowCvttHasNativeLowWord()
    {
        Assert.Equal((ushort)100, Plan.ImpactDelay(Skill() with { ProjectileSpeedB8 = -1 }, 0,
            new(float.NaN, 0, 0), default));
        Assert.Equal((ushort)100, Plan.ImpactDelay(Skill() with { ProjectileSpeedB8 = float.Epsilon }, 0,
            default, new(1, 0, 0)));
        Assert.Throws<ArgumentException>(() => Plan.ImpactDelay(Skill(), 0, new(float.NaN, 0, 0), default));
        Assert.Throws<ArgumentException>(() => Plan.ImpactDelay(Skill() with { ProjectileSpeedB8 = float.NaN }, 0, default, default));
    }

    [Fact]
    public void ExtendKeepsSignedMiddleComparisonAndUnsignedOuterMaxima()
    {
        // a has its high bit set. Signed middle max chooses m=1, so this is
        // deliberately different from a plain unsigned maximum of all inputs.
        var result = Plan.Extend(new(0, 0, 0, 0, 100), 0x80000000, 1, 0);
        Assert.Equal(1u, result.Attack728);
        Assert.Equal(1u, result.Movement72c);
        Assert.Equal(101u, result.Timeout738);
        Assert.Equal(0u, result.NextUpdate754);
    }

    [Theory]
    [InlineData(0, 2u, 2)]
    [InlineData(1, 2u, 2)]
    [InlineData(5, 2u, 6)]
    [InlineData(-1, 2u, 2)]
    [InlineData(5, 0u, 5)]
    public void CancellationCountRestorationUsesUnsignedMaximum(int count, uint templateCount, int expected)
    {
        var result = Plan.Cancel(Cancel(count, templateCount));
        Assert.Equal(expected, result.Entries[0].RemainingCount);
        Assert.Equal(1300u, result.Entries[0].LastStartTick);
        Assert.Equal(new Plan.Timers(2100, 2100, 2200, 2100, 100), result.Timers);
        Assert.Equal(2, result.CastState);
        Assert.Equal(0, result.CastingSkillId);
        Assert.Null(result.CastDeadline);
        Assert.Equal(Plan.ActionKind.QueueUpdate, result.Actions[^1].Kind);
        Assert.Equal(100u, result.Actions[^1].Tick);
        Assert.Equal(6, result.Actions[^1].Reason);
    }

    [Fact]
    public void CancellationFindsFirstTemplateMatchAndDoesNotChangeUnrelatedRows()
    {
        var input = Cancel() with { Table = Table(new Plan.Entry(100, 10, 0), new(100, 20, 0)),
            OrderedTemplateSkills = [new(100, 2), new(100, 3)] };
        var result = Plan.Cancel(input);
        Assert.Equal(new[] { 2, 0 }, result.Entries.Select(x => x.RemainingCount).ToArray());
    }

    [Fact]
    public void MissingTemplateMatchStillClearsAndRetries()
    {
        var result = Plan.Cancel(Cancel() with { SkillId = 200 });
        Assert.Equal(1, result.Entries[0].RemainingCount);
        Assert.Equal(3, result.Actions.Length);
        Assert.Equal(Plan.ActionKind.ClearCastState, result.Actions[0].Kind);
    }

    [Fact]
    public void TargetlessCancellationOnlyClearsCastState()
    {
        var input = Cancel() with { TargetPresent = false, SkillId = null,
            OrderedTemplateSkills = default, OriginalTick = null };
        var result = Plan.Cancel(input);
        Assert.Equal(Timers(), result.Timers);
        Assert.Single(result.Actions);
        Assert.Equal(Plan.ActionKind.ClearCastState, result.Actions[0].Kind);
        Assert.Empty(result.ConsumedEvidence);
    }

    [Fact]
    public void CancellationReplacesOldTimersAndWrapsRetry()
    {
        var result = Plan.Cancel(Cancel() with { OriginalTick = new(uint.MaxValue - 49, "wrap"),
            Timers = new(9000, 8000, 10000, 11000, 100) });
        Assert.Equal(new Plan.Timers(50, 50, 150, 50, 100), result.Timers);
    }

    [Fact]
    public void SourceIdentityAndCompleteTableAreRequired()
    {
        Assert.Throws<ArgumentException>(() => Plan.CountAllows(Context() with { SourceNpcSha256 = new('a', 64) }, Table(), 1, true));
        Assert.Throws<ArgumentException>(() => Plan.CountAllows(Context(), new("uninitialized", default), 1, true));
        Assert.Throws<ArgumentException>(() => Plan.CountAllows(Context(), Table(Enumerable.Repeat(new Plan.Entry(1, 0, 0), 21).ToArray()), 1, true));
        Assert.Throws<ArgumentException>(() => Plan.Cancel(Cancel() with { OrderedTemplateSkills = [] }));
        Assert.Throws<ArgumentException>(() => Plan.Cancel(Cancel() with { OriginalTick = null }));
    }
}

