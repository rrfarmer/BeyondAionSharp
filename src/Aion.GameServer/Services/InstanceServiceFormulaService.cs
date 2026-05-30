namespace Aion.GameServer.Services;

public sealed record InstanceCooldownRatePlan(
	bool HasInstanceCooldownPermission,
	bool MapIsExcluded,
	int CooldownRate,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record InstanceDestroyDelayPlan(
	int MaxPlayers,
	int DestroyDelaySeconds,
	bool IsSolo,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class InstanceServiceFormulaService
{
	public static InstanceCooldownRatePlan CreateCooldownRatePlan(
		bool hasInstanceCooldownPermission,
		bool mapIsExcluded,
		int instanceCooldownRate)
	{
		// Java parity: services/instance/InstanceService.getInstanceRate(Player, int).
		// returns INSTANCE_COOLDOWN_RATE if player has permission AND map is not excluded; else 1.
		var effectiveRate = hasInstanceCooldownPermission && !mapIsExcluded
			? instanceCooldownRate
			: 1;

		return new InstanceCooldownRatePlan(
			hasInstanceCooldownPermission,
			mapIsExcluded,
			effectiveRate,
			hasInstanceCooldownPermission && !mapIsExcluded
				? $"InstanceService.getInstanceRate -> hasPermission && !excluded -> INSTANCE_COOLDOWN_RATE={instanceCooldownRate}"
				: "InstanceService.getInstanceRate -> !hasPermission || excluded -> 1"
		);
	}

	public static InstanceDestroyDelayPlan CreateDestroyDelayPlan(
		int maxPlayers,
		int soloDestroyDelaySeconds,
		int normalDestroyDelaySeconds)
	{
		// Java parity: services/instance/InstanceService.getDestroyDelaySeconds(WorldMapInstance).
		// maxPlayers == 1 uses solo delay; otherwise normal delay.
		var isSolo = maxPlayers == 1;
		var delay = isSolo ? soloDestroyDelaySeconds : normalDestroyDelaySeconds;

		return new InstanceDestroyDelayPlan(
			maxPlayers,
			delay,
			isSolo,
			isSolo
				? $"InstanceService.getDestroyDelaySeconds -> maxPlayers==1 -> SOLO_INSTANCE_DESTROY_DELAY_SECONDS={soloDestroyDelaySeconds}"
				: $"InstanceService.getDestroyDelaySeconds -> maxPlayers={maxPlayers} -> INSTANCE_DESTROY_DELAY_SECONDS={normalDestroyDelaySeconds}"
		);
	}
}
