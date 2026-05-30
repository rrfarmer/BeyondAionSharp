namespace Aion.GameServer.Services;

public enum NpcDecayDelayPlanStatus
{
	ImmediateDecay,
	WithDropDecay,
	CustomDelay,
}

public sealed record NpcDecayDelayPlan(
	NpcDecayDelayPlanStatus Status,
	long DelayMilliseconds,
	bool HasDrop,
	string JavaSource
)
{
	public bool IsLive => false;
}

public enum NpcRespawnPlanStatus
{
	ShouldRespawn,
	NoRespawnTemplate,
	NoRespawnFlagged,
}

public sealed record NpcRespawnPlan(
	NpcRespawnPlanStatus Status,
	int RespawnTimeSeconds,
	long ScheduledDelayMilliseconds,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class NpcDecayDelayPlanService
{
	// Java parity: services/RespawnService constants.
	public const long ImmediateDecayMilliseconds = 2 * 1000;      // 2 seconds
	public const long WithDropDecayMilliseconds = 5 * 60 * 1000;  // 5 minutes

	public static NpcDecayDelayPlan CreateDecayDelayPlan(bool hasDrop)
	{
		// Java parity: services/RespawnService.scheduleDecayTask(Npc).
		if (!hasDrop)
		{
			return new NpcDecayDelayPlan(
				NpcDecayDelayPlanStatus.ImmediateDecay,
				ImmediateDecayMilliseconds,
				hasDrop,
				$"RespawnService.scheduleDecayTask -> drop==null || isEmpty -> IMMEDIATE_DECAY={ImmediateDecayMilliseconds}ms"
			);
		}

		return new NpcDecayDelayPlan(
			NpcDecayDelayPlanStatus.WithDropDecay,
			WithDropDecayMilliseconds,
			hasDrop,
			$"RespawnService.scheduleDecayTask -> drop exists -> WITH_DROP_DECAY={WithDropDecayMilliseconds}ms"
		);
	}

	public static NpcDecayDelayPlan CreateDecayDelayPlanWithDelay(long requestedDelayMilliseconds)
	{
		// Java parity: services/RespawnService.scheduleDecayTask(VisibleObject, long delay).
		// If delay == 0, use IMMEDIATE_DECAY to ensure death animation plays.
		var effectiveDelay = requestedDelayMilliseconds == 0 ? ImmediateDecayMilliseconds : requestedDelayMilliseconds;
		return new NpcDecayDelayPlan(
			effectiveDelay == ImmediateDecayMilliseconds && requestedDelayMilliseconds == 0
				? NpcDecayDelayPlanStatus.ImmediateDecay
				: NpcDecayDelayPlanStatus.CustomDelay,
			effectiveDelay,
			HasDrop: false,
			requestedDelayMilliseconds == 0
				? $"RespawnService.scheduleDecayTask -> delay==0 -> IMMEDIATE_DECAY={ImmediateDecayMilliseconds}ms (always delay for death animation)"
				: $"RespawnService.scheduleDecayTask -> delay={effectiveDelay}ms"
		);
	}

	public static NpcRespawnPlan CreateRespawnPlan(bool hasSpawnTemplate, bool isNoRespawn, int respawnTimeSeconds)
	{
		// Java parity: services/RespawnService.scheduleRespawn.
		if (!hasSpawnTemplate)
		{
			return new NpcRespawnPlan(
				NpcRespawnPlanStatus.NoRespawnTemplate,
				respawnTimeSeconds,
				ScheduledDelayMilliseconds: 0,
				"RespawnService.scheduleRespawn -> spawnTemplate == null -> return null"
			);
		}

		if (isNoRespawn)
		{
			return new NpcRespawnPlan(
				NpcRespawnPlanStatus.NoRespawnFlagged,
				respawnTimeSeconds,
				ScheduledDelayMilliseconds: 0,
				"RespawnService.scheduleRespawn -> spawnTemplate.isNoRespawn() -> return null"
			);
		}

		return new NpcRespawnPlan(
			NpcRespawnPlanStatus.ShouldRespawn,
			respawnTimeSeconds,
			(long)respawnTimeSeconds * 1000,
			$"RespawnService.scheduleRespawn -> schedule(respawnTask, {respawnTimeSeconds}s * 1000ms)"
		);
	}
}
