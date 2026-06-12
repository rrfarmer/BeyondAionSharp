using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskReviveService
{
	public const int KiskReviveId = 4;

	public static PlayerKiskReviveResult TryUseKiskRevive(
		Player player,
		PlayerKiskRegistry? registry,
		Func<int, WorldPosition?> kiskPositionResolver,
		DateTimeOffset? now = null)
	{
		// Java parity: network/aion/clientpackets/CM_REVIVE.runImpl dead gate + services/player/PlayerReviveService.kiskRevive.
		if (!IsDead(player))
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.PlayerNotDead);

		if ((player.GetKisk()?.GetObjectId() ?? 0) == 0 || registry == null)
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.NoBoundKisk);

		var kisk = registry.GetKiskState((player.GetKisk()?.GetObjectId() ?? 0));
		if (kisk == null)
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.NoBoundKisk);

		if (kisk.RemainingResurrects <= 0 || kisk.GetRemainingLifetimeSeconds(now ?? DateTimeOffset.UtcNow) <= 0)
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.KiskInactive);

		var position = kiskPositionResolver(kisk.ObjectId);
		if (!position.HasValue)
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.MissingKiskPosition);

		var resurrection = PlayerKiskResurrectionService.UseResurrection(kisk);
		if (!resurrection.UsedCharge)
			return PlayerKiskReviveResult.Rejected(PlayerKiskReviveStatus.KiskInactive);

		return PlayerKiskReviveResult.Used(kisk, position, resurrection);
	}

	private static bool IsDead(Player player)
	{
		return player.LifeStats?.CurrentHp <= 0
			|| player.CreatureState == PlayerCreatureState.Dead;
	}
}

public sealed record PlayerKiskReviveResult(
	PlayerKiskReviveStatus Status,
	PlayerKiskRuntimeState? Kisk,
	WorldPosition? KiskPosition,
	PlayerKiskResurrectionUseResult? Resurrection)
{
	public bool UsedKisk => Status is PlayerKiskReviveStatus.Used or PlayerKiskReviveStatus.Depleted;

	public bool ShouldDeleteKisk => Resurrection?.ShouldDeleteKisk == true;

	public static PlayerKiskReviveResult Rejected(PlayerKiskReviveStatus status)
	{
		return new PlayerKiskReviveResult(status, null, null, null);
	}

	public static PlayerKiskReviveResult Used(
		PlayerKiskRuntimeState kisk,
		WorldPosition position,
		PlayerKiskResurrectionUseResult resurrection)
	{
		var status = resurrection.ShouldDeleteKisk
			? PlayerKiskReviveStatus.Depleted
			: PlayerKiskReviveStatus.Used;
		return new PlayerKiskReviveResult(status, kisk, position, resurrection);
	}
}

public enum PlayerKiskReviveStatus
{
	Used,
	Depleted,
	PlayerNotDead,
	NoBoundKisk,
	KiskInactive,
	MissingKiskPosition,
}
