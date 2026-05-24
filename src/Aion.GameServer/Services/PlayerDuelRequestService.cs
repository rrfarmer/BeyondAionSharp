using System.Collections.Concurrent;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerDuelRequestService
{
	private readonly ConcurrentDictionary<int, int> _duels = new();

	public DuelRequestPlan SendDuelRequest(Player requester, Player? target)
	{
		// Java parity: services/DuelService.onDuelRequest through the represented request setup path.
		if (target == null || requester.ObjectId == target.ObjectId)
			return DuelRequestPlan.Rejected(requester.ObjectId, SmSystemMessage.DuelNoUserToRequest());

		if (IsDueling(requester))
			return DuelRequestPlan.Rejected(requester.ObjectId, SmSystemMessage.DuelYouAreInDuelAlready());

		if (IsDueling(target))
			return DuelRequestPlan.Rejected(requester.ObjectId, SmSystemMessage.DuelPartnerInDuelAlready(target.Name));

		if (target.Settings.DeniesDuelRequests())
			return DuelRequestPlan.Rejected(requester.ObjectId, SmSystemMessage.RejectedDuel(target.Name));

		if (requester.IsInState(PlayerCreatureState.Dead)
			|| target.IsInState(PlayerCreatureState.Dead)
			|| requester.LifeStats?.CurrentHp <= 0
			|| target.LifeStats?.CurrentHp <= 0)
		{
			return DuelRequestPlan.Rejected(requester.ObjectId, SmSystemMessage.DuelPartnerInvalid(target.Name));
		}

		var request = new PendingDuelRequest(requester.ObjectId, requester.Name, target.ObjectId, target.Name);
		if (!target.ResponseRequester.PutRequest(
			SmQuestionWindow.DuelAcceptRequest,
			new QuestionResponseRequest(requester.ObjectId, QuestionResponseRequestKind.DuelRequest, request)))
		{
			return DuelRequestPlan.Rejected(
				requester.ObjectId,
				SmSystemMessage.DuelCantRequestWhenHeIsAskedQuestion(target.Name));
		}

		target.PendingDuelRequest = request;
		requester.ResponseRequester.PutRequest(
			SmQuestionWindow.DuelWithdrawRequest,
			new QuestionResponseRequest(target.ObjectId, QuestionResponseRequestKind.DuelWithdraw, request));
		requester.PendingDuelWithdrawRequest = request;

		return DuelRequestPlan.Requested(
			request,
			[
				new DuelPacketIntent(target.ObjectId, new SmQuestionWindow(SmQuestionWindow.DuelAcceptRequest, 0, 0, requester.Name)),
				new DuelPacketIntent(target.ObjectId, SmSystemMessage.DuelRequested(requester.Name)),
				new DuelPacketIntent(requester.ObjectId, new SmQuestionWindow(SmQuestionWindow.DuelWithdrawRequest, 0, 0, target.Name)),
				new DuelPacketIntent(requester.ObjectId, SmSystemMessage.DuelRequestToPartner(target.Name)),
			]);
	}

	public DuelResponsePlan HandleTargetResponse(
		Player responder,
		int questionId,
		int response,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: CM_QUESTION_RESPONSE -> ResponseRequester.respond invokes the anonymous
		// DuelService request handler denyRequest or acceptRequest.
		if (questionId != SmQuestionWindow.DuelAcceptRequest)
			return DuelResponsePlan.Ignored();

		var dispatch = responder.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.DuelRequest)
			return DuelResponsePlan.MissingRequest();

		var request = dispatch.Request.Payload as PendingDuelRequest ?? responder.PendingDuelRequest;
		responder.PendingDuelRequest = null;
		if (request == null)
			return DuelResponsePlan.MissingRequest();

		var requester = resolvePlayer(request.RequesterObjectId);
		if (requester == null)
			return DuelResponsePlan.MissingRequest(request);

		if (!dispatch.Accepted)
		{
			requester.ResponseRequester.Remove(SmQuestionWindow.DuelWithdrawRequest);
			requester.PendingDuelWithdrawRequest = null;
			return DuelResponsePlan.Handled(
				request,
				[
					new DuelPacketIntent(requester.ObjectId, SmCloseQuestionWindow.DuelHeRejectDuel(responder.Name)),
					new DuelPacketIntent(responder.ObjectId, SmSystemMessage.DuelRejectDuel(requester.Name)),
				]);
		}

		if (IsDueling(requester))
			return DuelResponsePlan.Handled(request, Array.Empty<DuelPacketIntent>());

		var intents = new List<DuelPacketIntent>();
		if (requester.ResponseRequester.Remove(SmQuestionWindow.DuelWithdrawRequest))
		{
			requester.PendingDuelWithdrawRequest = null;
			intents.Add(new DuelPacketIntent(requester.ObjectId, SmCloseQuestionWindow.CloseQuestionWindow()));
		}

		RegisterDuel(requester.ObjectId, responder.ObjectId);
		intents.Add(new DuelPacketIntent(requester.ObjectId, SmDuel.Started(responder.ObjectId)));
		intents.Add(new DuelPacketIntent(responder.ObjectId, SmDuel.Started(requester.ObjectId)));
		return DuelResponsePlan.Handled(request, intents);
	}

	public DuelResponsePlan HandleWithdrawResponse(
		Player requester,
		int questionId,
		int response,
		Func<int, Player?> resolvePlayer)
	{
		// Java parity: DuelService.confirmDuelWith registers STR_DUEL_DO_YOU_WITHDRAW_REQUEST
		// on the requester; accepting the prompt cancels the target's pending request.
		if (questionId != SmQuestionWindow.DuelWithdrawRequest)
			return DuelResponsePlan.Ignored();

		var dispatch = requester.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.DuelWithdraw)
			return DuelResponsePlan.MissingRequest();

		var request = dispatch.Request.Payload as PendingDuelRequest ?? requester.PendingDuelWithdrawRequest;
		requester.PendingDuelWithdrawRequest = null;
		if (request == null)
			return DuelResponsePlan.MissingRequest();

		if (!dispatch.Accepted)
			return DuelResponsePlan.Handled(request, Array.Empty<DuelPacketIntent>());

		var target = resolvePlayer(request.TargetObjectId);
		if (target == null)
			return DuelResponsePlan.MissingRequest(request);

		target.ResponseRequester.Remove(SmQuestionWindow.DuelAcceptRequest);
		target.PendingDuelRequest = null;
		return DuelResponsePlan.Handled(
			request,
			[
				new DuelPacketIntent(target.ObjectId, SmCloseQuestionWindow.DuelRequesterWithdrawRequest(requester.Name)),
				new DuelPacketIntent(requester.ObjectId, SmSystemMessage.DuelWithdrawRequest(target.Name)),
			]);
	}

	public bool IsDueling(Player player)
	{
		return _duels.TryGetValue(player.ObjectId, out var opponentId)
			&& _duels.ContainsKey(opponentId);
	}

	public int? GetOpponentId(Player player)
	{
		return _duels.TryGetValue(player.ObjectId, out var opponentId) ? opponentId : null;
	}

	public DuelEndPlan LoseDuel(Player loser, Func<int, Player?> resolvePlayer)
	{
		// Java parity: DuelService.loseDuel sends DUEL_LOST to the loser, DUEL_WON to the online
		// winner, then removes both directions from the duel map.
		var opponentId = GetOpponentId(loser);
		if (opponentId == null)
			return DuelEndPlan.NotDueling();

		var winner = resolvePlayer(opponentId.Value);
		var intents = new List<DuelPacketIntent>
		{
			new(loser.ObjectId, SmDuel.Result(DuelResultKind.Lose, winner?.Name ?? opponentId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
		};
		if (winner != null)
			intents.Add(new DuelPacketIntent(winner.ObjectId, SmDuel.Result(DuelResultKind.Win, loser.Name)));

		RemoveDuel(loser.ObjectId);
		return DuelEndPlan.Ended(DuelEndReason.Loss, loser.ObjectId, opponentId.Value, intents);
	}

	public DuelEndPlan DrawDuel(Player firstPlayer, Func<int, Player?> resolvePlayer)
	{
		// Java parity: DuelService.createTask draw callback sends DUEL_DRAW to both still-dueling
		// players, then removes both directions from the duel map. The scheduler itself is not ported here.
		var opponentId = GetOpponentId(firstPlayer);
		if (opponentId == null)
			return DuelEndPlan.NotDueling();

		var opponent = resolvePlayer(opponentId.Value);
		var intents = new List<DuelPacketIntent>
		{
			new(firstPlayer.ObjectId, SmDuel.Result(DuelResultKind.Draw, opponent?.Name ?? opponentId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
		};
		if (opponent != null)
			intents.Add(new DuelPacketIntent(opponent.ObjectId, SmDuel.Result(DuelResultKind.Draw, firstPlayer.Name)));

		RemoveDuel(firstPlayer.ObjectId);
		return DuelEndPlan.Ended(DuelEndReason.Draw, firstPlayer.ObjectId, opponentId.Value, intents);
	}

	private void RegisterDuel(int requesterObjectId, int responderObjectId)
	{
		// Java parity: DuelService.registerDuel stores both object-id directions.
		_duels[requesterObjectId] = responderObjectId;
		_duels[responderObjectId] = requesterObjectId;
	}

	private void RemoveDuel(int playerObjectId)
	{
		// Java parity: DuelService.removeDuel removes the player and opponent entries, then cancels draw tasks.
		if (_duels.TryRemove(playerObjectId, out var opponentId))
			_duels.TryRemove(opponentId, out _);
	}
}

public sealed record DuelPacketIntent(int RecipientObjectId, GameServerPacket Packet);

public sealed record DuelRequestPlan(
	DuelRequestStatus Status,
	PendingDuelRequest? Request,
	IReadOnlyList<DuelPacketIntent> PacketIntents,
	DuelPacketIntent? RejectionIntent)
{
	public static DuelRequestPlan Requested(
		PendingDuelRequest request,
		IReadOnlyList<DuelPacketIntent> packetIntents)
	{
		return new DuelRequestPlan(DuelRequestStatus.Requested, request, packetIntents, null);
	}

	public static DuelRequestPlan Rejected(int requesterObjectId, SmSystemMessage message)
	{
		return new DuelRequestPlan(
			DuelRequestStatus.Rejected,
			null,
			Array.Empty<DuelPacketIntent>(),
			new DuelPacketIntent(requesterObjectId, message));
	}
}

public enum DuelRequestStatus
{
	Requested,
	Rejected,
}

public sealed record DuelResponsePlan(
	DuelResponseStatus Status,
	PendingDuelRequest? Request,
	IReadOnlyList<DuelPacketIntent> PacketIntents)
{
	public static DuelResponsePlan Ignored()
	{
		return new DuelResponsePlan(DuelResponseStatus.Ignored, null, Array.Empty<DuelPacketIntent>());
	}

	public static DuelResponsePlan MissingRequest(PendingDuelRequest? request = null)
	{
		return new DuelResponsePlan(DuelResponseStatus.MissingRequest, request, Array.Empty<DuelPacketIntent>());
	}

	public static DuelResponsePlan Handled(
		PendingDuelRequest request,
		IReadOnlyList<DuelPacketIntent> packetIntents)
	{
		return new DuelResponsePlan(DuelResponseStatus.Handled, request, packetIntents);
	}
}

public enum DuelResponseStatus
{
	Ignored,
	MissingRequest,
	Handled,
}

public sealed record DuelEndPlan(
	DuelEndStatus Status,
	DuelEndReason? Reason,
	int PlayerObjectId,
	int? OpponentObjectId,
	IReadOnlyList<DuelPacketIntent> PacketIntents)
{
	public static DuelEndPlan NotDueling()
	{
		return new DuelEndPlan(DuelEndStatus.NotDueling, null, 0, null, Array.Empty<DuelPacketIntent>());
	}

	public static DuelEndPlan Ended(
		DuelEndReason reason,
		int playerObjectId,
		int opponentObjectId,
		IReadOnlyList<DuelPacketIntent> packetIntents)
	{
		return new DuelEndPlan(DuelEndStatus.Ended, reason, playerObjectId, opponentObjectId, packetIntents);
	}
}

public enum DuelEndStatus
{
	NotDueling,
	Ended,
}

public enum DuelEndReason
{
	Loss,
	Draw,
}
