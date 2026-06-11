using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using Status = Aion.GameServer.Model.GameObjects.Players.FriendList.Status;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_FRIEND_LIST (Ben, Neon). Sends the player's friend list (id/name/level/class/gender/map/online-or-lastonline/note/status + active house address+door + memo). Converges PlayerEnterWorldService. FriendList/Friend/House/HousingService/AionServerPacket red-tolerated.</summary>
public class SM_FRIEND_LIST : AionServerPacket
{
    protected override void WriteImpl(AionConnection con)
    {
        FriendList list = con.GetActivePlayer().GetFriendList();

        WriteH(-list.GetSize());
        WriteC(0);// unk

        foreach (Friend friend in list)
        {
            WriteD(friend.GetObjectId());
            WriteS(friend.GetName());
            WriteD(friend.GetLevel());
            WriteD(friend.GetPlayerClass().GetClassId());
            WriteC(friend.GetGender().GetGenderId());
            WriteD(friend.GetMapId());
            WriteD(friend.GetStatus() == Status.ONLINE ? 0 : friend.GetLastOnlineEpochSeconds());
            WriteS(friend.GetNote()); // Friend note
            WriteC(friend.GetStatus().GetId());

            House house = HousingService.GetInstance().FindActiveHouse(friend.GetObjectId());
            WriteD(house == null ? 0 : house.GetAddress().GetId());
            WriteC(house == null ? 0 : house.GetDoorState().GetId());

            WriteS(friend.GetFriendMemo());
        }
    }
}
