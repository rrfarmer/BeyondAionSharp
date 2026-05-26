namespace Aion.GameServer.Services;

public enum BindPointTeleportRequirementStatus
{
	Ready,
	InvalidStartWorld,
	InvalidRace,
	NotEnoughKinah,
	CooldownNotReady,
}

public sealed record BindPointTeleportRequirementsPlan(
	BindPointTeleportRequirementStatus Status,
	bool CanTeleport,
	int HotspotId,
	int PlayerWorldId,
	int HotspotWorldId,
	string? PlayerRace,
	string? HotspotRace,
	long RequiredPrice,
	long CurrentKinah,
	int? CooldownTimeLeftSeconds,
	string? SystemMessage,
	string? AuditMessage,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportRequirementsPlanService
{
	public static BindPointTeleportRequirementsPlan CreatePlan(
		int hotspotId,
		int playerWorldId,
		int hotspotWorldId,
		string? playerRace,
		string? hotspotRace,
		long currentKinah,
		long requiredPrice,
		int? cooldownTimeLeftSeconds = null)
	{
		// Java parity: services/teleport/BindPointTeleportService.checkRequirements.
		// This is only validation metadata; packet sends, AuditLogger, and cooldown storage remain live-boundary work.
		if (playerWorldId != hotspotWorldId)
		{
			return Failed(
				BindPointTeleportRequirementStatus.InvalidStartWorld,
				hotspotId,
				playerWorldId,
				hotspotWorldId,
				playerRace,
				hotspotRace,
				currentKinah,
				requiredPrice,
				cooldownTimeLeftSeconds,
				"STR_CANNOT_MOVE_TO_AIRPORT_NO_ROUTE",
				$"tried to use hotspot teleport {hotspotId} from invalid start world {playerWorldId}, expected {hotspotWorldId}");
		}

		if (!IsRaceAllowed(playerRace, hotspotRace))
		{
			return Failed(
				BindPointTeleportRequirementStatus.InvalidRace,
				hotspotId,
				playerWorldId,
				hotspotWorldId,
				playerRace,
				hotspotRace,
				currentKinah,
				requiredPrice,
				cooldownTimeLeftSeconds,
				systemMessage: null,
				$"tried to use hotspot teleport {hotspotId} for invalid race {playerRace}, expected {hotspotRace}");
		}

		if (currentKinah < requiredPrice)
		{
			return Failed(
				BindPointTeleportRequirementStatus.NotEnoughKinah,
				hotspotId,
				playerWorldId,
				hotspotWorldId,
				playerRace,
				hotspotRace,
				currentKinah,
				requiredPrice,
				cooldownTimeLeftSeconds,
				"STR_CANNOT_MOVE_TO_AIRPORT_NOT_ENOUGH_FEE",
				auditMessage: null);
		}

		if (cooldownTimeLeftSeconds is > 0)
		{
			return Failed(
				BindPointTeleportRequirementStatus.CooldownNotReady,
				hotspotId,
				playerWorldId,
				hotspotWorldId,
				playerRace,
				hotspotRace,
				currentKinah,
				requiredPrice,
				cooldownTimeLeftSeconds,
				"STR_FLYING_TIME_NOT_READY",
				auditMessage: null);
		}

		return new BindPointTeleportRequirementsPlan(
			BindPointTeleportRequirementStatus.Ready,
			CanTeleport: true,
			hotspotId,
			playerWorldId,
			hotspotWorldId,
			playerRace,
			hotspotRace,
			requiredPrice,
			currentKinah,
			cooldownTimeLeftSeconds,
			SystemMessage: null,
			AuditMessage: null,
			"BindPointTeleportService.checkRequirements",
			IsLive: false);
	}

	private static bool IsRaceAllowed(string? playerRace, string? hotspotRace)
	{
		return string.Equals(playerRace, "PC_ALL", StringComparison.Ordinal)
			|| string.Equals(playerRace, hotspotRace, StringComparison.Ordinal);
	}

	private static BindPointTeleportRequirementsPlan Failed(
		BindPointTeleportRequirementStatus status,
		int hotspotId,
		int playerWorldId,
		int hotspotWorldId,
		string? playerRace,
		string? hotspotRace,
		long currentKinah,
		long requiredPrice,
		int? cooldownTimeLeftSeconds,
		string? systemMessage,
		string? auditMessage)
	{
		return new BindPointTeleportRequirementsPlan(
			status,
			CanTeleport: false,
			hotspotId,
			playerWorldId,
			hotspotWorldId,
			playerRace,
			hotspotRace,
			requiredPrice,
			currentKinah,
			cooldownTimeLeftSeconds,
			systemMessage,
			auditMessage,
			"BindPointTeleportService.checkRequirements",
			IsLive: false);
	}
}
