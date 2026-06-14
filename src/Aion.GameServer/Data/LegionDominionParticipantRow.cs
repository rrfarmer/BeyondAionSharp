namespace Aion.GameServer.Data;

/// <summary>
/// Row DTO for a legion's Legion Dominion participation ranking, consumed by the
/// SM_LEGION_DOMINION_RANK packet writer (SmLegionDominionRank). Extracted from the
/// retired reworked PlayerEnterWorldRepository so the live packet keeps compiling.
/// </summary>
public sealed record LegionDominionParticipantRow(
	int LegionId,
	string LegionName,
	int Points,
	int SurvivedTime,
	long ParticipatedEpochSeconds);
