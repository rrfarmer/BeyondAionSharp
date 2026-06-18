using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Symphony (Pad). Exchanges a required collection item for prizes.</summary>
public class Symphony : PlayerCommand
{
    private static readonly ILogger log = NullLogger.Instance;
    private static readonly int requiredItem = 182007170;
    private static readonly int[][] rewards =
    {
        // COLLECTION_COUNT, REWARD_ID, REWARD_COUNT
        new[] { 3, 186000236, 10 }, // Blood Mark
        new[] { 5, 186000399, 10 }, // Honorable Conqueror's Mark
        new[] { 15, 166000195, 5 }, // Epsilon Enchantment Stone
        new[] { 30, 186000055, 5 }, // Major Ancient Goblet
        new[] { 50, 188053610, 3 }, // [Event] Level 70 Composite Manastone Bundle
        new[] { 60, 188053295, 1 }, // Empyrean Plume Chest
        new[] { 75, 188053903, 1 }, // Honorable Equipment of Conquest Box
        new[] { 80, 166020000, 10 }, // Omega Enchantment Stone
        new[] { 80, 166500002, 10 }, // Amplification Stone
        new[] { 80, 166030005, 10 }, // Tempering Solution
        new[] { 100, 188950015, 2 }, // Special Courier Pass (Eternal/Lv. 61-65)
        new[] { 250, 187000090, 1 }, // Tiamat's Spectral Wings
        new[] { 300, 188054238, 1 }, // Iron Wall Armor Box
    };

    public Symphony()
        : base("symphony", "Exchanges " + ChatUtil.Item(requiredItem) + " for prizes.")
    {
        SetSyntaxInfo("Type .symphony <id> to get your reward:",
            "[1] - (" + rewards[0][0] + " copies): " + rewards[0][2] + "x " + ChatUtil.Item(rewards[0][1]),
            "[2] - (" + rewards[1][0] + " copies): " + rewards[1][2] + "x " + ChatUtil.Item(rewards[1][1]),
            "[3] - (" + rewards[2][0] + " copies): " + rewards[2][2] + "x " + ChatUtil.Item(rewards[2][1]),
            "[4] - (" + rewards[3][0] + " copies): " + rewards[3][2] + "x " + ChatUtil.Item(rewards[3][1]),
            "[5] - (" + rewards[4][0] + " copies): " + rewards[4][2] + "x " + ChatUtil.Item(rewards[4][1]),
            "[6] - (" + rewards[5][0] + " copies): " + rewards[5][2] + "x " + ChatUtil.Item(rewards[5][1]),
            "[7] - (" + rewards[6][0] + " copies): " + rewards[6][2] + "x " + ChatUtil.Item(rewards[6][1]),
            "[8] - (" + rewards[7][0] + " copies): " + rewards[7][2] + "x " + ChatUtil.Item(rewards[7][1]),
            "[9] - (" + rewards[8][0] + " copies): " + rewards[8][2] + "x " + ChatUtil.Item(rewards[8][1]),
            "[10] - (" + rewards[9][0] + " copies): " + rewards[9][2] + "x " + ChatUtil.Item(rewards[9][1]),
            "[11] - (" + rewards[10][0] + " copies): " + rewards[10][2] + "x " + ChatUtil.Item(rewards[10][1]),
            "[12] - (" + rewards[11][0] + " copies): " + rewards[11][2] + "x " + ChatUtil.Item(rewards[11][1]),
            "[13] - (" + rewards[12][0] + " copies): " + rewards[12][2] + "x " + ChatUtil.Item(rewards[12][1]));
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        // Java parity: try { rewardIndex = parseInt - 1; ... } catch (IllegalArgumentException e) { sendInfo(player, e instanceof NumberFormatException ? "Invalid prize." : e.getMessage()); }
        // The Java exception-as-control-flow is reproduced as explicit branches with identical outcomes:
        // - non-numeric -> "Invalid prize."; out-of-range index -> null message -> syntax info; insufficient items -> the "You need ..." message.
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
        if (player.GetInventory().GetItemCountByItemId(requiredItem) < cost || !player.GetInventory().DecreaseByItemId(requiredItem, cost))
        {
            SendInfo(player, "You need " + cost + " " + ChatUtil.Item(requiredItem) + " for this.");
            return;
        }

        int itemId = rewards[rewardIndex][1];
        int itemCount = rewards[rewardIndex][2];

        long notAddedCount = ItemService.AddItem(player, itemId, itemCount, true,
            new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
        if (notAddedCount > 0)
        {
            log.LogWarning("[Legendary Symphony Event] " + notAddedCount + "x " + itemId + " could not be added to " + player.GetName() + "'s inventory.");
        }
    }
}
