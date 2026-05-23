using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class PlayerAllianceRuntimeTests
{
	[Fact]
	public void CreateAlliance_AttachesLeaderToFirstJavaAllianceGroup()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);

		var snapshot = runtime.CreateAlliance(88001, leader, PlayerAllianceTeamType.AutoAlliance);

		Assert.Equal(88001, snapshot.AllianceId);
		Assert.Equal(1001, snapshot.LeaderObjectId);
		Assert.Equal([1001], snapshot.MemberObjectIds);
		Assert.Equal([1001], snapshot.MemberObjectIdsByGroupId[1000]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1001]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1002]);
		Assert.Empty(snapshot.MemberObjectIdsByGroupId[1003]);
		Assert.Equal(PlayerAllianceTeamType.AutoAlliance, snapshot.TeamType);
		Assert.Equal(PlayerTeamMembership.Alliance, leader.TeamMembership);
		Assert.Equal(88001, leader.CurrentTeamId);
		Assert.Equal([1001], leader.CurrentTeamMemberObjectIds);
		Assert.Same(snapshot, leader.CurrentAllianceSnapshot);
		Assert.Null(leader.CurrentGroupSnapshot);
		Assert.Same(snapshot, runtime.Resolve(leader));
		var descriptor = Assert.IsType<PlayerAllianceDescriptor>(runtime.GetDescriptor(88001));
		Assert.Equal(24, descriptor.MaxMemberCount);
		Assert.Equal(6, descriptor.MaxGroupMemberCount);
		Assert.Equal([1000, 1001, 1002, 1003], descriptor.AllianceGroupIds);
		Assert.True(runtime.HasMember(88001, 1001));
		Assert.True(runtime.IsLeader(88001, leader));
		Assert.False(runtime.IsFull(88001));
		var leaderMember = Assert.IsType<PlayerAllianceMember>(runtime.GetMember(88001, 1001));
		Assert.Equal(88001, leaderMember.AllianceId);
		Assert.Equal(1000, leaderMember.AllianceGroupId);
		Assert.Same(leader, leaderMember.Player);
	}

	[Fact]
	public void AddMember_FillsAllianceGroupsInJavaOrderAndCapsAtSixPerGroup()
	{
		var runtime = new PlayerAllianceRuntime();
		var members = Enumerable.Range(0, 24)
			.Select(index => CreatePlayer(1001 + index, $"Member{index}", worldId: 210010000 + index))
			.ToArray();
		runtime.CreateAlliance(88001, members[0]);

		PlayerAllianceSnapshot snapshot = members[0].CurrentAllianceSnapshot!;
		foreach (var member in members.Skip(1))
			snapshot = runtime.AddMember(88001, member);

		Assert.True(runtime.IsFull(88001));
		Assert.Equal(members.Select(member => member.ObjectId).ToArray(), snapshot.MemberObjectIds);
		Assert.Equal([1001, 1002, 1003, 1004, 1005, 1006], runtime.GetMemberObjectIdsByGroupId(88001, 1000));
		Assert.Equal([1007, 1008, 1009, 1010, 1011, 1012], runtime.GetMemberObjectIdsByGroupId(88001, 1001));
		Assert.Equal([1013, 1014, 1015, 1016, 1017, 1018], runtime.GetMemberObjectIdsByGroupId(88001, 1002));
		Assert.Equal([1019, 1020, 1021, 1022, 1023, 1024], runtime.GetMemberObjectIdsByGroupId(88001, 1003));
		Assert.Same(snapshot, members[0].CurrentAllianceSnapshot);
		Assert.Same(snapshot, members[23].CurrentAllianceSnapshot);
		var rejected = CreatePlayer(2001, "Rejected", worldId: 220010000);
		Assert.Throws<InvalidOperationException>(() => runtime.AddMember(88001, rejected));
		Assert.Null(rejected.CurrentAllianceSnapshot);
	}

	[Fact]
	public void RemoveMember_ClearsRemovedPlayerAndRefreshesRemainingAllianceSnapshot()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var removed = CreatePlayer(1002, "Removed", worldId: 220010000);
		var remaining = CreatePlayer(1003, "Remaining", worldId: 230010000);
		runtime.CreateAlliance(88001, leader);
		runtime.AddMember(88001, removed);
		runtime.AddMember(88001, remaining);
		runtime.SetViceCaptains(88001, [1002, 1003]);

		var snapshot = Assert.IsType<PlayerAllianceSnapshot>(runtime.RemoveMember(removed));

		Assert.Equal([1001, 1003], snapshot.MemberObjectIds);
		Assert.Equal([1001, 1003], snapshot.MemberObjectIdsByGroupId[1000]);
		Assert.Equal([1003], snapshot.ViceCaptainObjectIds);
		Assert.Equal(PlayerTeamMembership.None, removed.TeamMembership);
		Assert.Equal(0, removed.CurrentTeamId);
		Assert.Empty(removed.CurrentTeamMemberObjectIds);
		Assert.Null(removed.CurrentAllianceSnapshot);
		Assert.Equal(PlayerTeamMembership.Alliance, leader.TeamMembership);
		Assert.Equal(PlayerTeamMembership.Alliance, remaining.TeamMembership);
		Assert.Same(snapshot, leader.CurrentAllianceSnapshot);
		Assert.Same(snapshot, remaining.CurrentAllianceSnapshot);
		Assert.False(runtime.HasMember(88001, 1002));
		Assert.True(runtime.HasMember(88001, 1003));
		Assert.False(runtime.IsViceCaptain(88001, 1002));
		Assert.True(runtime.IsViceCaptain(88001, 1003));
	}

	[Fact]
	public void Snapshot_CreatesAllianceInfoPlanForExistingPlanners()
	{
		var runtime = new PlayerAllianceRuntime();
		var leader = CreatePlayer(1001, "Leader", worldId: 210010000);
		var viceCaptain = CreatePlayer(1002, "Vice", worldId: 220010000);
		runtime.CreateAlliance(88001, leader);
		var snapshot = runtime.AddMember(88001, viceCaptain);
		snapshot = Assert.IsType<PlayerAllianceSnapshot>(runtime.SetViceCaptains(88001, [1002, 404]));

		var infoPlan = snapshot.CreateInfoPacketPlan(activePlayerMapId: viceCaptain.Position.WorldId);

		Assert.Equal(2, infoPlan.AllianceGroupSize);
		Assert.Equal(88001, infoPlan.AllianceId);
		Assert.Equal(1001, infoPlan.LeaderObjectId);
		Assert.Equal(220010000, infoPlan.ActivePlayerMapId);
		Assert.Equal([1002], infoPlan.ViceCaptainObjectIds);
		Assert.Equal([1002, 0, 0, 0], infoPlan.PaddedViceCaptainObjectIds);
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, infoPlan.LootRules.LootRule);
		Assert.Equal(0x3F, infoPlan.TeamType);
		Assert.Equal(0, infoPlan.TeamSubType);
		Assert.Equal(
			[
				new PlayerAllianceInfoGroupPlaceholder(0, 1000),
				new PlayerAllianceInfoGroupPlaceholder(1, 1001),
				new PlayerAllianceInfoGroupPlaceholder(2, 1002),
				new PlayerAllianceInfoGroupPlaceholder(3, 1003),
			],
			infoPlan.GroupPlaceholders);
	}

	private static Player CreatePlayer(int objectId, string name, int worldId)
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
}
