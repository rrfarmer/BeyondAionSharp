using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/items/storage/Storage.persistentState and model/gameobjects/player/Equipment.persistentState.
public enum StoragePersistentState
{
	Updated,
	UpdateRequired,
}

public sealed class Player
{
	public const byte MailboxClosedState = 0;
	public const byte MailboxRegularState = 1;
	public const byte MailboxExpressState = 2;
	public const float DefaultBoundRadius = 0.25f;

	public int ObjectId { get; init; }

	public int AccountId { get; init; }

	// Java parity: model/gameobjects/player/PlayerAccount.accessLevel used by AdminService.hasAccess checks.
	public byte AccessLevel { get; set; }

	// Java parity: model/account/PlayerAccount.membership consumed by chat/player-info packets.
	public byte AccountMembership { get; set; }

	// Java parity: model/account/Account.creationDate is supplied by the login server in
	// SM_ACCOUNT_AUTH_RESPONSE and later consumed by FactionPackService/VeteranRewardService.
	public long? AccountCreationEpochMillis { get; set; }

	// Java parity: model/team/legion/LegionMember data used by chat/player info packets.
	public int LegionId { get; set; }

	// Java parity: Player.getLegion().getLegionLevel() used by DialogService BUY goods-list filtering.
	public int LegionLevel { get; set; }

	public string LegionName { get; set; } = string.Empty;

	public byte LegionEmblemId { get; set; }

	public byte LegionEmblemType { get; set; }

	public byte LegionEmblemColorA { get; set; }

	public byte LegionEmblemColorR { get; set; }

	public byte LegionEmblemColorG { get; set; }

	public byte LegionEmblemColorB { get; set; }

	public string Name { get; init; } = string.Empty;

	public string PlayerClass { get; init; } = string.Empty;

	public string Race { get; init; } = string.Empty;

	public string Gender { get; init; } = string.Empty;

	public string Note { get; set; } = string.Empty;

	public CharacterAppearance Appearance { get; set; } = new();

	// Java parity: model/gameobjects/player/Player.getLevel consumed by portal and action guards.
	public int Level { get; set; } = 1;

	// Java parity: model/gameobjects/player/Player.isMentor consumed by portal and quest/drop guards.
	public bool IsMentor { get; set; }

	// Java parity: model/gameobjects/player/Player.lookingForGroup toggled by CM_PLAYER_STATUS_INFO GROUP_SET_LFG.
	public bool IsLookingForGroup { get; set; }

	public long Exp { get; set; }

	public long RecoverableExp { get; set; }

	public int Dp { get; set; }

	// Java parity: model/gameobjects/player/Player.setPlayerResActivate cleared by PlayerReviveService.revive.
	public bool IsPlayerResurrectionActive { get; set; }

	// Java parity: model/gameobjects/player/Player.resurrectionSkill reset by PlayerReviveService.revive.
	public int ResurrectionSkillId { get; set; }

	// Java parity: model/gameobjects/player/Player.isInResPostState cleared by Player.unsetResPosState after revive routing.
	public bool IsInResurrectionPositionState { get; set; }

	public float ResurrectionPositionX { get; set; }

	public float ResurrectionPositionY { get; set; }

	public float ResurrectionPositionZ { get; set; }

	public long ReposeEnergy { get; init; }

	public bool IsOnline { get; set; }

	public DateTime? LastOnline { get; set; }

	public int NpcExpands { get; set; }

	public int QuestExpands { get; init; }

	public int ItemExpands { get; set; }

	public int WarehouseNpcExpands { get; set; }

	public int WarehouseBonusExpands { get; set; }

	public int TitleId { get; set; }

	public int BonusTitleId { get; set; }

	public WorldPosition Position { get; set; }

	// Java parity: model/account/PlayerAccountData.updateBoundingRadius sets
	// PlayerCommonData front/side bound radius to 0.25f; PositionUtil uses max(front, side).
	public float BoundRadius { get; set; } = DefaultBoundRadius;

	// Java parity: model/gameobjects/player/Player.portAnimation is written by SM_PLAYER_INFO and reset after spawn/level-ready fanout.
	public ArrivalAnimation PortAnimation { get; set; } = ArrivalAnimation.None;

	// Java parity: controllers/movement/PlayerMoveController state mirrored for CM_MOVE/SM_MOVE.
	public PlayerMovementState Movement { get; } = new();

	// Java parity: model/gameobjects/Creature.state queried through Creature.isInState(CreatureState).
	public PlayerCreatureState CreatureState { get; set; }

	// Java parity: model/gameobjects/player/Player.flyState queried by Player.isFlying/isInFlyingState.
	public PlayerFlyState FlyState { get; set; }

	// Java parity: model/gameobjects/player/Player.isFlyingBeforeDeath controls PlayerController.onBeforeSpawn state cleanup.
	public bool IsFlyingBeforeDeath { get; set; }

	// Java parity: controllers/effect/EffectController.abnormals queried by CM_EMOTION and action guards.
	public PlayerAbnormalState AbnormalState { get; set; }

	// Java parity: skillengine/model/Effect.isNoResurrectPenalty queried by PlayerReviveService.revive.
	public bool HasNoResurrectPenaltyEffect { get; set; }

	// Java parity: model/gameobjects/Creature.visualState drives hide/protection visibility packets.
	public int VisualState { get; set; } = PlayerVisualStates.Visible;

	// Java parity: model/gameobjects/Creature.seeState written by SM_PLAYER_STATE.
	public int SeeState { get; set; }

	// Java parity: model/actions/PlayerMode.RIDE queried through Player.isInPlayerMode.
	public bool IsInRideMode { get; set; }

	// Java parity: model/gameobjects/player/Player.ride stores the active RideInfo while mounted.
	public PlayerRideInfo? RideInfo { get; set; }

	// Java parity: model/gameobjects/player/Player.isInSprintMode toggled by CM_EMOTION START_SPRINT/END_SPRINT.
	public bool IsInSprintMode { get; set; }

	// Java parity: model/gameobjects/player/Player.flyReuseTime used by FlyController start/glide cooldown checks.
	public long FlyReuseTimeMillis { get; set; }

	// Java parity: model/templates/zone/ZoneType.FLY membership queried by FlyController.canFly.
	public bool IsInsideFlyZone { get; set; }

