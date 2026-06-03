using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupDisconnectedPlannerTests
{
	[Fact]
	public void PlanReturnsMissingMemberWhenJavaCheckConditionWouldSkipEvent()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true);
		var member = CreatePlayer(1002, "Member", isOnline: true);
		var stale = CreatePlayer(9999, "Stale", isOnline: false);
		stale.TeamMembership = PlayerTeamMembership.Group;
		stale.CurrentTeamId = 99001;
		runtime.CreateOrUpdateGroup(99001, [leader, member]);
		var planner = new PlayerGroupDisconnectedPlanner(runtime);

		var plan = planner.Plan(stale);

		Assert.False(plan.IsPlanned);
		Assert.Equal(PlayerGroupDisconnectedPlanStatus.MissingMember, plan.Status);
		Assert.Equal(99001, plan.TeamId);
		Assert.Equal(9999, plan.DisconnectedPlayerObjectId);
		Assert.Empty(plan.PacketIntents);
		Assert.Null(plan.LeaderChangePlan);
	}

	[Fact]
	public void PlanFlagsDisbandWhenJavaOnlineMembersAreEmpty()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: false);
		var member = CreatePlayer(1002, "Member", isOnline: false);
		runtime.CreateOrUpdateGroup(99001, [leader, member]);
		var planner = new PlayerGroupDisconnectedPlanner(runtime);

		var plan = planner.Plan(leader);

		Assert.False(plan.IsPlanned);
		Assert.Equal(PlayerGroupDisconnectedPlanStatus.NoOnlineMembersDisband, plan.Status);
		Assert.True(plan.WouldDisbandIfNoOnlineMembersRemain);
		Assert.False(plan.WouldTriggerLeaderChange);
		Assert.Empty(plan.PacketIntents);
		Assert.True(runtime.HasMember(99001, 1001));
		Assert.True(runtime.HasMember(99001, 1002));
	}

	[Fact]
	public void PlanCreatesNonLeaderDisconnectedFanoutLikeJavaPlayerDisconnectedEvent()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: true, worldId: 210010000);
		var disconnected = CreatePlayer(1002, "Disconnected", isOnline: false, worldId: 220010000);
		var other = CreatePlayer(1003, "Other", isOnline: true, worldId: 230010000);
		runtime.CreateOrUpdateGroup(99001, [leader, disconnected, other]);
		var planner = new PlayerGroupDisconnectedPlanner(runtime);

		var plan = planner.Plan(disconnected);

		Assert.True(plan.IsPlanned);
		Assert.Equal(PlayerGroupDisconnectedPlanStatus.Planned, plan.Status);
		Assert.False(plan.WouldDisbandIfNoOnlineMembersRemain);
		Assert.False(plan.WouldTriggerLeaderChange);
		Assert.Null(plan.FallbackLeaderObjectId);
		Assert.Null(plan.LeaderChangePlan);
		Assert.Collection(
			plan.PacketIntents,
			intent => AssertDisconnectedSystemMessage(intent, sequence: 0, recipientObjectId: 1001, subjectObjectId: 1002, "Disconnected"),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 1, recipientObjectId: 1001, subjectObjectId: 1002, isOnline: false),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 2, recipientObjectId: 1002, subjectObjectId: 1001, isOnline: true),
			intent => AssertDisconnectedSystemMessage(intent, sequence: 3, recipientObjectId: 1003, subjectObjectId: 1002, "Disconnected"),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 4, recipientObjectId: 1003, subjectObjectId: 1002, isOnline: false),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 5, recipientObjectId: 1002, subjectObjectId: 1003, isOnline: true));
	}

	[Fact]
	public void PlanCreatesLeaderChangeBeforeDisconnectedFanoutForLeaderLogout()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = CreatePlayer(1001, "Leader", isOnline: false, worldId: 210010000);
		var fallback = CreatePlayer(1002, "Fallback", isOnline: true, worldId: 220010000);
		var other = CreatePlayer(1003, "Other", isOnline: true, worldId: 230010000);
		runtime.CreateOrUpdateGroup(99001, [leader, fallback, other]);
		var planner = new PlayerGroupDisconnectedPlanner(runtime);

		var plan = planner.Plan(leader);

		Assert.True(plan.IsPlanned);
		Assert.True(plan.WouldTriggerLeaderChange);
		Assert.Equal(1002, plan.FallbackLeaderObjectId);
		var leaderChange = Assert.IsType<PlayerGroupLeaderChangePlan>(plan.LeaderChangePlan);
		Assert.Equal(1002, leaderChange.NewLeaderObjectId);
		Assert.Collection(
			leaderChange.PacketIntents,
			intent => AssertLeaderChangeIntent(intent, sequence: 0, recipientObjectId: 1001, expectedLeaderObjectId: 1002, expectedMessageId: 1300154, "Fallback"),
			intent => AssertLeaderChangeIntent(intent, sequence: 1, recipientObjectId: 1002, expectedLeaderObjectId: 1002, expectedMessageId: 1300155),
			intent => AssertLeaderChangeIntent(intent, sequence: 2, recipientObjectId: 1003, expectedLeaderObjectId: 1002, expectedMessageId: 1300154, "Fallback"));
		Assert.Collection(
			plan.PacketIntents,
			intent => AssertDisconnectedSystemMessage(intent, sequence: 0, recipientObjectId: 1002, subjectObjectId: 1001, "Leader"),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 1, recipientObjectId: 1002, subjectObjectId: 1001, isOnline: false),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 2, recipientObjectId: 1001, subjectObjectId: 1002, isOnline: true),
			intent => AssertDisconnectedSystemMessage(intent, sequence: 3, recipientObjectId: 1003, subjectObjectId: 1001, "Leader"),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 4, recipientObjectId: 1003, subjectObjectId: 1001, isOnline: false),
			intent => AssertDisconnectedMemberInfo(intent, sequence: 5, recipientObjectId: 1001, subjectObjectId: 1003, isOnline: true));
		var descriptor = Assert.IsType<PlayerGroupDescriptor>(runtime.GetDescriptor(99001));
		Assert.Equal(1001, descriptor.LeaderObjectId);
	}

	private static Player CreatePlayer(int objectId, string name, bool isOnline, int worldId = 210010000)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			IsOnline = isOnline,
			PlayerClass = "GLADIATOR",
			Level = 25,
			Position = new WorldPosition(worldId, objectId, objectId + 1, objectId + 2, 64),
		};
	}

	private static void AssertDisconnectedSystemMessage(
		PlayerGroupDisconnectedPacketIntent intent,
		int sequence,
		int recipientObjectId,
		int subjectObjectId,
		string playerName)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(subjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerGroupDisconnectedPacketIntentKind.SystemMessage, intent.Kind);
		var message = Assert.IsType<SmSystemMessage>(intent.CreatePacket());
		Assert.Equal(1300175, message.MessageId);
		Assert.Equal([playerName], message.Parameters);
	}

	private static void AssertDisconnectedMemberInfo(
		PlayerGroupDisconnectedPacketIntent intent,
		int sequence,
		int recipientObjectId,
		int subjectObjectId,
		bool isOnline)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(subjectObjectId, intent.SubjectObjectId);
		Assert.Equal(PlayerGroupDisconnectedPacketIntentKind.MemberInfo, intent.Kind);
		Assert.IsType<SmGroupMemberInfo>(intent.CreatePacket());
		AssertMemberInfoPlan(intent.MemberInfoPlan, 99001, subjectObjectId, PlayerGroupEvent.Disconnected, isOnline);
	}

	private static void AssertLeaderChangeIntent(
		PlayerGroupLeaderChangePacketIntent intent,
		int sequence,
		int recipientObjectId,
		int expectedLeaderObjectId,
		int expectedMessageId,
		params string[] expectedParameters)
	{
		Assert.Equal(sequence, intent.Sequence);
		Assert.Equal(recipientObjectId, intent.RecipientObjectId);
		Assert.Equal(expectedLeaderObjectId, intent.GroupInfoPlan.LeaderObjectId);
		Assert.Equal(99001, intent.GroupInfoPlan.TeamId);
		Assert.Equal(expectedMessageId, intent.SystemMessage.MessageId);
		Assert.Equal(expectedParameters, intent.SystemMessage.Parameters);
	}

	private static void AssertMemberInfoPlan(
		PlayerGroupMemberInfoPacketPlan? plan,
		int expectedGroupId,
		int expectedMemberObjectId,
		PlayerGroupEvent expectedEvent,
		bool isOnline)
	{
		var actual = Assert.IsType<PlayerGroupMemberInfoPacketPlan>(plan);
		Assert.Equal(expectedGroupId, actual.GroupId);
		Assert.Equal(expectedMemberObjectId, actual.MemberObjectId);
		Assert.Equal(expectedEvent, actual.RequestedEvent);
		Assert.Equal(expectedEvent, actual.EffectiveEvent);
		Assert.Equal(isOnline, actual.IsOnline);
		Assert.Equal((int)PlayerGroupEvent.Disconnected, actual.PrefixSnapshot.EventId);
		Assert.True(actual.WritesLifeStatsBlock);
		Assert.True(actual.WritesPositionBlock);
		Assert.True(actual.WritesCommonDataBlock);
		Assert.False(actual.WritesName);
		Assert.False(actual.WritesAbnormalEffects);
		Assert.False(actual.WritesSlotTimers);
	}
}
