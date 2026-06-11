using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerChatService (Source, Neon). Chat flood detection (isFlooding) and chat logging (logWhisper/logMessage) routed to CHAT_LOG or ADMINAUDIT_LOG (when GM involved in a whisper), honoring private/general chat logging settings, with per-ChatType formatting. String.format(%s/%d)->string.Format({0}); enum.toString()->ToString(). ChatType/SecurityConfig/LoggingConfig red-tolerated.</summary>
public class PlayerChatService
{
    private static readonly ILogger playerLog = NullLoggerFactory.Instance.CreateLogger("CHAT_LOG");
    private static readonly ILogger gmLog = NullLoggerFactory.Instance.CreateLogger("ADMINAUDIT_LOG");

    public static bool IsFlooding(Player player)
    {
        player.SetLastMessageTime();

        if (player.FloodMsgCount() > SecurityConfig.FLOOD_MSG)
            return true;

        return false;
    }

    public static void LogWhisper(Player sender, Player receiver, string message)
    {
        LogMessage(sender, ChatType.WHISPER, message, receiver);
    }

    public static void LogMessage(Player sender, ChatType type, string message)
    {
        LogMessage(sender, type, message, null);
    }

    private static void LogMessage(Player sender, ChatType type, string message, Player receiver)
    {
        ILogger log = playerLog;

        // log whisper to adminaudit.log, if GM is involved (ignores private chat logging settings)
        if (type == ChatType.WHISPER && (sender.IsStaff() || (receiver != null && receiver.IsStaff())) && LoggingConfig.LOG_GMAUDIT)
            log = gmLog;
        else
        {
            switch (type)
            {
                case ChatType.WHISPER:
                case ChatType.LEGION:
                    if (!LoggingConfig.LOG_PRIVATE_CHATS)
                        return;
                    break;
                default:
                    if (!LoggingConfig.LOG_GENERAL_CHATS)
                        return;
                    break;
            }
        }

        switch (type)
        {
            case ChatType.WHISPER:
                log.LogInformation(string.Format("[{0}] - [{1}]>[{2}]: {3}", type.ToString(), sender.GetName(), receiver != null ? receiver.GetName() : "", message));
                break;
            case ChatType.GROUP:
            case ChatType.ALLIANCE:
            case ChatType.GROUP_LEADER:
            case ChatType.LEAGUE:
            case ChatType.LEAGUE_ALERT:
                log.LogInformation(string.Format("[{0}] <{1}> - [{2}]: {3}", type.ToString(), sender.GetCurrentTeamId(), sender.GetName(), message));
                break;
            case ChatType.LEGION:
                log.LogInformation(string.Format("[{0}] <{1}> - [{2}]: {3}", type.ToString(), sender.GetLegion().GetName(), sender.GetName(), message));
                break;
            case ChatType.NORMAL:
            case ChatType.SHOUT:
            default:
                log.LogInformation(string.Format("[{0}] - [{1}]({2}): {3}", type.ToString(), sender.GetName(), sender.GetRace().ToString(), message));
                break;
        }
    }
}
