using System.Threading;

namespace Aion.GameServer.Model.GameObjects;

public sealed class QuestionResponseRegistry
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, QuestionResponseRequest> _activeRequests = [];

	public bool PutRequest(int questionId, QuestionResponseRequest? request)
	{
		// Java parity: ResponseRequester.putRequest(messageId, handler) rejects null handlers and uses putIfAbsent.
		if (request == null)
			return false;

		lock (_sync)
		{
			if (_activeRequests.ContainsKey(questionId))
				return false;

			_activeRequests[questionId] = request;
			return true;
		}
	}

	public QuestionResponseDispatch? Respond(int questionId, int responseCode)
	{
		// Java parity: ResponseRequester.respond removes the handler before invoking RequestResponseHandler.handle.
		QuestionResponseRequest? request;
		lock (_sync)
		{
			if (!_activeRequests.Remove(questionId, out request))
				return null;
		}

		return new QuestionResponseDispatch(
			questionId,
			responseCode,
			Accepted: responseCode != 0,
			request);
	}

	public bool Remove(int questionId)
	{
		// Java parity: ResponseRequester.remove(messageId).
		lock (_sync)
			return _activeRequests.Remove(questionId);
	}

	public IReadOnlyList<QuestionResponseDispatch> DenyAll()
	{
		// Java parity: ResponseRequester.denyAll handles every active request with response 0, then clears the map.
		KeyValuePair<int, QuestionResponseRequest>[] requests;
		lock (_sync)
		{
			requests = _activeRequests.ToArray();
			_activeRequests.Clear();
		}

		return requests
			.Select(request => new QuestionResponseDispatch(
				request.Key,
				ResponseCode: 0,
				Accepted: false,
				request.Value))
			.ToArray();
	}

	public int Count
	{
		get
		{
			lock (_sync)
				return _activeRequests.Count;
		}
	}
}

public enum QuestionResponseRequestKind
{
	Unknown,
	LeagueInvite,
	FriendInvite,
	RiftPortal,
	KiskBind,
	SoulBind,
	ChargeAll,
	TeleportToNpc,
	GroupInvite,
	AllianceInvite,
	DuelRequest,
	DuelWithdraw,
	ExperienceRecovery,
	ExchangeRequest,
	RecallInstant,
	CraftSkillLearn,
	StorageExpansion,
}

public sealed record QuestionResponseRequest(
	int RequesterObjectId,
	QuestionResponseRequestKind Kind,
	object? Payload = null);

public sealed record QuestionResponseDispatch(
	int QuestionId,
	int ResponseCode,
	bool Accepted,
	QuestionResponseRequest Request);
