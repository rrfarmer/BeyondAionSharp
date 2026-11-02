using Aion.GameServer.Handlers.AdminCommands;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Itemcooltime (Neon). Removes and disables cooldowns of all items.</summary>
public class Itemcooltime : ConsoleCommand
{
    public Itemcooltime()
        : base("itemcooltime", "Removes and disables cooldowns of all items.")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        RemoveCd.RemoveItemCooldowns(player);
        player.SetCustomState(CustomPlayerState.NO_ITEM_COOLDOWN_MODE); // remove_skill_delay_all 1 re-enables it because of how the GM panel buttons work
        SendInfo(player, "Cooldown times of all items have been disabled.");
    }
}
