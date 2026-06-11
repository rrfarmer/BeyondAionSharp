using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Abyss;
using State = Aion.GameServer.Network.Aion.AionConnection.State;
using AbyssRankUpdateType = Aion.GameServer.Model.GameObjects.Players.AbyssRank.AbyssRankUpdateType;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_ABYSS_RANKING_LEGIONS (SheppeR). Requests the abyss legion ranking for a race. AbyssRank.AbyssRankUpdateType aliased; AionClientPacket base red-tolerated.</summary>
public class CM_ABYSS_RANKING_LEGIONS : AionClientPacket
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(CM_ABYSS_RANKING_LEGIONS));

    private byte raceId;

    public CM_ABYSS_RANKING_LEGIONS(int opcode, ISet<State> validStates)
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
                updateType = AbyssRankUpdateType.LEGION_ELYOS;
                break;
            case 1:
                queriedRace = Race.ASMODIANS;
                updateType = AbyssRankUpdateType.LEGION_ASMODIANS;
                break;
            default:
                log.LogWarning("Received invalid raceId (" + raceId + ") from player " + player);
                return;
        }
        // calculate rankings and send packet
        SM_ABYSS_RANKING_LEGIONS legionRanking;
        if (player.IsAbyssRankListUpdated(updateType))
        {
            legionRanking = new SM_ABYSS_RANKING_LEGIONS(AbyssRankingCache.GetInstance().GetLastUpdate(), queriedRace);
        }
        else
        {
            legionRanking = AbyssRankingCache.GetInstance().GetLegions(queriedRace);
            player.SetAbyssRankListUpdated(updateType);
        }
        SendPacket(legionRanking);
    }
}
