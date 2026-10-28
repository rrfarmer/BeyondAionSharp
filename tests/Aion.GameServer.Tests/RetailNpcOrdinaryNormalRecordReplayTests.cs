using Aion.GameServer.Controllers.Attack.RetailNpc;
using Replay = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcOrdinaryNormalRecordReplay;
using Draw = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcOrdinaryNormalRecordReplay.DrawStage;
using Feature = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcOrdinaryNormalRecordReplay.UnsupportedFeature;
using State = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcOrdinaryNormalRecordReplay.FeatureState;

namespace Aion.GameServer.Tests;

/// <summary>
/// Chosen static/instruction-derived cases, not captured native outcomes or a pilot/player
/// profile claim. Expected float bits are independently pinned by exact Fraction arithmetic
/// in phase3/normal-replay-fixtures. No actor, RNG, scheduler, packet or HP owner is run.
/// </summary>
public sealed class RetailNpcOrdinaryNormalRecordReplayTests
{
    private const string Evidence = "chosen-static-fixture:not-a-loaded-actor-or-native-oracle";
    private static Replay.AbsenceReceipt[] Absences() => Enum.GetValues<Feature>()
        .Select(f => new Replay.AbsenceReceipt(f, State.VerifiedAbsent, Evidence)).ToArray();
    private static Replay.EffectiveAction Baseline(bool playerDefender = false)
    {
        var scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
            playerDefender ? RetailNpcNormalActorKind.Player : RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
            212776, playerDefender ? 0 : 212765);
        return new(scope,
            new(RetailNpcNormalScope.MainBinarySha256,
                "33dfd001439620a102ccfca9b30765578efa88acb6f561f8c116ce8d98873335",
                "frozen-USA-static-source; loaded-context-unavailable", "static-action", 11, 22,
                "static-attacker-generation", "static-defender-generation", Evidence),
            new(212776, 0, 0, 1, 0, 200, 200, 1f, 0f, 0, 0, 0, Evidence, 1, 0, 0, Evidence),
            new(30, 1000, 1000, 0, 0, 0, 0, 0, Evidence),
            new(30, 1000, 0, 0, 0, -1300, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Evidence),
            new(0, 0, 0, 0, 0, 0, 0, Evidence), new(true, false, 0, Evidence),
            new(0, playerDefender ? 1 : 0, 0, 0, Evidence), true, Evidence, Absences());
    }
    private static Replay.OriginalInvocation Call(Draw stage, int result = 1000,
        int first = 1, int second = 1000, bool adapter = true) =>
        new(RetailNpcNormalScope.MainBinarySha256, "static-action", stage, first, second, result, adapter, Evidence);
    private static Replay.OriginalInvocation[] Calls(Replay.EffectiveAction x, params (Draw Stage, int Result)[] later) =>
        new[] { Call(Draw.Base, x.SelectedProfile.MaximumDamage, x.SelectedProfile.MinimumDamage,
            x.SelectedProfile.MaximumDamage, x.SelectedProfile.MinimumDamage != x.SelectedProfile.MaximumDamage) }
            .Concat(later.Select(r => Call(r.Stage, r.Result))).ToArray();
    private static Replay.Result Normal(Replay.EffectiveAction x) => Replay.Replay(x,
        Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1000)));
    private static Replay.PrimaryCommitSnapshot Commit(Replay.Result result, int hp = 1000) =>
        new(result.Source, hp, true, 0, 0, State.VerifiedAbsent, true, Absences(), Evidence);
    private static void Bits(uint expected, float actual) => Assert.Equal(expected, BitConverter.SingleToUInt32Bits(actual));

    [Fact]
    public void NeutralPhysicalRecordHasExactStatusAndOriginalWrapperOrder()
    {
        var r = Normal(Baseline());
        Assert.Equal(200, r.RecordDamage); Bits(0x43480000, r.PreprotectorFloat);
        Assert.Equal((byte)10, r.BaseStatus); Assert.Equal((byte)0, r.CriticalStatus);
        Assert.Equal(0u, r.AggregateFlags); Assert.False(r.TerminalMiss);
        Assert.Equal(new[] { Draw.Base, Draw.Dodge, Draw.MainCritical }, r.OriginalInvocations.Select(x => x.Stage));
        Assert.Empty(r.CounterIntents); Assert.Null(r.FinalHpAfterCallbacks);
        Assert.Equal(2, r.RequiredZeroOverrideConsultations);
    }
    [Fact]
    public void ArmorRunsBeforeTheActiveAndCriticalStages()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600 } };
        var r = Normal(x);
        Assert.Equal(140, r.RecordDamage); Bits(0x430c0000, r.PreprotectorFloat);
        Assert.Equal(60f, r.Arithmetic.PhysicalDefense!.Value.ArmorTerm);
        Assert.Null(r.Arithmetic.ElementalDefense);
    }
    [Theory]
    [InlineData(false, 0x433d0000u, 189)]
    [InlineData(true, 0x42fc0000u, 126)]
    public void ElementalDamageAndPhysicalActiveGroupRemainIndependent(bool elemental, uint bits, int damage)
    {
        var x = Baseline();
        x = x with
        {
            SelectedProfile = x.SelectedProfile with { DamageElement = elemental ? 1 : 0, PhysicalFlag8e4 = elemental ? 0u : 1u },
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500, MagicalCritical14c0 = 500 },
            EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600, MagicalDefense = 600, Parry14b4 = 1400 },
            NativeEligibility = x.NativeEligibility with { ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Parry, 400), (Draw.MainCritical, 500)));
        Assert.Equal(damage, r.RecordDamage); Bits(bits, r.PreprotectorFloat);
        Bits(0x42fc0000, r.Arithmetic.AfterActiveDefense);
        Assert.Equal((byte)2, r.BaseStatus); Assert.Equal((byte)12, r.CriticalStatus);
        Assert.Equal(64u, r.AggregateFlags); Assert.Equal(64u, Assert.Single(r.CounterIntents).Mask);
        Assert.Null(r.CounterIntents[0].OriginalTick); Assert.Null(r.CounterIntents[0].Deadline);
        Assert.Null(r.Arithmetic.MagicalResistThreshold);
    }
    [Fact]
    public void SuccessfulNpcBlockSuppressesParryButStillComposesCritical()
    {
        var x = Baseline(); x = x with
        {
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500 },
            EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600, Block14b6 = 1500 },
            NativeEligibility = x.NativeEligibility with { BlockSlot129Result = 1500, ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Block, 500), (Draw.MainCritical, 500)));
        Assert.Equal(189, r.RecordDamage); Assert.Equal((byte)4, r.BaseStatus); Assert.Equal((byte)12, r.CriticalStatus);
        Assert.Equal(32u, r.AggregateFlags); Assert.Null(r.Arithmetic.ParryThreshold);
        Assert.Equal(new[] { Draw.Base, Draw.Dodge, Draw.Block, Draw.MainCritical }, r.OriginalInvocations.Select(c => c.Stage));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MagicalGroupUsesItsOwnResistStageIndependentlyFromSelectedDamage(bool elemental)
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with
            { WeaponGroup8ec = 7, DamageElement = elemental ? 1 : 0, PhysicalFlag8e4 = elemental ? 0u : 1u } };
        var r = Replay.Replay(x, Calls(x, (Draw.MagicalResist, 1000), (Draw.MainCritical, 1000)));
        Assert.Equal(200, r.RecordDamage); Assert.Null(r.Arithmetic.DodgeThreshold);
        Assert.Equal(0, r.Arithmetic.MagicalResistThreshold);
    }
    [Theory]
    [InlineData(false, 0, 128u)]
    [InlineData(true, 6, 256u)]
    public void TerminalStatusStopsBeforeCritical(bool resist, int status, uint flags)
    {
        var x = Baseline(); x = x with
        {
            SelectedProfile = x.SelectedProfile with { WeaponGroup8ec = resist ? 7 : 0 },
            EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 1300, MagicalResistance149c = 500 }
        };
        var r = Replay.Replay(x, Calls(x, (resist ? Draw.MagicalResist : Draw.Dodge, resist ? 500 : 300)));
        Assert.True(r.TerminalMiss); Assert.Equal(0, r.RecordDamage); Bits(0, r.PreprotectorFloat);
        Assert.Equal((byte)status, r.BaseStatus); Assert.Equal((byte)0, r.CriticalStatus);
        Assert.Equal(flags, r.AggregateFlags); Assert.Null(r.Arithmetic.CriticalThreshold);
        Assert.Null(r.Arithmetic.AfterCritical); Assert.Equal(0, r.RequiredZeroOverrideConsultations);
    }
    [Fact]
    public void DrawImmediatelyAboveDodgeCapFailsAndCriticalStillConsumesItsCall()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 1300 } };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 301), (Draw.MainCritical, 1000)));
        Assert.Equal(200, r.RecordDamage); Assert.Equal(300, r.Arithmetic.DodgeThreshold);
    }
    [Fact]
    public void NegativeCriticalChanceStillRequiresOriginalCall()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { PhysicalCriticalReduceRate14c6 = 1 } };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1)));
        Assert.Equal(-1, r.Arithmetic.CriticalThreshold); Assert.Equal((byte)0, r.CriticalStatus);
        Assert.Equal(3, r.OriginalInvocations.Count);
    }
    [Fact]
    public void LevelRulePrecedesArmorSubtraction()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { Level = 35, PhysicalDefense = 600 } };
        var r = Normal(x); Assert.Equal(80, r.RecordDamage); Bits(0x42a00000, r.PreprotectorFloat);
        Bits(0x430c0000, r.Arithmetic.PhysicalDefense!.Value.AfterLevel);
    }
    [Fact]
    public void UnshieldedPlayerAppliesTheCompositePveOperandOnce()
    {
        var x = Baseline(true); x = x with { NativeClassification = x.NativeClassification with { PhysicalPveDefenseOperand = 500 } };
        var r = Normal(x); Assert.Equal(100, r.RecordDamage); Bits(0x42c80000, r.PreprotectorFloat);
        Assert.Equal(0.5f, r.Arithmetic.PveMultiplier);
        // Two NPC classifiers do not enter that modifier branch, even with unused operands.
        var n = Baseline(); n = n with { NativeClassification = n.NativeClassification with { PhysicalPveDefenseOperand = 500 } };
        Assert.Equal(200, Normal(n).RecordDamage);
    }
    [Fact]
    public void PlayerParryMultipliesBeforeNpcPhysicalCritical()
    {
        var x = Baseline(true); x = x with
        {
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500 },
            EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600, Parry14b4 = 1400 },
            NativeEligibility = x.NativeEligibility with { ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Parry, 400), (Draw.MainCritical, 500)));
        Bits(0x42a80000, r.Arithmetic.AfterActiveDefense); Bits(0x42fc0000, r.PreprotectorFloat);
        Assert.Equal(126, r.RecordDamage);
    }
    [Fact]
    public void ExplicitZeroBaseAndFullArmorUseNativeCalculationMinimums()
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with { MinimumDamage = 0, MaximumDamage = 0 } };
        var r = Normal(x); Assert.Equal(1, r.RecordDamage); Bits(0x3f800000, r.PreprotectorFloat);
        var a = Baseline(); a = a with { EffectiveDefender = a.EffectiveDefender with { PhysicalDefense = 3000 } };
        Assert.Equal(1, Normal(a).RecordDamage);
    }
    [Theory]
    [InlineData((ushort)199, (ushort)201)]
    [InlineData((ushort)201, (ushort)199)]
    public void InclusiveBaseRetainsOriginalEndpointOrderAndAcceptsSortedMaximum(ushort first, ushort second)
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with { MinimumDamage = first, MaximumDamage = second } };
        var calls = Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1000)); calls[0] = calls[0] with { Result = 201 };
        var r = Replay.Replay(x, calls);
        Assert.Equal(201, r.RecordDamage); Assert.True(r.OriginalInvocations[0].InclusiveAdapterInvoked);
        Assert.Equal(first, r.OriginalInvocations[0].FirstEndpoint); Assert.Equal(second, r.OriginalInvocations[0].SecondEndpoint);
    }
    [Fact]
    public void FinalFloatBelowSignedConversionLimitIsPreservedAndNextFloatRejected()
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with { MinimumDamage = 1, MaximumDamage = 1, FlatInt = 2147483519 } };
        var r = Normal(x); Assert.Equal(2147483520, r.RecordDamage); Bits(0x4effffff, r.PreprotectorFloat);
        var bad = x with { SelectedProfile = x.SelectedProfile with { FlatInt = int.MaxValue } };
        Assert.Throws<ArithmeticException>(() => Normal(bad)); // f32 ==2^31, not int.MaxValue.
    }
    [Fact]
    public void ProvenFlatIntegerWrapPrecedesFloatMinimum()
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with { MinimumDamage = 1, MaximumDamage = 1, FlatInt = int.MaxValue, FlatWord = 1 } };
        var r = Normal(x); Assert.Equal(int.MinValue, r.Arithmetic.Base.SummedFlat);
        Bits(0xcf000000, r.Arithmetic.Base.BeforeMinimum); Assert.Equal(1, r.RecordDamage);
    }
    [Fact]
    public void CriticalReductionIsAppliedToTheAlreadyParriedDamage()
    {
        var x = Baseline(); x = x with
        {
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500 },
            EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600, Parry14b4 = 1400, PhysicalCriticalDamageReduce14ca = 500 },
            NativeEligibility = x.NativeEligibility with { ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Parry, 400), (Draw.MainCritical, 500)));
        Assert.Equal(126, r.RecordDamage); Assert.Equal(1f, r.Arithmetic.CriticalMultiplier);
        Assert.Equal((byte)12, r.CriticalStatus);
    }
    [Fact]
    public void ZeroChanceAtEveryEligibleStageDoesNotOptimizeAwayOriginalCalls()
    {
        var x = Baseline(); x = x with
        {
            EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 1000, Block14b6 = 1000, Parry14b4 = 1000 },
            NativeEligibility = x.NativeEligibility with { BlockSlot129Result = 1000, ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1), (Draw.Block, 1), (Draw.Parry, 1), (Draw.MainCritical, 1)));
        Assert.Equal(200, r.RecordDamage); Assert.Equal(5, r.OriginalInvocations.Count);
        Assert.Equal(0, r.Arithmetic.DodgeThreshold); Assert.Equal(0, r.Arithmetic.BlockThreshold);
        Assert.Equal(0, r.Arithmetic.ParryThreshold); Assert.Equal(0, r.Arithmetic.CriticalThreshold);
    }
    [Fact]
    public void EligibilitySkipsDodgeCallWithoutSkippingParry()
    {
        var x = Baseline(); x = x with { NativeEligibility = x.NativeEligibility with { DodgeTableAllows = false, ParryTableAllows = true } };
        var r = Replay.Replay(x, Calls(x, (Draw.Parry, 1), (Draw.MainCritical, 1)));
        Assert.Equal(200, r.RecordDamage); Assert.Null(r.Arithmetic.DodgeThreshold);
        Assert.Equal(new[] { Draw.Base, Draw.Parry, Draw.MainCritical }, r.OriginalInvocations.Select(c => c.Stage));
    }
    [Fact]
    public void PhysicalRandomDefenseGatePreservesCriticalCallAndRawCodeBonus()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { RawCode13c = 5 } };
        var r = Replay.Replay(x, Calls(x, (Draw.MainCritical, 500)));
        Assert.Equal(300, r.RecordDamage); Assert.Null(r.Arithmetic.DodgeThreshold);
        Assert.Equal(500, r.Arithmetic.CriticalThreshold);
        var a = Baseline(); a = a with { EffectiveAttacker = a.EffectiveAttacker with { AbnormalFlags1250 = 4 } };
        Assert.Equal(200, Replay.Replay(a, Calls(a, (Draw.MainCritical, 1))).RecordDamage);
    }
    [Fact]
    public void MagicalResistanceLevelOffsetAppliesWithZeroResistance()
    {
        var x = Baseline(); x = x with
        {
            SelectedProfile = x.SelectedProfile with { WeaponGroup8ec = 7 },
            EffectiveDefender = x.EffectiveDefender with { Level = 35 }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.MagicalResist, 100)));
        Assert.Equal(100, r.Arithmetic.MagicalResistThreshold); Assert.True(r.TerminalMiss);
    }
    [Fact]
    public void PlayerParryRetainsIndependentFractionVectorRounding()
    {
        var x = Baseline(true); x = x with
        {
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500 },
            EffectiveDefender = x.EffectiveDefender with { Parry14b4 = 1400 },
            NativeEligibility = x.NativeEligibility with { ParryTableAllows = true }
        };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Parry, 400), (Draw.MainCritical, 500)));
        Bits(0x42f00001, r.Arithmetic.AfterActiveDefense); // 200*f32(.6), not exactly120.
        Assert.Equal(180, r.RecordDamage);
    }
    [Fact]
    public void OutputKeepsSourceIndicesAndCopiesOriginalReceiptStorage()
    {
        var x = Baseline(); x = x with { SelectedProfile = x.SelectedProfile with { SourceIndex = 7, CombinedIndex = 13 } };
        var calls = Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1000)); var copy = calls.ToArray();
        var r = Replay.Replay(x, calls); calls[0] = default;
        Assert.Equal(7, r.SelectedSourceIndex); Assert.Equal((byte)13, r.SelectedCombinedIndex);
        Assert.Equal(copy, r.OriginalInvocations);
        Assert.Throws<NotSupportedException>(() => ((IList<Replay.OriginalInvocation>)r.OriginalInvocations).Add(default));
    }
    [Theory]
    [InlineData(1000, 200, 800, false)]
    [InlineData(100, 100, 0, true)]
    [InlineData(200, 200, 0, true)]
    [InlineData(60, 60, 0, true)]
    public void PrimaryPredictionKeepsRecordCellDeltaAndFinalHpSeparate(int hp, int capped, int after, bool lethal)
    {
        var r = Normal(Baseline()); var p = Replay.PredictSerialPrimaryCommit(r, Commit(r, hp));
        Assert.Equal(200, r.RecordDamage); Assert.Equal(200, p.RequestedApplicationCell);
        Assert.Equal(capped, p.CellAfterCap); Assert.Equal(capped, p.PrimaryDelta);
        Assert.Equal(after, p.HpAfterPrimaryArithmetic); Assert.Equal(lethal, p.LethalArithmeticBranch);
        Assert.Null(p.FinalHpAfterCallbacks);
    }
    [Fact]
    public void ZeroHitStillRequiresOriginalDispatchAndLastAttackerReceipts()
    {
        var x = Baseline(); x = x with { EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 1300 } };
        var r = Replay.Replay(x, Calls(x, (Draw.Dodge, 1)));
        var p = Replay.PredictSerialPrimaryCommit(r, Commit(r, 1));
        Assert.Equal(1, p.HpAfterPrimaryArithmetic); Assert.Equal(0, p.PrimaryDelta); Assert.False(p.LethalArithmeticBranch);
        Assert.Contains(p.RequiredOriginalBoundaries, b => b.Boundary == "ActorGetHitDamageEntryAndLastAttacker");
        Assert.DoesNotContain(p.RequiredOriginalBoundaries, b => b.Boundary == "PrimaryHpOperation");
        Assert.Contains(p.RequiredOriginalBoundaries, b => b.Boundary == "SendHpAfterPrimary");
    }
    [Fact]
    public void OwnerReceiptsRetainTheTwoHpNotificationBoundariesInOrder()
    {
        var r = Normal(Baseline()); var p = Replay.PredictSerialPrimaryCommit(r, Commit(r));
        var names = p.RequiredOriginalBoundaries.Select(b => b.Boundary).ToArray();
        Assert.True(Array.IndexOf(names, "SendHpInsideBaseAndHpChange") < Array.IndexOf(names, "AttackerResultSync"));
        Assert.True(Array.IndexOf(names, "AttackerResultSync") < Array.IndexOf(names, "SendHpAfterPrimary"));
        Assert.Contains("ByValueOnHitCallback", names); Assert.Contains("OnHitPass2", names);
        var absentManager = Replay.PredictSerialPrimaryCommit(r, Commit(r) with { HasSkillEffectManager = false });
        Assert.DoesNotContain(absentManager.RequiredOriginalBoundaries, b => b.Boundary is "ByValueOnHitCallback" or "OnHitPass2");
    }
    public static IEnumerable<object[]> UnsupportedStates()
    {
        foreach (Feature feature in Enum.GetValues<Feature>())
        foreach (State state in new[] { State.Unknown, State.PresentUnsupported })
            yield return new object[] { (int)feature, (int)state };
    }
    [Theory]
    [MemberData(nameof(UnsupportedStates))]
    public void EveryUnknownOrPresentUnsupportedFeatureRejectsBeforeReceipts(int rawFeature, int rawState)
    {
        var x = Baseline(); var absent = Absences(); int index = Array.FindIndex(absent, r => (int)r.Feature == rawFeature);
        absent[index] = absent[index] with { State = (State)rawState };
        Assert.Throws<ArgumentException>(() => Replay.Replay(x with { FeatureAbsences = absent }, Array.Empty<Replay.OriginalInvocation>()));
        var r = Normal(x);
        Assert.Throws<ArgumentException>(() => Replay.PredictSerialPrimaryCommit(r, Commit(r) with { FeatureAbsencesAtCommit = absent }));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void AbsenceReceiptSetMustBeCompleteUniqueKnownAndSourced(int bad)
    {
        var x = Baseline(); var a = Absences();
        IReadOnlyList<Replay.AbsenceReceipt> invalid = bad switch
        {
            0 => a[..^1],
            1 => a.Append(a[0]).ToArray(),
            2 => a.Select((r, i) => i == 0 ? a[1] : r).ToArray(),
            3 => a.Select((r, i) => i == 0 ? r with { Feature = (Feature)1000 } : r).ToArray(),
            _ => a.Select((r, i) => i == 0 ? r with { Evidence = "" } : r).ToArray()
        };
        Assert.Throws<ArgumentException>(() => Normal(x with { FeatureAbsences = invalid }));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)] [InlineData(18)] [InlineData(19)]
    [InlineData(20)] [InlineData(21)] [InlineData(22)] [InlineData(23)] [InlineData(24)]
    [InlineData(25)] [InlineData(26)] [InlineData(27)] [InlineData(28)] [InlineData(29)]
    [InlineData(30)] [InlineData(31)] [InlineData(32)] [InlineData(33)] [InlineData(34)]
    [InlineData(35)] [InlineData(36)] [InlineData(37)] [InlineData(38)] [InlineData(39)]
    public void InvalidOrUnsupportedActionFactsAreRejected(int bad)
    {
        var x = Baseline();
        x = bad switch
        {
            0 => x with { Source = x.Source with { MainSha256 = new string('0', 64) } },
            1 => x with { Source = x.Source with { DatasetSha256 = "unknown" } },
            2 => x with { Source = x.Source with { RegionContext = "" } },
            3 => x with { Source = x.Source with { AttackerObjectId = 0 } },
            4 => x with { Source = x.Source with { DefenderObjectId = x.Source.AttackerObjectId } },
            5 => x with { Source = x.Source with { DefenderGeneration = "" } },
            6 => x with { Source = x.Source with { ActionId = "" } },
            7 => x with { OriginalWorldAndActorGuardsPassed = false },
            8 => x with { GuardEvidence = "" },
            9 => x with { SelectedProfile = x.SelectedProfile with { NpcTemplateId = 212835 } },
            10 => x with { SelectedProfile = x.SelectedProfile with { SourceIndex = -1 } },
            11 => x with { SelectedProfile = x.SelectedProfile with { MainHitCount = 2 } },
            12 => x with { SelectedProfile = x.SelectedProfile with { OffhandHitCount = 1 } },
            13 => x with { SelectedProfile = x.SelectedProfile with { DamageElement = 7 } },
            14 => x with { SelectedProfile = x.SelectedProfile with { ElementMappingEvidence = "" } },
            15 => x with { SelectedProfile = x.SelectedProfile with { PhysicalFlag8e4 = 0 } },
            16 => x with { SelectedProfile = x.SelectedProfile with { NormalAttackExclusiveType450 = 1 } },
            17 => x with { EffectiveAttacker = x.EffectiveAttacker with { ForcedCompareCounter = 1 } },
            18 => x with { EffectiveAttacker = x.EffectiveAttacker with { AbnormalFlags1250 = 0x20 } },
            19 => x with { EffectiveDefender = x.EffectiveDefender with { PhysicalDamageOverride = 1 } },
            20 => x with { EffectiveDefender = x.EffectiveDefender with { ElementalDamageOverride = 1 } },
            21 => x with { NativeEligibility = x.NativeEligibility with { BlockSlot129Result = 1 } },
            22 => x with { Scope = RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
                RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.OrdinaryOwnerlessNpc, 0, 212765) },
            23 => x with { EffectiveDefender = x.EffectiveDefender with { MinimumElementalDefense = -1299 } },
            24 => x with { EffectiveAttacker = x.EffectiveAttacker with { Level = 0 } },
            25 => x with { EffectiveDefender = x.EffectiveDefender with { Level = 101 } },
            26 => x with { EffectiveAttacker = x.EffectiveAttacker with { StatRatio1514 = 999 } },
            27 => x with { EffectiveDefender = x.EffectiveDefender with { StatRatio1514 = 65001 } },
            28 => x with { EffectiveDefender = x.EffectiveDefender with { Evasion14ae = -1 } },
            29 => x with { EffectiveDefender = x.EffectiveDefender with { Parry14b4 = 65536 } },
            30 => x with { EffectiveDefender = x.EffectiveDefender with { PhysicalCriticalDamageReduce14ca = 65536 } },
            31 => x with { EffectiveDefender = x.EffectiveDefender with { PhysicalCriticalReduceRate14c6 = 65536 } },
            32 => x with { SelectedProfile = x.SelectedProfile with { EffectiveProfileRate = float.NaN } },
            33 => x with { SelectedProfile = x.SelectedProfile with { AdditiveRate = float.PositiveInfinity } },
            34 => x with { NativeClassification = x.NativeClassification with { AttackerSlot127Result = 1 } },
            35 => x with { NativeClassification = x.NativeClassification with { DefenderSlot127Result = 1 } },
            36 => x with { EffectiveDefender = x.EffectiveDefender with { RawCode13c = 16 } },
            37 => x with { EffectiveAttacker = x.EffectiveAttacker with { Evidence = "" } },
            38 => x with { NativeClassification = x.NativeClassification with { Evidence = "" } },
            _ => x with { Source = x.Source with { Evidence = "" } }
        };
        Assert.ThrowsAny<ArgumentException>(() => Normal(x));
    }
    [Fact]
    public void PlayerShieldEligibilityIsRejectedAndSourceWordMismatchCannotBeInvented()
    {
        var x = Baseline(true); x = x with { NativeEligibility = x.NativeEligibility with { BlockSlot129Result = 1 } };
        Assert.Throws<ArgumentException>(() => Normal(x));
        var n = Baseline(); n = n with { EffectiveDefender = n.EffectiveDefender with { Block14b6 = 10 } };
        Assert.Throws<ArgumentException>(() => Normal(n));
    }
    [Fact]
    public void OwnedScaledUnverifiedAndPvpCannotCreateAnAdmittedScope()
    {
        foreach (var actor in new[] { RetailNpcNormalActorKind.OwnedNpc, RetailNpcNormalActorKind.ScaledNpc, RetailNpcNormalActorKind.Unverified })
            Assert.Throws<ArgumentOutOfRangeException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
                actor, RetailNpcNormalActorKind.Player, 212776, 0));
        Assert.Throws<ArgumentException>(() => RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.Player, RetailNpcNormalActorKind.Player, 0, 0));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(10)] [InlineData(11)] [InlineData(12)] [InlineData(13)]
    public void OriginalInvocationOrderEndpointsIdentityAndConsumptionAreExact(int bad)
    {
        var x = Baseline(); var calls = Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1000));
        switch (bad)
        {
            case 0:
                x = x with { EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 1300 } };
                calls[1] = calls[1] with { Result = 1 }; break; // critical is now an extra invocation.
            case 1: calls = calls[..^1]; break;
            case 2: (calls[1], calls[2]) = (calls[2], calls[1]); break;
            case 3:
                x = x with { SelectedProfile = x.SelectedProfile with { MinimumDamage = 199, MaximumDamage = 201 } };
                calls[0] = Call(Draw.Base, 200, 199, 201, false); break;
            case 4: calls[0] = calls[0] with { InclusiveAdapterInvoked = true }; break;
            case 5: calls[0] = calls[0] with { Result = 201 }; break;
            case 6: calls[1] = calls[1] with { Result = 1001 }; break;
            case 7: calls[1] = calls[1] with { SecondEndpoint = 999 }; break;
            case 8: calls[1] = calls[1] with { ActionId = "other-action" }; break;
            case 9: calls[1] = calls[1] with { MainSha256 = new string('0', 64) }; break;
            case 10: calls[1] = calls[1] with { Evidence = "" }; break;
            case 11: calls = Array.Empty<Replay.OriginalInvocation>(); break;
            case 12: calls = new[] { calls[0], calls[0], calls[1], calls[2] }; break;
            case 13: calls[2] = calls[2] with { InclusiveAdapterInvoked = false }; break;
        }
        Assert.ThrowsAny<ArgumentException>(() => Replay.Replay(x, calls));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
    public void PrimaryCommitRejectsUnverifiedOrDifferentCompletionContext(int bad)
    {
        var r = Normal(Baseline()); var c = Commit(r);
        c = bad switch
        {
            0 => c with { CurrentHpBeforePrimaryOperation = 0 },
            1 => c with { CurrentHpBeforePrimaryOperation = -1 },
            2 => c with { OriginalActorDispatchGuardsPassed = false },
            3 => c with { NativeDeathPolicySlot179Result = 1 },
            4 => c with { RawNoKillFlag = 1 },
            5 => c with { InterveningWrites = State.Unknown },
            6 => c with { InterveningWrites = State.PresentUnsupported },
            7 => c with { Source = c.Source with { DefenderGeneration = "other-generation" } },
            8 => c with { Source = c.Source with { ActionId = "other-action" } },
            _ => c with { Evidence = "" }
        };
        Assert.ThrowsAny<ArgumentException>(() => Replay.PredictSerialPrimaryCommit(r, c));
    }
    [Fact]
    public void ExactFixturePrimaryBoundariesRetainTheirRequestedCells()
    {
        var x = Baseline(); x = x with
        {
            EffectiveAttacker = x.EffectiveAttacker with { PhysicalCritical14bc = 500 },
            EffectiveDefender = x.EffectiveDefender with { PhysicalDefense = 600, Parry14b4 = 1400 },
            NativeEligibility = x.NativeEligibility with { ParryTableAllows = true }
        };
        var parry = Replay.Replay(x, Calls(x, (Draw.Dodge, 1000), (Draw.Parry, 400), (Draw.MainCritical, 500)));
        var capped = Replay.PredictSerialPrimaryCommit(parry, Commit(parry, 100));
        Assert.Equal(189, capped.RequestedApplicationCell); Assert.Equal(100, capped.CellAfterCap);
        var equal = Replay.PredictSerialPrimaryCommit(parry, Commit(parry, 189));
        Assert.Equal(189, equal.RequestedApplicationCell); Assert.Equal(189, equal.CellAfterCap);
        Assert.True(equal.LethalArithmeticBranch); Assert.Null(equal.FinalHpAfterCallbacks);
        var low = Baseline(); low = low with { EffectiveDefender = low.EffectiveDefender with { Level = 35, PhysicalDefense = 600 } };
        var hit = Normal(low); var p = Replay.PredictSerialPrimaryCommit(hit, Commit(hit, 60));
        Assert.Equal(80, p.RequestedApplicationCell); Assert.Equal(60, p.CellAfterCap);
        Assert.Equal(0, p.HpAfterPrimaryArithmetic); Assert.Null(p.FinalHpAfterCallbacks);
    }
    [Fact]
    public void StatusIntegerOverflowAndNonfiniteIntermediatesReject()
    {
        var x = Baseline(); x = x with
        {
            EffectiveDefender = x.EffectiveDefender with { Evasion14ae = 65535 },
            LoadedMovement = x.LoadedMovement with { DefenderEvasionAddition = int.MaxValue }
        };
        Assert.Throws<ArithmeticException>(() => Normal(x));
        var overflow = Baseline(); overflow = overflow with
        { SelectedProfile = overflow.SelectedProfile with { EffectiveProfileRate = float.MaxValue } };
        Assert.Throws<ArithmeticException>(() => Normal(overflow));
    }
    [Fact]
    public void AbsentTopLevelFactsAndReceiptsAreNotDefaulted()
    {
        var x = Baseline(); var calls = Calls(x, (Draw.Dodge, 1000), (Draw.MainCritical, 1000));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(null!, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x, null!));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { Scope = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { SelectedProfile = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { EffectiveAttacker = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { EffectiveDefender = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { LoadedMovement = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { NativeEligibility = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { NativeClassification = null! }, calls));
        Assert.Throws<ArgumentNullException>(() => Replay.Replay(x with { FeatureAbsences = null! }, calls));
        var r = Normal(x);
        Assert.Throws<ArgumentNullException>(() => Replay.PredictSerialPrimaryCommit(null!, Commit(r)));
        Assert.Throws<ArgumentNullException>(() => Replay.PredictSerialPrimaryCommit(r, null!));
    }
}
