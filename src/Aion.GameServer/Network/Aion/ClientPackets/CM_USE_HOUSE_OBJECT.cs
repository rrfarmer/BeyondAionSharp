using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;
using Aion.GameServer.Model.Templates.Housing;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_USE_HOUSE_OBJECT (Rolandas). Triggers a house object's dialog. HouseObject&lt;?&gt; -> &lt;PlaceableHouseObject&gt;. World red-tolerated.</summary>
public class CM_USE_HOUSE_OBJECT : AionClientPacket
{
    int itemObjectId;

    public CM_USE_HOUSE_OBJECT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        itemObjectId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;

        VisibleObject visObject = global::Aion.GameServer.World.World.GetInstance().FindVisibleObject(itemObjectId);
        if (visObject == null)
            return;
        if (visObject is HouseObject<PlaceableHouseObject>)
        {
            ((HouseObject<PlaceableHouseObject>)visObject).GetController().OnDialogRequest(player);
        }
    }
}
