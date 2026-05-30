using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PrivateStoreOpenGuardPlanStatus
{
	CanOpen,
	BlockedFlying,
	BlockedInMove,
	BlockedInCombatMode,
	BlockedTrading,
	BlockedRideOrRobotMode,
	BlockedHidden,
	BlockedDead,
	BlockedInChair,
	BlockedStoreAlreadyOpen,
}

public sealed record PrivateStoreOpenGuardPlan(
	PrivateStoreOpenGuardPlanStatus Status,
	SmSystemMessage? DenialMessage,
	bool CanOpen,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class PrivateStoreOpenGuardPlanService
{
	public static PrivateStoreOpenGuardPlan CreatePlan(
		bool isFlying,
		bool isInMove,
		bool isInCombatMode,
		bool isTrading,
		bool isInRideOrRobotMode,
		bool isHidden,
		bool isDead,
		bool isInChairState,
		bool storeAlreadyOpen)
	{
		// Java parity: services/PrivateStoreService.canOpenPrivateStore guard order.
		// Guards with system messages fire first; silent guards (dead, chair, store-open) follow.
		if (isFlying)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedFlying,
				SmSystemMessage.PersonalShopDisabledInFlyMode(),
				"PrivateStoreService.canOpenPrivateStore -> player.isFlying() -> STR_PERSONAL_SHOP_DISABLED_IN_FLY_MODE");

		if (isInMove)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedInMove,
				SmSystemMessage.PersonalShopDisabledInMovingObject(),
				"PrivateStoreService.canOpenPrivateStore -> getMoveController().isInMove() -> STR_PERSONAL_SHOP_DISABLED_IN_MOVING_OBJECT");

		if (isInCombatMode)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedInCombatMode,
				SmSystemMessage.PersonalShopDisabledInCombatMode(),
				"PrivateStoreService.canOpenPrivateStore -> player.isInAttackMode() -> STR_PERSONAL_SHOP_DISABLED_IN_COMBAT_MODE");

		if (isTrading)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedTrading,
				SmSystemMessage.CantOpenStoreDuringCrafting(),
				"PrivateStoreService.canOpenPrivateStore -> player.isTrading() -> STR_MSG_CANT_OPEN_STORE_DURING_CRAFTING");

		if (isInRideOrRobotMode)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedRideOrRobotMode,
				SmSystemMessage.PersonalShopRestrictionRide(),
				"PrivateStoreService.canOpenPrivateStore -> isInPlayerMode(RIDE)||isInRobotMode() -> STR_MSG_PERSONAL_SHOP_RESTRICTION_RIDE");

		if (isHidden)
			return Block(PrivateStoreOpenGuardPlanStatus.BlockedHidden,
				SmSystemMessage.PersonalShopDisabledInHiddenMode(),
				"PrivateStoreService.canOpenPrivateStore -> effectController.isAbnormalSet(HIDE) -> STR_PERSONAL_SHOP_DISABLED_IN_HIDDEN_MODE");

		if (isDead)
			return SilentBlock(PrivateStoreOpenGuardPlanStatus.BlockedDead,
				"PrivateStoreService.canOpenPrivateStore -> player.isDead() -> return false");

		if (isInChairState)
			return SilentBlock(PrivateStoreOpenGuardPlanStatus.BlockedInChair,
				"PrivateStoreService.canOpenPrivateStore -> player.isInState(CHAIR) -> return false");

		if (storeAlreadyOpen)
			return SilentBlock(PrivateStoreOpenGuardPlanStatus.BlockedStoreAlreadyOpen,
				"PrivateStoreService.canOpenPrivateStore -> player.getStore() != null -> return false");

		return new PrivateStoreOpenGuardPlan(
			PrivateStoreOpenGuardPlanStatus.CanOpen,
			DenialMessage: null,
			CanOpen: true,
			"PrivateStoreService.canOpenPrivateStore -> all guards passed -> return true");
	}

	private static PrivateStoreOpenGuardPlan Block(PrivateStoreOpenGuardPlanStatus status, SmSystemMessage message, string source)
	{
		return new PrivateStoreOpenGuardPlan(status, message, CanOpen: false, source);
	}

	private static PrivateStoreOpenGuardPlan SilentBlock(PrivateStoreOpenGuardPlanStatus status, string source)
	{
		return new PrivateStoreOpenGuardPlan(status, DenialMessage: null, CanOpen: false, source);
	}
}
