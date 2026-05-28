using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class NearbyQuestRefreshPlanService
{
	public static NearbyQuestRefreshPlan CreatePlan(
		Player player,
		WorldMapInstanceRuntimeState? instance,
		NearbyQuestTemplateTable? questTemplates)
	{
		// Java parity breadcrumb: PlayerController.updateNearbyQuests sends SM_NEARBY_QUESTS after filtering
		// WorldMapInstance.getQuestIds(). This plan is intentionally non-sending until the controller boundary is safe.
		if (instance == null)
			return NearbyQuestRefreshPlan.NoWorldInstance();
		if (questTemplates == null)
			return NearbyQuestRefreshPlan.NoQuestTemplates(instance.QuestIds.Count);
		if (instance.QuestIds.Count == 0)
			return NearbyQuestRefreshPlan.NoWorldQuestIds();

		var projection = NearbyQuestMarkerProjectionService.ProjectMarkers(player, instance, questTemplates);
		var rejectionCounts = projection.RejectedQuestIds.Values
			.GroupBy(failure => failure)
			.ToDictionary(group => group.Key, group => group.Count());
		var status = projection.Markers.Count == 0
			? NearbyQuestRefreshPlanStatus.NoMarkers
			: NearbyQuestRefreshPlanStatus.Ready;
		return new NearbyQuestRefreshPlan(
			status,
			instance.QuestIds.Count,
			projection.Markers,
			projection.RejectedQuestIds,
			rejectionCounts);
	}

	public static NearbyQuestPacketFactoryPlan CreatePacketFactoryPlan(NearbyQuestRefreshPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan);

		// Java parity breadcrumb: PlayerController.updateNearbyQuests always sends
		// new SM_NEARBY_QUESTS(nearbyQuestList) after resolving the live map-region
		// quest ids and filtering them through QuestService.checkStartConditions.
		if (!plan.WouldSendPacket)
			return NearbyQuestPacketFactoryPlan.Blocked(plan.Status);

		return NearbyQuestPacketFactoryPlan.Created(plan.Markers);
	}
}

public sealed record NearbyQuestRefreshPlan(
	NearbyQuestRefreshPlanStatus Status,
	int WorldQuestIdCount,
	IReadOnlyList<NearbyQuestMarker> Markers,
	IReadOnlyDictionary<int, NearbyQuestStartConditionFailure> RejectedQuestIds,
	IReadOnlyDictionary<NearbyQuestStartConditionFailure, int> RejectionCounts)
{
	public bool WouldSendPacket => Status
		is NearbyQuestRefreshPlanStatus.Ready
		or NearbyQuestRefreshPlanStatus.NoWorldQuestIds
		or NearbyQuestRefreshPlanStatus.NoMarkers;

	public bool HasUnsupportedDependencies =>
		RejectionCounts.ContainsKey(NearbyQuestStartConditionFailure.UnsupportedXmlStartConditions)
		|| RejectionCounts.ContainsKey(NearbyQuestStartConditionFailure.UnsupportedInventoryItems)
		|| RejectionCounts.ContainsKey(NearbyQuestStartConditionFailure.UnsupportedRepeatTiming);

	public static NearbyQuestRefreshPlan NoWorldInstance()
	{
		return Empty(NearbyQuestRefreshPlanStatus.NoWorldInstance);
	}

	public static NearbyQuestRefreshPlan NoQuestTemplates(int worldQuestIdCount)
	{
		return Empty(NearbyQuestRefreshPlanStatus.NoQuestTemplates, worldQuestIdCount);
	}

	public static NearbyQuestRefreshPlan NoWorldQuestIds()
	{
		return Empty(NearbyQuestRefreshPlanStatus.NoWorldQuestIds);
	}

	private static NearbyQuestRefreshPlan Empty(
		NearbyQuestRefreshPlanStatus status,
		int worldQuestIdCount = 0)
	{
		return new NearbyQuestRefreshPlan(
			status,
			worldQuestIdCount,
			Array.Empty<NearbyQuestMarker>(),
			new Dictionary<int, NearbyQuestStartConditionFailure>(),
			new Dictionary<NearbyQuestStartConditionFailure, int>());
	}
}

public enum NearbyQuestRefreshPlanStatus
{
	Ready,
	NoWorldInstance,
	NoQuestTemplates,
	NoWorldQuestIds,
	NoMarkers,
}

public sealed record NearbyQuestPacketFactoryPlan(
	NearbyQuestPacketFactoryPlanStatus Status,
	IReadOnlyList<NearbyQuestMarker> Markers,
	SmNearbyQuests? Packet,
	string JavaSource)
{
	public bool HasPacket => Packet is not null;

	public static NearbyQuestPacketFactoryPlan Created(IReadOnlyList<NearbyQuestMarker> markers)
	{
		return new NearbyQuestPacketFactoryPlan(
			NearbyQuestPacketFactoryPlanStatus.PacketCreated,
			markers,
			new SmNearbyQuests(markers),
			"PlayerController.updateNearbyQuests -> new SM_NEARBY_QUESTS(nearbyQuestList)");
	}

	public static NearbyQuestPacketFactoryPlan Blocked(NearbyQuestRefreshPlanStatus refreshStatus)
	{
		return new NearbyQuestPacketFactoryPlan(
			NearbyQuestPacketFactoryPlanStatus.BlockedMissingDependency,
			Array.Empty<NearbyQuestMarker>(),
			Packet: null,
			$"PlayerController.updateNearbyQuests prerequisites unavailable: {refreshStatus}");
	}
}

public enum NearbyQuestPacketFactoryPlanStatus
{
	PacketCreated,
	BlockedMissingDependency,
}
