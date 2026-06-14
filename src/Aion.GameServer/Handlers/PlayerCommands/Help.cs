using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Help (Neon). Lists all commands the player is allowed to use.</summary>
public class Help : PlayerCommand
{
    public Help()
        : base("help", "Lists all commands you are allowed to use.")
    {
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        List<ChatCommand> allowedCommands = FindAllowedCommands(player);

        if (allowedCommands.Count != 0 && !(allowedCommands.Count == 1 && allowedCommands.Contains(this)))
        {
            allowedCommands.Sort((a, b) => string.CompareOrdinal(a.GetAliasWithPrefix().ToLower(), b.GetAliasWithPrefix().ToLower()));
            StringBuilder sb = new StringBuilder("List of available commands (" + allowedCommands.Count + "):");
            foreach (ChatCommand cmd in allowedCommands)
            {
                string desc = cmd.GetDescription().Length == 0 ? "No description available." : cmd.GetDescription();
                sb.Append("\n\t" + ChatUtil.Color(cmd.GetAliasWithPrefix(), Color.White) + " - " + desc);
            }
            sb.Append("\nType <" + ChatUtil.Color("command", Color.White) + "> " + ChatUtil.Color("help", Color.White)
                + " to get further information about a command.");
            SendInfo(player, sb.ToString());
        }
        else
        {
            SendInfo(player, "You are not allowed to use any chat commands other than " + ChatUtil.Color(GetAliasWithPrefix(), Color.White) + ".");
        }
    }

    private List<ChatCommand> FindAllowedCommands(Player player)
    {
        ChatProcessor cp = ChatProcessor.GetInstance();
        List<ChatCommand> cmds = new List<ChatCommand>();

        foreach (ChatCommand cmd in cp.GetCommandList())
        {
            if (cp.IsCommandAllowed(player, cmd) && !(cmd is ConsoleCommand))
                cmds.Add(cmd);
        }

        return cmds;
    }
}
