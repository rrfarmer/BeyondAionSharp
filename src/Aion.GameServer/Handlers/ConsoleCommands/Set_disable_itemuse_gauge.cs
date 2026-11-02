using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Set_disable_itemuse_gauge. Enables/disables the use time of your items.</summary>
public class Set_disable_itemuse_gauge : ConsoleCommand
{
    public Set_disable_itemuse_gauge()
        : base("set_disable_itemuse_gauge", "Enables/disables the use time of your items.", """
            1 - Items are used instantly.
            0 - Items take their regular use time.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 || !paramsArr[0].Equals("0") && !paramsArr[0].Equals("1"))
        {
            SendInfo(player);
            return;
        }
        if (paramsArr[0].Equals("1"))
        {
            player.SetCustomState(CustomPlayerState.NO_ITEM_USE_GAUGE_MODE);
            SendInfo(player, "Item usage animations have been disabled.");
        }
        else
        {
            player.UnsetCustomState(CustomPlayerState.NO_ITEM_USE_GAUGE_MODE);
            SendInfo(player, "Item usage animations have been enabled.");
        }
    }
}
