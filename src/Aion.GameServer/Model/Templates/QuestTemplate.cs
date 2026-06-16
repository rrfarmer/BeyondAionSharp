using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Quest;

namespace Aion.GameServer.Model.Templates;

/// <summary>Java parity: model/templates/QuestTemplate (MrPoke, vlog, Neon). implements L10n. @XmlList/@XmlAttribute enum-lists→Raw; nullable Race/Gender (Java object-enums, null when absent).</summary>
[XmlType("Quest")]
public class QuestTemplate : L10n
{
    [XmlElement("collect_items")] public CollectItems collectItems;
    [XmlElement("inventory_items")] public InventoryItems inventoryItems;
    [XmlElement("rewards")] public List<Aion.GameServer.Model.Templates.Quest.Rewards> rewards;
    [XmlElement("bonus")] public QuestBonuses bonus;
    [XmlElement("extended_rewards")] public Aion.GameServer.Model.Templates.Quest.Rewards extendedRewards;
    [XmlElement("quest_drop")] public List<QuestDrop> questDrop;
    [XmlElement("quest_kill")] public List<QuestKill> questKill;
    [XmlElement("start_conditions")] public List<XMLStartCondition> startConds;

    private List<PlayerClass> classPermitted;

    [XmlElement("class_permitted")]
    public string ClassPermittedRaw
    {
        get => classPermitted == null ? null : string.Join(" ", classPermitted);
        set => classPermitted = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (PlayerClass)Enum.Parse(typeof(PlayerClass), t)).ToList();
    }

    // Java parity: nullable Gender object-enum (absent -> null). XmlSerializer cannot bind Nullable<enum> to an
    // element, so round-trip through a string proxy (1:1 with JAXB).
    private Gender? genderPermitted;

    [XmlElement("gender_permitted")]
    public string GenderPermittedRaw
    {
        get => genderPermitted?.ToString();
        set => genderPermitted = value == null ? null : (Gender)Enum.Parse(typeof(Gender), value);
    }

    [XmlElement("quest_work_items")] public QuestWorkItems questWorkItems;
    [XmlElement("fighter_selectable_reward")] public List<QuestItems> fighterSelectableReward;
    [XmlElement("knight_selectable_reward")] public List<QuestItems> knightSelectableReward;
    [XmlElement("ranger_selectable_reward")] public List<QuestItems> rangerSelectableReward;
    [XmlElement("assassin_selectable_reward")] public List<QuestItems> assassinSelectableReward;
    [XmlElement("wizard_selectable_reward")] public List<QuestItems> wizardSelectableReward;
    [XmlElement("elementalist_selectable_reward")] public List<QuestItems> elementalistSelectableReward;
    [XmlElement("priest_selectable_reward")] public List<QuestItems> priestSelectableReward;
    [XmlElement("chanter_selectable_reward")] public List<QuestItems> chanterSelectableReward;
    [XmlElement("gunner_selectable_reward")] public List<QuestItems> gunnerSelectableReward;
    [XmlElement("rider_selectable_reward")] public List<QuestItems> riderSelectableReward;
    [XmlElement("bard_selectable_reward")] public List<QuestItems> bardSelectableReward;
    [XmlAttribute("id")] public int id;
    [XmlAttribute("name")] public string name;
    [XmlAttribute("nameId")] public int nameId;
    [XmlAttribute("minlevel_permitted")] public int minlevelPermitted;
    [XmlAttribute("maxlevel_permitted")] public int maxlevelPermitted;
    [XmlAttribute("rank")] public int rank;
    [XmlAttribute("max_repeat_count")] public int maxRepeatCount = 1;
    [XmlAttribute("reward_repeat_count")] public int rewardRepeatCount;
    [XmlAttribute("cannot_share")] public bool cannotShare;
    [XmlAttribute("cannot_giveup")] public bool cannotGiveup;
    [XmlAttribute("can_report")] public bool canReport;
    [XmlAttribute("use_class_reward")] public int useClassReward;

    // Java parity: nullable Race object-enum @XmlAttribute (absent -> null). XmlSerializer cannot bind
    // Nullable<enum> to an attribute, so round-trip through a string proxy (1:1 with JAXB).
    private Race? racePermitted;

    [XmlAttribute("race_permitted")]
    public string RacePermittedRaw
    {
        get => racePermitted?.ToString();
        set => racePermitted = value == null ? null : (Race)Enum.Parse(typeof(Race), value);
    }

    [XmlAttribute("combineskill")] public int combineskill;
    [XmlAttribute("combine_skillpoint")] public int combineSkillpoint;
    [XmlAttribute("timer")] public bool timer;
    [XmlAttribute("category")] public QuestCategory category = QuestCategory.QUEST;
    [XmlAttribute("extra_category")] public QuestExtraCategory extraCategory = QuestExtraCategory.NONE;

    private List<QuestRepeatCycle> repeatCycle;

    [XmlAttribute("repeat_cycle")]
    public string RepeatCycleRaw
    {
        get => repeatCycle == null ? null : string.Join(" ", repeatCycle);
        set => repeatCycle = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => (QuestRepeatCycle)Enum.Parse(typeof(QuestRepeatCycle), t)).ToList();
    }

    [XmlAttribute("npcfaction_id")] public int npcFactionId;
    [XmlAttribute("mentor_type")] public QuestMentorType mentorType = QuestMentorType.NONE;
    [XmlAttribute("target")] public QuestTarget target = QuestTarget.NONE;
    [XmlAttribute("restricted")] public bool restricted = false;
    [XmlAttribute("data_driven")] public bool dataDriven = false;

    public CollectItems GetCollectItems()
    {
        return collectItems;
    }

    public InventoryItems GetInventoryItems()
    {
        return inventoryItems;
    }

    public List<Aion.GameServer.Model.Templates.Quest.Rewards> GetRewards()
    {
        return rewards == null ? new List<Aion.GameServer.Model.Templates.Quest.Rewards>() : rewards;
    }

    public Aion.GameServer.Model.Templates.Quest.Rewards GetExtendedRewards()
    {
        return extendedRewards;
    }

    public QuestBonuses GetBonus()
    {
        return bonus;
    }

    public List<QuestDrop> GetQuestDrop()
    {
        return questDrop == null ? new List<QuestDrop>() : questDrop;
    }

    public List<QuestKill> GetQuestKill()
    {
        return questKill == null ? new List<QuestKill>() : questKill;
    }

    public List<XMLStartCondition> GetXMLStartConditions()
    {
        return startConds == null ? new List<XMLStartCondition>() : startConds;
    }

    public int GetRequiredConditionCount()
    {
        if (GetXMLStartConditions().Count == 0)
            return 0;
        int optionalCount = 0;
        int mandatoryCount = 0;
        foreach (XMLStartCondition cond in startConds)
        {
            if (cond.IsOptional())
                optionalCount++;
            else
                mandatoryCount++;
        }

        int result = Math.Min(1, optionalCount) + mandatoryCount;

        if (IsMaster())
            result += 1 - CraftConfig.MAX_MASTER_CRAFTING_SKILLS;

        return result;
    }

    public List<PlayerClass> GetClassPermitted()
    {
        return classPermitted == null ? new List<PlayerClass>() : classPermitted;
    }

    public Gender? GetGenderPermitted()
    {
        return genderPermitted;
    }

    public QuestWorkItems GetQuestWorkItems()
    {
        return questWorkItems;
    }

    public List<QuestItems> GetSelectableRewardByClass(PlayerClass playerClass)
    {
        switch (playerClass)
        {
            case PlayerClass.ASSASSIN:
                return assassinSelectableReward == null ? new List<QuestItems>() : assassinSelectableReward;
            case PlayerClass.CHANTER:
                return chanterSelectableReward == null ? new List<QuestItems>() : chanterSelectableReward;
            case PlayerClass.CLERIC:
                return priestSelectableReward == null ? new List<QuestItems>() : priestSelectableReward;
            case PlayerClass.GLADIATOR:
                return fighterSelectableReward == null ? new List<QuestItems>() : fighterSelectableReward;
            case PlayerClass.RANGER:
                return rangerSelectableReward == null ? new List<QuestItems>() : rangerSelectableReward;
            case PlayerClass.SORCERER:
                return wizardSelectableReward == null ? new List<QuestItems>() : wizardSelectableReward;
            case PlayerClass.SPIRIT_MASTER:
                return elementalistSelectableReward == null ? new List<QuestItems>() : elementalistSelectableReward;
            case PlayerClass.TEMPLAR:
                return knightSelectableReward == null ? new List<QuestItems>() : knightSelectableReward;
            case PlayerClass.GUNNER:
                return gunnerSelectableReward == null ? new List<QuestItems>() : gunnerSelectableReward;
            case PlayerClass.BARD:
                return bardSelectableReward == null ? new List<QuestItems>() : bardSelectableReward;
            case PlayerClass.RIDER:
                return riderSelectableReward == null ? new List<QuestItems>() : riderSelectableReward;
        }
        return new List<QuestItems>();
    }

    public int GetId()
    {
        return id;
    }

    public string GetName()
    {
        return name;
    }

    public int GetL10nId()
    {
        return nameId;
    }

    public int GetMinlevelPermitted()
    {
        return minlevelPermitted;
    }

    public int GetMaxlevelPermitted()
    {
        return maxlevelPermitted;
    }

    public int GetRequiredRank()
    {
        return rank;
    }

    public int GetMaxRepeatCount()
    {
        return maxRepeatCount;
    }

    public int GetRewardRepeatCount()
    {
        return rewardRepeatCount;
    }

    public bool IsCannotShare()
    {
        return cannotShare;
    }

    public bool IsCannotGiveup()
    {
        return cannotGiveup;
    }

    public bool IsCanReport()
    {
        return canReport;
    }

    public bool IsClassRewardOnEveryRepeat()
    {
        return useClassReward == 1;
    }

    public bool IsSingleTimeClassReward()
    {
        return useClassReward == 2;
    }

    public bool IsRepeatable()
    {
        return GetMaxRepeatCount() > 1;
    }

    public Race? GetRacePermitted()
    {
        return racePermitted;
    }

    public int GetCombineSkill()
    {
        return combineskill;
    }

    public int GetCombineSkillPoint()
    {
        return combineSkillpoint;
    }

    public bool IsTimer()
    {
        return timer;
    }

    public QuestCategory GetCategory()
    {
        return category;
    }

    public QuestExtraCategory GetExtraCategory()
    {
        return extraCategory;
    }

    public bool IsMentor()
    {
        return mentorType != QuestMentorType.NONE;
    }

    public QuestMentorType GetMentorType()
    {
        return mentorType;
    }

    public QuestTarget GetTarget()
    {
        return target;
    }

    public List<QuestRepeatCycle> GetRepeatCycle()
    {
        return repeatCycle;
    }

    public int GetNpcFactionId()
    {
        return npcFactionId;
    }

    public bool IsTimeBased()
    {
        return repeatCycle != null;
    }

    public bool IsDaily()
    {
        return IsTimeBased() && repeatCycle.Contains(QuestRepeatCycle.ALL);
    }

    public bool IsWeekly()
    {
        return IsTimeBased() && !IsDaily();
    }

    public bool IsMaster()
    {
        return GetCombineSkillPoint() == 499;
    }

    public bool IsExpert()
    {
        return GetCombineSkillPoint() == 399;
    }

    public bool IsProfession()
    {
        return IsMaster() || IsExpert();
    }

    /// <summary>True, if the quest is a mission quest (campaign quest).</summary>
    public bool IsMission()
    {
        return category == QuestCategory.MISSION;
    }

    public bool IsNoCount()
    {
        return category == QuestCategory.NON_COUNT || category == QuestCategory.EVENT;
    }

    public bool IsRestricted()
    {
        return restricted;
    }

    public bool IsDataDriven()
    {
        return dataDriven;
    }
}
