using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ProtectEffect (Sippolo, kecimis) : ShieldEffect. AttackShieldObserver 12-arg ctor (null healType); effector instanceof Summon→is Summon: anonymous SUMMONRELEASE observer→nested SummonReleaseObserver, else DeathObserver(_=>endEffect); empty endEffect override; getType()→PROTECT. base Value/Hittype/HitTypeProb red-tolerated.</summary>
[XmlType("ProtectEffect")]
public class ProtectEffect : ShieldEffect
{
    public override void StartEffect(Effect effect)
    {
        AttackShieldObserver asObserver = new AttackShieldObserver(Value, hitvalue, percent, false, effect, Hittype, GetType_(), HitTypeProb, 0, radius,
            null, 0);
        effect.AddObserver(effect.GetEffected(), asObserver);

        if (effect.GetEffector() is Summon)
        {
            effect.AddObserver(effect.GetEffector(), new SummonReleaseObserver(effect));
        }
        else
        {
            effect.AddObserver(effect.GetEffector(), new DeathObserver(_ => effect.EndEffect()));
        }
    }

    public override void EndEffect(Effect effect)
    {
    }

    public override ShieldType GetType_()
    {
        return ShieldType.PROTECT;
    }

    private sealed class SummonReleaseObserver : ActionObserver
    {
        private readonly Effect effect;

        public SummonReleaseObserver(Effect effect)
            : base(ObserverType.SUMMONRELEASE)
        {
            this.effect = effect;
        }

        public override void Summonrelease()
        {
            effect.EndEffect();
        }
    }
}
