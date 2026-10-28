using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatEquipmentLifecycleTests
{
    [SkippableFact]
    public void FreshPlayerRestoresAcceptedArrivalBeforeSortedListenersAndRetainsHistoryAcrossRemoval()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(hash),"Requires external source statistics package");
        using var catalog=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),hash!,Path.GetDirectoryName(manifest!)!);
        Assert.NotNull(RetailNpcStatCatalog.EquipmentResponses);
        var profile=RetailNpcStatCatalog.Members.First(x=>x.Profile.NpcId==236205).Profile;
        using var h=BossAiHarness.For(220030000).Build();
        var itemSets=new Aion.GameServer.Dataholders.ItemSetData {itemsetList=[]};itemSets.AfterUnmarshal(null!);
        h.Data.StaticData.GetType().GetProperty("ItemSetDataDh")!.SetValue(h.Data.StaticData,itemSets);
        // Minimal item metadata deliberately isolates lifecycle. Source response
        // descriptors come from the unchanged pinned IDs, not these test fields.
        static Item Armor(int owner,int id,ItemSlot slot)=>new(owner,new ItemTemplate {itemId=id},1,true,slot.GetSlotIdMask());
        foreach(bool commonFirst in new[]{false,true})
        {
            var player=h.SpawnPlayer();var equipment=player.GetEquipment();
            var common=Armor(900001,110101896,ItemSlot.TORSO);var normalOnly=Armor(900002,111300459,ItemSlot.GLOVES);
            foreach(var item in commonFirst?new[]{common,normalOnly}:new[]{normalOnly,common})equipment.OnLoadHandler(item);
            Assert.Equal(2,equipment.GetEquippedItems().Count);equipment.OnLoadApplyEquipmentStats();
            Assert.True(RetailNpcStatEquipmentResponses.TryPrepare(profile,player,out var handle,out string reason),reason);
            var value=handle.Current();Assert.True(value.NormalPercent);Assert.Equal(26,value.NormalRate);
            Assert.Equal(commonFirst,value.SkillPercent);Assert.Equal(6,value.SkillRate);
            Assert.Equal(commonFirst?940:1000,handle.Physical(1000));
            Assert.Equal(commonFirst?940:1000,handle.Spell(1000));Assert.Equal(740f,handle.Normal(1000,"main"));
            Item? removed=null;Exception? error=null;
            using var entered=new ManualResetEventSlim();using var finished=new ManualResetEventSlim();
            var writer=new Thread(()=>
            {
                entered.Set();try {removed=equipment.UnEquipItem(common.GetObjectId(),false);}
                catch(Exception ex){error=ex;}finally {finished.Set();}
            }) {IsBackground=true};
            equipment.InspectEquippedItems(items=>
            {
                writer.Start();Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
                Assert.True(SpinWait.SpinUntil(()=>finished.IsSet||(writer.ThreadState&ThreadState.WaitSleepJoin)!=0,TimeSpan.FromSeconds(5)));
                Assert.False(finished.IsSet);Assert.Equal(value,handle.Current());return 0;
            });
            Assert.True(writer.Join(TimeSpan.FromSeconds(5)));Assert.Null(error);Assert.Same(common,removed);
            Assert.Same(normalOnly,equipment.UnEquipItem(normalOnly.GetObjectId(),false));
            Assert.Empty(equipment.GetEquippedItems());var zero=handle.Current();
            Assert.True(zero.Matched);Assert.Equal(commonFirst,zero.SkillPercent);Assert.Equal(0,zero.SkillRate);
            common.SetEquipped(true);common.SetEquipmentSlot(ItemSlot.TORSO.GetSlotIdMask());
            equipment.OnLoadHandler(common);
            Aion.GameServer.Model.Stats.Listeners.ItemEquipmentListener.OnItemEquipment(common,player);
            Assert.Equal(commonFirst?940:1000,handle.Physical(1000));
        }
    }
}
