namespace Aion.GameServer.Services;

public enum VortexRiftEntryUpdatePlayerTargetPlanStatus
{
	MissingWorldTargetPlan,
	NoWorldTargets,
	NoOnlinePlayers,
	Planned,
}

public sealed record VortexRiftEntryUpdateOnlinePlayerSnapshot(
	int PlayerObjectId,
	int WorldId);

public sealed record VortexRiftEntryUpdatePlayerTargetPlan(
	VortexRiftEntryUpdatePlayerTargetPlanStatus Status,
	VortexRiftEntryUpdateWorldTargetPlan? WorldTargetPlan,
	IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> OnlinePlayers,
	IReadOnlyList<int> TargetPlayerObjectIds,
	string JavaSource);

public static class VortexRiftEntryUpdatePlayerTargetPlanService
{
	public static VortexRiftEntryUpdatePlayerTargetPlan CreatePlan(
		VortexRiftEntryUpdateWorldTargetPlan? worldTargetPlan,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers)
	{
		ArgumentNullException.ThrowIfNull(onlinePlayers);

		if (worldTargetPlan == null)
		{
			return CreateResult(
				VortexRiftEntryUpdatePlayerTargetPlanStatus.MissingWorldTargetPlan,
				null,
				onlinePlayers,
				[]);
		}

		if (worldTargetPlan.Status != VortexRiftEntryUpdateWorldTargetPlanStatus.Planned
			|| worldTargetPlan.WorldIds.Count == 0)
		{
			return CreateResult(
				VortexRiftEntryUpdatePlayerTargetPlanStatus.NoWorldTargets,
				worldTargetPlan,
				onlinePlayers,
				[]);
		}

		if (onlinePlayers.Count == 0)
		{
			return CreateResult(
				VortexRiftEntryUpdatePlayerTargetPlanStatus.NoOnlinePlayers,
				worldTargetPlan,
				onlinePlayers,
				[]);
		}

		var targets = new List<int>();
		foreach (var worldId in worldTargetPlan.WorldIds)
		{
			foreach (var player in onlinePlayers)
			{
				if (player.WorldId == worldId)
					targets.Add(player.PlayerObjectId);
			}
		}

		return CreateResult(
			VortexRiftEntryUpdatePlayerTargetPlanStatus.Planned,
			worldTargetPlan,
			onlinePlayers,
			targets);
	}

	private static VortexRiftEntryUpdatePlayerTargetPlan CreateResult(
		VortexRiftEntryUpdatePlayerTargetPlanStatus status,
		VortexRiftEntryUpdateWorldTargetPlan? worldTargetPlan,
		IReadOnlyList<VortexRiftEntryUpdateOnlinePlayerSnapshot> onlinePlayers,
		IReadOnlyList<int> targetPlayerObjectIds)
	{
		return new VortexRiftEntryUpdatePlayerTargetPlan(
			status,
			worldTargetPlan,
			onlinePlayers,
			targetPlayerObjectIds,
			"services/rift/RiftInformer.sendRiftInfo -> syncRiftsState(worldId, packets) -> WorldMapInstance.forEachPlayer");
	}
}
