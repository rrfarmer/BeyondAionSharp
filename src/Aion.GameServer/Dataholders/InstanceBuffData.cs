using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Instance_bonusatrr;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/InstanceBuffData. @XmlRootElement(instance_bonusattrs); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("instance_bonusattrs")]
public class InstanceBuffData
{
    [XmlElement("instance_bonusattr")] protected List<InstanceBonusAttr> instanceBonusattr;

    [XmlIgnore] private readonly Dictionary<int, InstanceBonusAttr> templates = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (InstanceBonusAttr template in instanceBonusattr)
        {
            templates[template.GetBuffId()] = template;
        }
        instanceBonusattr = null;
    }

    public int Size()
    {
        return templates.Count;
    }

    public InstanceBonusAttr GetInstanceBonusattr(int buffId)
    {
        return templates.TryGetValue(buffId, out var v) ? v : null;
    }
}
