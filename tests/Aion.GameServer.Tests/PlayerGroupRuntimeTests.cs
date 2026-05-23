using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
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
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, descriptor.LootRules.LootRule);
		Assert.Equal(1, (int)descriptor.LootRules.LootRule);
		Assert.Equal(0, descriptor.LootRules.Misc);
		Assert.Equal(0, descriptor.LootRules.CommonItemAbove);
		Assert.Equal(2, descriptor.LootRules.SuperiorItemAbove);
		Assert.Equal(2, descriptor.LootRules.HeroicItemAbove);
		Assert.Equal(2, descriptor.LootRules.FabledItemAbove);
		Assert.Equal(2, descriptor.LootRules.EternalItemAbove);
		Assert.Equal(2, descriptor.LootRules.MythicItemAbove);
		Assert.Equal(2, descriptor.LootRules.AutoDistributionId);
		var groupInfoPlan = PlayerGroupInfoPacketPlan.FromDescriptor(descriptor, activePlayerMapId: 210010000);
		Assert.Equal(99001, groupInfoPlan.TeamId);
		Assert.Equal(1001, groupInfoPlan.LeaderObjectId);
		Assert.Equal(210010000, groupInfoPlan.ActivePlayerMapId);
		Assert.Same(descriptor.LootRules, groupInfoPlan.LootRules);
		Assert.Equal(0x02, groupInfoPlan.ConstantGroupInfoMarker);
		Assert.Equal(0x00, groupInfoPlan.UnknownByte);
		Assert.Equal(0x02, groupInfoPlan.TeamType);
		Assert.Equal(1, groupInfoPlan.TeamSubType);
		Assert.Equal(0, groupInfoPlan.MessageId);
		Assert.Equal(string.Empty, groupInfoPlan.Name);
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
	public void CreateEnteredPacketPlan_ReturnsNonSendingGroupInfoPlanLikeJavaPlayerGroupEnteredEvent()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var existingMember = new Player { ObjectId = 1002 };
		var enteringPlayer = new Player
		{
			ObjectId = 1003,
			Name = "NewMember",
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		runtime.CreateOrUpdateGroup(99001, [leader, existingMember]);
		runtime.UpdateBrand(99001, brandId: 3, targetObjectId: 8001);
		runtime.AddMember(99001, enteringPlayer);

		var plan = Assert.IsType<PlayerGroupEnteredPacketPlan>(runtime.CreateEnteredPacketPlan(99001, enteringPlayer));

		Assert.Equal(99001, plan.TeamId);
		Assert.Equal(1003, plan.EnteringPlayerObjectId);
		Assert.True(plan.SendGroupInfoToEnteringPlayer);
		var groupInfoPlan = Assert.IsType<PlayerGroupInfoPacketPlan>(plan.GroupInfoPlan);
		Assert.Equal(99001, groupInfoPlan.TeamId);
		Assert.Equal(1001, groupInfoPlan.LeaderObjectId);
		Assert.Equal(220010000, groupInfoPlan.ActivePlayerMapId);
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, groupInfoPlan.LootRules.LootRule);
		Assert.Equal(0x3F, groupInfoPlan.TeamType);
		Assert.Equal(0, groupInfoPlan.TeamSubType);
		var groupInfoPacket = Assert.IsType<Network.Aion.ServerPackets.SmGroupInfo>(plan.CreateGroupInfoPacket());
		using var groupInfoReader = new PacketBuffer(SerializeUnencryptedPayload(groupInfoPacket));
		Assert.Equal(99001, groupInfoReader.ReadD());
		Assert.Equal(1001, groupInfoReader.ReadD());
		Assert.Equal(220010000, groupInfoReader.ReadD());
		Assert.Equal(1, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(0x02, groupInfoReader.ReadD());
		Assert.Equal(0, (int)groupInfoReader.ReadC());
		Assert.Equal(0x3F, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(string.Empty, groupInfoReader.ReadS());
		Assert.Equal(0, groupInfoReader.Remaining);
		Assert.Collection(
			plan.SystemMessageIntents,
			intent =>
			{
				Assert.Equal(1003, intent.RecipientObjectId);
				AssertSystemMessage(intent.Message, 1390262);
			},
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertSystemMessage(intent.Message, 1400009, "NewMember");
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				AssertSystemMessage(intent.Message, 1400009, "NewMember");
			});
		var brandIntent = Assert.IsType<PlayerGroupBrandIntent>(plan.BrandIntent);
		Assert.Equal(1003, brandIntent.RecipientObjectId);
		Assert.Equal(new Dictionary<int, int> { [3] = 8001 }, brandIntent.TargetObjectIdsByBrandId);
		AssertShowBrandPayload(brandIntent.CreatePacket(), (3, 8001));
		var abyssRankUpdateIntent = Assert.IsType<PlayerGroupAbyssRankUpdateIntent>(plan.AbyssRankUpdateIntent);
		Assert.Equal(1003, abyssRankUpdateIntent.PlayerObjectId);
		Assert.Equal(99001, abyssRankUpdateIntent.TeamObjectId);
		Assert.True(abyssRankUpdateIntent.IncludeSelf);
		AssertAbyssRankUpdateTeamPayload(abyssRankUpdateIntent.CreatePacket(), expectedPlayerObjectId: 1003, expectedTeamObjectId: 99001);
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
		var loggingInMember = new Player
		{
			ObjectId = 1002,
			IsOnline = true,
			Position = new WorldPosition(210010000, 100, 200, 300, 32),
		};

		var result = runtime.ReconnectMember(loggingInMember);

		Assert.True(result.Reconnected);
		var plan = Assert.IsType<PlayerGroupReconnectPacketPlan>(result.PacketPlan);
		Assert.Equal(99001, plan.TeamId);
		Assert.Equal(1002, plan.ReconnectingPlayerObjectId);
		Assert.True(plan.SendGroupInfoToReconnectingPlayer);
		var groupInfoPlan = Assert.IsType<PlayerGroupInfoPacketPlan>(plan.GroupInfoPlan);
		Assert.Equal(99001, groupInfoPlan.TeamId);
		Assert.Equal(1001, groupInfoPlan.LeaderObjectId);
		Assert.Equal(210010000, groupInfoPlan.ActivePlayerMapId);
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, groupInfoPlan.LootRules.LootRule);
		Assert.Equal(0x3F, groupInfoPlan.TeamType);
		Assert.Equal(0, groupInfoPlan.TeamSubType);
		var groupInfoPacket = Assert.IsType<Network.Aion.ServerPackets.SmGroupInfo>(plan.CreateGroupInfoPacket());
		using var groupInfoReader = new PacketBuffer(SerializeUnencryptedPayload(groupInfoPacket));
		Assert.Equal(99001, groupInfoReader.ReadD());
		Assert.Equal(1001, groupInfoReader.ReadD());
		Assert.Equal(210010000, groupInfoReader.ReadD());
		Assert.Equal(1, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(2, groupInfoReader.ReadD());
		Assert.Equal(0x02, groupInfoReader.ReadD());
		Assert.Equal(0, (int)groupInfoReader.ReadC());
		Assert.Equal(0x3F, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(string.Empty, groupInfoReader.ReadS());
		Assert.Equal(0, groupInfoReader.Remaining);
		Assert.Collection(
			plan.MemberInfoIntents,
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Join, intent.Event);
				Assert.Equal(5, (int)intent.Event);
				AssertMemberInfoPlan(intent.PacketPlan, 99001, 1002, PlayerGroupEvent.Join, PlayerGroupEvent.Join, isOnline: true, writesName: true, writesEffects: false);
			},
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
				Assert.Equal(13, (int)intent.Event);
				AssertMemberInfoPlan(intent.PacketPlan, 99001, 1002, PlayerGroupEvent.Enter, PlayerGroupEvent.Enter, isOnline: true, writesName: true, writesEffects: true);
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1001, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
				AssertMemberInfoPlan(intent.PacketPlan, 99001, 1001, PlayerGroupEvent.Enter, PlayerGroupEvent.EnterOffline, isOnline: false, writesName: true, writesEffects: false);
			},
			intent =>
			{
				Assert.Equal(1003, intent.RecipientObjectId);
				Assert.Equal(1002, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
				AssertMemberInfoPlan(intent.PacketPlan, 99001, 1002, PlayerGroupEvent.Enter, PlayerGroupEvent.Enter, isOnline: true, writesName: true, writesEffects: true);
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Equal(1003, intent.SubjectObjectId);
				Assert.Equal(PlayerGroupEvent.Enter, intent.Event);
				AssertMemberInfoPlan(intent.PacketPlan, 99001, 1003, PlayerGroupEvent.Enter, PlayerGroupEvent.EnterOffline, isOnline: false, writesName: true, writesEffects: false);
			});
	}

	[Fact]
	public void PlayerGroupMemberInfoPacketPlan_ModelsStableJavaHeaderAndEventBranches()
	{
		var offlineMember = new PlayerGroupMember(new Player { ObjectId = 1001, IsOnline = false });
		var onlineMember = new PlayerGroupMember(new Player { ObjectId = 1002, IsOnline = true });

		var offlineEnter = PlayerGroupMemberInfoPacketPlan.FromMember(99001, offlineMember, PlayerGroupEvent.Enter);
		var updateEffects = PlayerGroupMemberInfoPacketPlan.FromMember(99001, onlineMember, PlayerGroupEvent.UpdateEffects, slot: 4);
		var movement = PlayerGroupMemberInfoPacketPlan.FromMember(99001, onlineMember, PlayerGroupEvent.Movement);

		AssertMemberInfoPlan(offlineEnter, 99001, 1001, PlayerGroupEvent.Enter, PlayerGroupEvent.EnterOffline, isOnline: false, writesName: true, writesEffects: false);
		AssertMemberInfoPlan(updateEffects, 99001, 1002, PlayerGroupEvent.UpdateEffects, PlayerGroupEvent.UpdateEffects, isOnline: true, writesName: false, writesEffects: true, slot: 4);
		AssertMemberInfoPlan(movement, 99001, 1002, PlayerGroupEvent.Movement, PlayerGroupEvent.Movement, isOnline: true, writesName: false, writesEffects: false);
	}

	[Fact]
	public void PlayerGroupMemberInfoPrefixSnapshot_ModelsJavaLifeCommonAndPositionPrefix()
	{
		var onlineMember = new PlayerGroupMember(new Player
		{
			ObjectId = 1002,
			Name = "Singer",
			IsOnline = true,
			PlayerClass = "BARD",
			Gender = "FEMALE",
			Level = 44,
			IsMentor = true,
			FlyState = PlayerFlyState.Gliding,
			LifeStats = new PlayerLifeStats(CurrentHp: 111, CurrentMp: 222, CurrentFp: -5),
			Position = new WorldPosition(220010000, 12.5f, 23.25f, 34.75f, 64, InstanceId: 3),
		});
		var offlineMember = new PlayerGroupMember(new Player
		{
			ObjectId = 1003,
			Name = "Offline",
			IsOnline = false,
			PlayerClass = "RIDER",
			Gender = "MALE",
			Level = 12,
			LifeStats = new PlayerLifeStats(CurrentHp: 999, CurrentMp: 888, CurrentFp: 777),
			Position = new WorldPosition(210010000, 1, 2, 3, 0, InstanceId: 1),
		});

		var onlinePlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, onlineMember, PlayerGroupEvent.Enter);
		var offlinePlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, offlineMember, PlayerGroupEvent.Enter);

		var online = onlinePlan.PrefixSnapshot;
		Assert.Equal(3454, online.MaxHp);
		Assert.Equal(111, online.CurrentHp);
		Assert.Equal(4198, online.MaxMp);
		Assert.Equal(222, online.CurrentMp);
		Assert.Equal(60, online.MaxFp);
		Assert.Equal(0, online.CurrentFp);
		Assert.True(online.HasKnownLifeStatMaximums);
		Assert.Equal(0, online.Unknown3Point5);
		Assert.Equal(220010000, online.MapId);
		Assert.Equal(220010002, online.MapInstanceId);
		Assert.Equal(12.5f, online.X);
		Assert.Equal(23.25f, online.Y);
		Assert.Equal(34.75f, online.Z);
		Assert.Equal(16, online.ClassId);
		Assert.Equal(1, online.GenderId);
		Assert.Equal(44, online.Level);
		Assert.Equal(13, online.EventId);
		Assert.Equal(1, online.AlwaysOne);
		Assert.Equal(2, online.FlyState);
		Assert.Equal(1, online.MentorFlag);
		Assert.Equal("Singer", online.Name);

		var withKnownMaximums = online.WithKnownMaximums(maxHp: 100, maxMp: 300, maxFp: 60);
		Assert.Equal(100, withKnownMaximums.MaxHp);
		Assert.Equal(100, withKnownMaximums.CurrentHp);
		Assert.Equal(300, withKnownMaximums.MaxMp);
		Assert.Equal(222, withKnownMaximums.CurrentMp);
		Assert.Equal(60, withKnownMaximums.MaxFp);
		Assert.Equal(0, withKnownMaximums.CurrentFp);
		Assert.True(withKnownMaximums.HasKnownLifeStatMaximums);

		var offline = offlinePlan.PrefixSnapshot;
		Assert.Equal(PlayerGroupEvent.EnterOffline, offlinePlan.EffectiveEvent);
		Assert.Equal(0, offline.MaxHp);
		Assert.Equal(0, offline.CurrentHp);
		Assert.Equal(0, offline.MaxMp);
		Assert.Equal(0, offline.CurrentMp);
		Assert.Equal(0, offline.MaxFp);
		Assert.Equal(0, offline.CurrentFp);
		Assert.Equal(210010000, offline.MapId);
		Assert.Equal(210010000, offline.MapInstanceId);
		Assert.Equal(13, offline.ClassId);
		Assert.Equal(0, offline.GenderId);
		Assert.Equal(12, offline.Level);
		Assert.Equal(7, offline.EventId);
		Assert.Equal(0, offline.FlyState);
		Assert.Equal(0, offline.MentorFlag);
	}

	[Fact]
	public void PlayerGroupMemberInfoResourceMaximums_UsesStatsInfoLevelBasedMaxStats()
	{
		var member = new PlayerGroupMember(new Player
		{
			ObjectId = 1004,
			IsOnline = true,
			PlayerClass = "GLADIATOR",
			Level = 10,
			LifeStats = new PlayerLifeStats(CurrentHp: 10_000, CurrentMp: -5, CurrentFp: 100),
		});

		var plan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, member, PlayerGroupEvent.UpdateEffects);

		Assert.Equal(819, plan.PrefixSnapshot.MaxHp);
		Assert.Equal(819, plan.PrefixSnapshot.CurrentHp);
		Assert.Equal(840, plan.PrefixSnapshot.MaxMp);
		Assert.Equal(0, plan.PrefixSnapshot.CurrentMp);
		Assert.Equal(60, plan.PrefixSnapshot.MaxFp);
		Assert.Equal(100, plan.PrefixSnapshot.CurrentFp);
	}

	[Fact]
	public void SmGroupMemberInfo_WritesBranchlessFixedPrefixLikeJava()
	{
		var member = new PlayerGroupMember(new Player
		{
			ObjectId = 1004,
			IsOnline = true,
			Name = "Mover",
			PlayerClass = "GLADIATOR",
			Gender = "FEMALE",
			Level = 10,
			IsMentor = true,
			FlyState = PlayerFlyState.Flying,
			LifeStats = new PlayerLifeStats(CurrentHp: 777, CurrentMp: 333, CurrentFp: 44),
			Position = new WorldPosition(220010000, 10.5f, 20.25f, 30.75f, 64, InstanceId: 2),
		});
		var plan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, member, PlayerGroupEvent.Movement);

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new Network.Aion.ServerPackets.SmGroupMemberInfo(plan)));

		Assert.Equal(99001, reader.ReadD());
		Assert.Equal(1004, reader.ReadD());
		Assert.Equal(819, reader.ReadD());
		Assert.Equal(777, reader.ReadD());
		Assert.Equal(840, reader.ReadD());
		Assert.Equal(333, reader.ReadD());
		Assert.Equal(60, reader.ReadD());
		Assert.Equal(44, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(220010000, reader.ReadD());
		Assert.Equal(220010001, reader.ReadD());
		Assert.Equal(10.5f, reader.ReadF());
		Assert.Equal(20.25f, reader.ReadF());
		Assert.Equal(30.75f, reader.ReadF());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(10, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmGroupMemberInfo_ThrowsForUnportedEffectBranches()
	{
		var member = new PlayerGroupMember(new Player
		{
			ObjectId = 1004,
			IsOnline = true,
			Name = "Updater",
			PlayerClass = "GLADIATOR",
			Level = 10,
		});
		var plan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, member, PlayerGroupEvent.UpdateEffects);

		var exception = Assert.Throws<NotSupportedException>(() =>
			SerializeUnencryptedPayload(new Network.Aion.ServerPackets.SmGroupMemberInfo(plan)));

		Assert.Contains("SM_GROUP_MEMBER_INFO branch UpdateEffects is not ported yet", exception.Message);
	}

	[Fact]
	public void SmGroupMemberInfo_WritesJoinAndEnterOfflineNameBranchesLikeJava()
	{
		var joiningMember = new PlayerGroupMember(new Player
		{
			ObjectId = 1004,
			IsOnline = true,
			Name = "Joiner",
			PlayerClass = "GLADIATOR",
			Level = 10,
		});
		var offlineMember = new PlayerGroupMember(new Player
		{
			ObjectId = 1005,
			IsOnline = false,
			Name = "Offline",
			PlayerClass = "RIDER",
			Level = 20,
		});
		var joinPlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, joiningMember, PlayerGroupEvent.Join);
		var offlinePlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, offlineMember, PlayerGroupEvent.Enter);

		using var joinReader = new PacketBuffer(SerializeUnencryptedPayload(new Network.Aion.ServerPackets.SmGroupMemberInfo(joinPlan)));
		SkipGroupMemberInfoPrefix(joinReader);
		Assert.Equal("Joiner", joinReader.ReadS());
		Assert.Equal(0, joinReader.Remaining);

		using var offlineReader = new PacketBuffer(SerializeUnencryptedPayload(new Network.Aion.ServerPackets.SmGroupMemberInfo(offlinePlan)));
		SkipGroupMemberInfoPrefix(offlineReader);
		Assert.Equal("Offline", offlineReader.ReadS());
		Assert.Equal(0, offlineReader.Remaining);
	}

	[Fact]
	public void SmGroupMemberInfo_WritesEnterAndUpdateZeroEffectSkeletonLikeJava()
	{
		var member = new PlayerGroupMember(new Player
		{
			ObjectId = 1004,
			IsOnline = true,
			Name = "Effectless",
			PlayerClass = "GLADIATOR",
			Level = 10,
		});
		var enterPlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, member, PlayerGroupEvent.Enter);
		var updatePlan = PlayerGroupMemberInfoPacketPlan.FromMember(99001, member, PlayerGroupEvent.Update);

		AssertZeroEffectMemberInfoPayload(enterPlan, "Effectless");
		AssertZeroEffectMemberInfoPayload(updatePlan, "Effectless");
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
	public void PlayerGroupLootRules_DefaultsMatchJavaLootGroupRules()
	{
		var rules = PlayerGroupLootRules.Default();

		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, rules.LootRule);
		Assert.Equal(0, (int)PlayerGroupLootRuleType.FreeForAll);
		Assert.Equal(1, (int)PlayerGroupLootRuleType.RoundRobin);
		Assert.Equal(2, (int)PlayerGroupLootRuleType.Leader);
		Assert.Equal(0, rules.Misc);
		Assert.Equal(0, rules.CommonItemAbove);
		Assert.Equal(2, rules.SuperiorItemAbove);
		Assert.Equal(2, rules.HeroicItemAbove);
		Assert.Equal(2, rules.FabledItemAbove);
		Assert.Equal(2, rules.EternalItemAbove);
		Assert.Equal(2, rules.MythicItemAbove);
		Assert.Equal(2, rules.AutoDistributionId);
		Assert.Equal(3, (rules with { MythicItemAbove = 3 }).AutoDistributionId);
		Assert.Equal(0, (rules with { MythicItemAbove = 0 }).AutoDistributionId);
	}

	[Fact]
	public void UpdateBrand_StoresBrandAndPlansBroadcastLikeJavaTemporaryPlayerTeam()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var plan = Assert.IsType<PlayerGroupBrandUpdatePlan>(runtime.UpdateBrand(99001, brandId: 4, targetObjectId: 8002));

		Assert.Equal(99001, plan.TeamId);
		Assert.Equal(4, plan.BrandId);
		Assert.Equal(8002, plan.TargetObjectId);
		Assert.Collection(
			plan.BrandBroadcasts,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				AssertShowBrandPayload(intent.CreatePacket(), (4, 8002));
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				AssertShowBrandPayload(intent.CreatePacket(), (4, 8002));
			});
		var enteringPlayer = new Player { ObjectId = 1003 };
		runtime.AddMember(99001, enteringPlayer);
		var enteredPlan = Assert.IsType<PlayerGroupEnteredPacketPlan>(runtime.CreateEnteredPacketPlan(99001, enteringPlayer));
		var brandIntent = Assert.IsType<PlayerGroupBrandIntent>(enteredPlan.BrandIntent);
		Assert.Equal(new Dictionary<int, int> { [4] = 8002 }, brandIntent.TargetObjectIdsByBrandId);
	}

	[Fact]
	public void UpdateBrand_ReturnsNullForUnknownGroup()
	{
		var runtime = new PlayerGroupRuntime();

		var plan = runtime.UpdateBrand(99001, brandId: 4, targetObjectId: 8002);

		Assert.Null(plan);
	}

	[Fact]
	public void ChangeLootRules_UpdatesDescriptorAndPlansGroupInfoBroadcastLikeJavaEvent()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player
		{
			ObjectId = 1001,
			Position = new WorldPosition(210010000, 10, 20, 30, 64),
		};
		var member = new Player
		{
			ObjectId = 1002,
			Position = new WorldPosition(220010000, 40, 50, 60, 32),
		};
		runtime.CreateOrUpdateGroup(99001, [leader, member]);
		var changedRules = new PlayerGroupLootRules(
			PlayerGroupLootRuleType.Leader,
			Misc: 9,
			CommonItemAbove: 1,
			SuperiorItemAbove: 2,
			HeroicItemAbove: 3,
			FabledItemAbove: 4,
			EternalItemAbove: 5,
			MythicItemAbove: 6);

		var plan = Assert.IsType<PlayerGroupLootRulesChangedPacketPlan>(runtime.ChangeLootRules(99001, changedRules));

		Assert.Equal(99001, plan.TeamId);
		var descriptor = Assert.IsType<PlayerGroupDescriptor>(runtime.GetDescriptor(99001));
		Assert.Same(changedRules, descriptor.LootRules);
		Assert.Collection(
			plan.GroupInfoBroadcasts,
			intent =>
			{
				Assert.Equal(1001, intent.RecipientObjectId);
				Assert.Same(changedRules, intent.GroupInfoPlan.LootRules);
				Assert.Equal(210010000, intent.GroupInfoPlan.ActivePlayerMapId);
				AssertChangedLootRulesGroupInfoPayload(intent.CreateGroupInfoPacket(), expectedMapId: 210010000);
			},
			intent =>
			{
				Assert.Equal(1002, intent.RecipientObjectId);
				Assert.Same(changedRules, intent.GroupInfoPlan.LootRules);
				Assert.Equal(220010000, intent.GroupInfoPlan.ActivePlayerMapId);
				AssertChangedLootRulesGroupInfoPayload(intent.CreateGroupInfoPacket(), expectedMapId: 220010000);
			});
	}

	[Fact]
	public void ChangeLootRules_ReturnsNullForUnknownGroupWithoutChangingKnownGroups()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		runtime.CreateOrUpdateGroup(99001, [leader]);
		var changedRules = new PlayerGroupLootRules(
			PlayerGroupLootRuleType.Leader,
			Misc: 9,
			CommonItemAbove: 1,
			SuperiorItemAbove: 2,
			HeroicItemAbove: 3,
			FabledItemAbove: 4,
			EternalItemAbove: 5,
			MythicItemAbove: 6);

		var plan = runtime.ChangeLootRules(99002, changedRules);

		Assert.Null(plan);
		var descriptor = Assert.IsType<PlayerGroupDescriptor>(runtime.GetDescriptor(99001));
		Assert.Equal(PlayerGroupLootRuleType.RoundRobin, descriptor.LootRules.LootRule);
		Assert.Equal(0, descriptor.LootRules.Misc);
		Assert.Equal([1001], runtime.GetMemberObjectIds(99001));
	}

	[Fact]
	public void PlayerGroupType_JavaPacketFieldsMatchTeamType()
	{
		Assert.Equal((0x3F, 0), PlayerGroupType.Group.ToJavaPacketFields());
		Assert.Equal((0x02, 1), PlayerGroupType.AutoGroup.ToJavaPacketFields());
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
	public void CreateEnteredPacketPlan_ReturnsNullForPlayerOutsideGroup()
	{
		var runtime = new PlayerGroupRuntime();
		var leader = new Player { ObjectId = 1001 };
		var member = new Player { ObjectId = 1002 };
		var outsider = new Player
		{
			ObjectId = 1003,
			Position = new WorldPosition(220010000, 11, 22, 33, 64),
		};
		runtime.CreateOrUpdateGroup(99001, [leader, member]);

		var plan = runtime.CreateEnteredPacketPlan(99001, outsider);

		Assert.Null(plan);
		Assert.Equal(PlayerTeamMembership.None, outsider.TeamMembership);
		Assert.Null(outsider.CurrentGroupSnapshot);
		Assert.Equal([1001, 1002], runtime.GetMemberObjectIds(99001));
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
	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static void AssertChangedLootRulesGroupInfoPayload(GameServerPacket packet, int expectedMapId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(99001, reader.ReadD());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(expectedMapId, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(9, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(4, reader.ReadD());
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(6, reader.ReadD());
		Assert.Equal(0x02, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0x3F, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertSystemMessage(GameServerPacket packet, int expectedMessageId, params string[] expectedParameters)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(expectedMessageId, reader.ReadD());
		Assert.Equal(expectedParameters.Length, (int)reader.ReadC());
		foreach (var expectedParameter in expectedParameters)
			Assert.Equal(expectedParameter, reader.ReadS());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertAbyssRankUpdateTeamPayload(GameServerPacket packet, int expectedPlayerObjectId, int expectedTeamObjectId)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(expectedPlayerObjectId, reader.ReadD());
		Assert.Equal(expectedTeamObjectId, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertShowBrandPayload(GameServerPacket packet, params (int BrandId, int TargetObjectId)[] expectedBrands)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedBrands.Length, reader.ReadH());
		foreach (var (brandId, targetObjectId) in expectedBrands)
		{
			Assert.Equal(1, reader.ReadD());
			Assert.Equal(brandId, reader.ReadD());
			Assert.Equal(targetObjectId, reader.ReadD());
		}

		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertMemberInfoPlan(
		PlayerGroupMemberInfoPacketPlan? plan,
		int expectedGroupId,
		int expectedMemberObjectId,
		PlayerGroupEvent expectedRequestedEvent,
		PlayerGroupEvent expectedEffectiveEvent,
		bool isOnline,
		bool writesName,
		bool writesEffects,
		int slot = 0)
	{
		var actual = Assert.IsType<PlayerGroupMemberInfoPacketPlan>(plan);
		Assert.Equal(expectedGroupId, actual.GroupId);
		Assert.Equal(expectedMemberObjectId, actual.MemberObjectId);
		Assert.Equal(expectedRequestedEvent, actual.RequestedEvent);
		Assert.Equal(expectedEffectiveEvent, actual.EffectiveEvent);
		Assert.Equal((int)expectedEffectiveEvent, (int)actual.EffectiveEvent);
		Assert.Equal(slot, actual.Slot);
		Assert.Equal(isOnline, actual.IsOnline);
		Assert.True(actual.WritesLifeStatsBlock);
		Assert.True(actual.WritesPositionBlock);
		Assert.True(actual.WritesCommonDataBlock);
		if (!isOnline)
		{
			Assert.Equal(0, actual.PrefixSnapshot.CurrentHp);
			Assert.Equal(0, actual.PrefixSnapshot.CurrentMp);
			Assert.Equal(0, actual.PrefixSnapshot.CurrentFp);
		}
		Assert.Equal((int)expectedEffectiveEvent, actual.PrefixSnapshot.EventId);
		Assert.Equal(1, actual.PrefixSnapshot.AlwaysOne);
		Assert.Equal(writesName, actual.WritesName);
		Assert.Equal(writesEffects, actual.WritesAbnormalEffects);
		Assert.Equal(writesEffects, actual.WritesSlotTimers);
	}

	private static void SkipGroupMemberInfoPrefix(PacketBuffer reader)
	{
		for (var i = 0; i < 11; i++)
			reader.ReadD();
		reader.ReadF();
		reader.ReadF();
		reader.ReadF();
		for (var i = 0; i < 7; i++)
			reader.ReadC();
	}

	private static void AssertZeroEffectMemberInfoPayload(PlayerGroupMemberInfoPacketPlan plan, string expectedName)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new Network.Aion.ServerPackets.SmGroupMemberInfo(plan)));
		SkipGroupMemberInfoPrefix(reader);
		Assert.Equal(expectedName, reader.ReadS());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(127, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadH());
		for (var i = 0; i < 8; i++)
			Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}
}
