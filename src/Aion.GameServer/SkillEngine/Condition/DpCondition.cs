using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/DpCondition (ATracer).
/// </summary>
public class DpCondition : Condition
{
    [XmlAttribute("value")]
    public int Value;

    public override bool Validate(Skill skill)
    {
        return ((Player)skill.GetEffector()).GetCommonData().GetDp() >= Value;
    }
}
