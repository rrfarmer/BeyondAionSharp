namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneActorPlanService
{
	public const string TaskId = "ZONE_MATERIAL_ACTION";
	public const string ForceType = "MATERIAL_SKILL";
	public const int TickIntervalMilliseconds = 1000;

	public static WorldMapRegionMaterialZoneActorMovePlan CreateMovePlan(
		WorldMapRegionMaterialZoneActorMoveContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneCollisionMaterialActor.onMoved toggles isTouched
		// from CollisionResults.size(), calls act()/abort() only when touch state changes,
		// and optionally sends staff debug text with the touched or untouch geometry name.
		var isTouched = context.CollisionGeometryNames.Count > 0;
		if (isTouched == context.WasTouched)
		{
			return new WorldMapRegionMaterialZoneActorMovePlan(
				WorldMapRegionMaterialZoneActorMoveStatus.NoTouchStateChange,
				isTouched,
				Array.Empty<string>(),
				DebugMessage: null,
				"ZoneCollisionMaterialActor.onMoved no touch-state transition");
		}

		var sideEffects = new List<string>();
		string? debugMessage;
		if (isTouched)
		{
			if (context.Skills.Count > 0 && !context.HasScheduledTask)
			{
				sideEffects.Add("ThreadPoolManager.scheduleAtFixedRate");
				sideEffects.Add($"creature controller task added: {TaskId}");
			}

			debugMessage = context.ShowDetailsToStaff
				? $"Touched {context.CollisionGeometryNames[0]}"
				: null;
		}
		else
		{
			if (context.HasScheduledTask)
				sideEffects.Add($"creature controller task cancelled: {TaskId}");

			debugMessage = context.ShowDetailsToStaff
				? $"Untouched {context.GeometryName}"
				: null;
		}

		if (debugMessage is not null)
			sideEffects.Add($"staff debug message: {debugMessage}");

		return new WorldMapRegionMaterialZoneActorMovePlan(
			isTouched
				? WorldMapRegionMaterialZoneActorMoveStatus.TouchStarted
				: WorldMapRegionMaterialZoneActorMoveStatus.TouchEnded,
			isTouched,
			sideEffects,
			debugMessage,
			"ZoneCollisionMaterialActor.onMoved non-live behavior plan");
	}

	public static WorldMapRegionMaterialZoneActorTickPlan CreateTickPlan(
		WorldMapRegionMaterialZoneActorTickContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: AbstractMaterialSkillActor.MaterialSkillTask.run gates
		// by previous skill frequency, touch state, spawned/dead/protection flags, then
		// applies the first skill whose conditions match with ForceType.MATERIAL_SKILL.
		var frequency = context.PreviousSkillFrequency ?? 1;
		if (context.SecondsElapsed % frequency != 0)
			return SkippedTick(WorldMapRegionMaterialZoneActorTickStatus.SkippedFrequencyGate, "frequency gate");
		if (!context.IsTouched)
			return SkippedTick(WorldMapRegionMaterialZoneActorTickStatus.SkippedNotTouched, "not touched");
		if (!context.IsSpawned || context.IsDead)
			return SkippedTick(WorldMapRegionMaterialZoneActorTickStatus.SkippedInactiveCreature, "creature not spawned or dead");
		if (context.IsPlayerProtectionActive)
			return SkippedTick(WorldMapRegionMaterialZoneActorTickStatus.SkippedPlayerProtection, "player protection active");

		var skill = context.Skills.FirstOrDefault(skill => ConditionsMatch(skill.Conditions, context));
		if (skill is null)
			return SkippedTick(WorldMapRegionMaterialZoneActorTickStatus.SkippedNoMatchingCondition, "no matching material condition");

		var sideEffects = new List<string>();
		if (context.ShowDetailsToStaff)
			sideEffects.Add($"staff debug message: ZoneCollisionMaterialActor use skill={skill.SkillId}");
		sideEffects.Add($"SkillEngine.applyEffectDirectly {skill.SkillId}:{skill.SkillLevel} {ForceType}");

		return new WorldMapRegionMaterialZoneActorTickPlan(
			WorldMapRegionMaterialZoneActorTickStatus.SkillApplied,
			skill.SkillId,
			skill.SkillLevel,
			ForceType,
			sideEffects,
			"AbstractMaterialSkillActor.MaterialSkillTask.run non-live behavior plan");
	}

	public static WorldMapRegionMaterialZoneActorMovePlan CreateDiedPlan(bool hasScheduledTask)
	{
		var sideEffects = hasScheduledTask
			? new[] { $"creature controller task cancelled: {TaskId}" }
			: Array.Empty<string>();
		return new WorldMapRegionMaterialZoneActorMovePlan(
			WorldMapRegionMaterialZoneActorMoveStatus.TouchEnded,
			IsTouched: false,
			sideEffects,
			DebugMessage: null,
			"AbstractMaterialSkillActor.died sets isTouched=false and aborts");
	}

	private static WorldMapRegionMaterialZoneActorTickPlan SkippedTick(
		WorldMapRegionMaterialZoneActorTickStatus status,
		string javaSource)
	{
		return new WorldMapRegionMaterialZoneActorTickPlan(
			status,
			AppliedSkillId: null,
			AppliedSkillLevel: null,
			AppliedForceType: null,
			Array.Empty<string>(),
			$"AbstractMaterialSkillActor.MaterialSkillTask.run skipped: {javaSource}");
	}

	private static bool ConditionsMatch(
		IReadOnlyList<WorldMapRegionMaterialZoneActCondition> conditions,
		WorldMapRegionMaterialZoneActorTickContext context)
	{
		if (conditions.Count == 0)
			return true;

		foreach (var condition in conditions)
		{
			if (condition == WorldMapRegionMaterialZoneActCondition.Night
				&& context.DayTime == WorldMapRegionMaterialZoneDayTime.Night)
			{
				return true;
			}

			if (condition == WorldMapRegionMaterialZoneActCondition.Sunny
				&& (context.WeatherName is null
					|| !context.WeatherName.StartsWith("RAIN", StringComparison.Ordinal)
					|| context.WeatherIsBefore))
			{
				return true;
			}
		}

		return false;
	}
}

