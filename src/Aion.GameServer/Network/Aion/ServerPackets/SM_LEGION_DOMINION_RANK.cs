using System.Collections.Generic;
using Aion.GameServer.Model.LegionDominion;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_DOMINION_RANK (Yeats). Legion-dominion ranking (top 25 + the requesting legion if outside top). indexOf/subList/set/get -> IndexOf/GetRange/indexer; subList view-copy semantics fine since ranking is not read afterward. LegionDominion* red-tolerated.</summary>
public class SM_LEGION_DOMINION_RANK : AionServerPacket
{
    private readonly LegionDominionLocation loc;
    private readonly int rank;
    private readonly List<LegionDominionParticipantInfo> topParticipants;

    public SM_LEGION_DOMINION_RANK(LegionDominionLocation loc, Legion legion)
    {
        this.loc = loc;
        List<LegionDominionParticipantInfo> ranking = loc.GetLegionRanking(false);
        LegionDominionParticipantInfo participant = legion == null ? null : loc.GetParticipantInfo(legion.GetLegionId());
        rank = participant == null ? 0 : ranking.IndexOf(participant) + 1;
        topParticipants = ranking.Count > 25 ? ranking.GetRange(0, 25) : ranking;
        if (rank > topParticipants.Count) // if the ranked legion is not top-ranked, the last entry must be the ranked one
            topParticipants[topParticipants.Count - 1] = ranking[rank - 1];
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(loc.GetLocationId());
        WriteC(rank);
        WriteH(topParticipants.Count);
        foreach (LegionDominionParticipantInfo participant in topParticipants)
        {
            WriteD(participant.GetPoints());
            WriteD(participant.GetTime());
            WriteQ(participant.GetDate());
            WriteS(participant.GetLegionName());
        }
    }
}
