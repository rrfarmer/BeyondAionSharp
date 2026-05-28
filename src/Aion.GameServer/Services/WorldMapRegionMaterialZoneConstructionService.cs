namespace Aion.GameServer.Services;

public static class WorldMapRegionMaterialZoneConstructionService
{
	public const int ShieldMaterialId = 11;

	public static WorldMapRegionMaterialZoneConstructionPlan CreatePlan(
		WorldMapRegionMaterialZoneConstructionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Java parity breadcrumb: ZoneService.createMaterialZoneTemplate returns for ZoneName.NONE,
		// creates or reuses a collidable handler, then creates MaterialZoneTemplate area metadata
		// only when no ZoneInfo exists for the zone name.
		if (string.Equals(context.ZoneName, "NONE", StringComparison.Ordinal))
			return Blocked(context, WorldMapRegionMaterialZoneConstructionStatus.IgnoredNoneZoneName, "ZoneName.NONE returns without mutation");

		var sideEffects = new List<string>();
		WorldMapRegionMaterialZoneHandlerKind handlerKind;
		if (context.ExistingCollidableHandler)
		{
			handlerKind = WorldMapRegionMaterialZoneHandlerKind.Existing;
			sideEffects.Add("Duplicate material mesh warning");
		}
		else if (context.MaterialId == ShieldMaterialId)
		{
			if (!context.ShieldCanRegister)
				return Blocked(context, WorldMapRegionMaterialZoneConstructionStatus.BlockedMissingShieldHandler, "ShieldService.tryRegisterShield returned null");

			handlerKind = WorldMapRegionMaterialZoneHandlerKind.Shield;
			sideEffects.Add("ShieldService.tryRegisterShield");
			sideEffects.Add("collidable handler registered");
		}
		else
		{
			if (!context.MaterialTemplateExists)
				return Blocked(context, WorldMapRegionMaterialZoneConstructionStatus.BlockedMissingMaterialTemplate, "DataManager.MATERIAL_DATA.getTemplate returned null");

			handlerKind = WorldMapRegionMaterialZoneHandlerKind.Material;
			sideEffects.Add("MaterialZoneHandler created");
			sideEffects.Add("collidable handler registered");
		}

		if (context.WorldHadNoAreaList)
			sideEffects.Add("zoneByMapIdMap list created");

		if (context.ExistingZoneInfo)
		{
			return new WorldMapRegionMaterialZoneConstructionPlan(
				context.WorldId,
				context.ZoneName,
				handlerKind,
				WorldMapRegionMaterialZoneAreaKind.Existing,
				ZoneInfoCreated: false,
				WorldMapRegionMaterialZoneConstructionStatus.ReusedExistingZoneInfo,
				sideEffects,
				"ZoneService.createMaterialZoneTemplate reused existing ZoneInfo");
		}

		var areaKind = SelectAreaKind(context.GeometryName);
		sideEffects.Add("MaterialZoneTemplate created");
		sideEffects.Add(areaKind == WorldMapRegionMaterialZoneAreaKind.None
			? "no supported material area geometry"
			: "ZoneInfo added to zoneByMapIdMap");

		return new WorldMapRegionMaterialZoneConstructionPlan(
			context.WorldId,
			context.ZoneName,
			handlerKind,
			areaKind,
			ZoneInfoCreated: areaKind != WorldMapRegionMaterialZoneAreaKind.None,
			WorldMapRegionMaterialZoneConstructionStatus.Created,
			sideEffects,
			"ZoneService.createMaterialZoneTemplate non-live construction plan");
	}

	private static WorldMapRegionMaterialZoneConstructionPlan Blocked(
		WorldMapRegionMaterialZoneConstructionContext context,
		WorldMapRegionMaterialZoneConstructionStatus status,
		string javaSource)
	{
		return new WorldMapRegionMaterialZoneConstructionPlan(
			context.WorldId,
			context.ZoneName,
			WorldMapRegionMaterialZoneHandlerKind.None,
			WorldMapRegionMaterialZoneAreaKind.None,
			ZoneInfoCreated: false,
			status,
			Array.Empty<string>(),
			javaSource);
	}

	private static WorldMapRegionMaterialZoneAreaKind SelectAreaKind(string geometryName)
	{
		if (geometryName.Contains("CYLINDER", StringComparison.Ordinal)
			|| geometryName.Contains("CONE", StringComparison.Ordinal)
			|| geometryName.Contains("H_COLUME", StringComparison.Ordinal))
		{
			return WorldMapRegionMaterialZoneAreaKind.Cylinder;
		}

		return geometryName.Contains("SEMISPHERE", StringComparison.Ordinal)
			? WorldMapRegionMaterialZoneAreaKind.Semisphere
			: WorldMapRegionMaterialZoneAreaKind.Sphere;
	}
}

public sealed record WorldMapRegionMaterialZoneConstructionContext(
	int WorldId,
	string ZoneName,
	string GeometryName,
	int MaterialId,
	bool ExistingCollidableHandler,
	bool ShieldCanRegister,
	bool MaterialTemplateExists,
	bool WorldHadNoAreaList,
	bool ExistingZoneInfo);

public sealed record WorldMapRegionMaterialZoneConstructionPlan(
	int WorldId,
	string ZoneName,
	WorldMapRegionMaterialZoneHandlerKind HandlerKind,
	WorldMapRegionMaterialZoneAreaKind AreaKind,
	bool ZoneInfoCreated,
	WorldMapRegionMaterialZoneConstructionStatus Status,
	IReadOnlyList<string> SideEffects,
	string JavaSource);

public enum WorldMapRegionMaterialZoneHandlerKind
{
	None,
	Existing,
	Shield,
	Material,
}

public enum WorldMapRegionMaterialZoneAreaKind
{
	None,
	Existing,
	Sphere,
	Cylinder,
	Semisphere,
}

public enum WorldMapRegionMaterialZoneConstructionStatus
{
	Created,
	ReusedExistingZoneInfo,
	IgnoredNoneZoneName,
	BlockedMissingShieldHandler,
	BlockedMissingMaterialTemplate,
}
