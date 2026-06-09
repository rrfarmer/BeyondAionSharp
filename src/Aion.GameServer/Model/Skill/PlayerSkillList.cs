using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Skill;

/// <summary>Java parity: model/skill/PlayerSkillList implements SkillList&lt;Player&gt;.</summary>
public sealed class PlayerSkillList : SkillList<Aion.GameServer.Model.GameObjects.Player.Player>
{
    private readonly ConcurrentDictionary<int, PlayerSkillEntry> skills = new ConcurrentDictionary<int, PlayerSkillEntry>();
    private readonly List<PlayerSkillEntry> deletedSkills = new List<PlayerSkillEntry>();

    public PlayerSkillList()
    {
    }

    public PlayerSkillList(List<PlayerSkillEntry> playerSkills)
    {
        foreach (PlayerSkillEntry entry in playerSkills)
            skills[entry.GetSkillId()] = entry;
    }

    public List<PlayerSkillEntry> GetAllSkills()
    {
        return new List<PlayerSkillEntry>(skills.Values);
    }

    public List<PlayerSkillEntry> GetDeletedSkills()
    {
        lock (deletedSkills)
        {
            return new List<PlayerSkillEntry>(deletedSkills);
        }
    }

    public PlayerSkillEntry GetSkillEntry(int skillId)
    {
        return skills.TryGetValue(skillId, out PlayerSkillEntry entry) ? entry : null;
    }

    public bool AddSkill(Aion.GameServer.Model.GameObjects.Player.Player player, int skillId, int skillLevel)
    {
        return AddSkill(player, skillId, skillLevel, false);
    }

    public bool AddTemporarySkill(Aion.GameServer.Model.GameObjects.Player.Player player, int skillId, int skillLevel)
    {
        return AddSkill(player, skillId, skillLevel, true);
    }

    private bool AddSkill(Aion.GameServer.Model.GameObjects.Player.Player player, int skillId, int skillLevel, bool isTemporary)
    {
        lock (this)
        {
            PlayerSkillEntry existingSkill = GetSkillEntry(skillId);
            bool isNew = true;
            if (existingSkill != null)
            {
                if (skillLevel <= existingSkill.GetSkillLevel())
                    return false;
                existingSkill.SetSkillLvl(skillLevel);
                isNew = false;
            }
            else
            {
                skills[skillId] = new PlayerSkillEntry(player, skillId, skillLevel, isTemporary ? IPersistable.PersistentState.NOACTION : IPersistable.PersistentState.NEW);
                List<Aion.GameServer.SkillEngine.Model.SkillLearnTemplate> learnTemplates = Aion.GameServer.Dataholders.DataManager.SKILL_TREE_DATA.GetSkillsForSkill(skillId, player.GetPlayerClass(), player.GetRace(),
                    player.GetLevel());
                foreach (Aion.GameServer.SkillEngine.Model.SkillLearnTemplate learnTemplate in learnTemplates)
                {
                    if (learnTemplate.GetLearnSkill() != null && GetSkillEntry(learnTemplate.GetLearnSkill().Value) != null)
                    {
                        isNew = false;
                        break;
                    }
                }
            }
            Aion.GameServer.Services.SkillLearnService.OnLearnSkill(player, skillId, skillLevel, isNew);
            return true;
        }
    }

    /// <summary>Only for usage with gathering and crafting skills.</summary>
    public bool AddSkillXp(Aion.GameServer.Model.GameObjects.Player.Player player, int skillId, int xpReward, int objSkillLvl)
    {
        lock (this)
        {
            PlayerSkillEntry skill = GetSkillEntry(skillId);
            int skillLvl = skill.GetSkillLevel();
            if (skillLvl - objSkillLvl > 40)
                return false;

            switch (skillId)
            {
                case 30001:
                    if (skillLvl == 49)
                        return false; // human gathering is capped at 49 points
                    goto case 30002;
                case 30002:
                case 30003:
                    if (skillLvl == 449 || skillLvl >= 499 && Aion.GameServer.Configs.Main.CraftConfig.DISABLE_AETHER_AND_ESSENCE_TAPPING_CAP)
                        break; // break here to enable gather exp on master max lvl
                    goto case 40001;
                case 40001:
                case 40002:
                case 40003:
                case 40004:
                case 40007:
                case 40008:
                case 40010:
                    switch (skillLvl)
                    {
                        case 99:
                        case 199:
                        case 299:
                        case 399:
                        case 449:
                        case 499:
                        case 549:
                            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_CRAFT_INFO_MAXPOINT_UP());
                            return false; // disable exp gain to force mastering upgrade via npc
                    }
                    break;
            }

            int requiredExp = (int)(0.23 * (skillLvl + 17.2) * (skillLvl + 17.2));
            if (skill.GetCurrentXp() + xpReward >= requiredExp)
            {
                skillLvl++;
                skill.SetCurrentXp(0);
                skill.SetSkillLvl(skillLvl);
                Aion.GameServer.Services.SkillLearnService.OnLearnSkill(player, skillId, skillLvl, false);
            }
            else
                skill.SetCurrentXp(skill.GetCurrentXp() + xpReward);
            return true;
        }
    }

    public bool IsSkillPresent(int skillId)
    {
        return skills.ContainsKey(skillId);
    }

    public int GetSkillLevel(int skillId)
    {
        return skills[skillId].GetSkillLevel();
    }

    public bool RemoveSkill(int skillId)
    {
        lock (this)
        {
            if (!skills.TryRemove(skillId, out PlayerSkillEntry entry))
                return false;
            entry.SetPersistentState(IPersistable.PersistentState.DELETED);
            lock (deletedSkills)
            {
                deletedSkills.Add(entry);
            }
            return true;
        }
    }

    public int Size()
    {
        return skills.Count;
    }
}
