using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SUMMON_PANEL (ATracer, xTz). Summon status panel: objId/level/hp/maxhp/attack/pdef/mresist/livetime. Summon/CalculationType red-tolerated.</summary>
public class SM_SUMMON_PANEL : AionServerPacket
{
    private Summon summon;

    public SM_SUMMON_PANEL(Summon summon)
    {
        this.summon = summon;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(summon.GetObjectId());
        WriteH(summon.GetLevel());
        WriteD(0);// unk
        WriteD(0);// unk
        WriteD(summon.GetLifeStats().GetCurrentHp());
        WriteD(summon.GetGameStats().GetMaxHp().GetCurrent());
        WriteD(summon.GetGameStats().GetMainHandPAttack(CalculationType.DISPLAY).GetCurrent());
        WriteH(summon.GetGameStats().GetPDef().GetCurrent());
        WriteH(0);
        WriteH(summon.GetGameStats().GetMResist().GetCurrent());
        WriteH(0);// unk
        WriteH(0);// unk
        WriteD(summon.GetLiveTime()); // life time
    }
}
