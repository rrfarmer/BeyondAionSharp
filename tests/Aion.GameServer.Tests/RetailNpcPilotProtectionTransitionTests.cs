using Aion.GameServer.Controllers.Attack.RetailNpc;
using T = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPilotProtectionTransition;
using S = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPilotProtectionTransition.Stage;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPilotProtectionTransition.Presence;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPilotProtectionTransition.ManagerPresence;

namespace Aion.GameServer.Tests;

/// <summary>Chosen static/instruction-derived fixtures, not actual actors or native-oracle outcomes.</summary>
public sealed class RetailNpcPilotProtectionTransitionTests
{
    private const string E = "chosen-static-fixture:no-runtime-or-native-oracle";
    private const string ShieldSpan = "3900fc5afa4cf949c4a81fc0dafabf485e935768a623e601c366b20961e7c1d4";
    private const string ReflectSpan = "8d278399cd188dafbd2307e0d625580b9c3349994129f4845037e873b677c35e";
    private static T.Context Context(int direction = 0, int attackerNpc = 212876, int defenderNpc = 212833)
    {
        var a = direction == 1 ? RetailNpcNormalActorKind.Player : RetailNpcNormalActorKind.OrdinaryOwnerlessNpc;
        var d = direction == 2 ? RetailNpcNormalActorKind.Player : RetailNpcNormalActorKind.OrdinaryOwnerlessNpc;
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256, a, d,
            a == RetailNpcNormalActorKind.Player ? 0 : attackerNpc, d == RetailNpcNormalActorKind.Player ? 0 : defenderNpc);
        return new(scope, new(RetailNpcNormalScope.MainBinarySha256, T.Dataset, T.Region, "static-action", 11, 22, "a-life", "d-life", E), E);
    }
    private static T.Entry Entry(int direction = 0, T.CallKind kind = T.CallKind.NormalBase, int cls = 1, int damage = 100) =>
        new(Context(direction), kind, cls, damage, 0, 0, 0, M.Present, "manager", "epoch0", 0, true, true, E);
    private static T.Stamp Stamp(T.Transition s) => new(s.Input.Context.Source, s.ManagerIdentity, s.Epoch, s.NextOrdinal, E, true);
    private sealed record Fixture(bool Shield = true, bool Reflect = true, int Budget = 20000,
        int Percent = 73, int ShieldChance = 1000, int ShieldDraw = 1000, int ReflectChance = 1000,
        int ReflectDraw = 1000, bool Maximum = true, bool Minimum = false, int FirstOverride = 0,
        int SecondOverride = 0, bool BoundPresent = false, int NpcType = 1);
    private static T.Transition Drive(T.Transition state, Fixture? fixture = null, S? until = null)
    {
        var f = fixture ?? new();
        for (int k = 0; k < 50; k++)
        {
            if (state.Pending == until || state.Pending is S.Complete or S.Unsupported or S.OriginalRemoval) return state;
            var stamp = Stamp(state); uint flags = (f.Shield ? 0x40u : 0) | (f.Reflect ? 0x30u : 0);
            T.OriginalRead read = state.Pending switch
            {
                S.InitialFlags => new T.FlagsRead(stamp, flags, P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent),
                S.ShieldDraw => new T.DrawRead(stamp, 1, 1000, f.ShieldDraw),
                S.ShieldEntry => new T.ShieldRead(stamp, new(f.ShieldChance, f.Percent, 1)),
                S.ShieldBudget => new T.BudgetRead(stamp, f.Budget),
                S.BoundSkill => new T.BoundRead(stamp, new(f.BoundPresent ? M.Present : M.Absent, f.BoundPresent ? "original-skill" : null, f.BoundPresent ? 42 : null, E)),
                S.ShieldOutputFlags => new T.OutputFlagsRead(stamp, state.OutputFlags),
                S.MpFlags => new T.FlagsRead(stamp, flags, P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent),
                S.ReflectionGate => new T.GateRead(stamp, flags, state.Input.Context.Scope.Attacker == RetailNpcNormalActorKind.Player ? null : f.NpcType),
                S.Radii => new T.RadiiRead(stamp, 0, 0x42800000),
                S.MaximumGeometry => new T.GeometryRead(stamp, 0x42800000, f.Maximum),
                S.MinimumGeometry => new T.GeometryRead(stamp, 0, f.Minimum),
                S.ReflectionDraw => new T.DrawRead(stamp, 1, 1000, f.ReflectDraw),
                S.ReflectionChance => new T.ChanceRead(stamp, f.ReflectChance),
                S.ReflectionValues => new T.ValuesRead(stamp, 25, 60, 16699),
                S.OverrideFirst => new T.OverrideRead(stamp, 188, f.FirstOverride),
                S.OverrideSecond => new T.OverrideRead(stamp, 188, f.SecondOverride),
                S.LaterFlags => new T.FlagsRead(stamp, 0, P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent),
                _ => throw new InvalidOperationException("Unexpected fixture boundary")
            };
            state = T.Advance(state, read);
        }
        throw new InvalidOperationException("Fixture transition did not terminate");
    }
    private static T.ManagerSnapshot Post(T.Transition s, uint flags = 0x30, string epoch = "epoch1") =>
        new(s.ManagerIdentity, epoch, flags, new(0, 73, 1), new(0, 73, 1), 0,
            new(16699, 25, 60, 0, 0x42800000, 1000), new(16699, 25, 60, 0, 0x42800000, 1000),
            new(M.Present, "original-skill", 42, E), P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent, 22, "d-life", E);
    private static T.RemovalReceipt Removal(T.Transition s, T.ManagerSnapshot? post = null, T.CallbackObservation[]? callbacks = null) =>
        new(Stamp(s), s.Removal!, 0, true, callbacks ?? new[] { new T.CallbackObservation(1, "original EndSkillEffect and recompute", E) }, post ?? Post(s),
            new(11, "a-life", s.Input.Context.Scope.Attacker, s.Input.Context.Scope.AttackerNpcTemplateId, E),
            new(22, "d-life", s.Input.Context.Scope.Defender, s.Input.Context.Scope.DefenderNpcTemplateId, E), E);
    private static T.Activation Activation(int skill = 16522, int level = 24, int npc = 212876) =>
        new(Context(attackerNpc: npc), T.Actor.Attacker, T.Actor.Attacker, skill, skill == 16522 ? 1 : 0,
            level, M.Present, 0x80000000, skill == 16699 ? 0x80000010u : null,
            skill == 16522 ? ShieldSpan : ReflectSpan, "original-skill", 42, true, E);

    [Theory]
    [InlineData(22, 212777, 69)]
    [InlineData(23, 212778, 71)]
    [InlineData(23, 212780, 71)]
    [InlineData(24, 212876, 73)]
    public void Pilot_shield_activation_preserves_native_store_width_order_and_binding(int level, int npc, int expected)
    {
        var p = T.PrepareActivation(Activation(level: level, npc: npc));
        Assert.Equal(expected, p.PreparedHit); Assert.Equal(20000, p.PreparedBudget);
        Assert.Equal(new[] { 0x2c88, 0x2c90, 0x2c94, 0x2c9c, 0x2b18, 0x2cac, 0x127c }, p.Writes.Select(w => w.Offset));
        Assert.Equal(new[] { 8, 4, 8, 4, 4, 4, 4 }, p.Writes.Select(w => w.Width));
        Assert.Equal(1000ul | ((ulong)(uint)expected << 32), p.Writes[0].Bits);
        Assert.Equal("original-skill", p.BoundSkillWrite!.SkillKey);
        Assert.Equal(8, p.BoundSkillWrite.OrderAfterSevenScalarWrites); Assert.Equal(8, p.BoundSkillWrite.Width);
        Assert.False(p.AppliedToActor);
    }
    [Theory]
    [InlineData(212764)] [InlineData(212833)]
    public void Reflector_activation_preserves_separate_groups_and_real_second_flag_read(int npc)
    {
        var p = T.PrepareActivation(Activation(16699, 22, npc) with { OriginalSecondGroupFlags = 0x40000000 });
        Assert.Equal(60, p.PreparedHit); Assert.Equal(25, p.PreparedPercent);
        Assert.Equal(0u, p.MinimumBits); Assert.Equal(0x42800000u, p.MaximumBits);
        Assert.Equal(0x80000010ul, p.Writes[0].Bits); Assert.Equal(0x40000020ul, p.Writes[4].Bits);
        Assert.Equal(new[] { 4, 8, 8, 8, 4, 8, 8, 8 }, p.Writes.Select(w => w.Width));
        Assert.Null(p.BoundSkillWrite);
    }
    [Fact]
    public void Manager_null_activation_and_end_have_no_invented_writes()
    {
        var p = T.PrepareActivation(Activation() with { Manager = M.Absent });
        Assert.Empty(p.Writes); Assert.Null(p.BoundSkillWrite); Assert.Null(p.PreparedBudget);
        var end = T.PrepareEnd(new(Context(), T.Actor.Defender, 16522, 1, M.Absent, 0, null, ShieldSpan, 12, true, E));
        Assert.Empty(end.Writes);
    }
    [Fact]
    public void Shield_end_clears_only_budget_chances_and_flag_not_mode_or_pointer()
    {
        var p = T.PrepareEnd(new(Context(), T.Actor.Defender, 16522, 1, M.Present, 0x70, null, ShieldSpan, 12, true, E));
        Assert.Equal(new[] { 0x2cac, 0x2c88, 0x2c94, 0x2b18 }, p.Writes.Select(x => x.Offset));
        Assert.Equal(0x30ul, p.Writes[^1].Bits); Assert.Null(p.BoundSkillWrite);
        Assert.DoesNotContain(p.Writes, x => x.Offset is 0x188 or 0x2c8c or 0x2c90);
    }
    [Fact]
    public void Reflector_end_preserves_separate_original_second_group_flags_and_qword_clears()
    {
        var p = T.PrepareEnd(new(Context(), T.Actor.Defender, 16699, 0, M.Present, 0x70, 0x80000030, ReflectSpan, 0, true, E));
        Assert.Equal(0x60ul, p.Writes[0].Bits); Assert.Equal(0x80000010ul, p.Writes[4].Bits);
        Assert.Equal(new[] { 0x2c34, 0x2c3c, 0x2c44, 0x2c4c, 0x2c54, 0x2c5c }, p.Writes.Where(x => x.Width == 8).Select(x => x.Offset));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Activation_rejects_unverified_binding_source_or_dispatch(int bad)
    {
        var a = Activation();
        a = bad switch { 0 => a with { EffectIndex = 0 }, 1 => a with { SignedLevel = 25 }, 2 => a with { SkillRawSpanHash = "wrong" },
            3 => a with { OriginalDispatcherAdmitted = false }, 4 => a with { Manager = M.Unknown }, _ => Activation(16699, 22, 212833) with { OriginalSecondGroupFlags = null } };
        Assert.Throws<ArgumentException>(() => T.PrepareActivation(a));
    }

    [Theory]
    [InlineData(100, 69, 69)] [InlineData(100, 71, 71)] [InlineData(100, 73, 73)]
    [InlineData(1, 73, 0)] [InlineData(137, 73, 100)] [InlineData(30000000, 73, 0)]
    public void Shield_percent_uses_wrapped_product_float_division_and_truncation(int damage, int percent, int expected) =>
        Assert.Equal(expected, T.ShieldAmount(damage, percent, 1));
    [Theory]
    [InlineData(1, 60)] [InlineData(3, 60)] [InlineData(4, 61)] [InlineData(99, 84)]
    [InlineData(100, 85)] [InlineData(101, 85)] [InlineData(137, 94)] [InlineData(400, 160)]
    [InlineData(16777217, 4194364)] [InlineData(2147483647, 536870976)]
    public void Reflector_sum_matches_independent_fraction_vectors(int incoming, int expected) =>
        Assert.Equal(expected, T.ReflectionAmount(incoming, 25, 60));
    [Fact]
    public void General_flat_arithmetic_is_separate_from_pilot_activation_and_record_admission()
    {
        Assert.Equal(100, T.ShieldAmount(100, 200, 0));
        var s = Drive(T.Begin(Entry()), until: S.ShieldEntry);
        var rejected = T.Advance(s, new T.ShieldRead(Stamp(s), new(1000, 100, 0)));
        Assert.Equal(S.Unsupported, rejected.Pending);
        Assert.Contains("nonpilot", rejected.UnsupportedReason);
    }
    [Fact]
    public void Scalar_rounding_points_cannot_be_replaced_with_one_fused_operation()
    {
        // Independent Fraction RN-even: ratio3f9d70a4; product4b0fa89f;
        // separate ADDSS ->42c80000=100, one-round FMA ->42c7f3b6 truncates99.
        // General native scalar vector, not pilot activation values or an observed actor.
        Assert.Equal(100, T.ReflectionAmount(7654321, 123, -9414715));
        Assert.Equal(0x3f9d70a4, BitConverter.SingleToInt32Bits(123f / 100f));
        Assert.Equal(0x4b800000, BitConverter.SingleToInt32Bits((float)16777217));
    }
    [Theory]
    [InlineData(-1, 25, 60)] [InlineData(2147483647, 1000, 60)]
    public void Unsupported_negative_or_nonrepresentable_arithmetic_is_visible(int damage, int percent, int flat)
    {
        if (damage < 0) Assert.Throws<ArgumentOutOfRangeException>(() => T.ReflectionAmount(damage, percent, flat));
        else Assert.Throws<ArithmeticException>(() => T.ReflectionAmount(damage, percent, flat));
    }

    public static IEnumerable<object[]> DirectionsAndCalls()
    {
        foreach (int direction in new[] { 0, 1, 2 })
            foreach (var pair in new[] { (T.CallKind.NormalBase, 1), (T.CallKind.NormalBase, 2), (T.CallKind.PhysicalSkill, 1), (T.CallKind.MagicalSkill, 2) })
                yield return new object[] { direction, (int)pair.Item1, pair.Item2 };
    }
    [Theory]
    [MemberData(nameof(DirectionsAndCalls))]
    public void Both_actor_directions_and_damage_classes_keep_latched_damage_and_draw_order(int direction, int kind, int cls)
    {
        var s = Drive(T.Begin(Entry(direction, (T.CallKind)kind, cls)));
        Assert.Equal(S.Complete, s.Pending); Assert.Equal(27, s.RemainingDamage); Assert.Equal(19927, s.Budget);
        Assert.Equal(100, s.IncomingBeforeShield); Assert.Equal(85, s.ReflectedAmount); Assert.Equal(16699u, s.ReflectedSkill);
        Assert.Equal((byte)3, s.OutputFlags);
        Assert.Equal(new[] { S.ShieldDraw, S.ReflectionDraw }, s.Reads.Where(x => x.Stage is S.ShieldDraw or S.ReflectionDraw).Select(x => x.Stage));
        Assert.Null(s.PrimaryApplicationCell); Assert.Null(s.FinalHpAfterCallbacks);
    }
    [Theory]
    [InlineData(999, 1000, false)] [InlineData(999, 999, true)] [InlineData(1000, 1000, true)]
    public void Shield_original_chance_equality_and_always_draw(int chance, int draw, bool success)
    {
        var s = Drive(T.Begin(Entry()), new(ShieldChance: chance, ShieldDraw: draw, Reflect: false));
        Assert.Equal(success ? 27 : 100, s.RemainingDamage);
        Assert.Single(s.Reads, x => x.Stage == S.ShieldDraw);
        Assert.Equal(success ? (byte)2 : (byte)0, s.OutputFlags);
    }
    [Fact]
    public void Successful_zero_absorption_still_marks_flag2()
    {
        var s = Drive(T.Begin(Entry(damage: 1)), new(Reflect: false));
        Assert.Equal(1, s.RemainingDamage); Assert.Equal(20000, s.Budget); Assert.Equal((byte)2, s.OutputFlags);
    }
    [Theory]
    [InlineData(73, 27)] [InlineData(72, 28)] [InlineData(0, 100)]
    public void Budget_exhaustion_null_bound_keeps_damage_and_does_not_invent_removal(int budget, int remaining)
    {
        var s = Drive(T.Begin(Entry()), new(Budget: budget, Reflect: false));
        Assert.Equal(remaining, s.RemainingDamage); Assert.Equal(0, s.Budget);
        Assert.Empty(s.Removals); Assert.False(s.RetainsOriginalSideEffects); Assert.Equal((byte)2, s.OutputFlags);
    }
    [Fact]
    public void Class1_and_class2_share_budget_across_original_record_receipts()
    {
        var first = Drive(T.Begin(Entry()), new(Budget: 100, Reflect: false));
        var second = Drive(T.Begin(Entry(cls: 2)), new(Budget: first.Budget!.Value, Reflect: false));
        Assert.Equal(27, first.RemainingDamage); Assert.Equal(73, second.RemainingDamage); Assert.Equal(0, second.Budget);
    }
    [Fact]
    public void Original_removal_precedes_flag2_and_does_not_clear_bound_pointer_or_relatch_input()
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true));
        Assert.Equal(S.OriginalRemoval, s.Pending); Assert.Equal(0, s.OutputFlags); Assert.Equal(100, s.IncomingBeforeShield);
        Assert.Equal(12, s.Removal!.Reason); Assert.Equal(1, s.Removal.Param4);
        var resumed = T.ResumeRemoval(s, Removal(s));
        Assert.Equal(S.ShieldOutputFlags, resumed.Pending); Assert.Equal("epoch1", resumed.Epoch);
        Assert.Equal(M.Present, resumed.LastPoststate!.BoundSkill.Presence);
        var done = Drive(resumed, new(Shield: false));
        Assert.Equal(27, done.RemainingDamage); Assert.Equal(85, done.ReflectedAmount); Assert.Equal(100, done.IncomingBeforeShield);
        Assert.True(done.RetainsOriginalSideEffects); Assert.Single(done.Removals);
        Assert.DoesNotContain(done.Operations, x => x.Name.Contains("ClearBound"));
    }
    [Fact]
    public void Post_removal_flag_reads_are_fresh_and_unsupported_preserves_reached_state()
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true));
        var resumed = T.ResumeRemoval(s, Removal(s, Post(s, 0x830) with { MpShield = P.PresentUnsupported }));
        resumed = T.Advance(resumed, new T.OutputFlagsRead(Stamp(resumed), 0x80));
        var stopped = T.Advance(resumed, new T.FlagsRead(Stamp(resumed), 0x830, P.PresentUnsupported, P.VerifiedAbsent, P.VerifiedAbsent));
        Assert.Equal(S.Unsupported, stopped.Pending); Assert.True(stopped.RetainsOriginalSideEffects);
        Assert.Equal(27, stopped.RemainingDamage); Assert.Equal(0, stopped.Budget); Assert.Equal((byte)0x82, stopped.OutputFlags);
        Assert.Single(stopped.Removals); Assert.Null(stopped.FinalHpAfterCallbacks);
    }
    [Fact]
    public void Original_callback_immutable_ownership_and_lifecycle_change_are_preserved()
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true));
        var events = new[] { new T.CallbackObservation(1, "original StatUp End", E) };
        var next = T.ResumeRemoval(s, Removal(s, callbacks: events)); events[0] = new(1, "mutated caller array", E);
        Assert.Equal("original StatUp End", next.Removals[0].Callbacks[0].Event);
        var changed = T.ResumeRemoval(s, Removal(s, Post(s) with { Life = "new-life" }));
        Assert.Equal(S.Unsupported, changed.Pending); Assert.True(changed.RetainsOriginalSideEffects); Assert.Single(changed.Removals);
    }
    [Fact]
    public void Incomplete_original_removal_returns_typed_retained_unsupported_state()
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true));
        var next = T.ResumeRemoval(s, Removal(s) with { CompleteOriginalOutcome = false });
        Assert.Equal(S.Unsupported, next.Pending); Assert.True(next.RetainsOriginalSideEffects);
        Assert.Equal(27, next.RemainingDamage); Assert.Equal(100, next.IncomingBeforeShield);
    }
    [Fact]
    public void Unknown_post_removal_binding_and_stale_epoch_do_not_continue_as_absent()
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true));
        var stopped = T.ResumeRemoval(s, Removal(s, Post(s) with { BoundSkill = new(M.Unknown, null, null, E) }));
        Assert.Equal(S.Unsupported, stopped.Pending); Assert.True(stopped.RetainsOriginalSideEffects); Assert.Single(stopped.Removals);
        var resumed = T.ResumeRemoval(s, Removal(s));
        Assert.Throws<ArgumentException>(() => T.Advance(resumed, new T.OutputFlagsRead(Stamp(resumed) with { Epoch = "epoch0" }, 0)));
        Assert.Same(resumed, T.Advance(resumed));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void Both_post_callback_actor_identities_and_domains_must_remain_verified(int changed)
    {
        var s = Drive(T.Begin(Entry()), new(Budget: 73, BoundPresent: true)); var receipt = Removal(s);
        receipt = changed switch
        {
            0 => receipt with { AttackerAfter = receipt.AttackerAfter with { Life = "new-attacker-life" } },
            1 => receipt with { AttackerAfter = receipt.AttackerAfter with { Kind = RetailNpcNormalActorKind.Unverified } },
            2 => receipt with { DefenderAfter = receipt.DefenderAfter with { Kind = RetailNpcNormalActorKind.OwnedNpc } },
            _ => receipt with { DefenderAfter = receipt.DefenderAfter with { ObjectId = 99 } }
        };
        var stopped = T.ResumeRemoval(s, receipt);
        Assert.Equal(S.Unsupported, stopped.Pending); Assert.True(stopped.RetainsOriginalSideEffects);
        Assert.Single(stopped.Removals); Assert.Equal(27, stopped.RemainingDamage);
    }
    [Fact]
    public void Skip_later_is_after_Shield_and_wrapper_zero_has_no_draw()
    {
        var skipped = Drive(T.Begin(Entry() with { RawSkipLater = 1 }));
        Assert.Equal(27, skipped.RemainingDamage); Assert.DoesNotContain(skipped.Reads, x => x.Stage == S.ReflectionDraw);
        var zero = T.Begin(Entry(damage: 0)); Assert.Equal(S.Complete, zero.Pending); Assert.Empty(zero.Reads); Assert.Null(zero.MpDamage);
        var absent = T.Begin(Entry() with { Manager = M.Absent, ManagerIdentity = null, Epoch = null }); Assert.Equal(S.Complete, absent.Pending);
    }
    [Fact]
    public void Manager_MP_reset_precedes_flags_and_flags_zero_returns_without_a_native_latch_or_draw()
    {
        var s = T.Begin(Entry()); Assert.Equal(0, s.MpDamage);
        Assert.Equal("ResetMpOutputBeforeFlagRead", s.Operations[^1].Name);
        s = T.Advance(s, new T.FlagsRead(Stamp(s), 0, P.Unknown, P.Unknown, P.Unknown));
        Assert.Equal(S.Complete, s.Pending); Assert.DoesNotContain(s.Operations, x => x.Name == "LatchIncomingBeforeShield");
        Assert.DoesNotContain(s.Reads, x => x.Stage is S.ShieldDraw or S.ReflectionDraw);
    }
    [Theory]
    [InlineData(false, false, 0)] [InlineData(true, true, 0)] [InlineData(true, false, 85)]
    public void Geometry_is_max_then_min_including_zero_and_only_then_draw(bool max, bool min, int expected)
    {
        var s = Drive(T.Begin(Entry()), new(Shield: false, Maximum: max, Minimum: min));
        Assert.Equal(expected, s.ReflectedAmount);
        Assert.Equal(max ? 1 : 0, s.Reads.Count(x => x.Stage == S.MinimumGeometry));
        Assert.Equal(max && !min ? 1 : 0, s.Reads.Count(x => x.Stage == S.ReflectionDraw));
    }
    [Theory]
    [InlineData(500, 500, 85)] [InlineData(500, 501, 0)] [InlineData(1000, 1000, 85)]
    public void Reflection_probability_is_inclusive_and_always_consumes_eligible_draw(int chance, int draw, int expected)
    {
        var s = Drive(T.Begin(Entry()), new(Shield: false, ReflectChance: chance, ReflectDraw: draw));
        Assert.Equal(expected, s.ReflectedAmount); Assert.Single(s.Reads, x => x.Stage == S.ReflectionDraw);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void Both_classes_use_original_slot188_twice_and_second_value_wins(int cls)
    {
        var s = Drive(T.Begin(Entry(cls: cls)), new(Shield: false, FirstOverride: 41, SecondOverride: 42));
        Assert.Equal(42, s.ReflectedAmount); Assert.Equal(16699u, s.ReflectedSkill);
        Assert.Single(s.Reads, x => x.Stage == S.OverrideFirst); Assert.Single(s.Reads, x => x.Stage == S.OverrideSecond);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public void Second_override_zero_or_negative_keeps_original_metadata_and_flag(int second)
    {
        var s = Drive(T.Begin(Entry()), new(Shield: false, FirstOverride: 41, SecondOverride: second));
        Assert.Equal(second, s.ReflectedAmount); Assert.Equal(16699u, s.ReflectedSkill); Assert.Equal((byte)1, s.OutputFlags);
    }
    [Fact]
    public void NPC_type64_blocks_before_geometry_and_reflector_does_not_consume_use_count()
    {
        var blocked = Drive(T.Begin(Entry()), new(Shield: false, NpcType: 64));
        Assert.DoesNotContain(blocked.Reads, x => x.Stage == S.MaximumGeometry); Assert.Equal(0, blocked.ReflectedAmount);
        var normal = Drive(T.Begin(Entry()), new(Shield: false));
        Assert.Empty(normal.Removals); Assert.DoesNotContain(normal.Operations, x => x.Name.Contains("Decrement"));
    }
    [Theory]
    [InlineData((int)P.Unknown)] [InlineData((int)P.PresentUnsupported)]
    public void Unknown_or_present_later_protection_is_not_defaulted_absent(int presence)
    {
        var s = Drive(T.Begin(Entry()), until: S.LaterFlags);
        var next = T.Advance(s, new T.FlagsRead(Stamp(s), 0, P.VerifiedAbsent, (P)presence, P.VerifiedAbsent));
        Assert.Equal(S.Unsupported, next.Pending); Assert.Equal(85, next.ReflectedAmount);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void Wrong_original_stamp_and_order_are_rejected(int bad)
    {
        var s = T.Begin(Entry()); var stamp = Stamp(s);
        stamp = bad switch { 0 => stamp with { Ordinal = 2 }, 1 => stamp with { Epoch = "stale" }, 2 => stamp with { ManagerIdentity = "other" },
            3 => stamp with { OriginalOrderedReadInterval = false }, 4 => stamp with { Evidence = "" },
            5 => stamp with { Source = stamp.Source with { ActionId = "other" } }, _ => stamp with { Source = stamp.Source with { DefenderLife = "replaced" } } };
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.FlagsRead(stamp, 0, P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent)));
        Assert.Equal(S.InitialFlags, s.Pending);
    }
    [Fact]
    public void Wrong_read_type_bounds_extra_receipts_and_whole_skill_reflection_reject()
    {
        var s = T.Begin(Entry()); Assert.Throws<ArgumentException>(() => T.Advance(s, new T.DrawRead(Stamp(s), 1, 1000, 1000)));
        s = Drive(s, until: S.ShieldDraw);
        Assert.Throws<ArgumentException>(() => T.Advance(s, new T.DrawRead(Stamp(s), 0, 1000, 1000)));
        var complete = T.Begin(Entry(damage: 0)); Assert.Throws<ArgumentException>(() => T.Advance(complete, new T.OutputFlagsRead(Stamp(complete), 0)));
        var whole = T.Begin(Entry()); whole = T.Advance(whole, new T.FlagsRead(Stamp(whole), 0x1000, P.VerifiedAbsent, P.VerifiedAbsent, P.VerifiedAbsent));
        Assert.Equal(S.Unsupported, whole.Pending);
    }
    [Fact]
    public void Wrong_geometry_bound_slot_and_nonfinite_radius_are_visible()
    {
        var max = Drive(T.Begin(Entry()), until: S.MaximumGeometry);
        Assert.Throws<ArgumentException>(() => T.Advance(max, new T.GeometryRead(Stamp(max), 0, true)));
        var ov = Drive(T.Begin(Entry()), until: S.OverrideFirst);
        Assert.Throws<ArgumentException>(() => T.Advance(ov, new T.OverrideRead(Stamp(ov), 187, 0)));
        var radius = Drive(T.Begin(Entry()), until: S.Radii);
        var bad = T.Advance(radius, new T.RadiiRead(Stamp(radius), 0, 0x7f800000));
        Assert.Equal(S.Unsupported, bad.Pending); Assert.Equal(27, bad.RemainingDamage);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void Entry_rejects_wrong_source_or_unsupported_context(int bad)
    {
        var e = Entry();
        e = bad switch { 0 => e with { Damage = -1 }, 1 => e with { InitialReflectedAmount = 1 }, 2 => e with { Classification = 4 },
            3 => e with { RoundNearestEvenAndNormalReturn = false }, 4 => e with { Context = e.Context with { Source = e.Context.Source with { DatasetHash = "wrong" } } },
            _ => e with { Context = e.Context with { Source = e.Context.Source with { RegionContext = "loaded-region-invented" } } } };
        Assert.Throws<ArgumentException>(() => T.Begin(e));
    }
    [Fact]
    public void Source_gate_rejects_owned_scaled_and_PvP_pairs_before_protection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OwnedNpc, RetailNpcNormalActorKind.Player, 212876, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.ScaledNpc, RetailNpcNormalActorKind.Player, 212876, 0));
        Assert.Throws<ArgumentException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.Player, 0, 0));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Completion_keeps_primary_reflection_attribution_and_HP_owner_boundaries(bool skill)
    {
        var plan = T.DescribeCompletion(new(Context(), skill ? T.CallKind.MagicalSkill : T.CallKind.NormalBase, 2,
            15557, 27, 0, 0, 0, 85, 16699, 0, P.VerifiedAbsent, true, true, E));
        Assert.Equal(27, plan.PrimarySignedInput); Assert.Null(plan.PrimaryApplicationCell); Assert.Null(plan.ReflectedApplicationCell);
        Assert.Null(plan.FinalAttackerHp); Assert.Null(plan.FinalDefenderHp); Assert.False(plan.Executed);
        var reflected = Assert.Single(plan.Ordered, x => x.Operation.StartsWith("ReflectedGetHitDamage"));
        Assert.Equal(T.Actor.Attacker, reflected.Receiver); Assert.Equal(T.Actor.Defender, reflected.Source);
        Assert.Equal(85, reflected.InputDamage); Assert.Equal(852, reflected.RawThreat); Assert.Equal(9, reflected.ContextKind);
        Assert.Equal(0u, reflected.ContextSkillId); Assert.Equal(1u, reflected.ContextFlagC);
        Assert.Equal("original-null", reflected.ReflectionContextPointer0); Assert.Equal(0u, reflected.ReflectionContextField10);
        Assert.Equal(0xc5u, reflected.ReflectionContextField1C); Assert.Equal((ushort)0, reflected.ReflectionContextField20);
        Assert.False(reflected.PreEmissionProtectorReentry); Assert.False(reflected.CastCancelProbabilityDraw);
        Assert.Equal(skill ? 0 : 1, plan.Ordered.Count(x => x.Operation == "TargetSendHP"));
        Assert.True(plan.Ordered.ToList().FindIndex(x => x.Operation.StartsWith("ReflectedGetHitDamage")) > plan.Ordered.ToList().FindIndex(x => x.Operation == "PrimaryGetHPMPDamageAndHeal"));
        Assert.All(plan.Ordered, x => Assert.True(x.OriginalCallbackOutcomeRequired));
    }
    [Fact]
    public void Zero_reflection_skips_HP_dispatch_but_metadata_remains_distinct_and_system_order_is_explicit()
    {
        var p = T.DescribeCompletion(new(Context(), T.CallKind.PhysicalSkill, 1, 15557, 20, 0, 30, 0, 0, 16699, 3, P.PresentUnsupported, true, true, E));
        Assert.Equal(-10, p.PrimarySignedInput); Assert.Equal(16699u, p.RecordReflectionSkillId);
        Assert.DoesNotContain(p.Ordered, x => x.Operation.StartsWith("ReflectedGetHitDamage"));
        Assert.Equal(7, p.Ordered[1].ContextKind); Assert.Equal(15557u, p.Ordered[1].ContextSkillId);
        Assert.True(p.Ordered.ToList().FindIndex(x => x.Operation == "OriginalOptionalSystemSkillOutcome") < p.Ordered.ToList().FindIndex(x => x.Operation == "OriginalProtectedRecipientResolutionAndDamage"));
    }
}
