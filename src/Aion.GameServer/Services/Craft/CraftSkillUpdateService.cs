using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Craft;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.Craft;

/// <summary>Java parity: services/craft/CraftSkillUpdateService (MrPoke, sphinx, Imaginary, Pad). Singleton; professionByNpc HashMap→Dictionary (npcId→Profession); learnSkill (level/profession/cost gating, anonymous RequestResponseHandler→nested LearnSkillResponseHandler capturing price/skillId/skillLevel), expert/master craft-skill counters (Profession.values()→Values()), can-learn-more checks. map.put→indexer/get→GetValueOrDefault; Integer price→int?; ItemUpdateType alias. Profession/PlayerSkillList/RequestResponseHandler red-tolerated.</summary>
public class CraftSkillUpdateService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CraftSkillUpdateService));

    private static readonly Dictionary<int, Profession> professionByNpc = new Dictionary<int, Profession>();

    public static CraftSkillUpdateService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private CraftSkillUpdateService()
    {
        // Asmodian
        professionByNpc[204096] = Profession.ESSENCETAPPING;
        professionByNpc[830150] = Profession.ESSENCETAPPING;
        professionByNpc[204257] = Profession.AETHERTAPPING;
        professionByNpc[830148] = Profession.AETHERTAPPING;

        professionByNpc[204100] = Profession.COOKING;
        professionByNpc[830142] = Profession.COOKING;
        professionByNpc[204104] = Profession.WEAPONSMITHING;
        professionByNpc[830146] = Profession.WEAPONSMITHING;
        professionByNpc[204106] = Profession.ARMORSMITHING;
        professionByNpc[830144] = Profession.ARMORSMITHING;
        professionByNpc[204110] = Profession.TAILORING;
        professionByNpc[830136] = Profession.TAILORING;
        professionByNpc[204102] = Profession.ALCHEMY;
        professionByNpc[830138] = Profession.ALCHEMY;
        professionByNpc[204108] = Profession.HANDICRAFTING;
        professionByNpc[830140] = Profession.HANDICRAFTING;
        professionByNpc[798452] = Profession.CONSTRUCTION;
        professionByNpc[798456] = Profession.CONSTRUCTION;

        // Elyos
        professionByNpc[203780] = Profession.ESSENCETAPPING;
        professionByNpc[830066] = Profession.ESSENCETAPPING;
        professionByNpc[203782] = Profession.AETHERTAPPING;
        professionByNpc[830064] = Profession.AETHERTAPPING;

        professionByNpc[203784] = Profession.COOKING;
        professionByNpc[830058] = Profession.COOKING;
        professionByNpc[203788] = Profession.WEAPONSMITHING;
        professionByNpc[830062] = Profession.WEAPONSMITHING;
        professionByNpc[203790] = Profession.ARMORSMITHING;
        professionByNpc[830060] = Profession.ARMORSMITHING;
        professionByNpc[203793] = Profession.TAILORING;
        professionByNpc[830052] = Profession.TAILORING;
        professionByNpc[203786] = Profession.ALCHEMY;
        professionByNpc[830054] = Profession.ALCHEMY;
        professionByNpc[203792] = Profession.HANDICRAFTING;
        professionByNpc[830056] = Profession.HANDICRAFTING;
        professionByNpc[798450] = Profession.CONSTRUCTION;
        professionByNpc[798454] = Profession.CONSTRUCTION;

        log.LogInformation("CraftSkillUpdateService: Initialized.");
    }

    public Profession GetProfessionByNpc(Npc npc)
    {
        return professionByNpc.GetValueOrDefault(npc.GetNpcId());
    }

    public void LearnSkill(Player player, Npc npc)
    {
        if (player.GetLevel() < 10)
            return;
        Profession profession = professionByNpc.GetValueOrDefault(npc.GetNpcId());
        if (profession == null)
            return;
        int skillId = profession.GetSkillId();
        if (skillId == 0)
            return;

        PlayerSkillList skillList = player.GetSkillList();
        int skillLevel = skillList.IsSkillPresent(skillId) ? skillList.GetSkillLevel(skillId) : 0;
        int? price = profession.GetUpgradeCost(skillLevel);
        if (price == null)
        {
            if (skillLevel > profession.GetMaxUpgradableLevel())
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DONT_RANK_UP_GATHERING());
            else if (skillLevel == 399) // expert is granted by quest
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFT_CANT_EXTEND_MONEY());
            else if (skillLevel == 499) // master is granted by quest
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CRAFT_CANT_EXTEND_GRAND_MASTER());
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_DONT_RANK_UP());
            return;
        }

        RequestResponseHandler<Npc> responseHandler = new LearnSkillResponseHandler(npc, price.Value, skillId, skillLevel);

        if (player.GetResponseRequester().PutRequest(SM_QUESTION_WINDOW.STR_CRAFT_ADDSKILL_CONFIRM, responseHandler))
        {
            string professionName = skillLevel == 0 ? profession.GetClientName() : profession.GetClientName(skillLevel + 1);
            PacketSendUtility.SendPacket(player,
                new SM_QUESTION_WINDOW(SM_QUESTION_WINDOW.STR_CRAFT_ADDSKILL_CONFIRM, 0, 0, professionName, price.Value.ToString()));
        }
    }

    private sealed class LearnSkillResponseHandler : RequestResponseHandler<Npc>
    {
        private readonly int price;
        private readonly int skillId;
        private readonly int skillLevel;

        public LearnSkillResponseHandler(Npc npc, int price, int skillId, int skillLevel)
            : base(npc)
        {
            this.price = price;
            this.skillId = skillId;
            this.skillLevel = skillLevel;
        }

        public override void AcceptRequest(Npc requester, Player responder)
        {
            if (responder.GetInventory().TryDecreaseKinah(price, ItemUpdateType.DEC_KINAH_LEARN))
            {
                PlayerSkillList skillList = responder.GetSkillList();
                skillList.AddSkill(responder, skillId, skillLevel + 1);
            }
            else
            {
                PacketSendUtility.SendPacket(responder, SM_SYSTEM_MESSAGE.STR_NOT_ENOUGH_MONEY());
            }
        }
    }

    public int GetTotalExpertCraftingSkills(Player player)
    {
        int mastered = 0;

        foreach (Profession profession in System.Enum.GetValues<Profession>())
        {
            if (profession.IsCrafting() && player.GetSkillList().IsSkillPresent(profession.GetSkillId()))
            {
                int skillLvl = player.GetSkillList().GetSkillLevel(profession.GetSkillId());
                if (skillLvl > 399 && skillLvl <= 499)
                    mastered++;
            }
        }
        return mastered;
    }

    /// <summary>total number of mastered crafting skills</summary>
    public int GetTotalMasterCraftingSkills(Player player)
    {
        int mastered = 0;

        foreach (Profession profession in System.Enum.GetValues<Profession>())
        {
            if (profession.IsCrafting() && player.GetSkillList().IsSkillPresent(profession.GetSkillId()))
            {
                int skillLvl = player.GetSkillList().GetSkillLevel(profession.GetSkillId());
                if (skillLvl > 499)
                    mastered++;
            }
        }

        return mastered;
    }

    public bool CanLearnMoreExpertCraftingSkill(Player player)
    {
        if (GetTotalExpertCraftingSkills(player) + GetTotalMasterCraftingSkills(player) < CraftConfig.MAX_EXPERT_CRAFTING_SKILLS)
        {
            return true;
        }
        else
        {
            PacketSendUtility.SendMessage(player, "You can only be an expert in " + CraftConfig.MAX_EXPERT_CRAFTING_SKILLS + " professions.");
            return false;
        }
    }

    public bool CanLearnMoreMasterCraftingSkill(Player player)
    {
        if (GetTotalMasterCraftingSkills(player) < CraftConfig.MAX_MASTER_CRAFTING_SKILLS)
        {
            return true;
        }
        else
        {
            PacketSendUtility.SendMessage(player, "You can only be a master in " + CraftConfig.MAX_MASTER_CRAFTING_SKILLS + " professions.");
            return false;
        }
    }

    private static class SingletonHolder
    {
        internal static readonly CraftSkillUpdateService instance = new CraftSkillUpdateService();
    }
}
