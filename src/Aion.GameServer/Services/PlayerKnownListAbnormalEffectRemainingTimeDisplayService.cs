namespace Aion.GameServer.Services;

public sealed record PlayerKnownListAbnormalEffectRemainingTimeSnapshot(
	int? DurationMillis,
	long EndTimeUnixTimeMilliseconds,
	bool EffectedIsNpc,
	long NowUnixTimeMilliseconds);

public static class PlayerKnownListAbnormalEffectRemainingTimeDisplayService
{
	public const int NpcPermanentDisplayDurationMillis = 86_400_000;

	public static int Resolve(PlayerKnownListAbnormalEffectRemainingTimeSnapshot snapshot)
	{
		// Java parity breadcrumb: Effect.getRemainingTimeToDisplay() delegates to
		// getDuration() and getRemainingTimeMillis(). This helper keeps time an
		// explicit snapshot input instead of reading System.currentTimeMillis().
		if (snapshot.DurationMillis is null or 0)
			return -1;

		if (snapshot.EffectedIsNpc && snapshot.DurationMillis >= NpcPermanentDisplayDurationMillis)
			return -1;

		var remainingTimeMillis = snapshot.EndTimeUnixTimeMilliseconds - snapshot.NowUnixTimeMilliseconds;
		return remainingTimeMillis > int.MaxValue
			? -1
			: unchecked((int)remainingTimeMillis);
	}
}