	// Java parity: model/templates/zone/ZoneType.NO_FLY membership queried by FlyController.canFly.
	public bool IsInsideNoFlyZone { get; set; }

	// Java parity: model/gameobjects/player/Player.getTransformModel().getRes6() == 1 blocks fly/glide.
	public bool TransformForbidsFlight { get; set; }

	// Java parity: model/gameobjects/player/Player.getTransformModel().getBanMovement() == 1 blocks movement.
	public bool TransformBansMovement { get; set; }

	// Java parity: model/gameobjects/player/Player.flightPath stores active transporter/windstream mode.
	public PlayerFlightPathType? FlightPathType { get; set; }

	// Java parity: PlayerLifeStats.triggerFpReduce/triggerFpRestore task intent; timer execution is a later Phase 6 slice.
	public bool IsFpReduceActive { get; set; }

	public bool IsFpRestoreActive { get; set; }

	// Java parity: controllers/PlayerController.stanceObserver represented by its active stance skill id.
	public int StanceSkillId { get; set; }

	// Java parity: model/gameobjects/VisibleObject target set by network/aion/clientpackets/CM_TARGET_SELECT.
	public int TargetObjectId { get; set; }

	// Java parity: model/gameobjects/player/Player.createAggroList returns PlayerAggroList,
	// whose clear path is invoked by PlayerReviveService.revive.
	public PlayerOwnedAggroList AggroList { get; } = new();

	// Java parity: model/gameobjects/player/Player.isTrading guard used by mail and broker packets.
	public bool IsTrading { get; set; }

	// Java parity: services/ExchangeService.getCurrentExchange(player).isLocked represented without the full Exchange basket model.
	public bool IsExchangeLocked { get; set; }

	// Java parity: services/ExchangeService.getCurrentExchange(player).isConfirmed represented without the full Exchange basket model.
	public bool IsExchangeConfirmed { get; set; }

	public int CurrentExchangePartnerObjectId { get; set; }

	private IReadOnlyList<InventoryItem> _inventoryItems = Array.Empty<InventoryItem>();
	private IReadOnlyList<InventoryItem> _warehouseItems = Array.Empty<InventoryItem>();
	private IReadOnlyList<InventoryItem> _accountWarehouseItems = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> InventoryItems
	{
		get => _inventoryItems;
		set
		{
			_inventoryItems = value;
			InventoryStoragePersistentState = PromoteStoragePersistentState(InventoryStoragePersistentState, value.Where(item => !item.IsEquipped).ToArray());
			EquipmentPersistentState = PromoteEquipmentPersistentState(EquipmentPersistentState, value);
		}
	}

	public IReadOnlyList<InventoryItem> WarehouseItems
	{
		get => _warehouseItems;
		set
		{
			_warehouseItems = value;
			WarehouseStoragePersistentState = PromoteStoragePersistentState(WarehouseStoragePersistentState, value);
		}
	}

	public IReadOnlyList<InventoryItem> AccountWarehouseItems
	{
		get => _accountWarehouseItems;
		set
		{
			_accountWarehouseItems = value;
			AccountWarehouseStoragePersistentState = PromoteStoragePersistentState(AccountWarehouseStoragePersistentState, value);
		}
	}

	// Java parity: model/items/storage/Storage.persistentState for the currently modeled player-owned storages.
	public StoragePersistentState InventoryStoragePersistentState { get; private set; } = StoragePersistentState.Updated;

	public StoragePersistentState WarehouseStoragePersistentState { get; private set; } = StoragePersistentState.Updated;

	public StoragePersistentState AccountWarehouseStoragePersistentState { get; private set; } = StoragePersistentState.Updated;

	// Java parity: model/gameobjects/player/Equipment.persistentState.
	public StoragePersistentState EquipmentPersistentState { get; private set; } = StoragePersistentState.Updated;

