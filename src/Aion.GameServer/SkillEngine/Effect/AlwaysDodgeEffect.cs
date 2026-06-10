using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/AlwaysDodgeEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; anonymous AttackStatusObserver(value, DODGE).checkStatus→nested DodgeObserver capturing effect, --Value&lt;=0→endEffect. AttackStatusObserver/AttackStatus red-tolerated.</summary>
[XmlType("AlwaysDodgeEffect")]
public class AlwaysDodgeEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new DodgeObserver(Value, effect));
    }

    private sealed class DodgeObserver : AttackStatusObserver
    {
        private readonly Effect effect;

        public DodgeObserver(int value, Effect effect)
            : base(value, AttackStatus.DODGE)
        {
            this.effect = effect;
        }

        public override bool CheckStatus(AttackStatus status)
        {
            if (status == AttackStatus.DODGE)
            {
                if (--Value <= 0)
                    effect.EndEffect();
                return true;
            }
            return false;
        }
    }
}
