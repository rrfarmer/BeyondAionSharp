using System.Collections.Immutable;
using System.IO.Compression;
using System.Text.Json;
using C=Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserPhysicalSkillPveModifier;
using Aion.GameServer.Controllers.Attack.RetailNpc;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcToUserPhysicalSkillPveModifierTests
{
    private const string CorpusSha="83949fa4fc07112003bff7b6c4c49c25bef6731598eaa130c1fbdc09f0910d50";
    private const string Packed="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";
    private static readonly JsonDocument Corpus=Load();
    private static JsonDocument Load()
    {
        using var source=new MemoryStream(Convert.FromBase64String(Packed));
        using var zip=new GZipStream(source,CompressionMode.Decompress);
        using var output=new MemoryStream();zip.CopyTo(output);
        byte[] bytes=output.ToArray();
        Assert.Equal(CorpusSha,Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        return JsonDocument.Parse(bytes);
    }
    public static IEnumerable<object[]> Cases()
    {
        foreach(string name in new[]{"getter_cases","selector_cases","hp2_cases","ratio_cases","composed_cases","hp2_no_program_guards"})
            for(int i=0;i<Corpus.RootElement.GetProperty(name).GetArrayLength();i++)yield return new object[]{name,i};
    }
    private static C.Context Context()=>new(RetailNpcNormalScope.MainBinarySha256,RetailNpcPhysicalInstantParameters.PackageContext,
        "conditional-modifier-1","serialized-epoch-1",C.Origin.MathematicalFixture,"corrected-independent-source-corpus",C.ProofSha256,C.OracleAmendmentSha256,C.ReleaseEvidenceSha256);
    private static C.Binding Bind(ulong address,string name)=>new(address,name,name+"-generation-1",Context());
    private static C.Input Make()=>new(Context(),new(Bind(0x100000,"sourceNpc"),Bind(0x200000,"sourceTemplate"),212776,new()
    {Type144=2,Vtable=C.NpcVtable,Kind1940=1,Master188c=0,Related1894=0,Template1848=0x200000,Virtual1850=0,Difficulty1bf8=0,Current1bc8=0,Current1bd8=0,Template23c=0}),
        new(Bind(0x300000,"targetUser"),new(){Type144=1,Vtable=C.UserVtable,Primary6e50=0x400000,Backup6e38=0x500000,
            Current166c=0,CurrentCf4=0,Selector738=0,Maximum774=100,Current778=50,Program8008=0}),
        new(Bind(0x400000,"primary"),0,0,0),new(Bind(0x500000,"backup"),0,0,0),null,0,new(true,true,false,false),true,true);
    private static C.Input Getter(C.Input x,JsonElement g)
    {
        var p=g.GetProperty("primary");var b=g.GetProperty("backup");
        return x with{Primary=x.Primary! with{OccupiedWord=(ushort)p[0].GetInt32(),Flags1c8=p[1].GetUInt32(),Defense1c4=p[2].GetInt32()},
            Backup=x.Backup! with{OccupiedWord=(ushort)b[0].GetInt32(),Flags1c8=b[1].GetUInt32(),Defense1c4=b[2].GetInt32()},
            Target=x.Target with{Raw=x.Target.Raw with{Current166c=g.GetProperty("native166c").GetInt32(),CurrentCf4=g.GetProperty("nativecf4").GetInt32()}}};
    }
    private static C.Input Selector(C.Input x,JsonElement s)=>x with{
        Source=x.Source with{Raw=x.Source.Raw with{Current1bc8=s.GetProperty("nativeNpc1bc8").GetInt32(),Current1bd8=s.GetProperty("nativeNpc1bd8").GetInt32(),Template23c=s.GetProperty("template23c").GetInt32()}},
        Target=x.Target with{Raw=x.Target.Raw with{Selector738=s.GetProperty("nativeUser738").GetInt32()}}};
    private static C.Input Program(C.Input x,JsonElement p)=>x with{
        Target=x.Target with{Raw=x.Target.Raw with{Maximum774=p.GetProperty("maximumHP774").GetInt32(),Current778=p.GetProperty("currentHP778").GetInt32(),Program8008=0x600000}},
        Program=new(Bind(0x600000,"HP2"),Convert.FromHexString(p.GetProperty("raw_program").GetString()!).ToImmutableArray())};
    private static C.Result Complete(C.Input x)
    {
        var outcome=C.Calculate(x);Assert.True(outcome.Success,outcome.Diagnostic?.ToString());
        var r=outcome.Value!;Assert.False(r.ActivationReady);Assert.False(r.HostEffectsExecuted);Assert.False(r.CurrentActorObserved);Assert.Null(r.HpResult);return r;
    }
    private static void Stages(C.Result r,JsonElement expected)
    {
        foreach(var p in expected.EnumerateObject())Assert.Equal(Convert.ToUInt32(p.Value.GetString(),16),r.FloatBits[p.Name]);
    }
    [Theory][MemberData(nameof(Cases))]
    public void CorrectedIndependentRawSourceCorpus(string family,int index)
    {
        JsonElement e=Corpus.RootElement.GetProperty(family)[index];C.Input x=Make();
        switch(family)
        {
            case "getter_cases":
                var g=Complete(Getter(x,e));Assert.Equal(e.GetProperty("defense").GetInt32(),g.BaseDefense);
                Assert.Equal(e.GetProperty("selector_output").GetInt32(),g.SnapshotSelector);break;
            case "selector_cases":
                Assert.Equal(e.GetProperty("computed").GetInt32(),Complete(Selector(x,e)).SourceSlot190);break;
            case "hp2_cases":
                x=x with{Target=x.Target with{Raw=x.Target.Raw with{Current166c=e.GetProperty("base_defense").GetInt32()}}};
                var h=Complete(Program(x,e));Assert.Equal(e.GetProperty("delta").GetInt32(),h.Hp2Delta);
                Assert.Equal(e.GetProperty("selected_row").ValueKind==JsonValueKind.Null?(int?)null:e.GetProperty("selected_row").GetInt32(),h.SelectedHp2Row);
                Stages(h,e.GetProperty("stages"));break;
            case "ratio_cases":
                // This subcorpus independently checks the already-existing complete helper.
                // The new NPC->User chain always passes attack0; it never accepts a chosen attack ratio.
                int attack=e.GetProperty("attack").GetInt32(),defense=e.GetProperty("defense").GetInt32();
                var scope=RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,RetailNpcNormalActorKind.Player,212776,0);
                uint expected=Convert.ToUInt32(e.GetProperty("ratio_bits").GetString(),16);
                Assert.Equal(expected,BitConverter.SingleToUInt32Bits(RetailNpcNormalDamageStages.CalculatePveRatio(scope,attack,defense)));
                if(attack==0){x=x with{Target=x.Target with{Raw=x.Target.Raw with{Current166c=defense}}};Assert.Equal(expected,Complete(x).RatioBits);}break;
            case "composed_cases":
                var getter=Corpus.RootElement.GetProperty("getter_cases")[e.GetProperty("getter_index").GetInt32()];
                var selector=Corpus.RootElement.GetProperty("selector_cases")[e.GetProperty("selector_index").GetInt32()];
                var program=Corpus.RootElement.GetProperty("hp2_cases")[e.GetProperty("program_index").GetInt32()];
                var all=Complete(Program(Selector(Getter(x,getter),selector),program));
                Assert.Equal(e.GetProperty("base_defense_after_source_slot190").GetInt32(),all.DefenseBeforeHp2);
                Assert.Equal(e.GetProperty("computed_hp2").GetProperty("delta").GetInt32(),all.Hp2Delta);
                Assert.Equal(e.GetProperty("final_defense").GetInt32(),all.FinalDefense);
                Assert.Equal(Convert.ToUInt32(e.GetProperty("ratio_bits").GetString(),16),all.RatioBits);
                Stages(all,e.GetProperty("computed_hp2").GetProperty("stages"));break;
            case "hp2_no_program_guards":
                if(index!=0){var raw=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2),e.GetProperty("raw_tag_at_2").GetUInt16());
                    x=x with{Target=x.Target with{Raw=x.Target.Raw with{Program8008=0x600000}},Program=new(Bind(0x600000,"HP2"),raw.ToImmutableArray())};}
                var empty=Complete(x);Assert.Equal(0,empty.Hp2Delta);Assert.Null(empty.SelectedHp2Row);
                Assert.DoesNotContain(empty.Reads,r=>r.Operand.StartsWith("HP2 row",StringComparison.Ordinal));break;
            default:throw new InvalidOperationException(family);
        }
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    [InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)][InlineData(12)][InlineData(13)]
    [InlineData(14)][InlineData(15)][InlineData(16)][InlineData(17)][InlineData(18)][InlineData(19)][InlineData(20)]
    public void UnknownStaleUnsupportedOrAliasedInputsNeverPublishAPartialResult(int change)
    {
        var x=Make();
        x=change switch{
            0=>x with{Context=x.Context with{Origin=C.Origin.Unknown}},
            1=>x with{Source=x.Source with{Raw=x.Source.Raw with{Master188c=1}}},
            2=>x with{Target=x.Target with{Raw=x.Target.Raw with{Type144=2}}},
            3=>x with{Target=x.Target with{Raw=x.Target.Raw with{Vtable=C.NpcVtable}}},
            4=>x with{Target=x.Target with{Raw=x.Target.Raw with{Maximum774=null}}},
            5=>x with{Target=x.Target with{Raw=x.Target.Raw with{Current778=null}}},
            6=>x with{Target=x.Target with{Raw=x.Target.Raw with{CurrentCf4=null}}},
            7=>x with{Primary=null},
            8=>x with{Backup=null},
            9=>x with{Telemetry14275eb74=1},
            10=>x with{Target=x.Target with{Raw=x.Target.Raw with{Program8008=null}}},
            11=>x with{Target=x.Target with{Raw=x.Target.Raw with{Program8008=0x600000}}},
            12=>x with{Floating=x.Floating with{NearestEven=false}},
            13=>x with{Floating=x.Floating with{FlushToZero=true}},
            14=>x with{SerializedNoOtherWriter=false},
            15=>x with{NormalReturn=null},
            16=>x with{Source=x.Source with{Raw=x.Source.Raw with{Template1848=1}}},
            17=>x with{Target=x.Target with{Current=x.Target.Current with{Context=x.Context with{Epoch="stale"}}}},
            18=>x with{Source=x.Source with{Template=x.Source.Template with{Address=0x100000},Raw=x.Source.Raw with{Template1848=0x100000}}},
            19=>x with{Target=x.Target with{Raw=x.Target.Raw with{Selector738=null}}},
            20=>x with{Source=x.Source with{Raw=x.Source.Raw with{Template23c=null}}},
            _=>throw new InvalidOperationException()};
        var result=C.Calculate(x);Assert.False(result.Success);Assert.Null(result.Value);Assert.NotNull(result.Diagnostic);
    }
    [Fact]
    public void CallerHpGetterOrderSurvivesNullProgramAndUnreadBackupCanStayUnknown()
    {
        var x=Make();x=x with{Primary=x.Primary! with{OccupiedWord=1,Flags1c8=0x100000,Defense1c4=0},
            Backup=null,Target=x.Target with{Raw=x.Target.Raw with{Backup6e38=null,Current166c=null,CurrentCf4=null}},
            Source=x.Source with{Raw=x.Source.Raw with{Current1bd8=null}}};
        var r=Complete(x);Assert.Equal(0,r.BaseDefense);
        string[] names=r.Reads.Select(v=>v.Operand).ToArray();
        Assert.True(Array.IndexOf(names,"User+774:slot60")<Array.IndexOf(names,"User+778:slot59"));
        Assert.True(Array.IndexOf(names,"User+778:slot59")<Array.IndexOf(names,"User+8008"));
        Assert.DoesNotContain("User+6e38",names);Assert.DoesNotContain("User+166c",names);
        Assert.Equal("computed local telemetry guard+4:inactive release",names[^1]);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void ExceptionalOrMissingLateProgramNeverBecomesZeroDefenseFallback(int change)
    {
        var p=Corpus.RootElement.GetProperty("hp2_cases")[40];var x=Program(Make(),p);
        var bytes=x.Program!.Bytes.ToArray();
        if(change==0){bytes[4]=50;bytes[5]=0;bytes[6]=50;bytes[7]=0;x=x with{Target=x.Target with{Raw=x.Target.Raw with{Current778=50,Maximum774=100}}};}
        if(change==1)x=x with{Target=x.Target with{Raw=x.Target.Raw with{Maximum774=0}}};
        if(change==2)bytes=bytes[..4];
        x=x with{Program=x.Program with{Bytes=bytes.ToImmutableArray()}};
        var r=C.Calculate(x);Assert.False(r.Success);Assert.Null(r.Value);Assert.NotNull(r.Diagnostic);
    }

    // The original 408 cases and their corrected corpus above remain unchanged. These
    // source-proved bijections install the same scalar operands in DIFFERENT native
    // fields, poison the unused physical/magic fields, and check complete compositions.
    public static IEnumerable<object[]> NativeModeCases()
    {
        for(int mode=0;mode<3;mode++)
            foreach(string family in new[]{"getter_cases","selector_cases","composed_cases"})
                for(int i=0;i<Corpus.RootElement.GetProperty(family).GetArrayLength();i++)
                    yield return new object[]{mode,family,i};
    }
    private static C.Input ForMode(C.Input x,int mode)
    {
        Assert.InRange(mode,0,2);
        bool magical=mode!=0;
        C.Snapshot? Snapshot(C.Snapshot? value)=>value is null?null:value with{
            Flags214=magical&&value.Flags1c8.HasValue?(value.Flags1c8.Value&0x100000)!=0?0x20000u:0u:null,
            Defense210=magical?value.Defense1c4:null,
            Flags1c8=magical?null:value.Flags1c8,Defense1c4=magical?null:value.Defense1c4};
        var source=x.Source.Raw;var target=x.Target.Raw;
        return x with{
            Entry=mode==2?C.NativeEntry.MagicalSkill:C.NativeEntry.NormalAttack,
            NormalHand=mode==2?null:0,ModeSourceProof=C.ModeSourceProofSha256,
            Source=x.Source with{Raw=source with{
                NormalAttackElement8f4=mode==2?null:mode==0?0:1,
                Current1bd0=magical?source.Current1bc8:null,Current1be0=magical?source.Current1bd8:null,
                Template244=magical?source.Template23c:null,
                Current1bc8=magical?null:source.Current1bc8,Current1bd8=magical?null:source.Current1bd8,
                Template23c=magical?null:source.Template23c}},
            Target=x.Target with{Raw=target with{
                Current1674=magical?target.Current166c:null,CurrentD04=magical?target.CurrentCf4:null,
                Current166c=magical?null:target.Current166c,CurrentCf4=magical?null:target.CurrentCf4}},
            Primary=Snapshot(x.Primary),Backup=Snapshot(x.Backup)};
    }
    [Theory][MemberData(nameof(NativeModeCases))]
    public void CompleteNormalAndMagicalModeSourceBijections(int mode,string family,int index)
    {
        JsonElement e=Corpus.RootElement.GetProperty(family)[index];var x=Make();
        if(family=="getter_cases")x=Getter(x,e);
        else if(family=="selector_cases")x=Selector(x,e);
        else
        {
            x=Getter(x,Corpus.RootElement.GetProperty("getter_cases")[e.GetProperty("getter_index").GetInt32()]);
            x=Selector(x,Corpus.RootElement.GetProperty("selector_cases")[e.GetProperty("selector_index").GetInt32()]);
            x=Program(x,Corpus.RootElement.GetProperty("hp2_cases")[e.GetProperty("program_index").GetInt32()]);
        }
        var r=Complete(ForMode(x,mode));
        if(family=="getter_cases")
        {Assert.Equal(e.GetProperty("defense").GetInt32(),r.BaseDefense);Assert.Equal(e.GetProperty("selector_output").GetInt32(),r.SnapshotSelector);}
        else if(family=="selector_cases")Assert.Equal(e.GetProperty("computed").GetInt32(),r.SourceSlot190);
        else
        {
            Assert.Equal(e.GetProperty("base_defense_after_source_slot190").GetInt32(),r.DefenseBeforeHp2);
            Assert.Equal(e.GetProperty("computed_hp2").GetProperty("delta").GetInt32(),r.Hp2Delta);
            Assert.Equal(e.GetProperty("final_defense").GetInt32(),r.FinalDefense);
            Assert.Equal(Convert.ToUInt32(e.GetProperty("ratio_bits").GetString(),16),r.RatioBits);
            Stages(r,e.GetProperty("computed_hp2").GetProperty("stages"));
        }
        if(mode!=0)
        {
            Assert.DoesNotContain(r.Reads,v=>v.Operand is "User+166c" or "User+cf4:query7a" or
                "Npc+1bc8" or "Npc+1bd8" or "sourceTemplate+23c" or "primary+1c4" or "backup+1c4");
            Assert.Contains(r.Reads,v=>v.Operand=="sourceTemplate+244");
        }
        else Assert.DoesNotContain(r.Reads,v=>v.Operand is "User+1674" or "User+d04:query7c" or
            "Npc+1bd0" or "Npc+1be0" or "sourceTemplate+244" or "primary+210" or "backup+210");
    }
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void EverySupportedNonzeroNormalElementSelectsMagicWords(int element)
    {
        var x=ForMode(Make(),1);
        x=x with{Source=x.Source with{Raw=x.Source.Raw with{NormalAttackElement8f4=element,Current1bd0=50,Template244=25}},
            Target=x.Target with{Raw=x.Target.Raw with{Current1674=97,CurrentD04=3}}};
        var r=Complete(x);Assert.Equal(100,r.BaseDefense);Assert.Equal(25,r.SourceSlot190);
        Assert.Equal(125,r.FinalDefense);Assert.Equal(0x3f600000u,r.RatioBits);
        Assert.Contains(r.Reads,v=>v.NativeSite==0x14036c212&&v.Operand=="sourceTemplate+244");
    }
    [Theory]
    [InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    [InlineData(6)][InlineData(7)][InlineData(8)][InlineData(9)][InlineData(10)][InlineData(11)]
    public void WrongModeProvenanceOrReachedRawOperandsNeverBecomePhysicalFallback(int change)
    {
        var x=ForMode(Make(),1);
        x=change switch{
            0=>x with{ModeSourceProof=null},1=>x with{ModeSourceProof="unreviewed"},
            2=>x with{Entry=(C.NativeEntry)99},3=>x with{NormalHand=null},4=>x with{NormalHand=1},
            5=>x with{Source=x.Source with{Raw=x.Source.Raw with{NormalAttackElement8f4=null}}},
            6=>x with{Source=x.Source with{Raw=x.Source.Raw with{NormalAttackElement8f4=7}}},
            7=>x with{Source=x.Source with{Raw=x.Source.Raw with{Current1bd0=null}}},
            8=>x with{Source=x.Source with{Raw=x.Source.Raw with{Template244=null}}},
            9=>x with{Target=x.Target with{Raw=x.Target.Raw with{CurrentD04=null}}},
            10=>x with{Primary=x.Primary! with{OccupiedWord=1,Flags214=null}},
            11=>x with{Primary=x.Primary! with{OccupiedWord=1,Flags214=0x20000,Defense210=null}},
            _=>throw new InvalidOperationException()};
        var r=C.Calculate(x);Assert.False(r.Success);Assert.Null(r.Value);Assert.NotNull(r.Diagnostic);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)]
    public void NewModeSourceAdmissionIsMandatoryEvenWhenNoHpProgramIsReached(int mode)
    {
        var x=ForMode(Make(),mode) with{ModeSourceProof=null};
        var r=C.Calculate(x);Assert.False(r.Success);Assert.Equal("mode-source",r.Diagnostic!.Code);Assert.Null(r.Value);
    }
    [Fact]
    public void MagicalGetterAllowsUnreachedBackupAndLiveFieldsToRemainUnknown()
    {
        var x=ForMode(Make(),2);
        x=x with{Primary=x.Primary! with{OccupiedWord=1,Flags214=0x20000,Defense210=123},Backup=null,
            Target=x.Target with{Raw=x.Target.Raw with{Backup6e38=null,Current1674=null,CurrentD04=null}},
            Source=x.Source with{Raw=x.Source.Raw with{Current1be0=null}}};
        var r=Complete(x);Assert.Equal(123,r.BaseDefense);
        Assert.DoesNotContain(r.Reads,v=>v.Operand is "User+6e38" or "User+1674" or "User+d04:query7c");
    }
}
