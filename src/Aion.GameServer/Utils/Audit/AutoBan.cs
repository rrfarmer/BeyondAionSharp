using System;
using Aion.GameServer.Configs.Main;

namespace Aion.GameServer.Utils.Audit;

/// <summary>Java parity: utils/audit/AutoBan (synchro2).</summary>
public class AutoBan
{
    // TODO merge with AntiHackService punishment system / rework
    // Java parity: protected static (package-private call from AuditLogger) → internal.
    internal static void Punishment(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        string reason = "You have been punished due to illegal actions";
        string accountIp = player.GetClientConnection().GetIP();
        int accountId = player.GetClientConnection().GetAccount().GetId();
        int time = PunishmentConfig.PUNISHMENT_TIME;
        int minInDay = 1440;
        int dayCount = (int)Math.Floor((double)(time / minInDay));

        switch (PunishmentConfig.PUNISHMENT_TYPE)
        {
            case 1:
                player.GetClientConnection().Close(new Aion.GameServer.Network.Aion.ServerPackets.SmQuitResponse());
                break;
            case 2:
                Aion.GameServer.Services.PunishmentService.BanChar(player.GetObjectId(), dayCount, reason);
                break;
            case 3:
                Aion.GameServer.Network.Loginserver.LoginServer.GetInstance().SendBanPacket((byte)1, accountId, accountIp, time, 0);
                break;
            case 4:
                Aion.GameServer.Network.Loginserver.LoginServer.GetInstance().SendBanPacket((byte)2, accountId, accountIp, time, 0);
                break;
            case 5:
                player.GetClientConnection().Close();
                Aion.GameServer.Network.BannedMacManager.GetInstance().BanAddress(player.GetClientConnection().GetMacAddress(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + time * 60000L, reason);
                break;
        }
    }
}
