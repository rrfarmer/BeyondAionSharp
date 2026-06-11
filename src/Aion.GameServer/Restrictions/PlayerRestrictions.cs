using Aion.GameServer;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Panels;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Questengine;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Ban;
using Aion.GameServer.Services.Players;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World.Zone;

namespace Aion.GameServer.Restrictions;

/// <summary>
/// Java parity: restrictions/PlayerRestrictions (lord_rex, Sippolo). Static can*-restriction checks.
/// instanceof-pattern→is-pattern; ChatUtil.l10n→L10n; signed byte→sbyte; Gender/Race nullable→?.
/// NOTE: canInviteToTeam's TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt; param→TemporaryPlayerTeam&lt;TeamMember&lt;Player&gt;&gt;
/// (codebase bound); the 2 call-sites passing PlayerGroup/PlayerAlliance are red under C# invariance — red-tolerated,
/// fixed by the deferred non-generic TemporaryPlayerTeam base (task_f13a70b1). Most skillengine/service/packet deps red-tolerated.
/// </summary>
public class PlayerRestrictions
{
    private static bool CheckFly(Player player, VisibleObject target)
    {
        if (player.IsUsingFlightTransporterOrWindstream())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_RESTRICTION_NO_FLY());
            return false;
        }

        if (target is Player playerTarget && playerTarget.IsUsingFlightTransporterOrWindstream())
        {
            return false;
        }
        return true;
    }

    public static bool CanUseSkill(Player player, Skill skill)
    {
        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_TARGET_IS_NOT_VALID());
            return false;
        }
        VisibleObject target = player.GetTarget();
        SkillTemplate template = skill.GetSkillTemplate();

        // TODO check if its ok
        if (!CheckFly(player, target) || player.GetLifeStats().IsAboutToDie() || player.IsDead())
        {
            return false;
        }
        // check if is casting to avoid multicast exploit
        // TODO cancel skill if other is used
        if (player.IsCasting())
            return false;

        if (!player.CanAttack() && !template.HasEvadeEffect())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE());
            return false;
        }

        // in 3.0 players can use remove shock even when silenced
        if (template.GetType_() == SkillType.MAGICAL && player.GetEffectController().IsAbnormalSet(AbnormalState.SILENCE) && !template.HasEvadeEffect())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_MAGIC_SKILL_WHILE_SILENCED());
            return false;
        }

        if (template.GetType_() == SkillType.PHYSICAL && player.GetEffectController().IsAbnormalSet(AbnormalState.BIND))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_PHYSICAL_SKILL_IN_FEAR());
            return false;
        }

        if (player.IsSkillDisabled(template))
            return false;

        // cannot use skills while transformed
        if (player.GetTransformModel().IsActive())
        {
            if (player.GetTransformModel().GetBanUseSkills() == 1)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_CAST_IN_SHAPECHANGE());
                return false;
            }
            // can use only panel skills in FORM1
            if (player.GetTransformModel().GetType_() == TransformType.FORM1)
            {
                SkillPanel panel = DataManager.PANEL_SKILL_DATA.GetSkillPanel(player.GetTransformModel().GetPanelId());
                if (panel == null || !panel.IsSkillPresent(skill.GetSkillId()))
                {
                    AuditLogger.Log(player, "tried to use non panel skill while transformed in TransformType.FORM1");
                    return false;
                }
            }
        }

        // Fix for Summon Group Member, cannot be used while either caster or summoned is actively in combat
        // example skillId: 1606
        if (skill.GetSkillTemplate().HasRecallInstant())
        {
            if (!(target is Player))
                return false;
            if (player.GetController().IsInCombat() || ((Player)target).GetController().IsInCombat()
                || ((Player)target).GetTransformModel().GetRes1() == 1)// cannot be summoned while transformed
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_Recall_CANNOT_ACCEPT_EFFECT(target.GetName()));
                return false;
            }
        }

        if (template.HasResurrectEffect())
        {
            if (!(target is Player))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                return false;
            }
            Player targetPlayer = (Player)target;
            if (!targetPlayer.IsDead())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_TARGET_IS_NOT_VALID());
                return false;
            }
        }

        return true;
    }

    public static bool CanInviteToGroup(Player player, Player target)
    {
        return CanInviteToTeam(player, target, false, player.GetPlayerGroup());
    }

    public static bool CanInviteToAlliance(Player player, Player target)
    {
        return CanInviteToTeam(player, target, true, player.GetPlayerAlliance());
    }

    private static bool CanInviteToTeam(Player player, Player target, bool isAlliance, TemporaryPlayerTeam<TeamMember<Player>> team)
    {
        if (player.IsDead())
        {
            PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_CANT_INVITE_WHEN_DEAD() : SM_SYSTEM_MESSAGE.STR_PARTY_CANT_INVITE_WHEN_DEAD());
            return false;
        }
        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_INVITE_PARTY_COMMAND());
            return false;
        }
        if (target == null)
        {
            PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_NO_USER_TO_INVITE() : SM_SYSTEM_MESSAGE.STR_PARTY_NO_USER_TO_INVITE());
            return false;
        }
        if (target.IsInCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS) && !target.IsInFfaTeamMode()
                || player.IsInCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS) && !player.IsInFfaTeamMode())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DISABLE("FFA mode"));
            return false;
        }
        if (AutoGroupService.GetInstance().IsInAutoInstance(player) || AutoGroupService.GetInstance().IsInAutoInstance(target))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_CANT_INVITE_PARTY_COMMAND());
            return false;
        }
        if (team != null)
        {
            if (!team.IsLeader(player) && (!(team is PlayerAlliance alliance) || !alliance.IsViceCaptain(player)))
            {
                PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_ONLY_LEADER_CAN_INVITE() : SM_SYSTEM_MESSAGE.STR_PARTY_ONLY_LEADER_CAN_INVITE());
                return false;
            }
            if (team.IsFull())
            {
                PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_CANT_ADD_NEW_MEMBER() : SM_SYSTEM_MESSAGE.STR_PARTY_CANT_ADD_NEW_MEMBER());
                return false;
            }
        }
        if (target.Equals(player))
        {
            PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_CAN_NOT_INVITE_SELF() : SM_SYSTEM_MESSAGE.STR_PARTY_CAN_NOT_INVITE_SELF());
            return false;
        }
        if (target.GetRace() != player.GetRace() && (isAlliance ? !GroupConfig.ALLIANCE_INVITEOTHERFACTION : !GroupConfig.GROUP_INVITEOTHERFACTION))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PARTY_CANT_INVITE_OTHER_RACE());
            return false;
        }
        if (target.IsDead())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_UI_PARTY_DEAD());
            return false;
        }
        TemporaryPlayerTeam<TeamMember<Player>> targetTeam = target.GetCurrentTeam();
        if (targetTeam != null)
        {
            if (targetTeam == team)
            {
                PacketSendUtility.SendPacket(player, isAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_HE_IS_ALREADY_MEMBER_OF_OUR_FORCE(target.GetName()) : SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_ALREADY_MEMBER_OF_OUR_PARTY(target.GetName()));
                return false;
            }
            if (isAlliance && targetTeam is PlayerGroup targetGroup)
            {
                if (team != null && targetGroup.Size() + team.Size() > team.GetMaxMemberCount())
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FORCE_INVITE_FAILED_NOT_ENOUGH_SLOT());
                    return false;
                }
            }
            else
            {
                PacketSendUtility.SendPacket(player, targetTeam is PlayerAlliance ? SM_SYSTEM_MESSAGE.STR_FORCE_ALREADY_OTHER_FORCE(target.GetName()) : SM_SYSTEM_MESSAGE.STR_PARTY_HE_IS_ALREADY_MEMBER_OF_OTHER_PARTY(target.GetName()));
                return false;
            }
        }
        if (team is PlayerAlliance alliance2 && alliance2.GetTeamType().IsDefence())
        {
            if (targetTeam != null)
            {
                foreach (Player tm in targetTeam.GetMembers())
                {
                    if (tm.IsInInstance())
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FORCE_CANT_INVITE_WHEN_HE_IS_IN_INSTANCE());
                        return false;
                    }
                    else if (!VortexService.GetInstance().IsInsideVortexZone(tm))
                    {
                        // TODO: chk on retail
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_PARTY_ALLIANCE_CANT_INVITE_WHEN_HE_IS_ASKED_QUESTION(tm.GetName()));
                        return false;
                    }
                }
            }
            else if (!VortexService.GetInstance().IsInsideVortexZone(target))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_INVITE_DEFENSE_FORCE());
                return false;
            }
        }
        return true;
    }

    public static bool CanAttack(Player player, VisibleObject target)
    {
        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_TARGET_IS_NOT_VALID());
            return false;
        }

        if (!player.IsSpawned() || target == null || !CheckFly(player, target) || player.GetLifeStats().IsAboutToDie() || player.IsDead())
            return false;

        if (!player.CanAttack())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_ATTACK_WHILE_IN_ABNORMAL_STATE());
            PacketSendUtility.SendPacket(player, SM_ATTACK_RESPONSE.STOP_WITHOUT_MESSAGE(player.GetGameStats().GetAttackCounter()));
            return false;
        }

        if (!(target is Creature))
        {
            PacketSendUtility.SendPacket(player, SM_ATTACK_RESPONSE.STOP_INVALID_TARGET(player.GetGameStats().GetAttackCounter()));
            return false;
        }

        Creature creature = (Creature)target;

        if (creature.IsDead() || creature.GetLifeStats().IsAboutToDie())
        {
            PacketSendUtility.SendPacket(player, SM_ATTACK_RESPONSE.STOP_INVALID_TARGET(player.GetGameStats().GetAttackCounter()));
            return false;
        }

        // cannot attack while transformed
        if (player.GetTransformModel().GetRes3() == 1)
        {
            return false;
        }

        return player.IsEnemy(creature);
    }

    public static bool CanTrade(Player player)
    {
        if (player == null || player.IsDead() || !player.IsOnline())
            return false;
        if (GameServer.IsShuttingDownSoon())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DISABLE("Shutdown Progress"));
            return false;
        }
        if (player.IsTrading())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_EXCHANGE_PARTNER_IS_EXCHANGING_WITH_OTHER());
            return false;
        }
        return true;
    }

    public static bool CanChat(Player player)
    {
        if (player == null || !player.IsOnline())
            return false;

        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INGAME_BLOCK_IN_NO_CHAT(player.GetPrisonDurationSeconds() / 60 + 1));
            return false;
        }

        if (ChatBanService.IsBanned(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_INGAME_BLOCK_IN_NO_CHAT(ChatBanService.GetBanMinutes(player)));
            return false;
        }

        if (PlayerChatService.IsFlooding(player))
        {
            ChatBanService.BanPlayer(player, 2 * 60 * 1000);
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_FLOODING());
            return false;
        }

        return true;
    }

    public static bool CanUseItem(Player player, Item item)
    {
        if (player == null || !player.IsOnline())
            return false;

        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_TARGET_IS_NOT_VALID());
            return false;
        }

        if (player.GetLifeStats().IsAboutToDie() || player.IsDead())
            return false;

        if (player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_ITEM_WHILE_IN_ABNORMAL_STATE());
            return false;
        }

        // cannot use item while transformed
        if (player.GetTransformModel().GetRes5() == 1)
        {
            // client sends message by itself
            return false;
        }

        if (player.GetStore() != null) // You cannot use an item while running a Private Store.
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_USE_ITEM_DURING_PATH_FLYING(ChatUtil.L10n(1400061)));
            return false;
        }

        // Prevents potion spamming, and relogging to use kisks/aether jelly/long CD items.
        if (player.HasCooldown(item))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANT_USE_UNTIL_DELAY_TIME());
            return false;
        }

        ItemActions itemActions = item.GetItemTemplate().GetActions();
        if (itemActions == null || itemActions.GetItemActions().Count == 0)
        {
            if (!QuestEngine.GetInstance().IsRegisteredQuestItem(item.GetItemId()))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE());
                return false;
            }
        }

        ItemUseLimits limits = item.GetItemTemplate().GetUseLimits();
        if (limits.GetGenderPermitted() != null && limits.GetGenderPermitted() != player.GetGender())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_GENDER());
            return false;
        }

        if (item.GetItemTemplate().GetRace() != Race.PC_ALL && item.GetItemTemplate().GetRace() != player.GetRace())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_RACE());
            return false;
        }

        if (!item.GetItemTemplate().IsClassSpecific(player.GetCommonData().GetPlayerClass()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_INVALID_CLASS());
            return false;
        }

        int requiredLevel = item.GetItemTemplate().GetRequiredLevel(player.GetPlayerClass());
        if (requiredLevel > player.GetLevel())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_TOO_LOW_LEVEL_MUST_BE_THIS_LEVEL(item.GetL10n(), requiredLevel));
            return false;
        }

        sbyte levelRestrict = item.GetItemTemplate().GetMaxLevelRestrict(player.GetPlayerClass());
        if (levelRestrict != 0 && player.GetLevel() > levelRestrict)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANNOT_USE_ITEM_TOO_HIGH_LEVEL(levelRestrict, item.GetL10n()));
            return false;
        }

        if (item.GetItemTemplate().HasAreaRestriction())
        {
            ZoneName restriction = item.GetItemTemplate().GetUseArea();
            if (!player.IsInsideItemUseZone(restriction))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_USE_ITEM_IN_CURRENT_POSITION());
                return false;
            }
        }

        if (item.GetItemTemplate().GetActivationRace() != null)
        {
            // TODO: check retail messages
            if (!(player.GetTarget() is Creature))
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_CANT_FIND_VALID_TARGET());
                return false;
            }
            if (((Creature)player.GetTarget()).GetRace() != item.GetItemTemplate().GetActivationRace())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CANT_CAST_TO_CURRENT_TARGET());
                return false;
            }
        }

        return true;
    }

    public static bool CanChangeEquip(Player player)
    {
        if (player.IsInPrison())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_ACCUSE_TARGET_IS_NOT_VALID());
            return false;
        }
        if (player.GetController().IsUnderStance())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_EQUIP_ITEM_WHILE_IN_CURRENT_STANCE());
            return false;
        }
        if (player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CANT_ATTACK_STATE))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_SKILL_CAN_NOT_EQUIP_ITEM_WHILE_IN_ABNORMAL_STATE());
            return false;
        }
        if (player.GetController().HasScheduledTask(TaskId.ITEM_USE))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CANT_EQUIP_ITEM_IN_ACTION());
            return false;
        }
        return true;
    }
}
