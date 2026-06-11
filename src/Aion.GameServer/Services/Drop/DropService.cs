using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Actions;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Toypet;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using static Aion.GameServer.Services.Items.ItemService;
using Status = Aion.GameServer.Network.Aion.Serverpackets.SM_LOOT_STATUS.Status;

namespace Aion.GameServer.Services.Drop;

/// <summary>Java parity: services/drop/DropService (ATracer, xTz). Singleton; loot lifecycle — scheduleFreeForAll (240s), unregisterDrop, requestDropList/closeDropList, canDistribute/canAutoLoot, requestDropItem (kinah equal-split, team distribution roll/bid/misc, winning actions), distributeEqually, resend/announce. Idioms: schedule(lambda,240000)->async delegate; map.get/remove->GetValueOrDefault/TryRemove; synchronized(dropItems)->lock; instanceof Npc npc->is; streams->LINQ; ScheduledFuture+getDelay(MILLISECONDS) / TimeUnit red-tolerated; broadcastPacket predicate lambdas; nested TempTradeDropPredicate:ItemUpdatePredicate; currentTimeMillis->UtcNow.ToUnixTimeMilliseconds; switch-arrow dist 2/3. DropNpc/LootGroupRules/DAO red-tolerated.</summary>
public class DropService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(DropService));

    public static DropService GetInstance()
    {
        return SingletonHolder.instance;
    }

    public void ScheduleFreeForAll(int npcUniqueId)
    {
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcUniqueId);
            if (dropNpc != null)
            {
                DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcUniqueId).StartFreeForAll();
                VisibleObject visibleObject = World.World.GetInstance().FindVisibleObject(npcUniqueId);
                if (visibleObject != null && visibleObject.IsSpawned())
                {
                    // fix for elyos/asmodians being able to loot elyos/asmodian npcs
                    // TODO there might be more npcs who are friendly towards players and should not be loot able by them
                    if (visibleObject is Npc npc && npc.GetRace().IsAsmoOrEly())
                    {
                        PacketSendUtility.BroadcastPacket(npc, new SM_LOOT_STATUS(npcUniqueId, Status.LOOT_ENABLE), p => npc.GetRace() != p.GetRace());
                    }
                    else
                    {
                        PacketSendUtility.BroadcastPacket(visibleObject, new SM_LOOT_STATUS(npcUniqueId, Status.LOOT_ENABLE));
                    }
                }
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(240000));
    }

    /// <summary>After NPC despawns</summary>
    public void UnregisterDrop(Npc npc)
    {
        int npcObjId = npc.GetObjectId();
        DropRegistrationService.GetInstance().GetCurrentDropMap().TryRemove(npcObjId, out _);
        DropRegistrationService.GetInstance().GetDropRegistrationMap().TryRemove(npcObjId, out _);
    }

    /// <summary>When player clicks on dead NPC to request drop list</summary>
    public void RequestDropList(Player player, int npcObjectId)
    {
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjectId);
        if (player == null || dropNpc == null)
        {
            return;
        }

        if (player.IsLooting())
            CloseDropList(player, player.GetLootingNpcOid());

        if (!dropNpc.IsAllowedToLoot(player))
        {
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_LOOT_NO_RIGHT());
            return;
        }

        if (dropNpc.IsBeingLooted())
        {
            if (!dropNpc.GetLootingPlayer().IsOnline())
            {
                log.LogWarning(
                    dropNpc.GetLootingPlayer() + " is offline but was still set as drop looter for " + World.World.GetInstance().FindVisibleObject(npcObjectId));
            }
            else
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_LOOT_FAIL_ONLOOTING());
                return;
            }
        }

        dropNpc.SetLootingPlayer(player);
        VisibleObject visObj = World.World.GetInstance().FindVisibleObject(npcObjectId);
        if (visObj is Npc npc)
        {
            ScheduledTask decayTask = (ScheduledTask)npc.GetController().CancelTask(TaskId.DECAY);
            if (decayTask != null)
            {
                long remaingDecayTime = decayTask.GetDelay(TimeUnit.MILLISECONDS);
                dropNpc.SetRemaingDecayTime(remaingDecayTime);
            }
        }

        HashSet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npcObjectId);

        if (dropItems == null)
        {
            dropItems = new HashSet<DropItem>();
        }

        PacketSendUtility.SendPacket(player, new SM_LOOT_ITEMLIST(dropNpc, dropItems, player));
        PacketSendUtility.SendPacket(player, new SM_LOOT_STATUS(npcObjectId, Status.OPEN_DROP_LIST));
        player.UnsetState(CreatureState.ACTIVE);
        player.SetState(CreatureState.LOOTING);
        player.SetLootingNpcOid(npcObjectId);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.START_LOOT, 0, npcObjectId), true);
    }

    /// <summary>This method will change looted corpse to not in use</summary>
    public void CloseDropList(Player player, int npcObjectId)
    {
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjectId);

        player.UnsetState(CreatureState.LOOTING);
        player.SetState(CreatureState.ACTIVE);
        player.SetLootingNpcOid(0);
        PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_LOOT, 0, npcObjectId), true);

        if (dropNpc == null)
            return;

        if (!player.Equals(dropNpc.GetLootingPlayer()))
            return; // cheater :)

        HashSet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npcObjectId);
        dropNpc.SetLootingPlayer(null);

        Npc npc = (Npc)World.World.GetInstance().FindVisibleObject(npcObjectId);
        if (npc != null)
        {
            if (dropItems == null || dropItems.Count == 0)
            {
                npc.GetController().Delete();
                return;
            }

            RespawnService.ScheduleDecayTask(npc, dropNpc.GetRemaingDecayTime());

            LootGroupRules lootGroupRules = dropNpc.GetLootGroupRules();
            if (lootGroupRules != null && dropNpc.GetInRangePlayers().Count > 1 && dropNpc.GetAllowedLooters().Count == 1)
            {
                LootRuleType lrt = lootGroupRules.GetLootRule();
                if (lrt != LootRuleType.FREEFORALL)
                {
                    foreach (Player member in dropNpc.GetInRangePlayers())
                    {
                        if (member != null)
                            dropNpc.SetAllowedLooter(member);
                    }
                    foreach (DropItem dropItem in dropItems)
                    {
                        if (!dropItem.GetDropTemplate().IsEachMember())
                            dropItem.GetPlayerObjIds().Clear();
                    }
                }
            }
            PacketSendUtility.BroadcastPacket(npc, new SM_LOOT_STATUS(npcObjectId, Status.LOOT_ENABLE), dropNpc.IsAllowedToLoot);
        }
    }

    public bool CanDistribute(Player player, DropItem requestedItem)
    {
        int npcId = requestedItem.GetNpcObj();
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcId);
        if (dropNpc == null)
        {
            return false;
        }

        LootGroupRules lootGroupRules = dropNpc.GetLootGroupRules();
        if (lootGroupRules == null)
        {
            return true;
        }

        int itemId = requestedItem.GetDropTemplate().GetItemId();
        if (itemId != ItemId.KINAH)
        {
            if (dropNpc.GetInRangePlayers().Count > 1)
            {
                ItemQuality quality = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetItemQuality();
                dropNpc.SetDistributionId(lootGroupRules.GetAutodistributionId());
                dropNpc.SetDistributionType(lootGroupRules.GetQualityRule(quality));
            }
            else
                dropNpc.SetDistributionId(0);
            if (dropNpc.GetDistributionId() > 1 && dropNpc.GetDistributionType())
            {
                bool containDropItem = lootGroupRules.ContainDropItem(requestedItem);
                if (lootGroupRules.GetItemsToBeDistributed().Count == 0 || containDropItem)
                {
                    dropNpc.SetCurrentIndex(requestedItem.GetIndex());
                    foreach (Player member in dropNpc.GetInRangePlayers())
                    {
                        Player finalPlayer = World.World.GetInstance().GetPlayer(member.GetObjectId());
                        if (finalPlayer != null && finalPlayer.IsOnline())
                        {
                            dropNpc.AddPlayerStatus(finalPlayer);
                            finalPlayer.SetPlayerMode(PlayerMode.IN_ROLL, new InRoll(npcId, itemId, requestedItem.GetIndex(), dropNpc.GetDistributionId()));
                            PacketSendUtility.SendPacket(finalPlayer, new SM_GROUP_LOOT(dropNpc.GetLootingTeamId(), 0, itemId, (int)requestedItem.GetCount(),
                                npcId, dropNpc.GetDistributionId(), 1, requestedItem.GetIndex()));
                        }
                    }
                    lootGroupRules.SetPlayersInRoll(dropNpc.GetInRangePlayers(), dropNpc.GetDistributionId() == 2 ? 17000 : 32000, requestedItem.GetIndex(),
                        npcId);
                }
                else
                {
                    PacketSendUtility.SendPacket(player,
                        SM_SYSTEM_MESSAGE.STR_MSG_LOOT_ALREADY_DISTRIBUTING_ITEM(DataManager.ITEM_DATA.GetItemTemplate(itemId).GetL10n()));
                }
                if (!containDropItem)
                {
                    lootGroupRules.AddItemToBeDistributed(requestedItem);
                }
                return false;
            }
        }
        return true;
    }

    public bool CanAutoLoot(Player player, DropItem requestedItem)
    {
        int npcId = requestedItem.GetNpcObj();
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcId);
        if (dropNpc == null)
        {
            return false;
        }
        LootGroupRules lootGroupRules = dropNpc.GetLootGroupRules();
        if (lootGroupRules == null)
        {
            return true;
        }

        int itemId = requestedItem.GetDropTemplate().GetItemId();
        if (itemId == ItemId.KINAH)
            return true;

        int distId = lootGroupRules.GetAutodistributionId();
        if (dropNpc.GetInRangePlayers().Count <= 1)
        {
            distId = 0;
            dropNpc.SetDistributionId(distId);
        }

        ItemQuality quality = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetItemQuality();
        if (distId > 1 && lootGroupRules.GetQualityRule(quality))
        {
            bool anyOnline = false;
            foreach (Player member in dropNpc.GetInRangePlayers())
            {
                Player finalPlayer = World.World.GetInstance().GetPlayer(member.GetObjectId());
                if (finalPlayer != null && finalPlayer.IsOnline())
                {
                    anyOnline = true;
                    break;
                }
            }
            return !anyOnline;
        }
        return true;
    }

    public void RequestDropItem(Player player, int npcObjectId, int itemIndex)
    {
        RequestDropItem(player, npcObjectId, itemIndex, false);
    }

    public void RequestDropItem(Player player, int npcObjectId, int itemIndex, bool autoLoot)
    {
        HashSet<DropItem> dropItems = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(npcObjectId);
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjectId);
        DropItem requestedItem = null;
        // drop was unregistered
        if (dropItems == null || dropNpc == null)
        {
            return;
        }

        lock (dropItems)
        {
            foreach (DropItem dropItem in dropItems)
                if (dropItem.GetIndex() == itemIndex)
                {
                    requestedItem = dropItem;
                    break;
                }
        }

        if (requestedItem == null) // lag can cause drops to be displayed long enough for the client to send multiple loot requests when spamming 'C'
            return;

        // fix exploit
        if (!requestedItem.IsDistributeItem() && !dropNpc.IsAllowedToLoot(player))
        {
            return;
        }

        int itemId = requestedItem.GetDropTemplate().GetItemId();
        ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(itemId);
        if (template.HasLimitOne())
        {
            if (player.GetInventory().GetFirstItemByItemId(itemId) != null
                || player.GetStorage(StorageType.REGULAR_WAREHOUSE.GetId()).GetFirstItemByItemId(itemId) != null)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_CAN_NOT_GET_LORE_ITEM(template.GetL10n()));
                return;
            }
        }

        LootGroupRules lootGroupRules = dropNpc.GetLootGroupRules();
        if (lootGroupRules != null && !requestedItem.IsDistributeItem() && !requestedItem.IsFreeForAll())
        {
            if (lootGroupRules.ContainDropItem(requestedItem))
            {
                if (!autoLoot)
                    PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LOOT_ALREADY_DISTRIBUTING_ITEM(template.GetL10n()));
                return;
            }

            if (autoLoot && !CanAutoLoot(player, requestedItem))
                return;

            requestedItem.SetNpcObj(npcObjectId);
            if (!CanDistribute(player, requestedItem))
            {
                return;
            }
        }

        long initialCount = requestedItem.GetCount();
        // Kinah is distributed to all group/alliance members nearby.
        if (itemId == ItemId.KINAH)
        {
            var team = player.GetCurrentTeam();
            if (team == null)
            {
                requestedItem.SetCount(ItemService.AddItem(player, itemId, requestedItem.GetCount()));
            }
            else
            {
                List<Player> entitledPlayers = team
                    .FilterMembers(m => m.IsOnline() && !m.IsDead() && !m.IsMentor() && PositionUtil.IsInRange(m, player, GroupConfig.GROUP_MAX_DISTANCE));
                DistributeEqually(requestedItem, entitledPlayers);
            }
        }
        else if (!player.IsInTeam() && !requestedItem.IsItemWonNotCollected() && dropNpc.GetDistributionId() == 0)
        {
            requestedItem.SetCount(ItemService.AddItem(player, itemId, requestedItem.GetCount()));
        }
        else if (!requestedItem.IsDistributeItem())
        {
            if (lootGroupRules != null)
            {
                ItemQuality quality = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetItemQuality();
                if (lootGroupRules.IsMisc(quality))
                {
                    ICollection<Player> members = dropNpc.GetInRangePlayers();

                    if (members.Count > lootGroupRules.GetNrMisc())
                    {
                        lootGroupRules.SetNrMisc(lootGroupRules.GetNrMisc() + 1);
                    }
                    else
                    {
                        lootGroupRules.SetNrMisc(1);
                    }

                    int i = 0;
                    foreach (Player p in members)
                    {
                        i++;
                        if (i == lootGroupRules.GetNrMisc())
                        {
                            requestedItem.SetWinningPlayer(p);
                            break;
                        }
                    }
                }
                else
                {
                    requestedItem.SetWinningPlayer(player);
                }
            }
            else if (requestedItem.GetWinningPlayer() == null)
            {
                requestedItem.SetWinningPlayer(player);
            }

            if (requestedItem.GetWinningPlayer() != null)
            {
                requestedItem.SetCount(ItemService.AddItem(requestedItem.GetWinningPlayer(), itemId, requestedItem.GetCount(), false, new TempTradeDropPredicate(dropNpc)));

                WinningNormalActions(player, dropNpc, requestedItem);
            }
        }
        else if (!autoLoot && requestedItem.IsDistributeItem())
        { // handles distribution of item to correct player and messages accordingly
            if (!player.Equals(requestedItem.GetWinningPlayer()) && requestedItem.IsItemWonNotCollected())
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LOOT_ANOTHER_OWNER_ITEM());
                return;
            }
            else if (requestedItem.GetWinningPlayer().GetInventory().IsFull(template.GetExtraInventoryId()))
            {
                PacketSendUtility.SendPacket(requestedItem.GetWinningPlayer(), SM_SYSTEM_MESSAGE.STR_MSG_DICE_INVEN_ERROR());
                requestedItem.IsItemWonNotCollected(true);
                return;
            }

            requestedItem.SetCount(ItemService.AddItem(requestedItem.GetWinningPlayer(), itemId, requestedItem.GetCount(), false, new TempTradeDropPredicate(dropNpc)));

            switch (dropNpc.GetDistributionId())
            {
                case 2:
                    WinningRollActions(requestedItem.GetWinningPlayer(), itemId, npcObjectId);
                    break;
                case 3:
                    WinningBidActions(requestedItem.GetWinningPlayer(), npcObjectId, requestedItem.GetHighestValue());
                    break;
            }
        }

        if (requestedItem.GetCount() <= 0)
        {
            lock (dropItems)
            {
                dropItems.Remove(requestedItem);
            }
        }
        if (requestedItem.GetCount() < initialCount)
        {
            AnnounceDrop(requestedItem.GetWinningPlayer() != null ? requestedItem.GetWinningPlayer() : player, template);
            Pet pet = player.GetPet();
            if (pet != null && pet.GetCommonData().IsSelling())
            {
                List<Item> stacks = player.GetInventory().GetItemsByItemId(requestedItem.GetDropTemplate().GetItemId());
                if (stacks.Any(item => item.IsSellable() && item.GetItemTemplate().GetItemQuality() == ItemQuality.JUNK))
                {
                    PetService.GetInstance().Sell(pet, stacks);
                }
            }
        }

        if (!autoLoot)
            ResendDropList(dropNpc.GetLootingPlayer(), npcObjectId, dropNpc, dropItems);
    }

    private static void DistributeEqually(DropItem item, List<Player> players)
    {
        if (players.Count == 0)
            return;
        long countPerPlayer = item.GetCount() / players.Count;
        for (int i = players.Count - 1; i >= 0; i--)
        {
            long count = i == 0 ? item.GetCount() : countPerPlayer;
            long remainingCount = ItemService.AddItem(players[i], item.GetDropTemplate().GetItemId(), count);
            item.SetCount(item.GetCount() - count + remainingCount);
        }
    }

    private void ResendDropList(Player player, int npcObjectId, DropNpc dropNpc, HashSet<DropItem> dropItems)
    {
        Npc npc = (Npc)World.World.GetInstance().FindVisibleObject(npcObjectId);
        if (dropItems.Count != 0)
        {
            if (player != null)
            {
                PacketSendUtility.SendPacket(player, new SM_LOOT_ITEMLIST(dropNpc, dropItems, player));
            }
        }
        else
        {
            if (player != null)
            {
                PacketSendUtility.SendPacket(player, new SM_LOOT_STATUS(npcObjectId, Status.CLOSE_DROP_LIST));
                player.UnsetState(CreatureState.LOOTING);
                player.SetState(CreatureState.ACTIVE);
                PacketSendUtility.BroadcastPacket(player, new SM_EMOTION(player, EmotionType.END_LOOT, 0, npcObjectId), true);
            }
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    private void WinningRollActions(Player player, int itemId, int npcObjectId)
    {
        string itemL10n = DataManager.ITEM_DATA.GetItemTemplate(itemId).GetL10n();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_LOOT_GET_ITEM_ME(itemL10n));

        if (player.IsInTeam())
        {
            foreach (Player member in DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjectId).GetInRangePlayers())
            {
                if (member != null && !player.Equals(member))
                {
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_LOOT_GET_ITEM_OTHER(player.GetName(), itemL10n));
                }
            }
        }
    }

    private void WinningBidActions(Player player, int npcObjectId, long highestValue)
    {
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npcObjectId);
        if (highestValue > 0)
        {
            if (!player.GetInventory().TryDecreaseKinah(highestValue))
            {
                return;
            }
            PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_PAY_ACCOUNT_ME(highestValue));
        }

        List<Player> onlineMembers = dropNpc.GetInRangePlayers().Where(p => p.IsOnline() && !p.Equals(player)).ToList();
        foreach (Player member in onlineMembers)
        {
            PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_PAY_ACCOUNT_OTHER(player.GetName(), highestValue));
            long distributeKinah = highestValue / onlineMembers.Count;
            member.GetInventory().IncreaseKinah(distributeKinah);
            PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_PAY_DISTRIBUTE(highestValue, onlineMembers.Count, distributeKinah));
        }
    }

    private void WinningNormalActions(Player player, DropNpc dropNpc, DropItem requestedItem)
    {
        if (player == null || dropNpc == null)
            return;

        int itemId = requestedItem.GetDropTemplate().GetItemId();
        if (player.IsInTeam())
        {
            foreach (Player member in dropNpc.GetInRangePlayers())
            {
                if (member != null && !requestedItem.GetWinningPlayer().Equals(member) && member.IsOnline())
                    PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_GET_ITEM_PARTYNOTICE(requestedItem.GetWinningPlayer().GetName(),
                        DataManager.ITEM_DATA.GetItemTemplate(itemId).GetL10n()));
            }
        }
    }

    public void See(Player player, Npc npc)
    {
        if (!npc.IsDead())
            return;
        DropNpc dropNpc = DropRegistrationService.GetInstance().GetDropRegistrationMap().GetValueOrDefault(npc.GetObjectId());
        if (dropNpc != null && dropNpc.IsAllowedToLoot(player))
        {
            PacketSendUtility.SendPacket(player, new SM_LOOT_STATUS(npc.GetObjectId(), Status.LOOT_ENABLE));
        }
    }

    private void AnnounceDrop(Player player, ItemTemplate template)
    {
        if (DropConfig.MIN_ANNOUNCE_QUALITY == null || player.IsInInstance())
            return;
        if (template.GetItemQuality().GetQualityId() < DropConfig.MIN_ANNOUNCE_QUALITY.GetQualityId())
            return;
        PacketSendUtility.BroadcastToMap(player, SM_SYSTEM_MESSAGE.STR_FORCE_ITEM_WIN(player.GetName(), ChatUtil.Item(template.GetTemplateId())), 0,
            p => !p.Equals(player) && p.GetRace() == player.GetRace());
    }

    private sealed class TempTradeDropPredicate : ItemUpdatePredicate
    {
        private readonly DropNpc dropNpc;

        public TempTradeDropPredicate(DropNpc dropNpc)
        {
            this.dropNpc = dropNpc;
        }

        public override bool ChangeItem(Item input)
        {
            if (dropNpc.GetAllowedLooters().Count > 1)
            {
                ItemTemplate template = input.GetItemTemplate();
                if (template.GetTempExchangeTime() != 0)
                {
                    input.SetTemporaryExchangeTime((int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000) + (template.GetTempExchangeTime() * 60));
                    TemporaryTradeTimeTask.GetInstance().AddTask(input, dropNpc.GetAllowedLooters());
                }
                return true;
            }
            return false;
        }
    }

    private static class SingletonHolder
    {
        internal static readonly DropService instance = new DropService();
    }
}
