namespace Aion.GameServer.Services;

public sealed record DeathXpLossPlan(
	long TotalXpLost,
	int UnrecoverableXp,
	int RecoverableXp,
	long AllRecoverable,
	long NewExpRecoverable,
	long ExpNeed,
	string JavaSource
)
{
	public bool IsLive => false;
}

public sealed record ReposeEnergyMaxPlan(
	long ExpNeed,
	bool IsReadyForReposeEnergy,
	long ReposeMax,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class DeathXpLossPlanService
{
	// Java parity: model/gameobjects/player/PlayerCommonData.calculateExpLoss.
	public const double UnrecoverableFraction = 0.33333333;

	// Java parity: model/gameobjects/player/PlayerCommonData.updateMaxRepose.
	public const float ReposeMaxFraction = 0.25f;

	public static DeathXpLossPlan CreateDeathXpLossPlan(
		int level,
		long expNeed,
		long currentExpShown,
		long currentExpRecoverable)
	{
		// Java parity: PlayerCommonData.calculateExpLoss.
		var xpLost = XpLossPlanService.CreatePlan(level, expNeed).ExpLoss;

		// Split into unrecoverable (1/3) and recoverable (2/3)
		int unrecoverable = (int)(xpLost * UnrecoverableFraction);
		int recoverable = (int)xpLost - unrecoverable;
		long allExpLost = recoverable + currentExpRecoverable;

		// Cap recoverable at 25% of expNeed
		var newRecoverable = allExpLost;
		if (newRecoverable > expNeed * 0.25)
			newRecoverable = (long)Math.Round(expNeed * 0.25);

		return new DeathXpLossPlan(
			xpLost, unrecoverable, recoverable, allExpLost, newRecoverable, expNeed,
			$"PlayerCommonData.calculateExpLoss -> lost={xpLost} unrecoverable={unrecoverable} recoverable={recoverable} allRecoverable={allExpLost} -> cap={newRecoverable}"
		);
	}

	public static ReposeEnergyMaxPlan CreateReposeMaxPlan(bool isReadyForReposeEnergy, long expNeed)
	{
		// Java parity: PlayerCommonData.updateMaxRepose.
		// reposeMax = round(expNeed * 0.25f) if ready; 0 if not.
		if (!isReadyForReposeEnergy)
		{
			return new ReposeEnergyMaxPlan(expNeed, false, ReposeMax: 0,
				"PlayerCommonData.updateMaxRepose -> !isReadyForReposeEnergy -> reposeMax=0");
		}

		var reposeMax = (long)(expNeed * ReposeMaxFraction);
		return new ReposeEnergyMaxPlan(expNeed, true, reposeMax,
			$"PlayerCommonData.updateMaxRepose -> expNeed={expNeed} * 0.25 = {reposeMax}");
	}
}