public sealed record WorldMapRegionMaterialZoneActorMoveContext(
	string GeometryName,
	IReadOnlyList<string> CollisionGeometryNames,
	bool WasTouched,
	bool HasScheduledTask,
	bool ShowDetailsToStaff,
	IReadOnlyList<WorldMapRegionMaterialZoneActorSkillSnapshot> Skills);

public sealed record WorldMapRegionMaterialZoneActorTickContext(
	int SecondsElapsed,
	int? PreviousSkillFrequency,
	bool IsTouched,
	bool IsSpawned,
	bool IsDead,
	bool IsPlayerProtectionActive,
	bool ShowDetailsToStaff,
	WorldMapRegionMaterialZoneDayTime DayTime,
	string? WeatherName,
	bool WeatherIsBefore,
	IReadOnlyList<WorldMapRegionMaterialZoneActorSkillSnapshot> Skills);

public sealed record WorldMapRegionMaterialZoneActorSkillSnapshot(
	int SkillId,
	int SkillLevel,
	int Frequency,
	IReadOnlyList<WorldMapRegionMaterialZoneActCondition> Conditions);

public sealed record WorldMapRegionMaterialZoneActorMovePlan(
	WorldMapRegionMaterialZoneActorMoveStatus Status,
	bool IsTouched,
	IReadOnlyList<string> SideEffects,
	string? DebugMessage,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneActorTickPlan(
	WorldMapRegionMaterialZoneActorTickStatus Status,
	int? AppliedSkillId,
	int? AppliedSkillLevel,
	string? AppliedForceType,
	IReadOnlyList<string> SideEffects,
	string JavaSource);

public enum WorldMapRegionMaterialZoneActorMoveStatus
{
	NoTouchStateChange,
	TouchStarted,
	TouchEnded,
}

public enum WorldMapRegionMaterialZoneActorTickStatus
{
	SkillApplied,
	SkippedFrequencyGate,
	SkippedNotTouched,
	SkippedInactiveCreature,
	SkippedPlayerProtection,
	SkippedNoMatchingCondition,
}

public enum WorldMapRegionMaterialZoneActCondition
{
	Sunny,
	Night,
}

public enum WorldMapRegionMaterialZoneDayTime
{
	Morning,
	Afternoon,
	Evening,
	Night,
}
