namespace Aion.GameServer.Services;

public enum BindPointTeleportFinalMovementPlanStatus
{
	TeleportReady,
	BlockedAboutToDie,
	BlockedDead,
}

public enum BindPointTeleportFinalMovementPlanStep
{
	CheckAboutToDie,
	CheckDead,
	CreateTeleportIntent,
}

public sealed record BindPointTeleportDestinationFact(
	int WorldId,
	float X,
	float Y,
	float Z,
	byte Heading,
	int CurrentWorldId,
	int CurrentInstanceId);

public sealed record BindPointTeleportFinalMovementPlan(
	BindPointTeleportFinalMovementPlanStatus Status,
	BindPointTeleportDestinationFact Destination,
	bool PlayerIsDead,
	bool PlayerIsAboutToDie,
	bool ShouldTeleport,
	int TargetWorldId,
	int? TargetInstanceId,
	float TargetX,
	float TargetY,
	float TargetZ,
	byte TargetHeading,
	string TeleportAnimation,
	IReadOnlyList<BindPointTeleportFinalMovementPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportFinalMovementPlanService
{
	public const int DefaultCrossWorldInstanceId = 1;
	public const string DefaultTeleportAnimation = "TeleportAnimation.NONE";

	public static BindPointTeleportFinalMovementPlan CreatePlan(
		BindPointTeleportDestinationFact destination,
		bool playerIsDead,
		bool playerIsAboutToDie)
	{
		// Java parity: BindPointTeleportService.teleport schedules a final 1000ms task that only calls
		// TeleportService.teleportTo when the player is neither about to die nor dead.
		if (playerIsAboutToDie)
		{
			return CreateBlockedPlan(
				BindPointTeleportFinalMovementPlanStatus.BlockedAboutToDie,
				destination,
				playerIsDead,
				playerIsAboutToDie,
				[BindPointTeleportFinalMovementPlanStep.CheckAboutToDie],
				"BindPointTeleportService.teleport final task -> player.getLifeStats().isAboutToDie() blocks TeleportService.teleportTo");
		}

		if (playerIsDead)
		{
			return CreateBlockedPlan(
				BindPointTeleportFinalMovementPlanStatus.BlockedDead,
				destination,
				playerIsDead,
				playerIsAboutToDie,
				[
					BindPointTeleportFinalMovementPlanStep.CheckAboutToDie,
					BindPointTeleportFinalMovementPlanStep.CheckDead,
				],
				"BindPointTeleportService.teleport final task -> player.isDead() blocks TeleportService.teleportTo");
		}

		return new BindPointTeleportFinalMovementPlan(
			BindPointTeleportFinalMovementPlanStatus.TeleportReady,
			destination,
			PlayerIsDead: false,
			PlayerIsAboutToDie: false,
			ShouldTeleport: true,
			destination.WorldId,
			ResolveTargetInstanceId(destination),
			destination.X,
			destination.Y,
			destination.Z,
			destination.Heading,
			DefaultTeleportAnimation,
			[
				BindPointTeleportFinalMovementPlanStep.CheckAboutToDie,
				BindPointTeleportFinalMovementPlanStep.CheckDead,
				BindPointTeleportFinalMovementPlanStep.CreateTeleportIntent,
			],
			"BindPointTeleportService.teleport final task -> if (!isAboutToDie && !isDead) TeleportService.teleportTo(player, hotspot.worldId, hotspot.x, hotspot.y, hotspot.z); TeleportService uses player heading, NONE animation, and instance 1 when crossing worlds",
			IsLive: false);
	}

	private static BindPointTeleportFinalMovementPlan CreateBlockedPlan(
		BindPointTeleportFinalMovementPlanStatus status,
		BindPointTeleportDestinationFact destination,
		bool playerIsDead,
		bool playerIsAboutToDie,
		IReadOnlyList<BindPointTeleportFinalMovementPlanStep> steps,
		string javaSource)
	{
		return new BindPointTeleportFinalMovementPlan(
			status,
			destination,
			playerIsDead,
			playerIsAboutToDie,
			ShouldTeleport: false,
			destination.WorldId,
			TargetInstanceId: null,
			destination.X,
			destination.Y,
			destination.Z,
			destination.Heading,
			DefaultTeleportAnimation,
			steps,
			javaSource,
			IsLive: false);
	}

	private static int ResolveTargetInstanceId(BindPointTeleportDestinationFact destination)
	{
		// Java parity: TeleportService.teleportTo(player, worldId, x, y, z) delegates with instance 1 when crossing worlds,
		// otherwise it keeps player.getInstanceId().
		return destination.CurrentWorldId != destination.WorldId
			? DefaultCrossWorldInstanceId
			: destination.CurrentInstanceId;
	}
}
