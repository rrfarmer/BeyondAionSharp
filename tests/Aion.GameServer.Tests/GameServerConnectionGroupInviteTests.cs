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
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionGroupInviteTests
{
	[Fact]
	public async Task HandleInviteToGroupAsync_GroupInviteSendsInviterMessageAndQuestion()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Invited"));

		Assert.Equal(GroupInviteRequestStatus.Requested, result?.Status);
		Assert.Equal(1, invited.ResponseRequester.Count);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1300173, "Invited");
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1002, sent.PlayerObjectId);
		Assert.Equal(SmQuestionWindow.PartyInvite, Assert.IsType<SmQuestionWindow>(sent.Packet).Code);
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_NoSuchUserSendsJavaFailure()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Missing"));

		Assert.Null(result);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1300627, "Missing");
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_DeniedGroupRequestsSendsRejectedInvite()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		invited.Settings.Deny = PlayerSettings.DenyGroupRequests;
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, new PlayerGroupRuntime(), sent => localPackets.Add(sent));

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 0, "Invited"));

		Assert.Null(result);
		var local = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(local), 1390116, "Invited");
		Assert.Empty(registry.SentPackets);
		Assert.Equal(0, invited.ResponseRequester.Count);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_GroupInviteDenyClearsRequestAndRejectsInviter()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerGroupInviteRequestService().SendInvite(inviter, invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.PartyInvite, response: 0));

		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.Null(inviter.CurrentGroupSnapshot);
		Assert.Null(invited.CurrentGroupSnapshot);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent.Packet), 1300161, "Invited");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_GroupInviteAcceptCreatesGroupAndFansOutEnteredPackets()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerGroupInviteRequestService().SendInvite(inviter, invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, idFactory: new IDFactory());

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.PartyInvite, response: 1));

		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.NotNull(inviter.CurrentGroupSnapshot);
		Assert.Same(inviter.CurrentGroupSnapshot, invited.CurrentGroupSnapshot);
		Assert.Equal([1001, 1002], invited.CurrentGroupSnapshot?.MemberObjectIds);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1002 && send.Packet is SmGroupInfo);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1001 && send.Packet is SmSystemMessage);
		Assert.Contains(registry.SentPackets, send => send.Packet is SmGroupMemberInfo);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_GroupInviteAcceptUsesInjectedFindGroupRecorder()
	{
		var registry = new CapturingConnectionRegistry();
		var findGroupService = new FindGroupRecruitmentPlanService();
		var recorder = new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 200, serverId: 7);
		var groups = new PlayerGroupRuntime(findGroupService, serverId: 7);
		var inviteService = new PlayerGroupInviteRequestService(recorder);
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		findGroupService.AddRecruitment(inviter, "Need one", groupType: 3, nowEpochSeconds: 100);
		inviteService.SendInvite(inviter, invited);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			groups,
			idFactory: new IDFactory(),
			playerGroupInviteRequestService: inviteService);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.PartyInvite, response: 1));

		var groupSnapshot = invited.CurrentGroupSnapshot;
		Assert.NotNull(groupSnapshot);
		var teamId = groupSnapshot.TeamId;
		var recruitments = findGroupService.ShowRecruitments(inviter.Race, nowEpochSeconds: 201).Recruitments;
		var recruitment = Assert.Single(recruitments);
		Assert.Equal(teamId, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("Need one", recruitment.Message);
		Assert.Equal(0, findGroupService.ShowRecruitments(inviter.Race, nowEpochSeconds: 202)
			.Recruitments.Count(recruitment => recruitment.ObjectId == inviter.ObjectId));
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_LeagueInviteTypeTargetsAllianceLeaderAndRegistersQuestion()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var selected = CreatePlayer(1002, "Selected");
		var invitedLeader = CreatePlayer(1003, "InvitedLeader");
		alliances.CreateAlliance(88001, inviter);
		alliances.CreateAlliance(88002, invitedLeader);
		alliances.AddMember(88002, selected);
		registry.OnlinePlayers.AddRange([inviter, selected, invitedLeader]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, alliances: alliances, leagues: leagues);

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 28, "Selected"));

		Assert.Null(result);
		Assert.Null(selected.PendingLeagueInviteRequest);
		Assert.NotNull(invitedLeader.PendingLeagueInviteRequest);
		Assert.Equal(1, invitedLeader.ResponseRequester.Count);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1400559, "Selected", "InvitedLeader");
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1400558, "InvitedLeader", "2");
			},
			send =>
			{
				Assert.Equal(1003, send.PlayerObjectId);
				Assert.Equal(SmQuestionWindow.UnionInviteMe, Assert.IsType<SmQuestionWindow>(send.Packet).Code);
			});
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_LeagueInviteTypeRejectsPlayerWithoutAlliance()
	{
		var registry = new CapturingConnectionRegistry();
		var localPackets = new List<GameServerPacket>();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var leagues = new PlayerLeagueRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		alliances.CreateAlliance(88001, inviter);
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			groups,
			sent => localPackets.Add(sent),
			alliances: alliances,
			leagues: leagues);

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 28, "Invited"));

		Assert.Null(result);
		Assert.Equal(0, invited.ResponseRequester.Count);
		var sent = Assert.Single(localPackets);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent), 1400567, "Invited");
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_AllianceInviteTypeRegistersQuestion()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, alliances: alliances);

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 12, "Invited"));

		Assert.Null(result);
		Assert.NotNull(invited.PendingAllianceInviteRequest);
		Assert.Equal(1, invited.ResponseRequester.Count);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1301017, "Invited");
			},
			send =>
			{
				Assert.Equal(1002, send.PlayerObjectId);
				Assert.Equal(SmQuestionWindow.AllianceInvite, Assert.IsType<SmQuestionWindow>(send.Packet).Code);
			});
	}

	[Fact]
	public async Task HandleInviteToGroupAsync_AllianceInviteTypeRedirectsSelectedGroupMemberToLeader()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var selected = CreatePlayer(1002, "Selected");
		var invitedLeader = CreatePlayer(1003, "InvitedLeader");
		groups.CreateOrUpdateGroup(77001, [invitedLeader, selected]);
		registry.OnlinePlayers.AddRange([inviter, selected, invitedLeader]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, alliances: alliances);

		var result = await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 12, "Selected"));

		Assert.Null(result);
		Assert.Null(selected.PendingAllianceInviteRequest);
		Assert.NotNull(invitedLeader.PendingAllianceInviteRequest);
		Assert.Equal(1, invitedLeader.ResponseRequester.Count);
		Assert.Collection(
			registry.SentPackets,
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1300969, "Selected", "InvitedLeader");
			},
			send =>
			{
				Assert.Equal(1001, send.PlayerObjectId);
				AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(send.Packet), 1300968, "InvitedLeader", "2");
			},
			send =>
			{
				Assert.Equal(1003, send.PlayerObjectId);
				Assert.Equal(SmQuestionWindow.AllianceInvite, Assert.IsType<SmQuestionWindow>(send.Packet).Code);
			});
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_AllianceInviteDenyClearsRequestAndRejectsInviter()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerAllianceInviteRequestService().SendInvite(inviter, invited, groups, alliances, objectId => objectId == inviter.ObjectId ? inviter : invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, alliances: alliances);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.AllianceInvite, response: 0));

		Assert.Null(invited.PendingAllianceInviteRequest);
		Assert.Equal(0, invited.ResponseRequester.Count);
		var sent = Assert.Single(registry.SentPackets);
		Assert.Equal(1001, sent.PlayerObjectId);
		AssertSystemMessagePayload(Assert.IsType<SmSystemMessage>(sent.Packet), 1300190, "Invited");
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_AllianceInviteAcceptCreatesAllianceForSoloPlayers()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		new PlayerAllianceInviteRequestService().SendInvite(inviter, invited, groups, alliances, objectId => objectId == inviter.ObjectId ? inviter : invited);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, idFactory: new IDFactory(), alliances: alliances);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.AllianceInvite, response: 1));

		Assert.Null(invited.PendingAllianceInviteRequest);
		Assert.Equal(0, invited.ResponseRequester.Count);
		Assert.NotNull(inviter.CurrentAllianceSnapshot);
		Assert.Same(inviter.CurrentAllianceSnapshot, invited.CurrentAllianceSnapshot);
		Assert.Equal([1001, 1002], invited.CurrentAllianceSnapshot?.MemberObjectIds);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1002 && send.Packet is SmAllianceInfo);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1001 && send.Packet is SmSystemMessage);
		Assert.Contains(registry.SentPackets, send => send.Packet is SmAllianceMemberInfo);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_AllianceInviteAcceptUsesInjectedFindGroupRecorder()
	{
		var registry = new CapturingConnectionRegistry();
		var findGroupService = new FindGroupRecruitmentPlanService();
		var recorder = new FindGroupJoinedTeamLifecycleRecorder(findGroupService, () => 300, serverId: 7);
		var groups = new PlayerGroupRuntime(findGroupService, serverId: 7);
		var alliances = new PlayerAllianceRuntime(findGroupService, serverId: 7);
		var inviteService = new PlayerAllianceInviteRequestService(recorder);
		var inviter = CreatePlayer(1001, "Inviter");
		var invited = CreatePlayer(1002, "Invited");
		registry.OnlinePlayers.AddRange([inviter, invited]);
		findGroupService.AddRecruitment(inviter, "Force forming", groupType: 12, nowEpochSeconds: 250);
		inviteService.SendInvite(inviter, invited, groups, alliances, objectId => objectId == inviter.ObjectId ? inviter : invited);
		await using var pair = await TestConnectionPair.CreateAsync(
			registry,
			groups,
			idFactory: new IDFactory(),
			alliances: alliances,
			playerAllianceInviteRequestService: inviteService);

		await pair.Connection.HandleQuestionResponseAsync(invited, CreateQuestionResponse(SmQuestionWindow.AllianceInvite, response: 1));

		var allianceSnapshot = invited.CurrentAllianceSnapshot;
		Assert.NotNull(allianceSnapshot);
		var allianceId = allianceSnapshot.AllianceId;
		var recruitments = findGroupService.ShowRecruitments(inviter.Race, nowEpochSeconds: 301).Recruitments;
		var recruitment = Assert.Single(recruitments);
		Assert.Equal(allianceId, recruitment.ObjectId);
		Assert.False(recruitment.IsSoloPlayer);
		Assert.Equal("Force forming", recruitment.Message);
		Assert.Equal(0, findGroupService.ShowRecruitments(inviter.Race, nowEpochSeconds: 302)
			.Recruitments.Count(recruitment => recruitment.ObjectId == inviter.ObjectId));
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_AllianceInviteAcceptMergesRequesterAndInvitedGroupsLikeJavaCollectPlayersToAdd()
	{
		var registry = new CapturingConnectionRegistry();
		var groups = new PlayerGroupRuntime();
		var alliances = new PlayerAllianceRuntime();
		var inviter = CreatePlayer(1001, "Inviter");
		var requesterMember = CreatePlayer(1002, "RequesterMember");
		var invitedLeader = CreatePlayer(1003, "InvitedLeader");
		var selected = CreatePlayer(1004, "Selected");
		groups.CreateOrUpdateGroup(77001, [inviter, requesterMember]);
		groups.CreateOrUpdateGroup(77002, [invitedLeader, selected]);
		registry.OnlinePlayers.AddRange([inviter, requesterMember, invitedLeader, selected]);
		await using var pair = await TestConnectionPair.CreateAsync(registry, groups, idFactory: new IDFactory(), alliances: alliances);

		await pair.Connection.HandleInviteToGroupAsync(inviter, CreateInvitePacket(inviteType: 12, "Selected"));
		registry.SentPackets.Clear();
		await pair.Connection.HandleQuestionResponseAsync(invitedLeader, CreateQuestionResponse(SmQuestionWindow.AllianceInvite, response: 1));

		Assert.Null(inviter.CurrentGroupSnapshot);
		Assert.Null(requesterMember.CurrentGroupSnapshot);
		Assert.Null(invitedLeader.CurrentGroupSnapshot);
		Assert.Null(selected.CurrentGroupSnapshot);
		Assert.NotNull(inviter.CurrentAllianceSnapshot);
		Assert.Same(inviter.CurrentAllianceSnapshot, requesterMember.CurrentAllianceSnapshot);
		Assert.Same(inviter.CurrentAllianceSnapshot, invitedLeader.CurrentAllianceSnapshot);
		Assert.Same(inviter.CurrentAllianceSnapshot, selected.CurrentAllianceSnapshot);
		Assert.Equal([1001, 1002, 1003, 1004], inviter.CurrentAllianceSnapshot?.MemberObjectIds);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1002 && send.Packet is SmAllianceInfo);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1003 && send.Packet is SmAllianceInfo);
		Assert.Contains(registry.SentPackets, send => send.PlayerObjectId == 1004 && send.Packet is SmAllianceMemberInfo);
	}

	private static Player CreatePlayer(int objectId, string name)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			Position = new WorldPosition(210010000, objectId, 20, 30, 0),
		};
	}

	private static CmInviteToGroup CreateInvitePacket(byte inviteType, string playerName)
	{
		return Assert.IsType<CmInviteToGroup>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(97, buffer =>
			{
				buffer.WriteC(inviteType);
				buffer.WriteS(playerName);
			}), GameConnectionState.InGame));
	}

	private static CmQuestionResponse CreateQuestionResponse(int questionId, byte response)
	{
		return Assert.IsType<CmQuestionResponse>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(50, buffer =>
			{
				buffer.WriteD(questionId);
				buffer.WriteC(response);
				buffer.WriteC(0);
				buffer.WriteH(0);
				buffer.WriteD(0);
				buffer.WriteD(0);
				buffer.WriteH(0);
			}), GameConnectionState.InGame));
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static void AssertSystemMessagePayload(
		SmSystemMessage packet,
		int expectedMessageId,
		params string[] expectedParameters)
	{
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
			PlayerGroupRuntime groups,
			Action<GameServerPacket>? sentPacketObserver = null,
			IDFactory? idFactory = null,
			PlayerAllianceRuntime? alliances = null,
			PlayerLeagueRuntime? leagues = null,
			PlayerGroupInviteRequestService? playerGroupInviteRequestService = null,
			PlayerAllianceInviteRequestService? playerAllianceInviteRequestService = null)
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
				var crypt = new GameCrypt(() => 0x01020304);
				crypt.EnableKey();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"group-invite-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					connectionRegistry: registry,
					idFactory: idFactory,
					sentPacketObserver: sentPacketObserver,
					playerGroupRuntime: groups,
					playerAllianceRuntime: alliances,
					playerLeagueRuntime: leagues,
					playerGroupInviteRequestService: playerGroupInviteRequestService,
					playerAllianceInviteRequestService: playerAllianceInviteRequestService,
					crypt: crypt);
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
		public List<Player> OnlinePlayers { get; } = [];
		public List<SentPacketRecord> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = OnlinePlayers.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in OnlinePlayers)
				action(player);
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
