using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerSearchMatchServiceTests
{
	private const int SearcherObjectId = 1000;

	private static PlayerSearchCriteria DefaultCriteria(
		string searcherRace = "ELYOS",
		bool searcherIsStaff = false,
		string nameFilter = "",
		int region = 0,
		int classMask = 0,
		int minLevel = 0xFF,
		int maxLevel = 0xFF,
		int lfgOnly = 0,
		bool factionsSearchMode = false,
		bool searchGmList = false)
		=> new(searcherRace, searcherIsStaff, nameFilter, region, classMask, minLevel, maxLevel, lfgOnly, factionsSearchMode, searchGmList);

	private static PlayerSearchCandidate DefaultCandidate(
		int objectId = 2000,
		string name = "Target",
		string race = "ELYOS",
		int level = 50,
		int classId = 5,
		int worldId = 210010000,
		bool isStaff = false,
		bool isLookingForGroup = false,
		bool friendStatusOffline = false)
		=> new(objectId, name, race, level, classId, worldId, isStaff, isLookingForGroup, friendStatusOffline);

	[Fact]
	public void Matches_DefaultCriteriaAndCandidate_ReturnsTrue()
	{
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(), DefaultCandidate(), SearcherObjectId));
	}

	[Fact]
	public void Matches_SelfIsExcluded()
	{
		// Java parity: player.equals(activePlayer) -> skip.
		var candidate = DefaultCandidate(objectId: SearcherObjectId);
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_DifferentRaceExcludedForNonStaffWithoutFactionsMode()
	{
		var candidate = DefaultCandidate(race: "ASMODIANS");
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(searcherRace: "ELYOS"), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_DifferentRaceAllowedWithFactionsSearchMode()
	{
		var candidate = DefaultCandidate(race: "ASMODIANS");
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(searcherRace: "ELYOS", factionsSearchMode: true), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_DifferentRaceAllowedForStaffSearcher()
	{
		// Java parity: staff (isStaff) bypasses race / offline / staff filters.
		var candidate = DefaultCandidate(race: "ASMODIANS");
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(searcherRace: "ELYOS", searcherIsStaff: true), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_OfflineFriendStatusExcludedForNonStaff()
	{
		var candidate = DefaultCandidate(friendStatusOffline: true);
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_StaffCandidateExcludedUnlessSearchGmList()
	{
		var candidate = DefaultCandidate(isStaff: true);
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(), candidate, SearcherObjectId));
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(searchGmList: true), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_LfgOnlyExcludesNonLfgCandidate()
	{
		var candidate = DefaultCandidate(isLookingForGroup: false);
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(lfgOnly: 1), candidate, SearcherObjectId));
		var lfgCandidate = DefaultCandidate(isLookingForGroup: true);
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(lfgOnly: 1), lfgCandidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_NameFilterIsCaseInsensitiveSubstring()
	{
		var candidate = DefaultCandidate(name: "Shadowblade");
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(nameFilter: "shadow"), candidate, SearcherObjectId));
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(nameFilter: "light"), candidate, SearcherObjectId));
	}

	[Theory]
	[InlineData(40, 0xFF, true)]  // minLevel 40, candidate 50 -> ok
	[InlineData(60, 0xFF, false)] // minLevel 60, candidate 50 -> excluded
	[InlineData(0xFF, 45, false)] // maxLevel 45, candidate 50 -> excluded
	[InlineData(0xFF, 55, true)]  // maxLevel 55, candidate 50 -> ok
	public void Matches_LevelRangeFilter(int minLevel, int maxLevel, bool expected)
	{
		var candidate = DefaultCandidate(level: 50);
		Assert.Equal(expected, PlayerSearchMatchService.Matches(DefaultCriteria(minLevel: minLevel, maxLevel: maxLevel), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_ClassMaskFilter()
	{
		// classId 5 (Ranger) -> bit 1<<5 = 32.
		var candidate = DefaultCandidate(classId: 5);
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(classMask: 1 << 5), candidate, SearcherObjectId));
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(classMask: 1 << 6), candidate, SearcherObjectId));
	}

	[Fact]
	public void Matches_RegionFilter()
	{
		var candidate = DefaultCandidate(worldId: 210010000);
		Assert.True(PlayerSearchMatchService.Matches(DefaultCriteria(region: 210010000), candidate, SearcherObjectId));
		Assert.False(PlayerSearchMatchService.Matches(DefaultCriteria(region: 220010000), candidate, SearcherObjectId));
	}

	[Fact]
	public void SmPlayerSearch_WritesCountAndRowFields()
	{
		// Java parity: SM_PLAYER_SEARCH.writeImpl writes count then per-row fields.
		var rows = new[]
		{
			new PlayerSearchResultRow(210010000, 100.5f, 200.5f, 300.5f, ClassId: 5, GenderId: 1, Level: 50, Status: 2, Name: "Target"),
		};
		var packet = new SmPlayerSearch(rows);
		using var buffer = new PacketBuffer(SerializeUnencryptedPayload(packet));

		Assert.Equal(1, buffer.ReadH()); // count
		Assert.Equal(210010000, buffer.ReadD()); // worldId
		Assert.Equal(100.5f, buffer.ReadF()); // x
		Assert.Equal(200.5f, buffer.ReadF()); // y
		Assert.Equal(300.5f, buffer.ReadF()); // z
		Assert.Equal(5, buffer.ReadC()); // classId
		Assert.Equal(1, buffer.ReadC()); // genderId
		Assert.Equal(50, buffer.ReadC()); // level
		Assert.Equal(2, buffer.ReadC()); // status
		// Name field is fixed 27 chars + null terminator = 28 * 2 bytes; verify first char.
		Assert.Equal('T', (char)buffer.ReadH());
	}

	[Fact]
	public void SmPlayerSearch_OpcodeIs211()
	{
		// Java parity: ServerPacketsOpcodes addPacketOpcode(211, SM_PLAYER_SEARCH.class).
		Assert.Equal(211, SmPlayerSearch.PacketOpCode);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..]; // skip 2-byte length + 2-byte opcode + 1-byte static code + 2-byte ~opcode
	}
}
