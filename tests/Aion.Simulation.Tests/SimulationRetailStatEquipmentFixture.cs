using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Listeners;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    private static string? InstallExclusiveEquipmentFixture(Player player,string output)
    {
        string? mode=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_RESPONSE_FIXTURE");
        if(string.IsNullOrEmpty(mode))return null;
        Assert.Contains(mode,new[]{"common-first","reverse","removed-first"});
        var equipment=player.GetEquipment();
        var original=equipment.GetEquippedItems().Select(x=>new {owner=x.GetObjectId(),item=x.GetItemId(),slot=x.GetEquipmentSlot()}).ToArray();
        var events=new List<object>();var learned=new List<int>();
        var slots=new[]{ItemSlot.TORSO,ItemSlot.GLOVES,ItemSlot.SHOULDER,ItemSlot.PANTS,ItemSlot.BOOTS}.Select(x=>x.GetSlotIdMask()).ToHashSet();
        foreach(var item in equipment.GetEquippedItems().Where(x=>slots.Contains(x.GetEquipmentSlot())))
        {
            Assert.Same(item,equipment.UnEquipItem(item.GetObjectId(),false));
            events.Add(new {operation="remove",owner=item.GetObjectId(),item=item.GetItemId(),slot=0L});
        }
        Item Add(int id,ItemSlot slot)
        {
            var template=DataManager.ITEM_DATA.GetItemTemplate(id);Assert.NotNull(template);
            Assert.NotEqual(0L,template.GetItemSlot()&slot.GetSlotIdMask());
            var masteries=DataManager.SKILL_DATA.GetMasterySkills(template.GetItemGroup());
            if(masteries.Count>0&&!masteries.Any(player.GetSkillList().IsSkillPresent))
            {
                int mastery=masteries.Min();Assert.True(player.GetSkillList().AddTemporarySkill(player,mastery,1));learned.Add(mastery);
            }
            var item=new Item(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(),template,1,true,slot.GetSlotIdMask());
            equipment.OnLoadHandler(item);Assert.Same(item,equipment.GetEquippedItemByObjId(item.GetObjectId()));
            ItemEquipmentListener.OnItemEquipment(item,player);
            events.Add(new {operation="add",owner=item.GetObjectId(),item=id,slot=slot.GetSlotIdMask()});return item;
        }
        if(mode=="removed-first")
        {
            var first=Add(111300459,ItemSlot.GLOVES);
            Assert.Same(first,equipment.UnEquipItem(first.GetObjectId(),false));
            events.Add(new {operation="remove",owner=first.GetObjectId(),item=first.GetItemId(),slot=0L});
        }
        (int Id,ItemSlot Slot)[] parts=[(110101896,ItemSlot.TORSO),(111101711,ItemSlot.GLOVES),
            (112300427,ItemSlot.SHOULDER),(113300474,ItemSlot.PANTS),(114300490,ItemSlot.BOOTS)];
        foreach(var part in mode=="reverse"?parts.Reverse():parts)Add(part.Id,part.Slot);
        player.GetLifeStats().UpdateCurrentStats();
        File.WriteAllText(Path.Combine(output,"equipment-response-fixture.json"),JsonSerializer.Serialize(new
        {
            mode,player=player.GetObjectId(),level=player.GetLevel(),original,events,temporaryMasteries=learned,
            actual=equipment.GetEquippedItems().Select(x=>new {owner=x.GetObjectId(),item=x.GetItemId(),slot=x.GetEquipmentSlot()}),
            context=RetailNpcStatEquipmentResponses.ReviewedContextSha256,
            scope="Synthetic equipped-item restoration through actual host templates, load admission, listeners and HP owners in a fresh private clone. Includes over-level/test armor solely to exercise damage responses; not natural gear progression or fight-difficulty evidence. Original account/database/gear are untouched."
        }));
        return mode;
    }
}
