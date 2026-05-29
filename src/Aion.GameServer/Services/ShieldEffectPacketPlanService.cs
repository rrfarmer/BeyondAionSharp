using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ShieldEffectPacketPlanStatus
{
	PacketCreated,
	BlockedInvalidLocation,
}

public sealed record ShieldEffectPacketPlan(
	ShieldEffectPacketPlanStatus Status,
	IReadOnlyList<ShieldEffectLocationSnapshot> Locations,
	SmShieldEffect? Packet,
	bool ShouldSendToPlayer,
	bool ShouldBroadcastToMap,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class ShieldEffectPacketPlanService
{
	public static ShieldEffectPacketPlan CreateSendToPlayerPlan(IEnumerable<ShieldEffectLocationSnapshot> locations)
	{
		ArgumentNullException.ThrowIfNull(locations);

		var locationList = locations.ToList();
		var validation = ValidateLocations(locationList);
		if (validation is not null)
		{
			return validation with
			{
				ShouldSendToPlayer = false,
				ShouldBroadcastToMap = false,
				JavaSource = "SM_SHIELD_EFFECT(Collection<SiegeLocation>) requires non-null SiegeLocation entries with positive location ids",
			};
		}

		return new ShieldEffectPacketPlan(
			ShieldEffectPacketPlanStatus.PacketCreated,
			locationList,
			new SmShieldEffect(locationList),
			ShouldSendToPlayer: true,
			ShouldBroadcastToMap: false,
			"SiegeService.onEnterSiegeWorld -> PacketSendUtility.sendPacket(player, new SM_SHIELD_EFFECT(worldLocations.values()))");
	}

	public static ShieldEffectPacketPlan CreateMapBroadcastPlan(ShieldEffectLocationSnapshot location)
	{
		var locations = new[] { location };
		var validation = ValidateLocations(locations);
		if (validation is not null)
		{
			return validation with
			{
				ShouldSendToPlayer = false,
				ShouldBroadcastToMap = false,
				JavaSource = "SM_SHIELD_EFFECT(int) requires SiegeService.getSiegeLocation(location) to resolve a valid SiegeLocation",
			};
		}

		return new ShieldEffectPacketPlan(
			ShieldEffectPacketPlanStatus.PacketCreated,
			locations,
			new SmShieldEffect(locations),
			ShouldSendToPlayer: false,
			ShouldBroadcastToMap: true,
			"ShieldNpcAI.updateFortressShieldStatus -> PacketSendUtility.broadcastToMap(map, new SM_SHIELD_EFFECT(siegeLocationId))");
	}

	private static ShieldEffectPacketPlan? ValidateLocations(IReadOnlyList<ShieldEffectLocationSnapshot> locations)
	{
		// Java parity boundary: Java iterates the provided Collection<SiegeLocation> in its
		// current order. C# stores snapshots and blocks unresolved/null live SiegeLocation data
		// before packet creation rather than reproducing a later NullReferenceException.
		if (locations.Any(location => location.LocationId <= 0))
		{
			return new ShieldEffectPacketPlan(
				ShieldEffectPacketPlanStatus.BlockedInvalidLocation,
				locations,
				Packet: null,
				ShouldSendToPlayer: false,
				ShouldBroadcastToMap: false,
				"SM_SHIELD_EFFECT requires positive SiegeLocation ids");
		}

		return null;
	}
}
