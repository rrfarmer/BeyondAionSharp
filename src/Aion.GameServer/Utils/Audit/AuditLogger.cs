using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Utils.Audit;

/// <summary>Java parity: utils/audit/AuditLogger (MrPoke, Neon).</summary>
public class AuditLogger
{
    private static readonly ILogger log = NullLogger.Instance; // Java logger name: "AUDIT_LOG"

    /// <summary>
    /// Logs message, if audit log is enabled. Notifies permitted online staff members.
    /// Automatically punishes player, if punishments are enabled.
    /// </summary>
    public static void Log(Aion.GameServer.Model.GameObjects.Players.Player player, string message)
    {
        if (PunishmentConfig.PUNISHMENT_ENABLE)
            AutoBan.Punishment(player);

        if (LoggingConfig.LOG_AUDIT)
            log.LogInformation(player + " " + message);

        foreach (Aion.GameServer.Model.GameObjects.Players.Player gm in GMService.GetInstance().GetOnlineStaffMembers())
        {
            if (gm.HasAccess(AdminConfig.AUDIT_INFO))
                Aion.GameServer.Utils.PacketSendUtility.SendMessage(gm, Aion.GameServer.Utils.ChatUtil.Name(player) + " " + message, ChatType.YELLOW);
        }
    }
}
