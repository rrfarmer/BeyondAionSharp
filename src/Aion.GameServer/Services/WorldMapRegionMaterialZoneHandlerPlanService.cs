namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneHandlerPlanService
{
	public static WorldMapRegionMaterialZoneHandlerEnterPlan CreateEnterPlan(
		WorldMapRegionMaterialZoneHandlerContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: MaterialZoneHandler.onEnterZone skips owner race,
		// filters MaterialSkill by target, creates a ZoneCollisionMaterialActor with
		// PASS for material ids 14..16 and TOUCH otherwise, registers the observer,
		// stores it by creature object id, optionally sends staff debug text, then calls moved().
		var ownerRace = GetOwnerRace(context.GeometryName);
		if (ownerRace == context.CreatureRace)
		{
			return new WorldMapRegionMaterialZoneHandlerEnterPlan(
				WorldMapRegionMaterialZoneHandlerEnterStatus.IgnoredOwnerRace,
				ownerRace,
				Array.Empty<int>(),
				WorldMapRegionMaterialZoneCollisionCheckType.None,
				Array.Empty<string>(),
				"MaterialZoneHandler.onEnterZone owner race guard");
		}

		var matchingSkillIds = context.Skills
			.Where(skill => TargetMatches(skill.Target, context.CreatureKind, context.HasSummonMaster))
			.Select(skill => skill.SkillId)
			.ToArray();
		if (matchingSkillIds.Length == 0)
		{
			return new WorldMapRegionMaterialZoneHandlerEnterPlan(
				WorldMapRegionMaterialZoneHandlerEnterStatus.NoMatchingSkills,
				ownerRace,
				matchingSkillIds,
				WorldMapRegionMaterialZoneCollisionCheckType.None,
				Array.Empty<string>(),
				"MaterialZoneHandler.onEnterZone matching skill guard");
		}

		var sideEffects = new List<string>
		{
			"ZoneCollisionMaterialActor created",
			"observer added",
			"observed actor stored by creature object id",
			"actor.moved invoked",
		};
		if (context.ShowDetailsToStaff)
			sideEffects.Add($"staff debug message: Entered material zone {context.GeometryName}");

		return new WorldMapRegionMaterialZoneHandlerEnterPlan(
			WorldMapRegionMaterialZoneHandlerEnterStatus.ObserverRegistered,
			ownerRace,
			matchingSkillIds,
			context.MaterialId is >= 14 and <= 16
				? WorldMapRegionMaterialZoneCollisionCheckType.Pass
				: WorldMapRegionMaterialZoneCollisionCheckType.Touch,
			sideEffects,
			"MaterialZoneHandler.onEnterZone non-live behavior plan");
	}

	public static WorldMapRegionMaterialZoneHandlerLeavePlan CreateLeavePlan(
		WorldMapRegionMaterialZoneHandlerContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: MaterialZoneHandler.onLeaveZone removes the observed actor
		// for the creature object id, removes the observer, aborts the actor, and optionally
		// sends staff debug text.
		var hadObservedActor = context.ObservedCreatureObjectIds.Contains(context.CreatureObjectId);
		var sideEffects = new List<string>();
		if (hadObservedActor)
		{
			sideEffects.Add("observed actor removed");
			sideEffects.Add("observer removed");
			sideEffects.Add("actor.abort invoked");
		}

		if (context.ShowDetailsToStaff)
			sideEffects.Add($"staff debug message: Left material zone {context.GeometryName}");

		return new WorldMapRegionMaterialZoneHandlerLeavePlan(
			hadObservedActor
				? WorldMapRegionMaterialZoneHandlerLeaveStatus.ObserverRemoved
				: WorldMapRegionMaterialZoneHandlerLeaveStatus.NoObservedActor,
			sideEffects,
			"MaterialZoneHandler.onLeaveZone non-live behavior plan");
	}

	private static WorldMapRegionMaterialZoneRace GetOwnerRace(string geometryName)
	{
		if (geometryName.StartsWith("BU_AB_DARKSP", StringComparison.Ordinal))
			return WorldMapRegionMaterialZoneRace.Asmodians;
		if (geometryName.StartsWith("BU_AB_LIGHTSP", StringComparison.Ordinal))
			return WorldMapRegionMaterialZoneRace.Elyos;
		return WorldMapRegionMaterialZoneRace.None;
	}

	private static bool TargetMatches(
		WorldMapRegionMaterialZoneSkillTarget target,
		WorldMapRegionMaterialZoneCreatureKind creatureKind,
		bool hasSummonMaster)
	{
		return target switch
		{
			WorldMapRegionMaterialZoneSkillTarget.All => true,
			WorldMapRegionMaterialZoneSkillTarget.Npc => creatureKind == WorldMapRegionMaterialZoneCreatureKind.Npc,
			WorldMapRegionMaterialZoneSkillTarget.Player => creatureKind == WorldMapRegionMaterialZoneCreatureKind.Player,
			WorldMapRegionMaterialZoneSkillTarget.PlayerWithPet => creatureKind == WorldMapRegionMaterialZoneCreatureKind.Player
				|| (creatureKind == WorldMapRegionMaterialZoneCreatureKind.Summon && hasSummonMaster),
			_ => false,
		};
	}
}

public sealed record WorldMapRegionMaterialZoneHandlerContext(
	string GeometryName,
	int MaterialId,
	int CreatureObjectId,
	WorldMapRegionMaterialZoneRace CreatureRace,
	WorldMapRegionMaterialZoneCreatureKind CreatureKind,
	bool HasSummonMaster,
	bool ShowDetailsToStaff,
	IReadOnlyList<WorldMapRegionMaterialZoneSkillSnapshot> Skills,
	IReadOnlySet<int> ObservedCreatureObjectIds);

public sealed record WorldMapRegionMaterialZoneSkillSnapshot(
	int SkillId,
	WorldMapRegionMaterialZoneSkillTarget Target);

public sealed record WorldMapRegionMaterialZoneHandlerEnterPlan(
	WorldMapRegionMaterialZoneHandlerEnterStatus Status,
	WorldMapRegionMaterialZoneRace OwnerRace,
	IReadOnlyList<int> MatchingSkillIds,
	WorldMapRegionMaterialZoneCollisionCheckType CheckType,
	IReadOnlyList<string> SideEffects,
	string JavaSource);

public sealed record WorldMapRegionMaterialZoneHandlerLeavePlan(
	WorldMapRegionMaterialZoneHandlerLeaveStatus Status,
	IReadOnlyList<string> SideEffects,
	string JavaSource);

public enum WorldMapRegionMaterialZoneRace
{
	None,
	Elyos,
	Asmodians,
}

public enum WorldMapRegionMaterialZoneCreatureKind
{
	Creature,
	Player,
	Npc,
	Summon,
}

public enum WorldMapRegionMaterialZoneSkillTarget
{
	All,
	Npc,
	Player,
	PlayerWithPet,
}

public enum WorldMapRegionMaterialZoneCollisionCheckType
{
	None,
	Touch,
	Pass,
}

public enum WorldMapRegionMaterialZoneHandlerEnterStatus
{
	ObserverRegistered,
	IgnoredOwnerRace,
	NoMatchingSkills,
}

public enum WorldMapRegionMaterialZoneHandlerLeaveStatus
{
	ObserverRemoved,
	NoObservedActor,
}
