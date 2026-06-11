using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>
/// Java parity: skillengine/condition/PlayerMovedCondition (ATracer).
/// </summary>
public class PlayerMovedCondition : Condition
{
    [XmlAttribute("allow")]
    public bool Allow;

    public bool IsAllow()
    {
        return Allow;
    }

    public override bool Validate(Skill skill)
    {
        return Allow == skill.GetMoveListener().IsEffectorMoved();
    }
}
