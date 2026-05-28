using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum MovementCorrectionPacketPlanStatus
{
	ObjectPositionPacketCreated,
	SelfPositionPacketCreated,
	BlockedInvalidObject,
}

public sealed record MovementCorrectionPacketPlan(
	MovementCorrectionPacketPlanStatus Status,
	ObjectPositionSnapshot? ObjectPosition,
	PositionSelfSnapshot? SelfPosition,
	GameServerPacket? Packet,
	bool ShouldBroadcastPacket,
	bool ShouldBroadcastAndReceive,
	bool ShouldSendToOwner,
	bool ExpectsClientPositionSelfResponse,
	string JavaSource)
{
	public bool IsLive => false;
	public SmPosition? ObjectPacket => Packet as SmPosition;
	public SmPositionSelf? SelfPacket => Packet as SmPositionSelf;
}

public static class MovementCorrectionPacketPlanService
{
	public static MovementCorrectionPacketPlan CreateBroadcastObjectPlan(ObjectPositionSnapshot position, bool receiveAfterBroadcast = true)
	{
		// Java parity breadcrumb: ConfuseEffect.endEffect, FearEffect.endEffect, and
		// EternalBastionMountableAI.tryMountNpc use broadcastPacketAndReceive(new SM_POSITION(object)).
		// SimpleRootEffect.startEffect uses broadcastPacket(new SM_POSITION(effected)) for non-player sub effects.
		if (position.ObjectId <= 0)
			return Blocked(position, "SM_POSITION requires a live VisibleObject with a positive object id");

		return new MovementCorrectionPacketPlan(
			MovementCorrectionPacketPlanStatus.ObjectPositionPacketCreated,
			position,
			SelfPosition: null,
			new SmPosition(position),
			ShouldBroadcastPacket: true,
			ShouldBroadcastAndReceive: receiveAfterBroadcast,
			ShouldSendToOwner: false,
			ExpectsClientPositionSelfResponse: false,
			receiveAfterBroadcast
				? "PacketSendUtility.broadcastPacketAndReceive(object, new SM_POSITION(object))"
				: "PacketSendUtility.broadcastPacket(object, new SM_POSITION(object))");
	}

	public static MovementCorrectionPacketPlan CreateSelfPlan(PositionSelfSnapshot position)
	{
		// Java parity breadcrumb: SM_POSITION_SELF.writeImpl writes x/y/z/heading and the
		// client answers with CM_POSITION_SELF. No live C# caller dispatches this path yet.
		return new MovementCorrectionPacketPlan(
			MovementCorrectionPacketPlanStatus.SelfPositionPacketCreated,
			ObjectPosition: null,
			position,
			new SmPositionSelf(position),
			ShouldBroadcastPacket: false,
			ShouldBroadcastAndReceive: false,
			ShouldSendToOwner: true,
			ExpectsClientPositionSelfResponse: true,
			"SM_POSITION_SELF(float x, float y, float z, byte heading) -> CM_POSITION_SELF response");
	}

	private static MovementCorrectionPacketPlan Blocked(ObjectPositionSnapshot position, string javaSource)
	{
		return new MovementCorrectionPacketPlan(
			MovementCorrectionPacketPlanStatus.BlockedInvalidObject,
			position,
			SelfPosition: null,
			Packet: null,
			ShouldBroadcastPacket: false,
			ShouldBroadcastAndReceive: false,
			ShouldSendToOwner: false,
			ExpectsClientPositionSelfResponse: false,
			javaSource);
	}
}
