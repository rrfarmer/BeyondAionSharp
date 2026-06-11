using System.Collections.Generic;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.IdFactory;
using ItemDeleteType = Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType;
using PersistentState = Aion.GameServer.Model.GameObjects.Persistable.PersistentState;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_HOUSE_EDIT (Rolandas). House decoration/renovation editor (enter/exit modes, add/delete/spawn/move/despawn objects, switch building). HouseObject&lt;?&gt; -> &lt;PlaceableHouseObject&gt;. HousingService/HouseObjectFactory/SM_HOUSE_EDIT red-tolerated.</summary>
public class CM_HOUSE_EDIT : AionClientPacket
{
    private int action;
    private int itemObjectId;
    private float x, y, z;
    private int rotation;
    private int buildingId;

    public CM_HOUSE_EDIT(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        action = ReadUC();
        if (action == 3)
        {
            itemObjectId = ReadD();
        }
        else if (action == 4)
        {
            itemObjectId = ReadD();
        }
        else if (action == 5)
        {
            itemObjectId = ReadD();
            x = ReadF();
            y = ReadF();
            z = ReadF();
            rotation = ReadUH();
        }
        else if (action == 6)
        {
            itemObjectId = ReadD();
            x = ReadF();
            y = ReadF();
            z = ReadF();
            rotation = ReadUH();
        }
        else if (action == 7)
        {
            itemObjectId = ReadD();
        }
        else if (action == 16)
        {
            buildingId = ReadD();
        }
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (player == null)
            return;
        House house = player.GetActiveHouse();

        if (action == 1)
        { // Enter Decoration mode
            SendPacket(new SM_HOUSE_EDIT(action));
            SendPacket(new SM_HOUSE_REGISTRY(action));
            SendPacket(new SM_HOUSE_REGISTRY(action + 1));
        }
        else if (action == 2)
        { // Exit Decoration mode
            SendPacket(new SM_HOUSE_EDIT(action));
        }
        else if (action == 3)
        { // Add item
            Item item = player.GetInventory().GetItemByObjId(itemObjectId);
            if (item == null)
                return;

            ItemTemplate template = item.GetItemTemplate();
            player.GetInventory().Delete(item, ItemDeleteType.REGISTER);

            DecorateAction decorateAction = template.GetActions().GetDecorateAction();
            if (decorateAction != null)
            {
                HouseDecoration decor = new HouseDecoration(IDFactory.GetInstance().NextId(), decorateAction.GetTemplateId());
                house.GetRegistry().PutDecor(decor, true);
                SendPacket(new SM_HOUSE_EDIT(action, 2, decor.GetObjectId()));
            }
            else
            {
                HouseObject<PlaceableHouseObject> obj = HouseObjectFactory.CreateNew(house, template);
                house.GetRegistry().PutObject(obj, true);
                SendPacket(new SM_HOUSE_EDIT(action, 1, obj.GetObjectId()));
            }
        }
        else if (action == 4)
        { // Delete item
            house.GetRegistry().DiscardObject(house.GetRegistry().GetObjectByObjId(itemObjectId), false);
            SendPacket(new SM_HOUSE_EDIT(action, 1, itemObjectId));
            SendPacket(new SM_HOUSE_EDIT(4, 1, itemObjectId));
        }
        else if (action == 5)
        { // spawn object
            HouseObject<PlaceableHouseObject> obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            obj.SetX(x);
            obj.SetY(y);
            obj.SetZ(z);
            obj.SetRotation(rotation);
            SendPacket(new SM_HOUSE_EDIT(action, itemObjectId, x, y, z, rotation));
            obj.Spawn();
            house.GetRegistry().SetPersistentState(PersistentState.UPDATE_REQUIRED);
            SendPacket(new SM_HOUSE_EDIT(4, 1, itemObjectId));
            QuestEngine.GetInstance().OnHouseItemUseEvent(new QuestEnv(null, player, 0));
        }
        else if (action == 6)
        { // move object
            HouseObject<PlaceableHouseObject> obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            SendPacket(new SM_HOUSE_EDIT(action + 1, 0, itemObjectId));
            obj.GetController().Delete();
            obj.SetX(x);
            obj.SetY(y);
            obj.SetZ(z);
            obj.SetRotation(rotation);
            if (obj.GetPersistentState() == PersistentState.UPDATE_REQUIRED)
                house.GetRegistry().SetPersistentState(PersistentState.UPDATE_REQUIRED);
            SendPacket(new SM_HOUSE_EDIT(action - 1, itemObjectId, x, y, z, rotation));
            obj.Spawn();
        }
        else if (action == 7)
        { // despawn object
            HouseObject<PlaceableHouseObject> obj = house.GetRegistry().GetObjectByObjId(itemObjectId);
            if (obj == null)
                return;
            SendPacket(new SM_HOUSE_EDIT(action, 0, itemObjectId));
            obj.RemoveFromHouse();
            SendPacket(new SM_HOUSE_EDIT(3, 1, itemObjectId)); // place it back
        }
        else if (action == 14)
        { // enter renovation mode
            SendPacket(new SM_HOUSE_EDIT(14));
        }
        else if (action == 15)
        { // exit renovation mode
            SendPacket(new SM_HOUSE_EDIT(15));
        }
        else if (action == 16)
        {
            if (!RemoveRenovationCoupon(player, house))
            {
                AuditLogger.Log(player, "attempted house renovation without coupon");
                return;
            }
            HousingService.GetInstance().SwitchHouseBuilding(house, buildingId);
            house.GetController().UpdateAppearance();
        }
    }

    private bool RemoveRenovationCoupon(Player player, House house)
    {
        int typeId = house.GetHouseType().GetId();
        if (typeId == 0)
            return false; // studio
        int itemId = (player.GetRace().Equals(Race.ELYOS) ? 169661004 : 169661008) - typeId;
        if (player.GetInventory().GetItemCountByItemId(itemId) > 0)
            return player.GetInventory().DecreaseByItemId(itemId, 1);
        return false;
    }
}
