using System;
using System.Collections.Generic;
using System.Linq;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>
/// Java parity bridge: exposes the reworked-DTO shaped collections consumed by the player/item
/// persistence repository (PlayerEnterWorldRepository) on top of the faithful Player sub-objects.
/// These are read-only adapters over the faithful spine (GetSkillCoolDowns/GetItemCoolDowns/etc.),
/// keeping the existing DAO-style save helpers compiling without duplicating state.
/// </summary>
public partial class Player
{
    // Java parity: PlayerService.storePlayer -> PlayerCooldownsDAO.storePlayerCooldowns(player.getSkillCoolDowns()).
    public IReadOnlyDictionary<int, long> SkillCooldowns => GetSkillCoolDowns();

    // Java parity: ItemCooldownsDAO.storeItemCooldowns(player.getItemCoolDowns()).
    public IReadOnlyDictionary<int, Aion.GameServer.Model.GameObjects.PlayerItemCooldown> ItemCooldowns =>
        GetItemCoolDowns().ToDictionary(
            entry => entry.Key,
            entry => new Aion.GameServer.Model.GameObjects.PlayerItemCooldown(
                entry.Value.GetReuseTime(),
                entry.Value.GetUseDelay()));

    // Java parity: PortalCooldownsDAO.storePortalCooldowns(player.getPortalCooldownList()).
    public IReadOnlyDictionary<int, Aion.GameServer.Model.GameObjects.PlayerPortalCooldown> PortalCooldowns =>
        GetPortalCooldownList().GetPortalCoolDowns().ToDictionary(
            entry => entry.Key,
            entry => new Aion.GameServer.Model.GameObjects.PlayerPortalCooldown(
                entry.Value.GetWorldId(),
                entry.Value.GetReuseTime(),
                entry.Value.GetEnterCount()));

    // Java parity: CraftCooldownsDAO.storeCraftCooldowns(player.getCraftCooldowns()).
    public IReadOnlyDictionary<int, long> CraftCooldowns =>
        GetCraftCooldowns().ToDictionary(entry => entry.Key, entry => entry.Value);

    // Java parity: HouseObjectCooldownsDAO.storeHouseObjectCooldowns(player.getHouseObjectCooldowns()).
    public IReadOnlyDictionary<int, long> HouseObjectCooldowns =>
        GetHouseObjectCooldowns().ToDictionary(entry => entry.Key, entry => entry.Value);

    // Java parity: InventoryDAO.store(player) clears UPDATE_REQUIRED storage state after a flush.
    // The faithful storages already flip their persistent state inside GetDirtyItemsToUpdate(), so
    // this bridge is a no-op acknowledgement consumed by the repository's snapshot writers.
    public void MarkDirtyItemsPersisted()
    {
    }

    // Java parity: account passport state restored by AccountPassportsDAO.loadPassport(account).
    private IReadOnlyList<Aion.GameServer.Model.Account.Passport> _passports = Array.Empty<Aion.GameServer.Model.Account.Passport>();

    public IReadOnlyList<Aion.GameServer.Model.Account.Passport> Passports
    {
        get => _passports;
        set => _passports = value ?? Array.Empty<Aion.GameServer.Model.Account.Passport>();
    }

    public int PassportStamps { get; set; }

    public DateTime? LastPassportStamp { get; set; }

    // Java parity: PlayerQuestListDAO.store(player) iterates player.getQuestStateList().getAllQuestState().
    public IReadOnlyList<Aion.GameServer.Model.GameObjects.PlayerQuestState> Quests =>
        GetQuestStateList().GetAllQuestState()
            .Select(qs => new Aion.GameServer.Model.GameObjects.PlayerQuestState(
                qs.GetQuestId(),
                qs.GetStatus().ToString(),
                qs.GetQuestVars().GetQuestVars(),
                qs.GetFlags(),
                qs.GetCompleteCount(),
                qs.GetRewardGroup(),
                qs.GetNextRepeatTime().HasValue ? new DateTimeOffset(qs.GetNextRepeatTime()!.Value) : null,
                qs.GetLastCompleteTime().HasValue ? new DateTimeOffset(qs.GetLastCompleteTime()!.Value) : null))
            .ToList();

    // Java parity: PlayerEnterWorldService.GeneralUpdateTask saves player.getHouses().
    public IReadOnlyList<Aion.GameServer.Model.GameObjects.PlayerHouse> Houses =>
        GetHouses()
            .Select(BridgeHouse)
            .ToList();

    private static Aion.GameServer.Model.GameObjects.PlayerHouse BridgeHouse(Aion.GameServer.Model.House.House house) =>
        new Aion.GameServer.Model.GameObjects.PlayerHouse(
            ObjectId: house.GetObjectId(),
            AddressId: house.GetAddress().GetId(),
            BuildingId: house.GetBuilding().GetId(),
            AcquiredTime: house.GetAcquiredTime()?.UtcDateTime,
            NextPay: house.GetNextPay()?.UtcDateTime,
            IsInactive: house.IsInactive(),
            DoorState: (byte)(int)house.GetDoorState(),
            ShowOwnerName: house.IsShowOwnerName(),
            SignNotice: house.GetSignNotice(),
            TownLevel: house.GetTownLevel());
}
