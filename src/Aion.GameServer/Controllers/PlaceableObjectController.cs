using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/PlaceableObjectController (Rolandas) : VisibleObjectController&lt;HouseObject&lt;T&gt;&gt;. **Java `instanceof UseableHouseObject&lt;?&gt;` wildcard→`is UseableHouseObject&lt;T&gt;`** (class type param T:PlaceableHouseObject matches — sidesteps the HouseObject wildcard issue). PositionUtil/SM_SYSTEM_MESSAGE red-tolerated.</summary>
public class PlaceableObjectController<T> : VisibleObjectController<HouseObject<T>> where T : PlaceableHouseObject
{
    public override void OnDespawn()
    {
        base.OnDespawn();
        GetOwner().OnDespawn();
    }

    public void OnDialogRequest(Player player)
    {
        if (!PositionUtil.IsInTalkRange(player, GetOwner()))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_TOO_FAR_TO_USE());
            return;
        }
        GetOwner().OnDialogRequest(player);
    }

    public override void NotKnow(VisibleObject obj)
    {
        base.NotKnow(obj);
        if (GetOwner() is UseableHouseObject<T> useableHouseObject && obj is Player player)
            useableHouseObject.ReleaseOccupant(player);
    }
}
