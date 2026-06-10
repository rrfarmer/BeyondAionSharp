using System;
using System.Threading.Tasks;
using Aion.Commons.Nio;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Housing;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Time;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>
/// Java parity: model/gameobjects/UseableItemObject (Rolandas, Neon) : UseableHouseObject&lt;HousingUseableItem&gt;.
/// Integer→int? (useCount/requiredItem/cd/checkType/reward/removeCount); ByteBuffer→Commons.Nio.ByteBuffer; nested
/// PacketWriteHelper subclass UseDataWriter; anonymous Runnable→Schedule(ct=>{...;ValueTask.CompletedTask;},TimeSpan);
/// requiredItem^removeCount XOR; ServerTime.now().with(LocalTime.MAX).toEpochSecond()*1000→server-tz end-of-day
/// (23:59:59) from DateTimeOffset; currentTimeMillis→UtcNow.ToUnixTimeMilliseconds. PacketWriteHelper/ServerTime/
/// UseableHouseObject-base/SM_* red-tolerated.
/// </summary>
public class UseableItemObject : UseableHouseObject<HousingUseableItem>
{
    private static readonly ILogger log = NullLogger.Instance;

    private volatile bool mustGiveLastReward = false;
    private readonly UseDataWriter entryWriter;

    public UseableItemObject(HouseRegistry registry, int objId, int templateId) : base(registry, objId, templateId)
    {
        UseItemAction action = GetObjectTemplate().GetAction();
        if (action != null && action.GetFinalRewardId() != null && IsExpired())
            mustGiveLastReward = true;
        entryWriter = new UseDataWriter(this);
    }

    private class UseDataWriter : PacketWriteHelper
    {
        internal UseableItemObject obj;

        public UseDataWriter(UseableItemObject obj)
        {
            this.obj = obj;
        }

        protected override void WriteMe(ByteBuffer buffer)
        {
            WriteD(buffer, obj.GetObjectTemplate().GetUseCount() == null ? 0 : obj.GetOwnerUsedCount() + obj.GetVisitorUsedCount());
            UseItemAction action = obj.GetObjectTemplate().GetAction();
            WriteC(buffer, action == null || action.GetCheckType() == null ? 0 : action.GetCheckType().Value);
        }
    }