	// Java parity: model/items/storage/Storage.deletedItems for the currently modeled player-owned storages.
	public IReadOnlyList<InventoryItem> DeletedInventoryItems { get; private set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> DeletedWarehouseItems { get; private set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> DeletedAccountWarehouseItems { get; private set; } = Array.Empty<InventoryItem>();

	// Java parity: model/gameobjects/player/Player.getDirtyItemsToUpdate.
	public List<InventoryItem> GetDirtyItemsToUpdate()
	{
		var dirtyItems = new List<InventoryItem>();
		AddDirtyStorageItems(dirtyItems, StorageLocation.Cube, InventoryItems.Where(item => !item.IsEquipped).ToArray(), DeletedInventoryItems);
		AddDirtyStorageItems(dirtyItems, StorageLocation.Warehouse, WarehouseItems, DeletedWarehouseItems);
		AddDirtyStorageItems(dirtyItems, StorageLocation.AccountWarehouse, AccountWarehouseItems, DeletedAccountWarehouseItems);
		AddDirtyEquipmentItems(dirtyItems, InventoryItems);
		return dirtyItems;
	}

	// Java parity: Player.getDirtyItemsToUpdate resets storage/equipment persistent state after InventoryDAO.store(player).
	public void MarkDirtyItemsPersisted()
	{
		_inventoryItems = NormalizePersistentState(InventoryItems);
		_warehouseItems = NormalizePersistentState(WarehouseItems);
		_accountWarehouseItems = NormalizePersistentState(AccountWarehouseItems);
		InventoryStoragePersistentState = StoragePersistentState.Updated;
		WarehouseStoragePersistentState = StoragePersistentState.Updated;
		AccountWarehouseStoragePersistentState = StoragePersistentState.Updated;
		EquipmentPersistentState = StoragePersistentState.Updated;
		DeletedInventoryItems = Array.Empty<InventoryItem>();
		DeletedWarehouseItems = Array.Empty<InventoryItem>();
		DeletedAccountWarehouseItems = Array.Empty<InventoryItem>();
	}

	// Java parity: Storage.setPersistentState(UPDATE_REQUIRED) at the currently modeled player-owned storage boundary.
	public void MarkStorageDirty(int location)
	{
		switch ((StorageLocation)location)
		{
			case StorageLocation.Cube:
				InventoryStoragePersistentState = StoragePersistentState.UpdateRequired;
				return;
			case StorageLocation.Warehouse:
				WarehouseStoragePersistentState = StoragePersistentState.UpdateRequired;
				return;
			case StorageLocation.AccountWarehouse:
				AccountWarehouseStoragePersistentState = StoragePersistentState.UpdateRequired;
				return;
		}
	}

	// Java parity: model/gameobjects/player/Equipment.setPersistentState(UPDATE_REQUIRED).
	public void MarkEquipmentDirty()
	{
		EquipmentPersistentState = StoragePersistentState.UpdateRequired;
	}

	// Java parity: model/items/storage/Storage.delete(Item, ...) adds deleted rows to storage.deletedItems.
	public void TrackDeletedItem(InventoryItem item)
	{
		var deletedState = InventoryItem.TransitionPersistentState(item.PersistentState, InventoryItemPersistentState.Deleted);
		switch (deletedState)
		{
			case InventoryItemPersistentState.NoAction:
				RemoveDeletedItem(item);
				return;
			case InventoryItemPersistentState.Deleted:
				var deletedItem = CopyInventoryItem(item, deletedState);
				switch (item.Location)
				{
					case 0:
						DeletedInventoryItems = ReplaceDeletedItem(DeletedInventoryItems, deletedItem);
						InventoryStoragePersistentState = StoragePersistentState.UpdateRequired;
						return;
					case 1:
						DeletedWarehouseItems = ReplaceDeletedItem(DeletedWarehouseItems, deletedItem);
						WarehouseStoragePersistentState = StoragePersistentState.UpdateRequired;
						return;
					case 2:
						DeletedAccountWarehouseItems = ReplaceDeletedItem(DeletedAccountWarehouseItems, deletedItem);
						AccountWarehouseStoragePersistentState = StoragePersistentState.UpdateRequired;
						return;
				}

				return;
		}
	}

	public IReadOnlyList<PlayerSkill> Skills { get; set; } = Array.Empty<PlayerSkill>();

	public IReadOnlyDictionary<int, long> SkillCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerItemCooldown> ItemCooldowns { get; set; } = new Dictionary<int, PlayerItemCooldown>();

	public IReadOnlyList<PlayerQuestState> Quests { get; set; } = Array.Empty<PlayerQuestState>();

	// Java parity: model/gameobjects/player/Player.getNpcFactions queried by QuestService.checkStartConditions.
	public PlayerNpcFactionsSnapshot NpcFactions { get; set; } = PlayerNpcFactionsSnapshot.Empty;

	public IReadOnlyList<PlayerTitle> Titles { get; set; } = Array.Empty<PlayerTitle>();

	public IReadOnlyList<PlayerMotion> Motions { get; set; } = Array.Empty<PlayerMotion>();

	public IReadOnlyList<PlayerEmotion> Emotions { get; set; } = Array.Empty<PlayerEmotion>();

	public IReadOnlyList<int> Recipes { get; set; } = Array.Empty<int>();

	public IReadOnlyList<PlayerMacro> Macros { get; set; } = Array.Empty<PlayerMacro>();

	public IReadOnlyList<PlayerMail> Mailbox { get; set; } = Array.Empty<PlayerMail>();

	// Java parity: services/player/PlayerMailboxState and model/gameobjects/player/Mailbox.mailBoxState.
	public byte MailboxState { get; set; }

	// Java parity: CM_READ_EXPRESS_MAIL.runImpl checks Player.getPostman and TaskId.EXPRESS_MAIL_USE.
	public bool HasSummonedPostman { get; set; }

	public PostmanNpc? Postman { get; set; }

	public DateTimeOffset? ExpressMailCooldownUntil { get; set; }

	// Java parity: Player.getSummon() != null && Summon.isPet() guard used by CM_CASTSPELL pet-order skills.
	public bool HasPetSummon { get; set; }

	// Java parity: model/gameobjects/Summon.getObjectId consumed by PetOrderUseUltraSkillEffect -> SM_SUMMON_USESKILL.
	public int PetSummonObjectId { get; set; }

	// Java parity: model/gameobjects/Summon.getNpcId consumed by PetSkillData.getPetOrderSkill(orderSkillId, npcId).
	public int PetSummonNpcId { get; set; }

	// Java parity: model/gameobjects/player/Player.getSummonOrMercenary fallback until live Summon/Npc models exist.
	public int RepresentedSummonOrMercenaryObjectId { get; set; }

	public PlayerSummonOrMercenaryKind RepresentedSummonOrMercenaryKind { get; set; }

	public int RepresentedSummonOrMercenaryNpcId { get; set; }

	private readonly List<PlayerPetSkillOrder> _petSkillOrders = [];
	private readonly Dictionary<int, PlayerSummonKnownObject> _summonKnownObjects = [];

	private void AddDirtyStorageItems(
		List<InventoryItem> dirtyItems,
		StorageLocation location,
		IReadOnlyList<InventoryItem> items,
		IReadOnlyList<InventoryItem> deletedItems)
	{
		if (GetStoragePersistentState(location) != StoragePersistentState.UpdateRequired)
			return;

		dirtyItems.AddRange(items);
		dirtyItems.AddRange(deletedItems);
		SetStoragePersistentState(location, StoragePersistentState.Updated);
	}

	private void AddDirtyEquipmentItems(List<InventoryItem> dirtyItems, IReadOnlyList<InventoryItem> items)
	{
		if (EquipmentPersistentState != StoragePersistentState.UpdateRequired)
			return;

		dirtyItems.AddRange(items.Where(item => item.IsEquipped));
		EquipmentPersistentState = StoragePersistentState.Updated;
	}

	private static IReadOnlyList<InventoryItem> NormalizePersistentState(IReadOnlyList<InventoryItem> items)
	{
		if (!items.Any(item => item.PersistentState != InventoryItemPersistentState.Updated))
			return items;

		return items.Select(item => item.PersistentState == InventoryItemPersistentState.Updated ? item : CopyInventoryItem(item, InventoryItemPersistentState.Updated)).ToArray();
	}

	private static StoragePersistentState PromoteStoragePersistentState(
		StoragePersistentState currentState,
		IReadOnlyList<InventoryItem> items)
	{
		if (currentState == StoragePersistentState.UpdateRequired)
			return currentState;

		return items.Any(item => item.PersistentState is InventoryItemPersistentState.New
			or InventoryItemPersistentState.UpdateRequired
			or InventoryItemPersistentState.Deleted)
			? StoragePersistentState.UpdateRequired
			: StoragePersistentState.Updated;
	}

	private static StoragePersistentState PromoteEquipmentPersistentState(
		StoragePersistentState currentState,
		IReadOnlyList<InventoryItem> items)
	{
		if (currentState == StoragePersistentState.UpdateRequired)
			return currentState;

		return items.Any(item =>
			item.IsEquipped
			&& item.PersistentState is InventoryItemPersistentState.New
				or InventoryItemPersistentState.UpdateRequired
				or InventoryItemPersistentState.Deleted)
			? StoragePersistentState.UpdateRequired
			: StoragePersistentState.Updated;
	}

	private StoragePersistentState GetStoragePersistentState(StorageLocation location)
	{
		return location switch
		{
			StorageLocation.Cube => InventoryStoragePersistentState,
			StorageLocation.Warehouse => WarehouseStoragePersistentState,
			StorageLocation.AccountWarehouse => AccountWarehouseStoragePersistentState,
			_ => StoragePersistentState.Updated,
		};
	}

	private void SetStoragePersistentState(StorageLocation location, StoragePersistentState persistentState)
	{
		switch (location)
		{
			case StorageLocation.Cube:
				InventoryStoragePersistentState = persistentState;
				return;
			case StorageLocation.Warehouse:
				WarehouseStoragePersistentState = persistentState;
				return;
			case StorageLocation.AccountWarehouse:
				AccountWarehouseStoragePersistentState = persistentState;
				return;
		}
	}

	private void RemoveDeletedItem(InventoryItem item)
	{
		switch (item.Location)
		{
			case 0:
				DeletedInventoryItems = RemoveDeletedItem(DeletedInventoryItems, item.ObjectId);
				return;
			case 1:
				DeletedWarehouseItems = RemoveDeletedItem(DeletedWarehouseItems, item.ObjectId);
				return;
			case 2:
				DeletedAccountWarehouseItems = RemoveDeletedItem(DeletedAccountWarehouseItems, item.ObjectId);
				return;
		}
	}

	private static IReadOnlyList<InventoryItem> ReplaceDeletedItem(IReadOnlyList<InventoryItem> items, InventoryItem deletedItem)
	{
		var updated = items.Where(item => item.ObjectId != deletedItem.ObjectId).ToList();
		updated.Add(deletedItem);
		return updated.ToArray();
	}

	private static IReadOnlyList<InventoryItem> RemoveDeletedItem(IReadOnlyList<InventoryItem> items, int objectId)
	{
		if (!items.Any(item => item.ObjectId == objectId))
			return items;

		return items.Where(item => item.ObjectId != objectId).ToArray();
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, InventoryItemPersistentState persistentState)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = item.Count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			PendingTuneResult = item.PendingTuneResult,
			PersistentState = persistentState,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}

	private enum StorageLocation
	{
		Cube = 0,
		Warehouse = 1,
		AccountWarehouse = 2,
	}

	// Java parity: model/gameobjects/Summon.addSkillOrder represented until the full Summon model exists.
	public IReadOnlyList<PlayerPetSkillOrder> PetSkillOrders => _petSkillOrders;

	public void AddPetSkillOrder(PlayerPetSkillOrder order)
	{
		_petSkillOrders.Add(order);
	}

	public PlayerPetSkillOrder? RetrieveNextPetSkillOrder()
	{
		if (_petSkillOrders.Count == 0)
			return null;

		var order = _petSkillOrders[0];
		_petSkillOrders.RemoveAt(0);
		return order;
	}

	public void SetSummonKnownObject(int objectId, PlayerSummonKnownObjectKind kind)
	{
		// Java parity: CM_SUMMON_CASTSPELL resolves target via summon.getKnownList().getObject(targetObjId).
		SetSummonKnownObject(new PlayerSummonKnownObject(objectId, kind));
	}

	public void SetSummonKnownObject(PlayerSummonKnownObject knownObject)
	{
		// Java parity: KnownList stores VisibleObject instances; represented metadata is used until live objects exist.
		_summonKnownObjects[knownObject.ObjectId] = knownObject;
	}

	public bool TryGetSummonKnownObjectKind(int objectId, out PlayerSummonKnownObjectKind kind)
	{
		// Java parity: KnownList returns null for lagged-out or no-longer-visible targets.
		if (_summonKnownObjects.TryGetValue(objectId, out var knownObject))
		{
			kind = knownObject.Kind;
			return true;
		}

		kind = default;
		return false;
	}

	public bool TryGetSummonKnownObject(int objectId, out PlayerSummonKnownObject knownObject)
	{
		// Java parity: Player.getSummonOrMercenary can inspect known-list NPC creator/template metadata.
		return _summonKnownObjects.TryGetValue(objectId, out knownObject!);
	}

	public bool TryRenewSummonKnownObjectLastSkillTime(int objectId, long currentTimeMilliseconds)
	{
		// Java parity: NpcGameStats.renewLastSkillTime stores System.currentTimeMillis().
		if (!_summonKnownObjects.TryGetValue(objectId, out var knownObject))
			return false;

		_summonKnownObjects[objectId] = knownObject with
		{
			LastSkillTimeMilliseconds = currentTimeMilliseconds,
		};
		return true;
	}

	public bool TrySetSummonKnownObjectNextSkillDelay(int objectId, int nextSkillDelayMilliseconds)
	{
		// Java parity: NpcGameStats.setNextSkillDelay stores already-resolved non-random delays.
		if (!_summonKnownObjects.TryGetValue(objectId, out var knownObject))
			return false;

		_summonKnownObjects[objectId] = knownObject with
		{
			NextSkillDelayMilliseconds = nextSkillDelayMilliseconds,
		};
		return true;
	}

	public bool TryStoreSummonKnownObjectNpcSkillPreview(
		int objectId,
		PlayerSummonKnownObjectNpcSkillCandidateListProjection? skillListProjection,
		PlayerSummonKnownObjectNpcSkillSelectionPreview? selectionPreview,
		PlayerSummonKnownObjectNpcSkillActionPreview? actionPreview,
		PlayerSummonKnownObjectNpcSkillPostSpawnPreview? postSpawnPreview = null,
		PlayerSummonKnownObjectNpcSkillActionWorkflowPreview? actionWorkflowPreview = null,
		PlayerSummonKnownObjectNpcSkillPerformAttackPreview? performAttackPreview = null,
		PlayerSummonKnownObjectNpcSkillPerformAttackExecutionPreview? performAttackExecutionPreview = null,
		PlayerSummonKnownObjectNpcSkillSchedulerCallbackOutcome? schedulerCallbackOutcome = null)
	{
		// Java parity: represents Npc.getSkillList, SkillAttackManager.chooseNextSkill, skillAction, and fireOnEndCastEvents state without live AI mutation.
		if (!_summonKnownObjects.TryGetValue(objectId, out var knownObject))
			return false;

		_summonKnownObjects[objectId] = knownObject with
		{
			LastNpcSkillListProjection = skillListProjection,
			LastNpcSkillSelectionPreview = selectionPreview,
			LastNpcSkillActionPreview = actionPreview,
			LastNpcSkillPostSpawnPreview = postSpawnPreview,
			LastNpcSkillActionWorkflowPreview = actionWorkflowPreview,
			LastNpcSkillPerformAttackPreview = performAttackPreview,
			LastNpcSkillPerformAttackExecutionPreview = performAttackExecutionPreview,
			LastNpcSkillSchedulerCallbackOutcome = schedulerCallbackOutcome,
		};
		return true;
	}

	public PlayerSummonOrMercenaryKind GetSummonOrMercenaryKind(int objectId)
	{
		// Java parity: Player.getSummonOrMercenary first checks the owned summon, then creator-owned mercenary NPCs.
		if (HasPetSummon && PetSummonObjectId == objectId)
			return PlayerSummonOrMercenaryKind.PetSummon;

		if (TryGetSummonKnownObject(objectId, out var knownObject)
			&& knownObject.Kind == PlayerSummonKnownObjectKind.Creature
			&& knownObject.CreatorObjectId == ObjectId
			&& knownObject.NpcTemplateType == PlayerSummonKnownNpcTemplateType.Mercenary)
		{
			return PlayerSummonOrMercenaryKind.Mercenary;
		}

		return RepresentedSummonOrMercenaryObjectId == objectId
			? RepresentedSummonOrMercenaryKind
			: PlayerSummonOrMercenaryKind.None;
	}

	public int GetSummonOrMercenaryNpcId(int objectId)
	{
		// Java parity: CM_SUMMON_CASTSPELL mercenary branch uses creature.getObjectTemplate().getTemplateId().
		if (HasPetSummon && PetSummonObjectId == objectId)
			return PetSummonNpcId;

		if (TryGetSummonKnownObject(objectId, out var knownObject)
			&& knownObject.Kind == PlayerSummonKnownObjectKind.Creature
			&& knownObject.CreatorObjectId == ObjectId
			&& knownObject.NpcTemplateType == PlayerSummonKnownNpcTemplateType.Mercenary)
		{
			return knownObject.NpcTemplateId;
		}

		return RepresentedSummonOrMercenaryObjectId == objectId
			? RepresentedSummonOrMercenaryNpcId
			: 0;
	}

	public PlayerBrokerSettlementSummary BrokerSettlements { get; set; } = PlayerBrokerSettlementSummary.Empty;

	// Java parity: model/broker/BrokerPlayerCache remembers the last broker list/search for refresh after buy.
	public int BrokerMaskCache { get; set; }

	public byte BrokerSortTypeCache { get; set; }

	public int BrokerStartPageCache { get; set; }

	public IReadOnlyList<int> BrokerSearchItemIds { get; set; } = Array.Empty<int>();

	public IReadOnlyList<PlayerHouse> Houses { get; set; } = Array.Empty<PlayerHouse>();

	public IReadOnlyDictionary<int, long> CraftCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, long> HouseObjectCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerPortalCooldown> PortalCooldowns { get; set; } = new Dictionary<int, PlayerPortalCooldown>();

	public PlayerLifeStats? LifeStats { get; set; }

	public IReadOnlyList<PlayerFriend> Friends { get; set; } = Array.Empty<PlayerFriend>();

	// Java parity: model/gameobjects/player/FriendList.Status changed by CM_FRIEND_STATUS.
	public byte FriendListStatus { get; set; }

	// Java parity: model/gameobjects/player/ResponseRequester stores pending SM_QUESTION_WINDOW handlers.
	public QuestionResponseRegistry ResponseRequester { get; } = new();

	public PendingFriendRequest? PendingFriendRequest { get; set; }

	public PendingChargeAllRequest? PendingChargeAllRequest { get; set; }

	public PendingSoulBindRequest? PendingSoulBindRequest { get; set; }

	public PendingRiftPortalRequest? PendingRiftPortalRequest { get; set; }

	public PendingKiskBindRequest? PendingKiskBindRequest { get; set; }

	public PendingLeagueInviteRequest? PendingLeagueInviteRequest { get; set; }

	public PendingAllianceInviteRequest? PendingAllianceInviteRequest { get; set; }

	public PendingDuelRequest? PendingDuelRequest { get; set; }

	public PendingDuelRequest? PendingDuelWithdrawRequest { get; set; }

	public PendingExperienceRecoveryRequest? PendingExperienceRecoveryRequest { get; set; }

	public PendingExchangeRequest? PendingExchangeRequest { get; set; }

	public PendingRecallInstantRequest? PendingRecallInstantRequest { get; set; }

	public PendingCraftSkillLearnRequest? PendingCraftSkillLearnRequest { get; set; }

	public PendingStorageExpansionRequest? PendingStorageExpansionRequest { get; set; }

	// Java parity: controllers/CreatureController TaskId.TELEPORT stores TeleportService.SpawnTask until CM_TELEPORT_ANIMATION_DONE.
	public PendingPlayerTeleport? PendingTeleport { get; set; }

	// Java parity: model/gameobjects/player/Player.kisk points at the currently bound Kisk.
	public int BoundKiskObjectId { get; set; }

	// Java parity: model/gameobjects/player/Player.getCurrentTeam, currently narrowed to the RVController removal boundary until full team models land.
	public PlayerTeamMembership TeamMembership { get; set; }

	public bool IsInTeam => TeamMembership != PlayerTeamMembership.None;

	// Java parity: PlayerGroup.getTeamId / PlayerAlliance.getObjectId consumed by PortalService team instance lookup.
	public int CurrentTeamId { get; set; }

	// Java parity: PlayerGroup.getMembers used by PortalService group instance reuse/fanout; currently only plan metadata.
	public IReadOnlyList<int> CurrentTeamMemberObjectIds { get; set; } = Array.Empty<int>();

	// Java parity: Player.getPlayerGroup returns a live PlayerGroup; this snapshot is a narrow bridge until team lifecycle is ported.
	public PlayerGroupSnapshot? CurrentGroupSnapshot { get; set; }

	// Java parity: Player.getPlayerAlliance returns a live PlayerAlliance; this snapshot is a narrow bridge until alliance lifecycle is ported.
	public PlayerAllianceSnapshot? CurrentAllianceSnapshot { get; set; }

	// Java parity: model/gameobjects/player/Player.isLooting / getLootingNpcOid used by DropService request/close list.
	public int LootingNpcObjectId { get; set; }

	public bool IsLooting => IsInState(PlayerCreatureState.Looting);

	// Java parity: Player.usingItem set by SM_ITEM_USAGE_ANIMATION while delayed item use is active.
	public int UsingItemObjectId { get; set; }

	// Java parity: model/gameobjects/Creature.castingSkill represented by skill id until full Skill instances are ported.
	public int CastingSkillId { get; private set; }

	// Java parity: skillengine/model/Skill.SkillMethod used by PlayerController.cancelCurrentSkill branch fanout.
	public PlayerCastingSkillMethod CastingSkillMethod { get; private set; }

	// Java parity: SkillMethod.ITEM cancellation uses Skill.itemObjectId, item template id, first target, and delay id.
	public int CastingItemObjectId { get; private set; }

	public int CastingItemTemplateId { get; private set; }

	public int CastingFirstTargetObjectId { get; private set; }

	public int? CastingItemCooldownDelayId { get; private set; }

	// Java parity: model/gameobjects/player/Player.lastSkill updated when a non-null casting skill is cleared.
	public int LastCastingSkillId { get; private set; }

	public IReadOnlyList<PlayerBlockedUser> BlockedUsers { get; set; } = Array.Empty<PlayerBlockedUser>();

	// Java parity: Player.getStore().getSoldItems() preserves insertion order
	// for PrivateStoreService.getBoughtItems index-based CM_BUY_ITEM action 0 lookup.
	public IReadOnlyList<PrivateStoreListedItemSummary> PrivateStoreItems { get; set; } = Array.Empty<PrivateStoreListedItemSummary>();

	public PlayerAbyssRank AbyssRank { get; set; } = PlayerAbyssRank.Default();

	public PlayerSettings Settings { get; set; } = new();

	public PlayerBindPoint? BindPoint { get; set; }

	public bool IsInState(PlayerCreatureState state)
	{
		// Java parity: model/gameobjects/Creature.isInState, including exact-match multibit states.
		return state switch
		{
			PlayerCreatureState.Chair => CreatureState == PlayerCreatureState.Chair,
			PlayerCreatureState.PrivateShop => CreatureState == PlayerCreatureState.PrivateShop,
			_ => (CreatureState & state) == state,
		};
	}

	public void SetCreatureState(PlayerCreatureState state, bool enabled)
	{
		// Java parity: model/gameobjects/Creature.setState/unsetState bit updates.
		CreatureState = enabled ? CreatureState | state : CreatureState & ~state;
	}

	public void ReplaceCreatureState(PlayerCreatureState state)
	{
		// Java parity: model/gameobjects/Creature.setState(state, replace=true).
		CreatureState = state;
	}

	public void ClearResurrectionPositionState()
	{
		// Java parity: model/gameobjects/player/Player.unsetResPosState clears the positional resurrection flag and coordinates only when active.
		if (!IsInResurrectionPositionState)
			return;

		IsInResurrectionPositionState = false;
		ResurrectionPositionX = 0;
		ResurrectionPositionY = 0;
		ResurrectionPositionZ = 0;
	}

	public void StartLooting(int npcObjectId)
	{
		// Java parity: DropService.requestDropList unsets ACTIVE, sets LOOTING, and stores the looting NPC oid.
		SetCreatureState(PlayerCreatureState.Active, false);
		SetCreatureState(PlayerCreatureState.Looting, true);
		LootingNpcObjectId = npcObjectId;
	}

	public void StopLooting()
	{
		// Java parity: DropService.closeDropList unsets LOOTING, sets ACTIVE, and clears the looting NPC oid.
		SetCreatureState(PlayerCreatureState.Looting, false);
		SetCreatureState(PlayerCreatureState.Active, true);
		LootingNpcObjectId = 0;
	}

	public void SetCastingSkill(
		int skillId,
		PlayerCastingSkillMethod method = PlayerCastingSkillMethod.Cast,
		int itemObjectId = 0,
		int itemTemplateId = 0,
		int firstTargetObjectId = 0,
		int? itemCooldownDelayId = null)
	{
		// Java parity: model/gameobjects/Creature.setCasting stores the active Skill; this port stores the represented skill id.
		if (skillId == 0)
		{
			ClearCastingSkill();
			return;
		}

		CastingSkillId = skillId;
		CastingSkillMethod = method;
		CastingItemObjectId = itemObjectId;
		CastingItemTemplateId = itemTemplateId;
		CastingFirstTargetObjectId = firstTargetObjectId;
		CastingItemCooldownDelayId = itemCooldownDelayId;
	}

	public PlayerCastingSkillSnapshot? ClearCastingSkill()
	{
		// Java parity: model/gameobjects/player/Player.setCasting(null) records lastSkill before clearing the current cast.
		var skillId = CastingSkillId;
		if (skillId == 0)
			return null;

		var method = CastingSkillMethod;
		var itemObjectId = CastingItemObjectId;
		var itemTemplateId = CastingItemTemplateId;
		var firstTargetObjectId = CastingFirstTargetObjectId;
		var itemCooldownDelayId = CastingItemCooldownDelayId;
		LastCastingSkillId = skillId;
		CastingSkillId = 0;
		CastingSkillMethod = PlayerCastingSkillMethod.None;
		CastingItemObjectId = 0;
		CastingItemTemplateId = 0;
		CastingFirstTargetObjectId = 0;
		CastingItemCooldownDelayId = null;
		return new PlayerCastingSkillSnapshot(skillId, method, itemObjectId, itemTemplateId, firstTargetObjectId, itemCooldownDelayId);
	}

	public PlayerTeamMembership RemoveCurrentTeam()
	{
		// Java parity: PlayerGroupService.removePlayer / PlayerAllianceService.removePlayer clears the responder's current team.
		var removed = TeamMembership;
		TeamMembership = PlayerTeamMembership.None;
		CurrentGroupSnapshot = null;
		CurrentAllianceSnapshot = null;
		return removed;
	}

	public bool IsAbnormalSet(PlayerAbnormalState state)
	{
		// Java parity: controllers/effect/EffectController.isAbnormalSet.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) == state;
	}

	public bool IsInAnyAbnormalState(PlayerAbnormalState state)
	{
		// Java parity: controllers/effect/EffectController.isInAnyAbnormalState.
		return state == PlayerAbnormalState.None ? AbnormalState == PlayerAbnormalState.None : (AbnormalState & state) != 0;
	}

	public bool IsInVisualState(int state)
	{
		// Java parity: model/gameobjects/Creature.isInVisualState.
		return (VisualState & state) == state;
	}

	public void SetVisualState(int state)
	{
		// Java parity: model/gameobjects/Creature.setVisualState bitwise ORs the visual id.
		VisualState |= state;
	}

	public void UnsetVisualState(int state)
	{
		// Java parity: model/gameobjects/Creature.unsetVisualState clears the visual id bits.
		VisualState &= ~state;
	}

	public bool IsProtectionActive()
	{
		// Java parity: model/gameobjects/player/Player.isProtectionActive.
		return IsInVisualState(PlayerVisualStates.Blinking);
	}

	public bool StopProtectionActive()
	{
		// Java parity: controllers/PlayerController.stopProtectionActiveTask state mutation.
		if (!IsProtectionActive())
			return false;

		UnsetVisualState(PlayerVisualStates.Blinking);
		return true;
	}

	public bool IsInAnyHide()
	{
		// Java parity: model/gameobjects/Creature.isInAnyHide exact visual-state comparison.
		return VisualState != PlayerVisualStates.Visible && VisualState != PlayerVisualStates.Blinking;
	}

	public bool RemoveHideEffects()
	{
		// Java parity: controllers/effect/EffectController.removeHideEffects side effect needed by CM_SHOW_DIALOG.
		var visualState = VisualState;
		var abnormalState = AbnormalState;
		VisualState &= PlayerVisualStates.Blinking;
		AbnormalState &= ~PlayerAbnormalState.Hide;
		return VisualState != visualState || AbnormalState != abnormalState;
	}

	public bool IsUnderFear()
	{
		// Java parity: controllers/effect/EffectController.isUnderFear.
		return IsAbnormalSet(PlayerAbnormalState.Fear);
	}

	public bool IsConfused()
	{
		// Java parity: controllers/effect/EffectController.isConfused.
		return IsAbnormalSet(PlayerAbnormalState.Confuse);
	}

	public bool CanPerformMove()
	{
		// Java parity: Player.canPerformMove + Creature.canPerformMove; spawned/casting-skill exceptions are future slices.
		return !TransformBansMovement
			&& !IsInAnyAbnormalState(PlayerAbnormalState.CantMoveState);
	}

	public bool IsUnderStance()
	{
		// Java parity: controllers/PlayerController.isUnderStance.
		return StanceSkillId != 0;
	}

	public bool IsFlying()
	{
		// Java parity: model/gameobjects/player/Player.isFlying returns flyState >= 1.
		return FlyState != PlayerFlyState.None;
	}

	public void SetFlyState(PlayerFlyState state)
	{
		// Java parity: model/gameobjects/player/Player.setFlyState bitwise ORs FlyState ids.
		FlyState |= state;
	}

	public void UnsetFlyState(PlayerFlyState state)
	{
		// Java parity: model/gameobjects/player/Player.unsetFlyState clears FlyState ids.
		FlyState &= ~state;
	}

	public bool IsInFlyState(PlayerFlyState state)
	{
		// Java parity: model/gameobjects/player/Player.isInFlyState.
		return state == PlayerFlyState.None ? FlyState == PlayerFlyState.None : (FlyState & state) == state;
	}

	public bool IsInFlyingState()
	{
		// Java parity: model/gameobjects/player/Player.isInFlyingState checks FlyState.FLYING only.
		return IsInFlyState(PlayerFlyState.Flying);
	}

	public bool IsInGlidingState()
	{
		// Java parity: model/gameobjects/player/Player.isInGlidingState checks FlyState.GLIDING only.
		return IsInFlyState(PlayerFlyState.Gliding);
	}

	public bool IsUsingFlightPath(PlayerFlightPathType type)
	{
		// Java parity: model/gameobjects/player/Player.isUsingFlightPath.
		return FlightPathType == type && IsInState(PlayerCreatureState.Flying);
	}

	public void CompleteFlyTeleport()
	{
		// Java parity: controllers/PlayerController.onFlyTeleportEnd.
		if (IsUsingFlightPath(PlayerFlightPathType.Windstream))
		{
			SetCreatureState(PlayerCreatureState.Flying, enabled: false);
			UnsetFlyState(PlayerFlyState.Flying);
			SetFlyState(PlayerFlyState.Gliding);
			SetCreatureState(PlayerCreatureState.Active, enabled: true);
			SetCreatureState(PlayerCreatureState.Gliding, enabled: true);
			TriggerFpReduce();
		}
		else
		{
			SetCreatureState(PlayerCreatureState.Flying, enabled: false);
			SetCreatureState(PlayerCreatureState.Active, enabled: true);
		}

		FlightPathType = null;
	}

	public void StartFlying()
	{
		// Java parity: controllers/FlyController.startFly state and FP-reduce side effects.
		SetFlyState(PlayerFlyState.Flying);
		SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		if (IsInRideMode)
			SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: true);
		TriggerFpReduce();
	}

