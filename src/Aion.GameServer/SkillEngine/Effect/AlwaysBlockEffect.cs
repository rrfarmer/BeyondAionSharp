using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/AlwaysBlockEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; anonymous AttackStatusObserver(value, BLOCK).checkStatus→nested BlockObserver capturing effect, --Value&lt;=0→endEffect. AttackStatusObserver/AttackStatus red-tolerated.</summary>
[XmlType("AlwaysBlockEffect")]
public class AlwaysBlockEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(),RetailNpcStatPhysicalStatuses.BeginDefense(effect,this,RetailNpcStatStatusSources.StatusKind.Block));
    }

    public override void EndEffect(Effect effect)
        =>RetailNpcStatPhysicalStatuses.EndDefense(effect,this,RetailNpcStatStatusSources.StatusKind.Block);
}
