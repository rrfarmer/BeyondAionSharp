using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexStartInvasionRuntimeSnapshotCollectorService
{
	private readonly IVortexInvasionSpawnSnapshotSelector _invasionSpawnSelector;
	private readonly VortexDefenderAllianceUpdatePlanService _defenderAllianceUpdatePlanner;
	private readonly VortexDefenderInvitationBatchPlanService _defenderInvitationBatchPlanner;

	public VortexStartInvasionRuntimeSnapshotCollectorService(
		IVortexInvasionSpawnSnapshotSelector? invasionSpawnSelector = null,
		VortexDefenderAllianceUpdatePlanService? defenderAllianceUpdatePlanner = null,
		VortexDefenderInvitationBatchPlanService? defenderInvitationBatchPlanner = null)
	{
		_invasionSpawnSelector = invasionSpawnSelector ?? new VortexInvasionSpawnSnapshotSelectionService();
		_defenderAllianceUpdatePlanner = defenderAllianceUpdatePlanner ?? new VortexDefenderAllianceUpdatePlanService();
		_defenderInvitationBatchPlanner = defenderInvitationBatchPlanner ?? new VortexDefenderInvitationBatchPlanService();
	}

	public VortexStartInvasionSnapshotRequest Collect(
		VortexLocationSummary location,
		IEnumerable<IWorldNpcObject>? spawnedNpcs = null,
		IEnumerable<Player>? zonePlayers = null,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		IReadOnlyDictionary<int, bool>? defenderRequestSlotsByPlayerObjectId = null,
		bool defaultDefenderRequestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(location);

		// Java parity: services/vortex/Invasion.startInvasion reads the current
		// VortexLocation.spawned collection for despawn metadata, then
		// Invasion.updateAlliance scans VortexLocation.players and forwards only
		// exact defender-race matches to updateDefenders.
		var collectedSpawnedNpcs = (spawnedNpcs ?? [])
			.Select(VortexStartSpawnedNpcSnapshot.FromWorldNpc)
			.ToArray();
		var playerSnapshots = (zonePlayers ?? [])
			.Select(VortexZonePlayerSnapshot.FromPlayer)
			.ToArray();
		var defenderAllianceUpdatePlan = _defenderAllianceUpdatePlanner.CreatePlan(location, playerSnapshots);
		var defenderInvitationBatchPlan = _defenderInvitationBatchPlanner.CreatePlan(
			defenderAllianceUpdatePlan,
			existingDefenders,
			defenderAlliance,
			defenderRequestSlotsByPlayerObjectId,
			defaultDefenderRequestSlotAvailable);

		return new VortexStartInvasionSnapshotRequest(
			SpawnedNpcs: collectedSpawnedNpcs,
			DefenderAllianceUpdatePlan: defenderAllianceUpdatePlan,
			DefenderInvitationBatchPlan: defenderInvitationBatchPlan);
	}

	public VortexStartInvasionSnapshotRequest PrepareWithStaticInvasionSpawns(
		VortexLocationSummary location,
		NpcVortexSpawnTable vortexSpawns,
		IEnumerable<IWorldNpcObject>? spawnedNpcs = null,
		IEnumerable<Player>? zonePlayers = null,
		IReadOnlyList<VortexDefenderAddPlayerSnapshot>? existingDefenders = null,
		VortexDefenderAllianceSnapshot? defenderAlliance = null,
		IReadOnlyDictionary<int, bool>? defenderRequestSlotsByPlayerObjectId = null,
		bool defaultDefenderRequestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		// Java parity: Invasion.startInvasion composes pre-start runtime state
		// with VortexService.spawn(loc, VortexStateType.INVASION).
		var request = Collect(
			location,
			spawnedNpcs,
			zonePlayers,
			existingDefenders,
			defenderAlliance,
			defenderRequestSlotsByPlayerObjectId,
			defaultDefenderRequestSlotAvailable);
		return request.WithInvasionSpawns(_invasionSpawnSelector.SelectInvasionSpawns(location.Id, vortexSpawns));
	}
}
