using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/ChangeHateOnAttackedEffect (Sippolo) : EffectTemplate. @XmlAttribute value1(delta)/value2; applyEffect→addToEffectedController; startEffect: finalValue=value1+value2; anonymous ActionObserver(ATTACKED).attacked→nested HateObserver capturing effect+finalValue: creature is Npc→aggroList.addHate(effected, finalValue). Npc red-tolerated.</summary>
[XmlType("ChangeHateOnAttackedEffect")]
public class ChangeHateOnAttackedEffect : EffectTemplate
{
    [XmlAttribute]
    public int value1; // delta
    [XmlAttribute]
    public int value2;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        // TODO: maybe this isn't correct formula?
        int finalValue = value1 + value2;

        effect.AddObserver(effect.GetEffected(), new HateObserver(effect, finalValue));
    }

    private sealed class HateObserver : ActionObserver
    {
        private readonly Effect effect;
        private readonly int finalValue;

        public HateObserver(Effect effect, int finalValue)
            : base(ObserverType.ATTACKED)
        {
            this.effect = effect;
            this.finalValue = finalValue;
        }

        public override void Attacked(Creature creature, int skillId)
        {
            if (creature is Npc)
                creature.GetAggroList().AddHate(effect.GetEffected(), finalValue);
        }
    }
}
