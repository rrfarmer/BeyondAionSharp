using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum StaggerStumbleCalculatePlanStatus
{
	BlockedExistingAbnormal,
	BlockedCalculateFailed,
	NeedsGeoCollision,
	PlannedTargetLocation,
}

public sealed record GeoCollisionSnapshot(float X, float Y, float Z);

public sealed record StaggerStumbleCalculatePlanInput(
	ForcedMoveEffectKind EffectKind,
	ObjectPositionSnapshot EffectedPosition,
	float EffectorX,
	float EffectorY,
	bool HasPulledAbnormal,
	bool HasStaggerAbnormal,
	bool HasOpenAerialAbnormal,
	bool HasStumbleAbnormal,
	bool BaseCalculateSucceeded,
	bool IsSubEffect,
	bool IsEffectedPlayer,
	GeoCollisionSnapshot? ClosestCollision);

public sealed record StaggerStumbleCalculatePlan(
	StaggerStumbleCalculatePlanStatus Status,
	StaggerStumbleCalculatePlanInput Input,
	string ResistanceStatName,
	string SpellStatusName,
	bool ShouldSetSubEffectType,
	string? SubEffectTypeName,
	byte HeadingTowardsEffected,
	float MovementAngle,
	float RequestedCollisionX,
	float RequestedCollisionY,
	float RequestedCollisionZ,
	bool ShouldRequestGeoCollision,
	GeoCollisionSnapshot? TargetLocation,
	string JavaSource)
{
	public bool IsLive => false;
	public bool ShouldSetTargetLocation => TargetLocation is not null;
}

public static class StaggerStumbleCalculatePlanService
{
	private const float BackwardDistance = 2f;

	public static StaggerStumbleCalculatePlan CreatePlan(StaggerStumbleCalculatePlanInput input)
	{
		// Java parity breadcrumb: StaggerEffect.calculate and StumbleEffect.calculate first
		// reject existing forced-move abnormal states, then rely on EffectTemplate.calculate.
		// Successful non-player sub effects set SubEffectType, and both effects request a
		// GeoService closest-collision point two meters along the effector->effected heading.
		var javaSource = ResolveJavaSource(input.EffectKind);
		if (input.HasPulledAbnormal || input.HasStaggerAbnormal || input.HasOpenAerialAbnormal || input.HasStumbleAbnormal)
			return Blocked(input, StaggerStumbleCalculatePlanStatus.BlockedExistingAbnormal, javaSource);

		if (!input.BaseCalculateSucceeded)
			return Blocked(input, StaggerStumbleCalculatePlanStatus.BlockedCalculateFailed, javaSource);

		var heading = PositionUtilService.GetHeadingTowards(input.EffectorX, input.EffectorY, input.EffectedPosition.X, input.EffectedPosition.Y);
		var angle = PositionUtilService.ConvertHeadingToAngle(heading);
		var radian = Math.PI * angle / 180d;
		var requestedX = input.EffectedPosition.X + (float)(Math.Cos(radian) * BackwardDistance);
		var requestedY = input.EffectedPosition.Y + (float)(Math.Sin(radian) * BackwardDistance);
		var requestedZ = input.EffectedPosition.Z;
		var shouldSetSubEffectType = input.IsSubEffect && !input.IsEffectedPlayer;

		return new StaggerStumbleCalculatePlan(
			input.ClosestCollision is null
				? StaggerStumbleCalculatePlanStatus.NeedsGeoCollision
				: StaggerStumbleCalculatePlanStatus.PlannedTargetLocation,
			input,
			ResolveResistanceStatName(input.EffectKind),
			ResolveSpellStatusName(input.EffectKind),
			shouldSetSubEffectType,
			shouldSetSubEffectType ? ResolveSubEffectTypeName(input.EffectKind) : null,
			heading,
			angle,
			requestedX,
			requestedY,
			requestedZ,
			ShouldRequestGeoCollision: true,
			input.ClosestCollision,
			javaSource);
	}

	private static StaggerStumbleCalculatePlan Blocked(
		StaggerStumbleCalculatePlanInput input,
		StaggerStumbleCalculatePlanStatus status,
		string javaSource)
	{
		return new StaggerStumbleCalculatePlan(
			status,
			input,
			ResolveResistanceStatName(input.EffectKind),
			ResolveSpellStatusName(input.EffectKind),
			ShouldSetSubEffectType: false,
			SubEffectTypeName: null,
			HeadingTowardsEffected: 0,
			MovementAngle: 0,
			RequestedCollisionX: input.EffectedPosition.X,
			RequestedCollisionY: input.EffectedPosition.Y,
			RequestedCollisionZ: input.EffectedPosition.Z,
			ShouldRequestGeoCollision: false,
			TargetLocation: null,
			javaSource);
	}

	private static string ResolveResistanceStatName(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Stagger => "STAGGER_RESISTANCE",
			ForcedMoveEffectKind.Stumble => "STUMBLE_RESISTANCE",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Only stagger/stumble calculate planning is supported."),
		};
	}

	private static string ResolveSpellStatusName(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Stagger => "STAGGER",
			ForcedMoveEffectKind.Stumble => "STUMBLE",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Only stagger/stumble calculate planning is supported."),
		};
	}

	private static string ResolveSubEffectTypeName(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Stagger => "STAGGER",
			ForcedMoveEffectKind.Stumble => "STUMBLE",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Only stagger/stumble calculate planning is supported."),
		};
	}

	private static string ResolveJavaSource(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Stagger => "StaggerEffect.calculate",
			ForcedMoveEffectKind.Stumble => "StumbleEffect.calculate",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Only stagger/stumble calculate planning is supported."),
		};
	}
}