    public override void OnUse(Player player)
    {
        UseItemAction action = GetObjectTemplate().GetAction();
        if (action == null) // Some objects do not have actions; they are test items now
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_ALL_CANT_USE());
            return;
        }

        int ownerId = GetOwnerHouse().GetOwnerId();
        bool isOwner = ownerId == player.GetObjectId();
        if (!isOwner && GetObjectTemplate().IsOwnerOnly())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_IS_ONLY_FOR_OWNER_VALID());
            return;
        }

        if (player.GetHouseObjectCooldowns().HasCooldown(GetObjectId()))
        {
            if (GetObjectTemplate().GetCd() != null && GetObjectTemplate().GetCd() > 0)
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_CANNOT_USE_FLOWERPOT_COOLTIME());
            else
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_CANT_USE_PER_DAY());
            return;
        }

        int? useCount = GetObjectTemplate().GetUseCount();
        int currentUseCount = 0;
        if (useCount != null)
        {
            // Counter is for both, but could be made custom from configs
            currentUseCount = GetOwnerUsedCount() + GetVisitorUsedCount();
            if (currentUseCount >= useCount && !isOwner || currentUseCount > useCount && isOwner)
            {
                // if expiration is set then final reward has to be given for owner only due to inventory full. If inventory was not full, the object had to
                // be despawned, so we wouldn't reach this check.
                if (!mustGiveLastReward || !isOwner)
                {
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_ACHIEVE_USE_COUNT());
                    return;
                }
            }
        }

        if (mustGiveLastReward && !isOwner) // expired, wait for owner
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_DELETE_EXPIRE_TIME(GetObjectTemplate().GetL10n()));
            return;
        }

        if (GetObjectTemplate().GetPlacementLimit() == LimitType.COOKING)
        {
            // Check if player already has an item
            if (player.GetInventory().GetItemCountByItemId(action.GetRewardId().Value) > 0)
            {
                string rewardL10n = DataManager.ITEM_DATA.GetItemTemplate(action.GetRewardId().Value).GetL10n();
                PacketSendUtility.SendPacket(player,
                    SM_SYSTEM_MESSAGE.STR_MSG_CANNOT_USE_ALREADY_HAVE_REWARD_ITEM(rewardL10n, GetObjectTemplate().GetL10n()));
                return;
            }
        }

        int? requiredItem = GetObjectTemplate().GetRequiredItem();
        if (requiredItem != null)
        {
            if (action.GetCheckType() == 1) // equip item needed
            {
                if (player.GetEquipment().GetEquippedItemsByItemId(requiredItem.Value).Count == 0)
                {
                    string requiredItemL10n = DataManager.ITEM_DATA.GetItemTemplate(requiredItem.Value).GetL10n();
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_USE_HOUSE_OBJECT_ITEM_EQUIP(requiredItemL10n));
                    return;
                }
            }
            else if (player.GetInventory().GetItemCountByItemId(requiredItem.Value) < action.GetRemoveCount())
            {
                string requiredItemL10n = DataManager.ITEM_DATA.GetItemTemplate(requiredItem.Value).GetL10n();
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_CANT_USE_HOUSE_OBJECT_ITEM_CHECK(requiredItemL10n));
                return;
            }
        }

        if ((requiredItem != null) ^ (action.GetRemoveCount() != null))
        {
            log.LogWarning(this + " doesn't have valid usage requirements " + (requiredItem == null ? " (item missing)" : "(remove count missing)"));
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_ALL_CANT_USE());
            return;
        }

        if (player.GetInventory().IsFull())
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_WAREHOUSE_TOO_MANY_ITEMS_INVENTORY());
            return;
        }

        if (!SetOccupant(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_OCCUPIED_BY_OTHER());
            return;
        }

        int delayMs = GetObjectTemplate().GetDelay();
        int usedCount = useCount == null ? 0 : currentUseCount + 1;
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_USE(GetObjectTemplate().GetL10n()));
        PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), delayMs, 8));
        player.GetController().AddTask(TaskId.HOUSE_OBJECT_USE, ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            PacketSendUtility.SendPacket(player, new SM_USE_OBJECT(player.GetObjectId(), GetObjectId(), 0, 9));
            if (requiredItem != null && action.GetRemoveCount() != null && action.GetRemoveCount() > 0)
            {
                if (!player.GetInventory().DecreaseByItemId(requiredItem.Value, action.GetRemoveCount().Value))
                    return ValueTask.CompletedTask;
            }

            int rewardId = 0;
            bool delete = false;

            if (useCount != null)
            {
                if (action.GetFinalRewardId() != null && useCount.Value + 1 == usedCount)
                {
                    // visitors do not get final rewards
                    rewardId = action.GetFinalRewardId().Value;
                    delete = true;
                }
                else if (action.GetRewardId() != null)
                {
                    rewardId = action.GetRewardId().Value;
                    if (useCount.Value == usedCount)
                    {
                        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_FLOWERPOT_GOAL(GetObjectTemplate().GetL10n()));
                        if (action.GetFinalRewardId() == null)
                        {
                            delete = true;
                        }
                        else
                        {
                            SetMustGiveLastReward(true);
                            SetExpireTime((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000));
                            SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
                        }
                    }
                }
            }
            else if (action.GetRewardId() != null)
            {
                rewardId = action.GetRewardId().Value;
            }
            if (usedCount > 0)
            {
                if (!delete)
                    if (isOwner)
                        IncrementOwnerUsedCount();
                    else
                        IncrementVisitorUsedCount();
            }
            if (rewardId > 0)
            {
                ItemService.AddItem(player, rewardId, 1);
                string rewardL10n = DataManager.ITEM_DATA.GetItemTemplate(rewardId).GetL10n();
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_HOUSING_OBJECT_REWARD_ITEM(GetObjectTemplate().GetL10n(), rewardL10n));
            }
            PacketSendUtility.BroadcastPacket(player, new SM_OBJECT_USE_UPDATE(player.GetObjectId(), ownerId, usedCount, this), true);
            if (delete)
                DespawnAndRemoveHouseObject(player, false);
            else
            {
                long reuseTime;
                int? cd = GetObjectTemplate().GetCd();
                if (cd == null || cd == 0) // use once per day (cooldown ends at midnight)
                {
                    DateTimeOffset now = ServerTime.Now();
                    reuseTime = new DateTimeOffset(now.Year, now.Month, now.Day, 23, 59, 59, now.Offset).ToUnixTimeSeconds() * 1000;
                }
                else
                    reuseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + cd.Value * 1000;
                player.GetHouseObjectCooldowns().Put(GetObjectId(), reuseTime);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(delayMs)));
    }

    public void SetMustGiveLastReward(bool mustGiveLastReward)
    {
        this.mustGiveLastReward = mustGiveLastReward;
    }

    public override bool CanExpireNow()
    {
        return !mustGiveLastReward && !IsOccupied();
    }

    public void WriteUsageData(ByteBuffer buffer)
    {
        entryWriter.WriteMe(buffer);
    }

    public override bool HasUseCooldown()
    {
        return GetObjectTemplate().GetCd() != null && GetObjectTemplate().GetCd() > 0;
    }
}
