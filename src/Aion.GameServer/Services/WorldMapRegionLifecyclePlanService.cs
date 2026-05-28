namespace Aion.GameServer.Services;

public static class WorldMapRegionLifecyclePlanService
{
	public const int TransidiumAnnexWorldId = 400030000;
	public static readonly TimeSpan JavaDeactivationDelay = TimeSpan.FromSeconds(60);

	public static WorldMapRegionLifecyclePlan CreateAddPlan(
		WorldMapRegionLifecycleContext context,
		bool objectAlreadyPresent,
		bool isPlayer)
	{
		// Java parity breadcrumb: MapRegion.add puts the object first; only a new
		// Player whose incremented count becomes 1 activates self and neighbours.
		if (objectAlreadyPresent)
			return WorldMapRegionLifecyclePlan.NoChange(WorldMapRegionLifecycleAction.IgnoreExistingObject, context.SelfRegion.PlayerCount);
		if (!isPlayer)
			return WorldMapRegionLifecyclePlan.NoChange(WorldMapRegionLifecycleAction.AddNonPlayerObject, context.SelfRegion.PlayerCount);

		var newPlayerCount = context.SelfRegion.PlayerCount + 1;
		if (newPlayerCount != 1)
			return WorldMapRegionLifecyclePlan.PlayerCountChanged(WorldMapRegionLifecycleAction.AddPlayerObject, newPlayerCount);

		var activatedRegionIds = context.NeighboursIncludingSelf
			.Where(region => !region.IsActive)
			.Select(region => region.RegionId)
			.ToArray();

		return new WorldMapRegionLifecyclePlan(
			WorldMapRegionLifecycleAction.ActivateSelfAndNeighbours,
			newPlayerCount,
			ShouldScheduleDeactivation: false,
			DeactivationDelay: null,
			ShouldClearDeactivationPending: false,
			ActivatedRegionIds: activatedRegionIds,
			DeactivatedRegionIds: Array.Empty<int>(),
			BlockedReason: null,
			JavaSource: "MapRegion.add -> incrementPlayerCount == 1 -> activate");
	}

	public static WorldMapRegionLifecyclePlan CreateRemovePlan(
		WorldMapRegionLifecycleContext context,
		bool objectWasPresent,
		bool wasPlayer)
	{
		// Java parity breadcrumb: MapRegion.remove schedules deactivation only when
		// a removed Player decrements the count to 0.
		if (!objectWasPresent)
			return WorldMapRegionLifecyclePlan.NoChange(WorldMapRegionLifecycleAction.IgnoreMissingObject, context.SelfRegion.PlayerCount);
		if (!wasPlayer)
			return WorldMapRegionLifecyclePlan.NoChange(WorldMapRegionLifecycleAction.RemoveNonPlayerObject, context.SelfRegion.PlayerCount);

		var newPlayerCount = context.SelfRegion.PlayerCount == 0 ? 0 : context.SelfRegion.PlayerCount - 1;
		if (newPlayerCount != 0)
			return WorldMapRegionLifecyclePlan.PlayerCountChanged(WorldMapRegionLifecycleAction.RemovePlayerObject, newPlayerCount);
		if (context.SelfRegion.DeactivationPending)
		{
			return new WorldMapRegionLifecyclePlan(
				WorldMapRegionLifecycleAction.DeactivationAlreadyPending,
				newPlayerCount,
				ShouldScheduleDeactivation: false,
				DeactivationDelay: null,
				ShouldClearDeactivationPending: false,
				ActivatedRegionIds: Array.Empty<int>(),
				DeactivatedRegionIds: Array.Empty<int>(),
				BlockedReason: null,
				JavaSource: "MapRegion.scheduleDeactivation returns when deactivationPending is true");
		}

		return new WorldMapRegionLifecyclePlan(
			WorldMapRegionLifecycleAction.ScheduleDeactivation,
			newPlayerCount,
			ShouldScheduleDeactivation: true,
			DeactivationDelay: JavaDeactivationDelay,
			ShouldClearDeactivationPending: false,
			ActivatedRegionIds: Array.Empty<int>(),
			DeactivatedRegionIds: Array.Empty<int>(),
			BlockedReason: null,
			JavaSource: "MapRegion.remove -> decrementPlayerCount == 0 -> scheduleDeactivation(60s)");
	}

