using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/AlwaysParryEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; anonymous AttackStatusObserver(value, PARRY).checkStatus→nested ParryObserver capturing effect, --Value&lt;=0→endEffect. AttackStatusObserver/AttackStatus red-tolerated.</summary>
[XmlType("AlwaysParryEffect")]
public class AlwaysParryEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new ParryObserver(Value, effect));
    }

    private sealed class ParryObserver : AttackStatusObserver
    {
        private readonly Effect effect;

        public ParryObserver(int value, Effect effect)
            : base(value, AttackStatus.PARRY)
        {
            this.effect = effect;
        }

        public override bool CheckStatus(AttackStatus status)
        {
            if (status == AttackStatus.PARRY)
            {
                if (--Value <= 0)
                    effect.EndEffect();
                return true;
            }
            return false;
        }
    }
}
