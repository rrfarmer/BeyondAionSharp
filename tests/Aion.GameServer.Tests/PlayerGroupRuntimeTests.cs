using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerGroupRuntimeTests
{
	[Fact]
	public void CreateOrUpdateGroup_AttachesSharedSnapshotMetadataToMembers()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };

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
