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
		// Java parity: network/aion/serverpackets/SM_LEGION_INFO.writeImpl.
		// Current C# enter-world hydration has the legion identity, level, permissions, and disband time.
		// Ranking, contribution, dominion, and announcement fields remain defaulted until the shared Legion aggregate is ported.
		return new SmLegionInfo(
			player.LegionName,
			player.LegionLevel,
			rankingPosition: 0,
			player.LegionDeputyPermission,
			player.LegionCenturionPermission,
			player.LegionLegionaryPermission,
			player.LegionVolunteerPermission,
			contributionPoints: 0,
			player.LegionDisbandTime,
			occupiedLegionDominion: 0,
			lastLegionDominion: 0,
			currentLegionDominion: 0,
			player.LegionAnnouncement,
			player.LegionAnnouncementEpochSeconds);
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
