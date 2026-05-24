using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class PlayerRecallInstantRequestService
{
	public RecallInstantRequestPlan SendRecallRequest(Player effector, Player effected, WorldPosition destination)
	{
		// Java parity: skillengine/effect/RecallInstantEffect.applyEffect registers the effected player's
		// ResponseRequester before sending STR_SUMMON_PARTY_DO_YOU_ACCEPT_REQUEST.
		var request = new PendingRecallInstantRequest(
			effector.ObjectId,
			effector.Name,
			effected.ObjectId,
			effected.Name,
			destination,
			SmQuestionWindow.SummonPartyAcceptRequest);

		if (!effected.ResponseRequester.PutRequest(
			SmQuestionWindow.SummonPartyAcceptRequest,
			new QuestionResponseRequest(effector.ObjectId, QuestionResponseRequestKind.RecallInstant, request)))
		{
			return RecallInstantRequestPlan.DuplicateRequest(request);
		}

		effected.PendingRecallInstantRequest = request;
		return RecallInstantRequestPlan.Requested(
			request,
			new RecallPacketIntent(
				effected.ObjectId,
				new SmQuestionWindow(
					SmQuestionWindow.SummonPartyAcceptRequest,
					senderObjectId: 0,
					rangeOrCooldownSeconds: 0,
					effector.Name,
					"Summon Group Member",
					"30")));
	}

	public RecallInstantResponsePlan HandleResponse(
		Player effected,
		int questionId,
		int response,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond, which removes
		// RecallInstantEffect's RequestResponseHandler before accept/deny behavior.
		if (questionId != SmQuestionWindow.SummonPartyAcceptRequest)
			return RecallInstantResponsePlan.NotHandled(RecallInstantResponseStatus.WrongQuestion);

		var dispatch = effected.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.RecallInstant)
		{
			effected.PendingRecallInstantRequest = null;
			return RecallInstantResponsePlan.NotHandled(RecallInstantResponseStatus.NoPendingRequest);
		}

		var request = dispatch.Request.Payload as PendingRecallInstantRequest ?? effected.PendingRecallInstantRequest;
		effected.PendingRecallInstantRequest = null;
		if (request == null)
			return RecallInstantResponsePlan.NotHandled(RecallInstantResponseStatus.NoPendingRequest);

		var effector = resolvePlayer(request.EffectorObjectId);
		if (!dispatch.Accepted)
		{
			return effector == null
				? RecallInstantResponsePlan.CreateHandled(RecallInstantResponseStatus.Denied)
				: RecallInstantResponsePlan.CreateHandled(
					RecallInstantResponseStatus.Denied,
					new RecallPacketIntent(effector.ObjectId, SmSystemMessage.RecallRejectedEffect(effected.Name)),
					new RecallPacketIntent(effected.ObjectId, SmSystemMessage.RecallRejectEffect(effector.Name)));
		}

		if (effector == null)
			return RecallInstantResponsePlan.NotHandled(RecallInstantResponseStatus.EffectorMissing);

		var teleport = PlayerTeleportService.TeleportWithinSameInstance(effected, request.Destination);
		return RecallInstantResponsePlan.CreateAccepted(request, teleport);
	}
}

public sealed record RecallPacketIntent(int RecipientObjectId, GameServerPacket Packet);

public sealed record RecallInstantRequestPlan(
	RecallInstantRequestStatus Status,
	PendingRecallInstantRequest Request,
	IReadOnlyList<RecallPacketIntent> PacketIntents)
{
	public static RecallInstantRequestPlan Requested(PendingRecallInstantRequest request, params RecallPacketIntent[] intents)
	{
		return new RecallInstantRequestPlan(RecallInstantRequestStatus.Requested, request, intents);
	}

	public static RecallInstantRequestPlan DuplicateRequest(PendingRecallInstantRequest request)
	{
		return new RecallInstantRequestPlan(RecallInstantRequestStatus.DuplicateRequest, request, Array.Empty<RecallPacketIntent>());
	}
}

public enum RecallInstantRequestStatus
{
	Requested,
	DuplicateRequest,
}

public sealed record RecallInstantResponsePlan(
	bool Handled,
	RecallInstantResponseStatus Status,
	PendingRecallInstantRequest? Request,
	PlayerTeleportResult? Teleport,
	IReadOnlyList<RecallPacketIntent> PacketIntents)
{
	public static RecallInstantResponsePlan CreateAccepted(PendingRecallInstantRequest request, PlayerTeleportResult teleport)
	{
		return new RecallInstantResponsePlan(
			true,
			RecallInstantResponseStatus.Accepted,
			request,
			teleport,
			Array.Empty<RecallPacketIntent>());
	}

	public static RecallInstantResponsePlan CreateHandled(RecallInstantResponseStatus status, params RecallPacketIntent[] intents)
	{
		return new RecallInstantResponsePlan(true, status, null, null, intents);
	}

	public static RecallInstantResponsePlan NotHandled(RecallInstantResponseStatus status)
	{
		return new RecallInstantResponsePlan(false, status, null, null, Array.Empty<RecallPacketIntent>());
	}
}

public enum RecallInstantResponseStatus
{
	Accepted,
	Denied,
	WrongQuestion,
	NoPendingRequest,
	EffectorMissing,
}