	public void EndFlying()
	{
		// Java parity: controllers/FlyController.endFly state and FP-restore side effects.
		UnsetFlyState(PlayerFlyState.Flying);
		UnsetFlyState(PlayerFlyState.Gliding);
		SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		TriggerFpRestore();
	}

	public bool StartGliding()
	{
		// Java parity: controllers/FlyController.switchToGliding state and FP-reduce side effects.
		if (IsInGlidingState())
			return false;

		SetFlyState(PlayerFlyState.Gliding);
		SetCreatureState(PlayerCreatureState.Gliding, enabled: true);
		TriggerFpReduce();
		return true;
	}

	public bool StopGliding()
	{
		// Java parity: controllers/FlyController.onStopGliding.
		if (!IsInGlidingState())
			return false;

		UnsetFlyState(PlayerFlyState.Gliding);
		SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		if (IsInFlyingState())
		{
			TriggerFpReduce();
			return false;
		}

		TriggerFpRestore();
		return true;
	}

	public bool EnterFlyArea()
	{
		// Java parity: controllers/PlayerController.onEnterFlyArea -> PlayerLifeStats.triggerFpReduce.
		if (!IsFlying() && !IsInSprintMode)
			return false;

		TriggerFpReduce();
		return true;
	}

	public PlayerLeaveFlyAreaStatus LeaveFlyArea(int freeFlightAccessLevel)
	{
		// Java parity: controllers/PlayerController.onLeaveFlyArea state and FP-task intent; packet/audit fanout remains in GameServerConnection.
		if (AccessLevel >= freeFlightAccessLevel)
			return PlayerLeaveFlyAreaStatus.FreeFlightAccess;

		if (IsInFlyingState())
		{
			if (IsInGlidingState())
			{
				UnsetFlyState(PlayerFlyState.Flying);
				SetCreatureState(PlayerCreatureState.Flying, enabled: false);
				TriggerFpReduce();
				return PlayerLeaveFlyAreaStatus.ContinueGliding;
			}

			EndFlying();
			return PlayerLeaveFlyAreaStatus.EndedFlying;
		}

		if (IsInGlidingState())
		{
			TriggerFpReduce();
			return PlayerLeaveFlyAreaStatus.GlidingOutsideFlyArea;
		}

		return PlayerLeaveFlyAreaStatus.NoChange;
	}

