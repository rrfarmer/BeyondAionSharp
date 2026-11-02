using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_RESET_SKILL_COOLDOWN (ATracer).
/// Opcode 73, [S_RESET_SKILL_COOLING_TIME]: resets the cooldown of one skill. Upstream 9b15b6df8 renamed it from
/// SM_SUMMON_PANEL_REMOVE, which described the one caller rather than the packet; the bytes are unchanged.
/// </summary>
public class SM_RESET_SKILL_COOLDOWN : AionServerPacket
{
    private readonly int skillId;

    public SM_RESET_SKILL_COOLDOWN(int skillId)
    {
        this.skillId = skillId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(skillId);
        WriteC(skillId == 0 ? 0 : 1); // the client reads only the skill ID
    }
}
