using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Items;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AssemblyItemsData. @XmlRootElement(assembly_items).</summary>
[XmlRoot("assembly_items")]
public class AssemblyItemsData
{
    [XmlElement("item")] public List<AssemblyItem> items;

    public int Size()
    {
        return items.Count;
    }

    public AssemblyItem GetAssemblyItem(int itemId)
    {
        foreach (AssemblyItem assemblyItem in items)
        {
            if (assemblyItem.GetId() == itemId)
            {
                return assemblyItem;
            }
        }
        return null;
    }
}
