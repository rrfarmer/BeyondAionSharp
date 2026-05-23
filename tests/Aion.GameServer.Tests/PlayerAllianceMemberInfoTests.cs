using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceMemberInfoTests
{
	[Fact]
	public void PlayerAllianceEvent_JavaIdsMatchLegacyEnum()
	{
		Assert.Equal(0, (int)PlayerAllianceEvent.Leave);
		Assert.Equal(0, (int)PlayerAllianceEvent.Banned);
		Assert.Equal(1, (int)PlayerAllianceEvent.Movement);
		Assert.Equal(3, (int)PlayerAllianceEvent.Disconnected);
		Assert.Equal(5, (int)PlayerAllianceEvent.Join);
		Assert.Equal(5, (int)PlayerAllianceEvent.MemberGroupChange);
		Assert.Equal(7, (int)PlayerAllianceEvent.EnterOffline);
		Assert.Equal(65, (int)PlayerAllianceEvent.UpdateEffects);
		Assert.Equal(13, (int)PlayerAllianceEvent.Reconnect);
		Assert.Equal(13, (int)PlayerAllianceEvent.Enter);
		Assert.Equal(13, (int)PlayerAllianceEvent.Update);
		Assert.Equal(13, (int)PlayerAllianceEvent.AppointCaptain);
	}

	[Fact]
	public void PlayerAllianceMemberInfoEvent_PreservesJavaConstantIdentityForSameWireIds()
	{
		Assert.Equal(5, PlayerAllianceMemberInfoEvent.Join.WireId);
		Assert.Equal(5, PlayerAllianceMemberInfoEvent.MemberGroupChange.WireId);
		Assert.Equal(PlayerAllianceEvent.Join, PlayerAllianceMemberInfoEvent.Join.LegacyEvent);
		Assert.Equal(PlayerAllianceEvent.MemberGroupChange, PlayerAllianceMemberInfoEvent.MemberGroupChange.LegacyEvent);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.Join, PlayerAllianceMemberInfoEvent.Join.Kind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, PlayerAllianceMemberInfoEvent.MemberGroupChange.Kind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.Join, PlayerAllianceMemberInfoEvent.FromLegacyEvent(PlayerAllianceEvent.Join).Kind);
	}

	[Fact]
	public void CreateMovementUpdatePlan_ReturnsAllExceptPlayerIntentsLikeJavaPlayerAllianceUpdateEvent()
	{
		var planner = new PlayerAllianceMovementUpdatePlanner();
		var leader = new Player { ObjectId = 1001, Name = "Leader", IsOnline = true };
		var subject = new Player
		{
			ObjectId = 1002,
			Name = "Subject",
			IsOnline = true,
			PlayerClass = "CLERIC",
			Gender = "FEMALE",
			Level = 45,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var other = new Player { ObjectId = 1003, Name = "Other", IsOnline = true };

		var plan = Assert.IsType<PlayerAllianceMemberInfoUpdatePlan>(
			planner.CreateMovementUpdatePlan(88001, [leader, subject, other], subject));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1002, plan.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.Event);
		Assert.Equal(0, plan.Slot);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertMovementIntent(intent, recipientObjectId: 1001, subjectObjectId: 1002),
			intent => AssertMovementIntent(intent, recipientObjectId: 1003, subjectObjectId: 1002));
		Assert.DoesNotContain(plan.MemberInfoIntents, intent => intent.RecipientObjectId == subject.ObjectId);
	}

	[Fact]
	public void CreateMovementUpdatePlan_ReturnsNullForMissingAllianceMember()
	{
		var planner = new PlayerAllianceMovementUpdatePlanner();
		var member = new Player { ObjectId = 1001, IsOnline = true };
		var outsider = new Player { ObjectId = 1002, IsOnline = true };

		var plan = planner.CreateMovementUpdatePlan(88001, [member], outsider);

		Assert.Null(plan);
	}

	[Fact]
	public void SmAllianceMemberInfo_WritesOnlineNameZeroEffectBranchesLikeJava()
	{
		var member = new Player
		{
			ObjectId = 1001,
			Name = "Online",
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var plan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceEvent.Join);

		Assert.Equal(PlayerAllianceEvent.Join, plan.RequestedEvent);
		Assert.Equal(PlayerAllianceEvent.Join, plan.EffectiveEvent);
		Assert.True(plan.WritesName);
		Assert.True(plan.WritesAbnormalEffects);
		Assert.True(plan.WritesSlotTimers);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmAllianceMemberInfo(plan)));
		SkipAllianceMemberInfoPrefix(reader, expectedClassId: 5, expectedGenderId: 0, expectedLevel: 40, expectedEventId: (int)PlayerAllianceEvent.Join);
		Assert.Equal("Online", reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(127, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		for (var i = 0; i < 8; i++)
			Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmAllianceMemberInfo_WritesMemberGroupChangeNameOnlyDespiteSharedJoinWireId()
	{
		var member = new Player
		{
			ObjectId = 1001,
			Name = "Shifted",
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var joinPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceMemberInfoEvent.Join);
		var groupChangePlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceMemberInfoEvent.MemberGroupChange);

		Assert.Equal(PlayerAllianceMemberInfoEventKind.Join, joinPlan.EffectiveEventKind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, groupChangePlan.EffectiveEventKind);
		Assert.Equal((int)PlayerAllianceEvent.Join, joinPlan.PrefixSnapshot.EventId);
		Assert.Equal((int)PlayerAllianceEvent.MemberGroupChange, groupChangePlan.PrefixSnapshot.EventId);
		Assert.True(joinPlan.WritesAbnormalEffects);
		Assert.True(joinPlan.WritesSlotTimers);
		Assert.False(groupChangePlan.WritesAbnormalEffects);
		Assert.False(groupChangePlan.WritesSlotTimers);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmAllianceMemberInfo(groupChangePlan)));
		SkipAllianceMemberInfoPrefix(reader, expectedClassId: 5, expectedGenderId: 0, expectedLevel: 40, expectedEventId: (int)PlayerAllianceEvent.MemberGroupChange);
		Assert.Equal("Shifted", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void MemberGroupChangePlanner_PlansSingleMovePacketLikeJavaChangeMemberGroupEvent()
	{
		var planner = new PlayerAllianceMemberGroupChangePlanner();
		var firstMember = new Player
		{
			ObjectId = 1001,
			Name = "First",
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var secondMember = new Player { ObjectId = 1002, Name = "Second", IsOnline = true };

		var plan = Assert.IsType<PlayerAllianceMemberGroupChangePlan>(
			planner.CreateMemberGroupChangePlan(88001, [firstMember, secondMember], firstMemberObjectId: 1001, secondMemberObjectId: 0, targetAllianceGroupId: 3));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1001, plan.FirstMemberObjectId);
		Assert.Equal(0, plan.SecondMemberObjectId);
		Assert.Equal(3, plan.TargetAllianceGroupId);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertMemberGroupChangeIntent(intent, expectedSubjectObjectId: 1001, expectedName: "First"));
	}

	[Fact]
	public void MemberGroupChangePlanner_PlansSwapPacketsLikeJavaChangeMemberGroupEvent()
	{
		var planner = new PlayerAllianceMemberGroupChangePlanner();
		var firstMember = new Player
		{
			ObjectId = 1001,
			Name = "First",
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var secondMember = new Player
		{
			ObjectId = 1002,
			Name = "Second",
			IsOnline = true,
			PlayerClass = "CLERIC",
			Gender = "FEMALE",
			Level = 45,
			Position = new WorldPosition(220010000, 44, 55, 66, 64),
		};

		var plan = Assert.IsType<PlayerAllianceMemberGroupChangePlan>(
			planner.CreateMemberGroupChangePlan(88001, [firstMember, secondMember], firstMemberObjectId: 1001, secondMemberObjectId: 1002, targetAllianceGroupId: 0));

		Assert.Equal(1001, plan.FirstMemberObjectId);
		Assert.Equal(1002, plan.SecondMemberObjectId);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent => AssertMemberGroupChangeIntent(intent, expectedSubjectObjectId: 1001, expectedName: "First"),
			intent => AssertMemberGroupChangeIntent(intent, expectedSubjectObjectId: 1002, expectedName: "Second", expectedClassId: 10, expectedGenderId: 1, expectedLevel: 45));
	}

	[Fact]
	public void MemberGroupChangePlanner_ReturnsNullWhenAffectedMemberIsMissing()
	{
		var planner = new PlayerAllianceMemberGroupChangePlanner();
		var firstMember = new Player { ObjectId = 1001, Name = "First", IsOnline = true };

		Assert.Null(planner.CreateMemberGroupChangePlan(88001, [firstMember], firstMemberObjectId: 404, secondMemberObjectId: 0, targetAllianceGroupId: 3));
		Assert.Null(planner.CreateMemberGroupChangePlan(88001, [firstMember], firstMemberObjectId: 1001, secondMemberObjectId: 404, targetAllianceGroupId: 0));
	}

	[Fact]
	public void ViceCaptainAssignmentPlanner_PlansPromoteAllianceInfoBroadcastLikeJavaAssignViceCaptainEvent()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var target = CreateAllianceMember(1002, "Target", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);

		var plan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, target, other],
			currentViceCaptainObjectIds: [],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.Promote);

		Assert.Equal(PlayerAllianceRolePlanStatus.Planned, plan.Status);
		Assert.Equal([1002], plan.ViceCaptainObjectIdsAfterEvent);
		Assert.Null(plan.SystemMessageIntent);
		Assert.Collection(
			plan.AllianceInfoIntents,
			intent => AssertViceCaptainInfoIntent(intent, 1001, 3, 88001, 1001, 210010000, [1002, 0, 0, 0], PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId, "Target"),
			intent => AssertViceCaptainInfoIntent(intent, 1002, 3, 88001, 1001, 220010000, [1002, 0, 0, 0], PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId, "Target"),
			intent => AssertViceCaptainInfoIntent(intent, 1003, 3, 88001, 1001, 230010000, [1002, 0, 0, 0], PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId, "Target"));
	}

	[Fact]
	public void ViceCaptainAssignmentPlanner_PlansDemoteAllianceInfoBroadcastLikeJavaAssignViceCaptainEvent()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var target = CreateAllianceMember(1002, "Target", worldId: 220010000);

		var plan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, target],
			currentViceCaptainObjectIds: [1002, 1004],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.Demote);

		Assert.Equal(PlayerAllianceRolePlanStatus.Planned, plan.Status);
		Assert.Equal([1004], plan.ViceCaptainObjectIdsAfterEvent);
		Assert.Collection(
			plan.AllianceInfoIntents,
			intent => AssertViceCaptainInfoIntent(intent, 1001, 2, 88001, 1001, 210010000, [1004, 0, 0, 0], PlayerAllianceInfoPacketPlan.ViceCaptainDemoteMessageId, "Target"),
			intent => AssertViceCaptainInfoIntent(intent, 1002, 2, 88001, 1001, 220010000, [1004, 0, 0, 0], PlayerAllianceInfoPacketPlan.ViceCaptainDemoteMessageId, "Target"));
	}

	[Fact]
	public void ViceCaptainAssignmentPlanner_PlansCaptainDemotionWithEmptyMessageLikeJavaAssignViceCaptainEvent()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var oldLeader = CreateAllianceMember(1002, "OldLeader", worldId: 220010000);

		var plan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, oldLeader],
			currentViceCaptainObjectIds: [1003, 1004],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.DemoteCaptainToViceCaptain,
			isInLeague: true,
			leagueId: 77001);

		Assert.Equal(PlayerAllianceRolePlanStatus.Planned, plan.Status);
		Assert.True(plan.WouldBroadcastLeague);
		Assert.Equal([1003, 1004, 1002], plan.ViceCaptainObjectIdsAfterEvent);
		Assert.Collection(
			plan.AllianceInfoIntents,
			intent => AssertViceCaptainInfoIntent(intent, 1001, 2, 88001, 1001, 210010000, [1003, 1004, 1002, 0], messageId: 0, expectedMessage: string.Empty, expectedLeagueId: 77001),
			intent => AssertViceCaptainInfoIntent(intent, 1002, 2, 88001, 1001, 220010000, [1003, 1004, 1002, 0], messageId: 0, expectedMessage: string.Empty, expectedLeagueId: 77001));
	}

	[Fact]
	public void ViceCaptainAssignmentPlanner_ReturnsLeaderSystemMessageWhenPromoteLimitReachedLikeJava()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var target = CreateAllianceMember(1002, "Target", worldId: 220010000);

		var plan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, target],
			currentViceCaptainObjectIds: [1003, 1004, 1005, 1006],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.Promote);

		Assert.Equal(PlayerAllianceRolePlanStatus.PromoteLimitReached, plan.Status);
		Assert.Empty(plan.AllianceInfoIntents);
		var systemMessage = Assert.IsType<PlayerAllianceSystemMessageIntent>(plan.SystemMessageIntent);
		Assert.Equal(1001, systemMessage.RecipientObjectId);
		Assert.Equal(1301061, systemMessage.Message.MessageId);
	}

	[Fact]
	public void ViceCaptainAssignmentPlanner_SkipsMissingOrOfflineEventPlayerLikeJavaCheckCondition()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var offlineTarget = CreateAllianceMember(1002, "Target", worldId: 220010000);
		offlineTarget.IsOnline = false;

		var missingPlan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader],
			currentViceCaptainObjectIds: [1003],
			eventPlayerObjectId: 404,
			PlayerAllianceAssignType.Demote);
		var offlinePlan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, offlineTarget],
			currentViceCaptainObjectIds: [1002],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.Demote);

		Assert.Equal(PlayerAllianceRolePlanStatus.EventPlayerMissing, missingPlan.Status);
		Assert.Empty(missingPlan.AllianceInfoIntents);
		Assert.Equal([1003], missingPlan.ViceCaptainObjectIdsAfterEvent);
		Assert.Equal(PlayerAllianceRolePlanStatus.EventPlayerOffline, offlinePlan.Status);
		Assert.Empty(offlinePlan.AllianceInfoIntents);
		Assert.Equal([1002], offlinePlan.ViceCaptainObjectIdsAfterEvent);
	}

	[Fact]
	public void SmAllianceInfo_WritesViceCaptainPromotePayloadLikeJava()
	{
		var planner = new PlayerAllianceViceCaptainAssignmentPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var target = CreateAllianceMember(1002, "Target", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);
		var plan = planner.CreateAssignmentPlan(
			88001,
			leaderObjectId: 1001,
			[leader, target, other],
			currentViceCaptainObjectIds: [],
			eventPlayerObjectId: 1002,
			PlayerAllianceAssignType.Promote);

		var packet = Assert.IsType<SmAllianceInfo>(plan.AllianceInfoIntents[0].CreatePacket());
		AssertAllianceInfoPacketPayload(
			packet,
			expectedAllianceGroupSize: 3,
			expectedAllianceId: 88001,
			expectedLeaderObjectId: 1001,
			expectedActivePlayerMapId: 210010000,
			expectedPaddedViceCaptainIds: [1002, 0, 0, 0],
			expectedMessageId: PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId,
			expectedMessage: "Target");
	}

	[Fact]
	public void SmAllianceInfo_WritesMessageIdZeroEmptyMessageLikeJava()
	{
		var plan = PlayerAllianceInfoPacketPlan.FromSnapshot(
			allianceId: 88001,
			leaderObjectId: 1001,
			allianceGroupSize: 2,
			activePlayerMapId: 210010000,
			viceCaptainObjectIds: [1003, 1004, 1002],
			PlayerGroupLootRules.Default(),
			PlayerAllianceTeamType.Alliance,
			messageId: 0,
			message: "OldLeader");

		AssertAllianceInfoPacketPayload(
			new SmAllianceInfo(plan),
			expectedAllianceGroupSize: 2,
			expectedAllianceId: 88001,
			expectedLeaderObjectId: 1001,
			expectedActivePlayerMapId: 210010000,
			expectedPaddedViceCaptainIds: [1003, 1004, 1002, 0],
			expectedMessageId: 0,
			expectedMessage: string.Empty);
	}

	[Fact]
	public void SmAllianceInfo_RejectsLeagueRowsUntilPorted()
	{
		var plan = PlayerAllianceInfoPacketPlan.FromSnapshot(
			allianceId: 88001,
			leaderObjectId: 1001,
			allianceGroupSize: 2,
			activePlayerMapId: 210010000,
			viceCaptainObjectIds: [1003],
			PlayerGroupLootRules.Default(),
			PlayerAllianceTeamType.Alliance,
			messageId: PlayerAllianceInfoPacketPlan.ViceCaptainPromoteMessageId,
			message: "Target",
			leagueId: 77001);

		Assert.Throws<NotSupportedException>(() => SerializeUnencryptedPayload(new SmAllianceInfo(plan)));
	}

	[Fact]
	public void LeaderChangePlanner_PlansNonLeagueAllianceInfoAndSystemMessagesLikeJavaChangeAllianceLeaderEvent()
	{
		var planner = new PlayerAllianceLeaderChangePlanner();
		var oldLeader = CreateAllianceMember(1001, "OldLeader", worldId: 210010000);
		var newLeader = CreateAllianceMember(1002, "NewLeader", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);

		var plan = planner.CreateLeaderChangePlan(
			88001,
			oldLeaderObjectId: 1001,
			[oldLeader, newLeader, other],
			currentViceCaptainObjectIds: [1002, 1004],
			newLeaderObjectId: 1002,
			eventPlayerWasSpecified: true);

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1001, plan.OldLeaderObjectId);
		Assert.Equal(1002, plan.NewLeaderObjectId);
		Assert.True(plan.EventPlayerWasSpecified);
		Assert.False(plan.WouldBroadcastLeague);
		Assert.Equal([1004], plan.ViceCaptainObjectIdsAfterEvent);
		Assert.Collection(
			plan.AllianceInfoIntents,
			intent => AssertAllianceInfoIntentAndPacket(intent, 1001, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1002, expectedActivePlayerMapId: 210010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceInfoIntentAndPacket(intent, 1002, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1002, expectedActivePlayerMapId: 220010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceInfoIntentAndPacket(intent, 1003, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1002, expectedActivePlayerMapId: 230010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]));
		Assert.Collection(
			plan.SystemMessageIntents,
			intent => AssertSystemMessageIntent(intent, 1001, 1300998),
			intent => AssertSystemMessageIntent(intent, 1002, 1300999),
			intent => AssertSystemMessageIntent(intent, 1003, 1300998));
	}

	[Fact]
	public void LeaderChangePlanner_SkipsHeIsNewLeaderWhenEventPlayerIsMissingLikeJavaLeaveFallback()
	{
		var planner = new PlayerAllianceLeaderChangePlanner();
		var oldLeader = CreateAllianceMember(1001, "OldLeader", worldId: 210010000);
		var newLeader = CreateAllianceMember(1002, "NewLeader", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);

		var plan = planner.CreateLeaderChangePlan(
			88001,
			oldLeaderObjectId: 1001,
			[oldLeader, newLeader, other],
			currentViceCaptainObjectIds: [1002, 1004],
			newLeaderObjectId: 1002,
			eventPlayerWasSpecified: false);

		Assert.Collection(
			plan.SystemMessageIntents,
			intent => AssertSystemMessageIntent(intent, 1002, 1300999));
	}

	[Fact]
	public void LeaderChangePlanner_SkipsAllianceInfoWhenLeagueBroadcastWillHandleItLikeJava()
	{
		var planner = new PlayerAllianceLeaderChangePlanner();
		var oldLeader = CreateAllianceMember(1001, "OldLeader", worldId: 210010000);
		var newLeader = CreateAllianceMember(1002, "NewLeader", worldId: 220010000);

		var plan = planner.CreateLeaderChangePlan(
			88001,
			oldLeaderObjectId: 1001,
			[oldLeader, newLeader],
			currentViceCaptainObjectIds: [1002],
			newLeaderObjectId: 1002,
			eventPlayerWasSpecified: true,
			isInLeague: true);

		Assert.True(plan.WouldBroadcastLeague);
		Assert.Empty(plan.AllianceInfoIntents);
		Assert.Collection(
			plan.SystemMessageIntents,
			intent => AssertSystemMessageIntent(intent, 1001, 1300998),
			intent => AssertSystemMessageIntent(intent, 1002, 1300999));
	}

	[Fact]
	public void ConnectedPlanner_PlansReconnectPacketOrderLikeJavaPlayerConnectedEvent()
	{
		var planner = new PlayerAllianceConnectedPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var connected = CreateAllianceMember(1002, "Connected", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);

		var plan = Assert.IsType<PlayerAllianceConnectedPlan>(
			planner.CreateConnectedPlan(
				88001,
				leaderObjectId: 1001,
				[leader, connected, other],
				currentViceCaptainObjectIds: [1004],
				connectedPlayerObjectId: 1002));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1002, plan.ConnectedPlayerObjectId);
		Assert.Collection(
			plan.PacketIntents,
			intent => AssertAllianceInfoPacketIntent(intent, sequence: 0, recipientObjectId: 1002, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1001, expectedActivePlayerMapId: 220010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 1, recipientObjectId: 1002, subjectObjectId: 1002, expectedName: "Connected", expectedEventKind: PlayerAllianceMemberInfoEventKind.Reconnect),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 2, recipientObjectId: 1001, subjectObjectId: 1002, expectedName: "Connected", expectedEventKind: PlayerAllianceMemberInfoEventKind.Reconnect),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 3, recipientObjectId: 1002, subjectObjectId: 1001, expectedName: "Leader", expectedEventKind: PlayerAllianceMemberInfoEventKind.Reconnect),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 4, recipientObjectId: 1003, subjectObjectId: 1002, expectedName: "Connected", expectedEventKind: PlayerAllianceMemberInfoEventKind.Reconnect),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 5, recipientObjectId: 1002, subjectObjectId: 1003, expectedName: "Other", expectedEventKind: PlayerAllianceMemberInfoEventKind.Reconnect));
	}

	[Fact]
	public void ConnectedPlanner_ReturnsNullForMissingConnectedMemberLikeJavaConditionBoundary()
	{
		var planner = new PlayerAllianceConnectedPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);

		var plan = planner.CreateConnectedPlan(
			88001,
			leaderObjectId: 1001,
			[leader],
			currentViceCaptainObjectIds: [],
			connectedPlayerObjectId: 404);

		Assert.Null(plan);
	}

	[Fact]
	public void EnteredPlanner_PlansJoinAndBackfillPacketOrderLikeJavaPlayerAllianceEnteredEvent()
	{
		var planner = new PlayerAllianceEnteredPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);
		var invited = CreateAllianceMember(1002, "Invited", worldId: 220010000);
		var other = CreateAllianceMember(1003, "Other", worldId: 230010000);

		var plan = Assert.IsType<PlayerAllianceEnteredPlan>(
			planner.CreateEnteredPlan(
				88001,
				leaderObjectId: 1001,
				[leader, invited, other],
				currentViceCaptainObjectIds: [1004],
				invitedPlayerObjectId: 1002));

		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(1002, plan.InvitedPlayerObjectId);
		Assert.True(plan.WouldSendBrands);
		Assert.True(plan.WouldBroadcastAbyssRank);
		Assert.False(plan.WouldBroadcastLeague);
		Assert.Collection(
			plan.PacketIntents,
			intent => AssertAllianceInfoPacketIntent(intent, sequence: 0, recipientObjectId: 1002, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1001, expectedActivePlayerMapId: 220010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceSystemPacketIntent(intent, sequence: 1, recipientObjectId: 1002, expectedMessageId: 1390263),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 2, recipientObjectId: 1002, subjectObjectId: 1002, expectedName: "Invited", expectedEventKind: PlayerAllianceMemberInfoEventKind.Join),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 3, recipientObjectId: 1001, subjectObjectId: 1002, expectedName: "Invited", expectedEventKind: PlayerAllianceMemberInfoEventKind.Join),
			intent => AssertAllianceSystemPacketIntent(intent, sequence: 4, recipientObjectId: 1001, expectedMessageId: 1400013),
			intent => AssertAllianceInfoPacketIntent(intent, sequence: 5, recipientObjectId: 1001, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1001, expectedActivePlayerMapId: 210010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 6, recipientObjectId: 1002, subjectObjectId: 1001, expectedName: "Leader", expectedEventKind: PlayerAllianceMemberInfoEventKind.Enter),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 7, recipientObjectId: 1003, subjectObjectId: 1002, expectedName: "Invited", expectedEventKind: PlayerAllianceMemberInfoEventKind.Join),
			intent => AssertAllianceSystemPacketIntent(intent, sequence: 8, recipientObjectId: 1003, expectedMessageId: 1400013),
			intent => AssertAllianceInfoPacketIntent(intent, sequence: 9, recipientObjectId: 1003, expectedAllianceGroupSize: 3, expectedLeaderObjectId: 1001, expectedActivePlayerMapId: 230010000, expectedPaddedViceCaptainIds: [1004, 0, 0, 0]),
			intent => AssertAllianceMemberInfoPacketIntent(intent, sequence: 10, recipientObjectId: 1002, subjectObjectId: 1003, expectedName: "Other", expectedEventKind: PlayerAllianceMemberInfoEventKind.Enter));
	}

	[Fact]
	public void EnteredPlanner_ReturnsNullWhenInvitedMemberSnapshotIsMissing()
	{
		var planner = new PlayerAllianceEnteredPlanner();
		var leader = CreateAllianceMember(1001, "Leader", worldId: 210010000);

		var plan = planner.CreateEnteredPlan(
			88001,
			leaderObjectId: 1001,
			[leader],
			currentViceCaptainObjectIds: [],
			invitedPlayerObjectId: 404);

		Assert.Null(plan);
	}

	[Fact]
	public void SmAllianceMemberInfo_RewritesOfflineEnterToEnterOfflineLikeJava()
	{
		var member = new Player
		{
			ObjectId = 1001,
			Name = "Offline",
			IsOnline = false,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var plan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceEvent.Enter);

		Assert.Equal(PlayerAllianceEvent.Enter, plan.RequestedEvent);
		Assert.Equal(PlayerAllianceEvent.EnterOffline, plan.EffectiveEvent);
		Assert.True(plan.WritesName);
		Assert.False(plan.WritesAbnormalEffects);
		Assert.False(plan.WritesSlotTimers);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmAllianceMemberInfo(plan)));
		SkipAllianceMemberInfoPrefix(reader, expectedClassId: 5, expectedGenderId: 0, expectedLevel: 40, expectedEventId: (int)PlayerAllianceEvent.EnterOffline);
		Assert.Equal("Offline", reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmAllianceMemberInfo_WritesNonEmptyEffectEntriesLikeJava()
	{
		var member = new Player
		{
			ObjectId = 1001,
			Name = "Effected",
			IsOnline = true,
			PlayerClass = "CHANTER",
			Level = 50,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		var fullSlotEffect = new PlayerGroupMemberEffectInfo(
			EffectorObjectId: 7001,
			SkillId: 1234,
			SkillLevel: 3,
			TargetSlotOrdinal: 2,
			RemainingTimeToDisplayMillis: 45000);
		var targetedEffect = new PlayerGroupMemberEffectInfo(
			EffectorObjectId: 7002,
			SkillId: 5678,
			SkillLevel: 1,
			TargetSlotOrdinal: 4,
			RemainingTimeToDisplayMillis: 90000);
		var enterPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceEvent.Enter) with
		{
			AbnormalEffects = [fullSlotEffect],
		};
		var updateEffectsPlan = PlayerAllianceMemberInfoPacketPlan.FromPlayer(88001, member, PlayerAllianceEvent.UpdateEffects, slot: 4) with
		{
			AbnormalEffects = [targetedEffect],
		};

		AssertAllianceEffectPayload(enterPlan, expectedName: "Effected", expectedSlot: 127, fullSlotEffect);
		AssertAllianceEffectPayload(updateEffectsPlan, expectedName: null, expectedSlot: 4, targetedEffect);
	}

	private static void AssertMovementIntent(
		PlayerAllianceMemberInfoIntent intent,
		int recipientObjectId,
		int subjectObjectId)
	{
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(subjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, intent.Event);
		var plan = Assert.IsType<PlayerAllianceMemberInfoPacketPlan>(intent.PacketPlan);
		Assert.Equal(88001, plan.AllianceId);
		Assert.Equal(subjectObjectId, plan.MemberObjectId);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.RequestedEvent);
		Assert.Equal(PlayerAllianceEvent.Movement, plan.EffectiveEvent);
		Assert.Equal(0, plan.Slot);
		Assert.True(plan.IsOnline);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.Movement, plan.RequestedEventKind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.Movement, plan.EffectiveEventKind);
		Assert.Equal(10, plan.PrefixSnapshot.ClassId);
		Assert.Equal(1, plan.PrefixSnapshot.GenderId);
		Assert.Equal(45, plan.PrefixSnapshot.Level);
		Assert.Equal((int)PlayerAllianceEvent.Movement, plan.PrefixSnapshot.EventId);
		Assert.Equal(1, plan.PrefixSnapshot.AlwaysOne);
		Assert.Equal(0, plan.PrefixSnapshot.AllianceUnknown);
		AssertAllianceMemberInfoMovementPayload(intent.CreatePacket());
	}

	private static void AssertAllianceEffectPayload(
		PlayerAllianceMemberInfoPacketPlan plan,
		string? expectedName,
		int expectedSlot,
		PlayerGroupMemberEffectInfo expectedEffect)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmAllianceMemberInfo(plan)));
		SkipAllianceMemberInfoPrefix(
			reader,
			expectedClassId: 11,
			expectedGenderId: 0,
			expectedLevel: 50,
			expectedEventId: (int)plan.EffectiveEvent);
		if (expectedName != null)
			Assert.Equal(expectedName, reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedSlot, (int)reader.ReadC());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(expectedEffect.EffectorObjectId, reader.ReadD());
		Assert.Equal(expectedEffect.SkillId, reader.ReadH());
		Assert.Equal(expectedEffect.SkillLevel, (int)reader.ReadC());
		Assert.Equal(expectedEffect.TargetSlotOrdinal, (int)reader.ReadC());
		Assert.Equal(expectedEffect.RemainingTimeToDisplayMillis, reader.ReadD());
		for (var i = 0; i < 8; i++)
			Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertMemberGroupChangeIntent(
		PlayerAllianceMemberInfoIntent intent,
		int expectedSubjectObjectId,
		string expectedName,
		int expectedClassId = 5,
		int expectedGenderId = 0,
		int expectedLevel = 40)
	{
		Assert.Equal(0, intent.RecipientObjectId);
		Assert.Equal(expectedSubjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerAllianceEvent.MemberGroupChange, intent.Event);
		var plan = Assert.IsType<PlayerAllianceMemberInfoPacketPlan>(intent.PacketPlan);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, plan.RequestedEventKind);
		Assert.Equal(PlayerAllianceMemberInfoEventKind.MemberGroupChange, plan.EffectiveEventKind);
		Assert.True(plan.WritesName);
		Assert.False(plan.WritesAbnormalEffects);
		Assert.False(plan.WritesSlotTimers);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(Assert.IsType<SmAllianceMemberInfo>(intent.CreatePacket())));
		SkipAllianceMemberInfoPrefix(
			reader,
			expectedClassId,
			expectedGenderId,
			expectedLevel,
			expectedEventId: (int)PlayerAllianceEvent.MemberGroupChange);
		Assert.Equal(expectedName, reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertViceCaptainInfoIntent(
		PlayerAllianceInfoIntent intent,
		int expectedRecipientObjectId,
		int expectedAllianceGroupSize,
		int expectedAllianceId,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		IReadOnlyList<int> expectedPaddedViceCaptainIds,
		int messageId,
		string expectedMessage,
		int expectedLeagueId = 0)
	{
		Assert.Equal(expectedRecipientObjectId, intent.RecipientObjectId);
		Assert.Equal(expectedAllianceGroupSize, intent.PacketPlan.AllianceGroupSize);
		Assert.Equal(expectedAllianceId, intent.PacketPlan.AllianceId);
		Assert.Equal(expectedLeaderObjectId, intent.PacketPlan.LeaderObjectId);
		Assert.Equal(expectedActivePlayerMapId, intent.PacketPlan.ActivePlayerMapId);
		Assert.Equal(expectedPaddedViceCaptainIds, intent.PacketPlan.PaddedViceCaptainObjectIds);
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, intent.PacketPlan.LootRules.LootRule);
		Assert.Equal(0x02, intent.PacketPlan.ConstantGroupInfoMarker);
		Assert.Equal(0x00, intent.PacketPlan.UnknownByte);
		Assert.Equal(0x3F, intent.PacketPlan.TeamType);
		Assert.Equal(0, intent.PacketPlan.TeamSubType);
		Assert.Equal(expectedLeagueId, intent.PacketPlan.LeagueId);
		Assert.Equal(
			[
				new PlayerAllianceInfoGroupPlaceholder(0, 1000),
				new PlayerAllianceInfoGroupPlaceholder(1, 1001),
				new PlayerAllianceInfoGroupPlaceholder(2, 1002),
				new PlayerAllianceInfoGroupPlaceholder(3, 1003),
			],
			intent.PacketPlan.GroupPlaceholders);
		Assert.Equal(messageId, intent.PacketPlan.MessageId);
		Assert.Equal(expectedMessage, intent.PacketPlan.Message);
	}

	private static void AssertAllianceInfoPacketPayload(
		SmAllianceInfo packet,
		int expectedAllianceGroupSize,
		int expectedAllianceId,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		IReadOnlyList<int> expectedPaddedViceCaptainIds,
		int expectedMessageId,
		string expectedMessage)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedAllianceGroupSize, reader.ReadH());
		Assert.Equal(expectedAllianceId, reader.ReadD());
		Assert.Equal(expectedLeaderObjectId, reader.ReadD());
		Assert.Equal(expectedActivePlayerMapId, reader.ReadD());
		for (var i = 0; i < 4; i++)
			Assert.Equal(expectedPaddedViceCaptainIds[i], reader.ReadD());
		Assert.Equal((int)PlayerGroupLootRuleType.RoundRobin, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(0x02, reader.ReadD());
		Assert.Equal(0x00, (int)reader.ReadC());
		Assert.Equal(0x3F, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		for (var i = 0; i < 4; i++)
		{
			Assert.Equal(i, reader.ReadD());
			Assert.Equal(1000 + i, reader.ReadD());
		}

		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedMessage, reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertAllianceInfoIntentAndPacket(
		PlayerAllianceInfoIntent intent,
		int expectedRecipientObjectId,
		int expectedAllianceGroupSize,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		IReadOnlyList<int> expectedPaddedViceCaptainIds)
	{
		Assert.Equal(expectedRecipientObjectId, intent.RecipientObjectId);
		AssertAllianceInfoPacketPayload(
			intent.CreatePacket(),
			expectedAllianceGroupSize,
			expectedAllianceId: 88001,
			expectedLeaderObjectId,
			expectedActivePlayerMapId,
			expectedPaddedViceCaptainIds,
			expectedMessageId: 0,
			expectedMessage: string.Empty);
	}

	private static void AssertAllianceInfoPacketIntent(
		PlayerAlliancePacketIntent intent,
		int sequence,
		int recipientObjectId,
		int expectedAllianceGroupSize,
		int expectedLeaderObjectId,
		int expectedActivePlayerMapId,
		IReadOnlyList<int> expectedPaddedViceCaptainIds)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(PlayerAlliancePacketIntentKind.AllianceInfo, intent.Kind);
		Assert.NotNull(intent.AllianceInfoPlan);
		Assert.Null(intent.MemberInfoPlan);
		Assert.Null(intent.SystemMessage);
		AssertAllianceInfoPacketPayload(
			Assert.IsType<SmAllianceInfo>(intent.CreatePacket()),
			expectedAllianceGroupSize,
			expectedAllianceId: 88001,
			expectedLeaderObjectId,
			expectedActivePlayerMapId,
			expectedPaddedViceCaptainIds,
			expectedMessageId: 0,
			expectedMessage: string.Empty);
	}

	private static void AssertAllianceMemberInfoPacketIntent(
		PlayerAlliancePacketIntent intent,
		int sequence,
		int recipientObjectId,
		int subjectObjectId,
		string expectedName,
		PlayerAllianceMemberInfoEventKind expectedEventKind)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(PlayerAlliancePacketIntentKind.MemberInfo, intent.Kind);
		var plan = Assert.IsType<PlayerAllianceMemberInfoPacketPlan>(intent.MemberInfoPlan);
		Assert.Equal(subjectObjectId, plan.MemberObjectId);
		Assert.Equal(expectedEventKind, plan.RequestedEventKind);
		Assert.Equal(expectedEventKind, plan.EffectiveEventKind);
		Assert.True(plan.WritesName);
		Assert.True(plan.WritesAbnormalEffects);
		Assert.True(plan.WritesSlotTimers);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(Assert.IsType<SmAllianceMemberInfo>(intent.CreatePacket())));
		SkipAllianceMemberInfoPrefix(reader, expectedClassId: 5, expectedGenderId: 0, expectedLevel: 40, expectedEventId: plan.PrefixSnapshot.EventId);
		Assert.Equal(expectedName, reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(127, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		for (var i = 0; i < 8; i++)
			Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertAllianceSystemPacketIntent(
		PlayerAlliancePacketIntent intent,
		int sequence,
		int recipientObjectId,
		int expectedMessageId)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(PlayerAlliancePacketIntentKind.SystemMessage, intent.Kind);
		Assert.Null(intent.AllianceInfoPlan);
		Assert.Null(intent.MemberInfoPlan);
		var message = Assert.IsType<SmSystemMessage>(intent.CreatePacket());
		Assert.Equal(expectedMessageId, message.MessageId);
	}

	private static void AssertSystemMessageIntent(
		PlayerAllianceSystemMessageIntent intent,
		int expectedRecipientObjectId,
		int expectedMessageId)
	{
		Assert.Equal(expectedRecipientObjectId, intent.RecipientObjectId);
		Assert.Equal(expectedMessageId, intent.Message.MessageId);
	}

	private static Player CreateAllianceMember(int objectId, string name, int worldId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = true,
			PlayerClass = "RANGER",
			Level = 40,
			Position = new WorldPosition(worldId, 11, 22, 33, 64),
		};
	}

	private static void AssertAllianceMemberInfoMovementPayload(GameServerPacket? packet)
	{
		var actual = Assert.IsType<SmAllianceMemberInfo>(packet);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(actual));
		SkipAllianceMemberInfoPrefix(reader);
		Assert.Equal(0, reader.Remaining);
	}

	private static void SkipAllianceMemberInfoPrefix(
		PacketBuffer reader,
		int expectedClassId = 10,
		int expectedGenderId = 1,
		int expectedLevel = 45,
		int expectedEventId = (int)PlayerAllianceEvent.Movement)
	{
		for (var i = 0; i < 11; i++)
			reader.ReadD();
		reader.ReadF();
		reader.ReadF();
		reader.ReadF();
		Assert.Equal(expectedClassId, (int)reader.ReadC());
		Assert.Equal(expectedGenderId, (int)reader.ReadC());
		Assert.Equal(expectedLevel, (int)reader.ReadC());
		Assert.Equal(expectedEventId, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
