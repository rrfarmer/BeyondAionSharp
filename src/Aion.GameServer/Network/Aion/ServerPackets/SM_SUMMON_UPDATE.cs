using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SUMMON_UPDATE (ATracer). Sends a summon's level/mode/HP and full current+base stat block (attack/defense/resist/accuracy/crit/parry/evasion). Stat2/CalculationType.DISPLAY/Summon/AionServerPacket red-tolerated.</summary>
public class SM_SUMMON_UPDATE : AionServerPacket
{
    private Summon summon;

    public SM_SUMMON_UPDATE(Summon summon)
    {
        this.summon = summon;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(summon.GetLevel());
        WriteH(summon.GetMode().GetId());
        WriteD(0);// unk
        WriteD(0);// unk
        WriteD(summon.GetLifeStats().GetCurrentHp());

        Stat2 maxHp = summon.GetGameStats().GetMaxHp();
        WriteD(maxHp.GetCurrent());

        Stat2 mainHandPAttack = summon.GetGameStats().GetMainHandPAttack(CalculationType.DISPLAY);
        WriteD(mainHandPAttack.GetCurrent());

        Stat2 pDef = summon.GetGameStats().GetPDef();
        WriteD(pDef.GetCurrent());

        Stat2 mResist = summon.GetGameStats().GetMResist();
        WriteH(mResist.GetCurrent());

        Stat2 mDef = summon.GetGameStats().GetMDef();
        WriteD(mDef.GetCurrent());

        Stat2 accuracy = summon.GetGameStats().GetMainHandPAccuracy();
        WriteH(accuracy.GetCurrent());

        Stat2 mainHandPCritical = summon.GetGameStats().GetMainHandPCritical();
        WriteH(mainHandPCritical.GetCurrent());

        Stat2 mBoost = summon.GetGameStats().GetMBoost();
        WriteH(mBoost.GetCurrent());

        Stat2 suppression = summon.GetGameStats().GetMBResist();
        WriteH(suppression.GetCurrent());

        Stat2 mAccuracy = summon.GetGameStats().GetMAccuracy();
        WriteH(mAccuracy.GetCurrent());

        Stat2 mCritical = summon.GetGameStats().GetMCritical();
        WriteH(mCritical.GetCurrent());

        Stat2 parry = summon.GetGameStats().GetParry();
        WriteH(parry.GetCurrent());

        Stat2 evasion = summon.GetGameStats().GetEvasion();
        WriteH(evasion.GetCurrent());

        WriteD(maxHp.GetBase());
        WriteD(mainHandPAttack.GetBase());
        WriteD(pDef.GetBase());
        WriteH(mResist.GetBase());
        WriteD(mDef.GetBase());
        WriteH(accuracy.GetBase());
        WriteH(mainHandPCritical.GetBase());
        WriteH(mBoost.GetBase());
        WriteH(suppression.GetBase());
        WriteH(mAccuracy.GetBase());
        WriteH(mCritical.GetBase());
        WriteH(parry.GetBase());
        WriteH(evasion.GetBase());
    }
}
