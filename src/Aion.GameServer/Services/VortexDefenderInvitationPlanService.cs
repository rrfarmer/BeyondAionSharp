using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderInvitationPlanService
{
	public const int DefenderQuestionId = SmQuestionWindow.VortexDefenderInvitation;

	public VortexDefenderInvitationPlan CreatePlan(
		VortexZonePlayerSnapshot defender,
		IReadOnlySet<int>? existingDefenderObjectIds = null,
		VortexDefenderAllianceSnapshot? alliance = null,
		bool requestSlotAvailable = true)
	{
		ArgumentNullException.ThrowIfNull(defender);

		var existingDefenders = existingDefenderObjectIds ?? new HashSet<int>();
		if (existingDefenders.Contains(defender.PlayerObjectId))
		{
			return CreateSkippedPlan(
				VortexDefenderInvitationPlanStatus.AlreadyDefender,
				defender,
				alliance,
				existingDefenders);
		}

		if (alliance?.IsFull == true)
		{
			return CreateSkippedPlan(
				VortexDefenderInvitationPlanStatus.AllianceFull,
				defender,
				alliance,
				existingDefenders);
		}

		var status = requestSlotAvailable
			? VortexDefenderInvitationPlanStatus.InvitationPlanned
			: VortexDefenderInvitationPlanStatus.RequestNotStored;

		return new VortexDefenderInvitationPlan(
			status,
			defender,
			existingDefenders.ToArray(),
			alliance ?? VortexDefenderAllianceSnapshot.Missing,
			DefenderQuestionId,
			QuestionWindowArg1: 0,
			QuestionWindowArg2: 0,
			RequestSlotAvailable: requestSlotAvailable,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}

	private static VortexDefenderInvitationPlan CreateSkippedPlan(
		VortexDefenderInvitationPlanStatus status,
		VortexZonePlayerSnapshot defender,
		VortexDefenderAllianceSnapshot? alliance,
		IReadOnlySet<int> existingDefenderObjectIds)
	{
		return new VortexDefenderInvitationPlan(
			status,
			defender,
			existingDefenderObjectIds.ToArray(),
			alliance ?? VortexDefenderAllianceSnapshot.Missing,
			RequestId: null,
			QuestionWindowArg1: null,
			QuestionWindowArg2: null,
			RequestSlotAvailable: false,
			JavaSource: "services/vortex/Invasion.updateDefenders");
	}
}

public enum VortexDefenderInvitationPlanStatus
{
	AlreadyDefender,
	AllianceFull,
	RequestNotStored,
	InvitationPlanned,
}

public sealed record VortexDefenderInvitationPlan(
	VortexDefenderInvitationPlanStatus Status,
	VortexZonePlayerSnapshot Defender,
	IReadOnlyList<int> ExistingDefenderObjectIds,
	VortexDefenderAllianceSnapshot Alliance,
	int? RequestId,
	int? QuestionWindowArg1,
	int? QuestionWindowArg2,
	bool RequestSlotAvailable,
	string JavaSource)
{
	public bool WouldInstallRequest => Status is VortexDefenderInvitationPlanStatus.RequestNotStored
		or VortexDefenderInvitationPlanStatus.InvitationPlanned;
	public bool HasQuestionWindowIntent => Status == VortexDefenderInvitationPlanStatus.InvitationPlanned;
	public bool ShouldMutateLiveRequest => false;
	public bool ShouldSendLivePacket => false;
}

public sealed record VortexDefenderAllianceSnapshot(
	bool Exists,
	bool IsFull,
	bool IsDisbanded = false)
{
	public static VortexDefenderAllianceSnapshot Missing { get; } = new(Exists: false, IsFull: false);
	public static VortexDefenderAllianceSnapshot Open { get; } = new(Exists: true, IsFull: false);
	public static VortexDefenderAllianceSnapshot Full { get; } = new(Exists: true, IsFull: true);
	public static VortexDefenderAllianceSnapshot Disbanded { get; } = new(Exists: true, IsFull: false, IsDisbanded: true);
}

public sealed class VortexDefenderInvitationRequestSlotSnapshotService
{
	public VortexDefenderInvitationRequestSlotSnapshot CreateSnapshot(Player defender)
	{
		ArgumentNullException.ThrowIfNull(defender);

		// Java parity: services/vortex/Invasion.updateDefenders uses
		// Player.getResponseRequester().putRequest(904306, responseHandler).
		return new VortexDefenderInvitationRequestSlotSnapshot(
			defender.ObjectId,
			VortexDefenderInvitationPlanService.DefenderQuestionId,
			defender.ResponseRequester.IsRequestSlotAvailable(VortexDefenderInvitationPlanService.DefenderQuestionId),
			defender.ResponseRequester.Count,
			JavaSource: "model/gameobjects/player/ResponseRequester.putRequest");
	}

	public IReadOnlyDictionary<int, bool> CollectRequestSlotsByPlayerObjectId(IEnumerable<Player>? defenders)
	{
		return (defenders ?? [])
			.Select(CreateSnapshot)
			.ToDictionary(snapshot => snapshot.PlayerObjectId, snapshot => snapshot.RequestSlotAvailable);
	}
}

public sealed record VortexDefenderInvitationRequestSlotSnapshot(
	int PlayerObjectId,
	int QuestionId,
	bool RequestSlotAvailable,
	int ActiveRequestCount,
	string JavaSource);
