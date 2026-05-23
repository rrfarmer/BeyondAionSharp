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

		var snapshot = runtime.CreateOrUpdateGroup(99001, [leader, member]);

		Assert.Equal(99001, snapshot.TeamId);
		Assert.Equal([1001, 1002], snapshot.MemberObjectIds);
		Assert.Equal(PlayerTeamMembership.Group, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Group, member.TeamMembership);
		Assert.Equal(99001, leader.CurrentTeamId);
		Assert.Equal(99001, member.CurrentTeamId);
		Assert.Equal([1001, 1002], leader.CurrentTeamMemberObjectIds);
		Assert.Equal([1001, 1002], member.CurrentTeamMemberObjectIds);
		Assert.Same(snapshot, leader.CurrentGroupSnapshot);
		Assert.Same(snapshot, member.CurrentGroupSnapshot);
		Assert.Same(snapshot, runtime.Resolve(leader));
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
	}
}
