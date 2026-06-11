using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/AbstractHouseInfoPacket (Neon). Base for house info/render packets: WriteCommonInfo writes address/owner/building/door/sign/decor registry + legion emblem. PartType is a real C# enum (values()->Enum.GetValues, getRooms()->GetRooms ext); Integer decorId->int?; CHARNAME_MAX_LENGTH from AbstractPlayerInfoPacket. House/LegionMember/LegionService red-tolerated.</summary>
public abstract class AbstractHouseInfoPacket : AionServerPacket
{
    public const int SIGN_NOTICE_MAX_LENGTH = 64;
    protected readonly House house;

    protected AbstractHouseInfoPacket(House house)
    {
        this.house = house;
    }

    protected void WriteCommonInfo()
    {
        LegionMember member = house.IsInactive() || house.GetOwnerId() == 0 ? null : LegionService.GetInstance().GetLegionMember(house.GetOwnerId());

        WriteD(0);
        WriteD(house.GetAddress().GetId());
        WriteD(house.GetOwnerId());
        WriteD(house.GetBuilding().GetType_().GetId());
        WriteC(1); // unk

        WriteD(house.GetBuilding().GetId());
        WriteC(house.GetHouseOwnerStates());
        WriteC(house.GetDoorState().GetId());

        WriteS(house.GetOwnerName(), AbstractPlayerInfoPacket.CHARNAME_MAX_LENGTH);

        WriteD(member == null ? 0 : member.GetLegion().GetLegionId());

        WriteC(house.IsShowOwnerName() ? 1 : 0);
        WriteS(house.GetSignNotice(), SIGN_NOTICE_MAX_LENGTH); // client can display much longer strings but then decor won't show

        foreach (PartType partType in System.Enum.GetValues<PartType>())
        {
            for (int roomNo = 0; roomNo < partType.GetRooms(); roomNo++)
            {
                int? decorId = house.GetRegistry().GetUsedDecorId(partType, roomNo);
                WriteD(decorId == null ? 0 : decorId.Value);
            }
        }
        WriteD(0);
        WriteD(0);
        WriteC(0); // show legion flags near house door: 0 = none, 1 = left, 2 = right (1+2 = both)
        // Emblem and color
        if (member == null || member.GetLegion().GetLegionEmblem() == null)
        {
            WriteB(new byte[6]);
        }
        else
        {
            LegionEmblem emblem = member.GetLegion().GetLegionEmblem();
            WriteC(emblem.GetEmblemId());
            WriteC(emblem.GetEmblemType().GetValue());
            WriteC(emblem.GetColor_a());
            WriteC(emblem.GetColor_r());
            WriteC(emblem.GetColor_g());
            WriteC(emblem.GetColor_b());
        }
    }
}
