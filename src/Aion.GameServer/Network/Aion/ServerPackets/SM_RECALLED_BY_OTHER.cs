using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECALLED_BY_OTHER (SVDNESS). Summon-by-other confirmation window.</summary>
public class SM_RECALLED_BY_OTHER : AionServerPacket
{
    public const int RECALL_REQUEST_ID = 0x0F44;
    private readonly string casterName;
    private readonly int skillId;
    private readonly int timeSeconds;

    public SM_RECALLED_BY_OTHER(string casterName, int skillId, int timeSeconds)
    {
        this.casterName = casterName;
        this.skillId = skillId;
        this.timeSeconds = timeSeconds;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(0); // Retail always sends 0.
        WriteS(casterName);
        WriteH(skillId);
        WriteH(timeSeconds);
    }
}
