namespace Aion.GameServer.Services;

public sealed record RoundRobinLootCounterPlan(
	int GroupSize,
	int CurrentCounter,
	int NewCounter,
	int WinnerIndex,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class RoundRobinLootCounterPlanService
{
	public static RoundRobinLootCounterPlan CreatePlan(int groupSize, int currentCounter)
	{
		// Java parity: services/drop/DropRegistrationService initDropNpc ROUNDROBIN case.
		// if (size > nrRoundRobin) → nrRoundRobin++; else → nrRoundRobin = 1
		// Then pick player at index i == nrRoundRobin (1-based).
		int newCounter;
		if (groupSize > currentCounter)
			newCounter = currentCounter + 1;
		else
			newCounter = 1;

		// WinnerIndex is 0-based: player at index (newCounter - 1) wins
		var winnerIndex = newCounter - 1;

		return new RoundRobinLootCounterPlan(
			groupSize, currentCounter, newCounter, winnerIndex,
			$"DropRegistrationService ROUNDROBIN -> groupSize={groupSize} > nrRoundRobin({currentCounter})? {(groupSize > currentCounter ? "yes +1" : "no reset 1")} -> newCounter={newCounter} -> winner[{winnerIndex}]"
		);
	}
}
