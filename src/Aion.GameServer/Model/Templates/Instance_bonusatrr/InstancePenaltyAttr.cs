using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Change;

namespace Aion.GameServer.Model.Templates.Instance_bonusatrr;

/// <summary>Java parity: model/templates/instance_bonusatrr/InstancePenaltyAttr (xTz).</summary>
[XmlType("InstancePenaltyAttr")]
public class InstancePenaltyAttr
{
    [XmlAttribute("stat")] public StatEnum stat;
    [XmlAttribute("func")] public Func func;
    [XmlAttribute("value")] public int value;

    public StatEnum GetStat()
    {
        return stat;
    }

    public void SetStat(StatEnum value)
    {
        this.stat = value;
    }

    public Func GetFunc()
    {
        return func;
    }

    public void SetFunc(Func value)
    {
        this.func = value;
    }

    public int GetValue()
    {
        return value;
    }

    public void SetValue(int value)
    {
        this.value = value;
    }
}
