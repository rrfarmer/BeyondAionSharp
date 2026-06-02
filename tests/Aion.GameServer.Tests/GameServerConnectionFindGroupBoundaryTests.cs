using System.Net;
using System.Net.Sockets;
using System.Reflection;
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

public sealed class GameServerConnectionFindGroupBoundaryTests
{
	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ComposesAdapterPlanWithoutLiveDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 100);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(0, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionZeroCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(player, "Need healer", groupType: 2, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([player]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		var executorPlan = await new FindGroupSideEffectDispatchExecutorService(registry)
			.ExecuteAsync(plan!.IntentPlan.DirectPacketIntents, plan.IntentPlan.WorldBroadcastIntents);
		foreach (var step in executorPlan.ExecutionOrder)
			trace.Add($"{step.Sequence}:{step.Kind}:{step.RecipientObjectId}:{step.PacketType}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, plan.IntentPlan.ClientActionKind);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 0", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(player.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionOneCanProduceOrderedOptInWorldBroadcastFanoutTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var otherRace = CreatePlayer(0x01020306, "OtherRace", "ASMODIANS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(recruiter, "Need healer", groupType: 2, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([recruiter, sameRace, otherRace]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, recruiter);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(1);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteC(5);
				buffer.WriteC(6);
				buffer.WriteC(7);
				buffer.WriteC(8);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		var executorPlan = await new FindGroupSideEffectDispatchExecutorService(registry)
			.ExecuteAsync(plan!.IntentPlan.DirectPacketIntents, plan.IntentPlan.WorldBroadcastIntents);
		foreach (var step in executorPlan.ExecutionOrder)
			trace.Add($"{step.Sequence}:{step.Kind}:{step.RecipientObjectId}:{step.PacketType}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveRecruitment, plan.IntentPlan.ClientActionKind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		var intent = Assert.Single(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal("ELYOS", intent.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())", intent.JavaSource);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 1", "1:WorldBroadcast::SmFindGroup"], trace);
		var broadcast = Assert.Single(executorPlan.WorldBroadcasts);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(2, broadcast.SentCount);
		var recorded = Assert.Single(registry.WorldBroadcasts);
		Assert.Equal([recruiter.ObjectId, sameRace.ObjectId], recorded.RecipientObjectIds);
		Assert.DoesNotContain(otherRace.ObjectId, recorded.RecipientObjectIds);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(sentPackets);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 102).Recruitments);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionFiveCanProduceOrderedOptInWorldBroadcastFanoutTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var otherRace = CreatePlayer(0x01020306, "OtherRace", "ASMODIANS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddApplication(
			applicant,
			message: "Need group",
			groupType: 2,
			classId: 5,
			level: 65,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([applicant, sameRace, otherRace]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, applicant);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(5);
				buffer.WriteD(applicant.ObjectId);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		var executorPlan = await new FindGroupSideEffectDispatchExecutorService(registry)
			.ExecuteAsync(plan!.IntentPlan.DirectPacketIntents, plan.IntentPlan.WorldBroadcastIntents);
		foreach (var step in executorPlan.ExecutionOrder)
			trace.Add($"{step.Sequence}:{step.Kind}:{step.RecipientObjectId}:{step.PacketType}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveApplication, plan.IntentPlan.ClientActionKind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		var intent = Assert.Single(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal("ELYOS", intent.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())", intent.JavaSource);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 5", "1:WorldBroadcast::SmFindGroup"], trace);
		var broadcast = Assert.Single(executorPlan.WorldBroadcasts);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(2, broadcast.SentCount);
		var recorded = Assert.Single(registry.WorldBroadcasts);
		Assert.Equal([applicant.ObjectId, sameRace.ObjectId], recorded.RecipientObjectIds);
		Assert.DoesNotContain(otherRace.ObjectId, recorded.RecipientObjectIds);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(sentPackets);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 102).Applications);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwelveAcceptUsesConnectionResolverAndRuntimesWithoutLiveDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 6,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			playerGroupRuntime: new PlayerGroupRuntime(),
			playerAllianceRuntime: new PlayerAllianceRuntime());
		SetActivePlayer(fixture.Connection, responder);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		if (plan?.InvitePlan?.GroupInviteRequest != null)
			trace.Add($"1:InviteRequest:Group:{plan.InvitePlan.GroupInviteRequest.QuestionWindow?.Code}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedGroupInvite, plan.IntentPlan.InstanceApplicationStatus);
		Assert.NotNull(plan.IntentPlan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Group, plan.IntentPlan.InviteIntent!.Kind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.NotNull(plan.InvitePlan);
		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.GroupInvitePlanned, plan.InvitePlan!.Status);
		Assert.False(plan.InvitePlan.DispatchLiveSideEffects);
		Assert.Equal(GroupInviteRequestStatus.Requested, plan.InvitePlan.GroupInviteRequest?.Status);
		Assert.Equal(responder.ObjectId, plan.InvitePlan.GroupInviteRequest?.Request.InviterObjectId);
		Assert.Equal(SmQuestionWindow.PartyInvite, plan.InvitePlan.GroupInviteRequest?.QuestionWindow?.Code);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 12", "1:InviteRequest:Group:60000"], trace);
		Assert.Equal(1, applicant.ResponseRequester.Count);
		Assert.Empty(sentPackets);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwelveAcceptAllianceUsesConnectionResolverAndRuntimesWithoutLiveDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 7,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			playerGroupRuntime: new PlayerGroupRuntime(),
			playerAllianceRuntime: new PlayerAllianceRuntime());
		SetActivePlayer(fixture.Connection, responder);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		if (plan?.InvitePlan?.AllianceInviteRequest != null)
			trace.Add($"1:InviteRequest:Alliance:{plan.InvitePlan.AllianceInviteRequest.QuestionWindow?.Code}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.AcceptedAllianceInvite, plan.IntentPlan.InstanceApplicationStatus);
		Assert.NotNull(plan.IntentPlan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Alliance, plan.IntentPlan.InviteIntent!.Kind);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.NotNull(plan.InvitePlan);
		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.AllianceInvitePlanned, plan.InvitePlan!.Status);
		Assert.False(plan.InvitePlan.DispatchLiveSideEffects);
		Assert.Equal(AllianceInviteRequestStatus.Requested, plan.InvitePlan.AllianceInviteRequest?.Status);
		Assert.Equal(responder.ObjectId, plan.InvitePlan.AllianceInviteRequest?.Request?.RequesterObjectId);
		Assert.Equal(applicant.ObjectId, plan.InvitePlan.AllianceInviteRequest?.Request?.RequestTargetObjectId);
		Assert.Equal(SmQuestionWindow.AllianceInvite, plan.InvitePlan.AllianceInviteRequest?.QuestionWindow?.Code);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 12", "1:InviteRequest:Alliance:70000"], trace);
		Assert.Equal(1, applicant.ResponseRequester.Count);
		Assert.NotNull(applicant.PendingAllianceInviteRequest);
		Assert.Empty(sentPackets);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwelveDeclineComposesWhisperIntentWithoutLiveDispatch()
	{
		var sentPackets = new List<GameServerPacket>();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			playerGroupRuntime: new PlayerGroupRuntime(),
			playerAllianceRuntime: new PlayerAllianceRuntime());
		SetActivePlayer(fixture.Connection, responder);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(0);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.ComposedDisabledSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.Declined, plan.IntentPlan.InstanceApplicationStatus);
		Assert.Null(plan.IntentPlan.InviteIntent);
		Assert.Null(plan.InvitePlan);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmMessage), intent.Packet.GetType().Name);
		Assert.Contains("ChatUtil.l10n(1400217)", intent.JavaSource, StringComparison.Ordinal);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(0, applicant.ResponseRequester.Count);
		Assert.Empty(sentPackets);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwelveMissingApplicantComposesNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var missingApplicantObjectId = 0x01020304;
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 6,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			playerGroupRuntime: new PlayerGroupRuntime(),
			playerAllianceRuntime: new PlayerAllianceRuntime());
		SetActivePlayer(fixture.Connection, responder);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(missingApplicantObjectId);
				buffer.WriteC(1);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingApplicant, plan.IntentPlan.InstanceApplicationStatus);
		Assert.Null(plan.IntentPlan.InviteIntent);
		Assert.Null(plan.InvitePlan);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(sentPackets);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwelveAcceptMissingInstanceGroupComposesNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			playerGroupRuntime: new PlayerGroupRuntime(),
			playerAllianceRuntime: new PlayerAllianceRuntime());
		SetActivePlayer(fixture.Connection, responder);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan!.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(12, plan.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingInstanceGroup, plan.IntentPlan.InstanceApplicationStatus);
		Assert.Null(plan.IntentPlan.InviteIntent);
		Assert.Null(plan.InvitePlan);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(0, applicant.ResponseRequester.Count);
		Assert.Empty(sentPackets);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_UnconfiguredConnectionPreservesDeferredBoundary()
	{
		await using var fixture = await ConnectionFixture.CreateAsync(findGroupService: null);
		SetActivePlayer(fixture.Connection, CreatePlayer(0x01020304, "Recruiter", "ELYOS"));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);

		Assert.Null(plan);
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		var packet = GameClientPacketFactory.TryCreatePacket(
			CreateClientPayload(77, writePayload),
			GameConnectionState.InGame);
		return Assert.IsType<CmFindGroup>(packet);
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

	private static Player CreatePlayer(int objectId, string name, string race)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = "CLERIC",
			Level = 65,
			Position = new WorldPosition(210010000, 10, 20, 30, 0),
		};
	}

	private static void SetActivePlayer(GameServerConnection connection, Player player)
	{
		var activePlayerField = typeof(GameServerConnection).GetField("_activePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(activePlayerField);
		activePlayerField.SetValue(connection, player);

		var stateField = typeof(GameServerConnection).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(stateField);
		stateField.SetValue(connection, GameConnectionState.InGame);
	}

	private sealed class ConnectionFixture : IAsyncDisposable
	{
		private readonly TcpClient _client;

		private ConnectionFixture(TcpClient client, GameServerConnection connection)
		{
			_client = client;
			Connection = connection;
		}

		public GameServerConnection Connection { get; }

		public static async Task<ConnectionFixture> CreateAsync(
			FindGroupRecruitmentPlanService? findGroupService,
			Action<GameServerPacket>? sentPacketObserver = null,
			IGameClientConnectionRegistry? connectionRegistry = null,
			PlayerGroupRuntime? playerGroupRuntime = null,
			PlayerAllianceRuntime? playerAllianceRuntime = null)
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
				var compositionService = findGroupService == null
					? null
					: new FindGroupConnectionClientActionCompositionPlanService(
						new FindGroupClientActionPlanService(findGroupService));
				var dispatchAdapterService = findGroupService == null
					? null
					: new FindGroupConnectionBoundaryDispatchAdapterService();
				return new ConnectionFixture(
					client,
					new GameServerConnection(
						NullLogger.Instance,
						serverClient,
						"find-group-boundary-test",
						new GamePacketProcessor<string>((_, _) => Task.CompletedTask),
						options: new GameServerOptions(),
						connectionRegistry: connectionRegistry,
						sentPacketObserver: sentPacketObserver,
						crypt: crypt,
						playerGroupRuntime: playerGroupRuntime,
						playerAllianceRuntime: playerAllianceRuntime,
						findGroupConnectionClientActionCompositionPlanService: compositionService,
						findGroupConnectionBoundaryDispatchAdapterService: dispatchAdapterService));
			}
			finally
			{
				listener.Stop();
			}
		}

		public async ValueTask DisposeAsync()
		{
			await Connection.DisposeAsync();
			_client.Dispose();
		}
	}

	private sealed class CapturingConnectionRegistry(IReadOnlyList<Player> onlinePlayers) : IGameClientConnectionRegistry
	{
		public List<(int RecipientObjectId, GameServerPacket Packet)> DirectSends { get; } = [];

		public List<(GameServerPacket Packet, IReadOnlyList<int> RecipientObjectIds)> WorldBroadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = onlinePlayers.FirstOrDefault(candidate => string.Equals(candidate.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in onlinePlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			DirectSends.Add((playerObjectId, packet));
			return Task.FromResult(onlinePlayers.Any(player => player.ObjectId == playerObjectId));
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var recipients = onlinePlayers.Where(player => filter?.Invoke(player) != false).Select(player => player.ObjectId).ToArray();
			WorldBroadcasts.Add((packet, recipients));
			return Task.FromResult(recipients.Length);
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
}
