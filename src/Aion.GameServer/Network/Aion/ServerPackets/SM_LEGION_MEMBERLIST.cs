using System.Collections.Generic;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEGION_MEMBERLIST (Simple). Paged legion member list; WriteLegionMember is virtual so SM_GM_SHOW_LEGION_MEMBERLIST can override. isLast negates the size. LegionMember/House/HousingService/NetworkConfig red-tolerated.</summary>
public class SM_LEGION_MEMBERLIST : AionServerPacket
{
    private readonly bool isFirst, isLast;
    private readonly List<LegionMember> legionMembers;

    public SM_LEGION_MEMBERLIST(List<LegionMember> legionMembers, bool isFirst, bool isLast)
    {
        this.legionMembers = legionMembers;
        this.isFirst = isFirst;
        this.isLast = isLast;
    }

    protected override void WriteImpl(AionConnection con)
    {
        int size = legionMembers.Count;
        WriteC(isFirst ? 1 : 0);
        WriteH(isLast ? -size : size);
        foreach (LegionMember legionMember in legionMembers)
            WriteLegionMember(legionMember);
    }

    protected virtual void WriteLegionMember(LegionMember legionMember)
    {
        WriteD(legionMember.GetObjectId());
        WriteS(legionMember.GetName());
        WriteC(legionMember.GetPlayerClass().GetClassId());
        WriteD(legionMember.GetLevel());
        WriteC(legionMember.GetRank().GetRankId());
        WriteD(legionMember.GetWorldId());
        WriteC(legionMember.IsOnline() ? 1 : 0);
        WriteS(legionMember.GetSelfIntro());
        WriteS(legionMember.GetNickname());
        WriteD(legionMember.IsOnline() ? 0 : legionMember.GetLastOnlineEpochSeconds());
        House house = HousingService.GetInstance().FindActiveHouse(legionMember.GetObjectId());
        WriteD(house == null ? 0 : house.GetAddress().GetId());
        WriteD(house == null ? 0 : house.GetDoorState().GetId());
        WriteD(NetworkConfig.GAMESERVER_ID); // displays server number for each away player in region field
    }
}
