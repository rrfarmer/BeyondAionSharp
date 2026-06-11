using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/SwitchHostileEffect (Luzien) : EffectTemplate. applyEffect: effector instanceof Player player→summon else null; if summon: swap hate between effector and summon (stopHating both, addHate cross). AggroList red-tolerated.</summary>
[XmlType("SwitchHostileEffect")]
public class SwitchHostileEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        Creature summon = effector is Player player ? player.GetSummon() : null;
        if (summon != null)
        {
            AggroList aggroList = effect.GetEffected().GetAggroList();
            int playerHate = aggroList.GetHate(effector);
            int summonHate = aggroList.GetHate(summon);
            aggroList.StopHating(summon);
            aggroList.StopHating(effector);
            aggroList.AddHate(effector, summonHate);
            aggroList.AddHate(summon, playerHate);
        }
    }
}
