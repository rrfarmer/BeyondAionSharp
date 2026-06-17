using System.Collections.Generic;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Model;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_LEGION_MEMBERLIST (Yeats). GM variant of SM_LEGION_MEMBERLIST adding gender (via PlayerService) and omitting house fields. Overrides WriteLegionMember. PlayerService/LegionMember red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_GM_SHOW_LEGION_MEMBERLIST.java): 2026-06-17
// TIER-2 audit-only: writeLegionMember calls PlayerService.getOrLoadPlayerCommonData(objectId) (live PlayerService/DB-cache singleton),
// which the unit harness cannot bounded-build, so validated by line-by-line writeImpl comparison rather than a runtime golden.
// Confirmed IDENTICAL field set/order/encoding: writeD(objectId) writeS(name) writeC(playerClass.classId) writeC(gender.genderId via
// getOrLoadPlayerCommonData) writeD(level) writeC(rank.rankId) writeD(worldId) writeC(online?1:0) writeS(selfIntro) writeS(nickname)
// writeD(online?0:lastOnlineEpochSeconds). Only mechanical PascalCase renames. 0 fidelity discrepancies.
public class SM_GM_SHOW_LEGION_MEMBERLIST : SM_LEGION_MEMBERLIST
{
    public SM_GM_SHOW_LEGION_MEMBERLIST(List<LegionMember> legionMembers, bool isFirst, bool isLast)
        : base(legionMembers, isFirst, isLast)
    {
    }

    protected override void WriteLegionMember(LegionMember legionMember)
    {
        WriteD(legionMember.GetObjectId());
        WriteS(legionMember.GetName());
        WriteC(legionMember.GetPlayerClass().GetClassId());
        WriteC(PlayerService.GetOrLoadPlayerCommonData(legionMember.GetObjectId()).GetGender().GetGenderId());
        WriteD(legionMember.GetLevel());
        WriteC(legionMember.GetRank().GetRankId());
        WriteD(legionMember.GetWorldId());
        WriteC(legionMember.IsOnline() ? 1 : 0);
        WriteS(legionMember.GetSelfIntro());
        WriteS(legionMember.GetNickname());
        WriteD(legionMember.IsOnline() ? 0 : legionMember.GetLastOnlineEpochSeconds());
    }
}
