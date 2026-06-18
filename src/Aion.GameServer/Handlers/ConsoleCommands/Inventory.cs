using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Inventory (Yeats). No-op — the GM inventory-view body is commented out in the Java source.</summary>
public class Inventory : ConsoleCommand
{
    public Inventory()
        : base("inventory")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        return;
        // Java parity: the remainder of execute(...) is commented out in the Java source (SM_GM_SHOW_PLAYER_SKILLS-based inventory dump), so this command is a no-op.
    }
}
