using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerPortalCooldownService
{
	public static bool IsPortalUseDisabled(
		Player player,
		int worldId,
		InstanceCooltimeTable instanceCooltimes,
		DateTimeOffset now)
	{
		// Java parity: model/gameobjects/player/PortalCooldownList.isPortalUseDisabled.
		if (!player.PortalCooldowns.TryGetValue(worldId, out var cooldown))
			return false;

		if (IsExpired(cooldown, now))
		{
			RemovePortalCooldown(player, worldId);
			return false;
		}

		var template = instanceCooltimes.GetInstanceCooltimeByWorldId(worldId);
		return template != null && cooldown.EntryCount >= template.MaxCount;
	}

	public static long GetPortalCooldownTime(Player player, int worldId, DateTimeOffset now)
	{
		// Java parity: model/gameobjects/player/PortalCooldownList.getPortalCooldownTime.
		if (!player.PortalCooldowns.TryGetValue(worldId, out var cooldown))
			return 0;

		if (IsExpired(cooldown, now))
		{
			RemovePortalCooldown(player, worldId);
			return 0;
		}

		return cooldown.ReuseTimeMillis;
	}

	public static void AddPortalCooldown(Player player, int worldId, long reuseTimeMillis)
	{
		// Java parity: model/gameobjects/player/PortalCooldownList.addPortalCooldown increments the entry count for the reuse window.
		var cooldowns = player.PortalCooldowns.ToDictionary(pair => pair.Key, pair => pair.Value);
		if (!cooldowns.TryGetValue(worldId, out var cooldown))
			cooldown = new PlayerPortalCooldown(worldId, reuseTimeMillis, EntryCount: 0);

		cooldowns[worldId] = cooldown with
		{
			EntryCount = cooldown.EntryCount + 1,
		};
		player.PortalCooldowns = cooldowns;
	}

	public static void RemovePortalCooldown(Player player, int worldId)
	{
		if (!player.PortalCooldowns.ContainsKey(worldId))
			return;

		var cooldowns = player.PortalCooldowns.ToDictionary(pair => pair.Key, pair => pair.Value);
		cooldowns.Remove(worldId);
		player.PortalCooldowns = cooldowns;
	}

	private static bool IsExpired(PlayerPortalCooldown cooldown, DateTimeOffset now)
	{
		return cooldown.ReuseTimeMillis < now.ToUnixTimeMilliseconds();
	}
}
