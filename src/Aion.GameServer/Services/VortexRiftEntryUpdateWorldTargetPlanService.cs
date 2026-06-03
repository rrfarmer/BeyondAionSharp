namespace Aion.GameServer.Services;

public enum VortexRiftEntryUpdateWorldTargetPlanStatus
{
	MissingPortal,
	Planned,
}

public sealed record VortexRiftEntryUpdateWorldTargetPlan(
	VortexRiftEntryUpdateWorldTargetPlanStatus Status,
	RiftPortalState? Portal,
	bool IsMasterController,
	IReadOnlyList<int> WorldIds,
	string JavaSource);

public static class VortexRiftEntryUpdateWorldTargetPlanService
{
	public static VortexRiftEntryUpdateWorldTargetPlan CreatePlan(
		RiftPortalState? portal,
		bool isMasterController)
	{
		if (portal == null)
		{
			return new VortexRiftEntryUpdateWorldTargetPlan(
				VortexRiftEntryUpdateWorldTargetPlanStatus.MissingPortal,
				null,
				isMasterController,
				[],
				"controllers/RVController.getWorldsList requires an active controller owner before RiftInformer.sendRiftInfo can target worlds");
		}

		var ownerWorldId = isMasterController
			? portal.MasterNpc.Position.WorldId
			: portal.SlaveNpc.SpawnLocation.WorldId;
		var worldIds = isMasterController
			? new[] { ownerWorldId, portal.SlaveNpc.SpawnLocation.WorldId }
			: [ownerWorldId];

		return new VortexRiftEntryUpdateWorldTargetPlan(
			VortexRiftEntryUpdateWorldTargetPlanStatus.Planned,
			portal,
			isMasterController,
			worldIds,
			"controllers/RVController.getWorldsList -> services/rift/RiftInformer.sendRiftInfo");
	}
}
