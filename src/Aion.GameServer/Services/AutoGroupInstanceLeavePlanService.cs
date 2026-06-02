namespace Aion.GameServer.Services;

public static class AutoGroupInstanceLeavePlanService
{
	public static AutoGroupInstanceLeavePlan CreatePlan(AutoGroupInstanceLeaveFacts facts)
	{
		// Java parity: InstanceService.onLeaveInstance gates AutoGroupService.onLeaveInstance behind AUTO_GROUP_ENABLE.
		if (!facts.AutoGroupEnabled)
		{
			return new AutoGroupInstanceLeavePlan(
				AutoGroupInstanceLeaveStatus.AutoGroupDisabled,
				facts.PlayerObjectId,
				facts.InstanceKind,
				WouldInvokeAutoInstanceLeave: false,
				WouldUnregisterPlayer: false,
				WouldRemoveGroup: false,
				WouldRemoveAlliance: false,
				WouldRemoveHarmonyGroupTracking: false,
				WouldDestroyInstance: false,
				WouldCheckQuickEntries: false,
				WouldCheckOpenRegistrations: false,
				"InstanceService.onLeaveInstance -> if (AutoGroupConfig.AUTO_GROUP_ENABLE) is false");
		}

		if (!facts.HasAutoInstanceForCurrentMap)
		{
			return PlanWithoutRegisteredPlayer(
				facts,
				AutoGroupInstanceLeaveStatus.NoAutoInstanceForMap,
				"AutoGroupService.onLeaveInstance -> autoInstances.get(player.getWorldMapInstance()) returns null; PeriodicInstanceManager.checkAndSendOpenRegistrations(player)");
		}

		if (!facts.IsRegisteredAutoGroupPlayer)
		{
			return PlanWithoutRegisteredPlayer(
				facts,
				AutoGroupInstanceLeaveStatus.PlayerNotRegistered,
				"AutoGroupService.onLeaveInstance -> autoInstance exists but registeredAGPlayers does not contain player; PeriodicInstanceManager.checkAndSendOpenRegistrations(player)");
		}

		var remainingRegisteredPlayers = Math.Max(0, facts.RegisteredPlayerCountBeforeLeave - 1);
		var wouldDestroy = remainingRegisteredPlayers == 0 && facts.OnlinePlayersInsideAfterLeave == 0;
		var wouldCheckQuickEntries = !wouldDestroy && facts.QuickRegistrationAllowed;
		var removeGroup = facts.InstanceKind switch
		{
			AutoGroupInstanceKind.PvpRaceInstance or AutoGroupInstanceKind.HarmonyArena => facts.PlayerIsInGroup,
			_ => false,
		};
		var removeAlliance = facts.InstanceKind == AutoGroupInstanceKind.PvpRaceInstance
			&& !facts.PlayerIsInGroup
			&& facts.PlayerIsInAlliance;
		var removeHarmonyGroupTracking = facts.InstanceKind == AutoGroupInstanceKind.HarmonyArena;

		return new AutoGroupInstanceLeavePlan(
			AutoGroupInstanceLeaveStatus.RegisteredPlayerLeft,
			facts.PlayerObjectId,
			facts.InstanceKind,
			WouldInvokeAutoInstanceLeave: true,
			WouldUnregisterPlayer: true,
			WouldRemoveGroup: removeGroup,
			WouldRemoveAlliance: removeAlliance,
			WouldRemoveHarmonyGroupTracking: removeHarmonyGroupTracking,
			WouldDestroyInstance: wouldDestroy,
			WouldCheckQuickEntries: wouldCheckQuickEntries,
			WouldCheckOpenRegistrations: true,
			CreateRegisteredJavaSource(facts.InstanceKind, wouldDestroy, wouldCheckQuickEntries));
	}

	private static AutoGroupInstanceLeavePlan PlanWithoutRegisteredPlayer(
		AutoGroupInstanceLeaveFacts facts,
		AutoGroupInstanceLeaveStatus status,
		string javaSource)
	{
		return new AutoGroupInstanceLeavePlan(
			status,
			facts.PlayerObjectId,
			facts.InstanceKind,
			WouldInvokeAutoInstanceLeave: false,
			WouldUnregisterPlayer: false,
			WouldRemoveGroup: false,
			WouldRemoveAlliance: false,
			WouldRemoveHarmonyGroupTracking: false,
			WouldDestroyInstance: false,
			WouldCheckQuickEntries: false,
			WouldCheckOpenRegistrations: true,
			javaSource);
	}

	private static string CreateRegisteredJavaSource(
		AutoGroupInstanceKind instanceKind,
		bool wouldDestroy,
		bool wouldCheckQuickEntries)
	{
		var subtypeSource = instanceKind switch
		{
			AutoGroupInstanceKind.PvpRaceInstance => "AutoPvpInstance.onLeaveInstance -> unregister; remove group or alliance",
			AutoGroupInstanceKind.FreeForAllArena => "AutoPvPFFAInstance.onLeaveInstance -> unregister",
			AutoGroupInstanceKind.HarmonyArena => "AutoHarmonyInstance.onLeaveInstance -> unregister from harmony groups; remove group",
			_ => "AutoInstance.onLeaveInstance -> no-op; AutoGroupService still runs destroy/refill/open-registration flow",
		};
		var destroySource = wouldDestroy
			? "destroyOrAddPlayersFromQuickEntries -> destroyIfPossible removes auto instance and calls InstanceService.destroyInstance"
			: wouldCheckQuickEntries
				? "destroyOrAddPlayersFromQuickEntries -> not destroyed and quick registration allowed, checkQueueForQuickEntries"
				: "destroyOrAddPlayersFromQuickEntries -> not destroyed and quick registration not allowed";
		return $"AutoGroupService.onLeaveInstance -> {subtypeSource}; {destroySource}; PeriodicInstanceManager.checkAndSendOpenRegistrations(player)";
	}
}

public sealed record AutoGroupInstanceLeaveFacts(
	int PlayerObjectId,
	bool AutoGroupEnabled,
	bool HasAutoInstanceForCurrentMap,
	bool IsRegisteredAutoGroupPlayer,
	AutoGroupInstanceKind InstanceKind,
	int RegisteredPlayerCountBeforeLeave,
	int OnlinePlayersInsideAfterLeave,
	bool QuickRegistrationAllowed,
	bool PlayerIsInGroup,
	bool PlayerIsInAlliance);

public sealed record AutoGroupInstanceLeavePlan(
	AutoGroupInstanceLeaveStatus Status,
	int PlayerObjectId,
	AutoGroupInstanceKind InstanceKind,
	bool WouldInvokeAutoInstanceLeave,
	bool WouldUnregisterPlayer,
	bool WouldRemoveGroup,
	bool WouldRemoveAlliance,
	bool WouldRemoveHarmonyGroupTracking,
	bool WouldDestroyInstance,
	bool WouldCheckQuickEntries,
	bool WouldCheckOpenRegistrations,
	string JavaSource);

public enum AutoGroupInstanceLeaveStatus
{
	AutoGroupDisabled,
	NoAutoInstanceForMap,
	PlayerNotRegistered,
	RegisteredPlayerLeft,
}

public enum AutoGroupInstanceKind
{
	Base,
	PvpRaceInstance,
	FreeForAllArena,
	HarmonyArena,
}
