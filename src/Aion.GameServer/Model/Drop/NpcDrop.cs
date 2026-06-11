using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Model.Drop;

/// <summary>Java parity: model/drop/NpcDrop.</summary>
[XmlRoot("npc_drop")]
public class NpcDrop
{
    [XmlElement("drop_group")] private List<DropGroup> dropGroup;
    [XmlAttribute("npc_id")] private int npcId;

    public List<DropGroup> GetDropGroup()
    {
        return dropGroup;
    }

    public int GetNpcId()
    {
        return npcId;
    }

    public int DropCalculator(ISet<DropItem> result, int index, DropModifiers dropModifiers, ICollection<Player> groupMembers)
    {
        if (dropGroup == null || dropGroup.Count == 0)
            return index;
        foreach (DropGroup dg in dropGroup)
        {
            if (dg.GetRace() == Race.PC_ALL || dg.GetRace() == dropModifiers.GetDropRace())
            {
                index = dg.TryAddDropItems(result, index, dropModifiers, groupMembers);
            }
        }
        return index;
    }
}
