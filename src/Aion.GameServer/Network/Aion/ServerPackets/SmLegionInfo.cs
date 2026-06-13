using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionInfo : GameServerPacket
{
	public const int PacketOpCode = 110; // Java parity: ServerPacketsOpcodes addPacketOpcode(110, SM_LEGION_INFO.class).

	private readonly string _legionName;
	private readonly int _legionLevel;
	private readonly int _rankingPosition;
	private readonly int _deputyPermission;
	private readonly int _centurionPermission;
	private readonly int _legionaryPermission;
	private readonly int _volunteerPermission;
	private readonly long _contributionPoints;
	private readonly int _disbandTime;
	private readonly int _occupiedLegionDominion;
	private readonly int _lastLegionDominion;
	private readonly int _currentLegionDominion;
	private readonly string _announcement;
	private readonly int _announcementTime;

	public SmLegionInfo(
		string legionName,
		int legionLevel,
		int rankingPosition,
		int deputyPermission,
		int centurionPermission,
		int legionaryPermission,
		int volunteerPermission,
		long contributionPoints,
		int disbandTime,
		int occupiedLegionDominion,
		int lastLegionDominion,
		int currentLegionDominion,
		string? announcement = null,
		int announcementTime = 0)
		: base(PacketOpCode)
	{
		_legionName = legionName;
		_legionLevel = legionLevel;
		_rankingPosition = rankingPosition;
		_deputyPermission = deputyPermission;
		_centurionPermission = centurionPermission;
		_legionaryPermission = legionaryPermission;
		_volunteerPermission = volunteerPermission;
		_contributionPoints = contributionPoints;
		_disbandTime = disbandTime;
		_occupiedLegionDominion = occupiedLegionDominion;
		_lastLegionDominion = lastLegionDominion;
		_currentLegionDominion = currentLegionDominion;
		_announcement = announcement ?? string.Empty;
		_announcementTime = announcementTime;
	}

	public static SmLegionInfo FromPlayer(Player player)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_INFO.writeImpl — read legion data from the faithful Legion.
		// Ranking remains defaulted until the AbyssRankingCache legion path is ported.
		var legion = player.GetLegion();
		var announcement = legion?.GetAnnouncement();
		int announcementTime = announcement != null
			? (int)(System.DateTime.SpecifyKind(announcement.Time, System.DateTimeKind.Utc) - System.DateTime.UnixEpoch).TotalSeconds
			: 0;
		return new SmLegionInfo(
			player.LegionName,
			(legion?.GetLegionLevel() ?? 0),
			rankingPosition: 0,
			legion?.GetDeputyPermission() ?? 0,
			legion?.GetCenturionPermission() ?? 0,
			legion?.GetLegionaryPermission() ?? 0,
			legion?.GetVolunteerPermission() ?? 0,
			legion?.GetContributionPoints() ?? 0,
			legion?.GetDisbandTime() ?? 0,
			legion?.GetOccupiedLegionDominion() ?? 0,
			legion?.GetLastLegionDominion() ?? 0,
			legion?.GetCurrentLegionDominion() ?? 0,
			announcement?.Message,
			announcementTime);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteS(_legionName);
		buffer.WriteC(_legionLevel);
		buffer.WriteD(_rankingPosition);
		buffer.WriteH(_deputyPermission);
		buffer.WriteH(_centurionPermission);
		buffer.WriteH(_legionaryPermission);
		buffer.WriteH(_volunteerPermission);
		buffer.WriteQ(_contributionPoints);
		buffer.WriteD(0); // Java parity: SM_LEGION_INFO.writeImpl unk.
		buffer.WriteD(0); // Java parity: SM_LEGION_INFO.writeImpl unk.
		buffer.WriteD(_disbandTime);
		buffer.WriteD(_occupiedLegionDominion);
		buffer.WriteD(_lastLegionDominion);
		buffer.WriteD(_currentLegionDominion);
		WriteAnnouncements(buffer);
	}

	private void WriteAnnouncements(PacketBuffer buffer)
	{
		// Java parity: SM_LEGION_INFO.writeAnnouncements writes up to seven announcements and stops at an empty one.
		for (var i = 0; i < 7; i++)
		{
			var message = i == 0 ? _announcement : string.Empty;
			buffer.WriteS(message);
			if (string.IsNullOrEmpty(message))
				break;

			buffer.WriteD(_announcementTime);
		}
	}
}
