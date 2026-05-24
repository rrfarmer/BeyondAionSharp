using Aion.GameServer.Model.GameObjects;
using System.Threading;

namespace Aion.GameServer.Services;

public sealed class PlayerLeagueRuntime
{
	private readonly Lock _sync = new();
	private readonly Dictionary<int, List<PlayerLeagueMember>> _membersByLeagueId = [];
	private readonly Dictionary<int, int> _leagueIdByAllianceId = [];
	private readonly Dictionary<int, int> _leaderAllianceIdByLeagueId = [];

	public PlayerLeagueSnapshot CreateLeague(int leagueId, int leaderAllianceId)
	{
		// Java parity: model/team/league/LeagueService.createLeague creates a LeagueMember for the leader alliance at position 0.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leagueId, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaderAllianceId, 0);

		lock (_sync)
		{
			if (_membersByLeagueId.ContainsKey(leagueId))
				throw new InvalidOperationException("League already exists.");
			if (_leagueIdByAllianceId.ContainsKey(leaderAllianceId))
				throw new InvalidOperationException("Alliance is already in league.");

			var members = new List<PlayerLeagueMember> { new(leaderAllianceId, leaguePosition: 0) };
			_membersByLeagueId[leagueId] = members;
			_leagueIdByAllianceId[leaderAllianceId] = leagueId;
			_leaderAllianceIdByLeagueId[leagueId] = leaderAllianceId;
			return CreateSnapshot(leagueId, members, leaderAllianceId);
		}
	}

	public PlayerLeagueSnapshot AddAlliance(int leagueId, int allianceId)
	{
		// Java parity: model/team/league/events/LeagueJoinEvent adds the invited alliance at league.size().
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leagueId, 0);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(allianceId, 0);

		lock (_sync)
		{
			if (!_membersByLeagueId.TryGetValue(leagueId, out var members)
				|| !_leaderAllianceIdByLeagueId.TryGetValue(leagueId, out var leaderAllianceId))
				throw new InvalidOperationException("League should not be null");
			if (_leagueIdByAllianceId.ContainsKey(allianceId))
				throw new InvalidOperationException("Alliance is already in league.");
			if (members.Count >= 8)
				throw new InvalidOperationException("League is full.");

			members.Add(new PlayerLeagueMember(allianceId, members.Count));
			_leagueIdByAllianceId[allianceId] = leagueId;
			return CreateSnapshot(leagueId, members, leaderAllianceId);
		}
	}

	public PlayerLeagueSnapshot? ResolveByAllianceId(int allianceId)
	{
		// Java parity: model/team/alliance/PlayerAlliance.getLeague returns the live League pointer, modeled here by alliance id lookup.
		lock (_sync)
			return _leagueIdByAllianceId.TryGetValue(allianceId, out var leagueId)
				&& _membersByLeagueId.TryGetValue(leagueId, out var members)
				&& _leaderAllianceIdByLeagueId.TryGetValue(leagueId, out var leaderAllianceId)
					? CreateSnapshot(leagueId, members, leaderAllianceId)
					: null;
	}

	public IReadOnlyList<int> GetAllianceIdsByPosition(int leagueId)
	{
		// Java parity: model/team/league/League.getSortedMembers sorts by LeagueMember.getLeaguePosition.
		lock (_sync)
			return _membersByLeagueId.TryGetValue(leagueId, out var members)
				? members
					.OrderBy(member => member.LeaguePosition)
					.Select(member => member.AllianceId)
					.ToArray()
				: Array.Empty<int>();
	}

	public int? GetLeaguePosition(int leagueId, int allianceId)
	{
		lock (_sync)
			return _membersByLeagueId.TryGetValue(leagueId, out var members)
				? members.FirstOrDefault(member => member.AllianceId == allianceId)?.LeaguePosition
				: null;
	}

	public PlayerLeagueMovePlan? MoveAlliance(
		int callerAllianceId,
		int callerObjectId,
		int selectedAllianceId,
		int targetAllianceId,
		PlayerAllianceRuntime allianceRuntime)
	{
		// Java parity: model/team/league/LeagueService.moveAlliance dispatches LeagueMoveEvent only when the caller is
		// the leader player of the league leader alliance. Packet fanout from LeagueMoveEvent is intentionally deferred.
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(callerAllianceId, 0);

		lock (_sync)
		{
			if (!_leagueIdByAllianceId.TryGetValue(callerAllianceId, out var leagueId)
				|| !_membersByLeagueId.TryGetValue(leagueId, out var members)
				|| !_leaderAllianceIdByLeagueId.TryGetValue(leagueId, out var leaderAllianceId))
				throw new InvalidOperationException("League should not be null");

			var leaderDescriptor = allianceRuntime.GetDescriptor(leaderAllianceId);
			if (leaderDescriptor == null || leaderDescriptor.LeaderObjectId != callerObjectId)
				return null;

			var selected = members.FirstOrDefault(member => member.AllianceId == selectedAllianceId);
			if (selected == null)
				throw new InvalidOperationException($"League member should not be null: {selectedAllianceId}");

			var target = members.FirstOrDefault(member => member.AllianceId == targetAllianceId);
			if (target == null)
				throw new InvalidOperationException($"League member should not be null: {targetAllianceId}");

			var selectedCurrentPosition = selected.LeaguePosition;
			var targetCurrentPosition = target.LeaguePosition;
			selected.LeaguePosition = targetCurrentPosition;
			target.LeaguePosition = selectedCurrentPosition;

			return new PlayerLeagueMovePlan(
				leagueId,
				callerAllianceId,
				selectedAllianceId,
				targetAllianceId,
				selectedCurrentPosition,
				targetCurrentPosition,
				GetSortedAllianceIds(members));
		}
	}

	private static PlayerLeagueSnapshot CreateSnapshot(
		int leagueId,
		IReadOnlyList<PlayerLeagueMember> members,
		int leaderAllianceId)
	{
		return new PlayerLeagueSnapshot(
			leagueId,
			leaderAllianceId,
			GetSortedAllianceIds(members));
	}

	private static IReadOnlyList<int> GetSortedAllianceIds(IReadOnlyList<PlayerLeagueMember> members)
	{
		return members
			.OrderBy(member => member.LeaguePosition)
			.Select(member => member.AllianceId)
			.ToArray();
	}

	private sealed class PlayerLeagueMember(int allianceId, int leaguePosition)
	{
		public int AllianceId { get; } = allianceId;
		public int LeaguePosition { get; set; } = leaguePosition;
	}
}

public sealed record PlayerLeagueSnapshot(
	int LeagueId,
	int LeaderAllianceId,
	IReadOnlyList<int> AllianceIdsByPosition);

public sealed record PlayerLeagueMovePlan(
	int LeagueId,
	int CallerAllianceId,
	int SelectedAllianceId,
	int TargetAllianceId,
	int SelectedPreviousPosition,
	int TargetPreviousPosition,
	IReadOnlyList<int> AllianceIdsByPosition);
