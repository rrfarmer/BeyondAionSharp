using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_KISK_UPDATE (Sarynth). Kisk status (objId/creator/useMask/member counts/resurrects/lifetime). Kisk red-tolerated.</summary>
public class SM_KISK_UPDATE : AionServerPacket
{
    private readonly Kisk kisk;

    public SM_KISK_UPDATE(Kisk kisk)
    {
        this.kisk = kisk;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(kisk.GetObjectId());
        WriteD(kisk.GetCreatorId());
        WriteD(kisk.GetUseMask());
        WriteD(kisk.GetCurrentMemberCount());
        WriteD(kisk.GetMaxMembers());
        WriteD(kisk.GetRemainingResurrects());
        WriteD(kisk.GetMaxRessurects());
        WriteD(kisk.GetRemainingLifetime());
    }
}
