using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ForcedMoveEffectKind
{
	Pulled,
	OpenAerial,
	Stagger,
	Stumble,
}

public enum ForcedMoveStartEffectPlanStatus
{
	PlannedPlayerPacket,
	PlannedNpcNoPacket,
	BlockedInvalidEffected,
	BlockedInvalidPacketSource,
}

public sealed record ForcedMoveStartEffectPlanInput(
	ForcedMoveEffectKind EffectKind,
	ObjectPositionSnapshot EffectedCurrentPosition,
	float TargetX,
	float TargetY,
	float TargetZ,
	bool IsEffectedPlayer,
	bool IsReflected,
	int EffectorObjectId,
	int OriginalEffectedObjectId
);

public sealed record ForcedMoveStartEffectPlan(
	ForcedMoveStartEffectPlanStatus Status,
	ForcedMoveStartEffectPlanInput Input,
	int? CancelCurrentSkillSourceObjectId,
	bool ShouldRemoveParalyzeEffects,
	bool ShouldRemoveStunEffects,
	bool ShouldCallPlayerOnStopGliding,
	bool ShouldCallPlayerOnStopMove,
	bool ShouldUpdateWorldPosition,
	ObjectPositionSnapshot? UpdatedPosition,
	ForcedMovePacketPlan? ForcedMovePacketPlan,
	string AbnormalStateName,
	bool ShouldSetEffectedControllerAbnormal,
	bool ShouldSetEffectAbnormal,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ForcedMoveStartEffectPlanService
{
	public static ForcedMoveStartEffectPlan CreatePlan(ForcedMoveStartEffectPlanInput input)
	{
		// Java parity breadcrumb:
		// PulledEffect, OpenAerialEffect, StaggerEffect, and StumbleEffect startEffect methods
		// update world position, player-stop movement state, optionally emit SM_FORCED_MOVE for
		// players, then set the matching abnormal state. OpenAerial/Stagger/Stumble remove
		// paralyze effects, Stumble also removes stun effects, and reflected Pulled skips cancel/
		// stop logic while using originalEffected as the forced-move packet source.
		if (input.EffectedCurrentPosition.ObjectId <= 0)
		{
			return new ForcedMoveStartEffectPlan(
				ForcedMoveStartEffectPlanStatus.BlockedInvalidEffected,
				input,
				CancelCurrentSkillSourceObjectId: null,
				ShouldRemoveParalyzeEffects: false,
				ShouldRemoveStunEffects: false,
				ShouldCallPlayerOnStopGliding: false,
				ShouldCallPlayerOnStopMove: false,
				ShouldUpdateWorldPosition: false,
				UpdatedPosition: null,
				ForcedMovePacketPlan: null,
				AbnormalStateName: ResolveAbnormalStateName(input.EffectKind),
				ShouldSetEffectedControllerAbnormal: false,
				ShouldSetEffectAbnormal: false,
				"Forced-move startEffect requires a live effected Creature with a positive object id"
			);
		}

		var updatedPosition = new ObjectPositionSnapshot(
			input.EffectedCurrentPosition.ObjectId,
			input.TargetX,
			input.TargetY,
			input.TargetZ,
			input.EffectedCurrentPosition.Heading
		);

		var shouldSkipPullMotionStops = input.EffectKind == ForcedMoveEffectKind.Pulled && input.IsReflected;
		int? cancelCurrentSkillSourceObjectId = shouldSkipPullMotionStops ? null : input.EffectorObjectId;
		var shouldRemoveParalyzeEffects = input.EffectKind is ForcedMoveEffectKind.OpenAerial or ForcedMoveEffectKind.Stagger or ForcedMoveEffectKind.Stumble;
		var shouldRemoveStunEffects = input.EffectKind == ForcedMoveEffectKind.Stumble;
		var shouldCallPlayerOnStopGliding = input.IsEffectedPlayer && !shouldSkipPullMotionStops;
		var shouldCallPlayerOnStopMove = input.IsEffectedPlayer && !shouldSkipPullMotionStops;

		ForcedMovePacketPlan? forcedMovePacketPlan = null;
		var status = ForcedMoveStartEffectPlanStatus.PlannedNpcNoPacket;
		if (input.IsEffectedPlayer)
		{
			var sourceObjectId =
				input.EffectKind == ForcedMoveEffectKind.Pulled && input.IsReflected ? input.OriginalEffectedObjectId : input.EffectorObjectId;

			forcedMovePacketPlan = ForcedMovePacketPlanService.CreateBroadcastReceivePlan(
				new ForcedMoveSnapshot(
					SourceObjectId: sourceObjectId,
					TargetObjectId: input.EffectedCurrentPosition.ObjectId,
					X: input.TargetX,
					Y: input.TargetY,
					Z: input.TargetZ
				)
			);

			status =
				forcedMovePacketPlan.Status == ForcedMovePacketPlanStatus.PacketCreated
					? ForcedMoveStartEffectPlanStatus.PlannedPlayerPacket
					: ForcedMoveStartEffectPlanStatus.BlockedInvalidPacketSource;
		}

		var shouldApplyAbnormal = status is ForcedMoveStartEffectPlanStatus.PlannedPlayerPacket or ForcedMoveStartEffectPlanStatus.PlannedNpcNoPacket;
		return new ForcedMoveStartEffectPlan(
			status,
			input,
			cancelCurrentSkillSourceObjectId,
			shouldRemoveParalyzeEffects,
			shouldRemoveStunEffects,
			shouldCallPlayerOnStopGliding,
			shouldCallPlayerOnStopMove,
			ShouldUpdateWorldPosition: shouldApplyAbnormal,
			UpdatedPosition: shouldApplyAbnormal ? updatedPosition : null,
			forcedMovePacketPlan,
			ResolveAbnormalStateName(input.EffectKind),
			ShouldSetEffectedControllerAbnormal: shouldApplyAbnormal,
			ShouldSetEffectAbnormal: shouldApplyAbnormal,
			ResolveJavaSource(input.EffectKind)
		);
	}

	private static string ResolveAbnormalStateName(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Pulled => "PULLED",
			ForcedMoveEffectKind.OpenAerial => "OPENAERIAL",
			ForcedMoveEffectKind.Stagger => "STAGGER",
			ForcedMoveEffectKind.Stumble => "STUMBLE",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Unsupported forced-move effect kind."),
		};
	}

	private static string ResolveJavaSource(ForcedMoveEffectKind effectKind)
	{
		return effectKind switch
		{
			ForcedMoveEffectKind.Pulled => "PulledEffect.startEffect",
			ForcedMoveEffectKind.OpenAerial => "OpenAerialEffect.startEffect",
			ForcedMoveEffectKind.Stagger => "StaggerEffect.startEffect",
			ForcedMoveEffectKind.Stumble => "StumbleEffect.startEffect",
			_ => throw new ArgumentOutOfRangeException(nameof(effectKind), effectKind, "Unsupported forced-move effect kind."),
		};
	}
}
