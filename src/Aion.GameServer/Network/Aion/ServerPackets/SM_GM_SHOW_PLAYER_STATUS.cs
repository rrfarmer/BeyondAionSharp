using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_PLAYER_STATUS (Yeats). GM stat-sheet dump for a player (name + reserved Qs + full current/base stat block, same layout as SM_STATS_INFO). getCurrent()/getBase()->PascalCase; writeF(x/1000f); StatEnum/PlayerGameStats/CalculationType red-tolerated.</summary>
public class SM_GM_SHOW_PLAYER_STATUS : AionServerPacket
{
    private Player player;
    private PlayerGameStats pgs;
    private PlayerLifeStats pls;
    private PlayerCommonData pcd;

    public SM_GM_SHOW_PLAYER_STATUS(Player player)
    {
        this.player = player;
        this.pcd = player.GetCommonData();
        this.pgs = player.GetGameStats();
        this.pls = player.GetLifeStats();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteS(player.GetName());
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteQ(0);
        WriteD(0);
        WriteC(0);

        WriteH(pgs.GetPower().GetCurrent());// [current power]
        WriteH(pgs.GetHealth().GetCurrent());// [current health]
        WriteH(pgs.GetAccuracy().GetCurrent());// [current accuracy]
        WriteH(pgs.GetAgility().GetCurrent());// [current agility]
        WriteH(pgs.GetKnowledge().GetCurrent());// [current knowledge]
        WriteH(pgs.GetWill().GetCurrent());// [current will]

        WriteH(pgs.GetStat(StatEnum.WATER_RESISTANCE, 0).GetCurrent());// [current water]
        WriteH(pgs.GetStat(StatEnum.WIND_RESISTANCE, 0).GetCurrent());// [current wind]
        WriteH(pgs.GetStat(StatEnum.EARTH_RESISTANCE, 0).GetCurrent());// [current earth]
        WriteH(pgs.GetStat(StatEnum.FIRE_RESISTANCE, 0).GetCurrent());// [current fire]
        WriteH(pgs.GetStat(StatEnum.ELEMENTAL_RESISTANCE_LIGHT, 0).GetCurrent());// [current light resistance]
        WriteH(pgs.GetStat(StatEnum.ELEMENTAL_RESISTANCE_DARK, 0).GetCurrent());// [current dark resistance]

        WriteH(player.GetLevel());// [level]

        // something like very dynamic
        WriteH(0);// [unk]
        WriteH(0);// [unk]
        WriteH(0);// [unk]

        WriteQ(pcd.GetExpNeed());// [xp till next lv]
        WriteQ(pcd.GetExpRecoverable());// [recoverable exp]
        WriteQ(pcd.GetExpShown());// [current xp]

        WriteD(0);// [unk]

        WriteD(pgs.GetMaxHp().GetCurrent());// [max hp]
        WriteD(pls.GetCurrentHp());// [current hp]

        WriteD(pgs.GetMaxMp().GetCurrent());// [max mana]
        WriteD(pls.GetCurrentMp());// [current mana]

        WriteH(pgs.GetMaxDp().GetCurrent());// [max dp]
        WriteH(pcd.GetDp());// [current dp]

        WriteD(pgs.GetFlyTime().GetCurrent());// [max fly time]
        WriteD(pls.GetCurrentFp());// [current fly time]

        WriteC(player.GetFlyState());// [fly state]
        WriteC(player.GetMoveController().GetMovementMask());// [movementMask]

        WriteH(pgs.GetMainHandPAttack(CalculationType.DISPLAY).GetCurrent());// [current main hand attack]
        WriteH(pgs.GetOffHandPAttack(CalculationType.DISPLAY).GetCurrent());// [off hand attack]

        WriteH(0);// unk 3.0

        WriteD(pgs.GetPDef().GetCurrent());// [current pdef]
        WriteH(pgs.GetMainHandMAttack(CalculationType.DISPLAY).GetCurrent());// [current magic attack]
        WriteH(pgs.GetOffHandMAttack(CalculationType.DISPLAY).GetCurrent());
        WriteD(pgs.GetMDef().GetCurrent()); // [Current magic def]
        WriteH(pgs.GetMResist().GetCurrent());// [current mres]
        WriteH(0);// unk 3.0
        WriteF(pgs.GetAttackRange().GetCurrent() / 1000f);// attack range
        WriteH(pgs.GetAttackSpeed().GetCurrent());// attack speed
        WriteH(pgs.GetEvasion().GetCurrent());// [current evasion]
        WriteH(pgs.GetParry().GetCurrent());// [current parry]
        WriteH(pgs.GetBlock().GetCurrent());// [current block]
        WriteH(pgs.GetMainHandPCritical().GetCurrent());// [current main hand crit rate]
        WriteH(pgs.GetOffHandPCritical().GetCurrent());// [current off hand crit rate]
        WriteH(pgs.GetMainHandPAccuracy().GetCurrent());// [current main_hand_accuracy]
        WriteH(pgs.GetOffHandPAccuracy().GetCurrent());// [current off_hand_accuracy]

        WriteH(1);// [unk]

        WriteH(pgs.GetMAccuracy().GetCurrent());// [current magic accuracy]
        WriteH(pgs.GetMCritical().GetCurrent());// [current crit spell]

        WriteH(0);// [unk]

        WriteF(pgs.GetReverseStat(StatEnum.BOOST_CASTING_TIME, 1000).GetCurrent() / 1000f);// [current casting speed]
        WriteH(0); // [unk 3.5]
        WriteH(pgs.GetStat(StatEnum.CONCENTRATION, 0).GetCurrent());// [current concetration]
        WriteH(pgs.GetMBoost().GetCurrent());// [current magic boost]
        WriteH(pgs.GetMBResist().GetCurrent());// [current magic suppression]
        WriteH(pgs.GetStat(StatEnum.HEAL_BOOST, 0).GetCurrent());// [current heal_boost]
        WriteH(0);// unk
        WriteH(pgs.GetPCR().GetCurrent()); // [current strike resist]
        WriteH(pgs.GetMCR().GetCurrent());// [current spell resist]
        WriteH(pgs.GetStat(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent());// [current strike fortitude]
        WriteH(pgs.GetStat(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent());// [current spell fortitude]
        WriteD(player.GetInventory().GetLimit());
        WriteD(player.GetInventory().Size());
        WriteD(0);// [unk]
        WriteD(0);// [unk]
        WriteD(pcd.GetPlayerClass().GetClassId());// [Player Class id]

        WriteH(0);// unk 3.0
        WriteH(0);// unk 3.0
        WriteH(0); // [unk 3.5]
        WriteH(0); // [unk 3.5]
        WriteQ(pcd.GetCurrentReposeEnergy());
        WriteQ(pcd.GetMaxReposeEnergy());
        WriteQ(pcd.GetCurrentSalvationPercent());

        WriteH(0); // 4.3 NA
        WriteH(0); // 4.3 NA
        WriteH(1); // 4.3 NA
        WriteH(0); // 4.3 NA
        WriteH(0); //4.8
        WriteH(0); //4.8
        WriteH(0); //4.8
        WriteH(0); //4.8
        WriteH(pgs.GetPower().GetBase());// [base power]
        WriteH(pgs.GetHealth().GetBase());// [base health]
        WriteH(pgs.GetAccuracy().GetBase());// [base accuracy]
        WriteH(pgs.GetAgility().GetBase());// [base agility]
        WriteH(pgs.GetKnowledge().GetBase());// [base knowledge]
        WriteH(pgs.GetWill().GetBase());// [base will]
        WriteH(pgs.GetStat(StatEnum.WATER_RESISTANCE, 0).GetBase());// [base water res]
        WriteH(pgs.GetStat(StatEnum.WIND_RESISTANCE, 0).GetBase());// [base water res]
        WriteH(pgs.GetStat(StatEnum.EARTH_RESISTANCE, 0).GetBase());// [base earth resist]
        WriteH(pgs.GetStat(StatEnum.FIRE_RESISTANCE, 0).GetBase());// [base water res]
        WriteH(pgs.GetStat(StatEnum.ELEMENTAL_RESISTANCE_LIGHT, 0).GetBase());// [base light resistance]
        WriteH(pgs.GetStat(StatEnum.ELEMENTAL_RESISTANCE_DARK, 0).GetBase());// [base dark resistance]
        WriteD(pgs.GetMaxHp().GetBase());// [base hp]
        WriteD(pgs.GetMaxMp().GetBase());// [base mana]
        WriteH(pgs.GetMaxDp().GetBase());// [base dp]
        WriteH(21592);// to do display_max_point
        WriteD(pgs.GetFlyTime().GetBase());// [fly time]
        WriteH(pgs.GetMainHandPAttack(CalculationType.DISPLAY).GetBase());// [base main hand attack]
        WriteH(pgs.GetOffHandPAttack(CalculationType.DISPLAY).GetBase());// [base off hand attack]
        WriteH(pgs.GetMainHandMAttack(CalculationType.DISPLAY).GetBase());// [current magic attack]
        WriteH(pgs.GetOffHandMAttack(CalculationType.DISPLAY).GetBase());
        WriteD(pgs.GetPDef().GetBase()); // [base pdef]
        WriteD(pgs.GetMDef().GetBase());// [base magic def]
        WriteH(pgs.GetMResist().GetBase());// [base magic res]
        WriteF(pgs.GetAttackRange().GetBase() / 1000f);// [base attack range]
        WriteH(0); // [unk 3.5]
        WriteH(pgs.GetEvasion().GetBase());// [base evasion]
        WriteH(pgs.GetParry().GetBase());// [base parry]
        WriteH(pgs.GetBlock().GetBase());// [base block]
        WriteH(pgs.GetMainHandPCritical().GetBase());// [base main hand crit rate]
        WriteH(pgs.GetOffHandPCritical().GetBase());// [base off hand crit rate]
        WriteH(pgs.GetMCritical().GetBase());// [base magical crit rate]

        WriteH(0);// [unk]
        WriteH(pgs.GetMainHandPAccuracy().GetBase());// [base main hand accuracy]
        WriteH(pgs.GetOffHandPAccuracy().GetBase());// [base off hand accuracy]

        WriteH(0);// [unk]
        WriteH(pgs.GetMAccuracy().GetBase());// [base magic accuracy]
        WriteH(pgs.GetStat(StatEnum.CONCENTRATION, 0).GetBase());// [base concentration]
        WriteH(pgs.GetMBoost().GetBase());// [base magic boost]
        WriteH(pgs.GetMBResist().GetBase()); // [base magic suppression]
        WriteH(pgs.GetStat(StatEnum.HEAL_BOOST, 0).GetBase());// [base healboost]
        WriteH(0);// unk
        WriteH(pgs.GetPCR().GetBase());// [base strike resist]
        WriteH(pgs.GetMCR().GetBase());// [base spell resist]
        WriteH(pgs.GetStat(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0).GetBase());// [base strike fortitude]
        WriteH(pgs.GetStat(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetBase());// [base spell fortitude]
    }
}
