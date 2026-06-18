using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Status (Yeats). Shows the GM player-status window for a target/named player.</summary>
public class Status : ConsoleCommand
{
    public Status()
        : base("status")
    {
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        Player target = null;
        if (paramsArr.Length > 0)
        {
            target = World.World.GetInstance().GetPlayer(paramsArr[0]);
        }
        if (target == null && admin.GetTarget() is Player targetPlayer)
        {
            target = targetPlayer;
        }
        if (target != null)
        {
            PacketSendUtility.SendPacket(admin, new SM_GM_SHOW_PLAYER_STATUS(target));
        }
    }
}
