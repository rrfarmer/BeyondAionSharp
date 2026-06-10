using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/PetOrderUseUltraSkillEffect (ATracer, Sippolo) : EffectTemplate. @XmlAttribute release; applyEffect: Player effector; summon==null→return; PET_SKILL_DATA.getPetOrderSkill(orderSkillId, npcId), SKILL_DATA template; null→inline LoggerFactory.warn; addSkillOrder(petUseSkillId, lvl, effected, hate, release) + SM_SUMMON_USESKILL; calculate: Player && effected!=null→base.Calculate(effect,null,null). SkillTemplate/SM_SUMMON_USESKILL red-tolerated.</summary>
[XmlType("PetOrderUseUltraSkillEffect")]
public class PetOrderUseUltraSkillEffect : EffectTemplate
{
    [XmlAttribute]
    protected bool release;

    public override void ApplyEffect(Effect effect)
    {
        Player effector = (Player)effect.GetEffector();

        if (effector.GetSummon() == null)
        {
            return;
        }

        int effectorId = effector.GetSummon().GetObjectId();

        int npcId = effector.GetSummon().GetNpcId();
        int orderSkillId = effect.GetSkillId();

        int petUseSkillId = DataManager.PET_SKILL_DATA.GetPetOrderSkill(orderSkillId, npcId);
        SkillTemplate skillTemplate = DataManager.SKILL_DATA.GetSkillTemplate(petUseSkillId);
        if (skillTemplate == null)
        {
            NullLoggerFactory.Instance.CreateLogger(nameof(PetOrderUseUltraSkillEffect))
                .LogWarning("Couldn't find summon skill template for ID {PetUseSkillId} (summon order skill ID {OrderSkillId})", petUseSkillId, orderSkillId);
            return;
        }

        int skillLvl = skillTemplate.GetLvl();
        int targetId = effect.GetEffected().GetObjectId();
        int hate = effect.GetEffectHate() > 1 ? effect.GetEffectHate() : 0;
        effector.GetSummon().AddSkillOrder(petUseSkillId, skillLvl, effect.GetEffected(), hate, release);
        PacketSendUtility.SendPacket(effector, new SM_SUMMON_USESKILL(effectorId, petUseSkillId, skillLvl, targetId));
    }

    public override void Calculate(Effect effect)
    {
        if (effect.GetEffector() is Player && effect.GetEffected() != null)
            base.Calculate(effect, null, null);
    }
}
