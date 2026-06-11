using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;

namespace Aion.GameServer.SkillEngine.Condition;

/// <summary>Java parity: skillengine/condition/TargetCondition (ATracer, kecimis) : Condition. @XmlAttribute value(TargetAttribute); getValue getter; validate: NONE/ALL→true, AREA targetType→true, firstTarget not TARGET/TARGETORME→true, TARGETORME && effector==firstTarget→true; switch(value) NPC→firstTarget is Npc / PC→is Player; !result && Player→STR_SKILL_TARGET_IS_NOT_VALID. TargetAttribute/FirstTargetAttribute/TargetRangeAttribute red-tolerated.</summary>
[XmlType("TargetCondition")]
public class TargetCondition : Condition
{
    [XmlAttribute]
    protected TargetAttribute value;

    public TargetAttribute GetValue()
    {
        return value;
    }

    public override bool Validate(Skill skill)
    {
        if (value == TargetAttribute.NONE || value == TargetAttribute.ALL)
            return true;
        if (skill.GetSkillTemplate().GetProperties().GetTargetType().Equals(TargetRangeAttribute.AREA))
            return true;
        if (skill.GetSkillTemplate().GetProperties().GetFirstTarget() != FirstTargetAttribute.TARGET
            && skill.GetSkillTemplate().GetProperties().GetFirstTarget() != FirstTargetAttribute.TARGETORME)
            return true;
        if (skill.GetSkillTemplate().GetProperties().GetFirstTarget() == FirstTargetAttribute.TARGETORME && skill.GetEffector() == skill.GetFirstTarget())
            return true;

        bool result = false;
        switch (value)
        {
            case TargetAttribute.NPC:
                result = (skill.GetFirstTarget() is Npc);
                break;
            case TargetAttribute.PC:
                result = (skill.GetFirstTarget() is Player);
                break;
        }

        if (!result && skill.GetEffector() is Player)
            PacketSendUtility.SendPacket((Player)skill.GetEffector(), SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());

        return result;
    }
}
