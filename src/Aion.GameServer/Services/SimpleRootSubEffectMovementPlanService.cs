using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SimpleRootSubEffectMovementPlanStatus
{
	PlannedNoSubEffect,
	PlannedSubEffectPlayerNoBroadcast,
	PlannedSubEffectObjectBroadcast,
	BlockedInvalidObject,
}

public sealed record SimpleRootSubEffectMovementPlanInput(
	ObjectPositionSnapshot EffectedCurrentPosition,
	float TargetX,
	float TargetY,
	float TargetZ,
	bool IsEffectedPlayer,
	bool IsSubEffect);

public sealed record SimpleRootSubEffectMovementPlan(
	SimpleRootSubEffectMovementPlanStatus Status,
	SimpleRootSubEffectMovementPlanInput Input,
	bool ShouldSetSpellStatusNone,
	bool ShouldCallPlayerOnStopMove,
	bool ShouldUpdateWorldPosition,
	ObjectPositionSnapshot? UpdatedPosition,
	MovementCorrectionPacketPlan? MovementCorrectionPlan,
	bool ShouldSetEffectedControllerSimpleMoveBack,
	bool ShouldSetEffectSimpleMoveBack,
	string JavaSource)
{
	public bool IsLive => false;
	public bool ShouldBroadcastPosition => MovementCorrectionPlan?.ShouldBroadcastPacket == true;
	public bool ShouldBroadcastAndReceivePosition => MovementCorrectionPlan?.ShouldBroadcastAndReceive == true;
	public SmPosition? PositionPacket => MovementCorrectionPlan?.ObjectPacket;
}

public static class SimpleRootSubEffectMovementPlanService
{
	public static SimpleRootSubEffectMovementPlan CreatePlan(SimpleRootSubEffectMovementPlanInput input)
	{
		// Java parity breadcrumb:
		// SimpleRootEffect.startEffect -> setSpellStatus(NONE) ->
		// if (effected instanceof Player) onStopMove() ->
		// if (effect.isSubEffect()) World.updatePosition(...);
		// if (!(effected instanceof Player)) PacketSendUtility.broadcastPacket(effected, new SM_POSITION(effected));
		// then setAbnormal(SIMPLE_MOVE_BACK) on effected controller and effect.
		if (input.EffectedCurrentPosition.ObjectId <= 0)
		{
			return new SimpleRootSubEffectMovementPlan(
				SimpleRootSubEffectMovementPlanStatus.BlockedInvalidObject,
				input,
				ShouldSetSpellStatusNone: false,
				ShouldCallPlayerOnStopMove: false,
				ShouldUpdateWorldPosition: false,
				UpdatedPosition: null,
				MovementCorrectionPlan: null,
				ShouldSetEffectedControllerSimpleMoveBack: false,
				ShouldSetEffectSimpleMoveBack: false,
				"SimpleRootEffect.startEffect requires a live effected Creature with a positive object id");
		}

		var updatedPosition = new ObjectPositionSnapshot(
			input.EffectedCurrentPosition.ObjectId,
			input.TargetX,
			input.TargetY,
			input.TargetZ,
			input.EffectedCurrentPosition.Heading);

		MovementCorrectionPacketPlan? movementPlan = null;
		var status = SimpleRootSubEffectMovementPlanStatus.PlannedNoSubEffect;
		if (input.IsSubEffect)
		{
			if (input.IsEffectedPlayer)
			{
				status = SimpleRootSubEffectMovementPlanStatus.PlannedSubEffectPlayerNoBroadcast;
			}
			else
			{
				status = SimpleRootSubEffectMovementPlanStatus.PlannedSubEffectObjectBroadcast;
				movementPlan = MovementCorrectionPacketPlanService.CreateBroadcastObjectPlan(
					updatedPosition,
					receiveAfterBroadcast: false);
			}
		}

		return new SimpleRootSubEffectMovementPlan(
			status,
			input,
			ShouldSetSpellStatusNone: true,
			ShouldCallPlayerOnStopMove: input.IsEffectedPlayer,
			ShouldUpdateWorldPosition: input.IsSubEffect,
			UpdatedPosition: input.IsSubEffect ? updatedPosition : null,
			movementPlan,
			ShouldSetEffectedControllerSimpleMoveBack: true,
			ShouldSetEffectSimpleMoveBack: true,
			"SimpleRootEffect.startEffect");
	}
}
