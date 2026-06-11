using System.Xml.Serialization;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/AlwaysResistEffect (ATracer) : EffectTemplate. applyEffect→addToEffectedController; anonymous AttackStatusObserver(value, RESIST).checkStatus→nested ResistObserver capturing effect, --Value&lt;=0→endEffect. AttackStatusObserver/AttackStatus red-tolerated.</summary>
[XmlType("AlwaysResistEffect")]
public class AlwaysResistEffect : EffectTemplate
{
    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        effect.AddObserver(effect.GetEffected(), new ResistObserver(Value, effect));
    }

    private sealed class ResistObserver : AttackStatusObserver
    {
        private readonly Effect effect;

        public ResistObserver(int value, Effect effect)
            : base(value, AttackStatus.RESIST)
        {
            this.effect = effect;
        }

        public override bool CheckStatus(AttackStatus status)
        {
            if (status == AttackStatus.RESIST)
            {
                if (--Value <= 0)
                    effect.EndEffect();
                return true;
            }
            return false;
        }
    }
}
