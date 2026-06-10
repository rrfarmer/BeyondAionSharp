using System;
using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Templates.Item.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Restrictions;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_USE_ITEM (Avol, Neon). Uses an inventory item (with optional target item / house object / sync / return index) running its item actions. HouseObject&lt;?&gt; -> HouseObject&lt;PlaceableHouseObject&gt;; Collections.emptyList -> new List. QuestEngine/item actions red-tolerated.</summary>
public class CM_USE_ITEM : AionClientPacket
{
    private int uniqueItemId;
    private int targetItemId, syncId, indexReturn;

    public CM_USE_ITEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        uniqueItemId = ReadD();
        byte type = ReadC();
        switch (type)
        {
            case 2:
                targetItemId = ReadD();
                break;
            case 5: // instance cooltime reset scroll
                syncId = ReadD();
                break;
            case 6:
                indexReturn = ReadD();
                break;
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();

        if (player.IsProtectionActive())
            player.GetController().StopProtectionActiveTask();

        Item item = player.GetInventory().GetItemByObjId(uniqueItemId);
        if (item == null)
            return;

        Item targetItem = null;
        HouseObject<PlaceableHouseObject> targetHouseObject = null;
        if (targetItemId != 0)
        {
            targetItem = player.GetInventory().GetItemByObjId(targetItemId);
            if (targetItem == null)
                targetItem = player.GetEquipment().GetEquippedItemByObjId(targetItemId);
            if (targetItem == null && player.GetActiveHouse() != null)
                targetHouseObject = player.GetActiveHouse().GetRegistry().GetObjectByObjId(targetItemId);
        }

        // check use item multicast delay exploit cast (spam)
        if (player.IsCasting())
            player.GetController().CancelCurrentSkill(null);

        if (!PlayerRestrictions.CanUseItem(player, item))
            return;

        HandlerResult result = QuestEngine.GetInstance().OnItemUseEvent(new QuestEnv(null, player, 0), item);

        List<AbstractItemAction> itemActions = item.GetItemTemplate().GetActions() == null ? new List<AbstractItemAction>()
            : item.GetItemTemplate().GetActions().GetItemActions();

        if (itemActions.Count == 0 && result != HandlerResult.SUCCESS)
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_ITEM_IS_NOT_USABLE());
            return;
        }

        List<AbstractItemAction> actions = new List<AbstractItemAction>();
        foreach (AbstractItemAction itemAction in itemActions)
        {
            // check if the item can be used before placing it on the cooldown list.
            if (itemAction is DyeAction)
            {
                if (itemAction.CanAct(player, item, targetItem, targetHouseObject))
                    actions.Add(itemAction);
            }
            else if (itemAction is MultiReturnAction)
            {
                if (itemAction.CanAct(player, item, targetItem, indexReturn))
                    actions.Add(itemAction);
            }
            else if (itemAction is InstanceTimeClear)
            {
                if (itemAction.CanAct(player, item, targetItem, syncId))
                    actions.Add(itemAction);
            }
            else if (itemAction.CanAct(player, item, targetItem))
            {
                actions.Add(itemAction);
            }
        }

        if (actions.Count == 0)
            return; // notification should be handled in canAct

        int useDelay = item.GetItemTemplate().GetUseLimits().GetDelayTime();
        if (useDelay > 0)
            player.AddItemCoolDown(item.GetItemTemplate().GetUseLimits().GetDelayId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + useDelay, useDelay / 1000);

        // notify item use observer
        player.GetObserveController().NotifyItemuseObservers(item);

        foreach (AbstractItemAction itemAction in actions)
        {
            if (itemAction is DyeAction)
            {
                itemAction.Act(player, item, targetItem, targetHouseObject);
            }
            else if (itemAction is MultiReturnAction)
            {
                itemAction.Act(player, item, targetItem, indexReturn);
            }
            else if (itemAction is InstanceTimeClear)
            {
                itemAction.Act(player, item, targetItem, syncId);
            }
            else
            {
                itemAction.Act(player, item, targetItem);
            }
        }
    }
}
