using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PlayerKnownListPlayerSideEffectPacketConstructionStatus
{
	Constructed,
	PartiallyConstructed,
	NoDescriptors,
}

public enum PlayerKnownListPlayerSideEffectPacketConstructionResultStatus
{
	Constructed,
	BlockedSubjectMismatch,
	BlockedMissingRideNpcId,
	BlockedMissingAbnormalEffectFacts,
	UnsupportedDescriptor,
}

public sealed record PlayerKnownListPlayerSideEffectPacketConstructionRequest(
	PlayerKnownListPlayerSideEffectPlan SideEffectPlan,
	Player SubjectPlayer,
	IReadOnlyList<PlayerMotion> ActiveMotions,
	SmPlayerInfoViewerContext? ViewerContext = null,
	IReadOnlyList<SmAbnormalEffectEntry>? AbnormalEffects = null,
	int AbnormalEffectMask = 0,
	int AbnormalEffectSlots = SmAbnormalEffect.FullSkillTargetSlots);

public sealed record PlayerKnownListPlayerSideEffectPacketConstructionResult(
	PlayerKnownListPlayerSideEffectDescriptor Descriptor,
	PlayerKnownListPlayerSideEffectPacketConstructionResultStatus Status,
	GameServerPacket? Packet,
	string Notes = "");

public sealed record PlayerKnownListPlayerSideEffectPacketConstructionPlan(
	PlayerKnownListPlayerSideEffectPlan SideEffectPlan,
	IReadOnlyList<PlayerKnownListPlayerSideEffectPacketConstructionResult> Results,
	PlayerKnownListPlayerSideEffectPacketConstructionStatus Status,
	bool ExecutesLivePackets,
	bool IsLive,
	bool IsJavaControllerParity,
	string JavaSource);

public sealed class PlayerKnownListPlayerSideEffectPacketConstructionService
{
	public PlayerKnownListPlayerSideEffectPacketConstructionPlan Construct(
		PlayerKnownListPlayerSideEffectPacketConstructionRequest request)
	{
		// Java parity breadcrumb: PlayerController.sendPlayerInfoPackets and the
		// PlayerController.see abnormal-effect tail build packets in descriptor order.
		// This service constructs packet objects from supplied facts only; it never sends.
		var results = request.SideEffectPlan.Descriptors
			.Select(descriptor => ConstructDescriptor(request, descriptor))
			.ToArray();
		var status = results.Length == 0
			? PlayerKnownListPlayerSideEffectPacketConstructionStatus.NoDescriptors
			: results.All(result => result.Status == PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed)
				? PlayerKnownListPlayerSideEffectPacketConstructionStatus.Constructed
				: PlayerKnownListPlayerSideEffectPacketConstructionStatus.PartiallyConstructed;

		return new PlayerKnownListPlayerSideEffectPacketConstructionPlan(
			request.SideEffectPlan,
			results,
			status,
			ExecutesLivePackets: false,
			IsLive: false,
			IsJavaControllerParity: false,
			"Non-live packet construction metadata for com.aionemu.gameserver.controllers.PlayerController player see/notSee packet sequence.");
	}

	private static PlayerKnownListPlayerSideEffectPacketConstructionResult ConstructDescriptor(
		PlayerKnownListPlayerSideEffectPacketConstructionRequest request,
		PlayerKnownListPlayerSideEffectDescriptor descriptor)
	{
		if (descriptor.SubjectPlayerObjectId != request.SubjectPlayer.ObjectId)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedSubjectMismatch,
				"Descriptor subject id does not match the supplied subject player.");
		}

		return descriptor.Kind switch
		{
			PlayerKnownListPlayerSideEffectKind.SmPlayerInfo => Constructed(
				descriptor,
				new SmPlayerInfo(request.SubjectPlayer, descriptor.AggroIcon, request.ViewerContext)),
			PlayerKnownListPlayerSideEffectKind.SmMotion => Constructed(
				descriptor,
				new SmMotion(request.SubjectPlayer.ObjectId, request.ActiveMotions)),
			PlayerKnownListPlayerSideEffectKind.SmEmotionRide => ConstructRide(request, descriptor),
			PlayerKnownListPlayerSideEffectKind.SmPlayerStance => Constructed(
				descriptor,
				new SmPlayerStance(request.SubjectPlayer.ObjectId, descriptor.StanceState ?? 1)),
			PlayerKnownListPlayerSideEffectKind.SmAbnormalEffect => ConstructAbnormalEffect(request, descriptor),
			PlayerKnownListPlayerSideEffectKind.SmDelete => Constructed(
				descriptor,
				new SmDelete(request.SubjectPlayer.ObjectId, descriptor.DeleteAnimation ?? ObjectDeleteAnimation.FadeOut)),
			_ => Blocked(
				descriptor,
				PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.UnsupportedDescriptor,
				"Descriptor kind is not supported by the non-live construction bridge."),
		};
	}

	private static PlayerKnownListPlayerSideEffectPacketConstructionResult ConstructRide(
		PlayerKnownListPlayerSideEffectPacketConstructionRequest request,
		PlayerKnownListPlayerSideEffectDescriptor descriptor)
	{
		if (descriptor.RideNpcId is not { } rideNpcId)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedMissingRideNpcId,
				"Java reads player.ride.getNpcId(); supplied descriptor has no ride NPC id.");
		}

		return Constructed(
			descriptor,
			new SmEmotion(request.SubjectPlayer, EmotionType.Ride, 0, rideNpcId));
	}

	private static PlayerKnownListPlayerSideEffectPacketConstructionResult ConstructAbnormalEffect(
		PlayerKnownListPlayerSideEffectPacketConstructionRequest request,
		PlayerKnownListPlayerSideEffectDescriptor descriptor)
	{
		if (request.AbnormalEffects is null)
		{
			return Blocked(
				descriptor,
				PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.BlockedMissingAbnormalEffectFacts,
				"Java reads EffectController.getAbnormals/getAbnormalEffects(); no supplied abnormal-effect facts were provided.");
		}

		return Constructed(
			descriptor,
			new SmAbnormalEffect(
				request.SubjectPlayer,
				request.AbnormalEffectMask,
				request.AbnormalEffects,
				request.AbnormalEffectSlots));
	}

	private static PlayerKnownListPlayerSideEffectPacketConstructionResult Constructed(
		PlayerKnownListPlayerSideEffectDescriptor descriptor,
		GameServerPacket packet) =>
		new(
			descriptor,
			PlayerKnownListPlayerSideEffectPacketConstructionResultStatus.Constructed,
			packet);

	private static PlayerKnownListPlayerSideEffectPacketConstructionResult Blocked(
		PlayerKnownListPlayerSideEffectDescriptor descriptor,
		PlayerKnownListPlayerSideEffectPacketConstructionResultStatus status,
		string notes) =>
		new(descriptor, status, Packet: null, notes);
}
