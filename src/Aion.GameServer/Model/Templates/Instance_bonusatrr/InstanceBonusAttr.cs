using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Instance_bonusatrr;

/// <summary>Java parity: model/templates/instance_bonusatrr/InstanceBonusAttr (xTz).</summary>
[XmlType("InstanceBonusAttr")]
public class InstanceBonusAttr
{
    [XmlElement("penalty_attr")] public List<InstancePenaltyAttr> penaltyAttr;
    [XmlAttribute("buff_id")] public int buffId;

    /// <summary>Gets the value of the penaltyAttr property.</summary>
    public List<InstancePenaltyAttr> GetPenaltyAttr()
    {
        return penaltyAttr == null ? new List<InstancePenaltyAttr>() : penaltyAttr;
    }

    /// <summary>Gets the value of the buffId property.</summary>
    public int GetBuffId()
    {
        return buffId;
    }

}
