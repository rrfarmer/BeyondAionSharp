using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum ForcedMovePacketPlanStatus
{
	PacketCreated,
	BlockedInvalidSource,
	BlockedInvalidTarget,
}

public sealed record ForcedMovePacketPlan(
	ForcedMovePacketPlanStatus Status,
	ForcedMoveSnapshot? Snapshot,
	SmForcedMove? Packet,
	bool ShouldBroadcastPacket,
	bool ShouldBroadcastAndReceive,
	bool ShouldSendToOwner,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class ForcedMovePacketPlanService
{
	public static ForcedMovePacketPlan CreateBroadcastReceivePlan(ForcedMoveSnapshot snapshot)
	{
		// Java parity: PulledEffect.startEffect, OpenAerialEffect.startEffect,
		// StaggerEffect.startEffect, StumbleEffect.startEffect, AntiHackService.onMoveValidate,
		// and CM_MOVE all use PacketSendUtility.broadcastPacketAndReceive(..., new SM_FORCED_MOVE(...)).
		if (snapshot.SourceObjectId <= 0)
		{
			return new ForcedMovePacketPlan(
				ForcedMovePacketPlanStatus.BlockedInvalidSource,
				snapshot,
				Packet: null,
				ShouldBroadcastPacket: false,
				ShouldBroadcastAndReceive: false,
				ShouldSendToOwner: false,
				"SM_FORCED_MOVE requires a live source Creature with a positive object id"
			);
		}

		if (snapshot.TargetObjectId <= 0)
		{
			return new ForcedMovePacketPlan(
				ForcedMovePacketPlanStatus.BlockedInvalidTarget,
				snapshot,
				Packet: null,
				ShouldBroadcastPacket: false,
				ShouldBroadcastAndReceive: false,
				ShouldSendToOwner: false,
				"SM_FORCED_MOVE requires a live target Creature with a positive object id"
			);
		}

		return new ForcedMovePacketPlan(
			ForcedMovePacketPlanStatus.PacketCreated,
			snapshot,
			new SmForcedMove(snapshot),
			ShouldBroadcastPacket: true,
			ShouldBroadcastAndReceive: true,
			ShouldSendToOwner: false,
			"PacketSendUtility.broadcastPacketAndReceive(effected, new SM_FORCED_MOVE(source, targetObjectId, x, y, z))"
		);
	}
}
