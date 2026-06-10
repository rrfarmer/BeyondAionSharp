using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SET_NOTE (Ben). Sets the player's note and refreshes friend lists + broadcasts SM_UPDATE_NOTE. World/SM_FRIEND_LIST/SM_UPDATE_NOTE red-tolerated.</summary>
public class CM_SET_NOTE : AionClientPacket
{
    private string note;

    public CM_SET_NOTE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        note = ReadS();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (note.Equals(player.GetCommonData().GetNote()))
            return;
        player.GetCommonData().SetNote(note);
        foreach (Friend friend in player.GetFriendList())
        {
            Player friendPlayer = World.GetInstance().GetPlayer(friend.GetObjectId());
            if (friendPlayer != null)
                PacketSendUtility.SendPacket(friendPlayer, new SM_FRIEND_LIST());
        }
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_UPDATE_NOTE(player));
    }
}
