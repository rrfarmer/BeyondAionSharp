namespace Aion.GameServer.Services;

public enum ArcadeFrenzyPointsPlanStatus
{
	FrenzyNotTriggered,
	FrenzyTriggered,
}

public sealed record ArcadeFrenzyPointsPlan(
	ArcadeFrenzyPointsPlanStatus Status,
	int CurrentPoints,
	int AddedPoints,
	int NewPoints,
	int RemainderPoints,
	bool FrenzyTriggered,
	long FrenzyDurationMilliseconds,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ArcadeFrenzyPointsPlanService
{
	// Java parity: services/UpgradeArcadeService constants.
	public const int FrenzyModeThreshold = 100;
	public const int FrenzyDurationSeconds = 90;
	public const long FrenzyDurationMilliseconds = FrenzyDurationSeconds * 1000;
	public const int FrenzyPointsPerToken = 8;

	public static ArcadeFrenzyPointsPlan CreatePlan(int currentFrenzyPoints, int addedFrenzyPoints)
	{
		// Java parity: services/UpgradeArcadeService.increaseFrenzyPoints.
		var newPoints = currentFrenzyPoints + addedFrenzyPoints;

		if (newPoints >= FrenzyModeThreshold)
		{
			var remainder = newPoints % FrenzyModeThreshold;
			return new ArcadeFrenzyPointsPlan(
				ArcadeFrenzyPointsPlanStatus.FrenzyTriggered,
				currentFrenzyPoints, addedFrenzyPoints,
				NewPoints: remainder,
				RemainderPoints: remainder,
				FrenzyTriggered: true,
				FrenzyDurationMilliseconds,
				$"UpgradeArcadeService.increaseFrenzyPoints -> {currentFrenzyPoints}+{addedFrenzyPoints}={newPoints} >= {FrenzyModeThreshold} -> FRENZY! remainder={remainder}, duration={FrenzyDurationSeconds}s"
			);
		}

		return new ArcadeFrenzyPointsPlan(
			ArcadeFrenzyPointsPlanStatus.FrenzyNotTriggered,
			currentFrenzyPoints, addedFrenzyPoints,
			NewPoints: newPoints,
			RemainderPoints: newPoints,
			FrenzyTriggered: false,
			FrenzyDurationMilliseconds: 0,
			$"UpgradeArcadeService.increaseFrenzyPoints -> {currentFrenzyPoints}+{addedFrenzyPoints}={newPoints} < {FrenzyModeThreshold} -> no frenzy"
		);
	}
}
