using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class GroupDataExchangeFanoutPlanServiceTests
{
	[Fact]
	public void CreatePlan_ActionOnePlansNearbyBroadcastAndReceive()
	{
		var player = CreatePlayer(1001);
		var plan = CreatePlan(player, action: 1, groupType: 0, unknown2: 0, [1, 2, 255]);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.NearbyBroadcastVisiblePlayersAndSelf, plan.Status);
		Assert.Equal(1001, plan.SourcePlayerObjectId);
		Assert.True(plan.IncludeSourcePlayer);
		Assert.False(plan.IsLive);
		Assert.Empty(plan.RecipientObjectIds);
		Assert.Equal(Convert.FromHexString("01030000000102FF"), SerializeUnencryptedPayload(Assert.IsAssignableFrom<GameServerPacket>(plan.Packet)));
		Assert.Contains("broadcastPacketAndReceive", plan.JavaUtilityMethod, StringComparison.Ordinal);
	}

	[Fact]
	public void CreatePlan_EmptyDataIsIgnoredBeforeFanout()
	{
		var plan = CreatePlan(CreatePlayer(1001), action: 1, groupType: 0, unknown2: 0, []);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.IgnoredEmptyData, plan.Status);
		Assert.Null(plan.Packet);
		Assert.Empty(plan.RecipientObjectIds);
	}

	[Fact]
	public void CreatePlan_TooLargeDataIsRejectedBeforeFanout()
	{
		var plan = CreatePlan(
			CreatePlayer(1001),
			action: 1,
			groupType: 0,
			unknown2: 0,
			new byte[GroupDataExchangeFanoutPlanService.MaxExchangeDataSize + 1]);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.RejectedDataTooLarge, plan.Status);
		Assert.Null(plan.Packet);
		Assert.Equal(GroupDataExchangeFanoutPlanService.MaxExchangeDataSize + 1, plan.DataLength);
	}

	[Fact]
	public void CreatePlan_GroupTypeZeroPlansGroupRecipientsExceptSelf()
	{
		var groupRuntime = new PlayerGroupRuntime();
		var source = CreatePlayer(1001);
		var member = CreatePlayer(1002);
		var other = CreatePlayer(1003);
		groupRuntime.CreateOrUpdateGroup(9001, [source, member, other]);

		var plan = GroupDataExchangeFanoutPlanService.CreatePlan(
			source,
			action: 2,
			groupType: 0,
			unknown2: 7,
			[10, 11, 12, 13],
			groupRuntime,
			new PlayerAllianceRuntime(),
			new PlayerLeagueRuntime());

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.GroupBroadcastMembersExceptSelf, plan.Status);
		Assert.Equal([1002, 1003], plan.RecipientObjectIds);
		Assert.False(plan.IncludeSourcePlayer);
		Assert.Equal(Convert.FromHexString("0207040000000A0B0C0D"), SerializeUnencryptedPayload(Assert.IsAssignableFrom<GameServerPacket>(plan.Packet)));
	}

	[Fact]
	public void CreatePlan_GroupTypeOnePlansCurrentAllianceGroupRecipientsOnly()
	{
		var allianceRuntime = new PlayerAllianceRuntime();
		var members = Enumerable.Range(0, 7)
			.Select(index => CreatePlayer(2001 + index))
			.ToArray();
		allianceRuntime.CreateAlliance(9901, members[0]);
		foreach (var member in members.Skip(1))
			allianceRuntime.AddMember(9901, member);

		var plan = GroupDataExchangeFanoutPlanService.CreatePlan(
			members[0],
			action: 2,
			groupType: 1,
			unknown2: 7,
			[10, 11],
			new PlayerGroupRuntime(),
			allianceRuntime,
			new PlayerLeagueRuntime());

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.AllianceGroupBroadcastMembersExceptSelf, plan.Status);
		Assert.Equal([2002, 2003, 2004, 2005, 2006], plan.RecipientObjectIds);
		Assert.DoesNotContain(2007, plan.RecipientObjectIds);
	}

	[Fact]
	public void CreatePlan_GroupTypeTwoRequiresLeagueBeforeAllianceGroupRecipients()
	{
		var allianceRuntime = new PlayerAllianceRuntime();
		var leagueRuntime = new PlayerLeagueRuntime();
		var source = CreatePlayer(3001);
		var member = CreatePlayer(3002);
		allianceRuntime.CreateAlliance(9902, source);
		allianceRuntime.AddMember(9902, member);

		var missingLeague = GroupDataExchangeFanoutPlanService.CreatePlan(
			source,
			action: 2,
			groupType: 2,
			unknown2: 7,
			[10, 11],
			new PlayerGroupRuntime(),
			allianceRuntime,
			leagueRuntime);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.IgnoredNoRecipients, missingLeague.Status);
		Assert.Null(missingLeague.Packet);

		leagueRuntime.CreateLeague(7701, 9902);
		var inLeague = GroupDataExchangeFanoutPlanService.CreatePlan(
			source,
			action: 2,
			groupType: 2,
			unknown2: 7,
			[10, 11],
			new PlayerGroupRuntime(),
			allianceRuntime,
			leagueRuntime);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.LeagueAllianceGroupBroadcastMembersExceptSelf, inLeague.Status);
		Assert.Equal([3002], inLeague.RecipientObjectIds);
	}

	[Fact]
	public void CreatePlan_UnsupportedGroupTypeIsIgnored()
	{
		var plan = CreatePlan(CreatePlayer(1001), action: 2, groupType: 99, unknown2: 7, [10]);

		Assert.Equal(GroupDataExchangeFanoutPlanStatus.IgnoredUnsupportedGroupType, plan.Status);
		Assert.Null(plan.Packet);
		Assert.Empty(plan.RecipientObjectIds);
	}

	private static GroupDataExchangeFanoutPlan CreatePlan(
		Player? player,
		byte action,
		byte groupType,
		byte unknown2,
		byte[] data)
	{
		return GroupDataExchangeFanoutPlanService.CreatePlan(
			player,
			action,
			groupType,
			unknown2,
			data,
			new PlayerGroupRuntime(),
			new PlayerAllianceRuntime(),
			new PlayerLeagueRuntime());
	}

	private static Player CreatePlayer(int objectId)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			IsOnline = true,
		};
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
