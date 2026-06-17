using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/StorageObject (Rolandas).</summary>
public class StorageObject : UseableHouseObject<HousingStorage>
{
    public StorageObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }

    public override void OnUse(Player player)
    {
        if (player.GetObjectId() != GetOwnerHouse().GetOwnerId())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_IS_ONLY_FOR_OWNER_VALID());
            return;
        }

        if (!SetOccupant(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_OCCUPIED_BY_OTHER());
            return;
        }

        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_USE(GetObjectTemplate().GetL10n()));
        PacketSendUtility.SendPacket(player, new SM_OBJECT_USE_UPDATE(player.GetObjectId(), 0, 0, this));
    }
}
