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
	public async Task ProcessPacketAsync_ActionZeroAndFourSendRaceFilteredShowLists()
	{
		var sentPackets = new List<GameServerPacket>();
		var findGroupService = new FindGroupRecruitmentPlanService();
		var elyosRecruiter = CreatePlayer(2002, "ElyosRecruiter", "ELYOS");
		var asmodianRecruiter = CreatePlayer(3003, "AsmodianRecruiter", "ASMODIANS");
		var elyosApplicant = CreatePlayer(4004, "ElyosApplicant", "ELYOS");
		var asmodianApplicant = CreatePlayer(5005, "AsmodianApplicant", "ASMODIANS");
		findGroupService.AddRecruitment(elyosRecruiter, "Elyos recruit", groupType: 2, nowEpochSeconds: 100);
		findGroupService.AddRecruitment(asmodianRecruiter, "Asmo recruit", groupType: 3, nowEpochSeconds: 101);
		findGroupService.AddApplication(elyosApplicant, "Elyos apply", groupType: 4, classId: 10, level: 65, nowEpochSeconds: 102);
		findGroupService.AddApplication(asmodianApplicant, "Asmo apply", groupType: 5, classId: 7, level: 64, nowEpochSeconds: 103);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));

		SetActivePlayer(fixture.Connection, CreatePlayer(9001, "ElyosViewer", "ELYOS"));
		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(77, buffer => buffer.WriteC(0)));

		var recruitmentPacket = Assert.IsType<SmFindGroup>(Assert.Single(sentPackets));
		Assert.Equal(0, ReadPrivateField<int>(recruitmentPacket, "_action"));
		var recruitments = ReadPrivateField<IReadOnlyList<FindGroupRecruitmentSnapshot>>(recruitmentPacket, "_recruitments");
		var recruitment = Assert.Single(recruitments);
		Assert.Equal(elyosRecruiter.ObjectId, recruitment.ObjectId);
		Assert.Equal("Elyos recruit", recruitment.Message);
		Assert.Equal("ElyosRecruiter", recruitment.RecruiterName);
		Assert.Equal(2, recruitment.GroupType);

		sentPackets.Clear();
		SetActivePlayer(fixture.Connection, CreatePlayer(9002, "AsmodianViewer", "ASMODIANS"));
		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(77, buffer => buffer.WriteC(4)));

		var applicationPacket = Assert.IsType<SmFindGroup>(Assert.Single(sentPackets));
		Assert.Equal(4, ReadPrivateField<int>(applicationPacket, "_action"));
		var applications = ReadPrivateField<IReadOnlyList<FindGroupApplicationSnapshot>>(applicationPacket, "_applications");
		var application = Assert.Single(applications);
		Assert.Equal(asmodianApplicant.ObjectId, application.PlayerObjectId);
		Assert.Equal("Asmo apply", application.Message);
		Assert.Equal("AsmodianApplicant", application.PlayerName);
		Assert.Equal(5, application.GroupType);
		Assert.Equal(7, application.ClassId);
		Assert.Equal(64, application.Level);
	}

	[Fact]
	public async Task ProcessPacketAsync_ActionTwoAddRecruitmentSendsPostedMessageThenShowList()
	{
		var sentPackets = new List<GameServerPacket>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));
		SetActivePlayer(fixture.Connection, recruiter);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				77,
				buffer =>
				{
					buffer.WriteC(2);
					buffer.WriteD(0x7F7F7F7F);
					buffer.WriteS("Need healer");
					buffer.WriteC(3);
				}));

		Assert.Collection(
			sentPackets,
			packet =>
			{
				var systemMessage = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1400392, systemMessage.MessageId);
			},
			packet => Assert.IsType<SmFindGroup>(packet));
		var snapshot = Assert.Single(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 102).Recruitments);
		Assert.Equal(recruiter.ObjectId, snapshot.ObjectId);
		Assert.Equal("Need healer", snapshot.Message);
		Assert.Equal(3, snapshot.GroupType);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTwoCanProduceOrderedOptInPostedMessageBeforeShowListTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([recruiter]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, recruiter);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
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
		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, plan.IntentPlan.ClientActionKind);
		Assert.Collection(
			plan.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmSystemMessage), intent.Packet.GetType().Name);
				Assert.Equal(1400392, Assert.IsType<SmSystemMessage>(intent.Packet).MessageId);
				Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(0, recruitments))", intent.JavaSource);
			});
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(
			[
				"accepted disabled CM_FIND_GROUP action 2",
				"1:DirectPacket:16909060:SmSystemMessage",
				"2:DirectPacket:16909060:SmFindGroup",
			],
			trace);
		Assert.Equal([nameof(SmSystemMessage), nameof(SmFindGroup)], executorPlan.DirectPackets.Select(packet => packet.PacketType));
		Assert.All(executorPlan.DirectPackets, direct => Assert.True(direct.Sent));
		Assert.Equal([recruiter.ObjectId, recruiter.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var snapshot = Assert.Single(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 102).Recruitments);
		Assert.Equal(recruiter.ObjectId, snapshot.ObjectId);
		Assert.Equal("Need healer", snapshot.Message);
		Assert.Equal(3, snapshot.GroupType);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionFourCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var player = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddApplication(
			player,
			message: "Need group",
			groupType: 2,
			classId: 5,
			level: 65,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([player]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(4));

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
		Assert.Equal(FindGroupClientActionPlanKind.ShowApplications, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Contains("new SM_FIND_GROUP(4, applications)", intent.JavaSource, StringComparison.Ordinal);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 4", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(player.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task ProcessPacketAsync_ActionSixAddApplicationSendsPostedMessageThenShowList()
	{
		var sentPackets = new List<GameServerPacket>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));
		SetActivePlayer(fixture.Connection, applicant);

		await InvokeProcessPacketAsync(
			fixture.Connection,
			CreateClientPayload(
				77,
				buffer =>
				{
					buffer.WriteC(6);
					buffer.WriteD(0x7F7F7F7F);
					buffer.WriteS("Need group");
					buffer.WriteC(2);
					buffer.WriteC(5);
					buffer.WriteC(65);
				}));

		Assert.Collection(
			sentPackets,
			packet =>
			{
				var systemMessage = Assert.IsType<SmSystemMessage>(packet);
				Assert.Equal(1400393, systemMessage.MessageId);
			},
			packet => Assert.IsType<SmFindGroup>(packet));
		var snapshot = Assert.Single(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 102).Applications);
		Assert.Equal(applicant.ObjectId, snapshot.PlayerObjectId);
		Assert.Equal("Need group", snapshot.Message);
		Assert.Equal(2, snapshot.GroupType);
		Assert.Equal(5, snapshot.ClassId);
		Assert.Equal(65, snapshot.Level);
	}

	[Fact]
	public async Task ProcessPacketAsync_ActionTwoAndSixCanMaterializeAcceptedMutationPostBoundaryRows()
	{
		var sentPackets = new List<GameServerPacket>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var applicant = CreatePlayer(0x01020305, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));

		var actionTwoRow = await CaptureLiveMutationPostRowAsync(
			fixture.Connection,
			sentPackets,
			recruiter,
			nowEpochSeconds: 101,
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});
		var actionSixRow = await CaptureLiveMutationPostRowAsync(
			fixture.Connection,
			sentPackets,
			applicant,
			nowEpochSeconds: 102,
			buffer =>
			{
				buffer.WriteC(6);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need group");
				buffer.WriteC(2);
				buffer.WriteC(5);
				buffer.WriteC(65);
			});

		var guarded = FindGroupMutationPostGuardedFixtureResultContractService.Create(
			candidateRows: [actionTwoRow, actionSixRow]);
		var intake = FindGroupMutationPostCSharpLiveBoundaryRowIntakePreflightService.Create(guarded);
		var handoff = FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportService.Create(intake);

		Assert.Equal(FindGroupMutationPostGuardedFixtureResultContractStatus.ReadyForComparisonHandoff, guarded.Status);
		Assert.Equal(FindGroupMutationPostCSharpAcceptedBoundaryRowHandoffReportStatus.ReadyForJavaArtifactPairingRuntimeComparisonBlocked, handoff.Status);
		Assert.Equal(2, handoff.AcceptedLiveRowCount);
		Assert.True(handoff.HasActionTwoAcceptedRow);
		Assert.True(handoff.HasActionSixAcceptedRow);
		Assert.True(handoff.CanFeedJavaArtifactPairing);
		Assert.False(handoff.CanRunRuntimeComparison);
		Assert.False(handoff.CanClaimVerifiedParity);
		Assert.All(guarded.AcceptedLiveRows, row =>
		{
			Assert.True(row.BoundaryAccepted);
			Assert.True(row.ExecutorInvokedFromBoundary);
			Assert.True(row.RegistrySendsObservedInOrder);
		});
	}

	[Fact]
	public async Task ProcessPacketAsync_ActionTwoAndSixRowsFeedJavaCSharpValueComparison()
	{
		var sentPackets = new List<GameServerPacket>();
		var recruiter = CreatePlayer(2002, "Recruiter", "ELYOS");
		var applicant = CreatePlayer(4004, "Applicant", "ASMODIANS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet));

		var actionTwoRow = await CaptureLiveMutationPostRowAsync(
			fixture.Connection,
			sentPackets,
			recruiter,
			nowEpochSeconds: 101,
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});
		var actionSixRow = await CaptureLiveMutationPostRowAsync(
			fixture.Connection,
			sentPackets,
			applicant,
			nowEpochSeconds: 102,
			buffer =>
			{
				buffer.WriteC(6);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need group");
				buffer.WriteC(2);
				buffer.WriteC(5);
				buffer.WriteC(65);
			});
		var javaArtifacts = ShapeValidJavaArtifactsForLiveComparisonFixture();

		var report = FindGroupMutationPostProjectedRowValueComparisonExecutorService.Compare(
			javaArtifacts,
			[actionTwoRow, actionSixRow]);

		Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonStatus.Compared, report.Status);
		Assert.True(report.IsLive);
		Assert.True(report.HasActionTwoJavaRow);
		Assert.True(report.HasActionSixJavaRow);
		Assert.True(report.HasActionTwoAcceptedCSharpRow);
		Assert.True(report.HasActionSixAcceptedCSharpRow);
		Assert.True(report.AllComparedFieldsMatched);
		Assert.False(report.CanClaimVerifiedParity);
		Assert.Equal(18, report.Rows.Count);
		Assert.All(report.Rows, row => Assert.Equal(FindGroupMutationPostProjectedRowValueComparisonResultKind.Matched, row.ResultKind));
		Assert.Contains(report.Rows, row =>
			row.Action == 2
			&& row.FieldName == "visibleEntryObjectIdsAfterMutation"
			&& row.JavaValue == "[2002]"
			&& row.CSharpValue == "[2002]");
		Assert.Contains(report.Rows, row =>
			row.Action == 6
			&& row.FieldName == "postedSystemMessageId"
			&& row.JavaValue == "1400393"
			&& row.CSharpValue == "1400393");
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionSixCanProduceOrderedOptInPostedMessageBeforeShowListTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, applicant);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(6);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need group");
				buffer.WriteC(2);
				buffer.WriteC(5);
				buffer.WriteC(65);
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
		Assert.Equal(FindGroupClientActionPlanKind.AddApplication, plan.IntentPlan.ClientActionKind);
		Assert.Collection(
			plan.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmSystemMessage), intent.Packet.GetType().Name);
				Assert.Equal(1400393, Assert.IsType<SmSystemMessage>(intent.Packet).MessageId);
				Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(4, applications))", intent.JavaSource);
			});
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(
			[
				"accepted disabled CM_FIND_GROUP action 6",
				"1:DirectPacket:16909060:SmSystemMessage",
				"2:DirectPacket:16909060:SmFindGroup",
			],
			trace);
		Assert.Equal([nameof(SmSystemMessage), nameof(SmFindGroup)], executorPlan.DirectPackets.Select(packet => packet.PacketType));
		Assert.All(executorPlan.DirectPackets, direct => Assert.True(direct.Sent));
		Assert.Equal([applicant.ObjectId, applicant.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var snapshot = Assert.Single(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 102).Applications);
		Assert.Equal(applicant.ObjectId, snapshot.PlayerObjectId);
		Assert.Equal("Need group", snapshot.Message);
		Assert.Equal(2, snapshot.GroupType);
		Assert.Equal(5, snapshot.ClassId);
		Assert.Equal(65, snapshot.Level);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionEightCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var player = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([player]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(8);
				buffer.WriteD(0x11223344);
				buffer.WriteC(0);
				buffer.WriteS("Entry");
				buffer.WriteC(6);
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
		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 8", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(player.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var stored = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 102).InstanceGroups);
		Assert.Equal(player.ObjectId, stored.RecruiterObjectId);
		Assert.Equal(0x11223344, stored.InstanceMaskId);
		Assert.Equal(6, stored.MinMembers);
		Assert.Equal("Entry", stored.Message);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionNineRemovedCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var removed = CreatePlayer(0x01020304, "Removed", "ELYOS");
		var remaining = CreatePlayer(0x01020307, "Remaining", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(removed, 0x11223344, "Removed", minMembers: 3, nowEpochSeconds: 100);
		findGroupService.RegisterInstanceGroup(remaining, 0x11223345, "Remaining", minMembers: 2, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([removed]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, removed);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(9);
				buffer.WriteD(removed.ObjectId);
				buffer.WriteD(0x11223344);
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
		Assert.Equal(FindGroupClientActionPlanKind.RemoveInstanceGroup, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Removed, plan.IntentPlan.InstanceGroupStatus);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(removed.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 9", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(removed.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var stored = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 102).InstanceGroups);
		Assert.Equal(remaining.ObjectId, stored.GroupEntryId);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionNineMissingCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var player = CreatePlayer(0x01020304, "Player", "ELYOS");
		var remaining = CreatePlayer(0x01020307, "Remaining", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(remaining, 0x11223345, "Remaining", minMembers: 2, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([player]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, player);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(9);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteD(0x11223344);
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
		Assert.Equal(FindGroupClientActionPlanKind.RemoveInstanceGroup, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Missing, plan.IntentPlan.InstanceGroupStatus);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 9", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(player.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var stored = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 102).InstanceGroups);
		Assert.Equal(remaining.ObjectId, stored.GroupEntryId);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTenCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		var otherRace = CreatePlayer(0x01020308, "OtherRace", "ASMODIANS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(recruiter, 0x11223344, "Entry", minMembers: 3, nowEpochSeconds: 100);
		findGroupService.RegisterInstanceGroup(otherRace, 0x11223345, "Other", minMembers: 2, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([viewer]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, viewer);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

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
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 10", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(viewer.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var stored = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 102).InstanceGroups);
		Assert.Equal(recruiter.ObjectId, stored.GroupEntryId);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionTenFormAnywhereCanProduceOrderedMaskThenShowTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(recruiter, 0x11223344, "Entry", minMembers: 3, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([viewer]);
		var options = new GameServerOptions
		{
			Instance = new GameServerInstanceOptions { FormInstanceGroupAnywhere = true },
		};
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
			new AutoGroupSummary(303, 300120000, 0, 0, 0, 0, false, false, false, [700002]),
		]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			options: options,
			autoGroups: autoGroups);
		SetActivePlayer(fixture.Connection, viewer);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

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
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, plan.IntentPlan.ClientActionKind);
		Assert.Collection(
			plan.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
				Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
			});
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(
			[
				"accepted disabled CM_FIND_GROUP action 10",
				"1:DirectPacket:16909060:SmFindGroup",
				"2:DirectPacket:16909060:SmFindGroup",
			],
			trace);
		Assert.Equal([viewer.ObjectId, viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionThirteenUpdateCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(recruiter, 0x11223344, "Entry", minMembers: 3, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([viewer]);
		var options = new GameServerOptions
		{
			Instance = new GameServerInstanceOptions { FormInstanceGroupAnywhere = true },
		};
		var autoGroups = new AutoGroupTable(
		[
			new AutoGroupSummary(302, 300110000, 0, 0, 0, 0, false, false, false, [700001]),
		]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry,
			options: options,
			autoGroups: autoGroups);
		SetActivePlayer(fixture.Connection, viewer);
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(13));

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
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupsUpdate, plan.IntentPlan.ClientActionKind);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		Assert.DoesNotContain(
			plan.IntentPlan.DirectPacketIntents,
			packetIntent => packetIntent.JavaSource.Contains("instanceMaskIds", StringComparison.Ordinal));
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 13", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(viewer.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionElevenCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant, recruiter]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, applicant);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(11);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteD(0x11223344);
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
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplication, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.ApplicationSent, plan.IntentPlan.InstanceApplicationStatus);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(applicant))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 11", "1:DirectPacket:16909063:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(recruiter.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionElevenMissingRecipientRecordsNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var missingRecruiterObjectId = 0x01020307;
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, applicant);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(11);
				buffer.WriteD(missingRecruiterObjectId);
				buffer.WriteD(0x11223344);
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		var executorPlan = await new FindGroupSideEffectDispatchExecutorService(registry)
			.ExecuteAsync(plan!.IntentPlan.DirectPacketIntents, plan.IntentPlan.WorldBroadcastIntents);
		foreach (var step in executorPlan.ExecutionOrder)
			trace.Add($"{step.Sequence}:{step.Kind}:{step.RecipientObjectId}:{step.PacketType}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplication, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceApplicationPlanStatus.MissingRecipient, plan.IntentPlan.InstanceApplicationStatus);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 11"], trace);
		Assert.Empty(executorPlan.DirectPackets);
		Assert.Empty(executorPlan.WorldBroadcasts);
		Assert.Empty(executorPlan.ExecutionOrder);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionFifteenCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var viewer = CreatePlayer(0x01020307, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			recruiter,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 3,
			nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([viewer]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, viewer);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(15);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteD(0x11223344);
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
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupMembersInfo, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Shown, plan.IntentPlan.InstanceGroupMemberInfoStatus);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(16, List.of(instanceGroup)))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 15", "1:DirectPacket:16909063:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(viewer.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionSeventeenUpdatedCanProduceOrderedOptInDirectPacketTrace()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(recruiter, 0x11223344, "Old", minMembers: 3, nowEpochSeconds: 100);
		var registry = new CapturingConnectionRegistry([recruiter]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, recruiter);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(17);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteD(0x11223344);
				buffer.WriteS("New");
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
		Assert.Equal(FindGroupClientActionPlanKind.UpdateInstanceGroup, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Updated, plan.IntentPlan.InstanceGroupStatus);
		var intent = Assert.Single(plan.IntentPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), intent.Packet.GetType().Name);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 17", "1:DirectPacket:16909060:SmFindGroup"], trace);
		var directSend = Assert.Single(registry.DirectSends);
		Assert.Equal(recruiter.ObjectId, directSend.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), directSend.Packet.GetType().Name);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
		Assert.True(executorPlan.DispatchLiveSideEffects);
		Assert.Contains("Opt-in executor only", executorPlan.BoundaryNote, StringComparison.Ordinal);
		var stored = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 102).InstanceGroups);
		Assert.Equal("New", stored.Message);
	}

	[Fact]
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionSeventeenMissingRecordsNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([recruiter]);
		await using var fixture = await ConnectionFixture.CreateAsync(
			findGroupService,
			sentPacketObserver: packet => sentPackets.Add(packet),
			connectionRegistry: registry);
		SetActivePlayer(fixture.Connection, recruiter);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(17);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteD(0x11223344);
				buffer.WriteS("New");
			});

		var plan = fixture.Connection.CreateDisabledFindGroupBoundaryPlan(packet, nowEpochSeconds: 101);
		trace.Add($"accepted disabled CM_FIND_GROUP action {plan?.IntentPlan.Action}");
		var executorPlan = await new FindGroupSideEffectDispatchExecutorService(registry)
			.ExecuteAsync(plan!.IntentPlan.DirectPacketIntents, plan.IntentPlan.WorldBroadcastIntents);
		foreach (var step in executorPlan.ExecutionOrder)
			trace.Add($"{step.Sequence}:{step.Kind}:{step.RecipientObjectId}:{step.PacketType}");

		Assert.NotNull(plan);
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateInstanceGroup, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupInstanceGroupPlanStatus.Missing, plan.IntentPlan.InstanceGroupStatus);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 17"], trace);
		Assert.Empty(executorPlan.DirectPackets);
		Assert.Empty(executorPlan.WorldBroadcasts);
		Assert.Empty(executorPlan.ExecutionOrder);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
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
		Assert.Equal(FindGroupRecruitmentPlanStatus.Removed, plan.IntentPlan.RecruitmentStatus);
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
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionOneMissingRecruitmentRecordsNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([recruiter, sameRace]);
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
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveRecruitment, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupRecruitmentPlanStatus.Missing, plan.IntentPlan.RecruitmentStatus);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 1"], trace);
		Assert.Empty(executorPlan.DirectPackets);
		Assert.Empty(executorPlan.WorldBroadcasts);
		Assert.Empty(executorPlan.ExecutionOrder);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
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
		Assert.Equal(FindGroupApplicationPlanStatus.Removed, plan.IntentPlan.ApplicationStatus);
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
	public async Task CreateDisabledFindGroupBoundaryPlan_ActionFiveMissingApplicationRecordsNoSideEffects()
	{
		var sentPackets = new List<GameServerPacket>();
		var trace = new List<string>();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		var registry = new CapturingConnectionRegistry([applicant, sameRace]);
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
		Assert.Equal(FindGroupConnectionBoundaryDispatchAdapterStatus.NoSideEffects, plan.Status);
		Assert.False(plan.ShouldDispatchLiveSideEffects);
		Assert.False(plan.IsCmFindGroupBoundaryWired);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveApplication, plan.IntentPlan.ClientActionKind);
		Assert.Equal(FindGroupApplicationPlanStatus.Missing, plan.IntentPlan.ApplicationStatus);
		Assert.Empty(plan.IntentPlan.DirectPacketIntents);
		Assert.Empty(plan.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(["accepted disabled CM_FIND_GROUP action 5"], trace);
		Assert.Empty(executorPlan.DirectPackets);
		Assert.Empty(executorPlan.WorldBroadcasts);
		Assert.Empty(executorPlan.ExecutionOrder);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
		Assert.Empty(sentPackets);
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

	private static async Task InvokeProcessPacketAsync(GameServerConnection connection, byte[] payload)
	{
		var method = typeof(GameServerConnection).GetMethod("ProcessPacketAsync", BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(method);
		using var packet = new PacketBuffer(payload);
		var task = Assert.IsAssignableFrom<Task>(method.Invoke(connection, [packet]));
		await task;
	}

	private static async Task<FindGroupDirectPacketMutationPostBoundaryTraceExport> CaptureLiveMutationPostRowAsync(
		GameServerConnection connection,
		List<GameServerPacket> sentPackets,
		Player player,
		int nowEpochSeconds,
		Action<PacketBuffer> writePayload)
	{
		var packet = CreateFindGroupPacket(writePayload);
		var previewService = new FindGroupRecruitmentPlanService();
		var compositionPlan = new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(previewService))
			.CreateDisabledPlan(player, packet, nowEpochSeconds);
		var projected = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService
			.CreateExportFromDisabledPlan(compositionPlan);
		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created, projected.Status);

		sentPackets.Clear();
		SetActivePlayer(connection, player);
		await InvokeProcessPacketAsync(connection, CreateClientPayload(77, writePayload));

		var observed = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService
			.CreateExportFromLiveBoundaryObservation(projected.Export, sentPackets);
		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created, observed.Status);
		Assert.Contains("ProcessPacketAsync-observed", observed.Reason, StringComparison.Ordinal);
		return observed.Export;
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidJavaArtifactsForLiveComparisonFixture()
	{
		return new FindGroupMutationPostJavaTraceArtifactDirectoryReport(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			[
				ShapeValidJavaArtifactFile(JavaTraceRow(
					action: 2,
					mutationKind: "Recruitment",
					activePlayerObjectId: 2002,
					activePlayerRace: "ELYOS",
					mutatedEntryObjectId: 2002,
					postedSystemMessageId: 1400392,
					refreshedListAction: 0,
					visibleEntryObjectIdsAfterMutation: [2002])),
				ShapeValidJavaArtifactFile(JavaTraceRow(
					action: 6,
					mutationKind: "Application",
					activePlayerObjectId: 4004,
					activePlayerRace: "ASMODIANS",
					mutatedEntryObjectId: 4004,
					postedSystemMessageId: 1400393,
					refreshedListAction: 4,
					visibleEntryObjectIdsAfterMutation: [4004])),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid Java artifact rows for live ProcessPacketAsync comparison");
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidJavaArtifactFile(
		FindGroupMutationPostJavaTraceArtifactValidationTraceRow row)
	{
		return new FindGroupMutationPostJavaTraceArtifactDirectoryFileRow(
			row.Action,
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(row.Action),
					FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
					new FindGroupMutationPostJavaTraceArtifactValidationReport(
						[],
						IsValid: true,
						new FindGroupMutationPostJavaTraceArtifactMetadata(
							SchemaVersion: 1,
							TraceName: "cm-find-group-direct-mutation-post-boundary",
							[row])),
			"shape-valid Java artifact row matching the live boundary fixture identity");
	}

	private static FindGroupMutationPostJavaTraceArtifactValidationTraceRow JavaTraceRow(
		int action,
		string mutationKind,
		int activePlayerObjectId,
		string activePlayerRace,
		int mutatedEntryObjectId,
		int postedSystemMessageId,
		int refreshedListAction,
		IReadOnlyList<int> visibleEntryObjectIdsAfterMutation)
	{
		return new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
			SchemaVersion: 1,
			TraceName: "cm-find-group-direct-mutation-post-boundary",
			TraceSource: "Java",
			action,
			mutationKind,
			postedSystemMessageId,
			refreshedListAction,
			BoundaryAccepted: true,
			activePlayerObjectId,
			activePlayerRace,
			ServerEpochSeconds: 1700000000,
			mutatedEntryObjectId,
			StateMutationRecordedBeforeDirectPackets: true,
			PostedSystemMessageRecipientObjectId: activePlayerObjectId,
			PostedSystemMessageType: "SmSystemMessage",
			RefreshedListRecipientObjectId: activePlayerObjectId,
			RefreshedListPacketType: "SmFindGroup",
			visibleEntryObjectIdsAfterMutation,
			ExecutorInvokedFromBoundary: false,
			RegistrySendsObservedInOrder: false,
			WorldBroadcastCount: 0,
			InviteDispatchCount: 0);
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

	private static T ReadPrivateField<T>(object target, string fieldName)
	{
		var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.NotNull(field);
		return Assert.IsAssignableFrom<T>(field.GetValue(target));
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
			PlayerAllianceRuntime? playerAllianceRuntime = null,
			GameServerOptions? options = null,
			AutoGroupTable? autoGroups = null)
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
						new FindGroupClientActionPlanService(findGroupService),
						autoGroups: autoGroups,
						options: options);
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
						options: options ?? new GameServerOptions(),
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
