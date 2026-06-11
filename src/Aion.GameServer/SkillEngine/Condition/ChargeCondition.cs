using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/ChargeCondition (Rolandas). Base for charge conditions.
/// </summary>
public abstract class ChargeCondition : Condition
{
    [XmlAttribute("value")]
    public int value;
}
