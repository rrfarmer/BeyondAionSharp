using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;

namespace Aion.GameServer.Utils.ChatHandlers;

/// <summary>Java parity: utils/chathandlers/PlayerCommand (synchro2, Neon). Base for "." player commands. CommandsAccessService red-tolerated.</summary>
public abstract class PlayerCommand : ChatCommand
{
    public const string PREFIX = ".";

    public PlayerCommand(string alias, string description)
        : base(PREFIX, alias, description)
    {
    }

    public override bool ValidateAccess(Player player)
    {
        bool hasAccess = player.HasPermission(GetLevel()) || CommandsAccessService.HasAccess(player.GetObjectId(), GetAliasForLevel());
        if (!hasAccess && player.IsStaff())
            SendInfo(player, "<You need membership level " + GetLevel() + " or higher to use " + GetAliasWithPrefix() + ">");
        return hasAccess;
    }

    internal override bool Process(Player player, params string[] paramsArr)
    {
        if (!ValidateAccess(player))
            return player.IsStaff(); // return false for regular players, so chat will send entered text (this way you can't guess commands without rights)

        if (!Run(player, paramsArr))
            SendInfo(player, "<Error while executing command>");

        return true;
    }
}
