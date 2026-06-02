using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class FindGroupConnectionBoundarySideEffectCompositionEvidenceServiceTests
{
	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionZeroRecruitmentShowAsDirectPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var otherRace = CreatePlayer(0x01020305, "OtherRace", "ASMODIANS");
		registry.OnlineDirectRecipients.Add(viewer.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(
			viewer,
			message: "Need healer",
			groupType: 2,
			nowEpochSeconds: 0x01020305);
		findGroupService.AddRecruitment(
			otherRace,
			message: "Other",
			groupType: 3,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(0));

		var compositionPlan = compositionService.CreateDisabledPlan(
			viewer,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(0, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowRecruitments, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.False(evidence.IntentPlan.IsCmFindGroupBoundaryWired);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(0, recruitments))", intent.JavaSource);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionOneWorldBroadcastWithRaceFilter()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var otherRace = CreatePlayer(0x01020306, "OtherRace", "ASMODIANS");
		registry.WorldPlayers.AddRange([sameRace, otherRace]);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(
			recruiter,
			message: "Need healer",
			groupType: 2,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
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

		var compositionPlan = compositionService.CreateDisabledPlan(
			recruiter,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(1, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveRecruitment, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.False(evidence.IntentPlan.IsCmFindGroupBoundaryWired);
		Assert.Empty(evidence.IntentPlan.DirectPacketIntents);
		var intent = Assert.Single(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Equal("ELYOS", intent.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == recruitment.getRace())", intent.JavaSource);
		Assert.Empty(evidence.ExecutionPlan.DirectPackets);
		var broadcast = Assert.Single(evidence.ExecutionPlan.WorldBroadcasts);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(nameof(SmFindGroup), broadcast.PacketType);
		Assert.Equal("p -> p.getRace() == recorded race", broadcast.JavaFilter);
		Assert.Equal(1, broadcast.SentCount);
		var recorded = Assert.Single(registry.WorldBroadcasts);
		Assert.Equal([sameRace.ObjectId], recorded.RecipientObjectIds);
		Assert.Empty(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 0x01020307).Recruitments);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionTwoAddRecruitmentAsPostedMessageAndShowList()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		registry.OnlineDirectRecipients.Add(recruiter.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			recruiter,
			packet,
			nowEpochSeconds: 0x01020305);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(2, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.AddRecruitment, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(2, evidence.IntentPlan.DirectPacketIntents.Count);
		Assert.Collection(
			evidence.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
				Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_OFFER_PARTY_POSTED", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(0, recruitments))", intent.JavaSource);
			});
		Assert.Equal(2, evidence.ExecutionPlan.DirectPackets.Count);
		Assert.All(evidence.ExecutionPlan.DirectPackets, direct => Assert.True(direct.Sent));
		Assert.Equal([nameof(SmSystemMessage), nameof(SmFindGroup)], evidence.ExecutionPlan.DirectPackets.Select(direct => direct.PacketType));
		Assert.Equal([recruiter.ObjectId, recruiter.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
		var snapshot = Assert.Single(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 0x01020306).Recruitments);
		Assert.Equal("Need healer", snapshot.Message);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionThreeUpdateRecruitmentWithoutPackets()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddRecruitment(
			recruiter,
			message: "Old",
			groupType: 2,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(3);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteC(5);
				buffer.WriteC(6);
				buffer.WriteC(7);
				buffer.WriteC(8);
				buffer.WriteS("New");
				buffer.WriteC(4);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			recruiter,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(3, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateRecruitment, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.DirectPacketIntents);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(evidence.ExecutionPlan.DirectPackets);
		Assert.Empty(evidence.ExecutionPlan.WorldBroadcasts);
		Assert.Empty(registry.DirectSends);
		var snapshot = Assert.Single(findGroupService.ShowRecruitments("ELYOS", nowEpochSeconds: 0x01020307).Recruitments);
		Assert.Equal("New", snapshot.Message);
		Assert.Equal((byte)4, snapshot.GroupType);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionFourApplicationShowAsDirectPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var otherRace = CreatePlayer(0x01020305, "OtherRace", "ASMODIANS");
		registry.OnlineDirectRecipients.Add(viewer.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddApplication(
			viewer,
			message: "Need group",
			groupType: 2,
			classId: 5,
			level: 65,
			nowEpochSeconds: 0x01020305);
		findGroupService.AddApplication(
			otherRace,
			message: "Other",
			groupType: 3,
			classId: 5,
			level: 60,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(4));

		var compositionPlan = compositionService.CreateDisabledPlan(
			viewer,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(4, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowApplications, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(4, applications))", intent.JavaSource);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionFiveApplicationWorldBroadcastWithRaceFilter()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var sameRace = CreatePlayer(0x01020305, "SameRace", "ELYOS");
		var otherRace = CreatePlayer(0x01020306, "OtherRace", "ASMODIANS");
		registry.WorldPlayers.AddRange([sameRace, otherRace]);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddApplication(
			applicant,
			message: "Need group",
			groupType: 2,
			classId: 5,
			level: 65,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(5);
				buffer.WriteD(applicant.ObjectId);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			applicant,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(5, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveApplication, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.DirectPacketIntents);
		var intent = Assert.Single(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Equal("ELYOS", intent.Race);
		Assert.Equal("PacketSendUtility.broadcastToWorld(..., p -> p.getRace() == application.getPlayer().getRace())", intent.JavaSource);
		Assert.Empty(evidence.ExecutionPlan.DirectPackets);
		var broadcast = Assert.Single(evidence.ExecutionPlan.WorldBroadcasts);
		Assert.Equal("ELYOS", broadcast.Race);
		Assert.Equal(nameof(SmFindGroup), broadcast.PacketType);
		Assert.Equal("p -> p.getRace() == recorded race", broadcast.JavaFilter);
		Assert.Equal(1, broadcast.SentCount);
		var recorded = Assert.Single(registry.WorldBroadcasts);
		Assert.Equal([sameRace.ObjectId], recorded.RecipientObjectIds);
		Assert.Empty(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 0x01020307).Applications);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionSixAddApplicationAsPostedMessageAndShowList()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		registry.OnlineDirectRecipients.Add(applicant.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
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

		var compositionPlan = compositionService.CreateDisabledPlan(
			applicant,
			packet,
			nowEpochSeconds: 0x01020305);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(6, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.AddApplication, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(2, evidence.IntentPlan.DirectPacketIntents.Count);
		Assert.Collection(
			evidence.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
				Assert.Equal("SM_SYSTEM_MESSAGE.STR_PARTY_MATCH_SEEK_PARTY_POSTED", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(4, applications))", intent.JavaSource);
			});
		Assert.Equal(2, evidence.ExecutionPlan.DirectPackets.Count);
		Assert.All(evidence.ExecutionPlan.DirectPackets, direct => Assert.True(direct.Sent));
		Assert.Equal([nameof(SmSystemMessage), nameof(SmFindGroup)], evidence.ExecutionPlan.DirectPackets.Select(direct => direct.PacketType));
		Assert.Equal([applicant.ObjectId, applicant.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
		var snapshot = Assert.Single(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 0x01020306).Applications);
		Assert.Equal("Need group", snapshot.Message);
		Assert.Equal((byte)2, snapshot.GroupType);
		Assert.Equal((byte)5, snapshot.ClassId);
		Assert.Equal((byte)65, snapshot.Level);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionSevenUpdateApplicationWithoutPackets()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.AddApplication(
			applicant,
			message: "Old",
			groupType: 1,
			classId: 5,
			level: 64,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(7);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("New");
				buffer.WriteC(3);
				buffer.WriteC(10);
				buffer.WriteC(65);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			applicant,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(7, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateApplication, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.DirectPacketIntents);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(evidence.ExecutionPlan.DirectPackets);
		Assert.Empty(evidence.ExecutionPlan.WorldBroadcasts);
		Assert.Empty(registry.DirectSends);
		var snapshot = Assert.Single(findGroupService.ShowApplications("ELYOS", nowEpochSeconds: 0x01020307).Applications);
		Assert.Equal("New", snapshot.Message);
		Assert.Equal((byte)3, snapshot.GroupType);
		Assert.Equal((byte)10, snapshot.ClassId);
		Assert.Equal((byte)65, snapshot.Level);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionEightRegisterInstanceGroupAsDirectPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		registry.OnlineDirectRecipients.Add(recruiter.ObjectId);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService()));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(8);
				buffer.WriteD(0x11223344);
				buffer.WriteC(0);
				buffer.WriteS("Entry");
				buffer.WriteC(3);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			recruiter,
			packet,
			nowEpochSeconds: 0x01020305);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(8, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.RegisterInstanceGroup, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(14, List.of(instanceGroup)))", intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(recruiter.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([recruiter.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionNineRemoveInstanceGroupAsUpdatedShowList()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var removed = CreatePlayer(0x01020304, "Removed", "ELYOS");
		var remaining = CreatePlayer(0x01020307, "Remaining", "ELYOS");
		registry.OnlineDirectRecipients.Add(removed.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(removed, 0x11223344, "Removed", minMembers: 3, nowEpochSeconds: 0x01020305);
		findGroupService.RegisterInstanceGroup(remaining, 0x11223345, "Remaining", minMembers: 2, nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(9);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteD(0x11223344);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			removed,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(9, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.RemoveInstanceGroup, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(removed.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(removed.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		var remainingSnapshot = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 0x01020307).InstanceGroups);
		Assert.Equal(remaining.ObjectId, remainingSnapshot.GroupEntryId);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionTenInstanceGroupShowWithEnableRegisterPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		var otherRace = CreatePlayer(0x01020308, "OtherRace", "ASMODIANS");
		registry.OnlineDirectRecipients.Add(viewer.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			recruiter,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 3,
			nowEpochSeconds: 0x01020305);
		findGroupService.RegisterInstanceGroup(
			otherRace,
			instanceMaskId: 0x11223345,
			message: "Other",
			minMembers: 2,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(10));

		var compositionPlan = compositionService.CreateDisabledPlan(
			viewer,
			packet,
			nowEpochSeconds: 0x01020306,
			formInstanceGroupAnywhere: true,
			targetNpcInstanceMaskIds: [0x11223344],
			allRecruitableInstanceMaskIds: [0x11223344, 0x11223345]);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(10, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroups, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.False(evidence.IntentPlan.IsCmFindGroupBoundaryWired);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Equal(2, evidence.IntentPlan.DirectPacketIntents.Count);
		Assert.Collection(
			evidence.IntentPlan.DirectPacketIntents,
			intent =>
			{
				Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(instanceMaskIds))", intent.JavaSource);
			},
			intent =>
			{
				Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
				Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
			});
		Assert.Equal(2, evidence.ExecutionPlan.DirectPackets.Count);
		Assert.All(evidence.ExecutionPlan.DirectPackets, direct =>
		{
			Assert.True(direct.Sent);
			Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
			Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		});
		Assert.Equal([viewer.ObjectId, viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionThirteenInstanceGroupUpdateWithoutEnableRegisterPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var viewer = CreatePlayer(0x01020304, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		registry.OnlineDirectRecipients.Add(viewer.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			recruiter,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 3,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(13));

		var compositionPlan = compositionService.CreateDisabledPlan(
			viewer,
			packet,
			nowEpochSeconds: 0x01020306,
			formInstanceGroupAnywhere: true,
			targetNpcInstanceMaskIds: [0x11223344],
			allRecruitableInstanceMaskIds: [0x11223344, 0x11223345]);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(13, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupsUpdate, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionElevenInstanceApplicationAsDirectPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var recruiter = CreatePlayer(0x01020307, "Recruiter", "ELYOS");
		registry.OnlineDirectRecipients.Add(recruiter.ObjectId);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService()));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(11);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteD(0x11223344);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			applicant,
			packet,
			nowEpochSeconds: 0x01020305,
			resolvePlayer: Resolve(applicant, recruiter));
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(11, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplication, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Null(evidence.IntentPlan.InviteIntent);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(applicant))", intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(recruiter.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Equal([recruiter.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionTwelveDeclineAsWhisperDirectPacket()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		registry.OnlineDirectRecipients.Add(applicant.ObjectId);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService()));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(0);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			responder,
			packet,
			nowEpochSeconds: 0x01020305,
			resolvePlayer: Resolve(responder, applicant));
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(12, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Null(evidence.IntentPlan.InviteIntent);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(applicant.ObjectId, intent.RecipientObjectId);
		Assert.Equal(
			"PacketSendUtility.sendPacket(applicant, new SM_MESSAGE(responder, ChatUtil.l10n(1400217), ChatType.WHISPER))",
			intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(applicant.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmMessage), direct.PacketType);
		Assert.Equal([applicant.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public void CreateIntentPlan_ComposesParsedActionTwelveAcceptAsGroupInviteIntent()
	{
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 6,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			responder,
			packet,
			nowEpochSeconds: 0x01020306,
			resolvePlayer: Resolve(responder, applicant));
		var intentPlan = FindGroupConnectionBoundarySideEffectCompositionEvidenceService.CreateIntentPlan(compositionPlan);
		var invitePlan = new FindGroupInstanceApplicationInviteDispatchPlanService().CreateDisabledPlan(
			intentPlan.InviteIntent,
			Resolve(responder, applicant),
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(12, intentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, intentPlan.ClientActionKind);
		Assert.False(intentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(intentPlan.DirectPacketIntents);
		Assert.Empty(intentPlan.WorldBroadcastIntents);
		Assert.NotNull(intentPlan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Group, intentPlan.InviteIntent!.Kind);
		Assert.Equal(responder.ObjectId, intentPlan.InviteIntent.InviterObjectId);
		Assert.Equal(applicant.ObjectId, intentPlan.InviteIntent.InvitedObjectId);
		Assert.Equal("PlayerGroupService.inviteToGroup(responder, applicant)", intentPlan.InviteIntent.JavaSource);
		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.GroupInvitePlanned, invitePlan.Status);
		Assert.Equal(GroupInviteRequestStatus.Requested, invitePlan.GroupInviteRequest?.Status);
		Assert.Equal(SmQuestionWindow.PartyInvite, invitePlan.GroupInviteRequest?.QuestionWindow?.Code);
		Assert.False(invitePlan.DispatchLiveSideEffects);
	}

	[Fact]
	public void CreateIntentPlan_ComposesParsedActionTwelveAcceptAsAllianceInviteIntent()
	{
		var responder = CreatePlayer(0x01020307, "Responder", "ELYOS");
		var applicant = CreatePlayer(0x01020304, "Applicant", "ELYOS");
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			responder,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 7,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(12);
				buffer.WriteD(applicant.ObjectId);
				buffer.WriteC(1);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			responder,
			packet,
			nowEpochSeconds: 0x01020306,
			resolvePlayer: Resolve(responder, applicant));
		var intentPlan = FindGroupConnectionBoundarySideEffectCompositionEvidenceService.CreateIntentPlan(compositionPlan);
		var invitePlan = new FindGroupInstanceApplicationInviteDispatchPlanService().CreateDisabledPlan(
			intentPlan.InviteIntent,
			Resolve(responder, applicant),
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime());

		Assert.Equal(12, intentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.SendInstanceApplicationResult, intentPlan.ClientActionKind);
		Assert.False(intentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(intentPlan.DirectPacketIntents);
		Assert.Empty(intentPlan.WorldBroadcastIntents);
		Assert.NotNull(intentPlan.InviteIntent);
		Assert.Equal(FindGroupInstanceInviteKind.Alliance, intentPlan.InviteIntent!.Kind);
		Assert.Equal(responder.ObjectId, intentPlan.InviteIntent.InviterObjectId);
		Assert.Equal(applicant.ObjectId, intentPlan.InviteIntent.InvitedObjectId);
		Assert.Equal("PlayerAllianceService.inviteToAlliance(responder, applicant)", intentPlan.InviteIntent.JavaSource);
		Assert.Equal(FindGroupInstanceApplicationInviteDispatchStatus.AllianceInvitePlanned, invitePlan.Status);
		Assert.Equal(AllianceInviteRequestStatus.Requested, invitePlan.AllianceInviteRequest?.Status);
		Assert.Equal(SmQuestionWindow.AllianceInvite, invitePlan.AllianceInviteRequest?.QuestionWindow?.Code);
		Assert.False(invitePlan.DispatchLiveSideEffects);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionSeventeenUpdateInstanceGroupAsUpdatedShowList()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		registry.OnlineDirectRecipients.Add(recruiter.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			recruiter,
			instanceMaskId: 0x11223344,
			message: "Old",
			minMembers: 3,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(17);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteD(0x11223344);
				buffer.WriteS("New");
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			recruiter,
			packet,
			nowEpochSeconds: 0x01020306);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(17, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.UpdateInstanceGroup, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(recruiter.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(10, instanceGroups))", intent.JavaSource);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(recruiter.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		var snapshot = Assert.Single(findGroupService.ShowInstanceGroups("ELYOS", nowEpochSeconds: 0x01020307).InstanceGroups);
		Assert.Equal("New", snapshot.Message);
	}

	[Fact]
	public async Task ExecuteOptInAsync_ComposesParsedActionFifteenPlanAndExecutorResult()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var viewer = CreatePlayer(0x01020307, "Viewer", "ELYOS");
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS");
		recruiter.Position = new WorldPosition(300110000, 0, 0, 0, 0);
		registry.OnlineDirectRecipients.Add(viewer.ObjectId);
		var findGroupService = new FindGroupRecruitmentPlanService();
		findGroupService.RegisterInstanceGroup(
			recruiter,
			instanceMaskId: 0x11223344,
			message: "Entry",
			minMembers: 3,
			nowEpochSeconds: 0x01020305);
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(findGroupService));
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(15);
				buffer.WriteD(recruiter.ObjectId);
				buffer.WriteD(0x11223344);
			});

		var compositionPlan = compositionService.CreateDisabledPlan(
			viewer,
			packet,
			nowEpochSeconds: 0x01020305);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(FindGroupConnectionClientActionCompositionStatus.ComposedDisabledPlan, evidence.IntentPlan.CompositionStatus);
		Assert.Equal(15, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ShowInstanceGroupMembersInfo, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.False(evidence.IntentPlan.IsCmFindGroupBoundaryWired);
		Assert.False(evidence.IsCmFindGroupBoundaryWired);
		var intent = Assert.Single(evidence.IntentPlan.DirectPacketIntents);
		Assert.Equal(viewer.ObjectId, intent.RecipientObjectId);
		Assert.Equal("PacketSendUtility.sendPacket(player, new SM_FIND_GROUP(16, List.of(instanceGroup)))", intent.JavaSource);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		var direct = Assert.Single(evidence.ExecutionPlan.DirectPackets);
		Assert.True(direct.Sent);
		Assert.Equal(viewer.ObjectId, direct.RecipientObjectId);
		Assert.Equal(nameof(SmFindGroup), direct.PacketType);
		Assert.Contains("not by GameServerConnection.CmFindGroup", evidence.BoundaryNote, StringComparison.Ordinal);
		Assert.Equal([viewer.ObjectId], registry.DirectSends.Select(send => send.RecipientObjectId));
	}

	[Fact]
	public async Task ExecuteOptInAsync_LeavesParsedButNoRunImplActionWithoutSideEffects()
	{
		var registry = new FakeGameClientConnectionRegistry();
		var player = CreatePlayer(0x01020307, "Player", "ELYOS");
		var compositionService = new FindGroupConnectionClientActionCompositionPlanService(
			new FindGroupClientActionPlanService(new FindGroupRecruitmentPlanService()));
		var packet = CreateFindGroupPacket(buffer => buffer.WriteC(20));

		var compositionPlan = compositionService.CreateDisabledPlan(
			player,
			packet,
			nowEpochSeconds: 0x01020305);
		var evidence = await FindGroupConnectionBoundarySideEffectCompositionEvidenceService.ExecuteOptInAsync(
			compositionPlan,
			new FindGroupSideEffectDispatchExecutorService(registry));

		Assert.Equal(20, evidence.IntentPlan.Action);
		Assert.Equal(FindGroupClientActionPlanKind.ParsedButNoRunImpl, evidence.IntentPlan.ClientActionKind);
		Assert.False(evidence.IntentPlan.ShouldDispatchLiveSideEffects);
		Assert.Empty(evidence.IntentPlan.DirectPacketIntents);
		Assert.Empty(evidence.IntentPlan.WorldBroadcastIntents);
		Assert.Empty(evidence.ExecutionPlan.DirectPackets);
		Assert.Empty(evidence.ExecutionPlan.WorldBroadcasts);
		Assert.Empty(registry.DirectSends);
		Assert.Empty(registry.WorldBroadcasts);
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
			PlayerClass = "RANGER",
			Level = 65,
			Position = new WorldPosition(210010000, 11, 22, 33, 0),
		};
	}

	private static Func<int, Player?> Resolve(params Player[] players)
	{
		return objectId => players.FirstOrDefault(player => player.ObjectId == objectId);
	}

	private sealed class FakeGameClientConnectionRegistry : IGameClientConnectionRegistry
	{
		public HashSet<int> OnlineDirectRecipients { get; } = [];
		public List<Player> WorldPlayers { get; } = [];
		public List<DirectSendRecord> DirectSends { get; } = [];
		public List<WorldBroadcastRecord> WorldBroadcasts { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = WorldPlayers.FirstOrDefault(entry => string.Equals(entry.Name, playerName, StringComparison.OrdinalIgnoreCase));
			return player != null;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
			foreach (var player in WorldPlayers)
				action(player);
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			DirectSends.Add(new DirectSendRecord(playerObjectId, packet));
			return Task.FromResult(OnlineDirectRecipients.Contains(playerObjectId));
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			var recipients = WorldPlayers
				.Where(player => filter == null || filter(player))
				.Select(player => player.ObjectId)
				.ToArray();
			WorldBroadcasts.Add(new WorldBroadcastRecord(packet, recipients));
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

	private sealed record DirectSendRecord(int RecipientObjectId, GameServerPacket Packet);

	private sealed record WorldBroadcastRecord(GameServerPacket Packet, IReadOnlyList<int> RecipientObjectIds);
}
