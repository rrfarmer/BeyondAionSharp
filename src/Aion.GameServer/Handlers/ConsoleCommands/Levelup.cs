using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Levelup (ginho1, Neon). Levels the selected player up.</summary>
public class Levelup : ConsoleCommand
{
    public Levelup()
        : base("levelup", "Levels a player up.")
    {
        SetSyntaxInfo("<value> - Levels your target up by the specified number of levels (defaults to your character, if no player is targeted).");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }

        Player player = admin.GetTarget() is Player target ? target : admin;

        // Java parity: try { newLevel = getLevel() + parseInt(...) } catch (NumberFormatException) { ... }
        if (!TryParseInt(paramsArr[0], out int delta))
        {
            SendInfo(admin, "Please specify the number of levels to add.");
            return;
        }
        int newLevel = player.GetLevel() + delta;

        if (newLevel < 1 || newLevel > GSConfig.PLAYER_MAX_LEVEL)
        {
            SendInfo(admin, "Invalid level.");
            return;
        }

        player.GetCommonData().SetLevel(newLevel);
        SendInfo(admin, "Set " + player.GetName() + "'s level to " + player.GetLevel());
    }
}
