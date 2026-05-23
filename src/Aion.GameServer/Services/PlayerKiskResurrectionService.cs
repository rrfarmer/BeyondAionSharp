namespace Aion.GameServer.Services;

public static class PlayerKiskResurrectionService
{
	public static PlayerKiskResurrectionUseResult UseResurrection(PlayerKiskRuntimeState kisk)
	{
		// Java parity: model/gameobjects/Kisk.resurrectionUsed decrements, broadcasts SM_KISK_UPDATE,
		// and deletes the kisk when remaining resurrections reach zero.
		var previousRemaining = kisk.RemainingResurrects;
		if (!kisk.UseResurrection())
			return PlayerKiskResurrectionUseResult.NotActive(previousRemaining);

		return kisk.RemainingResurrects <= 0
			? PlayerKiskResurrectionUseResult.UsedAndDepleted(previousRemaining, kisk.RemainingResurrects)
			: PlayerKiskResurrectionUseResult.Used(previousRemaining, kisk.RemainingResurrects);
	}
}

public sealed record PlayerKiskResurrectionUseResult(
	PlayerKiskResurrectionUseStatus Status,
	int PreviousRemainingResurrections,
	int RemainingResurrections)
{
	public bool UsedCharge => Status is PlayerKiskResurrectionUseStatus.Used or PlayerKiskResurrectionUseStatus.Depleted;

	public bool ShouldBroadcastUpdate => UsedCharge;

	public bool ShouldDeleteKisk => Status == PlayerKiskResurrectionUseStatus.Depleted;

	public static PlayerKiskResurrectionUseResult Used(int previousRemaining, int remaining)
	{
		return new PlayerKiskResurrectionUseResult(PlayerKiskResurrectionUseStatus.Used, previousRemaining, remaining);
	}

	public static PlayerKiskResurrectionUseResult UsedAndDepleted(int previousRemaining, int remaining)
	{
		return new PlayerKiskResurrectionUseResult(PlayerKiskResurrectionUseStatus.Depleted, previousRemaining, remaining);
	}

	public static PlayerKiskResurrectionUseResult NotActive(int remaining)
	{
		return new PlayerKiskResurrectionUseResult(PlayerKiskResurrectionUseStatus.NotActive, remaining, remaining);
	}
}

public enum PlayerKiskResurrectionUseStatus
{
	Used,
	Depleted,
	NotActive,
}
