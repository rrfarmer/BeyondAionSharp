using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Grant (Neon). Grants/revokes account permissions.</summary>
public class Grant : AdminCommand
{
    public Grant()
        : base("grant", "Grants/revokes account permissions.")
    {
        SetSyntaxInfo(
            "<a> <level> [name] - Grants the specified access level (default: target's account, optional: specified character's account). 0 will remove the account's access level.",
            "<m> <level> [name] - Grants the specified membership level (default: target's account, optional: specified character's account). 0 will remove the account's membership level."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length < 2)
        {
            SendInfo(admin);
            return;
        }

        int type;
        if ("a".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            type = 1;
        }
        else if ("m".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase))
        {
            type = 2;
        }
        else
        {
            SendInfo(admin);
            return;
        }

        int level = int.Parse(paramsArr[1]);
        if (level < 0)
        {
            SendInfo(admin, "Level must not be negative.");
            return;
        }

        Player player;
        if (paramsArr.Length >= 3)
        {
            string playerName = Util.ConvertName(paramsArr[2]);
            player = World.World.GetInstance().GetPlayer(playerName);
            if (player == null)
            {
                PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_NO_SUCH_USER(playerName));
                return;
            }
        }
        else if (admin.GetTarget() is Player target)
        {
            player = target;
        }
        else
        {
            PacketSendUtility.SendPacket(admin, SM_SYSTEM_MESSAGE.STR_INVALID_TARGET());
            return;
        }
        if (type == 1)
        {
            if (!player.Equals(admin) && player.GetAccount().GetAccessLevel() >= admin.GetAccount().GetAccessLevel())
            {
                SendInfo(admin, "You are not allowed change the access level of players with the same or higher access level than your own.");
                return;
            }
        }

        LoginServer.GetInstance().SendLsControlPacket(type, level, player, admin);
    }
}
