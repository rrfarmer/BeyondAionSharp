using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INFLUENCE_RATIO (Nemiroff). Faction influence rates + per-world influence (subset of SM_FORTRESS_STATUS). Influence/SiegeRace/SiegeService red-tolerated.</summary>
public class SM_INFLUENCE_RATIO : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        Influence inf = Influence.GetInstance();

        WriteD(SiegeService.GetInstance().GetSecondsUntilNextFortressState());
        WriteF(inf.GetElyosInfluenceRate());
        WriteF(inf.GetAsmodianInfluenceRate());
        WriteF(inf.GetBalaurInfluenceRate());
        WriteH(inf.GetInfluenceRelevantWorldIds().Count);
        foreach (int worldId in inf.GetInfluenceRelevantWorldIds())
        {
            WriteD(worldId);
            WriteF(inf.GetInfluence(worldId, SiegeRace.ELYOS));
            WriteF(inf.GetInfluence(worldId, SiegeRace.ASMODIANS));
            WriteF(inf.GetInfluence(worldId, SiegeRace.BALAUR));
        }
    }
}
