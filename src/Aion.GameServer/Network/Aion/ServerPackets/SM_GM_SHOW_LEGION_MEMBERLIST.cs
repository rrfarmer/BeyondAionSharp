using System.Collections.Generic;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Services.Player;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GM_SHOW_LEGION_MEMBERLIST (Yeats). GM variant of SM_LEGION_MEMBERLIST adding gender (via PlayerService) and omitting house fields. Overrides WriteLegionMember. PlayerService/LegionMember red-tolerated.</summary>
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
