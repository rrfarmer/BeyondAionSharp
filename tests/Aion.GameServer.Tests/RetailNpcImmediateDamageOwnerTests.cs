using Aion.GameServer.Controllers.Attack.RetailNpc;
using T = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcImmediateDamageOwner;

namespace Aion.GameServer.Tests;

/// <summary>
/// Hand/instruction-derived static fixtures, not original actor captures or native oracle results.
/// O/T/L/B/Q/R/N/G/D labels map all 47 frozen independent vectors. Additional cases exercise
/// complete admitted invocation order, source/alias/FP domains and rejected reached programs.
/// </summary>
public sealed class RetailNpcImmediateDamageOwnerTests
{
    private static readonly T.Evidence E = new(T.FactKind.SourceBoundDeclaration,
        "chosen-static-fixture;not-runtime-or-native-oracle");
    private static readonly T.Evidence Unknown = new(T.FactKind.Unknown, "unknown");
    private const uint A = 0x80001002, D = 0x80001001;
    private const string Op = "static-normal-record", Epoch = "serialized-static-epoch";
    private static T.FloatingContract Fp => new(true, true, true, E);

    private static T.Tick Tick(T.TickRole role, uint value) => new(role, role switch
    {
        T.TickRole.AttributionOuter => 0x1402a740c,
        T.TickRole.AttributionDecay => 0x1402aa365,
        T.TickRole.TimerEntry => 0x140d01b02,
        T.TickRole.TimerDue => 0x140d01c29,
        T.TickRole.TimerExit => 0x140d01da2,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    }, value, Op, Epoch, E);

