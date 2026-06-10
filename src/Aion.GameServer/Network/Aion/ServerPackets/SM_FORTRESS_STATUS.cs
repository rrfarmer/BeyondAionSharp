using System.Collections.Generic;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FORTRESS_STATUS. Global fortress + influence state: next-state countdown, faction influence rates, per-world influence, per-fortress next state. FortressLocation/Influence/SiegeRace/SiegeService red-tolerated.</summary>
public class SM_FORTRESS_STATUS : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        IDictionary<int, FortressLocation> fortresses = SiegeService.GetInstance().GetFortresses();
        Influence inf = Influence.GetInstance();

        WriteC(1);
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
        WriteH(fortresses.Count);
        foreach (FortressLocation fortress in fortresses.Values)
        {
            WriteD(fortress.GetLocationId());
            WriteC(fortress.GetNextState());
        }
    }
}
