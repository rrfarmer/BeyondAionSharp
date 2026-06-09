using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Restriction;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemRestrictionCleanupData. @XmlRootElement(item_restriction_cleanups).</summary>
[XmlRoot("item_restriction_cleanups")]
public class ItemRestrictionCleanupData
{
    [XmlElement("cleanup")] private List<ItemCleanupTemplate> bplist;

    public int Size()
    {
        return GetList().Count;
    }

    public List<ItemCleanupTemplate> GetList()
    {
        return bplist == null ? new List<ItemCleanupTemplate>() : bplist;
    }

    public bool HasAccountOrLegionWhStorabilityDisabled(int itemId)
    {
        return bplist.Any(t => t.GetId() == itemId && (t.ResultAccountWH() == 0 || t.ResultLegionWH() == 0));
    }
}
