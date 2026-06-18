using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Easter (Neon, Estrayl, Farlon). Exchanges event eggs for prizes.</summary>
public class Easter : PlayerCommand
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly int neededItem = 186000175;
    private static readonly int[][] rewards =
    {
        new[] { 25, 0 }, new[] { 50, 186000147 }, new[] { 50, 186000055 }, new[] { 75, 166020000 }, new[] { 75, 188053609 }, new[] { 75, 166200013 },
        new[] { 75, 188053113 }, new[] { 100, 188053295 }, new[] { 100, 166030005 }, new[] { 300, 188053702 },
    };

    private static readonly int[][] randomItems =
    {
        new[] { 10, 162002030 }, // [Event] Premium Restoration Serum
        new[] { 50, 186000237 }, // Ancient Coin
        new[] { 3, 162000137 }, // Sublime Life Serum
        new[] { 3, 162000139 }, // Sublime Mana Serum
        new[] { 5, 186000146 }, // Guestpetal
        new[] { 1, 188054198 }, // Greater Scroll Bundle
        new[] { 10, 164000126 }, // Major Strike Resist Scroll
        new[] { 10, 164000130 }, // Major Spell Resist Scroll
    };

    public Easter()
        : base("easter", "Exchanges " + ChatUtil.Item(186000175) + " for prizes.")
    {
        SetSyntaxInfo("Type in .easter <id> to get your reward:",
            "[1] - (" + rewards[0][0] + " eggs) Random item",
            "[2] - (" + rewards[1][0] + " eggs) " + " 2x " + ChatUtil.Item(rewards[1][1]),
            "[3] - (" + rewards[2][0] + " eggs) " + " 3x " + ChatUtil.Item(rewards[2][1]),
            "[4] - (" + rewards[3][0] + " eggs) " + " 5x " + ChatUtil.Item(rewards[3][1]),
            "[5] - (" + rewards[4][0] + " eggs) " + " 3x " + ChatUtil.Item(rewards[4][1]),
            "[6] - (" + rewards[5][0] + " eggs) " + ChatUtil.Item(rewards[5][1]),
            "[7] - (" + rewards[6][0] + " eggs) " + " 3x " + ChatUtil.Item(rewards[6][1]),
            "[8] - (" + rewards[7][0] + " eggs) " + ChatUtil.Item(rewards[7][1]),
            "[9] - (" + rewards[8][0] + " eggs) " + " 5x " + ChatUtil.Item(rewards[8][1]),
            "[10] - (" + rewards[9][0] + " eggs) " + ChatUtil.Item(rewards[9][1]));
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        // Java parity: try { rewardIndex = parseInt - 1; ... } catch (IllegalArgumentException e) { sendInfo(player, e instanceof NumberFormatException ? "Invalid prize." : e.getMessage()); }
        // Reproduced as explicit branches with identical outcomes (non-numeric -> "Invalid prize."; out-of-range -> syntax info; insufficient -> "You need ..." message).
        if (!int.TryParse(paramsArr[0], out int parsed))
        {
            SendInfo(player, "Invalid prize.");
            return;
        }

        int rewardIndex = parsed - 1;
        if (rewardIndex < 0 || rewardIndex >= rewards.Length)
        {
            SendInfo(player);
            return;
        }

        int cost = rewards[rewardIndex][0];
        if (player.GetInventory().GetItemCountByItemId(neededItem) < cost || !player.GetInventory().DecreaseByItemId(neededItem, cost))
        {
            SendInfo(player, "You need " + cost + " " + ChatUtil.Item(neededItem) + " for this.");
            return;
        }

        int count = 1;
        int itemId = rewards[rewardIndex][1];
        if (itemId == 0)
        {
            int rndIndex = Rnd.NextInt(randomItems.Length);
            count = randomItems[rndIndex][0];
            itemId = randomItems[rndIndex][1];
        }
        switch (itemId)
        {
            case 186000147: // Mithril Medal
                count = 2;
                break;
            case 186000055: // Major Ancient Goblet
            case 188053113: // Ahserion's Flight Ancient Manastone Bundle
            case 188053609: // [Event] Level 60 Composite Manastone Bundle
                count = 3;
                break;
            case 166020000: // Omega Enchantment Stone
            case 166030005: // Tempering Solution
                count = 5;
                break;
        }

        long notAddedCount = ItemService.AddItem(player, itemId, count, true,
            new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
        if (notAddedCount > 0)
        {
            log.LogWarning("[Easter Event] " + notAddedCount + "/" + count + " of " + itemId + " could not be added.");
        }
    }
}
