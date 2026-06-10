using Aion.Commons.Utils;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/ProvokerEffect (ATracer, kecimis) : ShieldEffect. @XmlAttribute(name="provoke_target"/"skill_id"); observerType from Hittype NMLATK/BACKATK→ATTACK else ATTACKED; anonymous ActionObserver(attack/attacked + private tryApplyEffect)→nested ProvokeObserver capturing outer+effector; shouldApply switch-expr (PHHIT/MAHIT/BACKATK/default); getProvokeTarget switch-expr (ME/OPPONENT); Rnd.chance→Rnd.Chance; hitTypeProb→HitTypeProb; SM_SYSTEM_MESSAGE.STR_SKILL_PROC_EFFECT_OCCURRED static. ProvokeTarget/SkillType red-tolerated.</summary>
[XmlType("ProvokerEffect")]
public class ProvokerEffect : ShieldEffect
{
    [XmlAttribute("provoke_target")]
    protected ProvokeTarget provokeTarget;
    [XmlAttribute("skill_id")]
    protected int skillId;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        Creature effector = effect.GetEffector();
        ObserverType observerType = Hittype == HitType.NMLATK || Hittype == HitType.BACKATK ? ObserverType.ATTACK : ObserverType.ATTACKED;
        effect.AddObserver(effect.GetEffected(), new ProvokeObserver(this, observerType, effector));
    }

    private bool ShouldApply(Creature effector, Creature target, int attackSkillId)
    {
        if (provokeTarget == ProvokeTarget.OPPONENT && target == effector)
            return false;
        if (radius > 0 && !PositionUtil.IsInRange(effector, target, radius, false))
            return false;
        if (Rnd.Chance() >= HitTypeProb)
            return false;
        return Hittype switch
        {
            HitType.PHHIT => attackSkillId == 0 || DataManager.SKILL_DATA.GetSkillTemplate(attackSkillId).GetType_() == SkillType.PHYSICAL,
            HitType.MAHIT => attackSkillId != 0 && DataManager.SKILL_DATA.GetSkillTemplate(attackSkillId).GetType_() == SkillType.MAGICAL,
            HitType.BACKATK => PositionUtil.IsBehind(effector, target),
            _ => true,
        };
    }

    private Creature GetProvokeTarget(Creature effector, Creature target)
    {
        return provokeTarget switch
        {
            ProvokeTarget.ME => effector,
            ProvokeTarget.OPPONENT => target,
            _ => null,
        };
    }

    private sealed class ProvokeObserver : ActionObserver
    {
        private readonly ProvokerEffect outer;
        private readonly Creature effector;

        public ProvokeObserver(ProvokerEffect outer, ObserverType observerType, Creature effector)
            : base(observerType)
        {
            this.outer = outer;
            this.effector = effector;
        }

        public override void Attack(Creature attacked, int attackSkillId)
        {
            TryApplyEffect(attacked, attackSkillId, effector);
        }

        public override void Attacked(Creature attacker, int attackSkillId)
        {
            TryApplyEffect(attacker, attackSkillId, effector);
        }

        private void TryApplyEffect(Creature target, int attackSkillId, Creature effector)
        {
            if (outer.ShouldApply(effector, target, attackSkillId))
            {
                if (effector is Player player)
                {
                    PacketSendUtility.SendPacket(player,
                        SM_SYSTEM_MESSAGE.STR_SKILL_PROC_EFFECT_OCCURRED(DataManager.SKILL_DATA.GetSkillTemplate(outer.skillId).GetL10n()));
                }
                SkillEngine.GetInstance().ApplyEffectDirectly(outer.skillId, effector, outer.GetProvokeTarget(effector, target));
            }
        }
    }

    public override void EndEffect(Effect effect)
    {
    }
}
