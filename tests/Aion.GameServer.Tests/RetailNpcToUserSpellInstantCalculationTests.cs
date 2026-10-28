using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Commons.Utils.RetailRandom;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserSpellInstantCalculation;
using F = Aion.GameServer.Tests.UserMagicalFixture;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcToUserSpellInstantCalculationTests
{
    public static IEnumerable<object[]> Recipes()=>F.Evidence.RootElement.GetProperty("cases").EnumerateArray().Select(x=>new object[]{x.GetProperty("name").GetString()!});
    [Theory,MemberData(nameof(Recipes))]
    public void CompleteRawKnownBanksMatchAllIndependentStageWordsAndOriginalRandomState(string name)
    { Verify(F.Row(name)); }
    [Fact]
    public void OriginalIndex623LastSecondBlockWordAndResetAreNotCollapsedTo624()
    { Verify(F.Additional.RootElement.GetProperty("cases")[0]); }
    public static IEnumerable<object[]> Hp2Programs()=>F.Additional.RootElement.GetProperty("hp2_cases").EnumerateArray().Select(x=>new object[]{x.GetProperty("name").GetString()!});
    [Theory,MemberData(nameof(Hp2Programs))]
    public void CompleteRawHp2ProgramsMatchIndependentSignedZeroModesRowsAndFinalStages(string name)
    {
        var row=F.Additional.RootElement.GetProperty("hp2_cases").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==name);
        var f=Basic();f.Add(F.Program,32,"HP2-program");f.Put(F.Program,Convert.FromHexString(row.GetProperty("raw32").GetString()!));f.W64(F.User+0x8008,F.Program);
        f.W32(F.User+0x774,unchecked((uint)row.GetProperty("maximum").GetInt32()));f.W32(F.User+0x778,unchecked((uint)row.GetProperty("current").GetInt32()));f.W32(F.User+0x1674,unchecked((uint)row.GetProperty("base_defense").GetInt32()));
        var r=Good(f);Assert.Equal(row.GetProperty("delta").GetInt32(),r.Counters.Hp2Delta);Assert.Equal(row.GetProperty("final").GetInt32(),r.FinalRecordAmount);
        Assert.Equal(Convert.ToUInt32(row.GetProperty("ratio").GetString(),16),r.FloatWords["pve_ratio"]);
        foreach(var word in row.GetProperty("float_words").EnumerateObject())Assert.Equal(Convert.ToUInt32(word.Value.GetString(),16),r.FloatWords[word.Name]);
    }
    private static void Verify(JsonElement row)
    {
        var f=new F(row);var o=C.Calculate(f.Input());Assert.True(o.Complete,o.Failure?.ToString());var r=o.Value!;
        var e=row.GetProperty("outcome");Assert.Equal(e.GetProperty("checked").GetBoolean(),r.CheckPassed);
        // Original117 conflates a successful return/header and omits the known failed return.
        // Apply the separately reviewed F02 overlay without changing historical bytes.
        Assert.Equal(r.CheckPassed?NullableInt(e,"preliminary_return_and_header"):0,r.PreliminaryReturn);Assert.Equal(NullableInt(e,"final_record_damage"),r.FinalRecordAmount);
        var cursor=new U.Cursor(f.Context,r.CalculatedMemory,10000);
        Assert.Equal(Convert.FromHexString(row.GetProperty("window88").GetProperty("after").GetString()!),cursor.Raw(F.Window,88));
        var writes=r.Steps.Where(x=>x.Kind=="Write"&&x.Address>=F.Window&&x.Address<F.Window+88).ToArray();
        var expectedWrites=row.GetProperty("window88").GetProperty("ordered_writes").EnumerateArray().ToArray();Assert.Equal(expectedWrites.Length,writes.Length);
        for(int i=0;i<writes.Length;i++)
        {
            var w=expectedWrites[i];Assert.Equal(F.Window+(ulong)w.GetProperty("offset").GetInt32(),writes[i].Address);
            Assert.Equal(w.GetProperty("before").GetString(),writes[i].Before);Assert.Equal(w.GetProperty("after").GetString(),writes[i].After);
        }
        foreach(var p in row.GetProperty("float_words").EnumerateObject())Assert.Equal(Convert.ToUInt32(p.Value.GetString(),16),r.FloatWords[p.Name]);
        var calls=row.GetProperty("shared_rng_calls").EnumerateArray().ToArray();Assert.Equal(calls.Length,r.RandomCalls.Length);
        for(int i=0;i<calls.Length;i++)
        {
            var expected=calls[i];var actual=r.RandomCalls[i];Assert.Equal(expected.GetProperty("stage").GetString(),actual.Stage);
            Assert.Equal(expected.GetProperty("first").GetInt32(),actual.First);Assert.Equal(expected.GetProperty("second").GetInt32(),actual.Second);Assert.Equal(expected.GetProperty("return").GetInt32(),actual.Value);
            var raw=expected.GetProperty("raw").EnumerateArray().ToArray();Assert.Equal(raw.Length,actual.Draws.Length);
            for(int j=0;j<raw.Length;j++)
            {
                Assert.Equal(Convert.ToUInt64(raw[j].GetProperty("raw_u64").GetString(),16),actual.Draws[j].Value);
                Assert.Equal(raw[j].GetProperty("index_before").GetUInt32(),actual.Draws[j].IndexBefore);
                Assert.Equal(raw[j].GetProperty("index_selected").GetUInt32(),actual.Draws[j].SelectedIndex);
                Assert.Equal(raw[j].GetProperty("index_after").GetUInt32(),actual.Draws[j].IndexAfter);
                Assert.Equal(raw[j].GetProperty("accepted").GetBoolean(),actual.Draws[j].Disposition==RetailRandomDrawDisposition.Accepted);
            }
        }
        Assert.NotNull(r.RandomState);Assert.Equal(row.GetProperty("rng_after").GetProperty("sha256").GetString(),r.RandomState.Sha256);
        Assert.Equal(Convert.FromBase64String(row.GetProperty("rng_after").GetProperty("base64").GetString()!),r.RandomState.Bytes.ToArray());
        Assert.Equal(7ul+(ulong)calls.Length,r.RandomGeneration);Assert.Equal(r.RandomCalls.Sum(x=>x.Draws.Length)*40L,r.RandomTraceBytes);
        Assert.Equal(row.GetProperty("steps").GetProperty("resist_threshold").GetInt32(),r.Counters.ResistThreshold);
        if(r.CheckPassed)
        {
            Assert.NotNull(r.Dispatch);Assert.Equal(F.RunInfo,r.Dispatch.RunInfo);Assert.Equal(0,r.Dispatch.EffectIndex);Assert.Equal(0,r.Dispatch.ArgumentR8);Assert.Equal(0,r.Dispatch.ArgumentR9);
            Assert.Same(r.CalculatedMemory,r.Dispatch.CalculatedMemory);Assert.Same(r.RandomState,r.Dispatch.RandomState);Assert.Equal(r.RandomGeneration,r.Dispatch.RandomGeneration);Assert.Equal(f.Boundary,r.Dispatch.Boundary);
            Assert.Equal(0,r.Dispatch.Stack28);Assert.Equal(0x140bc10eeul,r.Dispatch.CallerSite);Assert.Equal(0x140bb8e50ul,r.Dispatch.Target);
            Assert.Equal(e.GetProperty("begin_fallback").GetInt32(),r.Dispatch.ChanceFallback);
            Assert.Equal(row.GetProperty("steps").GetProperty("boost_quotient_integer").GetInt32(),r.Counters.BoostIntegerQuotient);
            Assert.Equal(row.GetProperty("steps").GetProperty("armor").GetInt32(),r.Counters.Armor);Assert.Equal(row.GetProperty("steps").GetProperty("hp2_delta").GetInt32(),r.Counters.Hp2Delta);
            Assert.Equal(row.GetProperty("steps").GetProperty("pve_final").GetInt32(),r.Counters.PveDefense);
            Assert.Single(r.ConsumedClocks);Assert.DoesNotContain(r.Steps,x=>x.Site==0x140bc10f7||x.Site==0x140bc10d5);
        }
        else
        {
            Assert.Null(r.Dispatch);Assert.Equal(0,r.PreliminaryReturn);Assert.False(e.TryGetProperty("preliminary_return_and_header",out _));Assert.Equal(4984u,e.GetProperty("deadline13e8").GetUInt32());Assert.Single(r.ConsumedClocks);Assert.Equal(new byte[]{0xa5,0xa5,0xa5,0xa5},cursor.Raw(F.User+0x13e8,4));Assert.Throws<U.Domain>(()=>cursor.U32(F.User+0x13e8,0,"unreached deadline"));
            Assert.DoesNotContain(r.Steps,x=>x.Site==0x140bc1071);
        }
        Assert.Null(r.HpResult);Assert.Null(r.OuterReturn);Assert.False(r.ActivationReady);Assert.False(r.CurrentActorObserved);Assert.False(o.PrefixCommittable);
        Assert.Equal(0xa5,cursor.Raw(F.User+0x7fff,1)[0]);
    }
    private static int? NullableInt(JsonElement element,string key)=>element.TryGetProperty(key,out var value)&&value.ValueKind!=JsonValueKind.Null?value.GetInt32():null;
    private static F Basic()=>new(F.Row("binding_17039_212770_live"));
    private static C.Result Good(F f){var o=C.Calculate(f.Input());Assert.True(o.Complete,o.Failure?.ToString());return o.Value!;}
    private static C.Outcome Bad(F f){var o=C.Calculate(f.Input());Assert.False(o.Complete);Assert.Null(o.Value);Assert.NotNull(o.Failure);Assert.False(o.PrefixCommittable);if(o.KnownPrefix is not null)Assert.False(o.KnownPrefix.Committable);return o;}

    [Theory]
    [InlineData(0)] [InlineData(1000)]
    public void TwoMathematicalR2RowsRequireTheExplicit16725BridgeNotTheirOriginal17039Labels(int boost)
    {
        var row=F.Row("effective_singleton100_R2_135_boost"+boost);var f=new F(row,false);f.W32(F.Skill+0x2dc,135);
        Assert.Equal("stale-skill",Bad(f).Failure!.Code);Assert.Equal(boost==0?234:469,Good(new F(row)).FinalRecordAmount);
    }
    [Fact]
    public void SourceMinimumAndMaximumAreEffectiveUshortsAndAreNotRawMagicalAttack()
    {
        var f=Basic();f.W16(F.Npc+0x14dc,100);f.W16(F.Npc+0x14e0,100);var r=Good(f);
        Assert.Empty(Assert.Single(r.RandomCalls,x=>x.Stage=="CalcMagic.NpcBase").Draws);Assert.Equal(123,r.PreliminaryReturn);
        var swapped=Basic();swapped.W16(F.Npc+0x14dc,272);swapped.W16(F.Npc+0x14e0,181);
        var ordinary=Basic();ordinary.W16(F.Npc+0x14dc,181);ordinary.W16(F.Npc+0x14e0,272);
        Assert.Equal(Good(ordinary).FinalRecordAmount,Good(swapped).FinalRecordAmount);
    }
    [Fact]
    public void Signed32MdefHasTheIndependentHighBitOutcome()
    {var f=new F(F.Row("raw_mdef_high_bit_signed"));var r=Good(f);Assert.Equal(-214748368,r.Counters.Armor);Assert.Equal(198229472,r.FinalRecordAmount);}
    [Fact]
    public void OccupiedMissingFlagDoesNotReadUnknownBackup()
    {
        var f=Basic();f.W16(F.Primary,1);f.W32(F.Primary+0x214,0);f.W16(F.Primary+0xbc,0);f.Forget(F.User+0x6e38,8);
        Assert.Equal(186,Good(f).FinalRecordAmount);
    }
    [Fact]
    public void ExactSameSnapshotAliasUsesOneRawView()
    {var f=Basic();f.W64(F.User+0x6e38,F.Primary);Assert.Equal(186,Good(f).FinalRecordAmount);}
    [Fact]
    public void CachedNoncanonicalTrueSkipsUnknownCriticalOperandsAndOriginalCriticalDraw()
    {
        var f=Basic();f.W32(F.Window+0x54,7);f.W32(F.Window+0x50,0);f.Forget(F.Npc+0x14c0,2);f.Forget(F.Npc+0x208,8);f.Forget(F.User+0x14c8,2);f.Forget(F.User+0x14cc,2);
        var r=Good(f);Assert.Equal(186,r.FinalRecordAmount);Assert.DoesNotContain(r.RandomCalls,x=>x.Stage=="CalcMagic.uncachedCritical");
        Assert.DoesNotContain(r.Steps,x=>x.Kind=="Write"&&(x.Address==F.Window+0x50||x.Address==F.Window+0x54));
    }
    [Fact]
    public void CriticalDrawPrecedesEveryReachedCriticalGetterAndCachePresentPrecedesDecision()
    {
        var r=Good(Basic());var draw=Assert.Single(r.Steps,x=>x.Kind=="ComputedRng"&&x.Operand=="CalcMagic.uncachedCritical");
        var get=Assert.Single(r.Steps,x=>x.Operand=="Npc.critical14c0");Assert.True(draw.Sequence<get.Sequence);
        var present=Assert.Single(r.Steps,x=>x.Kind=="Write"&&x.Address==F.Window+0x54);var decision=Assert.Single(r.Steps,x=>x.Kind=="Write"&&x.Address==F.Window+0x50);Assert.True(present.Sequence<decision.Sequence);
    }
    [Theory]
    [InlineData(1331,1,0)] [InlineData(1332,0,0)] [InlineData(666,0,1)] [InlineData(667,2,1)]
    public void OriginalMovementStrictAgesSelectExactlyTheNativeFlags(uint age,int expected,int channel)
    {
        var f=Basic();byte[] raw=new byte[16];raw[channel*8]=(byte)(channel==0?1:2);BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(channel*8+4),unchecked(0u-age));f.Put(F.User+0x4084,raw);
        f.Clocks=f.Clocks.SetItem(0,f.Clocks[0] with {Tick=0});var r=Good(f);Assert.Equal((long)expected,Assert.Single(r.Steps,x=>x.Operand=="User.movement").Value);
    }
    [Fact]
    public void FailedStrictEqualResistSkipsUnknownRecordBaseBeginAndUnmappedDeadlineWithoutExtraClock()
    {
        var f=new F(F.Row("resist_strict_equal1"));f.Forget(F.Npc+0x14dc,2);f.Forget(F.Skill+0x4a0,8);f.Forget(F.Window,88);
        var r=Good(f);Assert.False(r.CheckPassed);Assert.Equal(0,r.PreliminaryReturn);Assert.Single(r.RandomCalls);Assert.Null(r.FinalRecordAmount);Assert.DoesNotContain(r.Steps,x=>x.Kind=="Write"&&x.Address>=F.Window&&x.Address<F.Window+88);
        var c=new U.Cursor(f.Context,r.CalculatedMemory,100);Assert.Equal(new byte[]{0xa5,0xa5,0xa5,0xa5},c.Raw(F.User+0x13e8,4));Assert.Throws<U.Domain>(()=>c.U32(F.User+0x13e8,0,"unknown/unmapped deadline"));Assert.DoesNotContain(r.Steps,x=>x.Address==F.User+0x13e8);
    }
    [Fact]
    public void OrdinaryFailedRollPreservesAKnownDeadlineAndRejectsAnInventedExtraClock()
    {
        var f=new F(F.Row("resist_strict_equal1"));f.W32(F.User+0x13e8,0x12345678);var r=Good(f);
        var c=new U.Cursor(f.Context,r.CalculatedMemory,100);Assert.Equal(0x12345678u,c.U32(F.User+0x13e8,0,"unchanged old deadline"));
        f.Clocks=f.Clocks.Add(f.Clock(C.ClockRole.UserMovement,0x1400b4640,0x1400b4650,0xfffffff0));
        var o=Bad(f);Assert.Equal("clock",o.Failure!.Code);Assert.Single(o.KnownPrefix!.RandomCalls);Assert.DoesNotContain(o.KnownPrefix.Steps,x=>x.Address==F.User+0x13e8);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)]
    public void ForcedCounterNonpositiveComputesOrdinaryResist(int value)
    {
        var f=Basic();f.Add(F.UserManager,0x4460,"User-manager");f.W64(F.User+0x158,F.UserManager);f.W32(F.UserManager+0x4400,unchecked((uint)value));Assert.Equal(186,Good(f).FinalRecordAmount);
    }
    [Fact]
    public void ActiveForceRetainsOnlyItsReachedReadPrefixAndDoesNotConsumeOriginalDraw()
    {
        var f=Basic();f.Add(F.UserManager,0x4460,"User-manager");f.W64(F.User+0x158,F.UserManager);f.W32(F.UserManager+0x4400,1);var o=Bad(f);
        Assert.Equal("forced-resist-owner",o.Failure!.Operand);Assert.Empty(o.KnownPrefix!.RandomCalls);Assert.Contains(o.KnownPrefix.Steps,x=>x.Operand=="User.forced_counter4400");
    }
    [Fact]
    public void InactiveNonNullOneTimeStillReadsEveryOrderedManagerInput()
    {
        var f=Basic();f.Add(F.NpcManager,0x4460,"Npc-manager");f.W64(F.Npc+0x158,F.NpcManager);f.W64(F.Npc+0x200,1);f.W64(F.Npc+0x208,1);f.W32(F.NpcManager+0x88,1);
        foreach(int off in new[]{0x4434,0x4438,0x443c,0x4440,0x4444,0x4450,0x4454})f.W32(F.NpcManager+(ulong)off,0);
        var r=Good(f);Assert.Equal(186,r.FinalRecordAmount);
        var reads=r.Steps.Where(x=>x.Kind=="Read"&&x.Operand.StartsWith("one_time.",StringComparison.Ordinal)).Select(x=>x.Address).ToArray();
        Assert.Equal(new ulong[]{F.NpcManager+0x443c,F.NpcManager+0x4440,F.NpcManager+0x4444,F.NpcManager+0x4438,F.NpcManager+0x4444,F.NpcManager+0x4434,F.NpcManager+0x4454,F.NpcManager+0x4450},reads);
    }
    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void ActiveOneTimeMask2WithholdsCandidateAtItsExactStage(int stage)
    {
        var f=Basic();f.Add(F.NpcManager,0x4460,"Npc-manager");f.W64(F.Npc+0x158,F.NpcManager);f.W32(F.NpcManager+0x88,1);
        foreach(int off in new[]{0x4434,0x4438,0x443c,0x4440,0x4444,0x4450,0x4454})f.W32(F.NpcManager+(ulong)off,0);
        f.W64(F.Npc+(stage==2?0x208ul:0x200ul),1);f.W32(F.NpcManager+(stage==2?0x4454ul:0x4444ul),2);
        var o=Bad(f);Assert.Equal("one-time.active_mask2",o.Failure!.Operand);Assert.Equal(stage==2?3:0,o.KnownPrefix!.RandomCalls.Length);
    }
    [Theory]
    [InlineData(0x1498ul,4)] [InlineData(0x149cul,2)] [InlineData(0x1504ul,2)] [InlineData(0x774ul,4)] [InlineData(0x778ul,4)] [InlineData(0x8008ul,8)]
    public void UnknownReachedUserDataProducesNoCommittableCandidate(ulong offset,int count)
    {var f=Basic();f.Forget(F.User+offset,count);var o=Bad(f);Assert.Equal("unknown",o.Failure!.Code);}
    [Fact]
    public void MissingSnapshotTailIsARealReadFailureAfterEarlierStages()
    {
        var f=new F(F.Row("binding_17039_212770_signed_snapshot_cache"));f.Forget(F.Primary+0x210,4);var o=Bad(f);
        Assert.Equal("unknown",o.Failure!.Code);Assert.NotEmpty(o.KnownPrefix!.RandomCalls);Assert.Null(o.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void LateUnknownOrActiveTailPreservesCalculatedPrefixWithoutPublishingDamage(int which)
    {
        var f=Basic();switch(which){case 0:f.Forget(F.Template+0x450,1);break;case 1:f.W8(F.Template+0x450,1);break;case 2:f.Forget(F.Skill+0x4a0,4);break;default:f.Forget(F.Window+0x54,4);break;}
        var o=Bad(f);Assert.NotEmpty(o.KnownPrefix!.RandomCalls);
        if(which==2)Assert.Contains(o.KnownPrefix.Steps,x=>x.Kind=="Write"&&x.Address==F.Window+4);
        else Assert.DoesNotContain(o.KnownPrefix.Steps,x=>x.Kind=="Write"&&x.Address==F.Window+4);
    }
    [Fact]
    public void LateLookupSelectedEntryIsReadEvenWhenCountRejectsIt()
    {var f=Basic();f.W32(0x14f398f40,0);f.Forget(F.Lookup+(212770ul-200000)*8,8);Assert.Equal("lookup.entry_before_count_compare",Bad(f).Failure!.Operand);}
    [Fact]
    public void NullLookupTableSkipsUnknownEntryAndSourceTemplateExclusiveField()
    {var f=Basic();f.W64(0x14f398f30,0);f.Forget(F.Template+0x450,1);Assert.Equal(186,Good(f).FinalRecordAmount);}
    [Fact]
    public void Hp2AbsoluteNineteenAndMaximumThenCurrentReadOrderArePreserved()
    {
        var f=new F(F.Row("HP2_absolute_binary32_delta19"));var r=Good(f);Assert.Equal(19,r.Counters.Hp2Delta);Assert.Equal(163,r.FinalRecordAmount);
        var maximum=Assert.Single(r.Steps,x=>x.Operand=="User.maximumHP774");var current=Assert.Single(r.Steps,x=>x.Operand=="User.currentHP778");var program=Assert.Single(r.Steps,x=>x.Operand=="User.HP2_program8008");Assert.True(maximum.Sequence<current.Sequence&&current.Sequence<program.Sequence);
    }
    [Fact]
    public void Hp2FirstMatchingRowSkipsUnknownLaterRows()
    {var f=new F(F.Row("HP2_absolute_binary32_delta19"));f.Forget(F.Program+16,16);Assert.Equal(163,Good(f).FinalRecordAmount);}
    [Fact]
    public void Hp2RawTagAndNoMatchBranchesAreLazy()
    {
        var f=new F(F.Row("HP2_absolute_binary32_delta19"));f.W16(F.Program+2,0);f.Forget(F.Program+4,28);Assert.Equal(167,Good(f).FinalRecordAmount);
        f=new F(F.Row("HP2_absolute_binary32_delta19"));foreach(int off in new[]{10,18,26})f.W16(F.Program+(ulong)off,0);f.Forget(F.Program,1);Assert.Equal(167,Good(f).FinalRecordAmount);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Hp2EqualWindowUnknownRowOrUnsupportedAliasStopsWholeResult(int which)
    {
        var f=new F(F.Row("HP2_absolute_binary32_delta19"));
        switch(which){case 0:f.W16(F.Program+4,80);f.W16(F.Program+6,80);break;case 1:f.Forget(F.Program+12,2);break;default:f.W64(F.User+0x8008,F.Window);break;}
        Bad(f);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void LateRandomOperationBudgetRetainsTruthfulNoAnswerPrefix(int operations)
    {var f=Basic();f.Limits=f.Limits with {MaxRandomOperations=operations};var o=Bad(f);Assert.Equal(operations,o.KnownPrefix!.RandomCalls.Length);Assert.Null(o.Value);}
    [Fact]
    public void RejectionBudgetFailureRetainsItsActualDiagnosticEnginePostimage()
    {
        var f=new F(F.Row("original_first_resist_raw_rejection"));f.Limits=f.Limits with {MaxRawDraws=1};var o=Bad(f);
        Assert.NotNull(o.KnownPrefix!.RandomFailure);Assert.Equal(RetailRandomStatus.BudgetExceeded,o.KnownPrefix.RandomFailure.Status);Assert.Single(o.KnownPrefix.RandomFailure.Draws);Assert.Equal(RetailRandomDrawDisposition.Rejected,o.KnownPrefix.RandomFailure.Draws[0].Disposition);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TraceAndDrawCapacitiesAreExplicitAndCumulative(int which)
    {var f=Basic();f.Limits=which switch{0=>f.Limits with {MaxSteps=10},1=>f.Limits with {MaxRawDraws=2},_=>f.Limits with {MaxRandomLogicalTraceBytes=119}};Bad(f);}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void EveryActorCurrentSourceLifetimeAndEnvironmentGateIsIndependent(int which)
    {
        var f=Basic();switch(which)
        {
            case 0:f.W64(F.User,U.NpcVtable);break;case 1:f.W32(F.User+0x144,2);break;case 2:f.W32(F.Npc+0x188c,1);break;case 3:f.W32(F.Npc+0x1894,1);break;
            case 4:f.W64(F.Npc+0x1850,1);break;case 5:f.W64(F.Npc+0x1bf8,1);break;case 6:f.W32(F.Npc+0x914,1);break;case 7:f.W32(F.Template+0x434,0x200000);break;
            case 8:f.Boundary=f.Boundary with {SkillGeneration="stale"};break;case 9:f.Context=f.Context with {Sources=f.Context.Sources with {Dataset="other"}};break;
            case 10:f.Floating=f.Floating with {NearestEven=null};break;default:f.Infrastructure=f.Infrastructure with {ExclusiveSerializedLease=null};break;
        }Bad(f);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void OriginalCallerCannotBeReplacedByAnotherLevelFrameOrOutputOwner(int which)
    {
        var f=Basic();switch(which){case 0:f.W64(F.Frame+0x1c8,F.Window+4);break;case 1:f.W64(F.RunInfo+0x50,F.Npc);break;case 2:f.W16(F.RunInfo+0x38,0xffff);break;case 3:f.Boundary=f.Boundary with {Window88=F.Npc+0x100};break;default:f.Boundary=f.Boundary with {CallerGeneration="stale"};break;}Bad(f);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void OriginalRegisterOperandsMustIndependentlyMatchTheRawRunInfo(int which)
    {
        var f=Basic();switch(which){case 0:f.W64(F.Registers,F.RunInfo);break;case 1:f.W16(F.Registers+8,22);break;default:f.W32(F.Registers+16,1);break;}
        Assert.Equal("stale-caller",Bad(f).Failure!.Code);
    }
    [Fact]
    public void RandomLifetimeAndOrderMustBindTheSameExplicitSerializedInvocation()
    {var f=Basic();f.Random=f.Random with {Ownership=f.Random.Ownership! with {OrderIdentity="other interval"}};Assert.Equal("rng-source",Bad(f).Failure!.Code);}
    [Fact]
    public void ManagedCurrentSnapshotIsAllowedWithoutPretendingObservedNativeMemory()
    {
        var f=Basic();f.Context=f.Context with {Origin=U.Origin.DeclaredCurrentSnapshot};f.Random=f.Random with {Origin=RetailRandomInputOrigin.DeclaredCurrentMemory,Ownership=f.Random.Ownership! with {Origin=RetailRandomInputOrigin.DeclaredCurrentMemory}};
        var r=Good(f);Assert.Equal(186,r.FinalRecordAmount);Assert.False(r.CurrentActorObserved);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void ActiveDebugTelemetryLockAndAttenuationChildrenDoNotBecomeAssumedSuccess(int which)
    {
        var f=Basic();switch(which){case 0:f.W32(0x14276916c,1);f.W32(0x142769b90,0x100);break;case 1:f.W32(0x14275eb74,1);break;case 2:f.W32(F.User+0x4094,1);break;default:f.W64(F.Skill+0xe10,1);break;}
        Assert.Equal("incomplete-child",Bad(f).Failure!.Code);
    }
    [Fact]
    public void PinnedMovementWordsCannotBeSubstitutedWithAnInventedRow()
    {var f=Basic();f.W32(0x141069ed0,100);Assert.Equal("source-table",Bad(f).Failure!.Code);}
    [Fact]
    public void BeginChanceHundredStillDrawsAndLeavesTheLaterOuterReturnCellUnknown()
    {
        var f=Basic();f.Forget(F.RunInfo+0x60,8);var r=Good(f);var last=r.RandomCalls[^1];Assert.Equal("Begin.always_1_100",last.Stage);Assert.Equal(1,last.First);Assert.Equal(100,last.Second);Assert.NotEmpty(last.Draws);
        Assert.Null(r.OuterReturn);Assert.Null(r.HpResult);Assert.DoesNotContain(r.Steps,x=>x.Site==0x140bc10f7);
        Assert.Throws<U.Domain>(()=>C.BeginDispatch.Create(new object(),F.RunInfo,0,f.Context,f.Selected,f.Memory(),null,0,f.Boundary));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void OriginalClockRoleLifeAndUnusedClockTailsCannotBeSubstituted(int which)
    {
        var f=Basic();f.Clocks=which switch{0=>[],1=>f.Clocks.SetItem(0,f.Clocks[0] with {Life="stale"}),_=>f.Clocks.Add(f.Clock(C.ClockRole.UserMovement,0x140bc0d54,0x140578ec9,100))};Assert.Equal("clock",Bad(f).Failure!.Code);
    }
    [Theory]
    [InlineData(0x00000001u)] [InlineData(0x7f800000u)] [InlineData(0x7fc00000u)]
    public void CurrentSelectedFloatOperandsDoNotAdmitUnknownExceptionalSourceRecipes(uint bits)
    {var f=Basic();f.W32(F.Skill+0x450,bits);Assert.Equal("stale-skill",Bad(f).Failure!.Code);}
    [Theory]
    [InlineData(0x00000001u)] [InlineData(0x7f800000u)] [InlineData(0x7fc00000u)] [InlineData(0x4f000000u)]
    public void FinitePrimitiveRejectsSubnormalNonfiniteAndUnrepresentableCvtt(uint bits)
    {Assert.Throws<U.Domain>(()=>U.Cvtt(BitConverter.UInt32BitsToSingle(bits)));}
    [Fact]
    public void NullReachedClockIsTypedFailureWithEarlierFramePrefixAndNoMovementOrRandomMutation()
    {
        var f=Basic();f.Clocks=[null!];var o=Bad(f);Assert.Equal("clock",o.Failure!.Code);Assert.NotNull(o.KnownPrefix);
        Assert.Empty(o.KnownPrefix.Clocks);Assert.Empty(o.KnownPrefix.RandomCalls);Assert.Null(o.KnownPrefix.RandomState);
        Assert.Contains(o.KnownPrefix.Steps,x=>x.Kind=="Write"&&x.Address==F.Frame+0x110);
        Assert.DoesNotContain(o.KnownPrefix.Steps,x=>x.Address==F.User+0x4094||x.Kind=="OriginalClock");
        var c=new U.Cursor(f.Context,o.KnownPrefix.Memory!,100);Assert.Equal(0u,c.U32(F.User+0x4094,0,"unchanged movement lock"));
        Assert.Equal(Convert.FromHexString(F.Row("binding_17039_212770_live").GetProperty("window88").GetProperty("before").GetString()!),c.Raw(F.Window,88));
    }
    [Fact]
    public void UnreachedNullClockTailIsRejectedAtTailWithoutDereferenceOrDiscardingCalculatedPrefix()
    {
        var f=Basic();f.Clocks=f.Clocks.Add(null!);var o=Bad(f);Assert.Equal("clock",o.Failure!.Code);Assert.Equal("unused tail",o.Failure.Operand);
        Assert.Single(o.KnownPrefix!.Clocks);Assert.Equal(4,o.KnownPrefix.RandomCalls.Length);Assert.Contains(o.KnownPrefix.Steps,x=>x.Kind=="Write"&&x.Address==F.Window+4);
    }
    [Fact]
    public void KnownNullManagerComputesResistAndDamageInactiveDefaultsWithoutManagerFieldsOrScratch()
    {
        var f=Basic();f.W64(F.Npc+0x200,1);f.W64(F.Npc+0x158,0);f.Forget(F.Npc+0x208,8);f.W32(F.Window+0x54,1);f.W32(F.Window+0x50,0);
        var baseline=Basic();baseline.Forget(F.Npc+0x208,8);baseline.W32(F.Window+0x54,1);baseline.W32(F.Window+0x50,0);
        SameInactiveOutput(Good(baseline),Good(f));var r=Good(f);
        Assert.Equal(new ulong[]{0x14036f45d,0x14036fff4},r.Steps.Where(x=>x.Kind=="ComputedInactiveDefault").Select(x=>x.Site));
        Assert.All(r.Steps.Where(x=>x.Kind=="ComputedInactiveDefault"),x=>Assert.Equal(1L,x.Value));
        Assert.DoesNotContain(r.Steps,x=>x.Operand=="Npc.manager_lock_mode"||x.Operand=="Npc.manager_lock_mode_after"||x.Operand.StartsWith("one_time.resist_",StringComparison.Ordinal)||x.Operand.StartsWith("one_time.damage_",StringComparison.Ordinal));
    }
    [Fact]
    public void KnownNullManagerComputesCriticalInactiveDefaultAfterItsOriginalDraw()
    {
        var f=Basic();f.W64(F.Npc+0x208,1);f.W64(F.Npc+0x158,0);var r=Good(f);SameInactiveOutput(Good(Basic()),r);
        var note=Assert.Single(r.Steps,x=>x.Kind=="ComputedInactiveDefault");Assert.Equal(0x1403703bdul,note.Site);Assert.Equal(1L,note.Value);
        Assert.True(Assert.Single(r.Steps,x=>x.Kind=="ComputedRng"&&x.Operand=="CalcMagic.uncachedCritical").Sequence<note.Sequence);
        Assert.DoesNotContain(r.Steps,x=>x.Operand=="one_time.critical_flags"||x.Operand=="one_time.critical_threshold"||x.Operand=="Npc.manager_lock_mode");
    }
    [Fact]
    public void BothNonnullMarkersAndNullManagerCalculateAllThreeDefaultsWithoutAFabricatedManagerImage()
    {
        var f=Basic();f.W64(F.Npc+0x200,1);f.W64(F.Npc+0x208,1);f.W64(F.Npc+0x158,0);var r=Good(f);SameInactiveOutput(Good(Basic()),r);
        Assert.Equal(new ulong[]{0x14036f45d,0x14036fff4,0x1403703bd},r.Steps.Where(x=>x.Kind=="ComputedInactiveDefault").Select(x=>x.Site));
        Assert.DoesNotContain(r.Steps,x=>x.Address>=F.NpcManager&&x.Address<F.NpcManager+0x4460);
    }
    private static void SameInactiveOutput(C.Result expected,C.Result actual)
    {
        Assert.Equal(expected.CheckPassed,actual.CheckPassed);Assert.Equal(expected.PreliminaryReturn,actual.PreliminaryReturn);Assert.Equal(expected.FinalRecordAmount,actual.FinalRecordAmount);
        Assert.Equal(expected.RandomState!.Bytes.ToArray(),actual.RandomState!.Bytes.ToArray());Assert.Equal(expected.RandomGeneration,actual.RandomGeneration);
        Assert.Equal(expected.RandomCalls.Select(x=>(x.Stage,x.Value,x.First,x.Second)),actual.RandomCalls.Select(x=>(x.Stage,x.Value,x.First,x.Second)));
        Assert.Equal(expected.ConsumedClocks.ToArray(),actual.ConsumedClocks.ToArray());
        var a=new U.Cursor(F.ContextValue,expected.CalculatedMemory,100);var b=new U.Cursor(F.ContextValue,actual.CalculatedMemory,100);Assert.Equal(a.Raw(F.Window,88),b.Raw(F.Window,88));
        Assert.Equal(expected.Dispatch!.ChanceFallback,actual.Dispatch!.ChanceFallback);
    }
}
