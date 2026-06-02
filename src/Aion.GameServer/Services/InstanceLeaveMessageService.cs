using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class InstanceLeaveMessageService
{
	public static InstanceLeaveMessagePlan CreateLeaveMessagePlan(
		WorldMapInstanceRuntimeState instance,
		int soloDestroyDelaySeconds,
		int normalDestroyDelaySeconds,
		bool registeredTeamHasNoMembers = false)
	{
		ArgumentNullException.ThrowIfNull(instance);

		// Java parity: InstanceService.onLeaveInstance only sends reset-warning packets for registered instances.
		if (instance.RegisteredCount == 0)
		{
			return new InstanceLeaveMessagePlan(
				InstanceLeaveMessageStatus.NoRegisteredObjects,
				null,
				null,
				"InstanceService.onLeaveInstance -> if (instance.getRegisteredCount() > 0) is false");
		}

		var delayPlan = InstanceServiceFormulaService.CreateDestroyDelayPlan(
			instance.MaxPlayers,
			soloDestroyDelaySeconds,
			normalDestroyDelaySeconds);
		var minutes = delayPlan.DestroyDelaySeconds / 60;

		if (instance.MaxPlayers == 1)
		{
			return new InstanceLeaveMessagePlan(
				InstanceLeaveMessageStatus.SoloInstance,
				minutes,
				SmSystemMessage.LeaveInstance(minutes),
				"InstanceService.onLeaveInstance -> maxPlayers == 1 -> STR_MSG_LEAVE_INSTANCE(getDestroyDelaySeconds(instance) / 60)");
		}

		if (instance.RegisteredTeamId.HasValue && registeredTeamHasNoMembers)
		{
			return new InstanceLeaveMessagePlan(
				InstanceLeaveMessageStatus.RegisteredTeamEmpty,
				0,
				SmSystemMessage.LeaveInstanceParty(0),
				"InstanceService.onLeaveInstance -> registeredTeam != null && registeredTeam.getMembers().isEmpty() -> STR_MSG_LEAVE_INSTANCE_PARTY(0)");
		}

		if (instance.PlayerCount <= 1)
		{
			return new InstanceLeaveMessagePlan(
				InstanceLeaveMessageStatus.LastOrOnlyPlayerInside,
				minutes,
				SmSystemMessage.LeaveInstanceParty(minutes),
				"InstanceService.onLeaveInstance -> playersInside.size() <= 1 -> STR_MSG_LEAVE_INSTANCE_PARTY(getDestroyDelaySeconds(instance) / 60)");
		}

		return new InstanceLeaveMessagePlan(
			InstanceLeaveMessageStatus.PlayersRemainInside,
			null,
			null,
			"InstanceService.onLeaveInstance -> playersInside.size() > 1 -> no reset-warning packet");
	}
}

public sealed record InstanceLeaveMessagePlan(
	InstanceLeaveMessageStatus Status,
	int? Minutes,
	SmSystemMessage? Packet,
	string JavaSource);

public enum InstanceLeaveMessageStatus
{
	NoRegisteredObjects,
	SoloInstance,
	RegisteredTeamEmpty,
	LastOrOnlyPlayerInside,
	PlayersRemainInside,
}
