using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers;

/// <summary>
/// Java parity: controllers/PlaceableObjectController (Rolandas) : VisibleObjectController&lt;HouseObject&lt;T&gt;&gt;.
/// C# generic-invariance break: the owner is the NON-GENERIC <see cref="HouseObject"/> (Java wildcard
/// <c>HouseObject&lt;?&gt;</c>), and Java's <c>instanceof UseableHouseObject&lt;?&gt;</c> wildcard becomes
/// <c>is UseableHouseObject</c> (non-generic base). PositionUtil/SM_SYSTEM_MESSAGE red-tolerated.
/// </summary>
public class PlaceableObjectController : VisibleObjectController<HouseObject>
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
        if (GetOwner() is UseableHouseObject useableHouseObject && obj is Player player)
            useableHouseObject.ReleaseOccupant(player);
    }
}
