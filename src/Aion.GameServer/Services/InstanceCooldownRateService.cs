using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class InstanceCooldownRateService
{
	public static int GetInstanceRate(Player player, int mapId, GameServerOptions options)
	{
		// Java parity: InstanceService.getInstanceRate(Player, int) uses Player.hasPermission(MembershipConfig.INSTANCES_COOLDOWN).
		return HasPermission(player, options.Membership.InstancesCooldown)
			&& !options.Instance.CooldownRateExcludedMaps.Contains(mapId)
				? options.Instance.CooldownRate
				: 1;
	}

	private static bool HasPermission(Player player, byte permissionLevel)
	{
		// Java parity: model/gameobjects/player/Player.hasPermission compares account membership to the configured permission byte.
		return player.AccountMembership >= permissionLevel;
	}
}
