using System;
using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/GatherableController (ATracer, sphinx, Cura).</summary>
public class GatherableController : VisibleObjectController<Gatherable>
{
    private int gatherCount;
    private Aion.GameServer.SkillEngine.Task.GatheringTask gatheringTask;

    public void StartGathering(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        Aion.GameServer.Model.Templates.Gather.GatherableTemplate template = GetOwner().GetObjectTemplate();
        if (player.GetLevel() < template.GetLevelLimit())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANT_GATHERING_B_LEVEL_CHECK(template.GetLevelLimit()));
            return;
        }
        if (player.IsInPlayerMode(PlayerMode.RIDE) && !player.HasPermission(MembershipConfig.GATHERING_ALLOW_ON_MOUNT))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_GATHER_RESTRICTION_RIDE());
            return;
        }
        if (player.GetInventory().IsFull())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_INVENTORY_IS_FULL());
            return;
        }
        if (player.GetController().IsUnderStance())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_CAN_NOT_GATHER_WHILE_IN_CURRENT_STANCE());
            return;
        }
        if (!Aion.GameServer.Utils.PositionUtil.IsInRange(GetOwner(), player, 3, false))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_TOO_FAR_FROM_GATHER_SOURCE());
            return;
        }
        if (!Aion.GameServer.World.Geo.GeoService.GetInstance().CanSee(player, GetOwner()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_OBSTACLE_EXIST());
            return;
        }
        if (player.IsGatherRestricted())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CAPTCHA_REMAIN_RESTRICT_TIME(player.GetGatherRestrictionDurationSeconds()));
            return;
        }

        if (!CheckPlayerSkill(player, template))
            return;

        List<Aion.GameServer.Model.Templates.Gather.Material> materials = GetMaterials(player, template);
        if (materials == null)
            return;

        // CAPTCHA
        if (SecurityConfig.CAPTCHA_ENABLE)
        {
            if (SecurityConfig.CAPTCHA_APPEAR.Equals(template.GetSourceType()) || SecurityConfig.CAPTCHA_APPEAR.Equals("ALL"))
            {
                int rate = SecurityConfig.CAPTCHA_APPEAR_RATE;
                if (template.GetCaptchaRate() > 0)
                    rate = (int)(template.GetCaptchaRate() * 0.1f);

                if (Aion.Commons.Utils.Rnd.Chance() < rate)
                {
                    player.SetCaptchaWord(Aion.GameServer.Utils.Captcha.CAPTCHAUtil.GetRandomWord());
                    player.SetCaptchaImage(Aion.GameServer.Utils.Captcha.CAPTCHAUtil.CreateCAPTCHA(player.GetCaptchaWord()).Array());
                    Aion.GameServer.Services.PunishmentService.SetIsNotGatherable(player, 0, true, SecurityConfig.CAPTCHA_EXTRACTION_BAN_TIME * 1000L);
                }
            }
        }

        int chance = Aion.Commons.Utils.Rnd.NextInt(10000000);
        int current = 0;
        Aion.GameServer.Model.Templates.Gather.Material curMaterial = null;
        foreach (Aion.GameServer.Model.Templates.Gather.Material mat in materials)
        {
            current += mat.GetRate();
            if (current >= chance)
            {
                curMaterial = mat;
                break;
            }
        }

        lock (this)
        {
            if (gatheringTask != null)
            {
                // sends STR_EXTRACT_GATHER_OCCUPIED_BY_OTHER and makes the client deselect the targeted gatherable
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmGatherUpdate(template, curMaterial, 0, 0, 8, 0, 0));
                return;
            }
            int skillLvlDiff = player.GetSkillList().GetSkillLevel(template.GetHarvestSkill()) - template.GetSkillLevel();
            gatheringTask = new Aion.GameServer.SkillEngine.Task.GatheringTask(player, GetOwner(), curMaterial, skillLvlDiff);
            gatheringTask.Start();
        }
    }

    /// <summary>Checks whether player has the needed skill for gathering and skill level is sufficient.</summary>
    private bool CheckPlayerSkill(Aion.GameServer.Model.GameObjects.Players.Player player, Aion.GameServer.Model.Templates.Gather.GatherableTemplate template)
    {
        int harvestSkillId = template.GetHarvestSkill();
        if (!player.GetSkillList().IsSkillPresent(harvestSkillId))
        {
            if (harvestSkillId == 30001)
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_INCORRECT_SKILL());
            }
            else
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                    Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_LEARN_SKILL(DataManager.SKILL_DATA.GetSkillTemplate(harvestSkillId).GetL10n()));
            }
            return false;
        }
        if (player.GetSkillList().GetSkillLevel(harvestSkillId) < template.GetSkillLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_GATHER_OUT_OF_SKILL_POINT(DataManager.SKILL_DATA.GetSkillTemplate(harvestSkillId).GetL10n()));
            return false;
        }
        return true;
    }

    private List<Aion.GameServer.Model.Templates.Gather.Material> GetMaterials(Aion.GameServer.Model.GameObjects.Players.Player player, Aion.GameServer.Model.Templates.Gather.GatherableTemplate template)
    {
        if (template.GetRequiredItemId() > 0)
        {
            if (template.GetCheckType() == 1)
            {
                bool hasRequiredItemEquipped = player.GetEquipment().GetEquippedItemsByItemId(template.GetRequiredItemId()).Count != 0;
                if (hasRequiredItemEquipped)
                    return template.GetExtraMaterials().GetMaterial();
            }
            else if (template.GetCheckType() == 2)
            {
                if (player.GetInventory().GetItemCountByItemId(template.GetRequiredItemId()) < template.GetEraseValue())
                {
                    string requiredItemL10n = Aion.GameServer.Utils.ChatUtil.L10n(template.GetRequiredItemNameId());
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_CANT_GATHERING_B_ITEM_CHECK(requiredItemL10n));
                    return null;
                }
                return template.GetExtraMaterials().GetMaterial();
            }
        }
        return template.GetMaterials().GetMaterial();
    }

    public void CompleteInteraction()
    {
        lock (this)
        {
            gatheringTask = null;
            if (++gatherCount == GetOwner().GetObjectTemplate().GetHarvestCount())
            {
                if (GetOwner().IsInInstance())
                    GetOwner().GetController().Delete();
                else
                    GetOwner().GetController().DeleteAndScheduleRespawn();
            }
        }
    }

    public void RewardPlayer(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        if (player != null)
        {
            int skillLvl = GetOwner().GetObjectTemplate().GetSkillLevel();
            int xpReward = (int)((0.0031 * (skillLvl + 5.3) * (skillLvl + 1592.8) + 60));

            int skillId = GetOwner().GetObjectTemplate().GetHarvestSkill();
            int gainedGatherXp = Aion.GameServer.Model.GameObjects.Players.Rates.SKILL_XP_GATHERING.CalcResult(player, xpReward);
            StatEnum? boostStat = StatEnum.GetModifier(skillId);
            if (boostStat != null)
                gainedGatherXp = (int)(gainedGatherXp * (player.GetGameStats().GetStat(boostStat.Value, 100).GetCurrent() / 100f));
            gainedGatherXp = Math.Max(1, gainedGatherXp);

            if (player.GetSkillList().AddSkillXp(player, skillId, gainedGatherXp, skillLvl))
            {
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_EXTRACT_GATHERING_SUCCESS_GETEXP());
                player.GetCommonData().AddExp(xpReward, Aion.GameServer.Model.GameObjects.Players.Rates.XP_GATHERING);
            }
            else
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage
                    .STR_MSG_DONT_GET_PRODUCTION_EXP(DataManager.SKILL_DATA.GetSkillTemplate(skillId).GetL10n()));
        }
    }

    public override void OnDespawn()
    {
        CancelGathering();
        base.OnDespawn();
    }

    public void CancelGathering()
    {
        lock (this)
        {
            if (gatheringTask == null)
                return;
            gatheringTask.Abort();
            gatheringTask = null;
        }
    }

    public int GetGatheringPlayerId()
    {
        lock (this)
        {
            return gatheringTask == null ? 0 : gatheringTask.GetGathererId();
        }
    }
}
