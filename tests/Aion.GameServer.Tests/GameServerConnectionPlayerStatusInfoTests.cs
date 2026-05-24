using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionPlayerStatusInfoTests
{
	[Fact]
	public async Task HandlePlayerStatusInfoAsync_StartReadyCheckBroadcastsJavaStatuses()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var plan = await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(PlayerAllianceReadyCheckCommand.Start, selectedObjectId: member.ObjectId));

		Assert.NotNull(plan);
		Assert.Equal(PlayerAllianceReadyCheckCommand.Start, plan.Command);
		Assert.Equal(1, plan.ReadyStatusAfter);
		Assert.Equal([1001, 1001, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceReadyCheck>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_NonReadyCommandAndMissingAllianceNoopLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var player = new Player { ObjectId = 1001, Name = "Solo" };
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime());

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			player,
			CreatePacket(PlayerAllianceReadyCheckCommand.Start, selectedObjectId: 1002)));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueAllianceMoveWithoutAllianceThrowsLikeJavaDirectPath()
	{
		var registry = new CapturingConnectionRegistry();
		var player = new Player { ObjectId = 1001, Name = "Solo" };
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				player,
				CreatePacket(commandCode: 31, selectedObjectId: 88001, allianceGroupId: 88002)));

		Assert.Equal("Player alliance should not be null", exception.Message);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_InvalidTeamCommandThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var player = new Player { ObjectId = 1001, Name = "Solo" };
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime());

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				player,
				CreatePacket(commandCode: 255, selectedObjectId: 0)));

		Assert.Equal("Invalid team command code 255", exception.Message);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueLeaveWithoutLeagueThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: 29, selectedObjectId: 0)));

		Assert.Equal("League should not be null", exception.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueLeaveLeaderReorganizesAndDisbandsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			leagueLeader,
			CreatePacket(commandCode: 29, selectedObjectId: 0)));

		Assert.Empty(leagues.GetAllianceIdsByPosition(77001));
		Assert.Null(leagues.ResolveByAllianceId(88001));
		Assert.Null(leagues.ResolveByAllianceId(88002));
		Assert.Equal([2001, 2001, 1001, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				220010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftHimMessageId,
				"LeagueLeader",
				77001,
				[new PlayerAllianceInfoLeagueRow(0, 88002, 1, "AllianceLeader", 220010000)]),
			send => AssertSystemMessagePayload(send, 1400588, "AllianceLeader"),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftMeMessageId,
				"LeagueLeader",
				expectedLeagueId: 0,
				expectedLeagueRows: []),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				220010000,
				PlayerAllianceInfoPacketPlan.LeagueDispersedMessageId,
				string.Empty,
				expectedLeagueId: 0,
				expectedLeagueRows: []));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueLeaveNonLeaderCompactsWithoutDisbandLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var leavingLeader = new Player { ObjectId = 2001, Name = "LeavingLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var remainingLeader = new Player { ObjectId = 3001, Name = "RemainingLeader", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, leavingLeader);
		alliances.CreateAlliance(88003, remainingLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		leagues.AddAlliance(77001, allianceId: 88003);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			leavingLeader,
			CreatePacket(commandCode: 29, selectedObjectId: 0)));

		Assert.Equal([88001, 88003], leagues.GetAllianceIdsByPosition(77001));
		Assert.Equal(0, leagues.GetLeaguePosition(77001, 88001));
		Assert.Null(leagues.GetLeaguePosition(77001, 88002));
		Assert.Equal(1, leagues.GetLeaguePosition(77001, 88003));
		Assert.Null(leagues.ResolveByAllianceId(88002));
		Assert.NotNull(leagues.ResolveByAllianceId(88001));
		Assert.NotNull(leagues.ResolveByAllianceId(88003));
		Assert.Equal([1001, 3001, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		var expectedRows = new[]
		{
			new PlayerAllianceInfoLeagueRow(0, 88001, 1, "LeagueLeader", 210010000),
			new PlayerAllianceInfoLeagueRow(1, 88003, 1, "RemainingLeader", 230010000),
		};
		Assert.Collection(
			registry.SentPackets,
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftHimMessageId,
				"LeavingLeader",
				77001,
				expectedRows),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88003,
				3001,
				230010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftHimMessageId,
				"LeavingLeader",
				77001,
				expectedRows),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				220010000,
				PlayerAllianceInfoPacketPlan.LeagueLeftMeMessageId,
				"LeavingLeader",
				expectedLeagueId: 0,
				expectedLeagueRows: []));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelWithoutLeagueThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: 30, selectedObjectId: 88002)));

		Assert.Equal("Player [id=1001, name=Leader] tried to execute league command without an active league alliance", exception.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelByLeaderRemovesTargetLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var expelledLeader = new Player { ObjectId = 2001, Name = "ExpelledLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var remainingLeader = new Player { ObjectId = 3001, Name = "RemainingLeader", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, expelledLeader);
		alliances.CreateAlliance(88003, remainingLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		leagues.AddAlliance(77001, allianceId: 88003);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			leagueLeader,
			CreatePacket(commandCode: 30, selectedObjectId: 88002)));

		Assert.Equal([88001, 88003], leagues.GetAllianceIdsByPosition(77001));
		Assert.Equal(0, leagues.GetLeaguePosition(77001, 88001));
		Assert.Null(leagues.GetLeaguePosition(77001, 88002));
		Assert.Equal(1, leagues.GetLeaguePosition(77001, 88003));
		Assert.Equal([1001, 3001, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		var expectedRows = new[]
		{
			new PlayerAllianceInfoLeagueRow(0, 88001, 1, "LeagueLeader", 210010000),
			new PlayerAllianceInfoLeagueRow(1, 88003, 1, "RemainingLeader", 230010000),
		};
		Assert.Collection(
			registry.SentPackets,
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueExpelMessageId,
				"ExpelledLeader",
				77001,
				expectedRows),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88003,
				3001,
				230010000,
				PlayerAllianceInfoPacketPlan.LeagueExpelMessageId,
				"ExpelledLeader",
				77001,
				expectedRows),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				220010000,
				PlayerAllianceInfoPacketPlan.LeagueExpelledMessageId,
				"LeagueLeader",
				expectedLeagueId: 0,
				expectedLeagueRows: []));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelInvalidTargetThrowsLikeJavaFindLeagueAlliance()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leagueLeader,
				CreatePacket(commandCode: 30, selectedObjectId: 88999)));

		Assert.Equal("Player [id=1001, name=LeagueLeader] tried to execute league command on invalid alliance 88999", exception.Message);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelByNonAllianceLeaderThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true };
		var leagueMember = new Player { ObjectId = 1002, Name = "LeagueMember", IsOnline = true };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.AddMember(88001, leagueMember);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leagueMember,
				CreatePacket(commandCode: 30, selectedObjectId: 88002)));

		Assert.Equal("Given player is not the league alliance leader", exception.Message);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelByNonLeagueLeaderAllianceThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true };
		var targetLeader = new Player { ObjectId = 3001, Name = "TargetLeader", IsOnline = true };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		alliances.CreateAlliance(88003, targetLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		leagues.AddAlliance(77001, allianceId: 88003);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				allianceLeader,
				CreatePacket(commandCode: 30, selectedObjectId: 88003)));

		Assert.Equal("Leader's alliance is not the league leader", exception.Message);
		Assert.Equal([88001, 88002, 88003], leagues.GetAllianceIdsByPosition(77001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueExpelLastAllianceDisbandsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var expelledLeader = new Player { ObjectId = 2001, Name = "ExpelledLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, expelledLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			leagueLeader,
			CreatePacket(commandCode: 30, selectedObjectId: 88002)));

		Assert.Empty(leagues.GetAllianceIdsByPosition(77001));
		Assert.Null(leagues.ResolveByAllianceId(88001));
		Assert.Null(leagues.ResolveByAllianceId(88002));
		Assert.Equal([1001, 2001, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueExpelMessageId,
				"ExpelledLeader",
				77001,
				[new PlayerAllianceInfoLeagueRow(0, 88001, 1, "LeagueLeader", 210010000)]),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88002,
				2001,
				220010000,
				PlayerAllianceInfoPacketPlan.LeagueExpelledMessageId,
				"LeagueLeader",
				expectedLeagueId: 0,
				expectedLeagueRows: []),
			send => AssertLeagueAllianceInfoPacket(
				send,
				88001,
				1001,
				210010000,
				PlayerAllianceInfoPacketPlan.LeagueDispersedMessageId,
				string.Empty,
				expectedLeagueId: 0,
				expectedLeagueRows: []));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueSetLeaderWithoutLeagueThrowsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: 32, selectedObjectId: 88002)));

		Assert.Equal("Player [id=1001, name=Leader] tried to execute league command without an active league alliance", exception.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueAllianceMoveWithoutLeagueThrowsLikeJavaDirectPath()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: 31, selectedObjectId: 88001, allianceGroupId: 88002)));

		Assert.Equal("League should not be null", exception.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueAllianceMoveByNonLeagueLeaderNoopsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			allianceLeader,
			CreatePacket(commandCode: 31, selectedObjectId: 88002, allianceGroupId: 88001)));

		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueAllianceMoveMissingTargetThrowsLikeJavaEventBoundary()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leagueLeader,
				CreatePacket(commandCode: 31, selectedObjectId: 88002, allianceGroupId: 88999)));

		Assert.Equal("League member should not be null: 88999", exception.Message);
		Assert.Equal([88001, 88002], leagues.GetAllianceIdsByPosition(77001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_LeagueAllianceMoveSwapsPositionsAndFansOutJavaPacketOrder()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var leagueLeader = new Player { ObjectId = 1001, Name = "LeagueLeader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var allianceLeader = new Player { ObjectId = 2001, Name = "AllianceLeader", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leagueLeader);
		alliances.CreateAlliance(88002, allianceLeader);
		leagues.CreateLeague(77001, leaderAllianceId: 88001);
		leagues.AddAlliance(77001, allianceId: 88002);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances, playerLeagueRuntime: leagues);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			leagueLeader,
			CreatePacket(commandCode: 31, selectedObjectId: 88002, allianceGroupId: 88001)));

		Assert.Equal([88002, 88001], leagues.GetAllianceIdsByPosition(77001));
		Assert.Equal(1, leagues.GetLeaguePosition(77001, 88001));
		Assert.Equal(0, leagues.GetLeaguePosition(77001, 88002));
		Assert.Equal([2001, 2001, 2001, 1001, 1001, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => AssertLeagueAllianceInfoPacket(send, 88002, 2001, 220010000),
			send => AssertSystemMessagePayload(send, 1400589, "0"),
			send => AssertSystemMessagePayload(send, 1400590, "LeagueLeader", "1"),
			send => AssertLeagueAllianceInfoPacket(send, 88001, 1001, 210010000),
			send => AssertSystemMessagePayload(send, 1400590, "AllianceLeader", "0"),
			send => AssertSystemMessagePayload(send, 1400589, "1"));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupSetLfgTogglesPlayerFlagLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var player = new Player { ObjectId = 1001, Name = "Solo" };
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime());

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			player,
			CreatePacket(commandCode: 9, selectedObjectId: 2)));
		Assert.True(player.IsLookingForGroup);

		Assert.Null(await pair.Connection.HandlePlayerStatusInfoAsync(
			player,
			CreatePacket(commandCode: 9, selectedObjectId: 1)));
		Assert.False(player.IsLookingForGroup);
		Assert.Empty(registry.SentPackets);
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(6)]
	public async Task HandlePlayerStatusInfoAsync_GroupFindMemberCommandsInvalidTargetThrowLikeJava(int commandCode)
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		groups.CreateOrUpdateGroup(99001, [leader, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: commandCode, selectedObjectId: 1999)));

		Assert.Equal("Player [id=1001, name=Leader] tried to execute team command on non-existent member with ID 1999", exception.Message);
		Assert.Equal([1001, 1002], groups.GetMemberObjectIds(99001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupSetLeaderChangesLeaderAndSendsGroupInfoThenMessages()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var target = new Player { ObjectId = 1002, Name = "Target", Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, target, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 3, selectedObjectId: target.ObjectId));

		Assert.Equal(target.ObjectId, groups.GetDescriptor(99001)?.LeaderObjectId);
		Assert.Equal([1001, 1001, 1002, 1002, 1003, 1003], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupInfo>(send.Packet),
			send => Assert.Equal(1300154, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupInfo>(send.Packet),
			send => Assert.Equal(1300155, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupInfo>(send.Packet),
			send => Assert.Equal(1300154, Assert.IsType<SmSystemMessage>(send.Packet).MessageId));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupRemoveMemberClearsMembershipAndSendsLeavePacketsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var removed = new Player { ObjectId = 1002, Name = "Removed", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, removed, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 6, selectedObjectId: removed.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Equal([1001, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal([1001, 1001, 1003, 1003, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300168, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300168, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupRemoveLeaderSelectsNextOnlineLeaderAfterLeaveFanoutLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var nextLeader = new Player { ObjectId = 1002, Name = "Next", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, nextLeader, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 6, selectedObjectId: leader.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(nextLeader.ObjectId, groups.GetDescriptor(99001)?.LeaderObjectId);
		Assert.Equal([1002, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal([1002, 1002, 1003, 1003, 1002, 1002, 1003, 1003, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300168, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300168, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupInfo>(send.Packet),
			send => Assert.Equal(1300155, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupInfo>(send.Packet),
			send => Assert.Equal(1300154, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupRemoveTwoMemberGroupDisbandsRemainingMemberLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var removed = new Player { ObjectId = 1002, Name = "Removed", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, removed]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 6, selectedObjectId: removed.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Empty(groups.GetMemberObjectIds(99001));
		Assert.Equal([1001, 1001, 1001, 1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300168, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300167, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupRemoveTwoMemberGroupSkipsOfflineRemainingDisbandPacketsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		registry.UnavailablePlayerObjectIds.Add(1001);
		var groups = new PlayerGroupRuntime();
		var offlineLeader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = false, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var removed = new Player { ObjectId = 1002, Name = "Removed", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		groups.CreateOrUpdateGroup(99001, [offlineLeader, removed]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			removed,
			CreatePacket(commandCode: 6, selectedObjectId: removed.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, offlineLeader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Empty(groups.GetMemberObjectIds(99001));
		var send = Assert.Single(registry.SentPackets);
		Assert.Equal(1002, send.PlayerObjectId);
		Assert.IsType<SmLeaveGroupMember>(send.Packet);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupBanFailureBranchesSendJavaMessages()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		var target = new Player { ObjectId = 1003, Name = "Target", IsOnline = true };
		groups.CreateOrUpdateGroup(99001, [leader, member, target]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 2, selectedObjectId: leader.ObjectId));
		await pair.Connection.HandlePlayerStatusInfoAsync(
			member,
			CreatePacket(commandCode: 2, selectedObjectId: target.ObjectId));

		var autoGroups = new PlayerGroupRuntime();
		var autoLeader = new Player { ObjectId = 2001, Name = "AutoLeader", IsOnline = true };
		var autoTarget = new Player { ObjectId = 2002, Name = "AutoTarget", IsOnline = true };
		autoGroups.CreateOrUpdateGroup(99002, [autoLeader, autoTarget], PlayerGroupType.AutoGroup);
		await using var autoPair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), autoGroups);
		await autoPair.Connection.HandlePlayerStatusInfoAsync(
			autoLeader,
			CreatePacket(commandCode: 2, selectedObjectId: autoTarget.ObjectId));

		Assert.Equal([1001, 1002, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1400705, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1301009, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1400749, Assert.IsType<SmSystemMessage>(send.Packet).MessageId));
		Assert.Equal([1001, 1002, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal([2001, 2002], autoGroups.GetMemberObjectIds(99002));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupBanMemberSendsBanFanoutAndBanishedMessageLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, banned, member]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 2, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.Equal([1001, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal([1001, 1001, 1003, 1003, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300177, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300177, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300166, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupBanTwoMemberGroupDisbandsBeforeBanishedMessageLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, banned]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 2, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.Empty(groups.GetMemberObjectIds(99001));
		Assert.Equal([1001, 1001, 1001, 1001, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300177, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300167, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet),
			send => Assert.Equal(1300166, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupBanOfflineMemberSkipsBannedPlayerPacketsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		registry.UnavailablePlayerObjectIds.Add(1002);
		var groups = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = false, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		groups.CreateOrUpdateGroup(99001, [leader, banned]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 2, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.Empty(groups.GetMemberObjectIds(99001));
		Assert.Equal([1001, 1001, 1001, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.Equal(1300177, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300167, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceChangeGroupSendsJavaServiceFailureMessages()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader" };
		var member = new Player { ObjectId = 1002, Name = "Member" };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var outsider = new Player { ObjectId = 2001, Name = "Outsider" };
		await pair.Connection.HandlePlayerStatusInfoAsync(
			outsider,
			CreatePacket(commandCode: 27, selectedObjectId: outsider.ObjectId, allianceGroupId: 1001));
		await pair.Connection.HandlePlayerStatusInfoAsync(
			member,
			CreatePacket(commandCode: 27, selectedObjectId: member.ObjectId, allianceGroupId: 1001));

		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(2001, send.PlayerObjectId);
				Assert.Equal(1301015, Assert.IsType<SmSystemMessage>(send.Packet).MessageId);
			},
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				Assert.Equal(1300976, Assert.IsType<SmSystemMessage>(send.Packet).MessageId);
			});
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceChangeGroupMovesMemberAndBroadcastsMemberInfo()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader" };
		var moved = new Player { ObjectId = 1002, Name = "Moved" };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, moved);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 27, selectedObjectId: moved.ObjectId, allianceGroupId: 1001));

		Assert.Equal([1001], alliances.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Equal([1002], alliances.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Equal([1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceMemberInfo>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceChangeGroupMissingFirstMemberNoopsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 27, selectedObjectId: 1999, allianceGroupId: 1001));

		Assert.Equal([1001, 1002], alliances.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(alliances.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceChangeGroupMissingSecondMemberNoopsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var first = new Player { ObjectId = 1002, Name = "First", IsOnline = true };
		var second = new Player { ObjectId = 1003, Name = "Second", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, first);
		alliances.AddMember(88001, second);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 27, selectedObjectId: first.ObjectId, allianceGroupId: 0, secondObjectId: 1999));

		Assert.Equal([1001, 1002, 1003], alliances.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceChangeGroupInvalidTargetGroupRemovesOldGroupBeforeThrowLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var moved = new Player { ObjectId = 1002, Name = "Moved", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, moved);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: 27, selectedObjectId: moved.ObjectId, allianceGroupId: 1999)));

		Assert.Equal("No such alliance group 1999", ex.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001], alliances.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Empty(alliances.GetMemberObjectIdsByGroupId(88001, 1999));
		var movedMember = alliances.GetMember(88001, moved.ObjectId);
		Assert.NotNull(movedMember);
		Assert.Equal(88001, movedMember.AllianceId);
		Assert.Equal(0, movedMember.AllianceGroupId);
		Assert.Equal(PlayerTeamMembership.Alliance, moved.TeamMembership);
		Assert.Equal(88001, moved.CurrentTeamId);
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_GroupMentoringCommandsToggleMentorAndSendGroupPackets()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var mentor = new Player { ObjectId = 1001, Name = "Mentor", Level = 30 };
		var mentee = new Player { ObjectId = 1002, Name = "Mentee", Level = 10 };
		groups.CreateOrUpdateGroup(99001, [mentor, mentee]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerAllianceRuntime(), groups);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			mentor,
			CreatePacket(commandCode: 10, selectedObjectId: 0));

		Assert.True(mentor.IsMentor);
		Assert.Equal([1001, 1002, 1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1400762, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1400763, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet));

		registry.SentPackets.Clear();
		await pair.Connection.HandlePlayerStatusInfoAsync(
			mentor,
			CreatePacket(commandCode: 11, selectedObjectId: 0));

		Assert.False(mentor.IsMentor);
		Assert.Equal([1001, 1002, 1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1400764, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1400765, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceViceCaptainCommandsMutateRolesAndBroadcastInfo()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var target = new Player { ObjectId = 1002, Name = "Target", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, target);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 25, selectedObjectId: target.ObjectId));

		Assert.True(alliances.IsViceCaptain(88001, target.ObjectId));
		Assert.Equal([1001, 1002, 1003], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceInfo>(send.Packet));

		registry.SentPackets.Clear();
		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 26, selectedObjectId: target.ObjectId));

		Assert.False(alliances.IsViceCaptain(88001, target.ObjectId));
		Assert.Equal([1001, 1002, 1003], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.All(registry.SentPackets, send => Assert.IsType<SmAllianceInfo>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceViceCaptainPromoteLimitSendsJavaLeaderMessage()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var vice1 = new Player { ObjectId = 1002, Name = "Vice1", IsOnline = true };
		var vice2 = new Player { ObjectId = 1003, Name = "Vice2", IsOnline = true };
		var vice3 = new Player { ObjectId = 1004, Name = "Vice3", IsOnline = true };
		var vice4 = new Player { ObjectId = 1005, Name = "Vice4", IsOnline = true };
		var target = new Player { ObjectId = 1006, Name = "Target", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		foreach (var player in new[] { vice1, vice2, vice3, vice4, target })
			alliances.AddMember(88001, player);
		alliances.SetViceCaptains(88001, [vice1.ObjectId, vice2.ObjectId, vice3.ObjectId, vice4.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 25, selectedObjectId: target.ObjectId));

		Assert.False(alliances.IsViceCaptain(88001, target.ObjectId));
		var send = Assert.Single(registry.SentPackets);
		Assert.Equal(leader.ObjectId, send.PlayerObjectId);
		Assert.Equal(1301061, Assert.IsType<SmSystemMessage>(send.Packet).MessageId);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceSetCaptainChangesLeaderAndDemotesOldLeaderToViceCaptain()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var target = new Player { ObjectId = 1002, Name = "Target", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, target);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 17, selectedObjectId: target.ObjectId));

		Assert.True(alliances.IsLeader(88001, target));
		Assert.True(alliances.IsViceCaptain(88001, leader.ObjectId));
		Assert.False(alliances.IsViceCaptain(88001, target.ObjectId));
		Assert.Equal([1001, 1002, 1003, 1001, 1002, 1003, 1001, 1002, 1003], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300998, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300999, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300998, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet));
	}

	[Theory]
	[InlineData(16)]
	[InlineData(17)]
	[InlineData(25)]
	[InlineData(26)]
	public async Task HandlePlayerStatusInfoAsync_AllianceFindMemberCommandsInvalidTargetThrowLikeJava(int commandCode)
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
			pair.Connection.HandlePlayerStatusInfoAsync(
				leader,
				CreatePacket(commandCode: commandCode, selectedObjectId: 1999)));

		Assert.Equal("Player [id=1001, name=Leader] tried to execute team command on non-existent member with ID 1999", exception.Message);
		Assert.Equal([1001, 1002], alliances.GetMemberObjectIds(88001));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveNonLeaderSendsLeaveFanoutThenBaseLeaveLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var leaver = new Player { ObjectId = 1002, Name = "Leaver", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, leaver);
		alliances.AddMember(88001, member);
		alliances.SetViceCaptains(88001, [leaver.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leaver,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, leaver.TeamMembership);
		Assert.False(alliances.IsViceCaptain(88001, leaver.ObjectId));
		Assert.Equal([1001, 1003], alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1001, 1001, 1003, 1003, 1003, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveTwoMemberAllianceDisbandsBeforeLeaverBaseLeaveLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var leaver = new Player { ObjectId = 1002, Name = "Leaver", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, leaver);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leaver,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, leaver.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1001, 1001, 1001, 1001, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300201, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveTwoMemberAllianceSkipsOfflineRemainingDisbandPacketsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		registry.UnavailablePlayerObjectIds.Add(1001);
		var alliances = new PlayerAllianceRuntime();
		var offlineLeader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = false, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var leaver = new Player { ObjectId = 1002, Name = "Leaver", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, offlineLeader);
		alliances.AddMember(88001, leaver);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leaver,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, offlineLeader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, leaver.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		var send = Assert.Single(registry.SentPackets);
		Assert.Equal(1002, send.PlayerObjectId);
		Assert.IsType<SmLeaveGroupMember>(send.Packet);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveLeaderPromotesOnlineViceCaptainBeforeLeaveFanoutLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var viceCaptain = new Player { ObjectId = 1002, Name = "Vice", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, viceCaptain);
		alliances.AddMember(88001, member);
		alliances.SetViceCaptains(88001, [viceCaptain.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.True(alliances.IsLeader(88001, viceCaptain));
		Assert.False(alliances.IsViceCaptain(88001, viceCaptain.ObjectId));
		Assert.Equal([1001, 1002, 1003, 1002, 1002, 1002, 1002, 1003, 1003, 1003, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300999, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveLeaderTwoMemberAllianceDisbandsAfterFallbackLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var viceCaptain = new Player { ObjectId = 1002, Name = "Vice", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, viceCaptain);
		alliances.SetViceCaptains(88001, [viceCaptain.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, viceCaptain.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1002, 1002, 1002, 1002, 1002, 1002, 1002, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300999, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1300978, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300201, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceLeaveLeaderTwoMemberNoOnlineFallbackDisbandsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		registry.UnavailablePlayerObjectIds.Add(1002);
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var offlineMember = new Player { ObjectId = 1002, Name = "Offline", IsOnline = false, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, offlineMember);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 14, selectedObjectId: 0));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, offlineMember.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		var send = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, send.PlayerObjectId);
		Assert.IsType<SmLeaveGroupMember>(send.Packet);
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceBanFailureBranchesSendJavaMessages()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var member = new Player { ObjectId = 1002, Name = "Member", IsOnline = true };
		var target = new Player { ObjectId = 1003, Name = "Target", IsOnline = true };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, member);
		alliances.AddMember(88001, target);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 16, selectedObjectId: leader.ObjectId));
		await pair.Connection.HandlePlayerStatusInfoAsync(
			member,
			CreatePacket(commandCode: 16, selectedObjectId: target.ObjectId));

		var autoAlliances = new PlayerAllianceRuntime();
		var autoLeader = new Player { ObjectId = 2001, Name = "AutoLeader", IsOnline = true };
		var autoTarget = new Player { ObjectId = 2002, Name = "AutoTarget", IsOnline = true };
		autoAlliances.CreateAlliance(88002, autoLeader, PlayerAllianceTeamType.AutoAlliance);
		autoAlliances.AddMember(88002, autoTarget);
		await using var autoPair = await TestConnectionPair.CreateAsync(registry, autoAlliances);
		await autoPair.Connection.HandlePlayerStatusInfoAsync(
			autoLeader,
			CreatePacket(commandCode: 16, selectedObjectId: autoTarget.ObjectId));

		Assert.Equal([1001, 1002, 2001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1400706, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1301009, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.Equal(1400749, Assert.IsType<SmSystemMessage>(send.Packet).MessageId));
		Assert.Equal([1001, 1002, 1003], alliances.GetMemberObjectIds(88001));
		Assert.Equal([2001, 2002], autoAlliances.GetMemberObjectIds(88002));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceBanMemberSendsBanFanoutThenBaseLeaveLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		var member = new Player { ObjectId = 1003, Name = "Member", IsOnline = true, Position = new WorldPosition(230010000, 7, 8, 9, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, banned);
		alliances.AddMember(88001, member);
		alliances.SetViceCaptains(88001, [banned.ObjectId]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 16, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.False(alliances.IsViceCaptain(88001, banned.ObjectId));
		Assert.Equal([1001, 1003], alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1001, 1001, 1003, 1003, 1003, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1300980, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300980, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300979, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceBanTwoMemberAllianceDisbandsBeforeBannedBaseLeaveLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = true, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, banned);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 16, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1001, 1001, 1001, 1001, 1002, 1002], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1300980, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300201, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet),
			send => Assert.Equal(1300979, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	[Fact]
	public async Task HandlePlayerStatusInfoAsync_AllianceBanOfflineMemberSkipsBannedPlayerPacketsLikeJava()
	{
		var registry = new CapturingConnectionRegistry();
		registry.UnavailablePlayerObjectIds.Add(1002);
		var alliances = new PlayerAllianceRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true, Position = new WorldPosition(210010000, 1, 2, 3, 0) };
		var banned = new Player { ObjectId = 1002, Name = "Banned", IsOnline = false, Position = new WorldPosition(220010000, 4, 5, 6, 0) };
		alliances.CreateAlliance(88001, leader);
		alliances.AddMember(88001, banned);
		await using var pair = await TestConnectionPair.CreateAsync(registry, alliances);

		await pair.Connection.HandlePlayerStatusInfoAsync(
			leader,
			CreatePacket(commandCode: 16, selectedObjectId: banned.ObjectId));

		Assert.Equal(PlayerTeamMembership.None, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, banned.TeamMembership);
		Assert.Empty(alliances.GetMemberObjectIds(88001));
		Assert.Equal([1001, 1001, 1001, 1001, 1001], registry.SentPackets.Select(send => send.PlayerObjectId));
		Assert.Collection(
			registry.SentPackets,
			send => Assert.Equal(1300980, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmAllianceMemberInfo>(send.Packet),
			send => Assert.IsType<SmAllianceInfo>(send.Packet),
			send => Assert.Equal(1300201, Assert.IsType<SmSystemMessage>(send.Packet).MessageId),
			send => Assert.IsType<SmLeaveGroupMember>(send.Packet));
	}

	private static CmPlayerStatusInfo CreatePacket(
		PlayerAllianceReadyCheckCommand command,
		int selectedObjectId)
	{
		return CreatePacket((int)command, selectedObjectId);
	}

	private static void AssertLeagueAllianceInfoPacket(
		SentPacketRecord send,
		int expectedAllianceId,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		int expectedMessageId = 0,
		string expectedMessage = "",
		int expectedLeagueId = 77001,
		IReadOnlyList<PlayerAllianceInfoLeagueRow>? expectedLeagueRows = null)
	{
		expectedLeagueRows ??=
		[
			new PlayerAllianceInfoLeagueRow(0, 88002, 1, "AllianceLeader", 220010000),
			new PlayerAllianceInfoLeagueRow(1, 88001, 1, "LeagueLeader", 210010000),
		];
		var packet = Assert.IsType<SmAllianceInfo>(send.Packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedAllianceId, reader.ReadD());
		Assert.Equal(expectedLeaderObjectId, reader.ReadD());
		Assert.Equal(expectedActivePlayerMapId, reader.ReadD());
		for (var i = 0; i < 4; i++)
			Assert.Equal(0, reader.ReadD());
		AssertDefaultLootRules(reader);
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
			AssertDefaultLootRules(reader);
			Assert.Equal(0x02, reader.ReadD());
			foreach (var row in expectedLeagueRows)
				AssertLeagueRow(reader, row.AlliancePosition, row.AllianceObjectId, row.MemberCount, row.CaptainName, row.CaptainWorldId);
		}
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertDefaultLootRules(PacketBuffer reader)
	{
		Assert.Equal((int)PlayerGroupLootRuleType.RoundRobin, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
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

	private static CmPlayerStatusInfo CreatePacket(
		int commandCode,
		int selectedObjectId,
		int allianceGroupId = 0,
		int secondObjectId = 0)
	{
		using var writer = new PacketBuffer();
		writer.WriteC(commandCode);
		writer.WriteD(selectedObjectId);
		writer.WriteD(allianceGroupId);
		writer.WriteD(secondObjectId);
		var packet = new CmPlayerStatusInfo(96, new HashSet<GameConnectionState> { GameConnectionState.InGame });
		using var reader = new PacketBuffer(writer.ToArray());
		packet.ReadFrom(reader);
		return packet;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private TestConnectionPair(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			_connection = connection;
		}

		public GameServerConnection Connection => _connection;

		public static async Task<TestConnectionPair> CreateAsync(
			IGameClientConnectionRegistry registry,
			PlayerAllianceRuntime playerAllianceRuntime,
			PlayerGroupRuntime? playerGroupRuntime = null,
			PlayerLeagueRuntime? playerLeagueRuntime = null)
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			try
			{
				var endpoint = (IPEndPoint)listener.LocalEndpoint;
				var client = new TcpClient();
				var acceptTask = listener.AcceptTcpClientAsync();
				await client.ConnectAsync(endpoint.Address, endpoint.Port);
				var serverClient = await acceptTask;
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"player-status-info-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					playerGroupRuntime: playerGroupRuntime,
					playerAllianceRuntime: playerAllianceRuntime,
					playerLeagueRuntime: playerLeagueRuntime);
				return new TestConnectionPair(client, connection);
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await _connection.DisposeAsync();
			_client.Dispose();
		}
	}

	private sealed class CapturingConnectionRegistry : IGameClientConnectionRegistry
	{
		public List<SentPacketRecord> SentPackets { get; } = [];
		public HashSet<int> UnavailablePlayerObjectIds { get; } = [];

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
			if (UnavailablePlayerObjectIds.Contains(playerObjectId))
				return Task.FromResult(false);

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
