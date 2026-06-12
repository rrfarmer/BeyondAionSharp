using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Abyss;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;
using AbyssRankUpdateType = global::Aion.GameServer.Model.GameObjects.Players.AbyssRank.AbyssRankUpdateType;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_PLAYERS (SheppeR). Requests the abyss player ranking for a race. AbyssRank.AbyssRankUpdateType aliased; AionClientPacket base red-tolerated.</summary>
public class CM_ABYSS_RANKING_PLAYERS : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_ABYSS_RANKING_PLAYERS));

    private byte raceId;

    public CM_ABYSS_RANKING_PLAYERS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        raceId = ReadC();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        Race queriedRace;
        AbyssRankUpdateType updateType;
        switch (raceId)
        {
            case 0:
                queriedRace = Race.ELYOS;
                updateType = AbyssRankUpdateType.PLAYER_ELYOS;
                break;
            case 1:
                queriedRace = Race.ASMODIANS;
                updateType = AbyssRankUpdateType.PLAYER_ASMODIANS;
                break;
            default:
                log.LogWarning("Received invalid raceId (" + raceId + ") from player " + player);
                return;
        }
        if (player.IsAbyssRankListUpdated(updateType))
        {
            SendPacket(new SM_ABYSS_RANKING_PLAYERS(AbyssRankingCache.GetInstance().GetLastUpdate(), queriedRace));
        }
        else
        {
            List<SM_ABYSS_RANKING_PLAYERS> results = AbyssRankingCache.GetInstance().GetPlayers(queriedRace);
            foreach (SM_ABYSS_RANKING_PLAYERS packet in results)
                SendPacket(packet);
            player.SetAbyssRankListUpdated(updateType);
        }
    }
}
