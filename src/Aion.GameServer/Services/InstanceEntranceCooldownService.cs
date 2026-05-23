using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InstanceEntranceCooldownService
{
	public static InstanceEntranceCooldownResult ApplyEntranceCooldown(
		Player player,
		int worldId,
		bool reenter,
		InstanceCooltimeTable instanceCooltimes,
		GameServerOptions options,
		DateTimeOffset now)
	{
		// Java parity: PortalService.transfer and AutoInstance.onPressEnter calculate instance entrance cooldowns after entry.
		var rate = InstanceCooldownRateService.GetInstanceRate(player, worldId, options);
		var reuseTimeMillis = instanceCooltimes.CalculateInstanceEntranceCooltime(worldId, now, rate);
		if (reuseTimeMillis > 0 && !reenter)
		{
			PlayerPortalCooldownService.AddPortalCooldown(player, worldId, reuseTimeMillis);
			return new InstanceEntranceCooldownResult(worldId, reuseTimeMillis, rate, Added: true);
		}

		return new InstanceEntranceCooldownResult(worldId, reuseTimeMillis, rate, Added: false);
	}
}

public sealed record InstanceEntranceCooldownResult(
	int WorldId,
	long ReuseTimeMillis,
	int InstanceCooldownRate,
	bool Added);
