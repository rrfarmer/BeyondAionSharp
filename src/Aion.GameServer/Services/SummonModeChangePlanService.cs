using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonModeChangeType
{
	Rest,
	Guard,
	Attack,
	Unk,
}

public enum SummonModeChangePlanStatus
{
	PlanCreated,
	BlockedEmptySummonName,
	BlockedInvalidSummonUpdateSnapshot,
}

public sealed record SummonModeChangePlan(
	SummonModeChangePlanStatus Status,
	SummonModeChangeType ModeChangeType,
	string? SummonName,
	SmSystemMessage? ModeMessage,
	SummonUpdatePacketPlan? SummonUpdatePacketPlan,
	IReadOnlyList<GameServerPacket> ImmediatePacketsInOrder,
	bool ShouldSendToMaster,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonModeChangePlanService
{
	public static SummonModeChangePlan CreatePlan(SummonModeChangeType modeChangeType, string? summonName, SummonUpdateSnapshot summonUpdateSnapshot)
	{
		// Java parity: SummonsService.restMode, guardMode, attackMode, setUnkMode each set the mode and send
		// a system message (except UNK) and then SM_SUMMON_UPDATE to the master.
		// Live cancelCurrentSkill, setMode, triggerRestoreTask, and cancelRestoreTask side effects are deferred.
		var requiresSummonName = modeChangeType != SummonModeChangeType.Unk;
		if (requiresSummonName && string.IsNullOrWhiteSpace(summonName))
		{
			return new SummonModeChangePlan(
				SummonModeChangePlanStatus.BlockedEmptySummonName,
				modeChangeType,
				summonName,
				ModeMessage: null,
				SummonUpdatePacketPlan: null,
				ImmediatePacketsInOrder: [],
				ShouldSendToMaster: false,
				BuildJavaSource(modeChangeType) + " -> requires a non-empty summon name"
			);
		}

		var summonUpdateJavaSource = BuildSummonUpdateJavaSource(modeChangeType);
		var summonUpdatePlan = SummonUpdatePacketPlanService.CreateSendToMasterPlan(summonUpdateSnapshot, summonUpdateJavaSource);
		if (summonUpdatePlan.Status != SummonUpdatePacketPlanStatus.PacketCreated)
		{
			return new SummonModeChangePlan(
				SummonModeChangePlanStatus.BlockedInvalidSummonUpdateSnapshot,
				modeChangeType,
				summonName,
				ModeMessage: null,
				summonUpdatePlan,
				ImmediatePacketsInOrder: [],
				ShouldSendToMaster: false,
				BuildJavaSource(modeChangeType) + " -> SM_SUMMON_UPDATE requires a valid snapshot"
			);
		}

		var modeMessage = BuildModeMessage(modeChangeType, summonName);
		var packets = modeMessage != null
			? (IReadOnlyList<GameServerPacket>)[modeMessage, summonUpdatePlan.Packet!]
			: (IReadOnlyList<GameServerPacket>)[summonUpdatePlan.Packet!];

		return new SummonModeChangePlan(
			SummonModeChangePlanStatus.PlanCreated,
			modeChangeType,
			summonName,
			modeMessage,
			summonUpdatePlan,
			packets,
			ShouldSendToMaster: true,
			BuildJavaSource(modeChangeType)
		);
	}

	private static SmSystemMessage? BuildModeMessage(SummonModeChangeType modeChangeType, string? summonName)
	{
		return modeChangeType switch
		{
			SummonModeChangeType.Rest => SmSystemMessage.SkillSummonRestMode(summonName!),
			SummonModeChangeType.Guard => SmSystemMessage.SkillSummonGuardMode(summonName!),
			SummonModeChangeType.Attack => SmSystemMessage.SkillSummonAttackMode(summonName!),
			SummonModeChangeType.Unk => null,
			_ => null,
		};
	}

	private static string BuildJavaSource(SummonModeChangeType modeChangeType)
	{
		return modeChangeType switch
		{
			SummonModeChangeType.Rest => "SummonsService.restMode -> PacketSendUtility.sendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_REST_MODE(summon.getL10n())) -> SM_SUMMON_UPDATE -> summon.getLifeStats().triggerRestoreTask() [deferred]",
			SummonModeChangeType.Guard => "SummonsService.guardMode -> PacketSendUtility.sendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_GUARD_MODE(summon.getL10n())) -> SM_SUMMON_UPDATE -> summon.getLifeStats().triggerRestoreTask() [deferred]",
			SummonModeChangeType.Attack => "SummonsService.attackMode -> PacketSendUtility.sendPacket(master, SM_SYSTEM_MESSAGE.STR_SKILL_SUMMON_ATTACK_MODE(summon.getL10n())) -> SM_SUMMON_UPDATE -> summon.getLifeStats().cancelRestoreTask() [deferred]",
			SummonModeChangeType.Unk => "SummonsService.setUnkMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))",
			_ => "SummonsService unknown mode",
		};
	}

	private static string BuildSummonUpdateJavaSource(SummonModeChangeType modeChangeType)
	{
		return modeChangeType switch
		{
			SummonModeChangeType.Rest => "SummonsService.restMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))",
			SummonModeChangeType.Guard => "SummonsService.guardMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))",
			SummonModeChangeType.Attack => "SummonsService.attackMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))",
			SummonModeChangeType.Unk => "SummonsService.setUnkMode -> PacketSendUtility.sendPacket(master, new SM_SUMMON_UPDATE(summon))",
			_ => "SummonsService unknown mode -> SM_SUMMON_UPDATE",
		};
	}
}
