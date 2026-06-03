using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum AutoGroupRegistrationGuardPlanStatus
{
	CanRegister,
	BlockedLevelOutOfRange,
	BlockedPvPArenaUnavailable,
	BlockedCooldown,
	BlockedEntryUnsupported,
	BlockedNotLeader,
	BlockedTooManyMembers,
	BlockedMemberCannotEnter,
}

public sealed record AutoGroupRegistrationGuardPlan(
	AutoGroupRegistrationGuardPlanStatus Status,
	int PlayerLevel,
	SmSystemMessage? DenialMessage,
	bool CanRegister,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AutoGroupRegistrationGuardPlanService
{
	public static AutoGroupRegistrationGuardPlan CreatePlan(
		int playerLevel,
		int minLevel,
		int maxLevel,
		bool isPvPArenaType,
		bool pvpArenaAvailable,
		bool hasCooldown)
	{
		// Java parity: services/AutoGroupService.canRegister early guard sequence.
		// !agt.isInLvlRange(player.getLevel()) → level check first.
		// PvP arena availability check second.
		// Cooldown check third.
		if (playerLevel < minLevel || playerLevel > maxLevel)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedLevelOutOfRange,
				playerLevel,
				SmSystemMessage.CantInstanceEnterLevel(),
				CanRegister: false,
				$"AutoGroupService.canRegister -> !isInLvlRange({playerLevel}, {minLevel}-{maxLevel}) -> STR_MSG_CANT_INSTANCE_ENTER_LEVEL"
			);
		}

		if (isPvPArenaType && !pvpArenaAvailable)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedPvPArenaUnavailable,
				playerLevel,
				DenialMessage: null,
				CanRegister: false,
				"AutoGroupService.canRegister -> isPvPArena && !isPvPArenaAvailable(player, agt) -> return false (no system message)"
			);
		}

		if (hasCooldown)
		{
			return new AutoGroupRegistrationGuardPlan(
				AutoGroupRegistrationGuardPlanStatus.BlockedCooldown,
				playerLevel,
				SmSystemMessage.CannotMakeInstanceCoolTime(),
				CanRegister: false,
				"AutoGroupService.canRegister -> AutoGroupUtility.hasCoolDown(player, mapId) -> STR_MSG_CANNOT_MAKE_INSTANCE_COOL_TIME"
			);
		}

		return new AutoGroupRegistrationGuardPlan(
			AutoGroupRegistrationGuardPlanStatus.CanRegister,
			playerLevel,
			DenialMessage: null,
			CanRegister: true,
			$"AutoGroupService.canRegister -> all common guards passed (level={playerLevel}, range={minLevel}-{maxLevel})"
		);
	}
}