	public static WorldMapRegionLifecyclePlan CreateScheduledDeactivationPlan(WorldMapRegionLifecycleContext context)
	{
		// Java parity breadcrumb: scheduled task clears deactivationPending, then
		// if self still has no players calls tryDeactivate for self and neighbours.
		if (context.SelfRegion.PlayerCount != 0)
			return BlockScheduledDeactivation(context, WorldMapRegionLifecycleBlockedReason.SelfRegionHasPlayers);
		if (context.IsInstanceType || context.MapId == TransidiumAnnexWorldId)
			return BlockScheduledDeactivation(context, WorldMapRegionLifecycleBlockedReason.InstanceOrTransidiumAnnex);
		if (context.NeighboursIncludingSelf.Any(region => region.PlayerCount > 0))
			return BlockScheduledDeactivation(context, WorldMapRegionLifecycleBlockedReason.NeighbourHasPlayers);

		var deactivatedRegionIds = context.NeighboursIncludingSelf
			.Where(region => region.IsActive)
			.Select(region => region.RegionId)
			.ToArray();

		return new WorldMapRegionLifecyclePlan(
			WorldMapRegionLifecycleAction.DeactivateSelfAndNeighbours,
			context.SelfRegion.PlayerCount,
			ShouldScheduleDeactivation: false,
			DeactivationDelay: null,
			ShouldClearDeactivationPending: true,
			ActivatedRegionIds: Array.Empty<int>(),
			DeactivatedRegionIds: deactivatedRegionIds,
			BlockedReason: null,
			JavaSource: "MapRegion.scheduleDeactivation task -> tryDeactivate for neighboursIncludingSelf");
	}

	private static WorldMapRegionLifecyclePlan BlockScheduledDeactivation(
		WorldMapRegionLifecycleContext context,
		WorldMapRegionLifecycleBlockedReason reason)
	{
		return new WorldMapRegionLifecyclePlan(
			WorldMapRegionLifecycleAction.DeactivationBlocked,
			context.SelfRegion.PlayerCount,
			ShouldScheduleDeactivation: false,
			DeactivationDelay: null,
			ShouldClearDeactivationPending: true,
			ActivatedRegionIds: Array.Empty<int>(),
			DeactivatedRegionIds: Array.Empty<int>(),
			BlockedReason: reason,
			JavaSource: "MapRegion.scheduleDeactivation task clears pending before tryDeactivate guards");
	}
}

public sealed record WorldMapRegionLifecycleContext(
	int MapId,
	bool IsInstanceType,
	WorldMapRegionLifecycleRegionState SelfRegion,
	IReadOnlyList<WorldMapRegionLifecycleRegionState> Neighbours)
{
	public IReadOnlyList<WorldMapRegionLifecycleRegionState> NeighboursIncludingSelf =>
		[SelfRegion, .. Neighbours];
}

public sealed record WorldMapRegionLifecycleRegionState(
	int RegionId,
	bool IsActive,
	int PlayerCount,
	bool DeactivationPending = false);

public sealed record WorldMapRegionLifecyclePlan(
	WorldMapRegionLifecycleAction Action,
	int ResultingSelfPlayerCount,
	bool ShouldScheduleDeactivation,
	TimeSpan? DeactivationDelay,
	bool ShouldClearDeactivationPending,
	IReadOnlyList<int> ActivatedRegionIds,
	IReadOnlyList<int> DeactivatedRegionIds,
	WorldMapRegionLifecycleBlockedReason? BlockedReason,
	string JavaSource)
{
	public static WorldMapRegionLifecyclePlan NoChange(
		WorldMapRegionLifecycleAction action,
		int playerCount)
	{
		return new WorldMapRegionLifecyclePlan(
			action,
			playerCount,
			ShouldScheduleDeactivation: false,
			DeactivationDelay: null,
			ShouldClearDeactivationPending: false,
			ActivatedRegionIds: Array.Empty<int>(),
			DeactivatedRegionIds: Array.Empty<int>(),
			BlockedReason: null,
			JavaSource: "MapRegion.add/remove object guard did not change player lifecycle state");
	}

	public static WorldMapRegionLifecyclePlan PlayerCountChanged(
		WorldMapRegionLifecycleAction action,
		int playerCount)
	{
		return new WorldMapRegionLifecyclePlan(
			action,
			playerCount,
			ShouldScheduleDeactivation: false,
			DeactivationDelay: null,
			ShouldClearDeactivationPending: false,
			ActivatedRegionIds: Array.Empty<int>(),
			DeactivatedRegionIds: Array.Empty<int>(),
			BlockedReason: null,
			JavaSource: "MapRegion player count changed without crossing activation/deactivation threshold");
	}
}

public enum WorldMapRegionLifecycleAction
{
	IgnoreExistingObject,
	AddNonPlayerObject,
	AddPlayerObject,
	ActivateSelfAndNeighbours,
	IgnoreMissingObject,
	RemoveNonPlayerObject,
	RemovePlayerObject,
	ScheduleDeactivation,
	DeactivationAlreadyPending,
	DeactivateSelfAndNeighbours,
	DeactivationBlocked,
}

public enum WorldMapRegionLifecycleBlockedReason
{
	SelfRegionHasPlayers,
	NeighbourHasPlayers,
	InstanceOrTransidiumAnnex,
}
