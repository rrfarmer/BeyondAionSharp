using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonReleaseNotificationPlanStatus
{
	PlanCreated,
	SkippedLogout,
	BlockedEmptySummonName,
	BlockedNegativeSkillId,
	BlockedInvalidSummonObjectId,
}

public sealed record SummonReleaseNotificationPlan(
	SummonReleaseNotificationPlanStatus Status,
	SummonReleaseUnsummonType UnsummonType,
	string? SummonName,
	int SummonedBySkillId,
	int SummonObjectId,
	SmSystemMessage? NotificationPacket,
	SummonReleasePacketSequencePlan? ReleasePacketSequencePlan,
	IReadOnlyList<GameServerPacket> PacketsInOrder,
	bool ShouldSendToMaster,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonReleaseNotificationPlanService
{
	public static SummonReleaseNotificationPlan CreatePlan(
		SummonReleaseUnsummonType unsummonType,
		string? summonName,
		int summonedBySkillId,
		int summonObjectId
	)
	{
		// Java parity: SummonsService.ReleaseSummonTask.run sends a release SM_SYSTEM_MESSAGE to the master
		// before SM_SUMMON_PANEL_REMOVE and SM_SUMMON_OWNER_REMOVE for COMMAND, DISTANCE, and UNSPECIFIED.
		// LOGOUT skips this notification and packet pair entirely.
		if (unsummonType == SummonReleaseUnsummonType.Logout)
		{
			return new SummonReleaseNotificationPlan(
				SummonReleaseNotificationPlanStatus.SkippedLogout,
				unsummonType,
				summonName,
				summonedBySkillId,
				summonObjectId,
				NotificationPacket: null,
				ReleasePacketSequencePlan: null,
				PacketsInOrder: [],
				ShouldSendToMaster: false,
				"SummonsService.ReleaseSummonTask.run -> LOGOUT branch skips SM_SYSTEM_MESSAGE, SM_SUMMON_PANEL_REMOVE, and SM_SUMMON_OWNER_REMOVE"
			);
		}

		SmSystemMessage? notificationPacket;
		string javaSource;
		if (unsummonType == SummonReleaseUnsummonType.Distance)
		{
			notificationPacket = SmSystemMessage.SkillSummonUnsummonByTooDistance();
			javaSource =
				"SummonsService.ReleaseSummonTask.run -> SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_BY_TOO_DISTANCE() -> SM_SUMMON_PANEL_REMOVE -> SM_SUMMON_OWNER_REMOVE";
		}
		else
		{
			if (string.IsNullOrWhiteSpace(summonName))
			{
				return new SummonReleaseNotificationPlan(
					SummonReleaseNotificationPlanStatus.BlockedEmptySummonName,
					unsummonType,
					summonName,
					summonedBySkillId,
					summonObjectId,
					NotificationPacket: null,
					ReleasePacketSequencePlan: null,
					PacketsInOrder: [],
					ShouldSendToMaster: false,
					"SummonsService.ReleaseSummonTask.run -> SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMONED(summon.getL10n()) requires a non-empty summon name"
				);
			}

			notificationPacket = SmSystemMessage.SkillSummonUnsummoned(summonName);
			javaSource =
				"SummonsService.ReleaseSummonTask.run -> SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMONED(summon.getL10n()) -> SM_SUMMON_PANEL_REMOVE -> SM_SUMMON_OWNER_REMOVE";
		}

		var releasePacketSequencePlan = SummonReleasePacketSequencePlanService.CreatePlan(unsummonType, summonedBySkillId, summonObjectId);
		if (releasePacketSequencePlan.Status != SummonReleasePacketSequencePlanStatus.SequenceCreated)
		{
			return new SummonReleaseNotificationPlan(
				releasePacketSequencePlan.Status == SummonReleasePacketSequencePlanStatus.BlockedNegativeSkillId
					? SummonReleaseNotificationPlanStatus.BlockedNegativeSkillId
					: SummonReleaseNotificationPlanStatus.BlockedInvalidSummonObjectId,
				unsummonType,
				summonName,
				summonedBySkillId,
				summonObjectId,
				notificationPacket,
				releasePacketSequencePlan,
				PacketsInOrder: [],
				ShouldSendToMaster: false,
				releasePacketSequencePlan.JavaSource
			);
		}

		return new SummonReleaseNotificationPlan(
			SummonReleaseNotificationPlanStatus.PlanCreated,
			unsummonType,
			summonName,
			summonedBySkillId,
			summonObjectId,
			notificationPacket,
			releasePacketSequencePlan,
			[notificationPacket, .. releasePacketSequencePlan.PacketsInOrder],
			ShouldSendToMaster: true,
			javaSource
		);
	}
}
