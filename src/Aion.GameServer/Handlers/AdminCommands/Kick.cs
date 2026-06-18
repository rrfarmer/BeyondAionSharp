using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Kick (Elusive, Neon).</summary>
public class Kick : AdminCommand
{
    public Kick()
        : base("kick", "Disconnects players from the server.")
    {
        SetSyntaxInfo(
            "<name> - Disconnects the player with the specified name.",
            "<ALL> - Disconnects everyone (parameter must be typed in uppercase, for safety)."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }

        if ("ALL".Equals(paramsArr[0]))
        {
            if (Aion.GameServer.World.World.GetInstance().GetAllPlayers().Count == 1)
            {
                SendInfo(admin, "There is nobody online to kick.");
                return;
            }
            Aion.GameServer.World.World.GetInstance().ForEachPlayer(player =>
            {
                if (!player.Equals(admin))
                {
                    player.GetClientConnection().Close(SM_SYSTEM_MESSAGE.STR_KICK_CHARACTER());
                    PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_USER_KICKED(player.GetName()));
                }
            });
        }
        else
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(Util.ConvertName(paramsArr[0]));
            if (player == null)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_BUDDYLIST_NO_OFFLINE_CHARACTER());
                return;
            }
            player.GetClientConnection().Close(SM_SYSTEM_MESSAGE.STR_KICK_CHARACTER());
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_USER_KICKED(player.GetName()));
        }
    }
}
