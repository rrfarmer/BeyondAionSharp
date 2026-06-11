using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SKILL_ACTIVATION (Sweetkr). Toggle-skill activation state (unk=0) or stigma-remove (unk=1, active). AionServerPacket red-tolerated.</summary>
public class SM_SKILL_ACTIVATION : AionServerPacket
{
    private bool isActive;
    private int unk;
    private int skillId;

    /// <summary>For toggle skills</summary>
    public SM_SKILL_ACTIVATION(int skillId, bool isActive)
    {
        this.skillId = skillId;
        this.isActive = isActive;
        this.unk = 0;
    }

    /// <summary>For stigma remove (should work in 1.5.1.15)</summary>
    public SM_SKILL_ACTIVATION(int skillId)
    {
        this.skillId = skillId;
        this.isActive = true;
        this.unk = 1;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId);
        WriteD(unk);
        WriteC(isActive ? 1 : 0);
    }
}
