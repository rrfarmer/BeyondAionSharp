using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Questengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Templates.Quest;

/// <summary>
/// Checks quest start conditions, listed in quest_data.xml. Java parity: model/templates/quest/XMLStartCondition (antness, vlog).
/// @XmlList element Integer lists → Raw space-sep string properties.
/// </summary>
[XmlType("QuestStartConditions")]
public class XMLStartCondition
{
    [XmlElement("finished")] protected List<FinishedQuestCond> finished;

    protected List<int> unfinished;
    protected List<int> noacquired;
    protected List<int> acquired;
    protected List<int> equipped;

    [XmlElement("unfinished")]
    public string UnfinishedRaw
    {
        get => unfinished == null ? null : string.Join(" ", unfinished);
        set => unfinished = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlElement("noacquired")]
    public string NoacquiredRaw
    {
        get => noacquired == null ? null : string.Join(" ", noacquired);
        set => noacquired = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlElement("acquired")]
    public string AcquiredRaw
    {
        get => acquired == null ? null : string.Join(" ", acquired);
        set => acquired = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlElement("equipped")]
    public string EquippedRaw
    {
        get => equipped == null ? null : string.Join(" ", equipped);
        set => equipped = value == null ? null : value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    [XmlElement("required_title")] protected int requiredTitle;

    public bool IsOptional()
    {
        return finished != null && finished.Count > 0;
    }

    /// <summary>Check, if the player has finished listed quests.</summary>
    private bool CheckFinishedQuests(QuestStateList qsl)
    {
        if (finished != null && finished.Count > 0)
        {
            foreach (FinishedQuestCond fqc in finished)
            {
                int questId = fqc.GetQuestId();
                int reward = fqc.GetReward();
                QuestState qs = qsl.GetQuestState(questId);
                if (qs == null || qs.GetStatus() != QuestStatus.COMPLETE || (reward >= 0 && (qs.GetRewardGroup() == null || reward != qs.GetRewardGroup().Value)))
                {
                    return false;
                }
                QuestTemplate template = DataManager.QUEST_DATA.GetQuestById(questId);
                if (template != null && template.IsRepeatable())
                {
                    if (template.GetMaxRepeatCount() != 255 && qs.GetCompleteCount() != template.GetMaxRepeatCount())
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>Check, if the player has not finished listed quests.</summary>
    private bool CheckUnfinishedQuests(QuestStateList qsl)
    {
        if (unfinished != null && unfinished.Count > 0)
        {
            foreach (int questId in unfinished)
            {
                QuestState qs = qsl.GetQuestState(questId);
                if (qs != null && qs.GetStatus() == QuestStatus.COMPLETE)
                    return false;
            }
        }
        return true;
    }

    /// <summary>Check, if the player has not acquired listed quests.</summary>
    private bool CheckNoAcquiredQuests(QuestStateList qsl)
    {
        if (noacquired != null && noacquired.Count > 0)
        {
            foreach (int questId in noacquired)
            {
                QuestState qs = qsl.GetQuestState(questId);
                if (qs != null && (qs.GetStatus() == QuestStatus.START || qs.GetStatus() == QuestStatus.REWARD))
                    return false;
            }
        }
        return true;
    }

    /// <summary>Check, if the player has acquired listed quests.</summary>
    private bool CheckAcquiredQuests(QuestStateList qsl)
    {
        if (acquired != null && acquired.Count > 0)
        {
            foreach (int questId in acquired)
            {
                QuestState qs = qsl.GetQuestState(questId);
                if (qs == null || qs.GetStatus() == QuestStatus.LOCKED)
                    return false;
            }
        }
        return true;
    }

    private bool CheckEquippedItems(Player player, bool warn)
    {
        if (!warn)
            return true;
        if (equipped != null && equipped.Count > 0)
        {
            foreach (int itemId in equipped)
            {
                if (!player.GetEquipment().GetEquippedItemIds().Contains(itemId))
                {
                    string requiredItem = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetL10n();
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_QUEST_ACQUIRE_ERROR_EQUIP_ITEM(requiredItem));
                    return false;
                }
            }
        }
        return true;
    }

    private bool IsRequiredTitleDisplayed(Player player)
    {
        if (requiredTitle != 0 && player.GetCommonData().GetTitleId() != requiredTitle)
            return false;
        return true;
    }

    /// <summary>Check all conditions.</summary>
    public bool Check(Player player, bool warn)
    {
        QuestStateList qsl = player.GetQuestStateList();
        return CheckFinishedQuests(qsl) && CheckUnfinishedQuests(qsl) && CheckAcquiredQuests(qsl) && CheckNoAcquiredQuests(qsl)
            && CheckEquippedItems(player, warn) && IsRequiredTitleDisplayed(player);
    }

    public List<FinishedQuestCond> GetFinishedPreconditions()
    {
        return finished;
    }
}
