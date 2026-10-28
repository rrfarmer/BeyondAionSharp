using Aion.GameServer.Controllers.Attack.RetailNpc;
using System.Text.Json;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcStatEquipmentResponseTests
{
    private static readonly Lazy<RetailNpcStatEquipmentResponses> Context=new(()=>RetailNpcStatEquipmentResponses.Parse(
        File.ReadAllBytes(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_TEST_CONTEXT")!),RetailNpcStatEquipmentResponses.ReviewedContextSha256));
    internal static RetailNpcStatEquipmentResponses Inputs()
    {
        Skip.If(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_TEST_CONTEXT")),"Requires pinned offline retail equipment context");
        return Context.Value;
    }
    private static JsonDocument Review(string file)
    {
        string context=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_TEST_CONTEXT")!;
        return JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(context))!,"positive-review",file)));
    }
    private static void Snapshot(JsonElement expected,RetailNpcStatEquipmentResponses.Response actual)
    {
        Assert.True(actual.Matched);Assert.Equal(expected.GetProperty("tag").GetString(),actual.Tag);
        Assert.Equal(expected.GetProperty("normal_flag").GetInt32()!=0,actual.NormalPercent);
        Assert.Equal(expected.GetProperty("normal_flat").GetInt32(),actual.NormalFlat);
        Assert.Equal(expected.GetProperty("normal_percent").GetInt32(),actual.NormalRate);
        Assert.Equal(expected.GetProperty("skill_flag").GetInt32()!=0,actual.SkillPercent);
        Assert.Equal(expected.GetProperty("skill_flat").GetInt32(),actual.SkillFlat);
        Assert.Equal(expected.GetProperty("skill_percent").GetInt32(),actual.SkillRate);
        Assert.Equal(expected.GetProperty("immune_word").GetInt32(),(int)actual.Immune);
    }
    [SkippableFact]
    public void LateConsumersReplayAllIndependentNativeArithmeticWitnesses()
    {
        var context=Inputs();using var doc=Review("arithmetic-vectors.json");using var contract=Review("contract.json");int count=0;
        foreach(var row in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            RetailNpcStatEquipmentResponses.Response response;
            if(row.TryGetProperty("sequence",out var sequence))
            {
                string tag=row.GetProperty("tag").GetString()!;
                var pair=contract.RootElement.GetProperty("all_mixed_definition_pairs").EnumerateArray().Single(x=>x.GetProperty("tag").GetString()==tag);
                var ids=pair.GetProperty("different_slot_example");int a=ids[0].GetInt32(),b=ids[1].GetInt32();var ledger=context.NewLedger();
                if(sequence.GetString()=="B_then_A"){ledger.Equip(2,b);ledger.Equip(1,a);}
                else {ledger.Equip(1,a);if(sequence.GetString()=="A_add_remove_then_B")ledger.Unequip(1,a);ledger.Equip(2,b);}
                response=ledger.Get(tag);
            }
            else
            {
                bool named=row.TryGetProperty("definition",out var name);
                var d=context.GetDefinition(named?name.GetString()!:"Test_Vritra1_ReduceSkill_Exceed");
                response=RetailNpcStatEquipmentResponses.Response.First(d);
                if(!named)for(int i=1;i<5;i++)response=response.Adjust(d,1);
            }
            int amount=row.GetProperty("amount").GetInt32();var expected=row.GetProperty("expected");
            string kind=row.GetProperty("kind").GetString()!;
            if(kind=="spell")Assert.Equal(expected.GetProperty("result").GetInt32(),response.Spell(amount));
            else
            {
                float result=kind=="normal"?response.Normal(amount):response.PhysicalValue(amount);
                Assert.Equal(Convert.ToUInt32(expected.GetProperty("resultBits").GetString(),16),BitConverter.SingleToUInt32Bits(result));
                Assert.Equal(expected.GetProperty("finalInteger").GetInt32(),kind=="normal"?RetailNpcUserMagicalInputs.Cvtt(result):response.Physical(amount));
            }
            count++;
        }
        Assert.Equal(92,count);
    }
    [SkippableFact]
    public void AllEightySelectedItemsRegisterTheirOrderedNativeDescriptorsExactlyOncePerObject()
    {
        var context=Inputs();using var doc=Review("contract.json");int count=0;
        foreach(var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var state=context.NewLedger();int id=item.GetProperty("item_id").GetInt32();state.Equip(100,id,8);
            foreach(var descriptor in item.GetProperty("ordered_descriptors").EnumerateArray())Snapshot(descriptor,state.Get(descriptor.GetProperty("tag").GetString()!));
            var first=state.Entries.ToDictionary();state.Equip(100,id,8);
            Assert.Equal(first.OrderBy(x=>x.Key),state.Entries.OrderBy(x=>x.Key));Assert.Single(state.Owners);
            state.Equip(101,id,16);Assert.Equal(2,state.Owners.Count);
            foreach(var pair in first)
            {
                Assert.Equal(unchecked(pair.Value.NormalRate*2),state.Get(pair.Key).NormalRate);
                Assert.Equal(unchecked((ushort)(pair.Value.Immune*2)),state.Get(pair.Key).Immune);
            }
            state.Unequip(101,id);Assert.Equal(first.OrderBy(x=>x.Key),state.Entries.OrderBy(x=>x.Key));
            state.Unequip(100,id);Assert.Empty(state.Owners);
            Assert.All(state.Entries.Values,x=>{Assert.True(x.Matched);Assert.Equal((0,0,0,0,(ushort)0),(x.NormalFlat,x.NormalRate,x.SkillFlat,x.SkillRate,x.Immune));});
            state.Equip(102,id,8);Assert.Equal(first.OrderBy(x=>x.Key),state.Entries.OrderBy(x=>x.Key));Assert.Null(state.Invalid);count++;
        }
        Assert.Equal(80,count);
    }
    [SkippableFact]
    public void EveryMixedDefinitionPairPreservesBothOrdersAndRemovedFirstWriter()
    {
        var context=Inputs();using var doc=Review("contract.json");int count=0;
        foreach(var pair in doc.RootElement.GetProperty("all_mixed_definition_pairs").EnumerateArray())
        {
            string tag=pair.GetProperty("tag").GetString()!;var ids=pair.GetProperty("different_slot_example");int a=ids[0].GetInt32(),b=ids[1].GetInt32();
            var ab=context.NewLedger();ab.Equip(1,a);ab.Equip(2,b);Snapshot(pair.GetProperty("A_then_B"),ab.Get(tag));
            var ba=context.NewLedger();ba.Equip(2,b);ba.Equip(1,a);Snapshot(pair.GetProperty("B_then_A"),ba.Get(tag));
            var removed=context.NewLedger();removed.Equip(1,a);removed.Unequip(1,a);removed.Equip(2,b);
            Snapshot(pair.GetProperty("A_add_remove_then_B"),removed.Get(tag));
            var fresh=context.NewLedger();fresh.Equip(2,b);Snapshot(pair.GetProperty("fresh_B"),fresh.Get(tag));count++;
        }
        Assert.Equal(5,count);
    }
    [SkippableFact]
    public void UnknownAndUnbalancedOwnersRemainVisibleInsteadOfRepairingHistory()
    {
        var context=Inputs();var state=context.NewLedger();state.Unequip(1,110101896);
        Assert.Equal("source-exclusive-unbalanced-owner-removal",state.Invalid);state.Equip(1,110101896);Assert.NotNull(state.Invalid);
        var drift=context.NewLedger();drift.Equip(1,110101896,8);drift.Equip(1,110101896,16);Assert.Equal("source-exclusive-owner-identity-drift",drift.Invalid);
        var unknown=context.NewLedger();unknown.Equip(1,int.MaxValue);Assert.StartsWith("source-exclusive-unknown-equipped-item",unknown.Invalid);
        var missing=RetailNpcStatEquipmentResponses.Response.Missing("vritra1");
        Assert.Equal(0,missing.Physical(0));Assert.Equal(0,missing.Spell(0));Assert.Equal(0f,missing.Normal(0));
    }
    [SkippableFact]
    public void CompleteNativeItemInputsDistinguishKnownEmptyUnknownAndActualResponseEquipment()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_EQUIPMENT_TEST_CONTEXT");
        Skip.If(string.IsNullOrEmpty(path),"Requires the pinned offline retail item context");
        var index=RetailNpcStatEquipmentResponses.Parse(File.ReadAllBytes(path!),RetailNpcStatEquipmentResponses.ReviewedContextSha256);
        Assert.Equal(128380,index.KnownCount);Assert.Equal(80,index.ResponderCount);
        Assert.True(index.CheckItems([],out _));
        Assert.True(index.CheckItems([100000001],out _));
        Assert.False(index.CheckItems([110101896],out string responder));
        Assert.Equal("source-exclusive-nonzero-response-equipment-110101896",responder);
        Assert.False(index.CheckItems([int.MaxValue],out string unknown));
        Assert.Equal("source-exclusive-unknown-equipped-item-2147483647",unknown);
        Assert.False(index.CheckItems([100000001,110101896],out _));
    }
}
