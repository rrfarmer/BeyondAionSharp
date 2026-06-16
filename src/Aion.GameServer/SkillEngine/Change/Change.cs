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
    // Java parity: @XmlAttribute("stat") StatEnum (nullable). XmlSerializer cannot bind Nullable<T> as an
    // attribute, so round-trip via a string proxy (null when the attribute is absent / unparseable, mirroring
    // JAXB's lenient default unmarshal-event handler).
    [XmlIgnore]
    public StatEnum? Stat;

    [XmlAttribute("stat")]
    public string? StatRaw
    {
        get => Stat?.ToString();
        set => Stat = value == null ? (StatEnum?)null : (System.Enum.TryParse<StatEnum>(value, out var v) ? v : (StatEnum?)null);
    }

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
