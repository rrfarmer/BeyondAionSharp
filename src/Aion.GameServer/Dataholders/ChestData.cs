using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Chest;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ChestData (Wakizashi). @XmlRootElement(chest_templates); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("chest_templates")]
public class ChestData
{
    // Public so XmlSerializer can populate it (JAXB used a private field via @XmlAccessorType(FIELD)).
    [XmlElement("chest")] public List<ChestTemplate> chests;

    [XmlIgnore] private readonly Dictionary<int, ChestTemplate> chestData = new();

    /// <summary>Initializes all maps for subsequent use - Don't nullify initial chest list as it will be used during reload.</summary>
    public void AfterUnmarshal(object parent)
    {
        chestData.Clear();

        foreach (ChestTemplate chest in chests)
        {
            chestData[chest.GetNpcId()] = chest;
        }
        chests = null;
    }

    public int Size()
    {
        return chestData.Count;
    }

    public ChestTemplate GetChestTemplate(int npcId)
    {
        return chestData.TryGetValue(npcId, out var v) ? v : null;
    }
}
