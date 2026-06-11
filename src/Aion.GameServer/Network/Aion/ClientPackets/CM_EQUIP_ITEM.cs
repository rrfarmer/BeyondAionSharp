using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_EQUIP_ITEM (Avol, ATracer). Equip (0) / unequip (1) / switch-weapons (2); broadcasts appearance update on change. Equipment/PlayerRestrictions/SM_UPDATE_PLAYER_APPEARANCE red-tolerated.</summary>
public class CM_EQUIP_ITEM : AionClientPacket
{
    private long slotRead;
    private int itemObjId;
    private byte action;

    public CM_EQUIP_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadC(); // 0/1/2 = equip/unequip/switch weapons
        slotRead = ReadQ();
        itemObjId = ReadD();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();

        activePlayer.GetController().CancelUseItem();

        if (!PlayerRestrictions.CanChangeEquip(activePlayer))
            return;

        Equipment equipment = activePlayer.GetEquipment();
        Item resultItem = null;
        switch (action)
        {
            case 0:
                resultItem = equipment.EquipItem(itemObjId, slotRead);
                break;
            case 1:
                resultItem = equipment.UnEquipItem(itemObjId);
                if (resultItem == null)
                    PacketSendUtility.SendPacket(activePlayer, SM_SYSTEM_MESSAGE.STR_UI_INVENTORY_FULL());
                break;
            case 2:
                equipment.SwitchHands();
                break;
        }

        if (resultItem != null || action == 2)
            PacketSendUtility.BroadcastPacket(activePlayer,
                new SM_UPDATE_PLAYER_APPEARANCE(activePlayer.GetObjectId(), equipment.GetEquippedForAppearance()), true);
    }
}
