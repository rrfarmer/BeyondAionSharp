using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class AutoGroupInstanceLeavePlanServiceTests
{
	[Fact]
	public void CreatePlan_SkipsAllWorkWhenJavaAutoGroupConfigDisabled()
	{
		var plan = AutoGroupInstanceLeavePlanService.CreatePlan(new AutoGroupInstanceLeaveFacts(
			PlayerObjectId: 1001,
			AutoGroupEnabled: false,
			HasAutoInstanceForCurrentMap: true,
			IsRegisteredAutoGroupPlayer: true,
			AutoGroupInstanceKind.PvpRaceInstance,
			RegisteredPlayerCountBeforeLeave: 1,
			OnlinePlayersInsideAfterLeave: 0,
			QuickRegistrationAllowed: true,
			PlayerIsInGroup: true,
			PlayerIsInAlliance: false));

		Assert.Equal(AutoGroupInstanceLeaveStatus.AutoGroupDisabled, plan.Status);
		Assert.False(plan.WouldInvokeAutoInstanceLeave);
		Assert.False(plan.WouldCheckOpenRegistrations);
		Assert.Contains("AUTO_GROUP_ENABLE", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_RefreshesOpenRegistrationsWhenNoRegisteredAutoInstancePlayerLikeJavaService()
	{
		var noInstance = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			hasAutoInstanceForCurrentMap: false,
			isRegisteredAutoGroupPlayer: false));
		var notRegistered = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			hasAutoInstanceForCurrentMap: true,
			isRegisteredAutoGroupPlayer: false));

		Assert.Equal(AutoGroupInstanceLeaveStatus.NoAutoInstanceForMap, noInstance.Status);
		Assert.False(noInstance.WouldInvokeAutoInstanceLeave);
		Assert.True(noInstance.WouldCheckOpenRegistrations);
		Assert.Equal(AutoGroupInstanceLeaveStatus.PlayerNotRegistered, notRegistered.Status);
		Assert.False(notRegistered.WouldUnregisterPlayer);
		Assert.True(notRegistered.WouldCheckOpenRegistrations);
		Assert.Contains("PeriodicInstanceManager.checkAndSendOpenRegistrations", notRegistered.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_PvpRaceLeaveUnregistersAndRemovesCurrentTeamLikeJavaAutoPvpInstance()
	{
		var groupPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			instanceKind: AutoGroupInstanceKind.PvpRaceInstance,
			playerIsInGroup: true,
			playerIsInAlliance: true,
			registeredPlayerCountBeforeLeave: 4,
			onlinePlayersInsideAfterLeave: 3,
			quickRegistrationAllowed: true));
		var alliancePlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			instanceKind: AutoGroupInstanceKind.PvpRaceInstance,
			playerIsInGroup: false,
			playerIsInAlliance: true,
			registeredPlayerCountBeforeLeave: 4,
			onlinePlayersInsideAfterLeave: 3,
			quickRegistrationAllowed: false));

		Assert.Equal(AutoGroupInstanceLeaveStatus.RegisteredPlayerLeft, groupPlan.Status);
		Assert.True(groupPlan.WouldInvokeAutoInstanceLeave);
		Assert.True(groupPlan.WouldUnregisterPlayer);
		Assert.True(groupPlan.WouldRemoveGroup);
		Assert.False(groupPlan.WouldRemoveAlliance);
		Assert.True(groupPlan.WouldCheckQuickEntries);
		Assert.Contains("AutoPvpInstance.onLeaveInstance", groupPlan.JavaSource, StringComparison.Ordinal);
		Assert.False(alliancePlan.WouldRemoveGroup);
		Assert.True(alliancePlan.WouldRemoveAlliance);
		Assert.False(alliancePlan.WouldCheckQuickEntries);
	}

	[Fact]
	public void CreatePlan_ArenaSubtypeLeaveBranchesMatchJavaUnregisterAndGroupCleanup()
	{
		var ffaPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			instanceKind: AutoGroupInstanceKind.FreeForAllArena,
			playerIsInGroup: true));
		var harmonyPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			instanceKind: AutoGroupInstanceKind.HarmonyArena,
			playerIsInGroup: true));

		Assert.True(ffaPlan.WouldUnregisterPlayer);
		Assert.False(ffaPlan.WouldRemoveGroup);
		Assert.False(ffaPlan.WouldRemoveAlliance);
		Assert.False(ffaPlan.WouldRemoveHarmonyGroupTracking);
		Assert.Contains("AutoPvPFFAInstance.onLeaveInstance", ffaPlan.JavaSource, StringComparison.Ordinal);
		Assert.True(harmonyPlan.WouldUnregisterPlayer);
		Assert.True(harmonyPlan.WouldRemoveGroup);
		Assert.True(harmonyPlan.WouldRemoveHarmonyGroupTracking);
		Assert.Contains("AutoHarmonyInstance.onLeaveInstance", harmonyPlan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_DestroysOnlyWhenRegisteredPlayersAndOnlinePlayersAreEmptyLikeJavaDestroyIfPossible()
	{
		var destroyPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			registeredPlayerCountBeforeLeave: 1,
			onlinePlayersInsideAfterLeave: 0,
			quickRegistrationAllowed: true));
		var refillPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			registeredPlayerCountBeforeLeave: 2,
			onlinePlayersInsideAfterLeave: 0,
			quickRegistrationAllowed: true));
		var onlinePlayerPlan = AutoGroupInstanceLeavePlanService.CreatePlan(ValidFacts(
			registeredPlayerCountBeforeLeave: 1,
			onlinePlayersInsideAfterLeave: 1,
			quickRegistrationAllowed: true));

		Assert.True(destroyPlan.WouldDestroyInstance);
		Assert.False(destroyPlan.WouldCheckQuickEntries);
		Assert.Contains("InstanceService.destroyInstance", destroyPlan.JavaSource, StringComparison.Ordinal);
		Assert.False(refillPlan.WouldDestroyInstance);
		Assert.True(refillPlan.WouldCheckQuickEntries);
		Assert.False(onlinePlayerPlan.WouldDestroyInstance);
		Assert.True(onlinePlayerPlan.WouldCheckQuickEntries);
	}

	private static AutoGroupInstanceLeaveFacts ValidFacts(
		bool autoGroupEnabled = true,
		bool hasAutoInstanceForCurrentMap = true,
		bool isRegisteredAutoGroupPlayer = true,
		AutoGroupInstanceKind instanceKind = AutoGroupInstanceKind.PvpRaceInstance,
		int registeredPlayerCountBeforeLeave = 2,
		int onlinePlayersInsideAfterLeave = 1,
		bool quickRegistrationAllowed = false,
		bool playerIsInGroup = false,
		bool playerIsInAlliance = false)
	{
		return new AutoGroupInstanceLeaveFacts(
			PlayerObjectId: 1001,
			autoGroupEnabled,
			hasAutoInstanceForCurrentMap,
			isRegisteredAutoGroupPlayer,
			instanceKind,
			registeredPlayerCountBeforeLeave,
			onlinePlayersInsideAfterLeave,
			quickRegistrationAllowed,
			playerIsInGroup,
			playerIsInAlliance);
	}
}
