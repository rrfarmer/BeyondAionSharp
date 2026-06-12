using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class RiftInformerService
{
	private const int AnnounceSlotCount = 12;
	private readonly RiftService _riftService;
	private readonly IGameClientConnectionRegistry? _connectionRegistry;
	private readonly Func<DateTimeOffset> _clock;

	public RiftInformerService(
		RiftService riftService,
		IGameClientConnectionRegistry? connectionRegistry = null,
		Func<DateTimeOffset>? clock = null)
	{
		_riftService = riftService;
		_connectionRegistry = connectionRegistry;
		_clock = clock ?? (() => DateTimeOffset.UtcNow);
	}

	public async Task<int> SendRiftsInfoAsync(int worldId)
	{
		// Java parity: services/rift/RiftInformer.sendRiftsInfo(int) syncs the requested world and then its twin.
		var sent = await SyncRiftsStateAsync(worldId, GetPackets(worldId));
		var twinId = GetTwinId(worldId);
		if (twinId > 0)
			sent += await SyncRiftsStateAsync(twinId, GetPackets(twinId));
		return sent;
	}

	public async Task<int> SendRiftsInfoAsync(Player player)
	{
		// Java parity: services/rift/RiftInformer.sendRiftsInfo(Player) sends current-world packets to the player, then broadcasts twin-world packets.
		var sent = await SyncRiftsStateAsync(player, GetPackets(player.GetPosition().WorldId));
		var twinId = GetTwinId(player.GetPosition().WorldId);
		if (twinId > 0)
			sent += await SyncRiftsStateAsync(twinId, GetPackets(twinId));
		return sent;
	}

	public Task<int> SendRiftDespawnAsync(int worldId, int objectId)
	{
		// Java parity: services/rift/RiftInformer.sendRiftDespawn sends action 4 only to the despawn world.
		return SyncRiftsStateAsync(worldId, [new SmRiftAnnounce(objectId)]);
	}

	public async Task<int> SendRiftInfoAsync(IReadOnlyList<int> worldIds)
	{
		// Java parity: services/rift/RiftInformer.sendRiftInfo(int[]) sends action 3 updates from the first world to every listed world.
		if (worldIds.Count == 0)
			return 0;

		var packets = GetEntryUpdatePackets(worldIds[0]);
		var sent = 0;
		foreach (var worldId in worldIds)
			sent += await SyncRiftsStateAsync(worldId, packets);
		return sent;
	}

	public RiftAnnounceData GetAnnounceData(int worldId)
	{
		// Java parity: services/rift/RiftInformer.getAnnounceData initializes all 12 announce slots before counting master rifts.
		var counts = new int[AnnounceSlotCount];
		foreach (var rift in _riftService.GetActiveRifts())
		{
			foreach (var portal in rift.Portals)
			{
				if (portal.MasterNpc.Position.WorldId != worldId)
					continue;

				var index = GetAnnounceIndex(portal.Definition, rift.GuardsRequested);
				if (index.HasValue)
					counts[index.Value]++;
			}
		}

		return new RiftAnnounceData(counts);
	}

	public int GetTwinId(int worldId)
	{
		// Java parity: services/rift/RiftInformer.getTwinId hardcoded map pair table.
		return worldId switch
		{
			110070000 => 220050000,
			210020000 => 220020000,
			210040000 => 220040000,
			210050000 => 220070000,
			210060000 => 120080000,
			210070000 => 220080000,
			120080000 => 210060000,
			220020000 => 210020000,
			220040000 => 210040000,
			220050000 => 110070000,
			220070000 => 210050000,
			220080000 => 210070000,
			_ => 0,
		};
	}

	private IReadOnlyList<SmRiftAnnounce> GetPackets(int worldId)
	{
		// Java parity: RiftInformer.getPackets(worldId) sends aggregate data, then action 2/3 packets for master RVControllers in that world.
		var packets = new List<SmRiftAnnounce> { new(GetAnnounceData(worldId)) };
		foreach (var rift in _riftService.GetActiveRifts())
		{
			foreach (var portal in rift.Portals)
			{
				if (portal.MasterNpc.Position.WorldId != worldId)
					continue;

				packets.Add(new SmRiftAnnounce(portal, isMaster: true, _clock));
				packets.Add(new SmRiftAnnounce(portal, isMaster: false, _clock));
			}
		}

		return packets;
	}

	private IReadOnlyList<SmRiftAnnounce> GetEntryUpdatePackets(int worldId)
	{
		// Java parity: RiftInformer.getPackets(worldId, -1) sends action 3 packets for master RVControllers only.
		var packets = new List<SmRiftAnnounce>();
		foreach (var rift in _riftService.GetActiveRifts())
		{
			foreach (var portal in rift.Portals)
			{
				if (portal.MasterNpc.Position.WorldId != worldId)
					continue;

				packets.Add(new SmRiftAnnounce(portal, isMaster: false, _clock));
			}
		}

		return packets;
	}

	private async Task<int> SyncRiftsStateAsync(int worldId, IReadOnlyList<SmRiftAnnounce> packets)
	{
		// Java parity: RiftInformer.syncRiftsState(int, packets) iterates players in the world's main map instance.
		if (_connectionRegistry == null)
			return 0;

		var sent = 0;
		foreach (var packet in packets)
		{
			sent += await _connectionRegistry.BroadcastToWorldAsync(
				packet,
				player => player.GetPosition().WorldId == worldId);
		}

		return sent;
	}

	private async Task<int> SyncRiftsStateAsync(Player player, IReadOnlyList<SmRiftAnnounce> packets)
	{
		// Java parity: RiftInformer.syncRiftsState(Player, packets) sends every generated rift packet directly to one player.
		if (_connectionRegistry == null)
			return 0;

		var sent = 0;
		foreach (var packet in packets)
		{
			if (await _connectionRegistry.SendPacketToPlayerAsync(player.ObjectId, packet))
				sent++;
		}

		return sent;
	}

	private static int? GetAnnounceIndex(RiftDefinition definition, bool guardsRequested)
	{
		// Java parity: RiftInformer.calcRiftsData counts vortex, normal, and volatile master rifts in the local 0-5 half.
		if (definition.IsVortex)
			return 1;
		if (definition.CanBeVolatile)
			return guardsRequested ? 4 : 0;
		if (definition.IsInvasionRift)
			return null;
		return 0;
	}
}

public sealed record RiftAnnounceData(IReadOnlyList<int> Counts)
{
	public int this[int index] => index >= 0 && index < Counts.Count ? Counts[index] : 0;
}
