using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SUMMON_PANEL_REMOVE (ATracer). Removes a summon skill panel entry (skillId + present flag).</summary>
public class SM_SUMMON_PANEL_REMOVE : AionServerPacket
{
    private int skillId;

    public SM_SUMMON_PANEL_REMOVE(int skillId)
    {
        this.skillId = skillId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId); // skillId
        if (skillId != 0)
            WriteC(1); // unk = 1
        else
            WriteC(0); // unk
    }
}
