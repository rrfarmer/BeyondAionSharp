using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

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
