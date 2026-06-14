using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Enemy (Neon). Modifies your enmity towards others.</summary>
public class Enemy : AdminCommand
{
    public Enemy()
        : base("enemy", "Modifies your enmity towards others.")
    {
        SetSyntaxInfo(
            "<all> [players|npcs] - Sets your enmity (default: you're enemy of everyone, optional: you're enemy of all players, or all npcs).",
            "<none> [players|npcs] - Disables your enmity (default: you're enemy of nobody, optional: you're enemy of no player, or no npc).",
            "<cancel> - Resets your enmity to the default."
        );
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }

        if (paramsArr[0].Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
            {
                player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_EVERYONE);
                player.SetCustomState(CustomPlayerState.ENEMY_OF_EVERYONE);
                SendInfo(player, "You are now enemy of everyone.");
            }
            else if (paramsArr[1].Equals("npcs", StringComparison.OrdinalIgnoreCase))
            {
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_EVERYONE);
                player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
                player.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_NPCS);
                SendInfo(player, "You are now enemy of all npcs.");
            }
            else if (paramsArr[1].Equals("players", StringComparison.OrdinalIgnoreCase))
            {
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_EVERYONE);
                player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_PLAYERS);
                player.SetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
                SendInfo(player, "You are now enemy of all players.");
            }
            else
            {
                SendInfo(player);
                return;
            }
        }
        else if (paramsArr[0].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            if (paramsArr.Length == 1)
            {
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_EVERYONE);
                player.SetCustomState(CustomPlayerState.NEUTRAL_TO_EVERYONE);
                SendInfo(player, "You are now neutral to everyone.");
            }
            else if (paramsArr[1].Equals("npcs", StringComparison.OrdinalIgnoreCase))
            {
                player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_EVERYONE);
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_ALL_NPCS);
                player.SetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_NPCS);
                SendInfo(player, "You are now neutral to all npcs.");
            }
            else if (paramsArr[1].Equals("players", StringComparison.OrdinalIgnoreCase))
            {
                player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_EVERYONE);
                player.UnsetCustomState(CustomPlayerState.ENEMY_OF_ALL_PLAYERS);
                player.SetCustomState(CustomPlayerState.NEUTRAL_TO_ALL_PLAYERS);
                SendInfo(player, "You are now neutral to all players.");
            }
            else
            {
                SendInfo(player);
                return;
            }
        }
        else if (paramsArr[0].Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            player.UnsetCustomState(CustomPlayerState.ENEMY_OF_EVERYONE);
            player.UnsetCustomState(CustomPlayerState.NEUTRAL_TO_EVERYONE);
            SendInfo(player, "You appear regular to everyone again.");
        }
        else
        {
            SendInfo(player);
            return;
        }
        player.GetController().OnChangedPlayerAttributes();
    }
}
