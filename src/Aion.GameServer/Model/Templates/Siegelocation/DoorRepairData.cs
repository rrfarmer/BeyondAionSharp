using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Siegelocation;

/// <summary>Java parity: model/templates/siegelocation/DoorRepairData.</summary>
[XmlType("DoorRepairData")]
public class DoorRepairData
{
    [XmlElement("door_repair_stone")] private List<DoorRepairStone> doorRepairTemplates;

    [XmlAttribute("item_id")] protected int itemId;
    [XmlAttribute("count")] protected int count;
    [XmlAttribute("cd")] protected int cd;

    [XmlIgnore] private Dictionary<int, DoorRepairStone> doorRepairStones = new Dictionary<int, DoorRepairStone>();

    // Java parity: afterUnmarshal(Unmarshaller, Object parent).
    public void AfterUnmarshal(object parent)
    {
        foreach (DoorRepairStone repairStone in doorRepairTemplates)
        {
            doorRepairStones[repairStone.staticId] = repairStone;
        }
        doorRepairTemplates = null;
    }

    public int GetItemId()
    {
        return itemId;
    }

    public int GetCount()
    {
        return count;
    }

    public int GetCd()
    {
        return cd * 1000;
    }

    public DoorRepairStone GetRepairStone(int stoneStaticId)
    {
        return doorRepairStones.TryGetValue(stoneStaticId, out var v) ? v : null;
    }

    public ICollection<DoorRepairStone> GetRepairStones()
    {
        return doorRepairStones.Values;
    }
}
