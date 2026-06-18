using System;
using Aion.GameServer.Custom.Pvpmap;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.PlayerCommands;

/// <summary>Java parity: data/handlers/playercommands/Pvp (Yeats). Join/leave/query the custom PvP-Map.</summary>
public class Pvp : PlayerCommand
{
    public Pvp()
        : base("pvp", "Join the custom PvP-Map where you can fight against the opposing faction.")
    {
        SetSyntaxInfo("<join | leave | info> - Join the PvP-Map by typing .pvp join.\nYou can leave the PvP-Map by typing .pvp leave.\nType .pvp info to see how many players are on the PvP-Map.");
    }

    public override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
        }
        else if (paramsArr.Length >= 1)
        {
            if (paramsArr[0].Equals("join", StringComparison.OrdinalIgnoreCase))
            {
                PvpMapService.GetInstance().JoinMap(player);
            }
            else if (paramsArr[0].Equals("leave", StringComparison.OrdinalIgnoreCase))
            {
                PvpMapService.GetInstance().LeaveMap(player);
            }
            else if (paramsArr[0].Equals("info", StringComparison.OrdinalIgnoreCase))
            {
                int size = PvpMapService.GetInstance().GetParticipantsSize();
                SendInfo(player, "There " + (size == 1 ? "is" : "are") + " currently " + (size == 0 ? "no" : size.ToString()) + " player" + (size != 1 ? "s" : "") + " on the map.");
            }
            else
            {
                SendInfo(player);
            }
        }
    }
}
