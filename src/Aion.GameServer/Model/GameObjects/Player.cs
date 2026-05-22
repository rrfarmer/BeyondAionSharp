using Aion.GameServer.Model.Account;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed class Player
{
	public const byte MailboxClosedState = 0;
	public const byte MailboxRegularState = 1;
	public const byte MailboxExpressState = 2;

	public int ObjectId { get; init; }

	public int AccountId { get; init; }

	// Java parity: model/gameobjects/player/PlayerAccount.accessLevel used by AdminService.hasAccess checks.
	public byte AccessLevel { get; set; }

	// Java parity: model/account/PlayerAccount.membership consumed by chat/player-info packets.
	public byte AccountMembership { get; set; }

	// Java parity: model/team/legion/LegionMember data used by chat/player info packets.
	public int LegionId { get; set; }

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

	public long Exp { get; init; }

	public long RecoverableExp { get; init; }

	public int Dp { get; init; }

	public long ReposeEnergy { get; init; }

	public bool IsOnline { get; set; }

	public DateTime? LastOnline { get; set; }

	public int NpcExpands { get; init; }

	public int QuestExpands { get; init; }

	public int ItemExpands { get; set; }

	public int WarehouseNpcExpands { get; init; }

	public int WarehouseBonusExpands { get; set; }

	public int TitleId { get; set; }

	public int BonusTitleId { get; set; }

	public WorldPosition Position { get; set; }

	// Java parity: controllers/movement/PlayerMoveController state mirrored for CM_MOVE/SM_MOVE.
	public PlayerMovementState Movement { get; } = new();

	// Java parity: model/gameobjects/Creature.state queried through Creature.isInState(CreatureState).
	public PlayerCreatureState CreatureState { get; set; }

	// Java parity: controllers/effect/EffectController.abnormals queried by CM_EMOTION and action guards.
	public PlayerAbnormalState AbnormalState { get; set; }

	// Java parity: model/actions/PlayerMode.RIDE queried through Player.isInPlayerMode.
	public bool IsInRideMode { get; set; }

	// Java parity: model/gameobjects/player/Player.ride stores the active RideInfo while mounted.
	public PlayerRideInfo? RideInfo { get; set; }

	// Java parity: model/gameobjects/player/Player.isInSprintMode toggled by CM_EMOTION START_SPRINT/END_SPRINT.
	public bool IsInSprintMode { get; set; }

	// Java parity: model/gameobjects/player/Player.flightPath stores active transporter/windstream mode.
	public PlayerFlightPathType? FlightPathType { get; set; }

	// Java parity: PlayerLifeStats.triggerFpReduce/triggerFpRestore task intent; timer execution is a later Phase 6 slice.
	public bool IsFpReduceActive { get; set; }

	public bool IsFpRestoreActive { get; set; }

	// Java parity: controllers/PlayerController.stanceObserver represented by its active stance skill id.
	public int StanceSkillId { get; set; }

	// Java parity: model/gameobjects/VisibleObject target set by network/aion/clientpackets/CM_TARGET_SELECT.
	public int TargetObjectId { get; set; }

	// Java parity: model/gameobjects/player/Player.isTrading guard used by mail and broker packets.
	public bool IsTrading { get; set; }

	public IReadOnlyList<InventoryItem> InventoryItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> WarehouseItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<InventoryItem> AccountWarehouseItems { get; set; } = Array.Empty<InventoryItem>();

	public IReadOnlyList<PlayerSkill> Skills { get; set; } = Array.Empty<PlayerSkill>();

	public IReadOnlyDictionary<int, long> SkillCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerItemCooldown> ItemCooldowns { get; set; } = new Dictionary<int, PlayerItemCooldown>();

	public IReadOnlyList<PlayerQuestState> Quests { get; set; } = Array.Empty<PlayerQuestState>();

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

	public PlayerBrokerSettlementSummary BrokerSettlements { get; set; } = PlayerBrokerSettlementSummary.Empty;

	// Java parity: model/broker/BrokerPlayerCache remembers the last broker list/search for refresh after buy.
	public int BrokerMaskCache { get; set; }

	public byte BrokerSortTypeCache { get; set; }

	public int BrokerStartPageCache { get; set; }

	public IReadOnlyList<int> BrokerSearchItemIds { get; set; } = Array.Empty<int>();

	public IReadOnlyList<PlayerHouse> Houses { get; set; } = Array.Empty<PlayerHouse>();

	public IReadOnlyDictionary<int, long> CraftCooldowns { get; set; } = new Dictionary<int, long>();

	public IReadOnlyDictionary<int, PlayerPortalCooldown> PortalCooldowns { get; set; } = new Dictionary<int, PlayerPortalCooldown>();

	public PlayerLifeStats? LifeStats { get; set; }

	public IReadOnlyList<PlayerFriend> Friends { get; set; } = Array.Empty<PlayerFriend>();

	// Java parity: model/gameobjects/player/FriendList.Status changed by CM_FRIEND_STATUS.
	public byte FriendListStatus { get; set; }

	// Java parity: model/gameobjects/player/ResponseRequester stores pending SM_QUESTION_WINDOW handlers.
	public PendingFriendRequest? PendingFriendRequest { get; set; }

	public PendingChargeAllRequest? PendingChargeAllRequest { get; set; }

	public PendingSoulBindRequest? PendingSoulBindRequest { get; set; }

	// Java parity: Player.usingItem set by SM_ITEM_USAGE_ANIMATION while delayed item use is active.
	public int UsingItemObjectId { get; set; }

	public IReadOnlyList<PlayerBlockedUser> BlockedUsers { get; set; } = Array.Empty<PlayerBlockedUser>();

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

	public bool IsUnderStance()
	{
		// Java parity: controllers/PlayerController.isUnderStance.
		return StanceSkillId != 0;
	}

	public bool IsFlying()
	{
		// Java parity: model/gameobjects/player/Player.isFlying, represented by current C# flying/gliding creature-state bits.
		return IsInState(PlayerCreatureState.Flying) || IsInState(PlayerCreatureState.Gliding);
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
		SetCreatureState(PlayerCreatureState.Flying, enabled: true);
		if (IsInRideMode)
			SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: true);
		TriggerFpReduce();
	}

	public void EndFlying()
	{
		// Java parity: controllers/FlyController.endFly state and FP-restore side effects.
		SetCreatureState(PlayerCreatureState.Flying, enabled: false);
		SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		SetCreatureState(PlayerCreatureState.FloatingCorpse, enabled: false);
		TriggerFpRestore();
	}

	public bool StopGliding()
	{
		// Java parity: controllers/FlyController.onStopGliding.
		if (!IsInState(PlayerCreatureState.Gliding))
			return false;

		SetCreatureState(PlayerCreatureState.Gliding, enabled: false);
		if (IsInState(PlayerCreatureState.Flying))
		{
			TriggerFpReduce();
			return false;
		}

		TriggerFpRestore();
		return true;
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
