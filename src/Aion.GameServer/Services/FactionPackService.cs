using System;
using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Utils.Time;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/FactionPackService (Estrayl, Neon).</summary>
public class FactionPackService
{
    private static readonly FactionPackService INSTANCE = new FactionPackService();
    private readonly DateTime elyosMinCreationTime = new DateTime(2020, 9, 14, 0, 0, 0);
    private readonly DateTime elyosMaxCreationTime = new DateTime(2020, 9, 26, 23, 59, 59);
    private readonly DateTime asmodianMinCreationTime = new DateTime(2022, 6, 18, 0, 0, 0);
    private readonly DateTime asmodianMaxCreationTime = new DateTime(2022, 7, 19, 23, 59, 59);
    private readonly List<RewardItem> rewards = new List<RewardItem>();

    public static FactionPackService GetInstance()
    {
        return INSTANCE;
    }

    private FactionPackService()
    {
        rewards.Add(new RewardItem(186000236, 500)); // Blood Mark
        rewards.Add(new RewardItem(162002030, 250)); // [Event] Premium Restoration Serum
        rewards.Add(new RewardItem(162000023, 100)); // Greater Healing Potion
        rewards.Add(new RewardItem(166000195, 50)); // Epsilon Enchantment Stone
        rewards.Add(new RewardItem(169630007, 1)); // [Expand Card] Expand Cube Ticket (lvl 4)
        rewards.Add(new RewardItem(188053526, 5)); // [Event] Aion's Steel Form Candy Box
    }

    public void AddPlayerCustomReward(Player player)
    {
        if (rewards.Count == 0 || player.GetLevel() != 65 || (player.GetCommonData().GetMailboxLetters() + rewards.Count > 100))
            return;
        if (player.GetRace() == Race.ASMODIANS)
            SendRewards(player, asmodianMinCreationTime, asmodianMaxCreationTime);
        else
            SendRewards(player, elyosMinCreationTime, elyosMaxCreationTime);
    }

    private void SendRewards(Player player, DateTime minCreationTime, DateTime maxCreationTime)
    {
        DateTime creationTime = ServerTime.OfEpochMilli(player.GetAccount().GetCreationDate()).DateTime;
        if (creationTime < minCreationTime)
            return;
        if (creationTime > maxCreationTime)
            return;
        int accountId = player.GetAccount().GetId();
        if (FactionPackDAO.LoadReceivingPlayer(accountId) > 0)
            return;
        if (!FactionPackDAO.StoreReceivingPlayer(accountId, player.GetObjectId()))
            return;
        foreach (RewardItem e in rewards)
        {
            ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(e.GetId());
            if (template != null && template.GetRace() == player.GetOppositeRace())
                continue;
            SystemMailService.SendMail(
                "Beyond Aion", player.GetName(), "Faction Pack", "Greetings Daeva!\n\n"
                    + "In gratitude for your decision to join this faction we prepared an additional item pack.\n\n" + "Enjoy your stay on Beyond Aion!",
                e.GetId(), e.GetCount(), 0, LetterType.EXPRESS);
        }
    }
}
