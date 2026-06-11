using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Items.Storage;

/// <summary>Java parity: model/items/storage/PlayerStorage extends Storage.</summary>
public class PlayerStorage : Storage
{
    private Aion.GameServer.Model.GameObjects.Players.Player actor;

    public PlayerStorage(Aion.GameServer.Model.GameObjects.Players.Player owner, StorageType storageType) : base(storageType)
    {
        this.actor = owner;
    }

    public sealed override void SetOwner(Aion.GameServer.Model.GameObjects.Players.Player actor)
    {
        this.actor = actor;
    }

    public override void OnLoadHandler(Item item)
    {
        if (item.IsEquipped())
            actor.GetEquipment().OnLoadHandler(item);
        else
        {
            base.OnLoadHandler(item);
        }
    }

    public override void IncreaseKinah(long amount)
    {
        IncreaseKinah(amount, actor);
    }

    public override void IncreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        IncreaseKinah(amount, updateType, actor);
    }

    public override bool TryDecreaseKinah(long amount)
    {
        return TryDecreaseKinah(amount, actor);
    }

    public override bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return TryDecreaseKinah(amount, updateType, actor);
    }

    public override void DecreaseKinah(long amount)
    {
        DecreaseKinah(amount, actor);
    }

    public override void DecreaseKinah(long amount, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        DecreaseKinah(amount, updateType, actor);
    }

    public override long IncreaseItemCount(Item item, long count)
    {
        return IncreaseItemCount(item, count, actor);
    }

    public override long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return IncreaseItemCount(item, count, updateType, actor);
    }

    public override long DecreaseItemCount(Item item, long count)
    {
        return DecreaseItemCount(item, count, actor);
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return DecreaseItemCount(item, count, updateType, actor);
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        return DecreaseItemCount(item, count, updateType, questStatus, actor);
    }

    public override Item Add(Item item)
    {
        return Add(item, actor);
    }

    public override Item Add(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemAddType addType)
    {
        return Add(item, addType, actor);
    }

    public override Item Put(Item item)
    {
        return Put(item, actor);
    }

    public override Item Delete(Item item)
    {
        return Delete(item, actor);
    }

    public override Item Delete(Item item, Aion.GameServer.Services.Item.ItemPacketService.ItemDeleteType deleteType)
    {
        return Delete(item, deleteType, actor);
    }

    public override bool DecreaseByItemId(int itemId, long count)
    {
        return DecreaseByItemId(itemId, count, actor);
    }

    public override bool DecreaseByItemId(int itemId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        return DecreaseByItemId(itemId, count, questStatus, actor);
    }

    public override bool DecreaseByObjectId(int itemObjId, long count)
    {
        return DecreaseByObjectId(itemObjId, count, actor);
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Questengine.Model.QuestStatus questStatus)
    {
        return DecreaseByObjectId(itemObjId, count, questStatus, actor);
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Item.ItemPacketService.ItemUpdateType updateType)
    {
        return DecreaseByObjectId(itemObjId, count, updateType, actor);
    }
}
