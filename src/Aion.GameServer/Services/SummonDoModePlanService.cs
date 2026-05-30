using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonDoModeType
{
	Rest,
	Guard,
	Attack,
	Release,
	Unk,
}

public enum SummonDoModePlanStatus
{
	PlanCreated,
	SkippedDeadSummon,
	SkippedNoMaster,
	SkippedReleaseNullUnsummonType,
}

public sealed record SummonDoModePlan(
	SummonDoModePlanStatus Status,
	SummonDoModeType ModeType,
	SummonReleaseUnsummonType? UnsummonType,
	int TargetObjectId,
	bool ShouldCancelReleaseTask,
	SummonModeChangePlan? ModeChangePlan,
	SummonReleaseUnsummonType? ReleasedUnsummonType,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonDoModePlanService
{
	public static SummonDoModePlan CreatePlan(
		SummonDoModeType modeType,
		bool isDead,
		bool hasMaster,
		SummonReleaseUnsummonType? unsummonType,
		int targetObjectId,
		string? summonName,
		SummonUpdateSnapshot? summonUpdateSnapshot)
	{
		// Java parity: SummonsService.doMode(SummonMode, Summon, int, UnsummonType).
		// Guard order: dead -> null master -> cancelReleaseTask (COMMAND non-RELEASE) -> mode dispatch.
		if (isDead)
		{
			return new SummonDoModePlan(
				SummonDoModePlanStatus.SkippedDeadSummon,
				modeType,
				unsummonType,
				targetObjectId,
				ShouldCancelReleaseTask: false,
				ModeChangePlan: null,
				ReleasedUnsummonType: null,
				"SummonsService.doMode -> summon.isDead() -> return"
			);
		}

		if (!hasMaster)
		{
			return new SummonDoModePlan(
				SummonDoModePlanStatus.SkippedNoMaster,
				modeType,
				unsummonType,
				targetObjectId,
				ShouldCancelReleaseTask: false,
				ModeChangePlan: null,
				ReleasedUnsummonType: null,
				"SummonsService.doMode -> summon.getMaster() == null -> return"
			);
		}

		var shouldCancelReleaseTask =
			unsummonType == SummonReleaseUnsummonType.Command
			&& modeType != SummonDoModeType.Release;

		if (modeType == SummonDoModeType.Release)
		{
			if (unsummonType == null)
			{
				return new SummonDoModePlan(
					SummonDoModePlanStatus.SkippedReleaseNullUnsummonType,
					modeType,
					unsummonType,
					targetObjectId,
					ShouldCancelReleaseTask: false,
					ModeChangePlan: null,
					ReleasedUnsummonType: null,
					"SummonsService.doMode -> RELEASE -> unsummonType == null -> no-op"
				);
			}

			return new SummonDoModePlan(
				SummonDoModePlanStatus.PlanCreated,
				modeType,
				unsummonType,
				targetObjectId,
				ShouldCancelReleaseTask: false,
				ModeChangePlan: null,
				ReleasedUnsummonType: unsummonType,
				"SummonsService.doMode -> RELEASE -> summon.getController().release(unsummonType) [live; use SummonReleaseNotificationPlanService for packet intent]"
			);
		}

		// REST / GUARD / ATTACK / UNK: compose packet plan from SummonModeChangePlanService.
		var modeChangeType = modeType switch
		{
			SummonDoModeType.Rest => SummonModeChangeType.Rest,
			SummonDoModeType.Guard => SummonModeChangeType.Guard,
			SummonDoModeType.Attack => SummonModeChangeType.Attack,
			SummonDoModeType.Unk => SummonModeChangeType.Unk,
			_ => SummonModeChangeType.Unk,
		};

		SummonModeChangePlan? modeChangePlan = null;
		if (summonUpdateSnapshot != null)
		{
			modeChangePlan = SummonModeChangePlanService.CreatePlan(modeChangeType, summonName, summonUpdateSnapshot);
		}

		var attackTargetNote = modeType == SummonDoModeType.Attack && targetObjectId != 0
			? $" targetObjId={targetObjectId} [passed to live attackMode(int); no packet effect in this planner]"
			: string.Empty;

		return new SummonDoModePlan(
			SummonDoModePlanStatus.PlanCreated,
			modeType,
			unsummonType,
			targetObjectId,
			shouldCancelReleaseTask,
			modeChangePlan,
			ReleasedUnsummonType: null,
			$"SummonsService.doMode -> {modeType} -> summon.getController().{modeType.ToString().ToLowerInvariant()}Mode(){attackTargetNote} [live side effects deferred]"
		);
	}
}