	public bool CanStartRide()
	{
		// Java parity: model/templates/item/actions/RideAction.canAct baseline guards before mounting.
		return !IsInRideMode
			&& !IsInState(PlayerCreatureState.Resting)
			&& !IsInAnyAbnormalState(PlayerAbnormalState.DismountRide);
	}

	public void MountRide(PlayerRideInfo rideInfo)
	{
		// Java parity: RideAction.act completion + PlayerActions.setPlayerMode(PlayerMode.RIDE).
		SetCreatureState(PlayerCreatureState.Active, enabled: false);
		SetCreatureState(PlayerCreatureState.Resting, enabled: true);
		if (IsFlying())
			SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: true);

		IsInRideMode = true;
		RideInfo = rideInfo;
	}

	public bool DismountRide()
	{
		// Java parity: PlayerActions.unsetPlayerMode(PlayerMode.RIDE).
		if (!IsInRideMode)
			return false;

		IsInRideMode = false;
		RideInfo = null;
		if (IsInSprintMode)
		{
			if (!IsFlying())
				TriggerFpRestore();
			IsInSprintMode = false;
		}

		SetCreatureState(PlayerCreatureState.Resting, enabled: false);
		SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		SetCreatureState(PlayerCreatureState.Active, enabled: true);
		return true;
	}

	public bool CanStartRideSprint()
	{
		// Java parity: CM_EMOTION.START_SPRINT guard using PlayerMode.RIDE, current FP, Player.isFlying, and RideInfo.canSprint.
		return IsInRideMode
			&& RideInfo is { } rideInfo
			&& (LifeStats?.GetCurrentFp() ?? 0) >= rideInfo.StartFp
			&& !IsFlying()
			&& rideInfo.CanSprint();
	}

	public bool CanEndRideSprint()
	{
		// Java parity: CM_EMOTION.END_SPRINT guard using PlayerMode.RIDE, RideInfo.canSprint, and Player.isInSprintMode.
		return IsInRideMode
			&& RideInfo is { } rideInfo
			&& rideInfo.CanSprint()
			&& IsInSprintMode;
	}

	public void StartRideSprint()
	{
		// Java parity: CM_EMOTION.START_SPRINT -> Player.setSprintMode(true) + PlayerLifeStats.triggerFpReduce.
		IsInSprintMode = true;
		TriggerFpReduce();
	}

	public void EndRideSprint()
	{
		// Java parity: CM_EMOTION.END_SPRINT -> Player.setSprintMode(false) + PlayerLifeStats.triggerFpRestore.
		IsInSprintMode = false;
		TriggerFpRestore();
	}

	public void TriggerFpReduce()
	{
		// Java parity: PlayerLifeStats.triggerFpReduce cancels FP restore before starting FP reduce.
		IsFpRestoreActive = false;
		IsFpReduceActive = true;
	}

	public void TriggerFpRestore()
	{
		// Java parity: PlayerLifeStats.triggerFpRestore cancels FP reduce before starting FP restore.
		IsFpReduceActive = false;
		IsFpRestoreActive = true;
	}

	public void AddItemCooldown(int delayId, int useDelayMillis, DateTimeOffset now)
	{
		// Java parity: model/gameobjects/player/Player.addItemCoolDown.
		if (useDelayMillis <= 0)
			return;

		var cooldowns = new Dictionary<int, PlayerItemCooldown>(ItemCooldowns)
		{
			[delayId] = new PlayerItemCooldown(now.ToUnixTimeMilliseconds() + useDelayMillis, useDelayMillis / 1000),
		};
		ItemCooldowns = cooldowns;
	}

	public void RemoveItemCooldown(int delayId)
	{
		// Java parity: model/gameobjects/player/Player.removeItemCoolDown.
		if (!ItemCooldowns.ContainsKey(delayId))
			return;

		var cooldowns = new Dictionary<int, PlayerItemCooldown>(ItemCooldowns);
		cooldowns.Remove(delayId);
		ItemCooldowns = cooldowns;
	}
}

public enum PlayerLeaveFlyAreaStatus
{
	NoChange,
	FreeFlightAccess,
	ContinueGliding,
	EndedFlying,
	GlidingOutsideFlyArea,
}

public enum PlayerCastingSkillMethod
{
	None,
	Cast,
	Item,
}

public sealed record PlayerCastingSkillSnapshot(
	int SkillId,
	PlayerCastingSkillMethod Method,
	int ItemObjectId,
	int ItemTemplateId,
	int FirstTargetObjectId,
	int? ItemCooldownDelayId)
{
	public bool HasItemCancellationMetadata => ItemObjectId != 0 && ItemTemplateId != 0 && FirstTargetObjectId != 0;
}

public sealed record PendingPlayerTeleport(WorldPosition Destination, TeleportAnimation Animation);
