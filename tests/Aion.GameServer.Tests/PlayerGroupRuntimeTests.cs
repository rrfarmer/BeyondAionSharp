using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupRuntimeTests
{
	[Fact]
	public void CreateOrUpdateGroup_AttachesSharedSnapshotMetadataToMembers()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player
		{
			ObjectId = 1001,
			Name = "Leader",
			IsOnline = true,
			Level = 25,
			Position = new WorldPosition(210010000, 10.5f, 20.25f, 30.75f, 64),
		};
		var member = new Player { ObjectId = 1002, Name = "Member" };

		var snapshot = runtime.CreateOrUpdateGroup(99001, [leader, member], PlayerGroupType.AutoGroup);

		Assert.Equal(99001, snapshot.TeamId);
		Assert.Equal([1001, 1002], snapshot.MemberObjectIds);
		var descriptor = Assert.IsType<PlayerGroupDescriptor>(runtime.GetDescriptor(99001));
		Assert.Equal(99001, descriptor.TeamId);
		Assert.Equal(1001, descriptor.LeaderObjectId);
		Assert.Equal(PlayerGroupType.AutoGroup, descriptor.TeamType);
		Assert.Equal(6, descriptor.MaxMemberCount);
		Assert.Equal(PlayerTeamMembership.Group, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Group, member.TeamMembership);
		Assert.Equal(99001, leader.CurrentTeamId);
		Assert.Equal(99001, member.CurrentTeamId);
		Assert.Equal([1001, 1002], leader.CurrentTeamMemberObjectIds);
		Assert.Equal([1001, 1002], member.CurrentTeamMemberObjectIds);
		Assert.Same(snapshot, leader.CurrentGroupSnapshot);
		Assert.Same(snapshot, member.CurrentGroupSnapshot);
		Assert.Same(snapshot, runtime.Resolve(leader));
		Assert.True(runtime.HasMember(99001, 1001));
		Assert.True(runtime.HasMember(99001, 1002));
		Assert.False(runtime.HasMember(99001, 1003));
		var leaderMember = Assert.IsType<PlayerGroupMember>(runtime.GetMember(99001, 1001));
		Assert.Equal(1001, leaderMember.ObjectId);
		Assert.Equal("Leader", leaderMember.Name);
		Assert.Same(leader, leaderMember.Player);
		Assert.True(leaderMember.IsOnline);
		Assert.Equal(10.5f, leaderMember.X);
		Assert.Equal(20.25f, leaderMember.Y);
		Assert.Equal(30.75f, leaderMember.Z);
		Assert.Equal(64, leaderMember.Heading);
		Assert.Equal(25, leaderMember.Level);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
		Assert.True(runtime.IsLeader(99001, leader));
		Assert.False(runtime.IsLeader(99001, member));
		Assert.False(runtime.IsFull(99001));
	}

	[Fact]
	public void RemoveMember_ClearsRemovedPlayerAndRefreshesRemainingSnapshot()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var removed = new Player { ObjectId = 1002 };
		var remaining = new Player { ObjectId = 1003 };
		runtime.CreateOrUpdateGroup(99001, [leader, removed, remaining]);

		var updatedSnapshot = runtime.RemoveMember(removed);

		Assert.NotNull(updatedSnapshot);
		Assert.Equal(99001, updatedSnapshot.TeamId);
		Assert.Equal([1001, 1003], updatedSnapshot.MemberObjectIds);
		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Equal(0, removed.CurrentTeamId);
		Assert.Empty(removed.CurrentTeamMemberObjectIds);
		Assert.Null(removed.CurrentGroupSnapshot);
		Assert.Equal(PlayerTeamMembership.Group, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Group, remaining.TeamMembership);
		Assert.Equal([1001, 1003], leader.CurrentTeamMemberObjectIds);
		Assert.Equal([1001, 1003], remaining.CurrentTeamMemberObjectIds);
		Assert.Same(updatedSnapshot, leader.CurrentGroupSnapshot);
		Assert.Same(updatedSnapshot, remaining.CurrentGroupSnapshot);
	}

	[Fact]
	public void AddMember_RefreshesSnapshotForExistingMembersAndNewMember()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		var added = new Player { ObjectId = 1003 };
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var updatedSnapshot = runtime.AddMember(99001, added);

		Assert.Equal([1001, 1002, 1003], updatedSnapshot.MemberObjectIds);
		Assert.Same(updatedSnapshot, leader.CurrentGroupSnapshot);
		Assert.Same(updatedSnapshot, member.CurrentGroupSnapshot);
		Assert.Same(updatedSnapshot, added.CurrentGroupSnapshot);
		Assert.Equal([1001, 1002, 1003], added.CurrentTeamMemberObjectIds);
		Assert.True(runtime.HasMember(99001, 1003));
		Assert.Equal([1001, 1002, 1003], runtime.GetMemberObjectIds(99001));
	}

	[Fact]
	public void AddMember_RejectsDuplicateMemberLikeJavaGeneralTeam()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var exception = Assert.Throws<InvalidOperationException>(() => runtime.AddMember(99001, member));

		Assert.Equal("Team member is already added.", exception.Message);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
		Assert.Same(member.CurrentGroupSnapshot, leader.CurrentGroupSnapshot);
	}

	[Fact]
	public void GetMember_ReturnsWrapperWithDeterministicLastOnlineUpdate()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001, Name = "Leader" };
		runtime.CreateOrUpdateGroup(99001, [leader]);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(123_456);

		var member = Assert.IsType<PlayerGroupMember>(runtime.GetMember(99001, 1001));
		member.UpdateLastOnlineTime(now);

		Assert.Equal(123_456, member.LastOnlineTimeMillis);
		Assert.Null(runtime.GetMember(99001, 9999));
	}

	[Fact]
	public void UpdateMemberLastOnlineTime_UpdatesGroupedMemberLikeJavaLogout()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		runtime.CreateOrUpdateGroup(99001, [leader, member]);
		var now = DateTimeOffset.FromUnixTimeMilliseconds(456_789);

		var updated = runtime.UpdateMemberLastOnlineTime(member, now);

		Assert.True(updated);
		Assert.Equal(456_789, runtime.GetMember(99001, 1002)?.LastOnlineTimeMillis);
		Assert.Equal(0, runtime.GetMember(99001, 1001)?.LastOnlineTimeMillis);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
	}

	[Fact]
	public void UpdateMemberLastOnlineTime_ReturnsFalseForPlayerWithoutRuntimeGroup()
	{
		var runtime = new PlayerGroupRuntime();
		var player = new Player { ObjectId = 1001 };

		var updated = runtime.UpdateMemberLastOnlineTime(player, DateTimeOffset.FromUnixTimeMilliseconds(456_789));

		Assert.False(updated);
		Assert.Null(runtime.GetMember(99001, 1001));
	}

	[Fact]
	public void UpdateMemberLastOnlineTime_ReturnsFalseForStaleGroupMetadataWithoutMutatingRuntime()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		var stale = new Player
		{
			ObjectId = 1003,
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = 99001,
		};
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var updated = runtime.UpdateMemberLastOnlineTime(stale, DateTimeOffset.FromUnixTimeMilliseconds(456_789));

		Assert.False(updated);
		Assert.Equal(PlayerTeamMembership.Group, stale.TeamMembership);
		Assert.Equal(99001, stale.CurrentTeamId);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
		Assert.Equal(0, runtime.GetMember(99001, 1001)?.LastOnlineTimeMillis);
		Assert.Equal(0, runtime.GetMember(99001, 1002)?.LastOnlineTimeMillis);
	}

	[Fact]
	public void TryReconnectMember_ReplacesStoredWrapperWithLoggingInPlayerAndRefreshesSnapshot()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var offlineMember = new Player
		{
			ObjectId = 1002,
			Name = "Offline",
			IsOnline = false,
		};
		runtime.CreateOrUpdateGroup(99001, [leader, offlineMember]);
		runtime.UpdateMemberLastOnlineTime(offlineMember, DateTimeOffset.FromUnixTimeMilliseconds(456_789));
		var loggingInMember = new Player
		{
			ObjectId = 1002,
			Name = "Online",
			IsOnline = true,
			Level = 27,
		};

		var reconnected = runtime.TryReconnectMember(loggingInMember);

		Assert.True(reconnected);
		var wrapper = Assert.IsType<PlayerGroupMember>(runtime.GetMember(99001, 1002));
		Assert.Same(loggingInMember, wrapper.Player);
		Assert.Equal("Online", wrapper.Name);
		Assert.True(wrapper.IsOnline);
		Assert.Equal(27, wrapper.Level);
		Assert.Equal(0, wrapper.LastOnlineTimeMillis);
		Assert.Equal(PlayerTeamMembership.None, offlineMember.TeamMembership);
		Assert.Null(offlineMember.CurrentGroupSnapshot);
		Assert.Equal(PlayerTeamMembership.Group, loggingInMember.TeamMembership);
		Assert.Equal(99001, loggingInMember.CurrentTeamId);
		Assert.Equal([1001, 1002], loggingInMember.CurrentTeamMemberObjectIds);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
	}

	[Fact]
	public void ReconnectMember_ReturnsNonSendingPacketIntentPlanLikeJavaPlayerConnectedEvent()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var offlineMember = new Player { ObjectId = 1002 };
		var otherMember = new Player { ObjectId = 1003 };
		runtime.CreateOrUpdateGroup(99001, [leader, offlineMember, otherMember]);
		var loggingInMember = new Player { ObjectId = 1002 };

		var result = runtime.ReconnectMember(loggingInMember);

		Assert.True(result.Reconnected);
		var plan = Assert.IsType<PlayerGroupReconnectPacketPlan>(result.PacketPlan);
		Assert.Equal(99001, plan.TeamId);
		Assert.Equal(1002, plan.ReconnectingPlayerObjectId);
		Assert.True(plan.SendGroupInfoToReconnectingPlayer);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Join, intent.Event);
				Assert.Equal(5, (int)intent.Event);
			},
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
				Assert.Equal(13, (int)intent.Event);
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1001, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
			},
			intent =>
			{
				Assert.Equal(1003, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1003, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
			});
	}

	[Fact]
	public void PlayerGroupEvent_IdsMatchJavaGroupEvent()
	{
		Assert.Equal(0, (int)PlayerGroupEvent.Leave);
		Assert.Equal(1, (int)PlayerGroupEvent.Movement);
		Assert.Equal(3, (int)PlayerGroupEvent.Disconnected);
		Assert.Equal(5, (int)PlayerGroupEvent.Join);
		Assert.Equal(7, (int)PlayerGroupEvent.EnterOffline);
		Assert.Equal(13, (int)PlayerGroupEvent.Enter);
		Assert.Equal(13, (int)PlayerGroupEvent.Update);
		Assert.Equal(65, (int)PlayerGroupEvent.UpdateEffects);
	}

	[Fact]
	public void TryReconnectMember_ReturnsFalseForUnknownPlayerWithoutMutatingRuntime()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		var unknown = new Player { ObjectId = 9999 };
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var reconnected = runtime.TryReconnectMember(unknown);

		Assert.False(reconnected);
		Assert.Equal(PlayerTeamMembership.None, unknown.TeamMembership);
		Assert.Null(unknown.CurrentGroupSnapshot);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
		Assert.Same(member, runtime.GetMember(99001, 1002)?.Player);
		var result = runtime.ReconnectMember(unknown);
		Assert.False(result.Reconnected);
		Assert.Null(result.PacketPlan);
	}

	[Fact]
	public void AddMember_RejectsPlayersBeyondJavaGroupCapacityWithoutAttachingRejectedPlayer()
	{
		var runtime = new PlayerGroupRuntime();
		var members = Enumerable.Range(1001, PlayerGroupDescriptor.JavaMaxMemberCount)
			.Select(objectId => new Player { ObjectId = objectId })
			.ToArray();
		var rejected = new Player { ObjectId = 2001 };
		runtime.CreateOrUpdateGroup(99001, members);

		var exception = Assert.Throws<InvalidOperationException>(() => runtime.AddMember(99001, rejected));

		Assert.Equal("Player group is full.", exception.Message);
		Assert.Equal(PlayerTeamMembership.None, rejected.TeamMembership);
		Assert.Equal(0, rejected.CurrentTeamId);
		Assert.Empty(rejected.CurrentTeamMemberObjectIds);
		Assert.Null(rejected.CurrentGroupSnapshot);
		Assert.True(runtime.IsFull(99001));
		Assert.Equal([1001, 1002, 1003, 1004, 1005, 1006], members[0].CurrentGroupSnapshot?.MemberObjectIds);
	}

	[Fact]
	public void RemoveMember_PreservesLeaderDescriptorWhenNonLeaderLeaves()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var removed = new Player { ObjectId = 1002 };
		var remaining = new Player { ObjectId = 1003 };
		runtime.CreateOrUpdateGroup(99001, [leader, removed, remaining]);

		runtime.RemoveMember(removed);

		var descriptor = Assert.IsType<PlayerGroupDescriptor>(runtime.GetDescriptor(99001));
		Assert.Equal(1001, descriptor.LeaderObjectId);
		Assert.Equal(PlayerGroupType.Group, descriptor.TeamType);
		Assert.Equal(6, descriptor.MaxMemberCount);
		Assert.True(runtime.IsLeader(99001, leader));
		Assert.False(runtime.HasMember(99001, 1002));
		Assert.Equal([1001, 1003], runtime.GetMemberObjectIds(99001));
	}

	[Fact]
	public void RemoveMember_RejectsMissingMemberLikeJavaGeneralTeam()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		var missing = new Player
		{
			ObjectId = 1003,
			TeamMembership = PlayerTeamMembership.Group,
			CurrentTeamId = 99001,
		};
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var exception = Assert.Throws<InvalidOperationException>(() => runtime.RemoveMember(missing));

		Assert.Equal("Team member is already removed.", exception.Message);
		Assert.Equal(PlayerTeamMembership.Group, missing.TeamMembership);
		Assert.Equal(99001, missing.CurrentTeamId);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
		Assert.True(runtime.HasMember(99001, 1001));
		Assert.True(runtime.HasMember(99001, 1002));
	}
}
