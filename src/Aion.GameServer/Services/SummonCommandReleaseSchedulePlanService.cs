using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonCommandReleaseSchedulePlanStatus
{
	PlanCreated,
	BlockedEmptySummonName,
	BlockedInvalidSummonUpdateSnapshot,
}

public sealed record SummonCommandReleaseSchedulePlan(
	SummonCommandReleaseSchedulePlanStatus Status,
	string? SummonName,
	SmSystemMessage? WarningPacket,
	SummonUpdatePacketPlan? SummonUpdatePacketPlan,
	IReadOnlyList<GameServerPacket> ImmediatePacketsInOrder,
	int ScheduledReleaseDelayMilliseconds,
	bool ShouldSendToMaster,
	bool ShouldStoreReleaseTask,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonCommandReleaseSchedulePlanService
{
	public const int ReleaseDelayMilliseconds = 5000;

	public static SummonCommandReleaseSchedulePlan CreatePlan(string? summonName, SummonUpdateSnapshot summonUpdateSnapshot)
	{
		// Java parity: SummonsService.ReleaseSummonTask.scheduleOrRun schedules a 5000 ms delayed release.
		// For COMMAND it immediately sends STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(summon.getL10n()),
		// then SM_SUMMON_UPDATE to the master, and stores the release task on the summon.
		if (string.IsNullOrWhiteSpace(summonName))
		{
			return new SummonCommandReleaseSchedulePlan(
				SummonCommandReleaseSchedulePlanStatus.BlockedEmptySummonName,
				summonName,
				WarningPacket: null,
				SummonUpdatePacketPlan: null,
				ImmediatePacketsInOrder: [],
				ScheduledReleaseDelayMilliseconds: 0,
				ShouldSendToMaster: false,
				ShouldStoreReleaseTask: false,
				"SummonsService.ReleaseSummonTask.scheduleOrRun -> SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(summon.getL10n()) requires a non-empty summon name"
			);
		}

		var warningPacket = SmSystemMessage.SkillSummonUnsummonFollower(summonName);
		var summonUpdatePacketPlan = SummonUpdatePacketPlanService.CreateSendToMasterPlan(
			summonUpdateSnapshot,
			"SummonsService.ReleaseSummonTask.scheduleOrRun -> PacketSendUtility.sendPacket(summon.getMaster(), new SM_SUMMON_UPDATE(summon))");
		if (summonUpdatePacketPlan.Status != SummonUpdatePacketPlanStatus.PacketCreated)
		{
			return new SummonCommandReleaseSchedulePlan(
				SummonCommandReleaseSchedulePlanStatus.BlockedInvalidSummonUpdateSnapshot,
				summonName,
				warningPacket,
				summonUpdatePacketPlan,
				ImmediatePacketsInOrder: [],
				ScheduledReleaseDelayMilliseconds: 0,
				ShouldSendToMaster: false,
				ShouldStoreReleaseTask: false,
				"SummonsService.ReleaseSummonTask.scheduleOrRun -> STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(summon.getL10n()) -> SM_SUMMON_UPDATE requires a valid summon update snapshot"
			);
		}

		return new SummonCommandReleaseSchedulePlan(
			SummonCommandReleaseSchedulePlanStatus.PlanCreated,
			summonName,
			warningPacket,
			summonUpdatePacketPlan,
			[warningPacket, summonUpdatePacketPlan.Packet!],
			ScheduledReleaseDelayMilliseconds: ReleaseDelayMilliseconds,
			ShouldSendToMaster: true,
			ShouldStoreReleaseTask: true,
			"SummonsService.ReleaseSummonTask.scheduleOrRun -> ThreadPoolManager.schedule(this, 5000) -> SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_UNSUMMON_FOLLOWER(summon.getL10n()) -> SM_SUMMON_UPDATE -> summon.setReleaseTask(releaseTask)"
		);
	}
}
