using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class InstanceEntranceCooldownService
{
	public static InstanceEntranceCooldownResult PreviewEntranceCooldown(
		Player player,
		int worldId,
		bool reenter,
		InstanceCooltimeTable instanceCooltimes,
		GameServerOptions options,
		DateTimeOffset now)
	{
		// Java parity: PortalService.transfer calculates the cooldown before deciding whether PortalCooldownList.addPortalCooldown will run.
		var rate = InstanceCooldownRateService.GetInstanceRate(player, worldId, options);
		var reuseTimeMillis = instanceCooltimes.CalculateInstanceEntranceCooltime(worldId, now, rate);
		return new InstanceEntranceCooldownResult(worldId, reuseTimeMillis, rate, Added: reuseTimeMillis > 0 && !reenter);
	}

	public static InstanceEntranceCooldownResult ApplyEntranceCooldown(
		Player player,
		int worldId,
		bool reenter,
		InstanceCooltimeTable instanceCooltimes,
		GameServerOptions options,
		DateTimeOffset now)
	{
		// Java parity: PortalService.transfer and AutoInstance.onPressEnter calculate instance entrance cooldowns after entry.
		var preview = PreviewEntranceCooldown(player, worldId, reenter, instanceCooltimes, options, now);
		if (preview.Added)
		{
			player.GetPortalCooldownList().AddPortalCooldown(worldId, preview.ReuseTimeMillis);
			return preview;
		}

		return preview;
	}

	public static SmInstanceInfo? CreateEntryInfoPacket(
		InstanceEntranceCooldownResult result,
		Player player,
		InstanceCooltimeTable instanceCooltimes,
		Func<DateTimeOffset>? clock = null)
	{
		// Java parity: model/gameobjects/player/PortalCooldownList.sendEntryInfo sends SM_INSTANCE_INFO mode 2 after addPortalCooldown.
		return result.Added
			? new SmInstanceInfo(2, player, instanceCooltimes, result.WorldId, clock)
			: null;
	}
}

public sealed record InstanceEntranceCooldownResult(
	int WorldId,
	long ReuseTimeMillis,
	int InstanceCooldownRate,
	bool Added);
