using System.Collections.Immutable;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using H = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcInstantHealPlan;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcInstantHealPlanTests
{
    private const string Analytic = "analytic instruction fixture; no native execution";
    private static H.Actor Actor(uint id) => new(id, $"fixture-lifetime-{id}", H.OrdinaryNpcVtable, 0, 0, false, Analytic);
    private static H.Context Context() => new(H.MainSha256, H.DatasetSha256, H.Region, "fixture-action", Actor(100), Actor(200), Analytic);
    private static H.Stamp Stamp(H.Context c, int n, H.ReadKind k) => new(c, n, k, Analytic);
    private static H.SkillSource Skill(int id = 16836) => id == 16836
        ? new(id, "NCL_HealSS_Tree", H.SkillFileSha256, "4211edff9c4d97c5a2e36f844c607df06709404d1766344a0f4cda499f0ca6d9",
            47345450, 47349608, 0, "Heal_Instant", 1, 0, 0, 20, 1, 22, Analytic)
        : new(id, "NEL_PetHeal_Nr", H.SkillFileSha256, "b1827888d8d816f7be2305c3ea45ae93f9a37b0ba7c241b3459aa421b74a8825",
            45891980, 45896144, 0, "Heal_Instant", 1, 0, 0, 25, 1, 22, Analytic);
    private static H.ThresholdSkill ThresholdSkill(string name = "configured", uint metadata = 77) => new(name, 123, 4, metadata, Analytic);
    private static H.Observation Input()
    {
        var c = Context();
        return new(c, Skill(), new(Stamp(c, 1, H.ReadKind.CheckMaximum), 5954),
            new(Stamp(c, 2, H.ReadKind.Begin), 5000, 1, int.MaxValue, 0, true),
            new(Stamp(c, 3, H.ReadKind.HealEntry), 0, 5000, 0),
            [new(Stamp(c, 4, H.ReadKind.CompareExchange), 5000, 5954, 5000)],
            new(Stamp(c, 5, H.ReadKind.Threshold), 0, null, null, null, null, null, null), null,
            new(Stamp(c, 6, H.ReadKind.HealReturn), 5954, 5954, 5954, 1000, 2000, 7, 9));
    }
    private static H.Observation NoCommit(H.Observation input) => input with
    { Attempts = [], Threshold = null, Callback = null, Returned = null };
    private static H.Observation ChangeContext(H.Observation input, H.Context c) => input with
    {
        Context = c, Maximum = input.Maximum with { Stamp = input.Maximum.Stamp with { Context = c } },
        Begin = input.Begin with { Stamp = input.Begin.Stamp with { Context = c } },
        Entry = input.Entry with { Stamp = input.Entry.Stamp with { Context = c } },
        Attempts = input.Attempts.Select(x => x with { Stamp = x.Stamp with { Context = c } }).ToImmutableArray(),
        Threshold = input.Threshold is { } t ? t with { Stamp = t.Stamp with { Context = c } } : null,
        Callback = input.Callback is { } b ? b with { Stamp = b.Stamp with { Context = c } } : null,
        Returned = input.Returned is { } r ? r with { Stamp = r.Stamp with { Context = c } } : null
    };

    // Fixed expected values were authored independently in heal_analytic_vectors.py before this unit.
    [Theory]
    [InlineData(20, 0, 0, 0, 0)]
    [InlineData(20, 1, 0, 0, 0)]
    [InlineData(20, 99, 19, 9, 23)]
    [InlineData(20, 100, 20, 10, 25)]
    [InlineData(20, 101, 20, 10, 25)]
    [InlineData(20, 5452, 1090, 545, 1362)]
    [InlineData(20, 5954, 1190, 595, 1487)]
    [InlineData(20, 16777217, 3355443, 1677721, 4194303)]
    [InlineData(20, 2000000000, 400000000, 200000000, 500000000)]
    [InlineData(25, 0, 0, 0, 0)]
    [InlineData(25, 1, 0, 0, 0)]
    [InlineData(25, 99, 24, 12, 30)]
    [InlineData(25, 100, 25, 12, 31)]
    [InlineData(25, 101, 25, 12, 31)]
    [InlineData(25, 5452, 1363, 681, 1703)]
    [InlineData(25, 5954, 1488, 744, 1860)]
    [InlineData(25, 16777217, 4194304, 2097152, 5242880)]
    [InlineData(25, 2000000000, 500000000, 250000000, 625000000)]
    public void PercentAndTargetMultiplierPreserveSeparateBinary32Stages(int percent, int maximum,
        int reserved, int half, int quarterMore)
    {
        int actual = H.CalculateNoBoostReserved(22, 0, percent, 1, maximum);
        Assert.Equal(reserved, actual);
        Assert.Equal(reserved, H.CalculateRequested(actual, 1));
        Assert.Equal(half, H.CalculateRequested(actual, .5f));
        Assert.Equal(quarterMore, H.CalculateRequested(actual, 1.25f));
    }

    [Theory]
    [InlineData(-32768, 65537, 0, 2147450880)]
    [InlineData(32767, 65539, 2147483647, 32764)]
    [InlineData(1, 0, 16777217, 16777216)]
    [InlineData(1, 0, 2147483647, -2147483648)]
    public void BaseUsesSignedLevelWrappedIntAndCvtt(short level, int r1, int r2, int expected) =>
        Assert.Equal(expected, H.CalculateNoBoostBase(level, r1, r2));

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(2147483647)]
    public void OnlyRawR6ExactlyOneSelectsPercent(int r6) =>
        Assert.Equal(20, H.CalculateNoBoostReserved(22, 0, 20, r6, 5954));

    [Fact]
    public void SelectedPilotPreservesRequestCappedCommitNotificationsAndOriginalRecord()
    {
        var result = H.Replay(Input());
        Assert.Equal(1190, result.Reserved);
        Assert.Equal(1190, result.Requested);
        Assert.Equal(954, result.HpArgumentCell);
        Assert.Equal(5954, result.PrimaryHp);
        Assert.True(result.HealReturnedTrue);
        Assert.Equal(new H.Record(-954, 0, -954, 100, 50, 7, 9), result.EmittedRecord);
        Assert.Equal(new[] { "SendHP", "SendHPChange", "AppendSkillDamageInfo", "UpdateRequestedMaximum", "UpdateRequestedMinimum" },
            result.Operations.Select(x => x.Kind).ToArray());
        var notification = result.Operations[1];
        Assert.Equal(0, notification.Value); Assert.Equal(5, notification.Type); Assert.Equal(0xc5u, notification.Metadata);
        Assert.Equal(1190, result.MinimumRequested); Assert.Equal(1190, result.MaximumRequested);
        Assert.Null(result.FinalHpAfterCallbacks); Assert.False(result.RuntimeVerified); Assert.False(result.ExecutesHpOrCallbacks);
    }

    [Fact]
    public void SecondPilotIsPercentageNotFlatTwentyFive()
    {
        var input = Input();
        var result = H.Replay(input with { Skill = Skill(16588) });
        Assert.Equal(1488, result.Reserved); Assert.Equal(954, result.HpArgumentCell);
    }

    [Theory]
    [InlineData(1, 5000, 0u)]
    [InlineData(0, 0, 0u)]
    [InlineData(0, -1, 0u)]
    [InlineData(0, 5000, 0x80u)]
    [InlineData(0, 5000, 0x180u)]
    public void LifeAndHpGuardPreventHealingButRetainRequestedMetadata(int life, int hp, uint flags)
    {
        var input = Input();
        var result = H.Replay(NoCommit(input with { Entry = input.Entry with { Life728 = life, CurrentHp = hp, Abnormal1250 = flags } }));
        Assert.False(result.HealReturnedTrue); Assert.Equal(hp, result.PrimaryHp);
        Assert.Equal(1190, result.HpArgumentCell); Assert.Equal(1190, result.MaximumRequested);
        Assert.Null(result.EmittedRecord); Assert.Empty(result.Attempts);
        Assert.Equal(new[] { "UpdateRequestedMaximum", "UpdateRequestedMinimum" }, result.Operations.Select(x => x.Kind).ToArray());
    }

    [Fact]
    public void ZeroModifierRemainsZeroAndDoesNotRestoreDefaultHealing()
    {
        var input = Input();
        var result = H.Replay(NoCommit(input with { Begin = input.Begin with { TargetMultiplier1050 = 0 } }));
        Assert.Equal(0, result.Requested); Assert.False(result.HealReturnedTrue);
        Assert.Equal(0, result.MinimumRequested); Assert.Equal(0, result.MaximumRequested);
        Assert.DoesNotContain(result.Operations, x => x.Kind == "UpdateRequestedMaximum");
    }

    [Fact]
    public void NativeMetadataWritesOccurOnlyOnStrictImprovement()
    {
        var input = Input();
        var result = H.Replay(input with { Begin = input.Begin with { ActionMinimumRequested = 1190, ActionMaximumRequested = 1190 } });
        Assert.DoesNotContain(result.Operations, x => x.Kind.StartsWith("UpdateRequested", StringComparison.Ordinal));
        Assert.Equal(1190, result.MinimumRequested); Assert.Equal(1190, result.MaximumRequested);
    }

    [Fact]
    public void CurrentHpAtCapProducesSuccessfulCasButFalseHealAndNoRecord()
    {
        var input = Input();
        var result = H.Replay(input with { Attempts = [input.Attempts[0] with { CurrentHp = 5954,
            CompareExchangeReturnedOldHp = 5954 }], Threshold = null, Returned = null });
        Assert.False(result.HealReturnedTrue); Assert.Equal(0, result.HpArgumentCell);
        Assert.True(Assert.Single(result.Attempts).Succeeded); Assert.Equal(5954, result.PrimaryHp);
    }

    [Fact]
    public void CasRetryRereadsHpAndMaximumWithoutReapplyingPercentage()
    {
        var input = Input(); var c = input.Context;
        var result = H.Replay(input with
        {
            Attempts = [input.Attempts[0] with { CompareExchangeReturnedOldHp = 5100 },
                new(Stamp(c, 5, H.ReadKind.CompareExchange), 5100, 6000, 5100)],
            Threshold = input.Threshold! with { Stamp = Stamp(c, 6, H.ReadKind.Threshold) },
            Returned = input.Returned! with { Stamp = Stamp(c, 7, H.ReadKind.HealReturn), RereadHp = 6000 }
        });
        Assert.Equal(2, result.Attempts.Length); Assert.False(result.Attempts[0].Succeeded);
        Assert.True(result.Attempts[1].Succeeded); Assert.Equal(900, result.HpArgumentCell);
        Assert.Equal(6000, result.PrimaryHp); Assert.Equal(-1000, result.EmittedRecord!.HpDelta);
        Assert.Equal(1190, result.Requested);
    }

    [Fact]
    public void HpCommitUsesWrappedSigned32AndHasNoInventedLowerClamp()
    {
        var input = Input();
        var result = H.Replay(input with
        {
            Attempts = [input.Attempts[0] with { CurrentHp = 2147483600, MaximumHp = int.MaxValue,
                CompareExchangeReturnedOldHp = 2147483600 }],
            Returned = input.Returned! with { RereadHp = -2147482506 }
        });
        Assert.Equal(-2147482506, result.PrimaryHp); Assert.Equal(1190, result.HpArgumentCell);
        Assert.True(result.HealReturnedTrue);
    }

    [Fact]
    public void NegativeTargetMultiplierIsNotSilentlyClampedToZero()
    {
        var input = Input();
        var result = H.Replay(input with { Begin = input.Begin with { TargetMultiplier1050 = -1 },
            Returned = input.Returned! with { RereadHp = 3810 } });
        Assert.Equal(-1190, result.Requested); Assert.Equal(3810, result.PrimaryHp);
        Assert.Equal(1190, result.EmittedRecord!.HpDelta);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(0x80000000u)]
    public void ThresholdFlagShortCircuitDoesNotReadConfiguredOrScalarFields(uint flags)
    {
        var input = Input();
        Assert.Equal(H.ThresholdCall.None, H.Replay(input with { Threshold = input.Threshold! with { Flags12cc = flags } }).ThresholdAction);
    }

    [Theory]
    [InlineData(false, 500, null, 0)]
    [InlineData(false, 501, null, -1)]
    [InlineData(true, 499, 501, 1)]
    [InlineData(true, 501, 500, -1)]
    [InlineData(true, 500, 500, -1)]
    public void ThresholdRetainsStrictComparisonAndSeparateSecondGetter(bool active, int firstHp, int? secondHp, int expectedCall)
    {
        var input = Input(); var c = input.Context;
        var action = expectedCall == 0 ? H.ThresholdCall.FireSystemSkill : expectedCall == 1
            ? H.ThresholdCall.RemoveSystemSkill : H.ThresholdCall.None;
        var callback = action == H.ThresholdCall.None ? null : new H.ThresholdReturn(Stamp(c, 6, H.ReadKind.ThresholdReturn),
            action, true, action == H.ThresholdCall.FireSystemSkill ? ThresholdSkill("reloaded", 99) : null, Analytic);
        var result = H.Replay(input with
        {
            Threshold = input.Threshold! with { Flags12cc = 1, Configured238 = ThresholdSkill(),
                Active218 = active, MaximumHp = 1000, Percent230 = 50, FirstHp = firstHp, SecondHp = secondHp },
            Callback = callback, Returned = input.Returned! with { Stamp = Stamp(c, callback is null ? 6 : 7, H.ReadKind.HealReturn) }
        });
        Assert.Equal(500, result.ThresholdValue); Assert.Equal(action, result.ThresholdAction);
        Assert.Equal(action == H.ThresholdCall.FireSystemSkill ? "reloaded" : null, result.ActivePointerWrite);
        Assert.Equal(action == H.ThresholdCall.FireSystemSkill ? 99u : (uint?)null, result.ActiveMetadataWrite);
        if (action == H.ThresholdCall.FireSystemSkill)
        {
            Assert.Equal(0x140360d70ul, result.Invocation!.Function);
            Assert.Equal(200u, result.Invocation.ThisObjectId); Assert.Equal(200u, result.Invocation.TargetObjectId);
            Assert.Equal(123, result.Invocation.SkillId); Assert.Equal((ushort)4, result.Invocation.SkillLevel);
            Assert.Equal(6, result.Invocation.StackWrites.Length); Assert.True(result.Invocation.OutgoingStack20Unwritten);
        }
        else if (action == H.ThresholdCall.RemoveSystemSkill)
        {
            Assert.Equal(0x1400b3e30ul, result.Invocation!.Function); Assert.Equal(0u, result.Invocation.Edx);
        }
        else Assert.Null(result.Invocation);
    }

    [Fact]
    public void ThresholdProductWrapsBeforeConvertingToFloat()
    {
        Assert.Equal(0, H.CalculateThreshold(1073741824, 4));
        Assert.Equal(21474836, H.CalculateThreshold(int.MaxValue, 1));
    }

    [Fact]
    public void RecordUsesOriginalRereadAfterCallbackRatherThanPrimaryCommit()
    {
        var input = Input();
        var result = H.Replay(input with { Returned = input.Returned! with { RereadHp = 100,
            TargetRatioCurrentHp = 100, TargetRatioMaximumHp = 1000 } });
        Assert.Equal(5954, result.PrimaryHp); Assert.Equal(4900, result.EmittedRecord!.HpDelta);
        Assert.Equal((byte)10, result.EmittedRecord.TargetRatio); Assert.Null(result.FinalHpAfterCallbacks);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(-1, 1000, 0)]
    [InlineData(1000, 0, 0)]
    [InlineData(1, 1000, 1)]
    [InlineData(500, 1000, 50)]
    [InlineData(1000, 1000, 100)]
    [InlineData(3000, 1000, 42)]
    public void RecordRatioUsesPositiveGuardsSigned64DivisionAndLowByte(int hp, int maximum, byte ratio)
    {
        var input = Input();
        var result = H.Replay(input with { Returned = input.Returned! with { TargetRatioCurrentHp = hp,
            TargetRatioMaximumHp = maximum, CasterRatioCurrentHp = hp, CasterRatioMaximumHp = maximum } });
        Assert.Equal(ratio, result.EmittedRecord!.TargetRatio); Assert.Equal(ratio, result.EmittedRecord.CasterRatio);
    }

    private static IEnumerable<H.SkillSource> InvalidSources()
    {
        var s = Skill();
        yield return s with { Id = 1 }; yield return s with { Devname = "wrong" };
        yield return s with { FileSha256 = "wrong" }; yield return s with { RawRecordSha256 = "wrong" };
        yield return s with { ByteStart = s.ByteStart + 1 }; yield return s with { ByteEndExclusive = s.ByteEndExclusive + 1 };
        yield return s with { EffectIndex = 1 }; yield return s with { EffectType = "Heal" };
        yield return s with { NoResist = 0 }; yield return s with { ApplyHealBoost = 1 };
        yield return s with { R1 = 1 }; yield return s with { R2 = 25 }; yield return s with { R6 = 0 };
        yield return s with { Evidence = "" };
    }
    [Fact]
    public void UnknownOrConflictingSourceNeverFallsBack()
    {
        foreach (var source in InvalidSources())
            Assert.Throws<ArgumentException>(() => H.Replay(Input() with { Skill = source }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void IndependentOwnerProfileAndVtableProofAreRequired(int field)
    {
        var input = Input(); var actor = input.Context.Target;
        actor = field switch
        {
            0 => actor with { MasterObjectId = null }, 1 => actor with { MasterObjectId = 1 },
            2 => actor with { SourceRelatedObjectId = null }, 3 => actor with { SourceRelatedObjectId = 1 },
            4 => actor with { VirtualProfilePresent = null }, 5 => actor with { VirtualProfilePresent = true },
            6 => actor with { Vtable = 1 }, _ => actor with { Generation = "" }
        };
        Assert.Throws<ArgumentException>(() => H.Replay(ChangeContext(input, input.Context with { Target = actor })));
    }

    [Fact]
    public void StaleContextAndWrongReadSequenceRejectBeforeRecord()
    {
        var input = Input();
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Context = input.Context with { MainSha256 = "wrong" } }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Context = input.Context with { ActionId = "other" } }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Begin = input.Begin with { Stamp = input.Begin.Stamp with { Ordinal = 3 } } }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Begin = input.Begin with { NearestEvenArithmetic = false } }));
    }

    [Fact]
    public void MissingOrExtraCasThresholdCallbackAndReturnEvidenceRejects()
    {
        var input = Input();
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Attempts = [] }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Attempts = default }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Attempts = [input.Attempts[0] with { CompareExchangeReturnedOldHp = 5001 }] }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Attempts = [input.Attempts[0], input.Attempts[0]] }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Threshold = null }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Returned = null }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Threshold = input.Threshold! with { FirstHp = 1 } }));
        Assert.Throws<ArgumentException>(() => H.Replay(input with { Threshold = input.Threshold! with { Configured238 = ThresholdSkill() } }));
        Assert.Throws<ArgumentException>(() => H.Replay(NoCommit(input) with { Entry = input.Entry with { Life728 = 1 }, Returned = input.Returned }));
    }

    [Fact]
    public void MissingThresholdConditionalReadsOrCallbackReturnNeverUsesDesiredHp()
    {
        var input = Input(); var c = input.Context;
        var threshold = input.Threshold! with { Flags12cc = 1, Configured238 = ThresholdSkill(),
            Active218 = false, MaximumHp = 1000, Percent230 = 50, FirstHp = 500 };
        var fire = new H.ThresholdReturn(Stamp(c, 6, H.ReadKind.ThresholdReturn), H.ThresholdCall.FireSystemSkill, true,
            ThresholdSkill("after"), Analytic);
        var complete = input with { Threshold = threshold, Callback = fire,
            Returned = input.Returned! with { Stamp = Stamp(c, 7, H.ReadKind.HealReturn) } };
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Callback = null }));
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Callback = fire with { OriginalCallReturned = false } }));
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Callback = fire with { ReloadedConfigured238 = null } }));
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Threshold = threshold with { MaximumHp = null } }));
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Threshold = threshold with { Active218 = true } }));
        Assert.Throws<ArgumentException>(() => H.Replay(complete with { Threshold = threshold with { SecondHp = 500 } }));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.Epsilon)]
    public void UnresolvedNonfiniteOrDenormalMultiplierRejects(float multiplier) =>
        Assert.Throws<ArgumentException>(() => H.CalculateRequested(100, multiplier));

    [Fact]
    public void FiniteOverflowUsesCvttIndefiniteRatherThanSaturatingOrThrowing() =>
        Assert.Equal(int.MinValue, H.CalculateRequested(int.MaxValue, float.MaxValue));
}
