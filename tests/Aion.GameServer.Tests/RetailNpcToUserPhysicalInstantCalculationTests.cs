using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Aion.GameServer.Commons.Utils.RetailRandom;
using C = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserPhysicalInstantCalculation;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using F = Aion.GameServer.Tests.UserPhysicalFixture;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcToUserPhysicalInstantCalculationTests
{
    public static IEnumerable<object[]> Transactions()=>F.Extra.RootElement.GetProperty("cases").EnumerateArray()
        .Select(x=>new object[]{x.GetProperty("name").GetString()!});
    [Theory,MemberData(nameof(Transactions))]
    public void CompleteActualUserTransactionsMatchIndependentWordsAndFullRngState(string name)
    {
        JsonElement row=Row(name); var f=Recipe(row);var outcome=C.Calculate(f.Input());Assert.True(outcome.Success,outcome.Failure?.ToString());
        var r=outcome.Value!;var e=row.GetProperty("expected"); Assert.Equal(e.GetProperty("hit").GetBoolean(),r.Hit);
        Assert.Equal(r.Hit,r.BeginInvoked); Assert.Equal(Int(e,"preliminary"),r.PreliminaryReturn);Assert.Equal(Int(e,"record"),r.FinalRecordDamage);
        var calls=row.GetProperty("calls").EnumerateArray().ToArray();Assert.Equal(calls.Length,r.RandomCalls.Length);
        for(int i=0;i<calls.Length;i++)
        {
            var want=calls[i];var actual=r.RandomCalls[i];Assert.Equal(want.GetProperty("stage").GetString(),actual.Stage);
            Assert.Equal(want.GetProperty("first").GetInt32(),actual.First);Assert.Equal(want.GetProperty("second").GetInt32(),actual.Second);
            Assert.Equal(want.GetProperty("value").GetInt32(),actual.ComputedReturn);
            var raw=want.GetProperty("trace").EnumerateArray().ToArray();Assert.Equal(raw.Length,actual.RawDraws.Length);
            for(int k=0;k<raw.Length;k++)
            {
                Assert.Equal(Convert.ToUInt64(raw[k].GetProperty("raw_u64").GetString(),16),actual.RawDraws[k].Value);
                Assert.Equal(raw[k].GetProperty("index_before").GetUInt32(),actual.RawDraws[k].IndexBefore);
                Assert.Equal(raw[k].GetProperty("stage").GetString(),actual.RawDraws[k].Regeneration.ToString());
                Assert.Equal(raw[k].GetProperty("accepted").GetBoolean(),actual.RawDraws[k].Disposition==RetailRandomDrawDisposition.Accepted);
            }
        }
        Assert.NotNull(r.RandomStateAfter);Assert.Equal(row.GetProperty("final_rng").GetProperty("sha256").GetString(),r.RandomStateAfter.Sha256);
        Assert.Equal(Convert.FromBase64String(row.GetProperty("final_rng").GetProperty("base64").GetString()!),r.RandomStateAfter.Bytes.ToArray());
        Assert.Equal(7ul+(ulong)calls.Length,r.RandomGenerationAfter);Assert.Equal(r.RandomCalls.Sum(x=>x.RawDraws.Length)*40L,r.RandomLogicalTraceBytes);
        Assert.Equal(row.GetProperty("clocks").EnumerateArray().Select(x=>x.GetString()),r.ConsumedClocks.Select(x=>x.Role.ToString()));
        var cursor=new U.Cursor(f.Context,r.CalculatedMemory,1000);
        if(r.Hit)
        {
            Assert.Equal(e.GetProperty("header").GetUInt32(),cursor.U32(F.Frame,0,"header"));
            Assert.Equal((byte)e.GetProperty("kind").GetInt32(),cursor.U8(F.Frame+0xa0+0x30,0,"kind"));
            Assert.Equal(e.GetProperty("critical").GetUInt32(),cursor.U32(F.Cache+4,0,"critical"));
            Assert.Equal((uint)r.FinalRecordDamage!.Value,cursor.U32(F.Frame+0xa0,0,"damage"));
            foreach(var value in e.GetProperty("floats").EnumerateObject()) Assert.Equal(Convert.ToUInt32(value.Value.GetString(),16),r.FloatBits[value.Name]);
            Assert.NotNull(r.Pve);Assert.True(Array.IndexOf(r.Pve.Reads.Select(x=>x.Operand).ToArray(),"User+774:slot60")<
                Array.IndexOf(r.Pve.Reads.Select(x=>x.Operand).ToArray(),"User+778:slot59"));
            Assert.Equal(new byte[]{0xa5,0xa5,0xa5},cursor.Raw(F.Frame+0xa0+53,3));
        }
        else Assert.DoesNotContain(r.Steps,x=>x.Kind=="Write"&&x.Address==F.Frame);
        if(e.GetProperty("counter").ValueKind!=JsonValueKind.Null)
        {
            ulong off=r.ConsumedClocks[^1].Role switch {C.ClockRole.DodgeCounter=>0x13e4ul,C.ClockRole.ParryCounter=>0x13e0ul,_=>0x13dcul};
            Assert.Equal(e.GetProperty("counter").GetUInt32(),cursor.U32(F.User+off,0,"counter"));
        }
        Assert.Null(r.HpResult);Assert.Null(r.ApplicationCell);Assert.False(r.HostEffectsExecuted);Assert.False(r.CurrentActorObserved);
        Assert.False(r.ActivationReady);Assert.False(r.GlobalNativeRngOrderProved);Assert.False(outcome.DiagnosticPrefixCommittable);
    }
    [Fact]
    public void TwoOriginalTicksSelectDifferentHitAndDefenseRows()
    {
        var f=new F();f.W8(F.User+0x408c,1);f.W32(F.User+0x4090,0);f.W32(0x141069ed0ul+28+4,100);
        f.Clocks=f.Clocks.SetItem(0,f.Clocks[0] with {Value=666}).SetItem(1,f.Clocks[1] with {Value=667});
        var r=Good(f);Assert.Equal(0x43cf0000u,r.FloatBits["after_pdef_and_ratio"]);Assert.Equal(575,r.FinalRecordDamage);
        Assert.Contains(r.Steps,x=>x.Kind=="Read"&&x.Address==0x141069ed0ul+5*4);
        Assert.Contains(r.Steps,x=>x.Kind=="Read"&&x.Address==0x141069ed0ul+28+4);
        Assert.Equal(new uint[]{666,667,2000},r.ConsumedClocks.Select(x=>x.Value));
    }
    [Fact]
    public void InclusiveNaturalBlockEqualityStillWinsAt385()
    {var f=new F();f.W16(F.User+0x14b6,885);var r=Good(f);Assert.Equal(605,r.FinalRecordDamage);Assert.Equal(385L,Assert.Single(r.Steps,x=>x.Operand=="block.threshold").Value);}
    [Fact]
    public void InclusiveNaturalParryEqualityStillWinsAt385()
    {var f=Recipe(Row("natural_User_parry_point_six"));f.W16(F.User+0x14b4,885);var r=Good(f);Assert.Equal(390,r.FinalRecordDamage);Assert.Equal(385L,Assert.Single(r.Steps,x=>x.Operand=="parry.threshold").Value);}
    [Fact]
    public void InclusiveDodgeEqualityStopsBeforeUnreachedCalculator()
    {
        var f=Recipe(Row("natural_dodge_terminates_before_Begin"));f.W16(F.User+0x14ae,501);f.Forget(F.Skill+0x4d4,1);
        var r=Good(f);Assert.False(r.Hit);Assert.Null(r.FinalRecordDamage);Assert.Single(r.RandomCalls);Assert.DoesNotContain(r.Steps,x=>x.Operand=="Skill.sharing_flag4d4");
    }
    [Fact]
    public void ActualUserLevelR7AndUnusedSourceQueryStayUnknown()
    {
        var f=new F();f.Forget(F.User+0x1492,2);f.Forget(F.Skill+0x47c,1);f.Forget(F.Npc+0x1bd8,4);f.Forget(F.RunInfo+0x90,2);
        Assert.Equal(605,Good(f).FinalRecordDamage);
    }
    [Fact]
    public void CurrentDeclarationNeverClaimsToObserveAnActor()
    {
        var f=new F();f.Context=f.Context with {Origin=U.Origin.DeclaredCurrentSnapshot};
        f.Random=f.Random with {Origin=RetailRandomInputOrigin.DeclaredCurrentMemory,Ownership=f.Random.Ownership! with {Origin=RetailRandomInputOrigin.DeclaredCurrentMemory}};
        var r=Good(f);Assert.False(r.CurrentActorObserved);Assert.Equal(605,r.FinalRecordDamage);
    }
    [Theory]
    [InlineData(0x14cauL,2)] [InlineData(0x774uL,4)] [InlineData(0x778uL,4)] [InlineData(0x8008uL,8)] [InlineData(0x3a78uL,4)]
    public void MissingLateConsumedUserDataWithholdsEveryCandidate(ulong field,int count)
    {
        var f=new F();if(field==0x14ca)f.W16(F.Npc+0x14bc,0);f.Forget(F.User+field,count);
        var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Null(o.Value);Assert.False(o.DiagnosticPrefixCommittable);
        Assert.NotNull(o.Failure);Assert.Contains(o.Failure.Code,new[]{"unknown","modifier-unknown"});
    }
    [Fact]
    public void ProgramConsumedFieldUnknownRejectsWhileUnconsumedRowsAreAllowed()
    {
        var f=Recipe(Row("HP2_computed_absolute_truncates_nineteen"));f.Forget(0x73000000+12,2);
        var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Equal("unknown",o.Failure!.Code);Assert.Null(o.Value);
        Assert.Equal(593,Good(Recipe(Row("HP2_computed_absolute_truncates_nineteen"))).FinalRecordDamage);
    }
    [Fact]
    public void LateProgramCannotAliasTheWrapperConditionOutput()
    {
        var f=new F();f.W64(F.User+0x8008,F.Frame+0x70);var o=C.Calculate(f.Input());
        Assert.False(o.Success);Assert.Equal("alias",o.Failure!.Code);Assert.Null(o.Value);
    }
    [Fact]
    public void LateClassLookupDoesNotUseTheSourceTemplateAsASubstitute()
    {
        var f=new F();f.W64(F.Lookup+(212776-200000)*8,0);f.Forget(F.Template+0x450,1);Assert.Equal(605,Good(f).FinalRecordDamage);
        f.W64(F.Lookup+(212776-200000)*8,F.Template);var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Equal("unknown",o.Failure!.Code);
    }
    [Fact]
    public void LookupStillDereferencesUnknownSelectedCellWhenCountRejectsIt()
    {
        var f=new F();f.W32(0x14f398f40,0);f.Forget(F.Lookup+(212776-200000)*8,8);var o=C.Calculate(f.Input());
        Assert.False(o.Success);Assert.Equal("class_lookup.selected_before_count_compare",o.Failure!.Operand);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void CapacitiesAreCumulativeAndRngPrefixIsDiagnosticOnly(int budget)
    {
        var f=new F();f.Limits=f.Limits with {MaxRawDraws=budget};var o=C.Calculate(f.Input());
        Assert.False(o.Success);Assert.Null(o.Value);Assert.NotNull(o.DiagnosticRandomPrefix);Assert.Equal(RetailRandomStatus.BudgetExceeded,o.DiagnosticRandomPrefix.Status);
        Assert.False(o.DiagnosticPrefixCommittable);Assert.StartsWith("rng-",o.Failure!.Code);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TraceOperationAndStepCapacityFailuresDoNotPublishDamage(int which)
    {
        var f=new F();f.Limits=which switch {0=>f.Limits with {MaxRandomLogicalTraceBytes=199},1=>f.Limits with {MaxRandomOperations=5},_=>f.Limits with {MaxSteps=5}};
        var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Null(o.Value);Assert.False(o.DiagnosticPrefixCommittable);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void ActualActorSourceEnvironmentAndClockContractsRejectMismatches(int which)
    {
        var f=new F();
        switch(which)
        {
            case 0:f.W32(F.User+0x144,2);break;
            case 1:f.W64(F.User,U.NpcVtable);break;
            case 2:f.Context=f.Context with {Origin=(U.Origin)99};break;
            case 3:f.Context=f.Context with {Sources=f.Context.Sources with {Main=new string('0',64)}};break;
            case 4:f.Floating=f.Floating with {FlushToZero=true};break;
            case 5:f.Infrastructure=f.Infrastructure with {SerializedNoOtherWriter=null};break;
            default:f.Clocks=f.Clocks.SetItem(1,f.Clocks[1] with {CallerSite=0x140bb8df9});break;
        }
        var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Null(o.Value);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void ReachedActiveChildrenRemainIncompleteRatherThanChosenSuccess(int which)
    {
        var f=new F();
        switch(which)
        {
            case 0:f.W64(F.Npc+0x200,1);break;
            case 1:f.W64(F.Npc+0x208,1);break;
            case 2:f.W32(F.RunInfo+0x58,1);break;
            case 3:f.W8(F.Skill+0xec4,1);break;
            case 4:f.W8(F.Template+0x450,1);break;
            case 5:f.W32(0x14276916c,1);f.W32(0x142769b90,0x100);break;
            case 6:f.W32(0x14275eb74,1);break;
            default:f.W32(F.User+0x4094,1);break;
        }
        var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Null(o.Value);Assert.Equal("incomplete-child",o.Failure!.Code);
    }
    [Fact]
    public void DebugPrimaryGateCanSkipUnknownSecondaryOrComputeItsZeroMask()
    {
        Assert.Equal(605,Good(new F()).FinalRecordDamage);var f=new F();f.W32(0x14276916c,1);f.W32(0x142769b90,0);
        Assert.Equal(605,Good(f).FinalRecordDamage);
    }
    [Fact]
    public void RaceLazyPublicationIsOneCalculatedWriteAndUserClassStillRead()
    {
        var f=new F();f.W64(0x154827468,0);f.W32(0x14f523f78,3);var r=Good(f);
        Assert.Equal("0f00000000000000",Assert.Single(r.Steps,x=>x.Operand=="race.single_publication").After);
        Assert.Contains(r.Steps,x=>x.Operand=="User.slot115.raw_class3a78");
    }
    [Fact]
    public void CounterDeadlineWrapsAndUnusedClockTailIsRejected()
    {
        var f=new F();f.Clocks=f.Clocks.SetItem(2,f.Clocks[2] with {Value=0xfffffff0});var r=Good(f);
        Assert.Equal(4984u,new U.Cursor(f.Context,r.CalculatedMemory,100).U32(F.User+0x13dc,0,"deadline"));
        f.Clocks=f.Clocks.Add(f.Clocks[2]);Assert.False(C.Calculate(f.Input()).Success);
    }
    [Fact]
    public void ConsumedPdefOutOfCvttRangeIsNotWrappedIntoPlausibleDamage()
    {var f=new F();f.W32(F.User+0x1494,uint.MaxValue);var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Equal("conversion",o.Failure!.Code);}
    [Fact]
    public void InputCurrentBytesAreImmutableAndOriginalImagesStayUnchanged()
    {
        var f=new F();var input=f.Input();string[] before=input.Memory.Images.Select(x=>x.BytesSha256).ToArray();
        f.W16(F.User+0x14b6,0);Assert.Equal(605,C.Calculate(input).Value!.FinalRecordDamage);
        Assert.Equal(before,input.Memory.Images.Select(x=>x.BytesSha256));Assert.Equal(0u,new U.Cursor(input.Context,input.Memory,100).U32(F.Cache+4,0,"initial critical"));
    }
    [Theory]
    [InlineData(-1,0ul)] [InlineData(0,1ul)] [InlineData(63,ulong.MaxValue)]
    public void LazyRaceLimitHasExactFiniteNativePublicationBoundary(int count,ulong expected)
    {
        var f=new F();f.W64(0x154827468,0);f.W32(0x14f523f78,unchecked((uint)count));var r=Good(f);
        Assert.Equal(expected,new U.Cursor(f.Context,r.CalculatedMemory,100).U64(0x154827468,0,"race"));
        Assert.Equal(count<0?0:1,r.Steps.Count(x=>x.Operand=="race.single_publication"));
    }
    [Fact]
    public void ReachedUnknownAttenuationOrActiveLazyLoopIsNotInvented()
    {
        var f=new F();f.Forget(F.Skill+0xe10,8);Assert.False(C.Calculate(f.Input()).Success);
        f=new F();f.W64(0x154827468,0);f.W32(0x14f523f78,64);var o=C.Calculate(f.Input());Assert.False(o.Success);Assert.Equal("race.limit",o.Failure!.Operand);
    }
    private static C.Result Good(F f)
    {var o=C.Calculate(f.Input());Assert.True(o.Success,o.Failure?.ToString());return o.Value!;}
    private static int? Int(JsonElement e,string name)=>e.GetProperty(name).ValueKind==JsonValueKind.Null?null:e.GetProperty(name).GetInt32();
    private static JsonElement Row(string name)=>F.Extra.RootElement.GetProperty("cases").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==name);
    private static F Recipe(JsonElement row)
    {
        var f=new F();f.SelectSkill(RetailNpcPhysicalInstantParametersTests.Select(row.GetProperty("selected_skill").GetInt32(),row.GetProperty("selected_npc").GetInt32()));
        foreach(var x in row.GetProperty("changes").EnumerateArray())
        {
            ulong a=Convert.ToUInt64(x.GetProperty("address").GetString(),16),v=x.GetProperty("value").GetUInt64();
            switch(x.GetProperty("width").GetInt32()) {case 1:f.W8(a,(byte)v);break;case 2:f.W16(a,(ushort)v);break;case 4:f.W32(a,(uint)v);break;default:f.W64(a,v);break;}
        }
        var state=row.GetProperty("initial_rng");f.Random=f.Random with {InitialStateBytes=Convert.FromBase64String(state.GetProperty("base64").GetString()!).ToImmutableArray(),InitialStateSha256=state.GetProperty("sha256").GetString()};
        f.Clocks=row.GetProperty("clocks").EnumerateArray().Select(x=>Enum.Parse<C.ClockRole>(x.GetString()!)).Select(role=>
            f.Clock(role,role==C.ClockRole.HitMovement?0x140bb8df9ul:role==C.ClockRole.CalculatorMovement?0x140bb8430ul:0x1400b4640ul,
                role is C.ClockRole.HitMovement or C.ClockRole.CalculatorMovement?0x140578ec9ul:0x1400b4650ul,
                role==C.ClockRole.HitMovement?100u:role==C.ClockRole.CalculatorMovement?200u:2000u)).ToImmutableArray();
        string? hp2=row.GetProperty("hp2").GetString();if(hp2 is not null)
        {
            const ulong program=0x73000000;f.Add(program,hp2=="other"?4:32,"current HP2 program");f.W64(F.User+0x8008,program);f.W16(program+2,hp2=="other"?(ushort)0x13:(ushort)0x12);
            if(hp2=="absolute")
            {f.W8(program,0);f.W16(program+4,0);f.W16(program+6,100);f.W8(program+8,1);f.W16(program+10,0x78);f.W16(program+12,0);f.W16(program+14,100);}
            else if(hp2=="none")for(int i=0;i<3;i++)f.W16(program+10+(ulong)i*8,0);
        }
        return f;
    }
}
