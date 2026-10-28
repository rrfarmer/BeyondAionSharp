using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using A = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalAttackPreparation;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using F = Aion.GameServer.Tests.NormalRecordFixture;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcNormalAttackPreparationTests
{
    public static IEnumerable<object[]> Pilot()=>F.Corpus.RootElement.GetProperty("cases").EnumerateArray().Where(x=>x.GetProperty("expected").GetProperty("profile_npc_id").ValueKind!=JsonValueKind.Null)
        .Select(x=>new object[]{x.GetProperty("name").GetString()!});
    [Theory,MemberData(nameof(Pilot))]
    public void AllTwentyFiveCurrentDProfilesBindTheirExactOrderedEntry(string name)
    {
        var f=new F(name);var selected=f.Admit();Assert.True(selected.Success,selected.Failure?.ToString());var s=selected.Value!;
        Assert.Equal(0,s.Ordinal);Assert.Equal(f.Projection.NpcId,s.NpcId);Assert.Equal(f.Projection.Receipt,s.Receipt);Assert.Equal(100,s.Modifier);Assert.Equal(1000,s.Weight);
        var result=A.Prepare(s,f.Memory());Assert.True(result.Success,result.Failure?.ToString());var c=new U.Cursor(f.Context.Common,result.Value!.CalculatedMemory,1000);
        Assert.Equal(s.Element,c.I32(F.Npc+0x8f4,0,"main element"));Assert.Equal(s.Element,c.I32(F.Npc+0x8f8,0,"off element"));
        Assert.Equal(s.Element==0?1u:0u,c.U32(F.Npc+0x8e4,0,"physical"));Assert.False(result.Value.ActivationReady);Assert.False(result.Value.CurrentActorObserved);
    }
    public static IEnumerable<object[]> Preparations()=>F.Corpus.RootElement.GetProperty("preparation_contexts").EnumerateArray().Select(x=>new object[]{x.GetProperty("name").GetString()!});
    [Theory,MemberData(nameof(Preparations))]
    public void PreparationMatchesIndependentFloatOperationOrderAndBothHandStores(string name)
    {
        var row=F.Corpus.RootElement.GetProperty("preparation_contexts").EnumerateArray().Single(x=>x.GetProperty("name").GetString()==name);var f=new F(row);var e=row.GetProperty("expected_preparation").GetProperty("expected");
        var admit=f.Admit();Assert.True(admit.Success,admit.Failure?.ToString());var r=A.Prepare(admit.Value!,f.Memory());Assert.True(r.Success,r.Failure?.ToString());
        var c=new U.Cursor(f.Context.Common,r.Value!.CalculatedMemory,1000);
        foreach(ulong off in new[]{0x8d4ul,0x8d8ul})Assert.Equal(Convert.ToUInt32(e.GetProperty("mod_rate8d4_8d8").GetString(),16),c.U32(F.Npc+off,0,"mod"));
        foreach(ulong off in new[]{0x8dcul,0x8e0ul})Assert.Equal(Convert.ToUInt32(e.GetProperty("additive_rate8dc_8e0").GetString(),16),c.U32(F.Npc+off,0,"rate"));
        foreach(ulong off in new[]{0x14d4ul,0x14d8ul})Assert.Equal(e.GetProperty("flat14d4_14d8").GetInt32(),c.I32(F.Npc+off,0,"flat"));
        Assert.Equal(0u,c.U32(F.Npc+0x14d0,0,"both original words cleared"));Assert.Equal(Convert.ToUInt32(e.GetProperty("range910").GetString(),16),c.U32(F.Npc+0x910,0,"range"));
        Assert.Equal(e.GetProperty("critical_pair14b8_14ba")[0].GetUInt16(),c.U16(F.Npc+0x14b8,0,"crit0"));Assert.Equal(e.GetProperty("critical_pair14b8_14ba")[1].GetUInt16(),c.U16(F.Npc+0x14ba,0,"crit1"));
        Assert.Equal(12,c.I32(F.Npc+0x8ec,0,"separate group remains unchanged"));
    }
    [Fact]
    public void PreparationRetainsBothModifierReadsAndRateBeforeFlatStoreOrder()
    {
        var f=new F();var r=A.Prepare(f.Admit().Value!,f.Memory());Assert.True(r.Success,r.Failure?.ToString());
        string[] reached=r.Value!.Steps.Select(x=>x.Operand).ToArray();
        string[] ordered=["entry.modifier14.first","Npc.modifier_rate8d4","entry.modifier14.second","Npc.modifier_rate8d8",
            "Npc.modifier_row.rate","Npc.additive_rate8e0","Npc.additive_rate8dc","Npc.modifier_row.flat","Npc.flat14d8","Npc.flat14d4"];
        int previous=-1;foreach(string name in ordered){int at=Array.IndexOf(reached,name);Assert.True(at>previous,name);previous=at;}
    }
    [Theory]
    [InlineData(-1)] [InlineData(1)] [InlineData(256)]
    public void MissingNormalOrdinalNeverFallsBackToFirst(int ordinal)
    {var o=new F().Admit(ordinal);Assert.False(o.Success);Assert.Null(o.Value);Assert.Equal("ordinal",o.Failure!.Operand);}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void StaleCurrentEntryCannotUsePreviouslyProjectedValues(int field)
    {
        var f=new F();if(field==0)f.W32(F.Entry,999);else if(field==1)f.W32(F.Entry+4,0);else if(field==2)f.W32(F.Entry+0x10,1);else f.W32(F.Entry+0x14,101);
        Assert.False(f.Admit().Success);
    }
    [Fact]
    public void ChangedStorageAfterPrivateAdmissionIsRecheckedAtUse()
    {var f=new F();var selected=f.Admit().Value!;f.W32(F.Entry+0x14,101);var r=A.Prepare(selected,f.Memory());Assert.False(r.Success);Assert.Equal("stale-entry",r.Failure!.Code);}
    [Fact]
    public void SkillBackedEntryIsNotNormalEvenWhenAllNormalScalarFieldsMatch()
    {var f=new F();f.W64(F.Entry+0x28,0x90000000);var o=f.Admit();Assert.False(o.Success);Assert.Equal("selected.Skill",o.Failure!.Operand);}
    [Fact]
    public void PrivateAdmissionHasNoRawConstructorOrMutableCopyRoute()
    {Assert.Empty(typeof(A.SelectedNormal).GetConstructors(BindingFlags.Public|BindingFlags.Instance));Assert.All(typeof(A.SelectedNormal).GetProperties(BindingFlags.NonPublic|BindingFlags.Instance),x=>Assert.False(x.CanWrite));}
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void UnknownOrInvalidSourceEvidenceDoesNotBecomeAdmitted(int kind)
    {
        var f=new F();f.Context=kind switch{0=>f.Context with{ParentEvidence=new string('0',64)},1=>f.Context with{DatasetId=new string('0',64)},_=>f.Context with{TypeTableEvidence="unknown"}};
        Assert.False(f.Admit().Success);
    }
    [Theory]
    [InlineData(0x1850ul,8)] [InlineData(0xa50ul,4)] [InlineData(0xa54ul,4)]
    public void UnknownReachedPreparationFieldWithholdsWholeCandidate(ulong offset,int count)
    {var f=new F();var s=f.Admit().Value!;f.Forget(F.Npc+offset,count);var o=A.Prepare(s,f.Memory());Assert.False(o.Success);Assert.Null(o.Value);}
    [Fact]
    public void ActiveVirtualRangeCannotSupplyAChosenEffectiveRange()
    {var f=new F();var s=f.Admit().Value!;f.W64(F.Npc+0x1850,0x90000000);var o=A.Prepare(s,f.Memory());Assert.False(o.Success);Assert.True(o.Failure!.Unsupported);}
    [Fact]
    public void ConstructedInitializerWritesAllOneHundredAndOneCellsWithoutFillingPadding()
    {
        var f=new F();f.Forget(0x145a28930,404);f.Forget(0x145a28ad0,404);
        var o=A.InitializeConstructedFloorTables(f.Context,f.Memory());Assert.True(o.Success,o.Failure?.ToString());var c=new U.Cursor(f.Context.Common,o.Value!.CalculatedMemory,1000);
        for(int level=0;level<=100;level++)
        {Assert.Equal(level<=50?-1000:-500-level*10,c.I32(0x145a28930ul+(ulong)level*4,0,"floor"));Assert.Equal(level<=50?1000:500+level*10,c.I32(0x145a28ad0ul+(ulong)level*4,0,"companion"));}
        Assert.Equal(202,o.Value.Steps.Count(x=>x.Kind=="Write"));
    }
    [Fact]
    public void CurrentUnknownTableDoesNotBecomeAClaimedStartupInitialization()
    {var f=new F();f.Context=f.Context with{Common=f.Context.Common with{Origin=U.Origin.DeclaredCurrentSnapshot}};Assert.False(A.InitializeConstructedFloorTables(f.Context,f.Memory()).Success);}
    [Fact]
    public void SecondOrderedNormalEntryUsesItsOwnPointerAndSourceValues()
    {
        var seed=new F();JsonNode row=JsonNode.Parse(seed.Row.GetRawText())!;var root=row["source"]!["profile"]!["raw_record"]!.AsArray();
        var group=root[2]!.AsArray().OfType<JsonArray>().Single(x=>x[0]!.GetValue<string>()=="attacks");var entries=group[2]!.AsArray();var second=entries[0]!.DeepClone().AsArray();
        foreach(var field in second[2]!.AsArray().OfType<JsonArray>())
        {string tag=field[0]!.GetValue<string>();if(tag=="attack_type")field[2]=new JsonArray("magical_water");else if(tag=="attack_mod")field[2]=new JsonArray("125");else if(tag=="attack_range")field[2]=new JsonArray("16");else if(tag=="attack_rate")field[2]=new JsonArray("500");}
        entries.Add(second);using var doc=JsonDocument.Parse(row.ToJsonString());var f=new F(doc.RootElement);
        const ulong extra=0x61000100;f.Add(F.Vector+8,8,"second current vector cell","vector-life");f.Add(extra,0x30,"second current normal entry","entry-life");
        f.W64(F.Template+0x78,F.Vector+16);f.W64(F.Vector+8,extra);f.W32(extra,500);f.W32(extra+4,0x41800000);f.W32(extra+0x10,1);f.W32(extra+0x14,125);
        var a=f.Admit(1);Assert.True(a.Success,a.Failure?.ToString());Assert.Equal(extra,a.Value!.EntryAddress);Assert.Equal(1,a.Value.Ordinal);Assert.Equal(125,a.Value.Modifier);
        var prepared=A.Prepare(a.Value,f.Memory());Assert.True(prepared.Success,prepared.Failure?.ToString());Assert.Equal(1,prepared.Value!.Element);
    }
}
