using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/PostboxObject (Rolandas).</summary>
public class PostboxObject : UseableHouseObject<HousingPostbox>
{
    public PostboxObject(HouseRegistry registry, int objId, int templateId)
        : base(registry, objId, templateId)
    {
    }

    public override void OnUse(Player player)
    {
        if (!SetOccupant(player))
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.STR_MSG_HOUSING_OBJECT_OCCUPIED_BY_OTHER());
            return;
        }

        player.GetMailbox().mailBoxState = PlayerMailboxState.REGULAR;
        PacketSendUtility.SendPacket(player, SmSystemMessage.STR_MSG_HOUSING_OBJECT_USE(GetObjectTemplate().GetL10n()));
        PacketSendUtility.SendPacket(player, new SM_DIALOG_WINDOW(GetObjectId(), DialogPage.MAIL.Id()));
        PacketSendUtility.SendPacket(player, new SM_OBJECT_USE_UPDATE(player.GetObjectId(), 0, 0, this));
    }
}
