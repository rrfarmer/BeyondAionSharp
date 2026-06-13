using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>Java parity: utils/chathandlers/ConsoleCommand (ginho1, Neon). Base for prefix-less console commands; GM-audit logged. CommandsAccessService red-tolerated.</summary>
public abstract class ConsoleCommand : ChatCommand
{
    public const string PREFIX = "";
    internal static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("ADMINAUDIT_LOG");

    // only for backwards compatibility TODO: remove when all commands are updated
    public ConsoleCommand(string alias)
        : this(alias, "")
    {
    }

    public ConsoleCommand(string alias, string description)
        : base(PREFIX, alias, description)
    {
    }

    public override bool ValidateAccess(Player player)
    {
        bool hasAccess = player.HasAccess((sbyte)GetLevel()) || CommandsAccessService.HasAccess(player.GetObjectId(), GetAlias());
        if (!hasAccess && player.IsStaff())
            SendInfo(player, "<You need access level " + GetLevel() + " or higher to use " + GetAliasWithPrefix() + ">");
        return hasAccess;
    }

    internal override bool Process(Player player, params string[] paramsArr)
    {
        if (!ValidateAccess(player))
            return player.IsStaff(); // return false for regular players, so chat will send entered text (this way you can't guess commands without rights)

        if (LoggingConfig.LOG_GMAUDIT)
            log.LogInformation("[Console Command] > [Player: " + player.GetName() + "]"
                + (player.GetTarget() != null ? "[Target: " + player.GetTarget().GetName() + "]" : "") + ": " + GetAliasWithPrefix() + " "
                + string.Join(" ", paramsArr));

        if (!Run(player, paramsArr))
            SendInfo(player, "<Error while executing command>");

        return true;
    }
}
