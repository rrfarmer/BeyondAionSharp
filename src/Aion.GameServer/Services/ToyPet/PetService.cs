using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Templates.Items.Actions;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Model.Trade;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World.Zone;
using ForceType = Aion.GameServer.SkillEngine.Model.Effect.ForceType;
using ItemUpdateType = Aion.GameServer.Services.Items.ItemPacketService.ItemUpdateType;

namespace Aion.GameServer.Services.ToyPet;

/// <summary>Java parity: services/toypet/PetService (M@xx, IlBuono, xTz, Rolandas). Pet rename/feed/doping/loot/sell logic. PetFunctionType SCREAMING; PetSpecialFunction/EmotionType/PetHungryLevel PascalCase. TimeUnit.SECONDS delays converted to millis. Pet templates/SkillEngine/TradeService red-tolerated.</summary>
public class PetService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PetService));

    public static PetService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private PetService()
    {
    }

    public void RenamePet(Player player, string name)
    {
        name = Util.ConvertName(name);
        Pet pet = player.GetPet();
        if (pet != null)
        {
            pet.GetCommonData().SetName(name);
            PlayerPetsDAO.UpdatePetName(pet.GetCommonData());
            PacketSendUtility.BroadcastPacket(player, new SM_PET(pet.GetObjectId(), pet.GetName()), true);
        }
    }

    public void OnPlayerLogin(Player player)
    {
        ICollection<PetCommonData> playerPets = player.GetPetList().GetPets();
        if (playerPets.Count != 0)
            PacketSendUtility.SendPacket(player, new SM_PET(playerPets));
    }

    public void RemoveObject(int objectId, int count, Player player)
    {
        Item item = player.GetInventory().GetItemByObjId(objectId);
        if (item == null || player.GetPet() == null || count > item.GetItemCount())
            return;

        Pet pet = player.GetPet();
        pet.GetCommonData().SetCancelFeed(false);
        PacketSendUtility.SendPacket(player, new SM_PET(1, item.GetObjectId(), count, pet));
        PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.START_FEEDING, 0, player.GetObjectId()));

        Schedule(pet, player, item, count);
    }

    private void Schedule(Pet pet, Player player, Item item, int count)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!pet.GetCommonData().GetCancelFeed())
                CheckFeeding(pet, player, item, count);
        }, 2500);
    }

    private void CheckFeeding(Pet pet, Player player, Item item, int count)
    {
        PetCommonData commonData = pet.GetCommonData();
        PetFeedProgress progress = commonData.GetFeedProgress();

        if (!commonData.GetCancelFeed())
        {
            PetFunction func = pet.GetObjectTemplate().GetPetFunction(PetFunctionType.FOOD);
            PetFlavour flavour = DataManager.PET_FEED_DATA.GetFlavourById(func.GetId());
            FoodType? foodType = flavour.GetFoodType(item.GetItemId());

            if (flavour.IsLovedFood(foodType.Value, item.GetItemId()) && progress.GetLovedFoodRemaining() == 0)
                foodType = null;

            if (foodType == null)
            {
                // non eatable item
                ItemPacketService.SendItemUnlockPacket(player, item);
                PacketSendUtility.SendPacket(player, new SM_PET(5, 0, 0, pet));
                PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.END_FEEDING, 0, player.GetObjectId()));
                PacketSendUtility.SendPacket(player,
                    SM_SYSTEM_MESSAGE.STR_MSG_TOYPET_FEED_FOOD_NOT_LOVEFLAVOR(pet.GetName(), item.GetItemTemplate().GetL10n()));
                return;
            }
            player.GetInventory().DecreaseItemCount(item, 1, ItemUpdateType.DEC_PET_FOOD);
            PetFeedResult reward = flavour.ProcessFeedResult(progress, foodType.Value, item.GetItemTemplate().GetLevel(), player.GetCommonData().GetLevel());

            if (progress.GetHungryLevel() == PetHungryLevel.FULL && reward != null)
            {
                PacketSendUtility.SendPacket(player, new SM_PET(2, item.GetObjectId(), 0, pet));
                PacketSendUtility.SendPacket(player, new SM_PET(6, reward.GetItem(), 0, pet));
                PacketSendUtility.SendPacket(player, new SM_PET(5, 0, 0, pet));
                PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.END_FEEDING, 0, player.GetObjectId()));
                PacketSendUtility.SendPacket(player, new SM_PET(7, 0, 0, pet)); // 2151591961

                ItemService.AddItem(player, reward.GetItem(), 1);
                long delay = flavour.GetCooldDown() * 60000;
                commonData.ScheduleRefeed(delay);
                long refeedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + delay;
                commonData.SetRefeedTime(refeedTime);
                PlayerPetsDAO.SetTime(pet.GetObjectId(), refeedTime);
                progress.Reset();
            }
            else
            {
                PacketSendUtility.SendPacket(player, new SM_PET(2, item.GetObjectId(), --count, pet));
                if (count > 0)
                    Schedule(pet, player, item, count);
                else
                {
                    PacketSendUtility.SendPacket(player, new SM_PET(5, 0, 0, pet));
                    PacketSendUtility.SendPacket(player, new SM_EMOTION(player, EmotionType.END_FEEDING, 0, player.GetObjectId()));
                }
            }
        }
    }

    public void UseDoping(Pet pet, int action, int itemId, int slot, int slot2)
    {
        if (pet.GetCommonData().GetDopingBag() == null)
            return;

        Player player = pet.GetMaster();
        if (action < 2)
        { // add, replace or delete item
            if (!ValidateSetDopeItem(pet, itemId, slot))
                return;
            pet.GetCommonData().GetDopingBag().SetItem(itemId, slot);
            PacketSendUtility.SendPacket(player, new SM_PET(action, itemId, slot));
        }
        else if (action == 2)
        {
            pet.GetCommonData().GetDopingBag().SwitchItems(slot, slot2);
            PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(action, slot2, slot));
        }
        else if (action == 3)
        { // use item
            if (!player.IsSpawned())
            { // player may be just despawned because of a pending teleport, schedule re-check
                ThreadPoolManager.GetInstance().Schedule(() => PacketSendUtility.SendPacket(player, new SM_PET(action, itemId, slot)), 5000);
                return;
            }

            Item useItem = player.GetInventory().GetItemsByItemId(itemId)[0];

            if (!IsPetItemUseAllowed(player, useItem))
            { // pet currently is not allowed to buff, schedule re-check
                ThreadPoolManager.GetInstance().Schedule(() => PacketSendUtility.SendPacket(player, new SM_PET(action, itemId, slot)), 20000);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long reuseTime = player.GetItemReuseTime(useItem.GetItemTemplate().GetUseLimits().GetDelayId());
            if (reuseTime != 0 && reuseTime > now)
            { // player still has cooldown, schedule re-check
                ThreadPoolManager.GetInstance().Schedule(() => PacketSendUtility.SendPacket(player, new SM_PET(action, itemId, slot)), reuseTime - now);
                return;
            }

            foreach (AbstractItemAction itemAction in useItem.GetItemTemplate().GetActions().GetItemActions())
            {
                if (itemAction is SkillUseAction)
                {
                    PacketSendUtility.BroadcastPacket(player,
                        new SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), player.GetObjectId(), useItem.GetObjectId(), useItem.GetItemId(), 0, 1, 1, 1, 0, 15360),
                        true);
                    Aion.GameServer.SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(((SkillUseAction)itemAction).GetSkillId(), ((SkillUseAction)itemAction).GetLevel(), player,
                        player, null, ForceType.DEFAULT);
                    int useDelay = useItem.GetItemTemplate().GetUseLimits().GetDelayTime();
                    player.AddItemCoolDown(useItem.GetItemTemplate().GetUseLimits().GetDelayId(), now + useDelay, useDelay / 1000);
                    player.GetInventory().DecreaseByItemId(itemId, 1);
                }
                else
                    log.LogWarning("Pet attempt to use not skill use item");
            }
            PacketSendUtility.SendPacket(player, new SM_PET(action, itemId, slot));
        }
    }

    private bool ValidateSetDopeItem(Pet pet, int itemId, int slot)
    {
        PetFunction petFunction = pet.GetObjectTemplate().GetPetFunction(PetFunctionType.DOPING);
        if (petFunction == null)
        {
            AuditLogger.Log(pet.GetMaster(), "tried to set buff item " + itemId + " but " + pet + " doesn't support buffing");
            return false;
        }
        PetDopingEntry dope = DataManager.PET_DOPING_DATA.GetDopingTemplate(petFunction.GetId());
        if (slot == 0 && !dope.IsUseFood())
        {
            AuditLogger.Log(pet.GetMaster(), "tried to set item " + itemId + " in pet buff food slot but " + pet + " doesn't support buffing with food");
            return false;
        }
        if (slot == 1 && !dope.IsUseDrink())
        {
            AuditLogger.Log(pet.GetMaster(), "tried to set item " + itemId + " in pet buff drink slot but " + pet + " doesn't support buffing with drinks");
            return false;
        }
        if (slot > 1 && slot - 1 > dope.GetScrollsUsed())
        {
            AuditLogger.Log(pet.GetMaster(), "tried to set item " + itemId + " in pet buff scroll slot " + (slot - 1) + " but " + pet + " only supports "
                + dope.GetScrollsUsed() + " scrolls");
            return false;
        }
        return true;
    }

    private bool IsPetItemUseAllowed(Player player, Item item)
    {
        if (item.GetItemTemplate().HasAreaRestriction())
        {
            ZoneName restriction = item.GetItemTemplate().GetUseArea();
            if (restriction != null && !player.IsInsideItemUseZone(restriction))
            {
                return false;
            }
        }
        return true;
    }

    public void ActivateLoot(Pet pet, bool activate)
    {
        if (activate)
        {
            if (!pet.GetObjectTemplate().ContainsFunction(PetFunctionType.LOOT))
            {
                AuditLogger.Log(pet.GetMaster(), "tried to enable auto-loot on non-looting " + pet);
                return;
            }
            var team = pet.GetMaster().GetCurrentTeam();
            if (team != null && team.GetLootGroupRules().GetLootRule() == LootRuleType.FREEFORALL)
            {
                PacketSendUtility.SendPacket(pet.GetMaster(), SM_SYSTEM_MESSAGE.STR_MSG_LOOTING_PET_MESSAGE03());
                return;
            }
            PacketSendUtility.SendPacket(pet.GetMaster(), SM_SYSTEM_MESSAGE.STR_MSG_LOOTING_PET_MESSAGE01());
        }
        pet.GetCommonData().SetIsLooting(activate);
        PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(PetSpecialFunction.AUTOLOOT, activate));
    }

    public void ActivateAutoSell(Pet pet, bool activate)
    {
        if (activate && !pet.GetObjectTemplate().ContainsFunction(PetFunctionType.MERCHANT))
        {
            AuditLogger.Log(pet.GetMaster(), "tried to enable auto-sell on non-selling " + pet);
            return;
        }
        pet.GetCommonData().SetIsSelling(activate);
        PacketSendUtility.SendPacket(pet.GetMaster(), new SM_PET(PetSpecialFunction.AUTOSELL, activate));
    }

    public void Sell(Pet pet, List<Item> items)
    {
        if (pet == null || !pet.GetCommonData().IsSelling())
            return;
        PetFunction pf = pet.GetObjectTemplate().GetPetFunction(PetFunctionType.MERCHANT);
        if (pf != null)
        {
            TradeList tradeList = new TradeList(pet.GetObjectId());
            foreach (Item item in items)
                tradeList.AddItem(item.GetObjectId(), item.GetItemCount());
            if (tradeList.Size() > 0)
            {
                TradeService.PerformSellToShop(pet.GetMaster(), tradeList, null, pf.GetRatePrice());
                PacketSendUtility.SendPacket(pet.GetMaster(), SM_SYSTEM_MESSAGE.STR_MSG_MERCHANT_PET_GET_SELL_ITEM(pet.GetName()));
            }
        }
    }

    private static class SingletonHolder
    {
        internal static readonly PetService instance = new PetService();
    }
}
