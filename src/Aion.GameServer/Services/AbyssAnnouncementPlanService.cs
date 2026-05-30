using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum AbyssAnnouncementPlanStatus
{
	ShouldAnnounce,
	SkippedRankTooLow,
	SkippedNotInAnnounceMap,
}

public sealed record AbyssAnnouncementPlan(
	AbyssAnnouncementPlanStatus Status,
	int WorldId,
	int RankId,
	SmSystemMessage? AnnouncementMessage,
	bool ShouldBroadcastToWorld,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class AbyssAnnouncementPlanService
{
	// Java parity: services/abyss/AbyssService.killAnnounceMaps.
	public static readonly IReadOnlySet<int> KillAnnounceMaps = new HashSet<int>
	{
		210050000, 210070000, 220070000, 220080000,
		400010000, 400020000, 400030000, 400040000, 400050000, 400060000,
		600010000, 600070000, 600090000, 600100000,
	};

	// Java parity: AbyssRankEnum.GRADE1_SOLDIER.getId() = 9.
	public const int MinAnnounceRankId = 9; // GRADE1_SOLDIER

	public static AbyssAnnouncementPlan CreateHighRankedDeathAnnouncementPlan(
		int rankId,
		int worldId,
		string raceL10n,
		string rankL10n,
		string playerName,
		string zoneName)
	{
		// Java parity: services/abyss/AbyssService.announceHighRankedDeath / shouldAnnounceHighRankedDeath.
		if (rankId < MinAnnounceRankId)
		{
			return new AbyssAnnouncementPlan(
				AbyssAnnouncementPlanStatus.SkippedRankTooLow,
				worldId, rankId,
				AnnouncementMessage: null,
				ShouldBroadcastToWorld: false,
				$"AbyssService.shouldAnnounceHighRankedDeath -> rankId({rankId}) < GRADE1_SOLDIER({MinAnnounceRankId}) -> false"
			);
		}

		if (!KillAnnounceMaps.Contains(worldId))
		{
			return new AbyssAnnouncementPlan(
				AbyssAnnouncementPlanStatus.SkippedNotInAnnounceMap,
				worldId, rankId,
				AnnouncementMessage: null,
				ShouldBroadcastToWorld: false,
				$"AbyssService.shouldAnnounceHighRankedDeath -> worldId({worldId}) not in killAnnounceMaps -> false"
			);
		}

		return new AbyssAnnouncementPlan(
			AbyssAnnouncementPlanStatus.ShouldAnnounce,
			worldId, rankId,
			SmSystemMessage.AbyssOrderRankerDie(raceL10n, rankL10n, playerName, zoneName),
			ShouldBroadcastToWorld: true,
			$"AbyssService.announceHighRankedDeath -> broadcastToWorld(STR_ABYSS_ORDER_RANKER_DIE) for {playerName} in worldId={worldId}"
		);
	}
}
