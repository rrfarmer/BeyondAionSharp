using System;
using System.Threading;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items.Storage;

namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionWarehouse extends Storage.</summary>
public class LegionWarehouse : Storage
{
    private const int DEFAULT_ROWS = 3; // hardcoded, as the client doesn't allow to change it
    private const int SLOTS_PER_ROW = 8;
    private int currentUser; // Java parity: AtomicInteger via Interlocked

    public LegionWarehouse(Legion legion) : base(StorageType.LEGION_WAREHOUSE)
    {
        UpdateLimit(legion.GetWarehouseExpansions());
    }

    /// <summary>Used to add kinah from successful sieges. StorageProxy should be normally used to act with.</summary>
    public override void IncreaseKinah(long amount)
    {
        int currentWhUser = GetCurrentUser();
        Aion.GameServer.Model.GameObjects.Players.Player player = currentWhUser == 0 ? null : Aion.GameServer.World.World.GetInstance().GetPlayer(currentWhUser);
        new LegionStorageProxy(this, player).IncreaseKinah(amount);
    }

    public override void IncreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool TryDecreaseKinah(long amount)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool TryDecreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override void DecreaseKinah(long amount)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override void DecreaseKinah(long amount, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override long IncreaseItemCount(Item item, long count)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override long IncreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override long DecreaseItemCount(Item item, long count)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override long DecreaseItemCount(Item item, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override Item Add(Item item)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override Item Add(Item item, Aion.GameServer.Services.Items.ItemPacketService.ItemAddType addType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override Item Put(Item item)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override Item Delete(Item item)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override Item Delete(Item item, Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType deleteType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool DecreaseByItemId(int itemId, long count)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool DecreaseByItemId(int itemId, long count, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool DecreaseByObjectId(int itemObjId, long count)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType updateType)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override bool DecreaseByObjectId(int itemObjId, long count, Aion.GameServer.QuestEngine.Model.QuestStatus questStatus)
    {
        throw new NotSupportedException("LWH should be used behind proxy");
    }

    public override void SetOwner(Aion.GameServer.Model.GameObjects.Players.Player player)
    {
        throw new NotSupportedException("LWH doesnt have owner");
    }

    public bool UnsetInUse(int playerObjId)
    {
        return Interlocked.CompareExchange(ref currentUser, 0, playerObjId) == playerObjId;
    }

    public bool SetInUse(int playerObjId)
    {
        return Interlocked.CompareExchange(ref currentUser, playerObjId, 0) == 0;
    }

    public int GetCurrentUser()
    {
        return Volatile.Read(ref currentUser);
    }

    public override void SetLimit(int limit)
    {
        throw new NotSupportedException("Slot limit is controlled by the expansion level, use updateLimit() instead");
    }

    public void UpdateLimit(int warehouseExpansions)
    {
        if (warehouseExpansions < 0)
            throw new ArgumentException();
        int rows = DEFAULT_ROWS + warehouseExpansions;
        base.SetLimit(rows * SLOTS_PER_ROW);
    }
}
