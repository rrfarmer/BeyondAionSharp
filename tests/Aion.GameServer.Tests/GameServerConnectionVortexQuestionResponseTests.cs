using System.Net;
using System.Net.Sockets;
using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class GameServerConnectionVortexQuestionResponseTests
{
	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderInviteConsumesRegistryAndReportsMetadata()
	{
		var reports = new List<VortexDefenderInvitationResponseConsumptionReport>();
		var responder = CreatePlayer();
		responder.TeamMembership = PlayerTeamMembership.Group;
		var pendingRequest = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: responder.ObjectId,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Open,
			ExistingDefenderObjectIds: [1001]);
		Assert.True(responder.ResponseRequester.PutRequest(
			SmQuestionWindow.VortexDefenderInvitation,
			new QuestionResponseRequest(
				responder.ObjectId,
				QuestionResponseRequestKind.VortexDefenderInvitation,
				pendingRequest)));
		await using var pair = await TestConnectionPair.CreateAsync(vortexReportObserver: reports.Add);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 1));

		Assert.Equal(0, responder.ResponseRequester.Count);
		Assert.Equal(PlayerTeamMembership.Group, responder.TeamMembership);
		Assert.Empty(pair.SentPackets);
		var report = Assert.Single(reports);
		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.Accepted, report.Status);
		Assert.True(report.RequestRemovedByRegistry);
		Assert.True(report.WouldInvokeHandler);
		Assert.True(report.HasVortexPayload);
		Assert.True(report.HasDispatchPlan);
		Assert.True(report.DispatchPlan?.HasAcceptancePlan);
		Assert.False(report.ShouldMutateLiveGroup);
		Assert.False(report.ShouldMutateLiveAlliance);
		Assert.False(report.ShouldMutateLiveDefenders);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderInviteReportsMissingWithoutPackets()
	{
		var reports = new List<VortexDefenderInvitationResponseConsumptionReport>();
		var responder = CreatePlayer();
		await using var pair = await TestConnectionPair.CreateAsync(vortexReportObserver: reports.Add);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 0));

		Assert.Equal(0, responder.ResponseRequester.Count);
		Assert.Empty(pair.SentPackets);
		var report = Assert.Single(reports);
		Assert.Equal(VortexDefenderInvitationResponseConsumptionReportStatus.RequestMissing, report.Status);
		Assert.False(report.RequestRemovedByRegistry);
		Assert.False(report.WouldInvokeHandler);
		Assert.False(report.HasDispatchPlan);
		Assert.False(report.ShouldMutateLiveGroup);
		Assert.False(report.ShouldMutateLiveAlliance);
		Assert.False(report.ShouldMutateLiveDefenders);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderAcceptanceObserverReportsTransitionAndParticipantForAccepted()
	{
		var acceptanceReports = new List<VortexDefenderAcceptanceRuntimeObserverReport>();
		var responder = CreatePlayer();
		responder.TeamMembership = PlayerTeamMembership.Group;
		var existingDefenderObjectId = 1001;
		var pendingRequest = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: responder.ObjectId,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Missing,
			ExistingDefenderObjectIds: [existingDefenderObjectId]);
		Assert.True(responder.ResponseRequester.PutRequest(
			SmQuestionWindow.VortexDefenderInvitation,
			new QuestionResponseRequest(
				responder.ObjectId,
				QuestionResponseRequestKind.VortexDefenderInvitation,
				pendingRequest)));
		await using var pair = await TestConnectionPair.CreateAsync(vortexAcceptanceObserver: acceptanceReports.Add);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 7));

		Assert.Equal(0, responder.ResponseRequester.Count);
		Assert.Empty(pair.SentPackets);
		var report = Assert.Single(acceptanceReports);
		Assert.True(report.Accepted);
		Assert.False(report.Denied);
		Assert.Equal(0, report.LocationId);
		Assert.Equal(1004, report.ResponderObjectId);
		Assert.Equal(VortexDefenderAcceptanceParticipantRuntimeReportStatus.ParticipantWouldBeRecorded, report.ParticipantStatus);
		Assert.True(report.WouldRecordParticipant);
		Assert.True(report.WouldPutParticipant);
		Assert.Equal([existingDefenderObjectId], report.DefenderObjectIdsBefore);
		Assert.Equal([existingDefenderObjectId, 1004], report.DefenderObjectIdsAfter);
		Assert.Equal(PlayerTeamMembership.Group, responder.TeamMembership);
		Assert.False(report.ShouldMutateLiveGroup);
		Assert.False(report.ShouldMutateLiveAlliance);
		Assert.False(report.ShouldMutateLiveDefenders);
		Assert.False(report.ShouldSendLivePacket);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderAcceptanceObserverUsesResolvedDefenderInputsWhenRuntimeAndLookupAvailable()
	{
		const int locationId = 1;
		const int existingDefenderId = 1001;
		var acceptanceReports = new List<VortexDefenderAcceptanceRuntimeObserverReport>();
		var responder = CreatePlayer();
		var existingDefender = new Player
		{
			ObjectId = existingDefenderId,
			Name = "ExistingDefender",
			Race = "ELYOS",
			PlayerClass = "WARRIOR",
			Gender = "MALE",
			IsOnline = true,
			TeamMembership = PlayerTeamMembership.Alliance,
			Position = new WorldPosition(220050000, 1, 2, 3, 0),
		};
		var runtime = new VortexInvasionRuntime();
		var fakeLocation = new VortexLocationSummary(
			locationId, DefendersRace: "ELYOS", InvadersRace: "ASMODIANS",
			HomePoint: new WorldPosition(120080000, 559.4f, 207.8f, 93.5f, 0),
			ResurrectionPoint: new WorldPosition(220050000, 2237.3f, 2801.5f, 73.3f, 0),
			StartPoint: new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0));
		runtime.StartInvasion(fakeLocation);
		// existingDefender is already in the invasion; responder is the new accepter not yet in runtime.
		Assert.True(runtime.AddDefender(locationId, existingDefender));
		// Both defenders added to runtime; FindDefenderLocationId resolves locationId for the responder.
		Assert.True(runtime.AddDefender(locationId, responder));
		var pendingRequest = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: responder.ObjectId,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Missing,
			ExistingDefenderObjectIds: [existingDefenderId]);
		Assert.True(responder.ResponseRequester.PutRequest(
			SmQuestionWindow.VortexDefenderInvitation,
			new QuestionResponseRequest(
				responder.ObjectId,
				QuestionResponseRequestKind.VortexDefenderInvitation,
				pendingRequest)));
		var players = new Dictionary<int, Player>
		{
			[existingDefenderId] = existingDefender,
			[responder.ObjectId] = responder,
		};
		await using var pair = await TestConnectionPair.CreateAsync(
			vortexAcceptanceObserver: acceptanceReports.Add,
			vortexInvasionRuntime: runtime,
			worldPlayerLookup: id => players.GetValueOrDefault(id));

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 7));

		var report = Assert.Single(acceptanceReports);
		Assert.True(report.Accepted);
		Assert.Equal(locationId, report.LocationId);
		// The resolver sees both existing defenders (1001, 1004) from the runtime snapshot,
		// so it derives existingDefenders=[1001, 1004] and Open alliance for 2 defenders.
		// Since the responder (1004) is already in existingDefenders, the participant report
		// marks AlreadyParticipant and keeps before/after equal — the resolver inputs reached the observer.
		Assert.Equal(VortexDefenderAcceptanceParticipantRuntimeReportStatus.AlreadyParticipant, report.ParticipantStatus);
		Assert.False(report.WouldRecordParticipant);
		// before/after reflect the resolver-derived defender ids (not the pending request list)
		Assert.True(report.DefenderObjectIdsBefore.Contains(existingDefenderId));
		Assert.True(report.DefenderObjectIdsBefore.Contains(responder.ObjectId));
		Assert.False(report.ShouldMutateLiveDefenders);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderAcceptanceObserverUsesRuntimeLocationIdWhenAvailable()
	{
		const int expectedLocationId = 1;
		var acceptanceReports = new List<VortexDefenderAcceptanceRuntimeObserverReport>();
		var responder = CreatePlayer();
		var runtime = new VortexInvasionRuntime();
		// Seed runtime so FindDefenderLocationId returns expectedLocationId for this player.
		var fakeLocation = new VortexLocationSummary(
			expectedLocationId, DefendersRace: "ELYOS", InvadersRace: "ASMODIANS",
			HomePoint: new WorldPosition(120080000, 559.4f, 207.8f, 93.5f, 0),
			ResurrectionPoint: new WorldPosition(220050000, 2237.3f, 2801.5f, 73.3f, 0),
			StartPoint: new WorldPosition(220050000, 2242.0f, 2797.0f, 75.4f, 0));
		runtime.StartInvasion(fakeLocation);
		Assert.True(runtime.AddDefender(expectedLocationId, responder));
		var pendingRequest = new PendingVortexDefenderInvitationRequest(
			RequesterObjectId: responder.ObjectId,
			QuestionId: SmQuestionWindow.VortexDefenderInvitation,
			DefenderAlliance: VortexDefenderAllianceSnapshot.Open,
			ExistingDefenderObjectIds: []);
		Assert.True(responder.ResponseRequester.PutRequest(
			SmQuestionWindow.VortexDefenderInvitation,
			new QuestionResponseRequest(
				responder.ObjectId,
				QuestionResponseRequestKind.VortexDefenderInvitation,
				pendingRequest)));
		await using var pair = await TestConnectionPair.CreateAsync(
			vortexAcceptanceObserver: acceptanceReports.Add,
			vortexInvasionRuntime: runtime);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 1));

		var report = Assert.Single(acceptanceReports);
		Assert.Equal(expectedLocationId, report.LocationId);
		Assert.True(report.Accepted);
		Assert.False(report.ShouldMutateLiveDefenders);
	}

	[Fact]
	public async Task HandleQuestionResponseAsync_VortexDefenderAcceptanceObserverReportsNoMutationForMissingRequest()
	{
		var acceptanceReports = new List<VortexDefenderAcceptanceRuntimeObserverReport>();
		var responder = CreatePlayer();
		await using var pair = await TestConnectionPair.CreateAsync(vortexAcceptanceObserver: acceptanceReports.Add);

		await pair.Connection.HandleQuestionResponseAsync(
			responder,
			CreateQuestionResponse(SmQuestionWindow.VortexDefenderInvitation, response: 1));

		Assert.Equal(0, responder.ResponseRequester.Count);
		Assert.Empty(pair.SentPackets);
		var report = Assert.Single(acceptanceReports);
		// Missing request → RequestMissing status → neither Accepted nor Denied
		Assert.False(report.Accepted);
		Assert.False(report.Denied);
		Assert.Equal(VortexDefenderAcceptanceParticipantRuntimeReportStatus.NoParticipantMutation, report.ParticipantStatus);
		Assert.False(report.WouldRecordParticipant);
		Assert.False(report.ShouldMutateLiveGroup);
		Assert.False(report.ShouldMutateLiveAlliance);
		Assert.False(report.ShouldMutateLiveDefenders);
		Assert.False(report.ShouldSendLivePacket);
	}

	private static Player CreatePlayer()
	{
		return new Player
		{
			ObjectId = 1004,
			Name = "Responder",
			Race = "ELYOS",
			PlayerClass = "WARRIOR",
			Gender = "MALE",
			IsOnline = true,
			Position = new WorldPosition(210010000, 1, 2, 3, 0)
		};
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

	private sealed class TestConnectionPair : IAsyncDisposable
	{
		private readonly TcpClient _client;
		private readonly GameServerConnection _connection;

		private TestConnectionPair(TcpClient client, GameServerConnection connection, List<GameServerPacket> sentPackets)
		{
			_client = client;
			_connection = connection;
			SentPackets = sentPackets;
		}

		public GameServerConnection Connection => _connection;

		public List<GameServerPacket> SentPackets { get; }

		public static async Task<TestConnectionPair> CreateAsync(
			Action<VortexDefenderInvitationResponseConsumptionReport>? vortexReportObserver = null,
			Action<VortexDefenderAcceptanceRuntimeObserverReport>? vortexAcceptanceObserver = null,
			VortexInvasionRuntime? vortexInvasionRuntime = null,
			Func<int, Player?>? worldPlayerLookup = null,
			VortexLocationService? defenderAcceptanceVortexLocationService = null)
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
				var sentPackets = new List<GameServerPacket>();
				var connection = new GameServerConnection(
					NullLogger.Instance,
					serverClient,
					"vortex-question-response-test",
					new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
					options: new GameServerOptions(),
					sentPacketObserver: sentPackets.Add,
					vortexDefenderInvitationResponseObserver: vortexReportObserver,
					vortexDefenderAcceptanceObserver: vortexAcceptanceObserver,
					vortexInvasionRuntime: vortexInvasionRuntime,
					worldPlayerLookup: worldPlayerLookup,
					defenderAcceptanceVortexLocationService: defenderAcceptanceVortexLocationService,
					crypt: crypt);
				return new TestConnectionPair(client, connection, sentPackets);
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
}
