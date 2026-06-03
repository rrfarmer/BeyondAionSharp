using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class VortexStopInvasionRuntimeSnapshotCollectorService
{
	private readonly IVortexPeaceSpawnSnapshotSelector _peaceSpawnSelector;

	public VortexStopInvasionRuntimeSnapshotCollectorService(
		IVortexPeaceSpawnSnapshotSelector? peaceSpawnSelector = null)
	{
		_peaceSpawnSelector = peaceSpawnSelector ?? new VortexPeaceSpawnSnapshotSelectionService();
	}

	public VortexStopInvasionSnapshotRequest Collect(
		VortexInvasionSnapshot? snapshot,
		IEnumerable<Player>? players = null,
		IEnumerable<PlayerKiskRuntimeState>? invaderKisks = null,
		IEnumerable<IWorldNpcObject>? spawnedNpcs = null,
		IReadOnlyDictionary<int, VortexKickPlayerAllianceSnapshot>? invaderAlliances = null)
	{
		// Java parity: services/vortex/Invasion.stopInvasion reads the active
		// invader map, VortexLocation.invadersKisks, VortexLocation.spawned, and
		// VortexLocation.vortexController.passedPlayers before live mutations.
		if (snapshot == null)
			return VortexStopInvasionSnapshotRequest.Empty;

		var invaderObjectIds = snapshot.InvaderObjectIds.ToHashSet();
		var collectedInvaders = (players ?? [])
			.Where(player => invaderObjectIds.Contains(player.ObjectId))
			.Select(VortexStopInvaderSnapshot.FromPlayer)
			.ToArray();
		var collectedKisks = (invaderKisks ?? [])
			.Select(VortexStopInvaderKiskSnapshot.FromRuntimeState)
			.ToArray();
		var collectedSpawnedNpcs = (spawnedNpcs ?? [])
			.Select(VortexStopSpawnedNpcSnapshot.FromWorldNpc)
			.ToArray();
		var collectedAlliances = (invaderAlliances ?? new Dictionary<int, VortexKickPlayerAllianceSnapshot>())
			.Where(entry => invaderObjectIds.Contains(entry.Key))
			.ToDictionary(entry => entry.Key, entry => entry.Value);

		return new VortexStopInvasionSnapshotRequest(
			Invaders: collectedInvaders,
			InvaderKisks: collectedKisks,
			SpawnedNpcs: collectedSpawnedNpcs,
			InvaderAlliances: collectedAlliances,
			PassedPlayerObjectIds: snapshot.PassedPlayerObjectIds.ToHashSet());
	}

	public VortexStopInvasionSnapshotRequest PrepareWithStaticPeaceSpawns(
		int vortexLocationId,
		VortexInvasionSnapshot? snapshot,
		NpcVortexSpawnTable vortexSpawns,
		IEnumerable<Player>? players = null,
		IEnumerable<PlayerKiskRuntimeState>? invaderKisks = null,
		IEnumerable<IWorldNpcObject>? spawnedNpcs = null,
		IReadOnlyDictionary<int, VortexKickPlayerAllianceSnapshot>? invaderAlliances = null)
	{
		ArgumentNullException.ThrowIfNull(vortexSpawns);

		// Java parity: Invasion.stopInvasion composes state collected before stop
		// side effects with VortexService.spawn(loc, VortexStateType.PEACE).
		var request = Collect(
			snapshot,
			players,
			invaderKisks,
			spawnedNpcs,
			invaderAlliances);
		if (snapshot == null)
			return request;

		return request.WithPeaceSpawns(_peaceSpawnSelector.SelectPeaceSpawns(vortexLocationId, vortexSpawns));
	}
}
