using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using ActionType = Aion.GameServer.Network.Aion.Serverpackets.SM_QUEST_ACTION.ActionType;

namespace Aion.GameServer.Services.Reward;

/// <summary>Java parity: services/reward/WebRewardService (KID, Neon). "WEB_REWARDS_LOG" logger; singleton; sendAvailableRewards loops unreceived rewards (sendRewardItem mail or executeRewardAction), stores received; nested MaxLevelReward (CopyOnWriteArraySet→ConcurrentDictionary-as-set pendingAscension, ascension-quest flow, daeva max-level gear via two PlayerClass switch blocks). add→TryAdd, remove→TryRemove, contains→ContainsKey; instanceof Npc npc→is; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; ChatUtil.l10n→L10n. RewardServiceDAO/QuestState/TeleportService red-tolerated.</summary>
public class WebRewardService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("WEB_REWARDS_LOG");
    private static WebRewardService instance = new WebRewardService();

    public static WebRewardService GetInstance()
    {
        return instance;
    }

    private WebRewardService()
    {
    }

    public void SendAvailableRewards(Player player)
    {
        if (player == null)
            return;
        List<RewardEntryItem> list = RewardServiceDAO.LoadUnreceived(player.GetObjectId());
        if (list.Count == 0)
            return;

        List<int> rewarded = new List<int>();
        foreach (RewardEntryItem item in list)
        {
            try
            {
                if (SendRewardItem(player, item) || ExecuteRewardAction(player, item))
                {
                    log.LogInformation("[WebRewardService][" + item.GetEntryId() + "] " + player + " has received " + item);
                    rewarded.Add(item.GetEntryId());
                }
                else
                {
                    log.LogWarning("[WebRewardService][" + item.GetEntryId() + "] " + player + " could not receive " + item);
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "[WebRewardService][" + item.GetEntryId() + "] error adding " + item + " to " + player);
            }
        }

        if (rewarded.Count > 0)
            RewardServiceDAO.StoreReceived(rewarded, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private bool SendRewardItem(Player player, RewardEntryItem item)
    {
        if (DataManager.ITEM_DATA.GetItemTemplate(item.GetId()) == null)
            return false;

        int itemId = 0;
        long kinahCount = 0, itemCount = 0;
        if (item.GetId() == ItemId.KINAH)
        {
            kinahCount = item.GetCount();
        }
        else
        {
            itemId = item.GetId();
            itemCount = item.GetCount();
        }

        return SystemMailService.SendMail("$$CASH_ITEM_MAIL", player.GetName(), item.GetId() + ", " + item.GetCount(),
            "0, " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + ",", itemId, itemCount, kinahCount, LetterType.BLACKCLOUD);
    }

    private bool ExecuteRewardAction(Player player, RewardEntryItem rewardItem)
    {
        switch (rewardItem.GetId())
        {
            case 1:
                return MaxLevelReward.Reward(player);
            default:
                return false;
        }
    }

    public static class MaxLevelReward
    {
        private static ConcurrentDictionary<int, byte> pendingAscension = new ConcurrentDictionary<int, byte>();

        public static bool IsPendingAscension(Player player)
        {
            return pendingAscension.ContainsKey(player.GetObjectId());
        }

        public static bool Reward(Player player)
        {
            if (!player.GetCommonData().IsDaeva())
            {
                if (!pendingAscension.TryAdd(player.GetObjectId(), 0))
                    return false;
                if (player.GetLevel() < 9)
                    player.GetCommonData().SetLevel(9); // reaching level 9 starts the ascension quest
                int questNpcId = player.GetRace() == Race.ELYOS ? 790001 : 203550; // Pernos / Munin
                int questId = player.GetRace() == Race.ELYOS ? 1006 : 2008;
                int questVar = player.GetRace() == Race.ELYOS ? 5 : 6;
                QuestState qs = player.GetQuestStateList().GetQuestState(questId);
                if (qs.GetStatus() != QuestStatus.REWARD)
                { // class selection is complete at this point
                    if (qs.GetStatus() == QuestStatus.COMPLETE)
                    { // if player switched back to a starting class (GM command or DB change)
                        qs.SetStatus(QuestStatus.START);
                        PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.ADD, qs));
                    }
                    if (qs.GetQuestVars().GetQuestVars() != questVar)
                        qs.SetQuestVar(questVar);
                    PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(ActionType.UPDATE, qs));
                }
                VisibleObject questNpc = player.GetKnownList().FindObject(o => o.Get() is Npc npc && npc.GetNpcId() == questNpcId);
                if (questNpc == null || PositionUtil.GetDistance(player, questNpc) >= 20)
                    TeleportService.SendTeleportRequest(player, questNpcId); // completing the quest (updates daeva status) calls the reward method again
            }
            else
            {
                int maxLevel = DataManager.PLAYER_EXPERIENCE_TABLE.GetMaxLevel() - 1; // max is 66
                if (player.GetLevel() >= maxLevel)
                    return false;
                pendingAscension.TryRemove(player.GetObjectId(), out _);
                AddBasicGear(player);
                player.GetCommonData().SetLevel(maxLevel);
                string message = ChatUtil.L10n(904804) + " Level " + player.GetLevel(); // You receive the following reward: Level 65
                PacketSendUtility.SendMessage(player, message, ChatType.BRIGHT_YELLOW);
                TeleportService.SendTeleportRequest(player, player.GetRace() == Race.ELYOS ? 798926 : 799225); // Outremus / Richelle for daevanion quests
            }
            return true;
        }

        private static void AddBasicGear(Player player)
        {
            ItemService.AddItem(player, 188053624, 10, true); // Unified Return Scroll Bundle
            switch (player.GetPlayerClass())
            { // weapons
                case PlayerClass.GLADIATOR:
                    ItemService.AddItem(player, 101300728, 1, true); // Transient Lance (14 days)
                    break;
                case PlayerClass.TEMPLAR:
                    ItemService.AddItem(player, 100900749, 1, true); // Transient Greatsword (14 days)
                    break;
                case PlayerClass.ASSASSIN:
                    ItemService.AddItem(player, 100000993, 1, true); // Transient Brand (14 days)
                    ItemService.AddItem(player, 100200882, 1, true); // Transient Dirk (14 days)
                    break;
                case PlayerClass.RANGER:
                    ItemService.AddItem(player, 101700795, 1, true); // Transient Bow (14 days)
                    break;
                case PlayerClass.SORCERER:
                    ItemService.AddItem(player, 100600830, 1, true); // Transient Tome (14 days)
                    break;
                case PlayerClass.SPIRIT_MASTER:
                    ItemService.AddItem(player, 100500775, 1, true); // Transient Sphere (14 days)
                    break;
                case PlayerClass.CLERIC:
                    ItemService.AddItem(player, 100100755, 1, true); // Transient Warhammer (14 days)
                    ItemService.AddItem(player, 115001049, 1, true); // Transient Shield (14 days)
                    break;
                case PlayerClass.CHANTER:
                    ItemService.AddItem(player, 101500778, 1, true); // Transient Staff (14 days)
                    break;
                case PlayerClass.RIDER:
                    ItemService.AddItem(player, 102101083, 1, true); // Atreian Faithful Cipher-Blade
                    break;
                case PlayerClass.GUNNER:
                    ItemService.AddItem(player, 101800899, 2, true); // Transient Pistol (14 days)
                    break;
                case PlayerClass.BARD:
                    ItemService.AddItem(player, 102000923, 1, true); // Transient Harp (14 days)
                    break;
            }
            switch (player.GetPlayerClass())
            { // armor
                case PlayerClass.GLADIATOR:
                case PlayerClass.TEMPLAR:
                    ItemService.AddItem(player, 110601053, 1, true); // Transient Breastplate (14 days)
                    ItemService.AddItem(player, 111601030, 1, true); // Transient Gauntlets (14 days)
                    ItemService.AddItem(player, 112601003, 1, true); // Transient Shoulderplates (14 days)
                    ItemService.AddItem(player, 113601014, 1, true); // Transient Greaves (14 days)
                    ItemService.AddItem(player, 114601010, 1, true); // Transient Sabatons (14 days)
                    break;
                case PlayerClass.ASSASSIN:
                case PlayerClass.RANGER:
                case PlayerClass.GUNNER:
                    ItemService.AddItem(player, 110301102, 1, true); // Transient Jerkin (14 days)
                    ItemService.AddItem(player, 111301057, 1, true); // Transient Vambrace (14 days)
                    ItemService.AddItem(player, 112301002, 1, true); // Transient Shoulderguards (14 days)
                    ItemService.AddItem(player, 113301074, 1, true); // Transient Breeches (14 days)
                    ItemService.AddItem(player, 114301109, 1, true); // Transient Boots (14 days)
                    break;
                case PlayerClass.SORCERER:
                case PlayerClass.SPIRIT_MASTER:
                case PlayerClass.BARD:
                    ItemService.AddItem(player, 110101165, 1, true); // Transient Tunic (14 days)
                    ItemService.AddItem(player, 111101056, 1, true); // Transient Gloves (14 days)
                    ItemService.AddItem(player, 112101014, 1, true); // Transient Pauldrons (14 days)
                    ItemService.AddItem(player, 113101069, 1, true); // Transient Leggings (14 days)
                    ItemService.AddItem(player, 114101097, 1, true); // Transient Shoes (14 days)
                    break;
                case PlayerClass.CLERIC:
                case PlayerClass.CHANTER:
                case PlayerClass.RIDER:
                    ItemService.AddItem(player, 110501071, 1, true); // Transient Hauberk (14 days)
                    ItemService.AddItem(player, 111501041, 1, true); // Transient Handguards (14 days)
                    ItemService.AddItem(player, 112500990, 1, true); // Transient Spaulders (14 days)
                    ItemService.AddItem(player, 113501049, 1, true); // Transient Chausses (14 days)
                    ItemService.AddItem(player, 114501057, 1, true); // Transient Brogans (14 days)
                    break;
            }
        }
    }
}
