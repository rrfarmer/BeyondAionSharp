using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/RecallInstantEffect (Bio, Sippolo, SVDNESS) : EffectTemplate. Delegates to RecallService.</summary>
[XmlType("RecallInstantEffect")]
public class RecallInstantEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        if (effect.GetEffector() is Player caster && effect.GetEffected() is Player effected)
            RecallService.GetInstance().RequestSummon(caster, effected, effect.GetSkillId());
    }

    public override void Calculate(Effect effect)
    {
        Creature effector = effect.GetEffector();
        if (RecallService.CanBeSummoned(effector, effect.GetEffected()))
        {
            effect.GetSkill().SetTargetPosition(effector.GetX(), effector.GetY(), effector.GetZ(), (sbyte)effector.GetHeading());
            effect.AddSuccessEffect(this);
        }
    }
}
