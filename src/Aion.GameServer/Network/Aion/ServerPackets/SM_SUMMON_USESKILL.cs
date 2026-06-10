using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SUMMON_USESKILL (ATracer). Summon casts a skill (summonId, skillId, lvl, targetId).</summary>
public class SM_SUMMON_USESKILL : AionServerPacket
{
    private int summonId;
    private int skillId;
    private int skillLvl;
    private int targetId;

    public SM_SUMMON_USESKILL(int summonId, int skillId, int skillLvl, int targetId)
    {
        this.summonId = summonId;
        this.skillId = skillId;
        this.skillLvl = skillLvl;
        this.targetId = targetId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(summonId);
        WriteH(skillId);
        WriteC(skillLvl);
        WriteD(targetId);
    }
}
