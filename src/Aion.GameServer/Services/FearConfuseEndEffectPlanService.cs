using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum FearConfuseEffectKind
{
	Confuse,
	Fear,
}

public enum FearConfuseEndEffectPlanStatus
{
	Planned,
	BlockedInvalidObject,
}

public sealed record FearConfuseEndEffectPlanInput(
	FearConfuseEffectKind EffectKind,
	ObjectPositionSnapshot EffectedPosition,
	bool IsEffectedNpc);

public sealed record FearConfuseEndEffectPlan(
	FearConfuseEndEffectPlanStatus Status,
	FearConfuseEffectKind EffectKind,
	ObjectPositionSnapshot EffectedPosition,
	string AbnormalStateName,
	bool ShouldUnsetAbnormal,
	bool ShouldAbortMove,
	MovementCorrectionPacketPlan MovementCorrectionPlan,
	bool ShouldSetNpcIdle,
	bool ShouldNotifyNpcAttackEvent,
	string JavaSource)
{
	public bool ShouldBroadcastPosition => MovementCorrectionPlan.ShouldBroadcastPacket;
	public bool ShouldBroadcastAndReceivePosition => MovementCorrectionPlan.ShouldBroadcastAndReceive;
	public SmPosition? PositionPacket => MovementCorrectionPlan.ObjectPacket;
}

public static class FearConfuseEndEffectPlanService
{
	public static FearConfuseEndEffectPlan CreatePlan(FearConfuseEndEffectPlanInput input)
	{
		// Java parity breadcrumb:
		// ConfuseEffect.endEffect and FearEffect.endEffect both unset the matching abnormal state,
		// abort movement, broadcastPacketAndReceive(new SM_POSITION(effected)), then for NPCs set
		// AI state IDLE and raise AIEventType.ATTACK with the effected creature.
		var packetPlan = MovementCorrectionPacketPlanService.CreateBroadcastObjectPlan(input.EffectedPosition);
		var isBlocked = packetPlan.Status == MovementCorrectionPacketPlanStatus.BlockedInvalidObject;
		var abnormalStateName = input.EffectKind switch
		{
			FearConfuseEffectKind.Confuse => "CONFUSE",
			FearConfuseEffectKind.Fear => "FEAR",
			_ => throw new ArgumentOutOfRangeException(nameof(input), input.EffectKind, "Unsupported fear/confuse effect kind."),
		};

		return new FearConfuseEndEffectPlan(
			isBlocked ? FearConfuseEndEffectPlanStatus.BlockedInvalidObject : FearConfuseEndEffectPlanStatus.Planned,
			input.EffectKind,
			input.EffectedPosition,
			abnormalStateName,
			ShouldUnsetAbnormal: !isBlocked,
			ShouldAbortMove: !isBlocked,
			packetPlan,
			ShouldSetNpcIdle: !isBlocked && input.IsEffectedNpc,
			ShouldNotifyNpcAttackEvent: !isBlocked && input.IsEffectedNpc,
			input.EffectKind == FearConfuseEffectKind.Confuse
				? "ConfuseEffect.endEffect"
				: "FearEffect.endEffect");
	}
}
