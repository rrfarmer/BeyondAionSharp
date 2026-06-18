using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/UnBanIp (Watson).</summary>
public class UnBanIp : AdminCommand
{
    public UnBanIp()
        : base("unbanip")
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr == null || paramsArr.Length < 1)
        {
            PacketSendUtility.SendMessage(player, "Syntax: //unbanip <mask>");
            return;
        }

        LoginServer.GetInstance().SendBanPacket((byte)2, 0, paramsArr[0], -1, player.GetObjectId());
    }

    private void Info(Player player, string message)
    {
        PacketSendUtility.SendMessage(player, "Syntax: //unbanip <mask>");
    }
}
