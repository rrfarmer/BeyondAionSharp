namespace Aion.GameServer.Services;

public enum BindPointTeleportSkillUseTaskPlanStatus
{
	ScheduleNewTask,
	ReplaceExistingTask,
	CancelExistingTask,
	NoTaskToCancel,
}

public enum BindPointTeleportSkillUseTaskPlanStep
{
	CheckTaskIdSkillUse,
	CancelExistingTask,
	ScheduleDelayedTask,
	StoreTask,
	RemoveTask,
}

public sealed record BindPointTeleportSkillUseTaskPlan(
	BindPointTeleportSkillUseTaskPlanStatus Status,
	int PlayerObjectId,
	int LocId,
	bool HasExistingTask,
	bool ShouldCancelExistingTask,
	bool ShouldScheduleTask,
	bool ShouldStoreTask,
	int DelayMilliseconds,
	string TaskIdName,
	int TaskIdOrdinal,
	IReadOnlyList<BindPointTeleportSkillUseTaskPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public enum BindPointTeleportCooldownPlanStatus
{
	AddCooldown,
	ActiveCooldown,
	NoCooldown,
	ExpiredCooldown,
}

public enum BindPointTeleportCooldownPlanStep
{
	CheckCooldownMap,
	CalculateTimeLeft,
	PutCooldown,
}

public sealed record BindPointTeleportCooldownFact(
	int PlayerObjectId,
	int LocId,
	long CooldownEndMillis);

public sealed record BindPointTeleportCooldownPlan(
	BindPointTeleportCooldownPlanStatus Status,
	int PlayerObjectId,
	int? LocId,
	long? CooldownEndMillis,
	int TimeLeftSeconds,
	bool ShouldStoreCooldown,
	IReadOnlyList<BindPointTeleportCooldownPlanStep> Steps,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportRuntimeStatePlanService
{
	public const int SkillUseTaskIdOrdinal = 16;
	public const string SkillUseTaskIdName = "TaskId.SKILL_USE";
	public const int SkillUseDelayMilliseconds = 10_000;
	public const int CooldownSeconds = 600;

	public static BindPointTeleportSkillUseTaskPlan CreateScheduleSkillUseTaskPlan(
		int playerObjectId,
		int locId,
		bool hasExistingSkillUseTask)
	{
		// Java parity: CreatureController.addTask(TaskId.SKILL_USE, Future) replaces and cancels an old task if present.
		var steps = new List<BindPointTeleportSkillUseTaskPlanStep>
		{
			BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse,
		};

		if (hasExistingSkillUseTask)
			steps.Add(BindPointTeleportSkillUseTaskPlanStep.CancelExistingTask);

		steps.Add(BindPointTeleportSkillUseTaskPlanStep.ScheduleDelayedTask);
		steps.Add(BindPointTeleportSkillUseTaskPlanStep.StoreTask);

		return new BindPointTeleportSkillUseTaskPlan(
			hasExistingSkillUseTask
				? BindPointTeleportSkillUseTaskPlanStatus.ReplaceExistingTask
				: BindPointTeleportSkillUseTaskPlanStatus.ScheduleNewTask,
			playerObjectId,
			locId,
			HasExistingTask: hasExistingSkillUseTask,
			ShouldCancelExistingTask: hasExistingSkillUseTask,
			ShouldScheduleTask: true,
			ShouldStoreTask: true,
			SkillUseDelayMilliseconds,
			SkillUseTaskIdName,
			SkillUseTaskIdOrdinal,
			steps,
			"BindPointTeleportService.teleport -> player.getController().addTask(TaskId.SKILL_USE, ThreadPoolManager.schedule(..., 10000)); CreatureController.addTask replaces old task with cancel(false)",
			IsLive: false);
	}

	public static BindPointTeleportSkillUseTaskPlan CreateCancelSkillUseTaskPlan(
		int playerObjectId,
		int locId,
		bool hasExistingSkillUseTask)
	{
		// Java parity: BindPointTeleportService.cancelTeleport checks hasTask(TaskId.SKILL_USE) before cancelTask.
		if (!hasExistingSkillUseTask)
		{
			return new BindPointTeleportSkillUseTaskPlan(
				BindPointTeleportSkillUseTaskPlanStatus.NoTaskToCancel,
				playerObjectId,
				locId,
				HasExistingTask: false,
				ShouldCancelExistingTask: false,
				ShouldScheduleTask: false,
				ShouldStoreTask: false,
				DelayMilliseconds: 0,
				SkillUseTaskIdName,
				SkillUseTaskIdOrdinal,
				[BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse],
				"BindPointTeleportService.cancelTeleport -> if (!hasTask(TaskId.SKILL_USE)) no-op",
				IsLive: false);
		}

		return new BindPointTeleportSkillUseTaskPlan(
			BindPointTeleportSkillUseTaskPlanStatus.CancelExistingTask,
			playerObjectId,
			locId,
			HasExistingTask: true,
			ShouldCancelExistingTask: true,
			ShouldScheduleTask: false,
			ShouldStoreTask: false,
			DelayMilliseconds: 0,
			SkillUseTaskIdName,
			SkillUseTaskIdOrdinal,
			[
				BindPointTeleportSkillUseTaskPlanStep.CheckTaskIdSkillUse,
				BindPointTeleportSkillUseTaskPlanStep.RemoveTask,
				BindPointTeleportSkillUseTaskPlanStep.CancelExistingTask,
			],
			"BindPointTeleportService.cancelTeleport -> player.getController().cancelTask(TaskId.SKILL_USE); CreatureController.cancelTask removes task then cancel(false)",
			IsLive: false);
	}

	public static BindPointTeleportCooldownPlan CreateAddCooldownPlan(
		int playerObjectId,
		int locId,
		long currentTimeMillis)
	{
		// Java parity: addCooldown stores System.currentTimeMillis() + COOLDOWN_IN_SECONDS * 1000.
		var cooldownEndMillis = currentTimeMillis + CooldownSeconds * 1000L;

		return new BindPointTeleportCooldownPlan(
			BindPointTeleportCooldownPlanStatus.AddCooldown,
			playerObjectId,
			locId,
			cooldownEndMillis,
			CooldownSeconds,
			ShouldStoreCooldown: true,
			[BindPointTeleportCooldownPlanStep.PutCooldown],
			"BindPointTeleportService.addCooldown -> cooldowns.put(player.getObjectId(), new Cooldown(locId, now + 600000))",
			IsLive: false);
	}

	public static BindPointTeleportCooldownPlan CreateLookupCooldownPlan(
		int playerObjectId,
		BindPointTeleportCooldownFact? cooldownFact,
		long currentTimeMillis)
	{
		// Java parity: getCooldown(player) reads the static player-id keyed map, then Cooldown.getTimeLeft floors whole seconds.
		if (cooldownFact == null || cooldownFact.PlayerObjectId != playerObjectId)
		{
			return new BindPointTeleportCooldownPlan(
				BindPointTeleportCooldownPlanStatus.NoCooldown,
				playerObjectId,
				LocId: null,
				CooldownEndMillis: null,
				TimeLeftSeconds: 0,
				ShouldStoreCooldown: false,
				[BindPointTeleportCooldownPlanStep.CheckCooldownMap],
				"BindPointTeleportService.getCooldown -> cooldowns.get(player.getObjectId()) returned null",
				IsLive: false);
		}

		var timeLeftSeconds = CalculateJavaTimeLeftSeconds(cooldownFact.CooldownEndMillis, currentTimeMillis);
		var status = timeLeftSeconds > 0
			? BindPointTeleportCooldownPlanStatus.ActiveCooldown
			: BindPointTeleportCooldownPlanStatus.ExpiredCooldown;

		return new BindPointTeleportCooldownPlan(
			status,
			playerObjectId,
			cooldownFact.LocId,
			cooldownFact.CooldownEndMillis,
			timeLeftSeconds,
			ShouldStoreCooldown: false,
			[
				BindPointTeleportCooldownPlanStep.CheckCooldownMap,
				BindPointTeleportCooldownPlanStep.CalculateTimeLeft,
			],
			status == BindPointTeleportCooldownPlanStatus.ActiveCooldown
				? "BindPointTeleportService.onLogin/checkRequirements -> cooldown.getTimeLeft() > 0"
				: "Cooldown.getTimeLeft -> expired or non-positive time returns 0",
			IsLive: false);
	}

	public static int CalculateJavaTimeLeftSeconds(long cooldownEndMillis, long currentTimeMillis)
	{
		var estimated = (int)((cooldownEndMillis - currentTimeMillis) / 1000);
		return estimated > 0 ? estimated : 0;
	}
}
