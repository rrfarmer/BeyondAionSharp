using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Autogroup;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/AutoGroupData. @XmlRootElement(auto_groups); computeIfAbsent→TryGetValue+init; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("auto_groups")]
public class AutoGroupData
{
    [XmlElement("auto_group")] protected List<AutoGroup> autoGroup;

    [XmlIgnore] private readonly Dictionary<int, AutoGroup> autoGroupByInstanceId = new();
    [XmlIgnore] private readonly Dictionary<int, List<int>> recruitableInstanceMaskIdByPortalNpc = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (AutoGroup ag in autoGroup)
        {
            autoGroupByInstanceId[ag.GetMaskId()] = ag;
            if (ag.IsRecruitableInstance())
            {
                foreach (int npcId in ag.GetNpcIds())
                {
                    if (!recruitableInstanceMaskIdByPortalNpc.TryGetValue(npcId, out var list))
                    {
                        list = new List<int>();
                        recruitableInstanceMaskIdByPortalNpc[npcId] = list;
                    }
                    list.Add(ag.GetMaskId());
                }
            }
        }
        autoGroup = null;
    }

    public AutoGroup GetTemplateByInstanceMaskId(int maskId)
    {
        return autoGroupByInstanceId.TryGetValue(maskId, out var v) ? v : null;
    }

    public List<int> GetRecruitableInstanceMaskIds(int portalNpcId)
    {
        return recruitableInstanceMaskIdByPortalNpc.TryGetValue(portalNpcId, out var v) ? v : null;
    }

    public List<int> GetRecruitableInstanceMaskIds()
    {
        return recruitableInstanceMaskIdByPortalNpc.Values.SelectMany(v => v).Distinct().ToList();
    }

    public int Size()
    {
        return autoGroupByInstanceId.Count;
    }
}
