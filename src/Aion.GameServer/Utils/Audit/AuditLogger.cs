using Aion.GameServer.Configs.Administration;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Utils.Audit;

/// <summary>Java parity: utils/audit/AuditLogger (MrPoke, Neon).</summary>
public class AuditLogger
{
    private static readonly ILogger log = AionLog.For("AUDIT_LOG"); // Java logger name: "AUDIT_LOG"

    /// <summary>Replaces control characters so a client cannot forge log lines.</summary>
    private static string Sanitize(string message)
    {
        if (message == null)
            return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder(message.Length);
        for (int i = 0; i < message.Length; i++)
        {
            char c = message[i];
            if (c == '\n')
                sb.Append("\\n");
            else if (c == '\r')
                sb.Append("\\r");
            else if (Aion.GameServer.Utils.ChatUtil.IsInvalidCharacter(c))
                sb.Append($"\\u{(int)c:x4}");
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Logs message, if audit log is enabled. Notifies permitted online staff members.
    /// Automatically punishes player, if punishments are enabled.
    /// </summary>
    public static void Log(Aion.GameServer.Model.GameObjects.Players.Player player, string message)
    {
        message = Sanitize(message);
        if (PunishmentConfig.PUNISHMENT_ENABLE)
            AutoBan.Punishment(player);

        if (LoggingConfig.LOG_AUDIT)
            log.LogInformation("{Player} {Message}", player, message);

        foreach (Aion.GameServer.Model.GameObjects.Players.Player gm in GMService.GetInstance().GetOnlineStaffMembers())
        {
            if (gm.HasAccess(AdminConfig.AUDIT_INFO))
                Aion.GameServer.Utils.PacketSendUtility.SendMessage(gm, Aion.GameServer.Utils.ChatUtil.CharName(player) + " " + message, ChatType.YELLOW);
        }
    }
}
