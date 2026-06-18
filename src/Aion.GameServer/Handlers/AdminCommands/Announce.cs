using System;
using System.Text;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Announce (Neon).</summary>
public class Announce : AdminCommand
{
    public Announce()
        : base("announce", "Sends a server-wide notice.")
    {
        SetSyntaxInfo(
            "<n|a> <message> - Sends the message either with your <n>ame or <a>nonymously.",
            "<ely|asmo> <message> - Sends an anonymous message to <ely>os or <asmo>dian players."
        );
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length <= 1)
        {
            SendInfo(admin);
            return;
        }

        string flag = paramsArr[0].ToLower();
        string[] flags = { "n", "a", "ely", "asmo" };
        if (Array.IndexOf(flags, flag) < 0)
        {
            SendInfo(admin);
            return;
        }

        StringBuilder sb = new StringBuilder();
        Race? allowedRace = null;
        switch (flag)
        {
            case "n":
                sb.Append(ChatUtil.Name(admin) + ":");
                break;
            case "a":
                sb.Append("Announce:");
                break;
            case "ely":
                sb.Append("Elyos:");
                allowedRace = Race.ELYOS;
                break;
            case "asmo":
                sb.Append("Asmodians:");
                allowedRace = Race.ASMODIANS;
                break;
        }

        for (int i = 1; i < paramsArr.Length; i++)
            sb.Append(" ").Append(paramsArr[i]);

        foreach (Player player in Aion.GameServer.World.World.GetInstance().GetAllPlayers())
            if (allowedRace == null || player.GetRace() == allowedRace || ValidateAccess(player))
                PacketSendUtility.SendMessage(player, sb.ToString(), ChatType.BRIGHT_YELLOW_CENTER);
    }
}