    private static T.Input Make(int damage = 20, int hp = 100, int mp = 0,
        int mpRequest = 0, bool noKill = false, int notify = 1, uint dirty = 1)
    {
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
            212876, 212835);
        var template = new T.NativeTemplate(0, 0, 1, 0, 0, 0, 0, 0, 0, 55);
        T.Actor Actor(uint id, int npc, string manager, int actorHp, int actorMp, uint flags) =>
            new(new(id, npc, 2, T.NpcVtable, "life-" + id, manager, Op, Epoch, E),
                new(100, actorHp, 0, actorMp, 0, flags, 9, 7, 999, null, 0, 0, 0), template,
                new(3, 0, 0, 17), new(123, 29, 2, 1, new(0x3f800000, 0x40000000, 0x40400000)));
        var manager = new T.Manager(T.ManagerPresence.Present, 0x123400, 2, "manager-r",
            [new(7, true, [], E)], E); // Unrelated effects are PRESENT, not defaulted absent.
        var bound = new T.BoundSkill(0, 7, E);
        var ticks = new List<T.Tick>();
        // These are chosen call inputs for each static fixture, never captured/native outcomes.
        if (damage > 0 || Math.Min(mp, mpRequest) > 0)
            ticks.Add(Tick(T.TickRole.AttributionOuter, 1_000_000));
        if (damage > 0 && damage >= hp && !noKill)
        {
            ticks.Add(Tick(T.TickRole.TimerEntry, 1_000_010));
            ticks.Add(Tick(T.TickRole.TimerDue, 1_000_011));
            ticks.Add(Tick(T.TickRole.TimerExit, 1_000_012));
        }
        return new(scope, new(RetailNpcNormalScope.MainBinarySha256, T.Dataset, T.Region, Op, Epoch, E),
            Actor(A, 212876, "manager-a", 100, 0, 0), Actor(D, 212835, "manager-r", hp, mp, dirty),
            new(0x141246950, 320050000, 320050000, 0, "world-generation", Op, Epoch, E),
            new(damage, mpRequest, 0, 0, notify, 1, 0, 2, 0, 0xc5, (byte)(noKill ? 1 : 0), 0,
                new(T.CellStorage.OrdinaryNormalRecordApplicationCell, "record-0", "completion-frame-primary-cell",
                    Op, Epoch, true, true, 0x14068ed40, T.WorldSourceSha256,
                    T.ApplicationCellRole.PrimaryNormalCompletionLocal, E)),
            new(0, 0, 0, 0, manager, bound, bound, bound,
                new(T.ActionPresence.VerifiedNull, null, E), E),
            new(1_000_000, 0, [], E), new(100, E),
            new(0, false, 0, 1, uint.MaxValue, 0, 0, 0,
                [new(0, uint.MaxValue, [])], E),
            new([new(1, T.SocketPresence.Present, 0, 3, 0, T.OpenSendOutcome.NormalQueuedOrSent, E)], E),
            Fp, new(true, true, true, true, true, true, true, E), ticks);
    }

    private static T.Result Complete(T.Input input)
    {
        var result = T.Evaluate(input);
        Assert.Null(result.Rejection);
        Assert.Equal(T.Completion.CompleteImmediateOwner, result.Status);
        Assert.NotNull(result.Calculated);
        Assert.Null(result.FinalHpAfterDeferredLife);
        Assert.Equal(0, result.RngDrawsConsumed);
        Assert.False(result.ActivationReady);
        Assert.Equal(Enumerable.Range(1, result.Events.Count), result.Events.Select(e => e.Ordinal));
        return result;
    }
    private static T.Result Reject(T.Input input, string stage)
    {
        var result = T.Evaluate(input);
        Assert.Equal(T.Completion.RejectedBeforeEvaluation, result.Status);
        Assert.Equal(stage, result.Rejection?.Stage);
        Assert.Null(result.Calculated); Assert.Null(result.CoreReturn); Assert.Null(result.OwnerReturn);
        Assert.Null(result.ApplicationCell); Assert.Null(result.ActualHpLoss); Assert.Null(result.ActualMpDecrement);
        Assert.Empty(result.Events); Assert.Empty(result.ConsumedTicks);
        Assert.Null(result.FinalHpAfterDeferredLife);
        return result;
    }

    [Theory]
    [InlineData("O01", 100, 20, 0, 0, false, 1, 80, 0, 20, 20, 0, 0, 0, true, false)]
    [InlineData("O02", 20, 100, 0, 0, false, 1, 0, 0, 20, 20, 0, 0, 1, true, true)]
    [InlineData("O03", 20, 100, 0, 0, true, 1, 1, 0, 20, 19, 0, 0, 0, true, false)]
    [InlineData("O04", 100, 0, 7, 3, false, 1, 100, 4, 0, 0, 3, 3, 0, true, false)]
    [InlineData("O05", 100, 0, 7, 3, false, 0, 100, 4, 0, 0, 3, 2, 0, true, false)]
    [InlineData("O06", 100, 0, 0, 10, false, 1, 100, 0, 0, 0, 0, 3, 0, false, false)]
    [InlineData("O07", 100, 0, 0, 0, false, 1, 100, 0, 0, 0, 0, 1, 0, false, false)]
    [InlineData("O08", 20, 20, 7, 10, false, 1, 0, 0, 20, 20, 7, 2, 1, true, true)]
    public void Complete_owner_static_cases(string vector, int hp, int damage, int mp, int mpRequest,
        bool noKill, int notify, int expectedHp, int expectedMp, int cell, int loss, int actualMp,
        int dirty, int pending, bool owner, bool timer)
    {
        Assert.StartsWith("O", vector);
        T.Input input = Make(damage, hp, mp, mpRequest, noKill, notify);
        var r = Complete(input); var s = r.Calculated!;
        Assert.Equal(expectedHp, s.Receiver.Hp778); Assert.Equal(expectedMp, s.Receiver.Mp790);
        Assert.Equal(damage, r.RequestedCell); Assert.Equal(cell, r.ApplicationCell);
        Assert.Equal(loss, r.ActualHpLoss); Assert.Equal(actualMp, r.ActualMpDecrement);
        Assert.Equal((uint)dirty, s.Receiver.Dirty770); Assert.Equal(pending, s.Receiver.Pending728);
        Assert.Equal(owner, r.CoreReturn); Assert.Equal(owner, r.OwnerReturn);
        Assert.Equal(A, s.Receiver.LastAttacker1308);
        Assert.Equal(timer, r.Events.OfType<T.TimerRequest>().Any());
        Assert.Equal(timer ? 4 : 3, s.AttackerReferences.Count8);
        Assert.Equal(timer ? 4 : 3, s.ReceiverReferences.Count8);
        Assert.Equal(timer ? 1 : 0, s.ReceiverReferences.PendingTimers10);
        Assert.Equal(timer ? A : (uint?)null, s.Receiver.Killer918);
        Assert.Equal(7, s.Receiver.Field72c);
        Assert.Equal(damage == 0 && mpRequest == 0 ? 9 : 0, s.Receiver.Field788);
        if (owner)
        {
            var entry = Assert.Single(s.Attribution.Entries);
            Assert.Equal(cell, entry.Damage); Assert.Equal(5, entry.Classification);
            Assert.Equal(2, entry.Faction); Assert.Equal(0u, entry.PartyId);
            Assert.Equal(0, entry.LegionId); Assert.Equal(0, entry.LegionWorld);
            Assert.Equal(1_000_000u, entry.FirstTick); Assert.Equal(1_000_000u, entry.LastTick);
        }
        else Assert.Empty(s.Attribution.Entries);
        if (timer)
        {
            var request = Assert.Single(r.Events.OfType<T.TimerRequest>());
            Assert.True(request.Enqueued); Assert.Equal(1_002_011u, request.NativeReturnValue);
            Assert.Equal(new T.TimerEntry(1_002_011, D, 3, 0, 17), request.Entry);
            Assert.Single(r.Events.OfType<T.Telemetry>());
        }
        else Assert.Empty(r.Events.OfType<T.Telemetry>());
        Assert.Equal(hp, input.Receiver.State.Hp778); // Pure input ownership, including lethal results.
    }

    private static string Name(T.OwnerEvent e) => e switch
    {
        T.Trace t => t.Kind.ToString(), T.NoEnd => "NoEnd", T.Pass2Read => "Pass2Read",
        T.Sync s => "Sync:" + s.Kind, T.WorldHp => "WorldHp", T.HistoryUpdate => "History",
        T.RefChange r => "Ref:" + r.Reason, T.TimerRequest => "TimerReturn",
        T.TimerInserted => "TimerInserted", T.Telemetry => "Telemetry", _ => throw new InvalidOperationException()
    };

    [Fact]
    public void Complete_nonlethal_event_order_includes_real_no_End_and_fresh_bookkeeping()
    {
        var r = Complete(Make());
        Assert.Equal(new[] { "LastAttackerWrite", "InitialGate", "BreakGuard", "OnGetGuard",
            "CastGuard", "SiegeGuard", "HpAtomic", "Pass2Guard", "Pass2Read", "HpThresholdGuard",
            "Sync:Hp", "DirtyClear", "MasterSkip", "WorldHp", "Npc159Nop", "Clear788",
            "Tail161", "NoEnd", "ActionGuard", "CoreReturn", "WorldOnDamageNop", "TribeRead",
            "RewardRead", "History", "MasterSkip", "BindstoneSkip", "Sync:Damage", "OwnerReturn" },
            r.Events.Select(Name));
        var read = Assert.Single(r.Events.OfType<T.Pass2Read>());
        Assert.Equal((100, 0, 80, 0), (read.MaxHp, read.MaxMp, read.CurrentHp, read.CurrentMp));
        var hp = Assert.Single(r.Events.OfType<T.WorldHp>());
        Assert.Equal((-20, 2, (byte)80, A, D), (hp.Delta, hp.ChangeType, hp.Ratio, hp.SourceId, hp.ReceiverId));
        Assert.Equal(0u, hp.Skill); Assert.Equal(0xc5, hp.Log);
        Assert.Equal(new T.PositionBits(0x3f800000, 0x40000000, 0x40400000), hp.Position);
        var sync = Assert.Single(r.Events.OfType<T.Sync>(), e => e.Kind == T.SyncKind.Damage);
        Assert.Equal(80, sync.CurrentHp); Assert.Null(sync.MaxHp); Assert.Equal(20, sync.ApplicationCell);
        Assert.Equal(0L, sync.Threat); Assert.Null(sync.HateArgs);
    }

    [Fact]
    public void Complete_lethal_order_finishes_immediate_owner_and_defers_Die()
    {
        var r = Complete(Make(100, 20, 7, 10));
        Assert.Equal(new[] { "LastAttackerWrite", "InitialGate", "BreakGuard", "OnGetGuard", "CastGuard",
            "MpAtomic", "MpThresholdGuard", "HpAtomic", "DeathPolicy", "PendingDeathWrite", "WhenDeadGuard",
            "DirtyClear", "MasterSkip", "WorldHp", "Npc159Nop", "Clear788", "Tail161", "NoEnd",
            "ActionGuard", "CoreReturn", "WorldOnDamageNop", "TribeRead", "RewardRead", "History",
            "MasterSkip", "BindstoneSkip", "Sync:Damage", "Ref:killer acquisition", "KillerCas",
            "TimerClosing", "TimerPendingWrite", "Ref:timer acquisition", "TimerQueueSelect",
            "TimerInserted", "TimerMinWake", "TimerWakeRequest", "TimerReturn", "Telemetry", "OwnerReturn" },
            r.Events.Select(Name));
        Assert.DoesNotContain(r.Events.OfType<T.Trace>(), e => e.Kind is T.TraceKind.HpThresholdGuard or T.TraceKind.Pass2Guard);
        var telemetry = Assert.Single(r.Events.OfType<T.Telemetry>());
        Assert.Equal(0x135u, telemetry.EventId); Assert.Equal(D, telemetry.ReceiverId); Assert.Equal(A, telemetry.SourceId);
        Assert.Equal(17, telemetry.SourceKind115); Assert.Equal(17, telemetry.ReceiverKind115);
        Assert.Equal((long)int.MinValue, telemetry.Sentinel30);
        Assert.Equal((long)int.MinValue, telemetry.Sentinel31); Assert.Equal((long)int.MinValue, telemetry.Sentinel32);
        Assert.Equal(0, r.Calculated!.Receiver.Hp778);
        Assert.Equal(0, r.Calculated.Receiver.Mp790);
        Assert.Null(r.FinalHpAfterDeferredLife);
    }

    [Theory]
    [InlineData("T01", 0, true)] [InlineData("T02", 1, false)] [InlineData("T03", 2, true)]
    [InlineData("T04", 3, false)] [InlineData("T05", 4, true)] [InlineData("T06", 255, true)]
    public void Tail_byte_and_early_gate_cases(string vector, int rawByte, bool attempt)
    {
        var i = Make(); i = i with { Guards = i.Guards with { Byte1425 = (byte)rawByte } };
        var r = Complete(i);
        Assert.StartsWith("T", vector); Assert.Equal(attempt, r.Events.OfType<T.NoEnd>().Any());
        if (attempt) Assert.Equal(10, Assert.Single(r.Events.OfType<T.NoEnd>()).Reason);
    }

    [Theory]
    [InlineData("T07", 0, 0, 9, false)] [InlineData("T08", 20, 0, 0, true)]
    public void Early_gate_leaves_788_while_nontrivial_gate_clears_it_and_both_preserve_72c(
        string vector, int damage, int mpRequest, int field788, bool tail)
    {
        var r = Complete(Make(damage, mpRequest: mpRequest)); Assert.StartsWith("T", vector);
        Assert.Equal(field788, r.Calculated!.Receiver.Field788); Assert.Equal(7, r.Calculated.Receiver.Field72c);
        Assert.Equal(A, r.Calculated.Receiver.LastAttacker1308);
        Assert.Equal(tail, r.Events.OfType<T.Trace>().Any(e => e.Kind == T.TraceKind.Tail161));
    }

    public static IEnumerable<object[]> LookupVectors()
    {
        yield return ["L01", 0UL, Array.Empty<T.EffectRecord>(), true, null!];
        yield return ["L02", 11UL, new T.EffectRecord[] { new(22, 1) }, true, null!];
        yield return ["L03", 11UL, new T.EffectRecord[] { new(11, 0) }, true, (short)0];
        yield return ["L04", 11UL, new T.EffectRecord[] { new(11, 0), new(11, 1) }, true, (short)0];
        yield return ["L05", 11UL, new T.EffectRecord[] { new(11, 1), new(11, 0) }, false, (short)1];
        yield return ["L06", 11UL, new T.EffectRecord[] { new(11, -1) }, false, (short)-1];
    }
    [Theory]
    [MemberData(nameof(LookupVectors))]
    public void First_pointer_no_End_cases(string vector, ulong bound, object recordData,
        bool admitted, short? first)
    {
        var records = (T.EffectRecord[])recordData;
        Assert.StartsWith("L", vector); var i = Make();
        i = i with { Guards = i.Guards with { Bound168 = new(bound, 7, E),
            Manager = i.Guards.Manager with { Groups = [new(7, true, records, E)] } } };
        if (!admitted) { Reject(i, "removal"); return; }
        var r = Complete(i); var lookup = Assert.Single(r.Events.OfType<T.NoEnd>());
        Assert.Equal(first, lookup.FirstLevel); Assert.Equal(bound, lookup.SkillPointer);
        Assert.Equal(7, lookup.Group); Assert.True(lookup.ManagerPresent);
    }

    [Theory]
    [InlineData("B01", 599999u, 0u, true)] [InlineData("B02", 600000u, 0u, false)]
    [InlineData("B03", 600001u, 0u, true)] [InlineData("B04", 1000000u, 400000u, false)]
    public void Attribution_wrap_and_decay_clear_cases(string vector, uint outer, uint last, bool cleared)
    {
        Assert.StartsWith("B", vector); var i = Make();
        i = i with { Attribution = new(last, 77, [new(44, 44, 0, 0, 0, 2, 5, 12, 0, 0, 0)], E),
            Ticks = [Tick(T.TickRole.AttributionOuter, outer)] };
        var r = Complete(i); var h = Assert.Single(r.Events.OfType<T.HistoryUpdate>());
        Assert.Equal(cleared, h.Cleared);
        Assert.Equal(cleared ? 1 : 2, r.Calculated!.Attribution.Entries.Count);
        Assert.Equal(77u, r.Calculated.Attribution.LastSpecialSource6c);
        Assert.Equal(outer, r.Calculated.Attribution.LastTick68);
    }

    [Theory]
    [InlineData("B05", 599999u, 0u, 6000000, 5242879)]
    [InlineData("B06", 600000u, 0u, 6000000, 0)]
    [InlineData("B07", 1000u, 999u, 20, 20)]
    [InlineData("B08", 1u, 4294967294u, 6000000, 5242879)]
    public void Attribution_wrap_and_decay_existing_cases(string vector, uint inner, uint entryLast,
        int oldDamage, int decayed)
    {
        Assert.StartsWith("B", vector); var i = Make();
        i = i with { Attribution = new(1_000_000, 0,
            [new(A, A, 3, 4, 5, 7, 9, oldDamage, 88, entryLast, 123)], E),
            Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.AttributionDecay, inner)] };
        var r = Complete(i); var update = Assert.Single(r.Events.OfType<T.HistoryUpdate>());
        Assert.False(update.Cleared); Assert.Equal(decayed, update.DecayedDamage);
        var entry = Assert.Single(r.Calculated!.Attribution.Entries);
        Assert.Equal(decayed + 20, entry.Damage); Assert.Equal(1_000_000u, entry.LastTick);
        Assert.Equal(decayed == 0 ? 1_000_000u : 88u, entry.FirstTick);
        Assert.Equal((3u, 4, 5, 7, 9, 123u),
            (entry.PartyId, entry.LegionId, entry.LegionWorld, entry.Faction, entry.Classification, entry.Flags));
        Assert.Equal(new[] { T.TickRole.AttributionOuter, T.TickRole.AttributionDecay }, r.ConsumedTicks.Select(t => t.Role));
    }

    [Theory]
    [InlineData("Q01", 100u, 99u, true)] [InlineData("Q02", 100u, 100u, false)]
    [InlineData("Q03", 100u, 101u, false)] [InlineData("Q04", 4294967280u, 5u, false)]
    [InlineData("Q05", 5u, 4294967280u, true)]
    public void Timer_heap_due_closing_comparison_cases(string vector, uint parent, uint next, bool bubble)
    {
        Assert.StartsWith("Q", vector); var i = Make(100, 20);
        i = i with { Scheduler = i.Scheduler with { Queues = [new(0, parent, [new(parent, 44, 4, 2, 5)])] },
            Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.TimerEntry, 100),
                Tick(T.TickRole.TimerDue, unchecked(next - 2000)), Tick(T.TickRole.TimerExit, 101)] };
        var r = Complete(i); var heap = Assert.Single(r.Calculated!.Scheduler.Queues).Heap;
        Assert.Equal(bubble ? D : 44u, heap[0].ActorId); Assert.Equal(bubble ? 44u : D, heap[1].ActorId);
        Assert.Equal(next, Assert.Single(r.Events.OfType<T.TimerRequest>()).NativeReturnValue);
    }

    [Theory]
    [InlineData("Q06", 1000u, 3000u)] [InlineData("Q07", 4294967280u, 1984u)]
    public void Timer_due_uses_second_original_tick(string vector, uint second, uint due)
    {
        Assert.StartsWith("Q", vector); var i = Make(100, 20);
        i = i with { Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.TimerEntry, 100),
            Tick(T.TickRole.TimerDue, second), Tick(T.TickRole.TimerExit, 101)] };
        var r = Complete(i); var request = Assert.Single(r.Events.OfType<T.TimerRequest>());
        Assert.Equal(due, request.Entry!.Due); Assert.Equal(due, request.NativeReturnValue);
        Assert.Equal(second, request.DueTick); Assert.True(request.Enqueued);
    }

    [Fact]
    public void Q08_Closing_IO_returns_before_timer_reference_pending_and_queue_changes()
    {
        var i = Make(100, 20); i = i with { Scheduler = i.Scheduler with { ClosingIo = 1 },
            Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.TimerEntry, 100)] };
        var r = Complete(i); var s = r.Calculated!;
        var timer = Assert.Single(r.Events.OfType<T.TimerRequest>());
        Assert.False(timer.Enqueued); Assert.Equal(0u, timer.NativeReturnValue);
        Assert.Null(timer.DueTick); Assert.Null(timer.ExitTick); Assert.Null(timer.Entry);
        Assert.Equal(3, s.ReceiverReferences.Count8); Assert.Equal(0, s.ReceiverReferences.PendingTimers10);
        Assert.Equal(4, s.AttackerReferences.Count8); Assert.Equal(A, s.Receiver.Killer918);
        Assert.Empty(Assert.Single(s.Scheduler.Queues).Heap); Assert.Equal(0UL, s.Scheduler.EnqueueCount);
        Assert.Single(r.Events.OfType<T.Telemetry>()); // Ignored timer failure does not skip lethal telemetry.
    }

    [Theory]
    [InlineData("R01", false, 4, 1)] [InlineData("R02", true, 3, 2)]
    public void References_and_killer_cases(string vector, bool existing, int count, int deltas)
    {
        Assert.StartsWith("R", vector); var i = Make(100, 20);
        if (existing) i = i with { Receiver = i.Receiver with { State = i.Receiver.State with { Killer918 = 44 } } };
        var r = Complete(i); Assert.Equal(count, r.Calculated!.AttackerReferences.Count8);
        Assert.Equal(existing ? 44u : A, r.Calculated.Receiver.Killer918);
        var refs = r.Events.OfType<T.RefChange>().Where(e => e.ActorId == A).ToArray();
        Assert.Equal(deltas, refs.Length); Assert.Equal(3, refs[0].Before); Assert.Equal(4, refs[0].After);
        if (existing) Assert.Equal((4, 3), (refs[1].Before, refs[1].After));
    }

    [Fact]
    public void R03_timer_acquires_actor_reference_and_pending_count_without_inline_callback()
    {
        var r = Complete(Make(100, 20));
        Assert.Equal(4, r.Calculated!.ReceiverReferences.Count8);
        Assert.Equal(1, r.Calculated.ReceiverReferences.PendingTimers10);
        Assert.Equal(17, Assert.Single(r.Events.OfType<T.TimerRequest>()).Entry!.Generation);
        Assert.Null(r.FinalHpAfterDeferredLife);
    }

    [Fact]
    public void N01_pending_death_suppresses_HP_sync_but_still_clears_dirty_bit()
    {
        var r = Complete(Make(100, 20, dirty: 3));
        Assert.DoesNotContain(r.Events.OfType<T.Sync>(), e => e.Kind == T.SyncKind.Hp);
        Assert.Equal(2u, r.Calculated!.Receiver.Dirty770);
        Assert.Contains(r.Events.OfType<T.Trace>(), e => e.Kind == T.TraceKind.MasterSkip && e.Before == 0);
        Assert.Single(r.Events.OfType<T.WorldHp>());
    }

    [Theory]
    [InlineData("N02", false, 0, 0, 0)]
    [InlineData("N03", true, 2, 1, 1)]
    public void Notification_transport_cases(string vector, bool present, int state, int result,
        int disposition)
    {
        Assert.StartsWith("N", vector); var i = Make();
        i = i with { Transport = new([new(1, present ? T.SocketPresence.Present : T.SocketPresence.VerifiedNull,
            present ? state : null, present ? 3 : null, present ? 0 : null, T.OpenSendOutcome.Unknown, E)], E) };
        var r = Complete(i);
        Assert.Equal(80, r.Calculated!.Receiver.Hp778); Assert.True(r.OwnerReturn);
        foreach (var sync in r.Events.OfType<T.Sync>())
        {
            Assert.Equal(result, sync.Transport.SendSyncReturn); Assert.Equal((T.TransportDisposition)disposition, sync.Transport.Disposition);
            Assert.True(sync.Transport.BufferReleaseInvoked);
            Assert.Equal(present ? 1 : 0, sync.Transport.SendSyncReferenceAdds);
            Assert.Equal(present ? 1 : 0, sync.Transport.SendSyncReferenceReleases);
        }
    }

    [Theory]
    [InlineData("G01", 10000.5f, 10000, true)] [InlineData("G02", 10001f, 10001, false)]
    public void Optional_adapter_comparison_cases(string vector, float value, int truncated, bool send)
    {
        Assert.StartsWith("G", vector);
        var result = T.CompareSuppliedDppsResult(value, Fp);
        Assert.Equal(truncated, result.Truncated); Assert.Equal(send, result.Within);
        // The owner emits a WorldHp request; this optional scalar check invents no geometry or recipient.
    }

    [Fact]
    public void D01_independent_related_owner_guard_is_not_inferred_from_null_1850()
    {
        var i = Make(); i = i with { Receiver = i.Receiver with { State = i.Receiver.State with { Related1894 = 1, Pointer1850 = 0 } } };
        Reject(i, "owner");
    }

    [Fact]
    public void NoKill_at_one_HP_keeps_positive_application_cell_but_has_zero_actual_loss()
    {
        var r = Complete(Make(1, 1, noKill: true));
        Assert.Equal(1, r.ApplicationCell); Assert.Equal(0, r.ActualHpLoss); Assert.True(r.OwnerReturn);
        Assert.Equal(-1, Assert.Single(r.Events.OfType<T.WorldHp>()).Delta);
        Assert.Equal(1, Assert.Single(r.Calculated!.Attribution.Entries).Damage);
        Assert.Empty(r.Events.OfType<T.TimerRequest>());
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(-1)]
    public void Generic_notification_uses_zero_delta_type5_c5_and_zero_critical(int rawNotify)
    {
        var i = Make(0, mp: 7, mpRequest: 3, notify: rawNotify);
        i = i with { Call = i.Call with { Log1c = 123, Critical21 = 255 } };
        var r = Complete(i); var hp = Assert.Single(r.Events.OfType<T.WorldHp>());
        Assert.Equal((0, 5, 0xc5, (byte)0), (hp.Delta, hp.ChangeType, hp.Log, hp.Critical));
        Assert.Equal(2u, r.Calculated!.Receiver.Dirty770);
    }

    [Fact]
    public void Zero_net_MP_still_reaches_MP_and_HP_threshold_guards_and_tail()
    {
        var r = Complete(Make(0, mpRequest: 10));
        Assert.False(r.OwnerReturn); Assert.Equal(3u, r.Calculated!.Receiver.Dirty770);
        Assert.Equal(new[] { T.TraceKind.MpAtomic, T.TraceKind.MpThresholdGuard, T.TraceKind.HpThresholdGuard },
            r.Events.OfType<T.Trace>().Where(e => e.Kind is T.TraceKind.MpAtomic or T.TraceKind.MpThresholdGuard or T.TraceKind.HpThresholdGuard).Select(e => e.Kind));
        Assert.Single(r.Events.OfType<T.NoEnd>()); Assert.Empty(r.Events.OfType<T.Sync>());
    }

    [Fact]
    public void Lethal_immediate_owner_preserves_undamaged_MP_until_deferred_life_owner()
    {
        var r = Complete(Make(100, 20, mp: 7));
        Assert.Equal(7, r.Calculated!.Receiver.Mp790); Assert.Equal(0, r.Calculated.Receiver.Hp778);
        Assert.Equal(0, r.ActualMpDecrement); Assert.Null(r.FinalHpAfterDeferredLife);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(-1)]
    public void Lethal_damage_sync_context10_gate_preserves_pending_read(int context10)
    {
        var i = Make(100, 20); i = i with { Call = i.Call with { Context10 = context10 } };
        var r = Complete(i); Assert.Equal(context10 == 0, r.Events.OfType<T.Sync>().Any(s => s.Kind == T.SyncKind.Damage));
        Assert.True(r.OwnerReturn); Assert.Equal(0, r.Calculated!.Receiver.Hp778);
    }

    [Fact]
    public void Hate_only_core_false_uses_sign_extended_threat_and_owner_returns_false()
    {
        var i = Make(0); i = i with { Call = i.Call with { RawThreat = int.MinValue } };
        var r = Complete(i); var sync = Assert.Single(r.Events.OfType<T.Sync>());
        Assert.Equal(T.SyncKind.Hate, sync.Kind); Assert.Equal((long)int.MinValue, sync.Threat);
        Assert.Equal(new T.HateParameters(A, 0, D, int.MinValue, 0, false), sync.HateArgs);
        Assert.Null(sync.CurrentHp); Assert.Null(sync.ApplicationCell); Assert.Null(sync.Skill);
        Assert.False(r.CoreReturn); Assert.False(r.OwnerReturn); Assert.Equal(100, r.Calculated!.Receiver.Hp778);
        Assert.Empty(r.Events.OfType<T.WorldHp>()); Assert.Empty(r.Events.OfType<T.HistoryUpdate>());
    }

    [Fact]
    public void Context0c_zero_suppresses_damage_and_hate_not_HP_or_attribution()
    {
        var i = Make(); i = i with { Call = i.Call with { Context0c = 0, RawThreat = 5 } };
        var r = Complete(i); Assert.Single(r.Events.OfType<T.Sync>(), s => s.Kind == T.SyncKind.Hp);
        Assert.DoesNotContain(r.Events.OfType<T.Sync>(), s => s.Kind is T.SyncKind.Hate or T.SyncKind.Damage);
        Assert.Single(r.Events.OfType<T.HistoryUpdate>()); Assert.True(r.OwnerReturn);
    }

    [Fact]
    public void Nonrandom_break_and_tail_removals_are_calculated_in_native_order()
    {
        var i = Make(); i = i with { Guards = i.Guards with { Abnormal1250 = 0x4000008,
            Bound160 = new(11, 2, E), Bound210 = new(22, 3, E), Bound168 = new(33, 7, E),
            Manager = i.Guards.Manager with { Groups = [new(2, true, [new(11, 0)], E),
                new(3, true, [new(99, 1)], E), new(7, true, [], E)] } } };
        var r = Complete(i); var removes = r.Events.OfType<T.NoEnd>().ToArray();
        Assert.Equal(new[] { 11UL, 22UL, 33UL }, removes.Select(x => x.SkillPointer));
        Assert.Equal(new[] { 20, 20, 10 }, removes.Select(x => x.Reason));
        Assert.Equal((short)0, removes[0].FirstLevel); Assert.Null(removes[1].FirstLevel); Assert.Null(removes[2].FirstLevel);
        Assert.True(removes[1].Ordinal < r.Events.OfType<T.Trace>().Single(t => t.Kind == T.TraceKind.OnGetGuard).Ordinal);
    }

    [Fact]
    public void Null_bound_does_not_index_invalid_group_but_nonnull_bound_does()
    {
        var i = Make(); i = i with { Guards = i.Guards with { Bound168 = new(0, -99, E),
            Manager = i.Guards.Manager with { Groups = [] } } };
        Assert.Equal(-99, Assert.Single(Complete(i).Events.OfType<T.NoEnd>()).Group);
        Reject(i with { Guards = i.Guards with { Bound168 = new(12, -99, E) } }, "removal");
    }

    [Theory]
    [InlineData(1)] [InlineData(3)]
    public void Tail_skip_does_not_invent_unknown_bound_pointer_or_group(int mode)
    {
        var i = Make(); i = i with { Guards = i.Guards with { Byte1425 = (byte)mode,
            Bound168 = new(null, null, Unknown) } };
        Assert.Empty(Complete(i).Events.OfType<T.NoEnd>());
    }

    [Fact]
    public void Verified_null_manager_skips_active_effect_flags_and_reached_no_End_is_false()
    {
        var i = Make(); i = i with { Guards = i.Guards with {
            Abnormal1250 = 0x210, Bound168 = new(9, -99, E),
            Manager = new(T.ManagerPresence.VerifiedNull, 0, null, "manager-r", [], E) } };
        var r = Complete(i); var removal = Assert.Single(r.Events.OfType<T.NoEnd>());
        Assert.False(removal.ManagerPresent); Assert.Null(removal.FirstLevel);
        Assert.DoesNotContain(r.Events.OfType<T.Trace>(), t => t.Kind is T.TraceKind.OnGetGuard or T.TraceKind.Pass2Guard);
    }

    [Fact]
    public void Zero_manager_flags_skip_Pass2_stat_getters_but_unrelated_flags_do_not()
    {
        var i = Make(); i = i with { Guards = i.Guards with { Manager = i.Guards.Manager with { Flags2b18 = 0 } } };
        Assert.Empty(Complete(i).Events.OfType<T.Pass2Read>());
        Assert.Single(Complete(Make()).Events.OfType<T.Pass2Read>());
    }

    [Fact]
    public void Early_zero_gate_does_not_require_unreached_effect_cast_action_reward_or_socket_inputs()
    {
        var i = Make(0); i = i with { Guards = i.Guards with { Abnormal1250 = null, CurrentCast131c = null,
            Systems12cc = null, Byte1425 = null, Manager = new(T.ManagerPresence.Unknown, 0, null, "manager-r", [], Unknown),
            ActionInfo = new(T.ActionPresence.Unknown, null, Unknown) },
            Floating = new(false, false, false, Unknown),
            Transport = new([], Unknown), Reward = new(0, Unknown) };
        var r = Complete(i); Assert.False(r.OwnerReturn); Assert.Equal(9, r.Calculated!.Receiver.Field788);
        Assert.Equal(new[] { "LastAttackerWrite", "InitialGate", "CoreReturn", "OwnerReturn" }, r.Events.Select(Name));
    }

    [Fact]
    public void Kind9_skips_cast_and_sleep_rng_but_tail_still_receives_native_kind2()
    {
        var i = Make(); i = i with { Call = i.Call with { Kind14 = 9, Cell = i.Call.Cell with {
                Role = T.ApplicationCellRole.RetaliationNormalCompletionLocal } },
            Guards = i.Guards with { CurrentCast131c = null, Abnormal1250 = 0x210 } };
        var r = Complete(i); Assert.DoesNotContain(r.Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.CastGuard);
        Assert.Single(r.Events.OfType<T.NoEnd>()); Assert.Equal(9, Assert.Single(r.Events.OfType<T.WorldHp>()).ChangeType);
    }

    [Theory]
    [InlineData(0x10u, "break")] [InlineData(0x200u, "break")]
    public void Reached_sleep_rng_is_rejected(uint abnormal, string stage)
    {
        var i = Make(); Reject(i with { Guards = i.Guards with { Abnormal1250 = abnormal } }, stage);
    }
    [Theory]
    [InlineData(0x100u, "on-get", 20)] [InlineData(0x200u, "on-get", 20)]
    [InlineData(0x80u, "pass2", 20)] [InlineData(0x2000u, "pass2", 20)]
    [InlineData(0x400u, "when-dead", 100)]
    public void Reached_effect_programs_reject_without_partial_HP_results(uint flags, string stage, int damage)
    {
        var i = Make(damage, damage == 100 ? 20 : 100);
        Reject(i with { Guards = i.Guards with { Manager = i.Guards.Manager with { Flags2b18 = flags } } }, stage);
    }

    [Fact]
    public void Unreached_Pass2_And_HP_system_bits_on_lethal_branch_are_not_blanket_absence_gates()
    {
        var i = Make(100, 20); i = i with { Guards = i.Guards with { Systems12cc = 1,
            Manager = i.Guards.Manager with { Flags2b18 = 0x2080 } } };
        Complete(i);
        var noKill = Make(100, 20, noKill: true); noKill = noKill with { Guards = noKill.Guards with {
            Manager = noKill.Guards.Manager with { Flags2b18 = 0x2480 }, Systems12cc = 1 } };
        Complete(noKill); // NoKill also skips WhenDead and Pass2.
    }

    [Theory]
    [InlineData(1u, 0)] [InlineData(2u, 10)]
    public void Reached_system_flags_reject_including_zero_net_MP(uint flags, int mpRequest)
    {
        var i = Make(0, mpRequest: mpRequest == 0 ? 1 : mpRequest);
        Reject(i with { Guards = i.Guards with { Systems12cc = flags } }, "system-effect");
    }

    [Theory]
    [InlineData(1u)] [InlineData(2u)] [InlineData(4u)] [InlineData(0x10u)]
    [InlineData(0x200u)] [InlineData(0x800u)] [InlineData(0x10000u)]
    public void Reached_action_mask_rejects_and_zero_other_bits_preserve_72c(uint mask)
    {
        var i = Make(); Reject(i with { Guards = i.Guards with {
            ActionInfo = new(T.ActionPresence.Present, mask, E) } }, "action-info");
    }

    [Fact]
    public void Active_cast_unknown_flags_and_incomplete_lookup_are_explicit_rejections()
    {
        var i = Make();
        Reject(i with { Guards = i.Guards with { CurrentCast131c = 1 } }, "cast");
        Reject(i with { Guards = i.Guards with { Abnormal1250 = null } }, "break abnormal1250");
        Reject(i with { Guards = i.Guards with { Systems12cc = null } }, "system flags12cc");
        Reject(i with { Guards = i.Guards with { Byte1425 = null } }, "tail byte1425");
        Reject(i with { Guards = i.Guards with { Manager = i.Guards.Manager with { Flags2b18 = null } } }, "manager flags2b18");
        Reject(i with { Guards = i.Guards with { Bound168 = new(12, 7, E),
            Manager = i.Guards.Manager with { Groups = [new(7, false, [], E)] } } }, "removal");
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(6)] [InlineData(12)]
    public void Exact_full_HP_siege_mask_is_rejected(int alert)
    {
        var i = Make(); i = i with { Receiver = i.Receiver with {
            Template = i.Receiver.Template with { RaceAlert3ff = (byte)alert } } };
        Reject(i, "siege");
    }

    [Theory]
    [InlineData(0x141243c18UL)] [InlineData(0x1412b21f8UL)] [InlineData(0x1412b3e00UL)]
    public void Derived_world_overrides_are_rejected_even_when_map_number_is_pilot(ulong vtable)
    {
        var i = Make(); Reject(i with { World = i.World with { Vtable = vtable } }, "world");
    }

    [Theory]
    [InlineData(0x141242520UL)] [InlineData(0x1412575d8UL)] [InlineData(0x141242cc0UL)]
    [InlineData(0x1412254f0UL)] [InlineData(0x141225c18UL)] [InlineData(0x141227518UL)]
    [InlineData(0x141229fa8UL)] [InlineData(0x141227090UL)] [InlineData(0x14122bc48UL)]
    [InlineData(0x14122a610UL)] [InlineData(0x14122b1f0UL)] [InlineData(0x141263180UL)]
    [InlineData(0x1412638f0UL)] [InlineData(0x1412546a8UL)] [InlineData(0x141246950UL)]
    [InlineData(0x1412521d8UL)]
    public void All_sixteen_actual_Nop_world_bindings_are_admitted(ulong vtable)
    {
        var i = Make(); Complete(i with { World = i.World with { Vtable = vtable } });
    }

    [Fact]
    public void Abyss_and_bindstone_programs_are_not_omitted()
    {
        var i = Make(); Reject(i with { World = i.World with { NativeId24 = 400010000 } }, "world");
        Reject(i with { Receiver = i.Receiver with { Template = i.Receiver.Template with { Bindstone400 = 1 } } }, "bindstone");
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(2, true, true)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public void Alias_unknown_overlap_or_dispatch_scratch_rejects(int storage, bool disjoint, bool original)
    {
        var i = Make(); Reject(i with { Call = i.Call with { Cell = i.Call.Cell with {
            Storage = (T.CellStorage)storage, DisjointFromActorOwnedStorage = disjoint, IsOriginalApplicationCell = original } } }, "alias");
    }

    [Fact]
    public void Alias_operation_epoch_and_provenance_are_real_admission_requirements()
    {
        var i = Make();
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { Operation = "other" } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { Epoch = "other" } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { OwnershipFunction = 0 } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { OwnershipSourceSha256 = "other" } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { Role = T.ApplicationCellRole.Unknown } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { Role = T.ApplicationCellRole.RetaliationNormalCompletionLocal } } }, "alias");
        Reject(i with { Call = i.Call with { Cell = i.Call.Cell with { Evidence = Unknown } } }, "application-cell alias");
        var observed = i with { Call = i.Call with { Cell = i.Call.Cell with {
            Evidence = new(T.FactKind.ObservedOriginal, "declared-test-of-observed-type;not-actual-observation") } } };
        Complete(observed);
        Assert.Equal(T.FactKind.SourceBoundDeclaration, i.Source.Evidence.Kind);
    }

    [Theory]
    [InlineData(-1, 0, 2, 0u)] [InlineData(0, -1, 2, 0u)]
    [InlineData(1, 0, 10, 0u)] [InlineData(1, 0, 2, 16522u)]
    public void Unsupported_invocation_does_not_enter_signed_dispatch_or_skill_owner(int hp, int mp, int kind, uint skill)
    {
        var i = Make(); Reject(i with { Call = i.Call with { RequestedHp = hp, RequestedMp = mp, Kind14 = kind, Skill18 = skill } }, "invocation");
    }

    [Fact]
    public void Pinned_source_scope_actor_generation_and_ownership_are_independent()
    {
        var i = Make();
        Reject(i with { Source = i.Source with { MainSha256 = new string('0', 64) } }, "identity");
        Reject(i with { Source = i.Source with { DatasetSha256 = "other" } }, "identity");
        Reject(i with { Source = i.Source with { RegionContext = "Japan" } }, "identity");
        Reject(i with { Attacker = i.Attacker with { Binding = i.Attacker.Binding with { Vtable = 1 } } }, "actor");
        Reject(i with { Attacker = i.Attacker with { Binding = i.Attacker.Binding with { Epoch = "later" } } }, "actor");
        Reject(i with { Receiver = i.Receiver with { Binding = i.Receiver.Binding with { TemplateId = 1 } } }, "actor");
        Reject(i with { Guards = i.Guards with { Manager = i.Guards.Manager with { Generation = "later" } } }, "generation");
        Reject(i with { Attacker = i.Attacker with { State = i.Attacker.State with { Master188c = 1 } } }, "owner");
        Reject(i with { Attacker = i.Attacker with { State = i.Attacker.State with { Related1894 = 1 } } }, "owner");
        var playerScope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, RetailNpcNormalActorKind.Player, 212876, 0);
        Reject(i with { Scope = playerScope }, "scope");
    }

    [Theory]
    [InlineData(0, 0, 0)] [InlineData(101, 0, 0)] [InlineData(10, -1, 0)] [InlineData(10, 0, 1)]
    public void Unsupported_initial_life_is_rejected(int hp, int mp, int pending)
    {
        var i = Make(); Reject(i with { Receiver = i.Receiver with { State = i.Receiver.State with {
            Hp778 = hp, Mp790 = mp, Pending728 = pending } } }, "life");
    }

    [Theory]
    [InlineData(1, 0)] [InlineData(0, 1)] [InlineData(0, 2)] [InlineData(0, 3)]
    public void Missing_infrastructure_contract_is_rejected(int normalReturn, int other)
    {
        var i = Make(); var c = i.Infrastructure;
        c = normalReturn == 1 ? c with { NormalAllocationLockTransportTelemetryReturn = false } : other switch {
            1 => c with { NoInlineGameCallback = false }, 2 => c with { NoDeferredCallbackDuringInvocation = false },
            3 => c with { StableActorAndSocketLifetimes = false }, _ => c };
        Reject(i with { Infrastructure = c }, "infrastructure");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Reward_FP_contract_rejects_wrong_rounding_exceptions_or_normal_return(int missing)
    {
        var i = Make(); var fp = missing switch { 0 => Fp with { RoundToNearestEven = false },
            1 => Fp with { MaskedExceptions = false }, _ => Fp with { NormalReturn = false } };
        Reject(i with { Floating = fp }, "floating");
    }

    [Theory]
    [InlineData(100, 1L, 1L, true)] [InlineData(99, 1L, 0L, false)]
    [InlineData(-100, 1L, -1L, true)] [InlineData(100, -1L, -1L, true)]
    [InlineData(0, 1L, 0L, false)] [InlineData(100, -9223372036854775808L, -9223372036854775808L, true)]
    public void XP_guard_preserves_signed_f32_divide_multiply_then_CVTT64(int rate, long xp, long scaled, bool add)
    {
        var i = Make(); i = i with { Reward = new(rate, E),
            Receiver = i.Receiver with { Template = i.Receiver.Template with { Xp198 = xp } },
            Ticks = add ? [Tick(T.TickRole.AttributionOuter, 1_000_000)] : [] };
        var r = Complete(i); var trace = Assert.Single(r.Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.RewardRead);
        Assert.Equal(scaled, trace.Before); Assert.Equal(add ? 1 : 0, trace.After);
        Assert.Equal(add, r.Events.OfType<T.HistoryUpdate>().Any());
    }

    [Fact]
    public void XP_max_long_rounds_to_unrepresentable_positive_2pow63_and_rejects()
    {
        var i = Make(); Reject(i with { Receiver = i.Receiver with {
            Template = i.Receiver.Template with { Xp198 = long.MaxValue } } }, "reward");
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Other_reward_fields_are_checked_after_zero_scaled_XP(int which)
    {
        var i = Make(); var t = i.Receiver.Template with { Xp198 = 0 };
        t = which switch { 0 => t with { Reward2f0 = 1 }, 1 => t with { Reward370 = -1 }, _ => t with { Reward1a0 = 1 } };
        Complete(i with { Receiver = i.Receiver with { Template = t } });
    }

    [Theory]
    [InlineData(0x7fc00000u)] [InlineData(0x7f800000u)] [InlineData(0xff800000u)]
    [InlineData(1u)] [InlineData(0x007fffffu)] [InlineData(0x4f000000u)] [InlineData(0xbf800000u)]
    public void Optional_visibility_rejects_nonfinite_subnormal_negative_or_unrepresentable(uint bits)
    {
        Assert.ThrowsAny<ArgumentException>(() => T.CompareSuppliedDppsResult(BitConverter.UInt32BitsToSingle(bits), Fp));
    }

    [Theory]
    [InlineData(0u)] [InlineData(0x80000000u)] [InlineData(0x00800000u)] [InlineData(0x4effffffu)]
    public void Optional_visibility_admits_zero_min_normal_and_last_representable_CVTT32(uint bits)
    {
        var r = T.CompareSuppliedDppsResult(BitConverter.UInt32BitsToSingle(bits), Fp);
        Assert.Equal(bits == 0x4effffff ? 2147483520 : 0, r.Truncated);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Malformed_original_clock_sequence_rejects_without_exposed_mutations(int variant)
    {
        var i = Make(100, 20); var ticks = i.Ticks.ToArray();
        if (variant == 0) ticks[1] = ticks[1] with { CallSite = 0 };
        if (variant == 1) ticks[1] = ticks[1] with { Epoch = "other" };
        if (variant == 2) (ticks[1], ticks[2]) = (ticks[2], ticks[1]);
        Reject(i with { Ticks = ticks }, "clock");
        Assert.Equal(20, i.Receiver.State.Hp778); Assert.Equal(3, i.Attacker.References.Count8);
    }

    [Fact]
    public void Extra_or_missing_original_ticks_are_not_generated_or_replayed()
    {
        var i = Make(); Reject(i with { Ticks = [] }, "clock");
        Reject(i with { Ticks = [.. i.Ticks, Tick(T.TickRole.AttributionDecay, 3)] }, "clock");
    }

    [Fact]
    public void Timer_TLS_rotation_minimum_wake_and_global_wrap_are_calculated()
    {
        var i = Make(100, 20); i = i with { Scheduler = i.Scheduler with {
            RotationEnabled = true, TlsIndex = uint.MaxValue, QueueCount = 2, GlobalNextWake = 1,
            AccumulatedDelay = ulong.MaxValue, EnqueueCount = ulong.MaxValue, PendingSleepMilliseconds = 5,
            Queues = [new(0, 1, []), new(1, 10, [])] } };
        var r = Complete(i); var s = r.Calculated!.Scheduler;
        Assert.Equal(0u, s.TlsIndex); Assert.Equal(1999UL, s.AccumulatedDelay); Assert.Equal(0UL, s.EnqueueCount);
        Assert.Equal(0u, s.PendingSleepMilliseconds); Assert.Equal(1u, s.GlobalNextWake);
        Assert.Equal(1u, s.Queues[0].MinimumWake); Assert.Single(s.Queues[0].Heap); Assert.Empty(s.Queues[1].Heap);
        Assert.Contains(r.Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.TimerSleepRequest && t.Before == 5);
        Assert.DoesNotContain(r.Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.TimerWakeRequest);
    }

    [Fact]
    public void Timer_due_zero_can_return_zero_after_successful_enqueue()
    {
        var i = Make(100, 20); var ticks = i.Ticks.ToArray(); ticks[2] = Tick(T.TickRole.TimerDue, unchecked(0u - 2000u));
        var r = Complete(i with { Ticks = ticks }); var request = Assert.Single(r.Events.OfType<T.TimerRequest>());
        Assert.True(request.Enqueued); Assert.Equal(0u, request.NativeReturnValue);
        Assert.Equal(1, r.Calculated!.ReceiverReferences.PendingTimers10);
    }

    [Fact]
    public void Timer_slow_log_uses_strict_unsigned_wrapped_first_plus_1000_comparison()
    {
        var i = Make(100, 20); var ticks = i.Ticks.ToArray(); ticks[1] = Tick(T.TickRole.TimerEntry, uint.MaxValue);
        ticks[3] = Tick(T.TickRole.TimerExit, 1000);
        Assert.Contains(Complete(i with { Ticks = ticks }).Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.TimerSlowLog);
        ticks[3] = Tick(T.TickRole.TimerExit, 999);
        Assert.DoesNotContain(Complete(i with { Ticks = ticks }).Events.OfType<T.Trace>(), t => t.Kind == T.TraceKind.TimerSlowLog);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Unsafe_references_timer_overflow_and_missing_queue_reject(int variant)
    {
        var i = Make(100, 20);
        i = variant switch {
            0 => i with { Attacker = i.Attacker with { References = i.Attacker.References with { History14 = 1 } } },
            1 => i with { Attacker = i.Attacker with { References = i.Attacker.References with { Count8 = int.MaxValue } } },
            2 => i with { Receiver = i.Receiver with { References = i.Receiver.References with { PendingTimers10 = int.MaxValue } } },
            _ => i with { Scheduler = i.Scheduler with { Queues = [] } } };
        Reject(i, variant < 2 ? "reference" : "timer");
    }

    [Fact]
    public void Timer_bad_heap_and_zero_rotation_divisor_reject()
    {
        var i = Make(100, 20);
        Reject(i with { Scheduler = i.Scheduler with { RotationEnabled = true, QueueCount = 0 } }, "timer");
        Reject(i with { Scheduler = i.Scheduler with { Queues = [new(0, 1,
            [new(100, 33, 3, 0, 1), new(99, 44, 3, 0, 2)])] } }, "timer");
    }

    [Fact]
    public void Open_transport_error_is_normal_return_not_an_HP_or_final_buffer_state_answer()
    {
        var i = Make(); i = i with { Transport = new([i.Transport.Sockets[0] with {
            OpenOutcome = T.OpenSendOutcome.NormalSendError }], E) };
        var r = Complete(i); Assert.Equal(80, r.Calculated!.Receiver.Hp778); Assert.True(r.OwnerReturn);
        foreach (var sync in r.Events.OfType<T.Sync>())
        {
            Assert.Equal(1, sync.Transport.SendSyncReturn); Assert.Equal(T.TransportDisposition.SendError, sync.Transport.Disposition);
            Assert.Null(sync.Transport.BufferReleaseInvoked); Assert.Null(sync.Transport.SocketReferencesAfter);
            Assert.Equal(1, sync.Transport.SendSyncReferenceAdds); Assert.Equal(1, sync.Transport.SendSyncReferenceReleases);
        }
    }

    [Fact]
    public void Source_flags_and_existing_damage_addition_preserve_native_widths()
    {
        var i = Make(); i = i with { Attacker = i.Attacker with { Template = i.Attacker.Template with {
            SourceType354 = 2, SourceFlag3ec = 1, SourceFlag3ed = 2 } } };
        var r = Complete(i); Assert.Equal(0x10101u, Assert.Single(r.Calculated!.Attribution.Entries).Flags);
        Assert.Equal(A, r.Calculated.Attribution.LastSpecialSource6c);
        // Negative existing int32 damage is not capped or silently normalized.
        i = Make(int.MaxValue, hp: 20, noKill: true);
        i = i with { Attribution = new(1_000_000, 0, [new(A, A, 0, 0, 0, 2, 5, int.MinValue, 0, 1_000_000, 0)], E),
            Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.AttributionDecay, 1_000_000)] };
        Assert.Equal(unchecked(int.MinValue + 20), Assert.Single(Complete(i).Calculated!.Attribution.Entries).Damage);
        i = Make(int.MaxValue, hp: int.MaxValue, noKill: true);
        i = i with { Receiver = i.Receiver with { State = i.Receiver.State with { MaxHp774 = int.MaxValue } },
            Attribution = new(1_000_000, 0, [new(A, A, 0, 0, 0, 2, 5, 0x4fffff, 0, 1_000_000, 0)], E),
            Ticks = [Tick(T.TickRole.AttributionOuter, 1_000_000), Tick(T.TickRole.AttributionDecay, 1_000_000)] };
        Assert.Equal(unchecked(0x4fffff + int.MaxValue), Assert.Single(Complete(i).Calculated!.Attribution.Entries).Damage);
    }

    [Fact]
    public void Nonfull_HP_template_alert1_uses_NPC_180_second_attribution_TTL()
    {
        var i = Make(5, 20); i = i with { Receiver = i.Receiver with {
            Template = i.Receiver.Template with { RaceAlert3ff = 1 } },
            Attribution = new(0, 0, [new(A, A, 0, 0, 0, 2, 5, 6000000, 11, 0, 0)], E),
            Ticks = [Tick(T.TickRole.AttributionOuter, 180000), Tick(T.TickRole.AttributionDecay, 180000)] };
        var r = Complete(i); var h = Assert.Single(r.Events.OfType<T.HistoryUpdate>());
        Assert.False(h.Cleared); Assert.Equal(0, h.DecayedDamage);
        var entry = Assert.Single(r.Calculated!.Attribution.Entries);
        Assert.Equal(5, entry.Damage); Assert.Equal(180000u, entry.FirstTick);
    }

    [Fact]
    public void Collections_are_owned_read_only_snapshots_including_unchanged_maps_and_heaps()
    {
        var i = Make(0); var map = new List<T.AttributionEntry> { new(44, 44, 0, 0, 0, 2, 5, 1, 0, 0, 0) };
        var heap = new List<T.TimerEntry> { new(50, 44, 3, 0, 9) };
        var queues = new List<T.TimerQueue> { new(0, 50, heap) };
        i = i with { Attribution = i.Attribution with { Entries = map }, Scheduler = i.Scheduler with { Queues = queues } };
        var r = Complete(i); map.Clear(); heap.Clear(); queues.Clear();
        Assert.Single(r.Calculated!.Attribution.Entries); Assert.Single(r.Calculated.Scheduler.Queues);
        Assert.Single(r.Calculated.Scheduler.Queues[0].Heap);
        Assert.Throws<NotSupportedException>(() => ((IList<T.AttributionEntry>)r.Calculated.Attribution.Entries).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<T.TimerEntry>)r.Calculated.Scheduler.Queues[0].Heap).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<T.OwnerEvent>)r.Events).Clear());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PhysicalSkillAdditionDoesNotLoosenTheOriginalNormalEntry(int mode)
    {
        T.Input i=Make();
        i=mode switch{
            0=>i with {Call=i.Call with {Skill18=16556}},
            1=>i with {Receiver=i.Receiver with {State=i.Receiver.State with {Pending728=1}}},
            _=>i with {Call=i.Call with {Cell=i.Call.Cell with {Storage=T.CellStorage.PhysicalSkillCompletionApplicationCell}}}};
        Reject(i,mode switch{0=>"invocation",1=>"life",_=>"alias"});
    }
    [Fact]
    public void PhysicalSkillProofHasNoPublicConstructorAndNullCannotBypassIt()
    {
        Type type=typeof(RetailNpcPhysicalSkillRecordCompletion.OwnerCall);
        Assert.DoesNotContain(type.GetConstructors(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance),x=>!x.IsPrivate);
        Assert.Throws<ArgumentNullException>(()=>T.EvaluatePhysicalSkill(Make(),null!));
        Assert.DoesNotContain(typeof(RetailNpcPhysicalSkillRecordCompletion.CoreGuards).GetProperties(),x=>x.Name=="ActionInfo");
    }
    [Fact]
    public void DistinctActualSkillEntryCalculatesNonzeroMetadataAndLatePrefixWithoutChangingNormalFailureContract()
    {
        var i=RetailNpcPhysicalSkillRecordCompletionTests.Make();
        var good=RetailNpcPhysicalSkillRecordCompletion.EvaluatePhysicalBegin(i);
        Assert.Equal(RetailNpcPhysicalSkillRecordCompletion.Completion.CompleteImmediateInvocation,good.Status);
        Assert.Equal(0u,Assert.Single(good.Primary!.Events.OfType<T.WorldHp>()).Skill); // Native Context08=0 clears this auxiliary skill field.
        Assert.Equal(16556,System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(good.Packet!.Bytes.AsSpan(31,2)));
        Assert.Equal(346,good.Primary.ApplicationCell);Assert.Null(good.Primary.FinalHpAfterDeferredLife);
        var guard=i.Primary.Guards with {Systems12cc=1};
        var late=RetailNpcPhysicalSkillRecordCompletion.EvaluatePhysicalBegin(i with {Primary=i.Primary with {Guards=guard}});
        Assert.Equal(RetailNpcPhysicalSkillRecordCompletion.Completion.IncompleteReachedChild,late.Status);
        Assert.Null(late.Primary);Assert.Null(late.Packet);Assert.NotNull(late.Known!.IncompleteOwnerState);
        Assert.Equal(654,late.Known.IncompleteOwnerState.Receiver.Hp778);
        Assert.Equal(346,late.Known.KnownOwnerApplicationCell);
        Assert.Contains(late.Known.OwnerEvents,x=>x is T.Trace t&&t.Kind==T.TraceKind.HpAtomic);
        Assert.Null(late.FinalHpAfterDeferredLife);
        T.Input normal=Make();Reject(normal with {Guards=normal.Guards with {Systems12cc=1}},"system-effect");
    }
}
