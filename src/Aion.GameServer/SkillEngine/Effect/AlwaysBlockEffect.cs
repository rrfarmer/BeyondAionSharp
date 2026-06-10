using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

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
        effect.AddObserver(effect.GetEffected(), new BlockObserver(Value, effect));
    }

    private sealed class BlockObserver : AttackStatusObserver
    {
        private readonly Effect effect;

        public BlockObserver(int value, Effect effect)
            : base(value, AttackStatus.BLOCK)
        {
            this.effect = effect;
        }

        public override bool CheckStatus(AttackStatus status)
        {
            if (status == AttackStatus.BLOCK)
            {
                if (--Value <= 0)
                    effect.EndEffect();
                return true;
            }
            return false;
        }
    }
}
