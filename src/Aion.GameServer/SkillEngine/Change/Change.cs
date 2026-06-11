using System.Xml.Serialization;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Condition;

namespace Aion.GameServer.SkillEngine.Change;

/// <summary>
/// Java parity: skillengine/change/Change (ATracer). A conditional stat change applied by an effect.
/// Namespace is lowercase 'change' to avoid the FQN clash with this class name.
/// </summary>
public class Change
{
    [XmlAttribute("stat")]
    public StatEnum? Stat;

    [XmlAttribute("func")]
    public Func Func;

    [XmlAttribute("value")]
    public int Value;

    [XmlAttribute("delta")]
    public int Delta;

    [XmlElement("conditions")]
    public Conditions? Conditions;

    public StatEnum? GetStat()
    {
        return Stat;
    }

    public Func GetFunc()
    {
        return Func;
    }

    public int GetValue()
    {
        return Value;
    }

    public int GetDelta()
    {
        return Delta;
    }

    public Conditions? GetConditions()
    {
        return Conditions;
    }
}
