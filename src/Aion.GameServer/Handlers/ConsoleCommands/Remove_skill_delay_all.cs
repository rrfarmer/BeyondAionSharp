using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.ConsoleCommands;

/// <summary>Java parity: data/handlers/consolecommands/Remove_skill_delay_all (Neon). Toggles zero-cooldown mode for all skills.</summary>
public class Remove_skill_delay_all : ConsoleCommand
{
    public Remove_skill_delay_all()
        : base("remove_skill_delay_all", "Enables/disables your skill cooldowns.", """
            1 - Enables your skill and item cooldowns.
            0 - Removes and disables your skill cooldowns.
            """)
    {
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0 || !paramsArr[0].Equals("0") && !paramsArr[0].Equals("1"))
        {
            SendInfo(player);
            return;
        }
        if (paramsArr[0].Equals("1"))
        {
            player.UnsetCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE);
            SendInfo(player, "Cooldown times of all skills have been recovered.");
            // ideally, the itemcooltime command should handle it but the 'On' GM panel button only sends this command
            player.UnsetCustomState(CustomPlayerState.NO_ITEM_COOLDOWN_MODE);
            SendInfo(player, "Cooldown times of all items have been recovered.");
        }
        else if (paramsArr[0].Equals("0"))
        {
            Aion.GameServer.Handlers.AdminCommands.RemoveCd.RemoveSkillCooldowns(player);
            player.SetCustomState(CustomPlayerState.NO_SKILL_COOLDOWN_MODE);
            SendInfo(player, "Cooldown times of all skills have been disabled.");
        }
    }
}
