using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MANASTONE (ATracer, Wakizashi). Enchant/manastone/godstone/amplify socket actions on items. EnchantService/StigmaService/ItemSocketService/EnchantItemAction red-tolerated.</summary>
public class CM_MANASTONE : AionClientPacket
{
    private int npcObjId;
    private int slotNum;
    private int actionType;
    private int targetFusedSlot;
    private int stoneUniqueId;
    private int targetItemUniqueId;
    private int supplementUniqueId;

    public CM_MANASTONE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        actionType = ReadUC();
        targetFusedSlot = ReadUC();
        targetItemUniqueId = ReadD();
        switch (actionType)
        {
            case 1:
            case 2:
            case 4:
            case 8:
                stoneUniqueId = ReadD();
                supplementUniqueId = ReadD();
                break;
            case 3:
                slotNum = ReadUC();
                ReadC();
                ReadH();
                npcObjId = ReadD();
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        switch (actionType)
        {
            case 1: // enchant stone
            case 2: // add manastone
                Item stone = player.GetInventory().GetItemByObjId(stoneUniqueId);
                if (stone == null)
                    return;
                Item targetItem = player.GetEquipment().GetEquippedItemByObjId(targetItemUniqueId);
                if (targetItem == null && (targetItem = player.GetInventory().GetItemByObjId(targetItemUniqueId)) == null)
                {
                    SendPacket(actionType == 1 ? SM_SYSTEM_MESSAGE.STR_ENCHANT_ITEM_NO_TARGET_ITEM() : SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_OPTION_NO_TARGET_ITEM());
                    return;
                }

                if (stone.GetItemTemplate().IsStigma() && targetItem.GetItemTemplate().IsStigma())
                {
                    StigmaService.ChargeStigma(player, targetItem, stone);
                }
                else
                {
                    EnchantItemAction action = new EnchantItemAction();
                    if (action.CanAct(player, stone, targetItem))
                    {
                        Item supplement = player.GetInventory().GetItemByObjId(supplementUniqueId);
                        if (supplement != null)
                        {
                            if (supplement.GetItemId() / 100000 != 1661) // suppliment id check
                                return;
                        }
                        action.Act(player, stone, targetItem, supplement, targetFusedSlot);
                    }
                }
                break;
            case 3: // remove manastone
                VisibleObject visibleObject = player.GetTarget();
                if (visibleObject.GetObjectId() == npcObjId && visibleObject is Npc npc && PositionUtil.IsInTalkRange(player, npc))
                    ItemSocketService.RemoveManastone(player, targetItemUniqueId, slotNum, targetFusedSlot != 1);
                break;
            case 4: // add godstone
                Item weaponItem = player.GetInventory().GetItemByObjId(targetItemUniqueId);
                if (weaponItem == null)
                {
                    bool isEquipped = player.GetEquipment().GetEquippedItemByObjId(targetItemUniqueId) != null;
                    SendPacket(isEquipped ? SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_CANNOT_GIVE_PROC_TO_EQUIPPED_ITEM() : SM_SYSTEM_MESSAGE.STR_GIVE_ITEM_PROC_NO_TARGET_ITEM());
                    return;
                }
                ItemSocketService.SocketGodstone(player, weaponItem, stoneUniqueId);
                break;
            case 8: // amplification
                EnchantService.AmplifyItem(player, targetItemUniqueId, supplementUniqueId, stoneUniqueId);
                break;
        }
    }
}
