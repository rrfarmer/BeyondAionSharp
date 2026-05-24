using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerExchangeRequestService
{
	private const float ExchangeRange = 5f;

	public ExchangeRequestPlan SendExchangeRequest(Player requester, Player? target)
	{
		// Java parity: network/aion/clientpackets/CM_EXCHANGE_REQUEST.runImpl guard and question setup.
		if (target == null || target.ObjectId == requester.ObjectId)
			return ExchangeRequestPlan.Failed(ExchangeRequestStatus.NoTarget, requester.ObjectId, SmSystemMessage.ExchangeNoOneToExchange());

		if (!IsInRange(requester, target, ExchangeRange))
			return ExchangeRequestPlan.Failed(ExchangeRequestStatus.TooFar, requester.ObjectId, SmSystemMessage.ExchangeTooFarToExchange());

		if (requester.IsInAnyHide())
			return ExchangeRequestPlan.Failed(ExchangeRequestStatus.RequesterInvisible, requester.ObjectId, SmSystemMessage.ExchangeCantExchangeWhileInvisible());

		if (target.IsInAnyHide())
			return ExchangeRequestPlan.Failed(ExchangeRequestStatus.TargetInvisible, requester.ObjectId, SmSystemMessage.ExchangeCantExchangeWithInvisibleUser());

		if (!string.Equals(requester.Race, target.Race, StringComparison.OrdinalIgnoreCase))
			return ExchangeRequestPlan.FailedSilently(ExchangeRequestStatus.RaceMismatch);

		if (target.Settings.DeniesTradeRequests())
			return ExchangeRequestPlan.Failed(ExchangeRequestStatus.TargetDeniedTrade, requester.ObjectId, SmSystemMessage.MsgRejectedTrade(target.Name));

		var pending = new PendingExchangeRequest(
			requester.ObjectId,
			target.ObjectId,
			requester.Name,
			target.Name,
			SmQuestionWindow.ExchangeAcceptRequest);

		if (!target.ResponseRequester.PutRequest(
			SmQuestionWindow.ExchangeAcceptRequest,
			new QuestionResponseRequest(requester.ObjectId, QuestionResponseRequestKind.ExchangeRequest, pending)))
		{
			return ExchangeRequestPlan.Failed(
				ExchangeRequestStatus.TargetBusy,
				requester.ObjectId,
				SmSystemMessage.ExchangeCantAskWhenHeIsAskedQuestion(target.Name));
		}

		target.PendingExchangeRequest = pending;
		return ExchangeRequestPlan.Requested(
			pending,
			new ExchangePacketIntent(requester.ObjectId, SmSystemMessage.ExchangeAskedExchangeToHim(target.Name)),
			new ExchangePacketIntent(target.ObjectId, new SmQuestionWindow(
				SmQuestionWindow.ExchangeAcceptRequest,
				senderObjectId: 0,
				rangeOrCooldownSeconds: 0,
				requester.Name)));
	}

	public ExchangeResponsePlan HandleResponse(
		Player responder,
		int questionId,
		int response,
		Func<int, Player?> resolvePlayer)
	{
		if (questionId != SmQuestionWindow.ExchangeAcceptRequest)
			return ExchangeResponsePlan.NotHandled(ExchangeResponseStatus.WrongQuestion);

		var dispatch = responder.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.ExchangeRequest)
		{
			responder.PendingExchangeRequest = null;
			return ExchangeResponsePlan.NotHandled(ExchangeResponseStatus.NoPendingRequest);
		}

		var request = dispatch.Request.Payload as PendingExchangeRequest ?? responder.PendingExchangeRequest;
		responder.PendingExchangeRequest = null;
		if (request == null)
			return ExchangeResponsePlan.NotHandled(ExchangeResponseStatus.NoPendingRequest);

		var requester = resolvePlayer(request.RequesterObjectId);
		if (!dispatch.Accepted)
		{
			return requester == null
				? ExchangeResponsePlan.CreateHandled(ExchangeResponseStatus.Denied)
				: ExchangeResponsePlan.CreateHandled(
					ExchangeResponseStatus.Denied,
					new ExchangePacketIntent(requester.ObjectId, SmSystemMessage.ExchangeHeRejectedExchange(responder.Name)));
		}

		if (requester == null)
			return ExchangeResponsePlan.NotHandled(ExchangeResponseStatus.RequesterOffline);

		if (!IsInRange(requester, responder, ExchangeRange))
			return ExchangeResponsePlan.CreateHandled(
				ExchangeResponseStatus.TooFar,
				new ExchangePacketIntent(responder.ObjectId, SmSystemMessage.ExchangeTooFarToExchange()));

		requester.IsTrading = true;
		responder.IsTrading = true;
		return ExchangeResponsePlan.CreateHandled(
			ExchangeResponseStatus.Accepted,
			new ExchangePacketIntent(responder.ObjectId, new SmExchangeRequest(requester.Name)),
			new ExchangePacketIntent(requester.ObjectId, new SmExchangeRequest(responder.Name)));
	}

	private static bool IsInRange(Player requester, Player target, float range)
	{
		var a = requester.Position;
		var b = target.Position;
		if (a.WorldId != b.WorldId || a.InstanceId != b.InstanceId)
			return false;
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var dz = a.Z - b.Z;
		return dx * dx + dy * dy + dz * dz <= range * range;
	}
}

public sealed record ExchangeRequestPlan(
	ExchangeRequestStatus Status,
	PendingExchangeRequest? Request,
	IReadOnlyList<ExchangePacketIntent> PacketIntents)
{
	public static ExchangeRequestPlan Requested(PendingExchangeRequest request, params ExchangePacketIntent[] intents)
	{
		return new ExchangeRequestPlan(ExchangeRequestStatus.Requested, request, intents);
	}

	public static ExchangeRequestPlan Failed(ExchangeRequestStatus status, int recipientObjectId, GameServerPacket packet)
	{
		return new ExchangeRequestPlan(status, null, [new ExchangePacketIntent(recipientObjectId, packet)]);
	}

	public static ExchangeRequestPlan FailedSilently(ExchangeRequestStatus status)
	{
		return new ExchangeRequestPlan(status, null, Array.Empty<ExchangePacketIntent>());
	}
}

public enum ExchangeRequestStatus
{
	Requested,
	NoTarget,
	TooFar,
	RequesterInvisible,
	TargetInvisible,
	RaceMismatch,
	TargetDeniedTrade,
	TargetBusy,
}

public sealed record ExchangeResponsePlan(
	bool Handled,
	ExchangeResponseStatus Status,
	IReadOnlyList<ExchangePacketIntent> PacketIntents)
{
	public static ExchangeResponsePlan CreateHandled(ExchangeResponseStatus status, params ExchangePacketIntent[] intents)
	{
		return new ExchangeResponsePlan(true, status, intents);
	}

	public static ExchangeResponsePlan NotHandled(ExchangeResponseStatus status)
	{
		return new ExchangeResponsePlan(false, status, Array.Empty<ExchangePacketIntent>());
	}
}

public enum ExchangeResponseStatus
{
	Accepted,
	Denied,
	WrongQuestion,
	NoPendingRequest,
	RequesterOffline,
	TooFar,
}

public sealed record ExchangePacketIntent(int RecipientObjectId, GameServerPacket Packet);
