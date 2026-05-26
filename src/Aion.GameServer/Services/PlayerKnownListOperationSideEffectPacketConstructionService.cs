using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListOperationSideEffectPacketConstructionStatus
{
	Constructed,
	PartiallyConstructed,
	NoAttachedSideEffects,
}

public enum PlayerKnownListOperationSideEffectPacketConstructionResultStatus
{
	Constructed,
	PartiallyConstructed,
	BlockedMissingSubjectFacts,
}

public sealed record PlayerKnownListOperationSideEffectPacketConstructionFacts(
	Player SubjectPlayer,
	IReadOnlyList<PlayerMotion> ActiveMotions,
	SmPlayerInfoViewerContext? ViewerContext = null,
	IReadOnlyList<SmAbnormalEffectEntry>? AbnormalEffects = null,
	int AbnormalEffectMask = 0,
	int AbnormalEffectSlots = SmAbnormalEffect.FullSkillTargetSlots,
	float RideMovementSpeed = 0,
	int RideBaseAttackSpeed = 0,
	int RideCurrentAttackSpeed = 0);

public sealed record PlayerKnownListOperationSideEffectPacketConstructionRequest(
	PlayerKnownListOperationSideEffectAttachmentPlan AttachmentPlan,
	IReadOnlyDictionary<int, PlayerKnownListOperationSideEffectPacketConstructionFacts> SubjectFactsByPlayerObjectId);

public sealed record PlayerKnownListOperationSideEffectPacketConstructionResult(
	PlayerKnownListOperationAttachedSideEffect AttachedSideEffect,
	PlayerKnownListOperationSideEffectPacketConstructionResultStatus Status,
	PlayerKnownListPlayerSideEffectPacketConstructionPlan? PacketConstructionPlan,
	string Notes = "");

public sealed record PlayerKnownListOperationSideEffectPacketConstructionPlan(
	PlayerKnownListOperationSideEffectAttachmentPlan AttachmentPlan,
	IReadOnlyList<PlayerKnownListOperationSideEffectPacketConstructionResult> Results,
	PlayerKnownListOperationSideEffectPacketConstructionStatus Status,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource);

public sealed class PlayerKnownListOperationSideEffectPacketConstructionService
{
	private readonly PlayerKnownListPlayerSideEffectPacketConstructionService _packetConstruction;

	public PlayerKnownListOperationSideEffectPacketConstructionService(
		PlayerKnownListPlayerSideEffectPacketConstructionService? packetConstruction = null)
	{
		_packetConstruction = packetConstruction ?? new PlayerKnownListPlayerSideEffectPacketConstructionService();
	}

	public PlayerKnownListOperationSideEffectPacketConstructionPlan Construct(
		PlayerKnownListOperationSideEffectPacketConstructionRequest request)
	{
		// Java parity breadcrumb: KnownList.updateVisibility and KnownList.del
		// produce directional PlayerController see/notSee callbacks in operation-step order.
		// This bridge applies packet construction metadata per attached side effect only.
		var results = request.AttachmentPlan.AttachedSideEffects
			.Select(attachment => ConstructAttachment(request, attachment))
			.ToArray();
		var status = results.Length == 0
			? PlayerKnownListOperationSideEffectPacketConstructionStatus.NoAttachedSideEffects
			: results.All(result => result.Status == PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed)
				? PlayerKnownListOperationSideEffectPacketConstructionStatus.Constructed
				: PlayerKnownListOperationSideEffectPacketConstructionStatus.PartiallyConstructed;

		return new PlayerKnownListOperationSideEffectPacketConstructionPlan(
			request.AttachmentPlan,
			results,
			status,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Non-live packet construction metadata for directional KnownList player see/notSee side-effect attachments.");
	}

	private PlayerKnownListOperationSideEffectPacketConstructionResult ConstructAttachment(
		PlayerKnownListOperationSideEffectPacketConstructionRequest request,
		PlayerKnownListOperationAttachedSideEffect attachment)
	{
		if (!request.SubjectFactsByPlayerObjectId.TryGetValue(
			attachment.SideEffectPlan.SubjectPlayerObjectId,
			out var facts))
		{
			return new PlayerKnownListOperationSideEffectPacketConstructionResult(
				attachment,
				PlayerKnownListOperationSideEffectPacketConstructionResultStatus.BlockedMissingSubjectFacts,
				PacketConstructionPlan: null,
				"No supplied subject player/motion/effect facts were available for this directional side-effect plan.");
		}

		var packetPlan = _packetConstruction.Construct(new PlayerKnownListPlayerSideEffectPacketConstructionRequest(
			attachment.SideEffectPlan,
			facts.SubjectPlayer,
			facts.ActiveMotions,
			facts.ViewerContext,
			facts.AbnormalEffects,
			facts.AbnormalEffectMask,
			facts.AbnormalEffectSlots,
			facts.RideMovementSpeed,
			facts.RideBaseAttackSpeed,
			facts.RideCurrentAttackSpeed));
		var status = packetPlan.Status == PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed
			? PlayerKnownListOperationSideEffectPacketConstructionResultStatus.Constructed
			: PlayerKnownListOperationSideEffectPacketConstructionResultStatus.PartiallyConstructed;

		return new PlayerKnownListOperationSideEffectPacketConstructionResult(
			attachment,
			status,
			packetPlan);
	}
}
