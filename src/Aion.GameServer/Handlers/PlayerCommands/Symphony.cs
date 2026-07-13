using System.Text;
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
    private const int REQUIRED_ITEM_ID = 182007170;
    private static readonly int[][] REWARDS =
    {
        // COLLECTION_COUNT, REWARD_ID, REWARD_COUNT
        new[] { 3, 186000236, 10 }, // Blood Mark
        new[] { 5, 186000399, 10 }, // Honorable Conqueror's Mark
        new[] { 15, 166000195, 5 }, // Epsilon Enchantment Stone
        new[] { 40, 188052388, 1 }, // Modor's Equipment Box
        new[] { 50, 188053695, 2 }, // High Grade Crafting Material Box of Conquest
        new[] { 50, 188053610, 3 }, // [Event] Level 70 Composite Manastone Bundle
        new[] { 60, 188053321, 1 }, // [Event] Empyrean Plume Chest
        new[] { 65, 188053903, 1 }, // Honorable Equipment of Conquest Box
        new[] { 70, 166020003, 10 }, // [Event] Omega Enchantment Stone
        new[] { 70, 166500005, 10 }, // [Event] Amplification Stone
        new[] { 70, 166030007, 10 }, // [Event] Tempering Solution
        new[] { 100, 188950015, 2 }, // Special Courier Pass (Eternal/Lv. 61-65)
        new[] { 150, 188053099, 1 }, // Pure Modor's Equipment Crux Box
        new[] { 200, 188054238, 1 }, // Iron Wall Armor Box
        new[] { 250, 187000090, 1 }, // Tiamat's Spectral Wings
    };

    public Symphony()
        : base("symphony", "Exchanges " + ChatUtil.Item(REQUIRED_ITEM_ID) + " for prizes.")
    {
        SetSyntaxInfo(BuildSyntaxInfo());
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
        if (rewardIndex < 0 || rewardIndex >= REWARDS.Length)
        {
            SendInfo(player);
            return;
        }

        int cost = REWARDS[rewardIndex][0];
        if (player.GetInventory().GetItemCountByItemId(REQUIRED_ITEM_ID) < cost || !player.GetInventory().DecreaseByItemId(REQUIRED_ITEM_ID, cost))
        {
            SendInfo(player, "You need " + cost + " " + ChatUtil.Item(REQUIRED_ITEM_ID) + " to buy this.");
            return;
        }

        int itemId = REWARDS[rewardIndex][1];
        int itemCount = REWARDS[rewardIndex][2];

        long notAddedCount = ItemService.AddItem(player, itemId, itemCount, true,
            new ItemService.ItemUpdatePredicate(ItemPacketService.ItemAddType.DECOMPOSABLE, ItemPacketService.ItemUpdateType.INC_CASH_ITEM));
        if (notAddedCount > 0)
        {
            log.LogWarning("[Legendary Symphony Event] {NotAddedCount}x {ItemId} could not be added to {PlayerName}'s inventory.",
                notAddedCount, itemId, player.GetName());
        }
    }

    private static string BuildSyntaxInfo()
    {
        var builder = new StringBuilder("Type .symphony <id> to get your reward:\n");
        for (int i = 0; i < REWARDS.Length; i++)
        {
            int[] reward = REWARDS[i];
            builder.Append('[').Append(i + 1).Append("] - (").Append(reward[0]).Append(" copies): ")
                .Append(reward[2]).Append("x ").Append(ChatUtil.Item(reward[1])).Append('\n');
        }
        return builder.ToString();
    }
}
