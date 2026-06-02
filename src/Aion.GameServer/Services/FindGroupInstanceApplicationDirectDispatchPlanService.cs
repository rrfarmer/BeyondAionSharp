using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class FindGroupInstanceApplicationDirectDispatchPlanService
{
	public static FindGroupInstanceApplicationDirectDispatchPlan CreateDisabledPlan(
		FindGroupInstanceApplicationPlan? applicationPlan,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: FindGroupService.sendInstanceApplication and the declined
		// sendInstanceApplicationResult branch both resolve a player through World.getPlayer(...)
		// before sending a direct packet. This disabled executor verifies the
		// connection-adjacent recipient boundary without sending packets.
		if (applicationPlan is null)
			return FindGroupInstanceApplicationDirectDispatchPlan.SkippedMissingPlan();

		if (applicationPlan.DirectPacketIntents.Count == 0)
			return FindGroupInstanceApplicationDirectDispatchPlan.SkippedMissingDirectPacketIntent(applicationPlan);

		var missingRecipients = new List<int>();
		foreach (var intent in applicationPlan.DirectPacketIntents)
		{
			if (resolvePlayer(intent.RecipientObjectId) is null)
				missingRecipients.Add(intent.RecipientObjectId);
		}

		if (missingRecipients.Count > 0)
			return FindGroupInstanceApplicationDirectDispatchPlan.SkippedMissingRecipient(applicationPlan, missingRecipients);

		var audit = FindGroupSideEffectDispatchAuditService.CreateAuditPlan(applicationPlan.DirectPacketIntents);
		return FindGroupInstanceApplicationDirectDispatchPlan.DirectPacketPlanned(applicationPlan, audit.DirectPackets);
	}
}

public enum FindGroupInstanceApplicationDirectDispatchStatus
{
	SkippedMissingPlan,
	SkippedMissingDirectPacketIntent,
	SkippedMissingRecipient,
	DirectPacketPlanned,
}

public sealed record FindGroupInstanceApplicationDirectDispatchPlan(
	FindGroupInstanceApplicationDirectDispatchStatus Status,
	FindGroupInstanceApplicationPlan? ApplicationPlan,
	IReadOnlyList<FindGroupDirectPacketDispatchAudit> DirectPackets,
	IReadOnlyList<int> MissingRecipientObjectIds,
	bool DispatchLiveSideEffects)
{
	public static FindGroupInstanceApplicationDirectDispatchPlan SkippedMissingPlan()
	{
		return new FindGroupInstanceApplicationDirectDispatchPlan(
			FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingPlan,
			ApplicationPlan: null,
			DirectPackets: [],
			MissingRecipientObjectIds: [],
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationDirectDispatchPlan SkippedMissingDirectPacketIntent(
		FindGroupInstanceApplicationPlan applicationPlan)
	{
		return new FindGroupInstanceApplicationDirectDispatchPlan(
			FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingDirectPacketIntent,
			applicationPlan,
			DirectPackets: [],
			MissingRecipientObjectIds: [],
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationDirectDispatchPlan SkippedMissingRecipient(
		FindGroupInstanceApplicationPlan applicationPlan,
		IReadOnlyList<int> missingRecipientObjectIds)
	{
		return new FindGroupInstanceApplicationDirectDispatchPlan(
			FindGroupInstanceApplicationDirectDispatchStatus.SkippedMissingRecipient,
			applicationPlan,
			DirectPackets: [],
			missingRecipientObjectIds,
			DispatchLiveSideEffects: false);
	}

	public static FindGroupInstanceApplicationDirectDispatchPlan DirectPacketPlanned(
		FindGroupInstanceApplicationPlan applicationPlan,
		IReadOnlyList<FindGroupDirectPacketDispatchAudit> directPackets)
	{
		return new FindGroupInstanceApplicationDirectDispatchPlan(
			FindGroupInstanceApplicationDirectDispatchStatus.DirectPacketPlanned,
			applicationPlan,
			directPackets,
			MissingRecipientObjectIds: [],
			DispatchLiveSideEffects: false);
	}
}
