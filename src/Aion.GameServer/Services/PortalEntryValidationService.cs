using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PortalEntryValidationService
{
	public static PortalEntryValidationResult ValidateCooldown(
		Player player,
		int worldId,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: services/teleport/PortalService.port rejects fresh instance creation when PortalCooldownList.isPortalUseDisabled(mapId).
		return PlayerPortalCooldownService.IsPortalUseDisabled(player, worldId, instanceCooltimes, now)
			? PortalEntryValidationResult.Rejected(
				PortalEntryValidationStatus.CooldownLocked,
				SmSystemMessage.CannotMakeInstanceCoolTime())
			: PortalEntryValidationResult.Allowed();
	}

	public static PortalEntryInstanceValidationResult ValidateCooldownForRegisteredInstance(
		Player player,
		int worldId,
		int maxPlayers,
		WorldMapRuntimeStateTable worldMaps,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: services/teleport/PortalService.port resolves registered solo/group/alliance instances before applying the cooldown lockout.
		var registeredInstance = ResolveRegisteredInstance(player, worldId, maxPlayers, worldMaps);
		if (registeredInstance == null || !registeredInstance.IsRegistered(player.ObjectId))
		{
			var validation = ValidateCooldown(player, worldId, instanceCooltimes, now);
			return validation.CanEnter
				? PortalEntryInstanceValidationResult.Allowed(null, reenter: false)
				: PortalEntryInstanceValidationResult.Rejected(validation.Status, validation.FailurePacket!);
		}

		var reenter = player.Position.WorldId != worldId || player.Position.InstanceId != registeredInstance.InstanceId;
		return PortalEntryInstanceValidationResult.Allowed(registeredInstance, reenter);
	}

	private static WorldMapInstanceRuntimeState? ResolveRegisteredInstance(
		Player player,
		int worldId,
		int maxPlayers,
		WorldMapRuntimeStateTable worldMaps)
	{
		return maxPlayers switch
		{
			1 => worldMaps.GetRegisteredInstance(worldId, player.ObjectId),
			_ => null,
		};
	}
}

public sealed record PortalEntryValidationResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	SmSystemMessage? FailurePacket)
{
	public static PortalEntryValidationResult Allowed()
	{
		return new PortalEntryValidationResult(true, PortalEntryValidationStatus.Allowed, null);
	}

	public static PortalEntryValidationResult Rejected(
		PortalEntryValidationStatus status,
		SmSystemMessage failurePacket)
	{
		return new PortalEntryValidationResult(false, status, failurePacket);
	}
}

public enum PortalEntryValidationStatus
{
	Allowed,
	CooldownLocked,
}

public sealed record PortalEntryInstanceValidationResult(
	bool CanEnter,
	PortalEntryValidationStatus Status,
	WorldMapInstanceRuntimeState? RegisteredInstance,
	bool Reenter,
	SmSystemMessage? FailurePacket)
{
	public static PortalEntryInstanceValidationResult Allowed(
		WorldMapInstanceRuntimeState? registeredInstance,
		bool reenter)
	{
		return new PortalEntryInstanceValidationResult(
			true,
			PortalEntryValidationStatus.Allowed,
			registeredInstance,
			reenter,
			null);
	}

	public static PortalEntryInstanceValidationResult Rejected(
		PortalEntryValidationStatus status,
		SmSystemMessage failurePacket)
	{
		return new PortalEntryInstanceValidationResult(false, status, null, false, failurePacket);
	}
}
