using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceOfflineTimeoutDispatchServiceTests
{
	[Fact]
	public async Task DispatchNextExpiredAsync_InLeagueDisbandSkipsOrdinaryBroadcastLikeJavaTimeout()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true, worldId: 210010000);
		var timedOut = CreatePlayer(1002, "TimedOut", isOnline: false, worldId: 220010000);
		var otherLeader = CreatePlayer(2001, "OtherLeader", isOnline: true, worldId: 230010000);
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, timedOut);
		alliances.CreateAlliance(88002, otherLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		alliances.SetLeagueId(88001, 77001);
		alliances.SetLeagueId(88002, 77001);
		alliances.UpdateMemberLastOnlineTime(timedOut, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		var service = new PlayerAllianceOfflineTimeoutDispatchService(alliances, leagues, registry);

		var result = Assert.IsType<PlayerAllianceOfflineTimeoutDispatchResult>(
			await service.DispatchNextExpiredAsync(
				DateTimeOffset.FromUnixTimeMilliseconds(700_000),
				allianceRemoveTimeSeconds: 600));

		Assert.Equal(9, result.SentPacketCount);
		Assert.Equal(88001, result.TimeoutPlan.AllianceId);
		Assert.Equal(1002, result.TimeoutPlan.TimedOutPlayerObjectId);
		Assert.Equal(77001, result.TimeoutPlan.LeagueId);
		Assert.True(result.TimeoutPlan.WasInLeague);
		Assert.False(result.TimeoutPlan.LeaveWorkflowPlan.AllianceLeavePlan.WouldBroadcastLeague);
		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, timedOut.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		Assert.Null(leagues.ResolveByAllianceId(88001));
		Assert.Null(leagues.ResolveByAllianceId(88002));
		Assert.Equal([1001, 1001, 1001, 2001, 2001, 1001, 2001, 1001, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => AssertSystemMessagePayload(send, 1300203, "TimedOut"),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				expectedAllianceGroupSize: 1,
				expectedLeagueRows:
				[
					new PlayerAllianceInfoLeagueRow(0, 88001, 1, "Leader", 210010000),
					new PlayerAllianceInfoLeagueRow(1, 88002, 1, "OtherLeader", 230010000),
				]),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				230010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftHimMessageId,
				"Leader",
				expectedLeagueRows: [new PlayerAllianceInfoLeagueRow(0, 88002, 1, "OtherLeader", 230010000)]),
			send => AssertSystemMessagePayload(send, 1400588, "OtherLeader"),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftMeMessageId,
				"Leader",
				expectedLeagueId: 0,
				expectedLeagueRows: []),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				230010000,
				PlayerAllianceInfoPacketPlan.LeagueDispersedMessageId,
				expectedLeagueId: 0,
				expectedLeagueRows: []),
			send => AssertSystemMessagePayload(send, 1300201),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task DispatchNextExpiredAsync_ReturnsNullWhenNoAllianceMemberExpired()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true, worldId: 210010000);
		var member = CreatePlayer(1002, "Member", isOnline: false, worldId: 220010000);
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		alliances.UpdateMemberLastOnlineTime(member, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		var service = new PlayerAllianceOfflineTimeoutDispatchService(alliances, leagueRuntime: null, registry);

		var result = await service.DispatchNextExpiredAsync(
			DateTimeOffset.FromUnixTimeMilliseconds(699_999),
			allianceRemoveTimeSeconds: 600);

		Assert.Null(result);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task DispatchExpiredScanAsync_DrainsExpiredMembersUsingConfiguredAllianceRemoveTimeLikeJavaChecker()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true, worldId: 210010000);
		var expiredOne = CreatePlayer(1002, "ExpiredOne", isOnline: false, worldId: 220010000);
		var expiredTwo = CreatePlayer(1003, "ExpiredTwo", isOnline: false, worldId: 230010000);
		var stillWaiting = CreatePlayer(1004, "StillWaiting", isOnline: false, worldId: 240010000);
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, expiredOne);
		alliances.AddMember(88001, expiredTwo);
		alliances.AddMember(88001, stillWaiting);
		alliances.UpdateMemberLastOnlineTime(expiredOne, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		alliances.UpdateMemberLastOnlineTime(expiredTwo, DateTimeOffset.FromUnixTimeMilliseconds(110_000));
		alliances.UpdateMemberLastOnlineTime(stillWaiting, DateTimeOffset.FromUnixTimeMilliseconds(250_001));
		var service = new PlayerAllianceOfflineTimeoutDispatchService(alliances, leagueRuntime: null, registry);

		var scanResult = await service.DispatchExpiredScanAsync(
			DateTimeOffset.FromUnixTimeMilliseconds(700_000),
			allianceRemoveTimeSeconds: 590);

		Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(700_000), scanResult.ScanTime);
		Assert.Equal(590, scanResult.AllianceRemoveTimeSeconds);
		Assert.Equal(2, scanResult.TimedOutMemberCount);
		Assert.Equal(15, scanResult.SentPacketCount);
		Assert.False(scanResult.WouldRemoveAnyOffenceInvader);
		Assert.Equal([1002, 1003], scanResult.DispatchResults.Select(result => result.TimeoutPlan.TimedOutPlayerObjectId));
		Assert.Equal([1001, 1004], alliances.GetMemberObjectIds(88001));
		Assert.Equal(PlayerTeamMembership.None, expiredOne.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, expiredTwo.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Alliance, stillWaiting.TeamMembership);
		Assert.Equal(15, registry.SentPackets.Count);
		Assert.Equal(5, registry.SentPackets.Count(send => send.Packet is SmAllianceInfo));
		Assert.Equal(5, registry.SentPackets.Count(send => send.Packet is SmAllianceMemberInfo));
		Assert.Equal(5, registry.SentPackets.Count(send => send.Packet is SmSystemMessage));
	}

	private static Player CreatePlayer(
		int objectId,
		string name,
		bool isOnline,
		int worldId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = isOnline,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(worldId, 1, 2, 3, 0),
		};
	}

	private static void AssertLeagueAllianceInfoPacket(
		SentPacketRecord send,
		int expectedAllianceId,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		int expectedMessageId = 0,
		string expectedMessage = "",
		int expectedLeagueId = 77001,
		IReadOnlyList<PlayerAllianceInfoLeagueRow>? expectedLeagueRows = null,
		PlayerGroupLootRules? expectedLeagueLootRules = null,
		int expectedAllianceGroupSize = 1)
	{
		expectedLeagueLootRules ??= new PlayerGroupLootRules(
			PlayerGroupLootRuleType.FreeForAll,
			Misc: 0,
			CommonItemAbove: 0,
			SuperiorItemAbove: 2,
			HeroicItemAbove: 2,
			FabledItemAbove: 2,
			EternalItemAbove: 2,
			MythicItemAbove: 2);
		expectedLeagueRows ??= [];
		var packet = Assert.IsType<SmAllianceInfo>(send.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAllianceGroupSize, reader.ReadH());
		Assert.Equal(expectedAllianceId, reader.ReadD());
		Assert.Equal(expectedLeaderObjectId, reader.ReadD());
		Assert.Equal(expectedActivePlayerMapId, reader.ReadD());
		for (var i = 0; i < 4; i++)
			Assert.Equal(0, reader.ReadD());
		AssertLootRules(reader, PlayerGroupLootRules.Default());
		Assert.Equal(0x02, reader.ReadD());
		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(0x3F, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedLeagueId, reader.ReadD());
		for (var i = 0; i < 4; i++)
		{
			Assert.Equal(i, reader.ReadD());
			Assert.Equal(1000 + i, reader.ReadD());
		}

		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedMessage, reader.ReadS());
		if (expectedLeagueRows.Count > 0)
		{
			Assert.Equal(expectedLeagueRows.Count, reader.ReadH());
			AssertLootRules(reader, expectedLeagueLootRules);
			Assert.Equal(0x02, reader.ReadD());
			foreach (var row in expectedLeagueRows)
				AssertLeagueRow(reader, row.AlliancePosition, row.AllianceObjectId, row.MemberCount, row.CaptainName, row.CaptainWorldId);
		}
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSystemMessagePayload(
		SentPacketRecord send,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		var packet = Assert.IsType<SmSystemMessage>(send.Packet);
		Assert.Equal(expectedMessageId, packet.MessageId);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertLootRules(PacketBuffer reader, PlayerGroupLootRules expectedLootRules)
	{
		Assert.Equal((int)expectedLootRules.LootRule, reader.ReadD());
		Assert.Equal(expectedLootRules.Misc, reader.ReadD());
		Assert.Equal(expectedLootRules.CommonItemAbove, reader.ReadD());
		Assert.Equal(expectedLootRules.SuperiorItemAbove, reader.ReadD());
		Assert.Equal(expectedLootRules.HeroicItemAbove, reader.ReadD());
		Assert.Equal(expectedLootRules.FabledItemAbove, reader.ReadD());
		Assert.Equal(expectedLootRules.EternalItemAbove, reader.ReadD());
		Assert.Equal(expectedLootRules.MythicItemAbove, reader.ReadD());
	}

	private static void AssertLeagueRow(
		PacketBuffer reader,
		int expectedPosition,
		int expectedAllianceId,
		int expectedMemberCount,
		string expectedCaptainName,
		int expectedCaptainWorldId)
	{
		Assert.Equal(expectedPosition, reader.ReadD());
		Assert.Equal(expectedAllianceId, reader.ReadD());
		Assert.Equal(expectedMemberCount, reader.ReadD());
		Assert.Equal(expectedCaptainName, reader.ReadS());
		Assert.Equal(expectedCaptainWorldId, reader.ReadD());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacketRecord> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			SentPackets.Add(new SentPacketRecord(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			return Task.FromResult(0);
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			return Task.FromResult(0);
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			return Task.FromResult(false);
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			return Task.FromResult(false);
		}
	}

	private sealed record SentPacketRecord(int PlayerObjectId, GameServerPacket Packet);
}
