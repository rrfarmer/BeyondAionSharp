using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupOfflineTimeoutDispatchServiceTests
{
	[Fact]
	public async Task DispatchNextExpiredAsync_RemovesExpiredOfflineMemberAndSendsTimeoutFanoutLikeJavaChecker()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true);
		var expired = CreatePlayer(1002, "Expired", isOnline: false);
		var waiting = CreatePlayer(1003, "Waiting", isOnline: false);
		groups.CreateOrUpdateGroup(99001, [leader, expired, waiting]);
		groups.UpdateMemberLastOnlineTime(expired, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		groups.UpdateMemberLastOnlineTime(waiting, DateTimeOffset.FromUnixTimeMilliseconds(450_001));
		var service = new PlayerGroupOfflineTimeoutDispatchService(groups, registry);

		var result = Assert.IsType<PlayerGroupOfflineTimeoutDispatchResult>(
			await service.DispatchNextExpiredAsync(
				DateTimeOffset.FromUnixTimeMilliseconds(700_000),
				groupRemoveTimeSeconds: 250));

		Assert.Equal(4, result.SentPacketCount);
		Assert.Equal(99001, result.TimeoutPlan.TeamId);
		Assert.Equal(1002, result.TimeoutPlan.TimedOutPlayerObjectId);
		Assert.Equal(PlayerGroupLeaveReason.LeaveTimeout, result.TimeoutPlan.LeavePlan.Reason);
		Assert.Equal([1001, 1003], groups.GetMemberObjectIds(99001));
		Assert.Equal(PlayerTeamMembership.None, expired.TeamMembership);
		Assert.Collection(
			registry.SentPackets,
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => AssertTimeoutSystemMessage(send, 1001, "Expired"),
			send => Assert.IsType<SmGroupMemberInfo>(send.Packet),
			send => AssertTimeoutSystemMessage(send, 1003, "Expired"));
	}

	[Fact]
	public async Task DispatchExpiredScanAsync_DrainsExpiredMembersLikeJavaCheckerRun()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true);
		var expiredOne = CreatePlayer(1002, "ExpiredOne", isOnline: false);
		var expiredTwo = CreatePlayer(1003, "ExpiredTwo", isOnline: false);
		var waiting = CreatePlayer(1004, "Waiting", isOnline: false);
		groups.CreateOrUpdateGroup(99001, [leader, expiredOne, expiredTwo, waiting]);
		groups.UpdateMemberLastOnlineTime(expiredOne, DateTimeOffset.FromUnixTimeMilliseconds(100_000));
		groups.UpdateMemberLastOnlineTime(expiredTwo, DateTimeOffset.FromUnixTimeMilliseconds(110_000));
		groups.UpdateMemberLastOnlineTime(waiting, DateTimeOffset.FromUnixTimeMilliseconds(250_001));
		var service = new PlayerGroupOfflineTimeoutDispatchService(groups, registry);

		var scanResult = await service.DispatchExpiredScanAsync(
			DateTimeOffset.FromUnixTimeMilliseconds(700_000),
			groupRemoveTimeSeconds: 590);

		Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(700_000), scanResult.ScanTime);
		Assert.Equal(590, scanResult.GroupRemoveTimeSeconds);
		Assert.Equal(2, scanResult.TimedOutMemberCount);
		Assert.Equal([1002, 1003], scanResult.DispatchResults.Select(result => result.TimeoutPlan.TimedOutPlayerObjectId));
		Assert.Equal([1001, 1004], groups.GetMemberObjectIds(99001));
		Assert.Equal(PlayerTeamMembership.None, expiredOne.TeamMembership);
		Assert.Equal(PlayerTeamMembership.None, expiredTwo.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Group, waiting.TeamMembership);
		Assert.Equal(10, scanResult.SentPacketCount);
	}

	private static Player CreatePlayer(int objectId, string name, bool isOnline)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = isOnline,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
		};
	}

	private static void AssertTimeoutSystemMessage(SentPacketRecord send, int recipientObjectId, string expectedPlayerName)
	{
		Assert.Equal(recipientObjectId, send.PlayerObjectId);
		var packet = Assert.IsType<SmSystemMessage>(send.Packet);
		Assert.Equal(1300176, packet.MessageId);
		Assert.Equal([expectedPlayerName], packet.Parameters);
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
