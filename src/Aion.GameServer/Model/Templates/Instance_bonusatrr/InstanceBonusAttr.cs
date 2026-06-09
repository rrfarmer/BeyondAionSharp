using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Instance_bonusatrr;

/// <summary>Java parity: model/templates/instance_bonusatrr/InstanceBonusAttr (xTz).</summary>
[XmlType("InstanceBonusAttr")]
public class InstanceBonusAttr
{
    [XmlElement("penalty_attr")] protected List<InstancePenaltyAttr> penaltyAttr;
    [XmlAttribute("buff_id")] protected int buffId;

    /// <summary>Gets the value of the penaltyAttr property.</summary>
    public List<InstancePenaltyAttr> GetPenaltyAttr()
    {
        if (penaltyAttr == null)
        {
            penaltyAttr = new List<InstancePenaltyAttr>();
        }
        return this.penaltyAttr;
    }

    /// <summary>Gets the value of the buffId property.</summary>
    public int GetBuffId()
    {
        return buffId;
    }

    /// <summary>Sets the value of the buffId property.</summary>
    public void SetBuffId(int value)
    {
        this.buffId = value;
    }
}
