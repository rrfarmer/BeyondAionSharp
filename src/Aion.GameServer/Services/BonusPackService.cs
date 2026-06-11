using System.Collections.Generic;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Mail;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/BonusPackService (Estrayl, Neon).</summary>
public class BonusPackService
{
    private static readonly BonusPackService INSTANCE = new BonusPackService();
    private readonly Dictionary<int, int> rewards = new Dictionary<int, int>();

    public static BonusPackService GetInstance()
    {
        return INSTANCE;
    }

    private BonusPackService()
    {
                            // itemId, count

        rewards[186000242] = 15; // Ceramium Medal
        rewards[186000130] = 6500; // Crucible Insignia
        rewards[186000051] = 5; // Major Ancient Crown
        rewards[166020003] = 15; // [Event] Omega Enchantment Stone

        rewards[186000236] = 250; // Blood Mark
        rewards[186000237] = 4500; // Ancient Coin
        rewards[186000409] = 150; // Daeva's Respite Coin

        rewards[188052562] = 5; // Scroll Bundle
        rewards[190100051] = 1; // Flying Pagati
    }

    public void AddPlayerCustomReward(Player player)
    {
        if (rewards == null || rewards.Count == 0)
            return;

        if (player.GetLevel() != 65)
            return;

        if (player.GetCommonData().GetMailboxLetters() + rewards.Count > 100)
            return;

        int accountId = player.GetAccount().GetId();
        if (BonusPackDAO.LoadReceivingPlayer(accountId) > 0)
            return;

        if (!BonusPackDAO.StoreReceivingPlayer(accountId, player.GetObjectId()))
            return;

        foreach (KeyValuePair<int, int> e in rewards)
        {
            SystemMailService.SendMail("Beyond Aion", player.GetName(), "Bonus Pack",
                "Greetings Daeva!\n\n" + "You have reached level 65 with your first character and therefore we have a special something for you."
                    + " In gratitude for your support we have prepared a package with valuable items for you.\n\n"
                    + "Enjoy your stay on Beyond Aion!", e.Key, e.Value, 0, LetterType.EXPRESS);
        }
    }
}
