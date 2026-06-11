using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Properties;

/// <summary>Java parity: skillengine/properties/FirstTargetProperty (ATracer). Static set(skill, properties) switch on getFirstTarget(): ME/TARGETORME (auto-retarget logic w/ targetRelation)/TARGET (skill 8000-9000 + NPC dispel exceptions, relation checks)/MYPET/MYMASTER/PASSIVE/TARGET_MYPARTY_NONVISIBLE/POINT; isTargetTeamMember (TemporaryPlayerTeam&lt;?&gt;→&lt;TeamMember&lt;Player&gt;&gt;); isTargetAllowed→TargetRelationProperty.isBuffAllowed; Npc heading update + effectedList.add. instanceof X x→is X x. Properties/enums/Summon red-tolerated.</summary>
public class FirstTargetProperty
{
    public static bool Set(Skill skill, Properties properties)
    {
        Creature effector = skill.GetEffector();
        switch (properties.GetFirstTarget())
        {
            case FirstTargetAttribute.ME:
                skill.SetFirstTargetRangeCheck(false);
                skill.SetFirstTarget(effector);
                break;
            case FirstTargetAttribute.TARGETORME:
                if (effector.Equals(skill.GetFirstTarget()))
                    break;
                bool changeTargetToMe = false;
                if (skill.GetFirstTarget() == null)
                {
                    changeTargetToMe = true;
                }
                else
                {
                    switch (properties.GetTargetRelation())
                    {
                        case TargetRelationAttribute.ENEMY:
                            if (!skill.GetFirstTarget().IsEnemy(effector))
                                changeTargetToMe = true;
                            break;
                        case TargetRelationAttribute.FRIEND:
                            if (skill.GetFirstTarget().IsEnemy(effector))
                                changeTargetToMe = true;
                            break;
                        case TargetRelationAttribute.MYPARTY:
                            if (!IsTargetTeamMember(skill, false))
                            {
                                if (skill.GetFirstTarget().IsEnemy(effector))
                                {
                                    changeTargetToMe = true;
                                }
                                else
                                {
                                    PacketSendUtility.SendPacket((Player)effector, SM_SYSTEM_MESSAGE.STR_SKILL_INVALID_TARGET_PARTY_ONLY());
                                    return false;
                                }
                            }
                            break;
                    }
                    if (!changeTargetToMe && !IsTargetAllowed(skill, skill.GetFirstTarget()))
                        changeTargetToMe = true;
                }
                if (changeTargetToMe)
                {
                    if (skill.GetFirstTarget() != null && effector is Player playerEffector)
                        PacketSendUtility.SendPacket(playerEffector, SM_SYSTEM_MESSAGE.STR_SKILL_AUTO_CHANGE_TARGET_TO_MY());
                    skill.SetFirstTarget(effector);
                }
                break;
            case FirstTargetAttribute.TARGET:
                // Exception for effect skills which are not used directly
                if (skill.GetSkillId() > 8000 && skill.GetSkillId() < 9000)
                    break;
                // Exception for NPC skills which applied on players
                if (skill.GetSkillTemplate().GetDispelCategory() == DispelCategoryType.NPC_BUFF
                    || skill.GetSkillTemplate().GetDispelCategory() == DispelCategoryType.NPC_DEBUFF_PHYSICAL)
                    break;

                TargetRelationAttribute relation = skill.GetSkillTemplate().GetProperties().GetTargetRelation();
                if (skill.GetFirstTarget() == null || skill.GetFirstTarget().Equals(effector))
                {
                    if (effector is Player playerEffector2)
                    {
                        if (skill.GetSkillTemplate().GetProperties().GetTargetType() == TargetRangeAttribute.AREA)
                            return skill.GetFirstTarget() != null;

                        TargetRangeAttribute type = skill.GetSkillTemplate().GetProperties().GetTargetType();
                        if ((relation != TargetRelationAttribute.ALL && relation != TargetRelationAttribute.MYPARTY && relation != TargetRelationAttribute.FRIEND)
                            || type == TargetRangeAttribute.PARTY)
                        {
                            PacketSendUtility.SendPacket(playerEffector2, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                            return false;
                        }
                    }
                }

                if (relation == TargetRelationAttribute.FRIEND)
                {
                    if (skill.GetFirstTarget() == null || effector.IsEnemy(skill.GetFirstTarget()))
                    {
                        if (effector is Player playerEffector3)
                            PacketSendUtility.SendPacket(playerEffector3, SM_SYSTEM_MESSAGE.STR_SKILL_INVALID_TARGET_NOTENEMY_ONLY());
                        return false;
                    }
                }
                else if (relation == TargetRelationAttribute.MYPARTY)
                {
                    if (!IsTargetTeamMember(skill, false))
                    {
                        if (effector is Player playerEffector4)
                            PacketSendUtility.SendPacket(playerEffector4, SM_SYSTEM_MESSAGE.STR_SKILL_INVALID_TARGET_PARTY_ONLY());
                        return false;
                    }
                }
                else if (relation != TargetRelationAttribute.ENEMY && !IsTargetAllowed(skill, skill.GetFirstTarget()))
                {
                    if (effector is Player playerEffector5)
                    {
                        PacketSendUtility.SendPacket(playerEffector5, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                    }
                    return false;
                }
                break;
            case FirstTargetAttribute.MYPET:
                if (effector is Player playerEffector6)
                {
                    Summon summon = playerEffector6.GetSummon();
                    if (summon == null || !IsTargetAllowed(skill, summon))
                    {
                        PacketSendUtility.SendPacket(playerEffector6, SM_SYSTEM_MESSAGE.STR_SKILL_INVALID_TARGET_PET_ONLY());
                        return false;
                    }
                    skill.SetFirstTarget(summon);
                }
                else
                {
                    return false;
                }
                break;
            case FirstTargetAttribute.MYMASTER:
                if (effector is Summon summon2)
                {
                    if (summon2.GetMaster() != null)
                        skill.SetFirstTarget(summon2.GetMaster());
                    else
                        return false;
                }
                else
                {
                    return false;
                }
                break;
            case FirstTargetAttribute.PASSIVE:
                skill.SetFirstTarget(effector);
                break;
            case FirstTargetAttribute.TARGET_MYPARTY_NONVISIBLE: // Summon Group Member
                if (!IsTargetTeamMember(skill, true))
                    return false;

                skill.SetFirstTargetRangeCheck(false);
                break;
            case FirstTargetAttribute.POINT:
                skill.SetFirstTarget(effector);
                return true;
        }

        if (skill.GetFirstTarget() != null)
        {
            // update heading for npcs (players may look in a different direction)
            if (effector is Npc && !effector.Equals(skill.GetFirstTarget()))
                effector.GetPosition().SetH(PositionUtil.GetHeadingTowards(effector, skill.GetFirstTarget()));
            skill.GetEffectedList().Add(skill.GetFirstTarget());
        }
        return true;
    }

    private static bool IsTargetTeamMember(Skill skill, bool onlyGroup)
    {
        if (skill.GetFirstTarget() is Player && skill.GetEffector() is Player)
        {
            Player effector = (Player)skill.GetEffector();
            TemporaryPlayerTeam<TeamMember<Player>> team = onlyGroup ? effector.GetCurrentGroup() : effector.GetCurrentTeam();
            if (team != null)
            {
                foreach (Player member in team.GetMembers())
                {
                    if (member.Equals(skill.GetFirstTarget()) && !member.Equals(skill.GetEffector()))
                        return true;
                }
            }
        }
        return false;
    }

    /// <summary>true = allow buff, false = deny buff</summary>
    public static bool IsTargetAllowed(Skill skill, Creature target)
    {
        Creature source = skill.GetEffector();
        return TargetRelationProperty.IsBuffAllowed(source, target);
    }
}
