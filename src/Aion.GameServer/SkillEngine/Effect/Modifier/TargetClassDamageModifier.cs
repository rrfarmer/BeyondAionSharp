using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.SkillEngine.Effects.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/TargetClassDamageModifier (Rolandas).
/// </summary>
public class TargetClassDamageModifier : ActionModifier
{
    [XmlAttribute("class")]
    public PlayerClass skillTargetClass;

    public override int Analyze(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Player)
        {
            Player player = (Player)effected;
            if (player.GetPlayerClass() == skillTargetClass)
            {
                return Value + effect.GetSkillLevel() * Delta;
            }
        }
        return 0;
    }

    public override bool Check(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Player)
        {
            Player player = (Player)effected;
            return player.GetPlayerClass() == skillTargetClass;
        }
        return false;
    }
}
