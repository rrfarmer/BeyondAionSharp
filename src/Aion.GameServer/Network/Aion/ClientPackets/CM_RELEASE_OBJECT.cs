using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RELEASE_OBJECT (Rolandas, Neon). Releases a useable house object the player occupies (postbox always notifies). UseableHouseObject&lt;?&gt; -> &lt;PlaceableHouseObject&gt;. SM_USE_OBJECT red-tolerated.</summary>
public class CM_RELEASE_OBJECT : AionClientPacket
{
    int targetObjectId;

    public CM_RELEASE_OBJECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        targetObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        VisibleObject object_ = player.GetKnownList().GetObject(targetObjectId);
        if (object_ is UseableHouseObject<PlaceableHouseObject> useableHouseObject && useableHouseObject.ReleaseOccupant(player))
        { // release object
            if (player.GetController().HasScheduledTask(TaskId.HOUSE_OBJECT_USE) || object_ is PostboxObject)
            { // post box always sends the message
                if (object_ is UseableItemObject) // reset visual use progress bar
                    PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), object_.GetObjectId(), 0, 9));
                SendPacket(SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_CANCEL_USE());
            }
            player.GetController().CancelTask(TaskId.HOUSE_OBJECT_USE);
        }
    }
}
