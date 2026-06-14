using System;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>Java parity: data/handlers/admincommands/Movie (d3v1an).</summary>
public class Movie : AdminCommand
{
    public Movie()
        : base("movie")
    {
        SetSyntaxInfo(
            "<cutsceneId> - Plays the given cutscene (correct rendering depends on your current map)",
            "m <movieId> - Plays the given movie cutscene"
        );
    }

    protected override void Execute(Player player, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(player);
            return;
        }
        bool isCutsceneMovie = "m".Equals(paramsArr[0], StringComparison.OrdinalIgnoreCase);
        int cutsceneId = int.Parse(paramsArr[isCutsceneMovie ? 1 : 0]);
        PacketSendUtility.SendPacket(player, new SM_PLAY_MOVIE(isCutsceneMovie, 0, 0, cutsceneId, true));
    }
}
