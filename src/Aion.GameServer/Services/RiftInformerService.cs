namespace Aion.GameServer.Services;

public sealed class RiftInformerService
{
	private const int AnnounceSlotCount = 12;
	private readonly RiftService _riftService;

	public RiftInformerService(RiftService riftService)
	{
		_riftService = riftService;
	}

	public RiftAnnounceData GetAnnounceData(int worldId)
	{
		// Java parity: services/rift/RiftInformer.getAnnounceData initializes all 12 announce slots before counting master rifts.
		var counts = new int[AnnounceSlotCount];
		foreach (var rift in _riftService.GetActiveRifts())
		{
			var definition = rift.Definition;
			if (definition == null)
				continue;

			var hasMasterInWorld = rift.Spawned.Any(
				npc => npc.Position.WorldId == worldId
					&& string.Equals(npc.Anchor, definition.MasterAnchor, StringComparison.Ordinal));
			if (!hasMasterInWorld)
				continue;

			var index = GetAnnounceIndex(definition, rift.GuardsRequested);
			if (index.HasValue)
				counts[index.Value]++;
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
