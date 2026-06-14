using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Remove_skill_delay_all (Neon). Toggles zero-cooldown mode for all skills.</summary>
public class Remove_skill_delay_all : ConsoleCommand
{
    public Remove_skill_delay_all()
        : base("remove_skill_delay_all", "Enables/disables zero cooldown mode for all skills.")
    {
        SetSyntaxInfo("<1|0> - Enable or disable skill cooldowns.");
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }
        if (paramsArr[0].Equals("1") && player.IsInCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE))
        {
            SendInfo(player, "Cooldown times of all skills have been recovered.");
            player.UnsetCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE);
        }
        else if (paramsArr[0].Equals("0") && !player.IsInCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE))
        {
            SendInfo(player, "Cooldown times of all skills have been disabled.");
            player.SetCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE);
        }
    }
}
