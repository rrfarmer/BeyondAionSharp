using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Leveldown (ginho1, Neon). Levels the selected player down.</summary>
public class Leveldown : ConsoleCommand
{
    public Leveldown()
        : base("leveldown", "Levels a player down.")
    {
        SetSyntaxInfo("<value> - Levels your target down by the specified number of levels.");
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 1)
        {
            SendInfo(admin);
            return;
        }

        VisibleObject target = admin.GetTarget();
        if (target is not Player player)
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }

        // Java parity: try { newLevel = getLevel() - parseInt(...) } catch (NumberFormatException) { ... }
        if (!int.TryParse(paramsArr[0], out int delta))
        {
            SendInfo(admin, "Please specify the number of levels to subtract.");
            return;
        }
        int newLevel = player.GetLevel() - delta;

        if (newLevel < 1 || newLevel > GSConfig.PLAYER_MAX_LEVEL)
        {
            SendInfo(admin, "Invalid level.");
            return;
        }

        player.GetCommonData().SetLevel(newLevel);
        SendInfo(admin, "Set " + player.GetName() + "'s level to " + player.GetLevel());
    }
}
