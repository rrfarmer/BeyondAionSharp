using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class NpcTargetChangePacketPlanService
{
	public const int TalkInfoRetargetDelayMilliseconds = 750;

	public static NpcTargetChangePacketPlan CreatePlan(NpcTargetChangeCoordinatePacketPlanInput input)
	{
		var headingTowardTarget = input.NewTargetObjectId != 0 && input.NewTargetObjectId != input.NpcObjectId
			? PositionUtilService.GetHeadingTowards(input.NpcX, input.NpcY, input.TargetX, input.TargetY)
			: input.CurrentHeading;

		return CreatePlan(new NpcTargetChangePacketPlanInput(
			input.NpcObjectId,
			input.NewTargetObjectId,
			input.CurrentHeading,
			headingTowardTarget,
			input.IsDead,
			input.HasTalkInfo));
	}

	public static NpcTargetChangePacketPlan CreatePlan(NpcTargetChangePacketPlanInput input)
	{
		// Java parity breadcrumb: controllers/NpcController.onTargetChanged clears
		// attacked count, renews last target-change time, then either schedules AI think
		// for talk NPC target clear or broadcasts SM_LOOKATOBJECT for visible updates.
		if (input.NpcObjectId <= 0)
			return NpcTargetChangePacketPlan.Blocked(input, NpcTargetChangePacketPlanStatus.BlockedInvalidNpc);

		if (input.IsDead)
			return NpcTargetChangePacketPlan.NoPacket(input, NpcTargetChangePacketPlanStatus.NoPacketNpcDead);

		if (input.NewTargetObjectId == 0 && input.HasTalkInfo)
			return NpcTargetChangePacketPlan.ScheduleThink(input);

		var heading = input.NewTargetObjectId != 0 && input.NewTargetObjectId != input.NpcObjectId
			? input.HeadingTowardTarget
			: input.CurrentHeading;
		return NpcTargetChangePacketPlan.PacketCreated(input, heading);
	}
}

public sealed record NpcTargetChangePacketPlanInput(
	int NpcObjectId,
	int NewTargetObjectId,
	int CurrentHeading,
	int HeadingTowardTarget,
	bool IsDead,
	bool HasTalkInfo);

public sealed record NpcTargetChangeCoordinatePacketPlanInput(
	int NpcObjectId,
	int NewTargetObjectId,
	float NpcX,
	float NpcY,
	float TargetX,
	float TargetY,
	int CurrentHeading,
	bool IsDead,
	bool HasTalkInfo);

public sealed record NpcTargetChangePacketPlan(
	NpcTargetChangePacketPlanStatus Status,
	int NpcObjectId,
	int NewTargetObjectId,
	int SelectedHeading,
	bool ShouldClearAttackedCount,
	bool ShouldRenewLastTargetChangeTime,
	bool ShouldScheduleThink,
	int ScheduledThinkDelayMilliseconds,
	SmLookAtObject? Packet,
	string JavaSource)
{
	public bool ShouldBroadcastPacket => Packet is not null;

	public static NpcTargetChangePacketPlan PacketCreated(
		NpcTargetChangePacketPlanInput input,
		int selectedHeading)
	{
		return new NpcTargetChangePacketPlan(
			NpcTargetChangePacketPlanStatus.PacketCreated,
			input.NpcObjectId,
			input.NewTargetObjectId,
			selectedHeading,
			ShouldClearAttackedCount: true,
			ShouldRenewLastTargetChangeTime: true,
			ShouldScheduleThink: false,
			ScheduledThinkDelayMilliseconds: 0,
			new SmLookAtObject(new LookAtObjectSnapshot(input.NpcObjectId, input.NewTargetObjectId, selectedHeading)),
			"NpcController.onTargetChanged -> PacketSendUtility.broadcastPacket(new SM_LOOKATOBJECT(owner))");
	}

	public static NpcTargetChangePacketPlan ScheduleThink(NpcTargetChangePacketPlanInput input)
	{
		return new NpcTargetChangePacketPlan(
			NpcTargetChangePacketPlanStatus.ScheduledThinkForTalkNpcTargetClear,
			input.NpcObjectId,
			input.NewTargetObjectId,
			input.CurrentHeading,
			ShouldClearAttackedCount: true,
			ShouldRenewLastTargetChangeTime: true,
			ShouldScheduleThink: true,
			NpcTargetChangePacketPlanService.TalkInfoRetargetDelayMilliseconds,
			Packet: null,
			"NpcController.onTargetChanged schedules AI think after target clear for talk NPC");
	}

	public static NpcTargetChangePacketPlan NoPacket(
		NpcTargetChangePacketPlanInput input,
		NpcTargetChangePacketPlanStatus status)
	{
		return new NpcTargetChangePacketPlan(
			status,
			input.NpcObjectId,
			input.NewTargetObjectId,
			input.CurrentHeading,
			ShouldClearAttackedCount: true,
			ShouldRenewLastTargetChangeTime: true,
			ShouldScheduleThink: false,
			ScheduledThinkDelayMilliseconds: 0,
			Packet: null,
			"NpcController.onTargetChanged stopped before packet broadcast");
	}

	public static NpcTargetChangePacketPlan Blocked(
		NpcTargetChangePacketPlanInput input,
		NpcTargetChangePacketPlanStatus status)
	{
		return new NpcTargetChangePacketPlan(
			status,
			input.NpcObjectId,
			input.NewTargetObjectId,
			input.CurrentHeading,
			ShouldClearAttackedCount: false,
			ShouldRenewLastTargetChangeTime: false,
			ShouldScheduleThink: false,
			ScheduledThinkDelayMilliseconds: 0,
			Packet: null,
			"NpcController.onTargetChanged requires a live NPC owner");
	}
}

public enum NpcTargetChangePacketPlanStatus
{
	PacketCreated,
	ScheduledThinkForTalkNpcTargetClear,
	NoPacketNpcDead,
	BlockedInvalidNpc,
}
