using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Model.Drop;

/// <summary>Java parity: model/drop/DropGroup.</summary>
[XmlRoot("drop_group")]
public class DropGroup
{
    [XmlElement("drop")] private List<Drop> drops;
    [XmlAttribute("race")] private Race race = Race.PC_ALL;
    [XmlAttribute("name")] private string name;
    [XmlAttribute("level_based_chance_reduction")] private bool useLevelBasedChanceReduction;
    [XmlAttribute("max_items")] private int maxItems = 1;

    public List<Drop> GetDrop()
    {
        return drops;
    }

    public Race GetRace()
    {
        return race;
    }

    public int GetMaxItems()
    {
        return maxItems;
    }

    public string GetName()
    {
        return name;
    }

    public bool IsUseLevelBasedChanceReduction()
    {
        return useLevelBasedChanceReduction;
    }

    public int TryAddDropItems(ISet<DropItem> result, int index, DropModifiers dropModifiers, ICollection<Player> groupMembers)
    {
        ISet<Drop> remainingDrops = new HashSet<Drop>(drops);
        for (int i = 0; i < maxItems && remainingDrops.Count != 0; i++)
        {
            float chance = Rnd.Chance();
            float nearestChanceDiff = float.MaxValue;
            List<Drop> nearestDropsOfSameChance = new List<Drop>();
            foreach (Drop drop in remainingDrops)
            {
                float finalChance = dropModifiers.CalculateDropChance(drop.GetChance(), IsUseLevelBasedChanceReduction());
                if (chance < finalChance)
                {
                    float chanceDiff = finalChance - chance;
                    if (nearestDropsOfSameChance.Count == 0 || chanceDiff <= nearestChanceDiff)
                    {
                        if (chanceDiff < nearestChanceDiff)
                        {
                            nearestDropsOfSameChance.Clear();
                            nearestChanceDiff = chanceDiff;
                        }
                        nearestDropsOfSameChance.Add(drop);
                    }
                }
            }
            Drop drop2 = Rnd.Get(nearestDropsOfSameChance);
            if (drop2 != null)
            {
                index = AddDropItem(index, result, drop2, groupMembers);
                remainingDrops.Remove(drop2);
            }
        }
        return index;
    }

    private int AddDropItem(int index, ISet<DropItem> result, Drop drop, ICollection<Player> groupMembers)
    {
        if (drop.IsEachMember() && groupMembers != null && groupMembers.Count != 0)
        {
            foreach (Player player in groupMembers)
            {
                DropItem dropitem = new DropItem(drop);
                dropitem.CalculateCount();
                dropitem.SetIndex(index++);
                dropitem.SetPlayerObjId(player.GetObjectId());
                dropitem.SetWinningPlayer(player);
                dropitem.IsDistributeItem(true);
                result.Add(dropitem);
            }
        }
        else
        {
            DropItem dropitem = new DropItem(drop);
            dropitem.CalculateCount();
            dropitem.SetIndex(index++);
            result.Add(dropitem);
        }
        return index;
    }
}
