using Aion.GameServer.Controllers.Attack.RetailNpc;
using T = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcForcedDefenseTransition;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcForcedDefenseTransition.Family;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcForcedDefenseTransition.Presence;
using R = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcForcedDefenseTransition.Receiver;
using S = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcForcedDefenseTransition.Stage;

namespace Aion.GameServer.Tests;

/// <summary>
/// Chosen static/instruction-derived states, not captured actors or native-oracle results.
/// External phase3/forced-defense-fixtures pins branch/count/deadline expectations.
/// No effect callback, getter, RNG, clock, actor or HP owner is executed.
/// </summary>
public sealed class RetailNpcForcedDefenseTransitionTests
{
    private const string E = "chosen-static-fixture:not-original-runtime-or-native-oracle";
    private static T.ActorSnapshot Actor(string id, P manager = P.Present, bool attacker = false) => new(manager,
        manager == P.Present ? "manager-" + id : null, 0, 0,
        Enum.GetValues<F>().Select(f => new T.FamilyState(f, manager == P.Present ? 0 : null,
            manager == P.Present ? 2 : null, new(P.VerifiedAbsent, null, 0, E))).ToArray(), id, E,
        attacker ? 11u : 22u, attacker ? "static-attacker-life" : "static-defender-life");
    private static T.ActorSnapshot Family(T.ActorSnapshot actor, F family, int? compare, int? uses,
        P? binding = null, string? key = null, int? identity = 42) => actor with
    {
        Families = actor.Families.Select(f => f.Family != family ? f : f with
        {
            Comparison = compare, Uses = uses,
            Binding = binding is P p ? new(p, p == P.Present ? key ?? "static-skill" : null, identity, E) : f.Binding
        }).ToArray()
    };
    private static T.FamilyState Pair(T.ActorSnapshot actor, F family) => actor.Families.Single(f => f.Family == family);
    private static T.EntrySnapshot Entry(int direction = 0, bool physical = true, int a = 0)
    {
        var attacker = direction == 2 ? RetailNpcNormalActorKind.Player : RetailNpcNormalActorKind.OrdinaryOwnerlessNpc;
        var defender = direction == 1 ? RetailNpcNormalActorKind.Player : RetailNpcNormalActorKind.OrdinaryOwnerlessNpc;
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256, attacker, defender,
            attacker == RetailNpcNormalActorKind.Player ? 0 : 212776, defender == RetailNpcNormalActorKind.Player ? 0 : 212765);
        return new(scope, new(RetailNpcNormalScope.MainBinarySha256, T.DatasetSha256, T.RegionContext,
                "static-action", 11, 22, "static-attacker-life", "static-defender-life", E),
            Family(Actor("entry-attacker", attacker: true), physical ? F.AlwaysHit : F.AlwaysNoResist, a, 2),
            Actor("entry-defender"), physical ? 1u : 0u, 1u, true, P.VerifiedAbsent, E);
    }
    private static T.ActorSnapshot Current(T.Transition s) => s.PendingReceiver == R.Attacker ? s.Attacker : s.Defender;
    private static T.Stamp Stamp(T.Transition s) => new(s.Source, s.PendingReceiver,
        s.PendingReceiver == R.Attacker ? s.Source.AttackerGeneration : s.Source.DefenderGeneration,
        Current(s).SnapshotId, s.NextOrdinal, E);
    // Supplies explicit raw fixture values; expected status/counts never come from this helper.
    private static T.OriginalRead Read(T.Transition s, int tableEntry = 0)
    {
        var actor = Current(s); var stamp = Stamp(s);
        var family = s.PendingFamily is F f ? Pair(actor, f) : null;
        return s.PendingStage switch
        {
            S.BlindRead => new T.BlindRead(stamp, actor.AbnormalFlags1250!.Value),
            S.DodgeTableRead => new T.TableRead(stamp, T.DodgeTableAddress, actor.NativeTableIndex13d0!.Value, tableEntry),
            S.ParryTableRead => new T.TableRead(stamp, T.ParryTableAddress, actor.NativeTableIndex13d0!.Value, tableEntry),
            S.DodgeComparisonRead or S.BlockComparisonRead or S.ParryComparisonRead or S.ResistComparisonRead =>
                new T.ComparisonRead(stamp, family!.Family, actor.ManagerPresence, actor.ManagerIdentity, family.Comparison),
            S.ConsumeRead => new T.ConsumeRead(stamp, family!.Family, actor.ManagerPresence, actor.ManagerIdentity,
                family.Comparison, actor.ManagerPresence == P.Present && family.Comparison != 0 ? family.Uses : null),
            S.BoundSkillRead => new T.BoundSkillRead(stamp, family!.Family, family.Binding.Presence,
                family.Binding.SkillKey, family.Binding.Presence == P.Present ? family.Binding.EffectIdentity : null),
            _ => throw new InvalidOperationException("The fixture requires a different original boundary.")
        };
    }
    private static T.Transition ReadOne(T.Transition s, int tableEntry = 0) => T.Advance(s, Read(s, tableEntry));
    private static T.Transition UntilBoundary(T.Transition s, int dodgeTable = 0, int parryTable = 0)
    {
        for (int i = 0; s.Status == T.Progress.NeedsOriginalRead; i++)
        {
            Assert.True(i < 40, "A finite forced precheck must reach a boundary.");
            s = ReadOne(s, s.PendingStage == S.DodgeTableRead ? dodgeTable : parryTable);
        }
        return s;
    }
    private static T.OriginalTick Tick(T.Transition s, uint raw = 0) => new(Stamp(s), T.CounterHelperAddress,
        s.ChosenCode switch { 0 => 128u, 1 => 64u, 2 => 32u, 3 => 256u, _ => throw new InvalidOperationException() }, raw);
    private static T.Result Done(T.Transition s, uint tick = 0)
    {
        s = UntilBoundary(s);
        if (s.Status == T.Progress.NeedsOriginalTick) s = T.Advance(s, tick: Tick(s, tick));
        return T.Finish(s);
    }
    private static T.Transition Await(F family)
    {
        bool opposing = family is F.AlwaysHit or F.AlwaysNoResist;
        var e = Entry(opposing ? 2 : 0, family is not (F.AlwaysNoResist or F.AlwaysResist), opposing ? 1 : 0);
        e = opposing ? e with { Attacker = Family(e.Attacker, family, 1, 1, P.Present) }
            : e with { Defender = Family(e.Defender, family, 1, 1, P.Present) };
        var s = UntilBoundary(T.Begin(e));
        Assert.Equal(T.Progress.NeedsOriginalRemoval, s.Status);
        return s;
    }
    private static T.OriginalRemoval Removal(T.Transition s, int result = 1,
        T.ActorSnapshot? attacker = null, T.ActorSnapshot? defender = null,
        P dispatch = P.VerifiedAbsent, params T.CallbackObservation[] callbacks) => new(Stamp(s), s.Removal!, result,
            dispatch, callbacks, (attacker ?? s.Attacker) with { SnapshotId = s.Attacker.SnapshotId + ".after" },
            (defender ?? s.Defender) with { SnapshotId = s.Defender.SnapshotId + ".after" }, true, E);

    [Theory]
    [InlineData(0, 5, 5, false, false)]
    [InlineData(3, 0, 0, false, false)]
    [InlineData(3, 1, 0, true, true)]
    [InlineData(3, 2, 1, true, false)]
    [InlineData(-3, -1, -2, true, true)]
    [InlineData(1, int.MinValue, int.MaxValue, true, false)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue - 1, true, false)]
    [InlineData(1, 1, 0, true, true)]
    [InlineData(int.MinValue, int.MinValue, int.MaxValue, true, false)]
    public void CounterArithmeticPreservesSignedWrappedBoundaries(int comparison, int before, int after, bool changed, bool expired)
    {
        var c = T.ConsumeManagerPair(P.Present, comparison, before);
        Assert.Equal(before, c.UsesBefore); Assert.Equal(after, c.UsesAfter);
        Assert.Equal(changed, c.Decremented); Assert.Equal(expired, c.Expired);
    }
    [Fact]
    public void KnownAbsentManagerDoesNotInventAZeroCount()
    {
        var c = T.ConsumeManagerPair(P.VerifiedAbsent, null, null);
        Assert.Null(c.UsesBefore); Assert.Null(c.UsesAfter); Assert.False(c.Decremented); Assert.False(c.Expired);
        Assert.Throws<ArgumentException>(() => T.ConsumeManagerPair(P.Unknown, null, null));
        Assert.Throws<ArgumentException>(() => T.ConsumeManagerPair(P.Present, null, 1));
        Assert.Throws<ArgumentException>(() => T.ConsumeManagerPair(P.Present, 1, null));
        Assert.Throws<ArgumentException>(() => T.ConsumeManagerPair(P.VerifiedAbsent, 0, 0));
        Assert.Throws<ArgumentException>(() => T.ConsumeManagerPair((P)99, 1, 1));
    }

    public static IEnumerable<object[]> CompletePaths()
    {
        foreach (int direction in new[] { 0, 1, 2 })
        {
            yield return new object[] { direction, (int)F.AlwaysDodge, true, 0, 128u, 0x13e4 };
            yield return new object[] { direction, (int)F.AlwaysParry, true, 1, 64u, 0x13e0 };
            yield return new object[] { direction, (int)F.AlwaysBlock, true, 2, 32u, 0x13dc };
            yield return new object[] { direction, (int)F.AlwaysResist, false, 3, 256u, 0x13e8 };
        }
    }
    [Theory]
    [MemberData(nameof(CompletePaths))]
    public void EveryChosenFamilyCompletesInEachAdmittedActorDirection(int direction, int familyCode, bool physical, int code, uint mask, int offset)
    {
        var family = (F)familyCode;
        var e = Entry(direction, physical); e = e with { Defender = Family(e.Defender, family, 1, 2) };
        var r = Done(T.Begin(e));
        Assert.Equal(code, r.PrecheckCode); Assert.Equal(0, r.LatchedComparison);
        Assert.Equal(1, Pair(r.Defender, family).Uses);
        var clock = Assert.Single(r.CounterWrites);
        Assert.Equal(mask, clock.Mask); Assert.Equal(offset, clock.Offset); Assert.Equal(5000u, clock.Deadline);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Kind == "OriginalRemovalReturn");
        Assert.Null(r.Damage); Assert.Null(r.FinalHpAfterCallbacks);
        Assert.Equal(code is 0 or 3, r.TerminalBeforeBase);
    }
    [Theory]
    [InlineData(0, true)] [InlineData(0, false)]
    [InlineData(1, true)] [InlineData(1, false)]
    [InlineData(2, true)] [InlineData(2, false)]
    public void NoForcedFamilyConsumesNoCountsAndRequestsNoClock(int direction, bool physical)
    {
        var r = Done(T.Begin(Entry(direction, physical)));
        Assert.Equal(4, r.PrecheckCode); Assert.Empty(r.CounterWrites);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Kind is "ConsumeRead" or "OriginalRemovalReturn");
        Assert.All(r.Defender.Families, x => Assert.Equal(2, x.Uses));
    }
    public static IEnumerable<object[]> ClockVectors()
    {
        foreach (var row in new[] { (F.AlwaysDodge, true, 0, 128u, 0x13e4), (F.AlwaysParry, true, 1, 64u, 0x13e0),
            (F.AlwaysBlock, true, 2, 32u, 0x13dc), (F.AlwaysResist, false, 3, 256u, 0x13e8) })
            foreach (var tick in new[] { (0u, 5000u), (4294962295u, uint.MaxValue), (4294962296u, 0u), (uint.MaxValue, 4999u) })
                yield return new object[] { (int)row.Item1, row.Item2, row.Item3, row.Item4, row.Item5, tick.Item1, tick.Item2 };
    }
    [Theory]
    [MemberData(nameof(ClockVectors))]
    public void SuppliedOriginalTickUsesExactSlotAndUint32Overflow(int familyCode, bool physical, int code, uint mask, int offset, uint tick, uint deadline)
    {
        var family = (F)familyCode;
        var e = Entry(0, physical); e = e with { Defender = Family(e.Defender, family, 1, 2) };
        var r = Done(T.Begin(e), tick); var write = Assert.Single(r.CounterWrites);
        Assert.Equal(code, r.PrecheckCode); Assert.Equal(mask, write.Mask); Assert.Equal(offset, write.Offset);
        Assert.Equal(tick, write.OriginalTick); Assert.Equal(deadline, write.Deadline);
    }
    [Theory]
    [InlineData((int)F.AlwaysHit, 27, 4)] [InlineData((int)F.AlwaysDodge, 13, 0)]
    [InlineData((int)F.AlwaysParry, 14, 1)] [InlineData((int)F.AlwaysBlock, 15, 2)]
    [InlineData((int)F.AlwaysNoResist, 24, 4)] [InlineData((int)F.AlwaysResist, 25, 3)]
    public void AllSixRemovalChannelsPreserveExactReasonAndClearPointerAfterReturnZero(int familyCode, int reason, int code)
    {
        var family = (F)familyCode;
        var s = Await(family); var request = s.Removal!;
        Assert.Equal(reason, request.Reason); Assert.Equal(1, request.Param4); Assert.Equal(42, request.EffectIdentity);
        Assert.Equal("static-skill", request.SkillKey); Assert.Equal(family, request.Family);
        var next = T.ResumeRemoval(s, Removal(s, result: 0));
        var actor = request.Receiver == R.Attacker ? next.Attacker : next.Defender;
        var pair = Pair(actor, family);
        Assert.Equal(P.VerifiedAbsent, pair.Binding.Presence); Assert.Null(pair.Binding.SkillKey);
        Assert.Equal(42, pair.Binding.EffectIdentity); Assert.Equal(1, pair.Comparison); Assert.Equal(0, pair.Uses);
        Assert.Equal(code, Done(next).PrecheckCode);
    }
    [Fact]
    public void PlayerOpposingRemovalCannotReplaceTheLatchedComparison()
    {
        var e = Entry(2, true, 5);
        e = e with { Attacker = Family(e.Attacker, F.AlwaysHit, 5, 1, P.Present) };
        foreach (var f in new[] { F.AlwaysDodge, F.AlwaysBlock, F.AlwaysParry })
            e = e with { Defender = Family(e.Defender, f, 4, 2) };
        var s = UntilBoundary(T.Begin(e)); Assert.Equal(R.Attacker, s.Removal!.Receiver);
        var after = Family(s.Attacker, F.AlwaysHit, 0, 0, P.VerifiedAbsent);
        var next = T.ResumeRemoval(s, Removal(s, attacker: after, dispatch: P.Present,
            callbacks: new[] { new T.CallbackObservation(0, "EndAlwaysHit", E) }));
        Assert.Equal(S.BlindRead, next.PendingStage); Assert.Equal(5, next.LatchedComparison);
        var r = Done(next);
        Assert.Equal(4, r.PrecheckCode); Assert.All(new[] { F.AlwaysDodge, F.AlwaysBlock, F.AlwaysParry },
            f => Assert.Equal(1, Pair(r.Defender, f).Uses));
        Assert.Equal(0, Pair(r.Attacker, F.AlwaysHit).Comparison);
    }
    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(0, false)] [InlineData(1, false)]
    public void NpcCallerRetainsOpposingUseWhileDefenderFailuresStillInvokeConsume(int direction, bool physical)
    {
        var e = Entry(direction, physical, 5); var own = physical ? F.AlwaysHit : F.AlwaysNoResist;
        e = e with { Attacker = Family(e.Attacker, own, 5, 1, P.Present) };
        var r = Done(T.Begin(e));
        Assert.Equal(4, r.PrecheckCode); Assert.Equal(1, Pair(r.Attacker, own).Uses);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Receiver == R.Attacker && x.Kind == "ConsumeRead");
        Assert.Equal(physical ? 3 : 1, r.Operations.Count(x => x.Kind == "ConsumeNoop"));
        Assert.All(r.Defender.Families, x => Assert.Equal(2, x.Uses));
    }
    [Theory]
    [InlineData(2, 1)] [InlineData(0, 2)] [InlineData(1, 2)]
    public void ElementalOpposingConsumptionIsCallerSpecific(int direction, int ownUsesAfter)
    {
        var e = Entry(direction, false, 7); e = e with { Defender = Family(e.Defender, F.AlwaysResist, 8, 2) };
        var r = Done(T.Begin(e));
        Assert.Equal(ownUsesAfter, Pair(r.Attacker, F.AlwaysNoResist).Uses);
        Assert.Equal(1, Pair(r.Defender, F.AlwaysResist).Uses); Assert.Equal(3, r.PrecheckCode);
    }
    [Fact]
    public void FailedDodgeExpirySuspendsBeforeFreshBlockRead()
    {
        var e = Entry(0, true, 1); e = e with { Defender = Family(e.Defender, F.AlwaysDodge, 1, 1, P.Present) };
        var s = UntilBoundary(T.Begin(e)); Assert.Null(s.ChosenCode);
        var after = Family(Family(s.Defender, F.AlwaysDodge, 0, 0, P.VerifiedAbsent), F.AlwaysBlock, 4, 2);
        var receipt = Removal(s, defender: after, dispatch: P.Present,
            callbacks: new[] { new T.CallbackObservation(0, "EndAlwaysDodge", E), new T.CallbackObservation(1, "ObservedStatChange", E) });
        var next = T.ResumeRemoval(s, receipt);
        Assert.Equal(S.BlockComparisonRead, next.PendingStage);
        var stale = (T.ComparisonRead)Read(next);
        Assert.Throws<ArgumentException>(() => T.Advance(next, stale with { Stamp = stale.Stamp with { SnapshotId = s.Defender.SnapshotId } }));
        var r = Done(next); Assert.Equal(2, r.PrecheckCode); Assert.Equal(1, Pair(r.Defender, F.AlwaysBlock).Uses);
        Assert.Equal(new[] { "WriteWrappedUses", "ClearBoundPointerAfterOriginalReturn", "SignedStrictComparison", "WriteWrappedUses", "WriteCounterDeadline" },
            r.Operations.SkipWhile(x => x.Kind != "WriteWrappedUses").Select(x => x.Kind));
    }
    [Fact]
    public void ParryTableAndIndexAreReadAfterFailedBlockRemoval()
    {
        var e = Entry(0, true, 1); e = e with { Defender = Family(e.Defender, F.AlwaysBlock, 1, 1, P.Present) };
        var s = UntilBoundary(T.Begin(e), dodgeTable: 1);
        var after = Family(s.Defender, F.AlwaysBlock, 0, 0, P.VerifiedAbsent) with { NativeTableIndex13d0 = 7 };
        var next = T.ResumeRemoval(s, Removal(s, defender: after));
        Assert.Equal(S.ParryTableRead, next.PendingStage);
        next = ReadOne(next, tableEntry: 1); var r = T.Finish(next);
        Assert.Equal(4, r.PrecheckCode);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Kind == "ComparisonRead" && x.Family == F.AlwaysParry);
    }
    [Fact]
    public void ChosenStatusSurvivesCallbackClearingItsPairAndChangingAttackerComparison()
    {
        var s = Await(F.AlwaysDodge); Assert.Equal(0, s.ChosenCode);
        var a = Family(s.Attacker, F.AlwaysHit, 1000, 2);
        var d = Family(s.Defender, F.AlwaysDodge, 0, 0, P.VerifiedAbsent);
        var next = T.ResumeRemoval(s, Removal(s, attacker: a, defender: d,
            dispatch: P.Present, callbacks: new[] { new T.CallbackObservation(0, "EndChosenDefense", E) }));
        Assert.Equal(S.OriginalTick, next.PendingStage); Assert.Equal(0, next.LatchedComparison);
        var r = Done(next); Assert.Equal(0, r.PrecheckCode); Assert.Equal(1000, Pair(r.Attacker, F.AlwaysHit).Comparison);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Family == F.AlwaysBlock);
    }
    [Fact]
    public void CallbackRebindingIsOverwrittenButItsNewIdentityIsPreserved()
    {
        var s = Await(F.AlwaysBlock);
        var d = Family(s.Defender, F.AlwaysBlock, 7, 9, P.Present, "callback-new-skill", 999);
        var next = T.ResumeRemoval(s, Removal(s, defender: d, dispatch: P.Present,
            callbacks: new[] { new T.CallbackObservation(0, "ObservedRebind", E) }));
        var p = Pair(next.Defender, F.AlwaysBlock);
        Assert.Equal(P.VerifiedAbsent, p.Binding.Presence); Assert.Null(p.Binding.SkillKey);
        Assert.Equal(999, p.Binding.EffectIdentity); Assert.Equal(7, p.Comparison); Assert.Equal(9, p.Uses);
        var clear = Assert.Single(next.Operations, x => x.Kind == "ClearBoundPointerAfterOriginalReturn");
        Assert.Equal("callback-new-skill", clear.Before); Assert.Equal("VerifiedAbsent", clear.After);
        var original = Assert.Single(next.OriginalRemovals);
        Assert.Equal(P.Present, Pair(original.DefenderAfterOriginalReturn, F.AlwaysBlock).Binding.Presence);
        Assert.Equal("callback-new-skill", Pair(original.DefenderAfterOriginalReturn, F.AlwaysBlock).Binding.SkillKey);
        Assert.Equal("ObservedRebind", Assert.Single(original.Callbacks).OriginalEvent);
        Assert.Equal(2, Done(next).PrecheckCode);
    }
    [Theory]
    [InlineData((int)F.AlwaysDodge, true, 0)] [InlineData((int)F.AlwaysParry, true, 1)]
    [InlineData((int)F.AlwaysBlock, true, 2)] [InlineData((int)F.AlwaysResist, false, 3)]
    public void ZeroUsesCanStillChooseAForcedStatusWithoutRemoval(int familyCode, bool physical, int code)
    {
        var family = (F)familyCode;
        var e = Entry(0, physical); e = e with { Defender = Family(e.Defender, family, 1, 0, P.Present) };
        var r = Done(T.Begin(e)); Assert.Equal(code, r.PrecheckCode);
        Assert.Equal(0, Pair(r.Defender, family).Uses); Assert.Equal(P.Present, Pair(r.Defender, family).Binding.Presence);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Kind is "BoundSkillRead" or "OriginalRemovalReturn");
    }
    [Fact]
    public void ExpiredCountAndAbsentBindingDoNotFabricatePairClearing()
    {
        var e = Entry(0, false); e = e with { Defender = Family(e.Defender, F.AlwaysResist, 1, 1, P.VerifiedAbsent) };
        var r = Done(T.Begin(e)); var p = Pair(r.Defender, F.AlwaysResist);
        Assert.Equal(3, r.PrecheckCode); Assert.Equal(0, p.Uses); Assert.Equal(1, p.Comparison);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Kind == "OriginalRemovalReturn");
    }
    [Fact]
    public void StrictEqualityConsumesEveryAllowedPhysicalFamily()
    {
        var e = Entry(0, true, 5);
        foreach (var f in new[] { F.AlwaysDodge, F.AlwaysBlock, F.AlwaysParry }) e = e with { Defender = Family(e.Defender, f, 5, 2) };
        var r = Done(T.Begin(e)); Assert.Equal(4, r.PrecheckCode); Assert.Empty(r.CounterWrites);
        Assert.Equal(3, r.Operations.Count(x => x.Kind == "WriteWrappedUses"));
        Assert.All(new[] { F.AlwaysDodge, F.AlwaysBlock, F.AlwaysParry }, f => Assert.Equal(1, Pair(r.Defender, f).Uses));
    }
    [Fact]
    public void NegativeOpposingComparisonCanChooseDodgeAgainstKnownAbsentManager()
    {
        var e = Entry(0, true, -1); e = e with { Defender = Actor("absent-defender", P.VerifiedAbsent) };
        var r = Done(T.Begin(e)); Assert.Equal(0, r.PrecheckCode);
        Assert.DoesNotContain(r.Operations, x => x.Kind == "WriteWrappedUses");
        Assert.All(r.Defender.Families, x => Assert.Null(x.Uses));
    }
    [Fact]
    public void ProhibitedTablesSkipTheirComparisonAndConsumeReads()
    {
        var s = UntilBoundary(T.Begin(Entry(0, true, 5)), dodgeTable: -1, parryTable: 2);
        var r = T.Finish(s); Assert.Equal(4, r.PrecheckCode);
        Assert.DoesNotContain(r.OriginalReceipts, x => x.Family is F.AlwaysDodge or F.AlwaysParry);
        Assert.Single(r.OriginalReceipts, x => x.Kind == "ConsumeRead");
    }
    [Fact]
    public void PostOpposingRemovalBlindIsAnExplicitUnsupportedBoundary()
    {
        var s = Await(F.AlwaysHit);
        var next = T.ResumeRemoval(s, Removal(s, attacker: s.Attacker with { AbnormalFlags1250 = 0x20 }));
        Assert.Equal(S.BlindRead, next.PendingStage); next = ReadOne(next);
        Assert.Equal(T.Progress.Unsupported, next.Status); Assert.NotNull(next.UnsupportedReason);
        Assert.DoesNotContain(next.Receipts, x => x.Receiver == R.Defender); Assert.Empty(next.Deadlines);
        Assert.Throws<ArgumentException>(() => T.Finish(next));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public void InvalidEntryProvenanceAndUnknownRequiredInputsReject(int variant)
    {
        var e = Entry();
        e = variant switch
        {
            0 => e with { Source = e.Source with { MainSha256 = new string('0', 64) } },
            1 => e with { Source = e.Source with { DatasetSha256 = new string('0', 64) } },
            2 => e with { Source = e.Source with { RegionContext = "country5" } },
            3 => e with { Source = e.Source with { ActionId = "" } },
            4 => e with { Source = e.Source with { AttackerGeneration = "" } },
            5 => e with { Source = e.Source with { AttackerObjectId = 22 } },
            6 => e with { OriginalNormalEntryGuardsPassed = false },
            7 => e with { InterveningWrites = P.Unknown },
            8 => e with { MainPhysicalFlag8e4 = null },
            9 => e with { Evidence = "" },
            _ => e with { OffhandPhysicalFlag8e8 = null }
        };
        Assert.Throws<ArgumentException>(() => T.Begin(e));
    }
    [Theory]
    [InlineData((int)RetailNpcNormalActorKind.Player, (int)RetailNpcNormalActorKind.Player)]
    [InlineData((int)RetailNpcNormalActorKind.OwnedNpc, (int)RetailNpcNormalActorKind.Player)]
    [InlineData((int)RetailNpcNormalActorKind.Player, (int)RetailNpcNormalActorKind.ScaledNpc)]
    [InlineData((int)RetailNpcNormalActorKind.Unverified, (int)RetailNpcNormalActorKind.OrdinaryOwnerlessNpc)]
    public void ScopeDependencyRejectsPvpOwnedScaledAndUnverifiedActors(int attackerKind, int defenderKind)
    {
        var a = (RetailNpcNormalActorKind)attackerKind; var d = (RetailNpcNormalActorKind)defenderKind;
        Assert.ThrowsAny<ArgumentException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            a, d, a == RetailNpcNormalActorKind.Player ? 0 : 212776, d == RetailNpcNormalActorKind.Player ? 0 : 212765));
    }
    [Fact]
    public void ScopeDependencyRequiresMatchedNpcIdentityBeforeTransitionCreation()
    {
        Assert.ThrowsAny<ArgumentException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 0, 0));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void ReadReceiptMustMatchScopeGenerationSnapshotReceiverAndOrder(int variant)
    {
        var s = T.Begin(Entry()); var read = (T.BlindRead)Read(s); var stamp = read.Stamp;
        stamp = variant switch
        {
            0 => stamp with { Source = stamp.Source with { ActionId = "other" } },
            1 => stamp with { Source = stamp.Source with { DatasetSha256 = new string('f', 64) } },
            2 => stamp with { Source = stamp.Source with { RegionContext = "other" } },
            3 => stamp with { Source = stamp.Source with { DefenderObjectId = 33 } },
            4 => stamp with { ActorGeneration = "other-life" },
            5 => stamp with { SnapshotId = "older-snapshot" },
            6 => stamp with { Receiver = R.Defender },
            7 => stamp with { Ordinal = stamp.Ordinal + 1 },
            _ => stamp with { Evidence = "" }
        };
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { Stamp = stamp }));
    }
    [Fact]
    public void WrongReadKindTableAddressAndIndexReject()
    {
        var s = T.Begin(Entry()); var stamp = Stamp(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.TableRead(stamp, T.DodgeTableAddress, 0, 0)));
        s = ReadOne(s); var table = (T.TableRead)Read(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, table with { TableAddress = T.ParryTableAddress }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, table with { NativeIndex = 1 }));
    }
    [Fact]
    public void WrongFamilyAndFabricatedComparisonRejectBeforeItCanAffectStatus()
    {
        var e = Entry(); e = e with { Defender = Family(e.Defender, F.AlwaysDodge, 1, 2) };
        var s = ReadOne(ReadOne(T.Begin(e))); var r = (T.ComparisonRead)Read(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, r with { Family = F.AlwaysBlock }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, r with { Comparison = 999 }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, r with { ManagerIdentity = "another-manager" }));
        s = ReadOne(s); var consume = (T.ConsumeRead)Read(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, consume with { Comparison = 999 }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, consume with { Uses = 999 }));
    }
    [Fact]
    public void UnknownCurrentFieldsCannotBecomeZeroOrAbsent()
    {
        var e = Entry(); var s = T.Begin(e with { Attacker = e.Attacker with { AbnormalFlags1250 = null } });
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.BlindRead(Stamp(s), 0)));
        e = e with { Defender = Actor("unknown-defender", P.Unknown) };
        s = ReadOne(ReadOne(T.Begin(e)));
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.ComparisonRead(Stamp(s), F.AlwaysDodge, P.Unknown, null, null)));
        e = Entry(); e = e with { Defender = Family(e.Defender, F.AlwaysDodge, null, 2) };
        s = ReadOne(ReadOne(T.Begin(e)));
        Assert.Throws<ArgumentException>(() => T.Advance(s, Read(s)));
    }
    [Fact]
    public void ZeroComparisonShortCircuitsCountReceiptAndLeavesKnownCountUntouched()
    {
        var s = ReadOne(ReadOne(ReadOne(T.Begin(Entry(0, true, 5)))));
        Assert.Equal(S.ConsumeRead, s.PendingStage); var read = (T.ConsumeRead)Read(s);
        Assert.Null(read.Uses);
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { Uses = 2 }));
        var next = T.Advance(s, read); Assert.Equal(2, Pair(next.Defender, F.AlwaysDodge).Uses);
    }
    [Fact]
    public void RequiredFailedConsumptionCannotBeSkippedByAdvancingToNextFamily()
    {
        var s = ReadOne(ReadOne(ReadOne(T.Begin(Entry(0, true, 5)))));
        Assert.Equal(S.ConsumeRead, s.PendingStage);
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.ComparisonRead(Stamp(s), F.AlwaysBlock,
            s.Defender.ManagerPresence, s.Defender.ManagerIdentity, 0)));
        Assert.Throws<ArgumentException>(() => T.Finish(s));
    }
    [Fact]
    public void BoundPointerAndIdentityReadsMustMatchAndCannotInventAbsentIdentityReads()
    {
        var e = Entry(0, false); e = e with { Defender = Family(e.Defender, F.AlwaysResist, 1, 1, P.Present) };
        var s = ReadOne(ReadOne(ReadOne(T.Begin(e)))); Assert.Equal(S.BoundSkillRead, s.PendingStage);
        var read = (T.BoundSkillRead)Read(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { SkillKey = "other-skill" }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { EffectIdentity = 999 }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { Family = F.AlwaysDodge }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { Presence = P.Unknown }));
        e = e with { Defender = Family(e.Defender, F.AlwaysResist, 1, 1, P.VerifiedAbsent) };
        s = ReadOne(ReadOne(ReadOne(T.Begin(e)))); read = (T.BoundSkillRead)Read(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, read with { EffectIdentity = 0 }));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void OriginalRemovalArgumentsAndCompletionEvidenceAreNotReplaceable(int variant)
    {
        var s = Await(F.AlwaysBlock); var r = Removal(s);
        r = variant switch
        {
            0 => r with { Call = r.Call with { SkillKey = "wrong-skill" } },
            1 => r with { Call = r.Call with { EffectIdentity = 999 } },
            2 => r with { Call = r.Call with { Reason = 999 } },
            3 => r with { Call = r.Call with { Receiver = R.Attacker } },
            4 => r with { CompleteOutcomeEvidence = false },
            5 => r with { ReturnValue = 2 },
            6 => r with { Call = r.Call with { Family = F.AlwaysResist } },
            7 => r with { Call = r.Call with { Param4 = 0 } },
            _ => r with { Call = r.Call with { ExpectedOrdinal = r.Call.ExpectedOrdinal + 1 } }
        };
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    [InlineData(6)] [InlineData(7)]
    public void UnknownOrStaleRemovalOutcomesRejectRatherThanInventCallbackWrites(int variant)
    {
        var s = Await(F.AlwaysBlock); var r = Removal(s);
        r = variant switch
        {
            0 => r with { EndCallbackDispatch = P.Unknown },
            1 => r with { EndCallbackDispatch = P.Present },
            2 => r with { AttackerAfter = s.Attacker },
            3 => r with { DefenderAfter = r.DefenderAfter with { AbnormalFlags1250 = null } },
            4 => r with { Evidence = "" },
            5 => r with { Stamp = r.Stamp with { ActorGeneration = "another-life" } },
            6 => r with { DefenderAfter = r.DefenderAfter with { ActorGeneration = "replacement-life" } },
            _ => r with { AttackerAfter = r.AttackerAfter with { ObjectId = 33 } }
        };
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
    }
    [Fact]
    public void CallbackOrderAndVerifiedAbsentDispatchAreExplicit()
    {
        var s = Await(F.AlwaysBlock);
        var r = Removal(s, dispatch: P.Present, callbacks: new[]
        { new T.CallbackObservation(0, "First", E), new T.CallbackObservation(0, "DuplicateOrder", E) });
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
        r = Removal(s, result: 0, dispatch: P.Present, callbacks: new[] { new T.CallbackObservation(0, "ContradictoryEnd", E) });
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
        r = Removal(s, callbacks: new[] { new T.CallbackObservation(0, "AbsentButHasEvents", E) });
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
        r = Removal(s, dispatch: P.Present, callbacks: new[] { new T.CallbackObservation(0, "MissingEvidence", "") });
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, r));
    }
    [Fact]
    public void ReusedReceiptCannotSkipAnotherBoundaryOrResumeAgainAfterProgress()
    {
        var s = T.Begin(Entry()); var read = Read(s); var next = T.Advance(s, read);
        Assert.Throws<ArgumentException>(() => T.Advance(next, read));
        var pending = Await(F.AlwaysBlock); var removal = Removal(pending);
        next = T.ResumeRemoval(pending, removal);
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(next, removal));
        Assert.Throws<ArgumentException>(() => T.Advance(pending, new T.BlindRead(Stamp(pending), 0)));
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, removal));
    }
    [Fact]
    public void MissingInputLeavesPendingRequestAndExtraClockInputsReject()
    {
        var initial = T.Begin(Entry()); Assert.Same(initial, T.Advance(initial));
        Assert.Throws<ArgumentException>(() => T.Finish(initial));
        var e = Entry(); e = e with { Defender = Family(e.Defender, F.AlwaysDodge, 1, 2) };
        var s = UntilBoundary(T.Begin(e)); Assert.Equal(S.OriginalTick, s.PendingStage);
        var tick = Tick(s);
        Assert.Throws<ArgumentException>(() => T.Advance(s, tick: tick with { Mask = 1 }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, tick: tick with { HelperAddress = 1 }));
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.BlindRead(Stamp(s), 0)));
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.BlindRead(Stamp(s), 0), tick));
        var complete = T.Advance(s, tick: tick);
        Assert.Throws<ArgumentException>(() => T.Advance(complete, tick: tick));
        Assert.Throws<ArgumentException>(() => T.Advance(complete, new T.BlindRead(Stamp(complete), 0)));
        Assert.Throws<ArgumentException>(() => T.Advance(initial, tick: tick));
    }
    [Fact]
    public void SnapshotInputCollectionsAreCopiedAndCalculatedStateDoesNotMutateInput()
    {
        var e = Entry(); var values = e.Defender.Families.ToArray();
        values[1] = values[1] with { Comparison = 1, Uses = 2 };
        e = e with { Defender = e.Defender with { Families = values } };
        var s = T.Begin(e); values[1] = values[1] with { Comparison = 999, Uses = 999 };
        var r = Done(s); Assert.Equal(0, r.PrecheckCode); Assert.Equal(1, Pair(r.Defender, F.AlwaysDodge).Uses);
        Assert.Equal(2, Pair(s.Defender, F.AlwaysDodge).Uses);
        Assert.Equal(999, Pair(e.Defender, F.AlwaysDodge).Uses);
    }
    [Fact]
    public void PostCallbackArraysAndOriginalObservationsHaveIndependentReadOnlyOwnership()
    {
        var s = Await(F.AlwaysBlock);
        var values = s.Defender.Families.ToArray();
        values[3] = values[3] with { Comparison = 7, Uses = 9 };
        var callbacks = new[] { new T.CallbackObservation(0, "ObservedEnd", E) };
        var receipt = Removal(s, defender: s.Defender with { Families = values }, dispatch: P.Present, callbacks: callbacks);
        var next = T.ResumeRemoval(s, receipt);
        values[3] = values[3] with { Comparison = 999, Uses = 999 };
        callbacks[0] = new(0, "ChangedOutsideTransition", E);
        var r = Done(next); Assert.Equal(7, Pair(r.Defender, F.AlwaysBlock).Comparison); Assert.Equal(9, Pair(r.Defender, F.AlwaysBlock).Uses);
        var original = Assert.Single(r.OriginalRemovals);
        Assert.Equal("ObservedEnd", Assert.Single(original.Callbacks).OriginalEvent);
        Assert.Equal(7, Pair(original.DefenderAfterOriginalReturn, F.AlwaysBlock).Comparison);
        Assert.Equal(P.Present, Pair(original.DefenderAfterOriginalReturn, F.AlwaysBlock).Binding.Presence);
        Assert.Equal(P.VerifiedAbsent, Pair(r.Defender, F.AlwaysBlock).Binding.Presence);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CompletePostCallbackEvidenceCannotContainUnknownModeledFields(int variant)
    {
        var s = Await(F.AlwaysBlock); var receipt = Removal(s); var d = receipt.DefenderAfter;
        d = variant switch
        {
            0 => Actor("after-unknown", P.Unknown),
            1 => Family(d, F.AlwaysBlock, 1, null),
            2 => Family(d, F.AlwaysBlock, null, 0),
            _ => Family(d, F.AlwaysBlock, 1, 0, P.Unknown)
        };
        Assert.Throws<ArgumentException>(() => T.ResumeRemoval(s, receipt with { DefenderAfter = d }));
    }
    [Fact]
    public void NonzeroRawFlagClassifiersAreLatchedWithoutInventingAnElementOrWeaponGroup()
    {
        var e = Entry(); e = e with { MainPhysicalFlag8e4 = uint.MaxValue, OffhandPhysicalFlag8e8 = 0 };
        var r = Done(T.Begin(e)); Assert.True(r.PhysicalSelector); Assert.Equal(1, r.MainClass); Assert.Equal(2, r.OffhandClass);
        Assert.Equal(4, r.PrecheckCode); Assert.Null(r.Damage); Assert.Null(r.FinalHpAfterCallbacks);
    }
    [Fact]
    public void FamilyShapesAndPresenceCannotHideUnknownCountsOrBindings()
    {
        var e = Entry();
        Assert.Throws<ArgumentException>(() => T.Begin(e with { Defender = e.Defender with { Families = e.Defender.Families.Take(5).ToArray() } }));
        var duplicate = e.Defender.Families.ToArray(); duplicate[0] = duplicate[1];
        Assert.Throws<ArgumentException>(() => T.Begin(e with { Defender = e.Defender with { Families = duplicate } }));
        Assert.Throws<ArgumentException>(() => T.Begin(e with { Attacker = Actor("unknown", P.Unknown, attacker: true) }));
        e = e with { Defender = Family(e.Defender, F.AlwaysDodge, 1, null) };
        var s = ReadOne(ReadOne(ReadOne(T.Begin(e))));
        Assert.Throws<ArgumentException>(() => T.Advance(s, Read(s)));
        e = Entry(); e = e with { Defender = Family(e.Defender, F.AlwaysDodge, 1, 1, P.Unknown) };
        s = ReadOne(ReadOne(ReadOne(ReadOne(T.Begin(e)))));
        Assert.Equal(S.BoundSkillRead, s.PendingStage);
        Assert.Throws<ArgumentException>(() => T.Advance(s, Read(s)));
    }
}
