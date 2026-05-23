using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class PlayerKiskNpcInfoPacketService
{
	public static PlayerKiskNpcInfoPacketPlan CreatePacket(
		IWorldNpcObject npc,
		Player viewer,
		PlayerKiskRegistry? kiskRegistry,
		PlayerKiskNpcInfoZoneCounters? zoneCounters = null)
	{
		// Java parity: SM_NPC_INFO(Npc, Player) writes npc.getType(player); Kisk.getType(Player) is viewer-specific.
		var kisk = kiskRegistry?.GetKiskState(npc.ObjectId);
		if (kisk == null)
			return PlayerKiskNpcInfoPacketPlan.Default(new SmNpcInfo(npc), registeredKisk: false);

		if (zoneCounters == null)
			return PlayerKiskNpcInfoPacketPlan.MissingCounters(new SmNpcInfo(npc));

		var creatureType = PlayerKiskAttackabilityService.GetCreatureType(
			viewer,
			kisk,
			zoneCounters.KiskSiegeZoneCount,
			zoneCounters.KiskPvpZoneCount,
			zoneCounters.PlayerSiegeZoneCount,
			zoneCounters.PlayerPvpZoneCount);
		return new PlayerKiskNpcInfoPacketPlan(
			new SmNpcInfo(npc, (int)creatureType),
			RegisteredKisk: true,
			UsedCreatureTypeOverride: true,
			CreatureType: creatureType,
			MissingZoneCounters: false);
	}

	public static PlayerKiskNpcInfoPacketPlan CreatePacket(
		IWorldNpcObject npc,
		Player viewer,
		PlayerKiskRegistry? kiskRegistry,
		CreaturePvpZoneCounterService zoneCounterService)
	{
		ArgumentNullException.ThrowIfNull(zoneCounterService);
		var kiskCounters = zoneCounterService.GetCounters(npc.ObjectId);
		var playerCounters = zoneCounterService.GetCounters(viewer.ObjectId);
		return CreatePacket(
			npc,
			viewer,
			kiskRegistry,
			new PlayerKiskNpcInfoZoneCounters(
				kiskCounters.SiegeZoneCount,
				kiskCounters.PvpZoneCount,
				playerCounters.SiegeZoneCount,
				playerCounters.PvpZoneCount));
	}
}

public sealed record PlayerKiskNpcInfoZoneCounters(
	int KiskSiegeZoneCount,
	int KiskPvpZoneCount,
	int PlayerSiegeZoneCount,
	int PlayerPvpZoneCount);

public sealed record PlayerKiskNpcInfoPacketPlan(
	SmNpcInfo Packet,
	bool RegisteredKisk,
	bool UsedCreatureTypeOverride,
	PlayerKiskCreatureType? CreatureType,
	bool MissingZoneCounters)
{
	public static PlayerKiskNpcInfoPacketPlan Default(SmNpcInfo packet, bool registeredKisk)
	{
		return new PlayerKiskNpcInfoPacketPlan(
			packet,
			registeredKisk,
			UsedCreatureTypeOverride: false,
			CreatureType: null,
			MissingZoneCounters: false);
	}

	public static PlayerKiskNpcInfoPacketPlan MissingCounters(SmNpcInfo packet)
	{
		return new PlayerKiskNpcInfoPacketPlan(
			packet,
			RegisteredKisk: true,
			UsedCreatureTypeOverride: false,
			CreatureType: null,
			MissingZoneCounters: true);
	}
}
