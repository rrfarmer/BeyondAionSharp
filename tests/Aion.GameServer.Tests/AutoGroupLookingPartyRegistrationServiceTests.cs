using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class AutoGroupLookingPartyRegistrationServiceTests
{
	[Fact]
	public void EntryRequestTypeParser_MatchesJavaEntryRequestTypeIds()
	{
		Assert.Equal(AutoGroupEntryRequestType.NewGroupEntry, AutoGroupEntryRequestTypeParser.GetTypeById(0));
		Assert.Equal(AutoGroupEntryRequestType.QuickGroupEntry, AutoGroupEntryRequestTypeParser.GetTypeById(1));
		Assert.Equal(AutoGroupEntryRequestType.GroupEntry, AutoGroupEntryRequestTypeParser.GetTypeById(2));
		Assert.Null(AutoGroupEntryRequestTypeParser.GetTypeById(3));
	}

	[Fact]
	public void StartLooking_RegistersSoloPlayerForValidJavaWindowHundredRequest()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var player = CreatePlayer(objectId: 1001, level: 50);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = service.StartLooking(
			player,
			107,
			AutoGroupEntryRequestType.NewGroupEntry,
			autoGroups);

		Assert.Equal(AutoGroupStartLookingStatus.Registered, result.Status);
		Assert.True(result.RegisteredQueue);
		Assert.Equal(AutoGroupEntryRequestType.NewGroupEntry, result.EntryRequestType);
		Assert.Equal([1001], result.Registration?.MemberObjectIds);
		Assert.True(service.IsSearching(1001, 107));
		Assert.Equal(1, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_RegistersCurrentGroupMembersLikeJavaCreateMembers()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var member = CreatePlayer(objectId: 1002, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, member]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.Registered, result.Status);
		Assert.Equal([1001, 1002], result.Registration?.MemberObjectIds);
		Assert.True(service.IsSearching(1002, 107));
	}

	[Fact]
	public void StartLooking_DuplicateMemberRegistrationIsNoOpLikeJavaAlreadyRegisteredBranch()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var player = CreatePlayer(objectId: 1001, level: 50);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);
		service.StartLooking(player, 107, AutoGroupEntryRequestType.NewGroupEntry, autoGroups);

		var result = service.StartLooking(player, 107, AutoGroupEntryRequestType.QuickGroupEntry, autoGroups);

		Assert.Equal(AutoGroupStartLookingStatus.AlreadyRegistered, result.Status);
		Assert.False(result.RegisteredQueue);
		Assert.Equal(1, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_NewOrQuickEntryRejectsTeamPlayerLikeJavaNotLeader()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var member = CreatePlayer(objectId: 1002, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, member]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.QuickGroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader, result.GuardPlan?.Status);
		Assert.Equal(1400182, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.False(service.IsSearching(leader.ObjectId, 107));
		Assert.Equal(0, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_UnsupportedEntryFlagIsSilentNoOpLikeJavaTemplateFlag()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var player = CreatePlayer(objectId: 1001, level: 50);
		var autoGroups = new AutoGroupTable(
		[
			CreateAutoGroup(107, 300110000, registerQuick: false),
		]);

		var result = service.StartLooking(
			player,
			107,
			AutoGroupEntryRequestType.QuickGroupEntry,
			autoGroups);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedEntryUnsupported, result.GuardPlan?.Status);
		Assert.Null(result.GuardPlan?.DenialMessage);
		Assert.False(service.IsSearching(player.ObjectId, 107));
	}

	[Fact]
	public void StartLooking_GroupEntryRejectsSoloOrNonLeaderLikeJavaNotLeader()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var solo = CreatePlayer(objectId: 1001, level: 50);
		var leader = CreatePlayer(objectId: 1002, level: 50);
		var member = CreatePlayer(objectId: 1003, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, member]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var soloResult = service.StartLooking(solo, 107, AutoGroupEntryRequestType.GroupEntry, autoGroups);
		var memberResult = service.StartLooking(member, 107, AutoGroupEntryRequestType.GroupEntry, autoGroups, groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, soloResult.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader, soloResult.GuardPlan?.Status);
		Assert.Equal(1400182, soloResult.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, memberResult.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedNotLeader, memberResult.GuardPlan?.Status);
		Assert.Equal(1400182, memberResult.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(0, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_PeriodicGroupEntryRejectsTooManyMembersLikeJavaMaxMemberCount()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var member1 = CreatePlayer(objectId: 1002, level: 50);
		var member2 = CreatePlayer(objectId: 1003, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, member1, member2]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);
		var instanceCooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300110000, "PC_ALL", MaxCount: 5, MaxMemberLight: 2, MaxMemberDark: 2),
		]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime,
			instanceCooltimes: instanceCooltimes);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedTooManyMembers, result.GuardPlan?.Status);
		Assert.Equal(1400180, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["2", "300110000"], result.GuardPlan?.DenialMessage?.Parameters);
		Assert.False(service.IsSearching(leader.ObjectId, 107));
	}

	[Fact]
	public void StartLooking_HarmonyGroupEntryRejectsTooManyMembersLikeJavaFixedSize()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var member1 = CreatePlayer(objectId: 1002, level: 50);
		var member2 = CreatePlayer(objectId: 1003, level: 50);
		var member3 = CreatePlayer(objectId: 1004, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, member1, member2, member3]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(33, 300350000)]);

		var result = service.StartLooking(
			leader,
			33,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedTooManyMembers, result.GuardPlan?.Status);
		Assert.Equal(1400180, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["3", "300350000"], result.GuardPlan?.DenialMessage?.Parameters);
		Assert.Equal(0, service.GetLookingPartyCount(33));
	}

	[Fact]
	public void StartLooking_HarmonyGroupEntryRejectsMemberMissingTicketLikeJava()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var ticketedMember = CreatePlayer(objectId: 1002, level: 50);
		ticketedMember.InventoryItems =
		[
			new InventoryItem { ObjectId = 9001, ItemId = PvPArenaAvailabilityPlanService.HarmonyArenaTicketItemId, Count = 1 },
		];
		var missingTicketMember = CreatePlayer(objectId: 1003, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, ticketedMember, missingTicketMember]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(33, 300350000)]);

		var result = service.StartLooking(
			leader,
			33,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedHarmonyMemberMissingItem, result.GuardPlan?.Status);
		Assert.Equal(1400187, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["Player1003"], result.GuardPlan?.DenialMessage?.Parameters);
		var memberDenial = Assert.Single(result.GuardPlan?.MemberDenials ?? []);
		Assert.Equal(missingTicketMember.ObjectId, memberDenial.MemberObjectId);
		Assert.Equal(1400219, memberDenial.Message.MessageId);
		Assert.Equal(0, service.GetLookingPartyCount(33));
	}

	[Fact]
	public void StartLooking_GroupEntryRejectsOutOfLevelMemberLikeJavaEnterMember()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var underleveledMember = CreatePlayer(objectId: 1002, level: 45);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, underleveledMember]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedMemberCannotEnter, result.GuardPlan?.Status);
		Assert.Equal(1400187, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["Player1002"], result.GuardPlan?.DenialMessage?.Parameters);
		Assert.Equal(0, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_GroupEntryRejectsCooldownMemberLikeJavaEnterMember()
	{
		var now = DateTimeOffset.FromUnixTimeMilliseconds(100_000);
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var cooldownMember = CreatePlayer(objectId: 1002, level: 50);
		cooldownMember.PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
		{
			[300110000] = new PlayerPortalCooldown(300110000, ReuseTimeMillis: 200_000, EntryCount: 1),
		};
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, cooldownMember]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);
		var instanceCooltimes = new InstanceCooltimeTable(
		[
			new InstanceCooltimeSummary(8, 300110000, "PC_ALL", MaxCount: 1, MaxMemberLight: 6, MaxMemberDark: 6),
		]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime,
			instanceCooltimes: instanceCooltimes,
			now: now);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedMemberCannotEnter, result.GuardPlan?.Status);
		Assert.Equal(1400187, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["Player1002"], result.GuardPlan?.DenialMessage?.Parameters);
		Assert.Equal(0, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_GroupEntryRejectsSearchingMemberLikeJavaEnterMember()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var groupRuntime = new PlayerGroupRuntime();
		var leader = CreatePlayer(objectId: 1001, level: 50);
		var searchingMember = CreatePlayer(objectId: 1002, level: 50);
		groupRuntime.CreateOrUpdateGroup(teamId: 77, [leader, searchingMember]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);
		service.RegisterLookingParty(107, [searchingMember.ObjectId]);

		var result = service.StartLooking(
			leader,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups,
			groupRuntime);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByEntryGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedMemberCannotEnter, result.GuardPlan?.Status);
		Assert.Equal(1400187, result.GuardPlan?.DenialMessage?.MessageId);
		Assert.Equal(["Player1002"], result.GuardPlan?.DenialMessage?.Parameters);
		Assert.Equal(1, service.GetLookingPartyCount(107));
		Assert.False(service.IsSearching(leader.ObjectId, 107));
	}

	[Fact]
	public void StartLooking_LevelGuardBlocksBeforeQueueMutationLikeJavaCanRegister()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var player = CreatePlayer(objectId: 1001, level: 45);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = service.StartLooking(player, 107, AutoGroupEntryRequestType.GroupEntry, autoGroups);

		Assert.Equal(AutoGroupStartLookingStatus.BlockedByCommonGuard, result.Status);
		Assert.Equal(AutoGroupRegistrationGuardPlanStatus.BlockedLevelOutOfRange, result.GuardPlan?.Status);
		Assert.NotNull(result.GuardPlan?.DenialMessage);
		Assert.False(service.IsSearching(1001, 107));
		Assert.Equal(0, service.GetLookingPartyCount(107));
	}

	[Fact]
	public void StartLooking_MissingAutoGroupIsNoOpLikeJavaNullTypeBranch()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		var player = CreatePlayer(objectId: 1001, level: 50);

		var result = service.StartLooking(
			player,
			107,
			AutoGroupEntryRequestType.GroupEntry,
			autoGroups: null);

		Assert.Equal(AutoGroupStartLookingStatus.MissingAutoGroup, result.Status);
		Assert.False(result.RegisteredQueue);
		Assert.False(service.IsSearching(1001, 107));
	}

	[Fact]
	public async Task StopRegistrationsByMaskId_RemovesMaskQueueAndSendsCancelWindowLikeJava()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001, 1002]);
		service.RegisterLookingParty(107, [1003]);
		service.RegisterLookingParty(108, [2001]);
		var registry = new RecordingConnectionRegistry([1001, 1002, 1003, 2001]);
		var autoGroups = new AutoGroupTable(
		[
			CreateAutoGroup(107, 300110000),
			CreateAutoGroup(108, 300120000),
		]);

		var result = await service.StopRegistrationsByMaskIdAsync(107, autoGroups, registry);

		Assert.Equal(107, result.MaskId);
		Assert.Equal(2, result.RemovedPartyCount);
		Assert.Equal([1001, 1002, 1003], result.RemovedMemberObjectIds);
		Assert.Equal(3, result.SentPackets);
		Assert.True(result.HasAutoGroupData);
		Assert.Equal(0, service.GetLookingPartyCount(107));
		Assert.Equal(1, service.GetLookingPartyCount(108));
		Assert.Collection(
			registry.SentPackets,
			delivery => AssertCancelWindow(delivery, 1001),
			delivery => AssertCancelWindow(delivery, 1002),
			delivery => AssertCancelWindow(delivery, 1003));
	}

	[Fact]
	public async Task StopRegistrationsByMaskId_DoesNotDedupeMemberPacketsLikeJavaLoop()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001]);
		service.RegisterLookingParty(107, [1001]);
		var registry = new RecordingConnectionRegistry([1001]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = await service.StopRegistrationsByMaskIdAsync(107, autoGroups, registry);

		Assert.Equal(2, result.RemovedPartyCount);
		Assert.Equal([1001, 1001], result.RemovedMemberObjectIds);
		Assert.Equal(2, result.SentPackets);
		Assert.Collection(
			registry.SentPackets,
			delivery => AssertCancelWindow(delivery, 1001),
			delivery => AssertCancelWindow(delivery, 1001));
	}

	[Fact]
	public async Task StopRegistrationsByMaskId_MissingMaskIsNoOpLikeJavaRemoveNull()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(108, [2001]);
		var registry = new RecordingConnectionRegistry([2001]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(108, 300120000)]);

		var result = await service.StopRegistrationsByMaskIdAsync(107, autoGroups, registry);

		Assert.Equal(107, result.MaskId);
		Assert.Equal(0, result.RemovedPartyCount);
		Assert.Empty(result.RemovedMemberObjectIds);
		Assert.Equal(0, result.SentPackets);
		Assert.False(result.HasAutoGroupData);
		Assert.Equal(1, service.GetLookingPartyCount(108));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task StopRegistrationsByMaskId_RemovesQueueEvenWhenAutoGroupDataMissing()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001, 1002]);
		var registry = new RecordingConnectionRegistry([1001, 1002]);

		var result = await service.StopRegistrationsByMaskIdAsync(107, autoGroups: null, registry);

		Assert.Equal(1, result.RemovedPartyCount);
		Assert.Equal([1001, 1002], result.RemovedMemberObjectIds);
		Assert.Equal(0, result.SentPackets);
		Assert.False(result.HasAutoGroupData);
		Assert.Equal(0, service.GetLookingPartyCount(107));
		Assert.Empty(registry.SentPackets);
	}

	[Fact]
	public async Task CancelRegistration_LeaderRemovesWholePartyAndSendsCancelWindowLikeJava()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001, 1002]);
		var registry = new RecordingConnectionRegistry([1001, 1002]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = await service.CancelRegistrationAsync(1001, 107, autoGroups, registry);

		Assert.Equal(AutoGroupCancelRegistrationStatus.LeaderPartyRemoved, result.Status);
		Assert.False(result.RemovedMemberOnly);
		Assert.Equal([1001, 1002], result.NotifiedMemberObjectIds);
		Assert.Equal(2, result.SentPackets);
		Assert.Equal(0, service.GetLookingPartyCount(107));
		Assert.False(service.IsSearching(1002, 107));
		Assert.Collection(
			registry.SentPackets,
			delivery => AssertCancelWindow(delivery, 1001),
			delivery => AssertCancelWindow(delivery, 1002));
	}

	[Fact]
	public async Task CancelRegistration_MemberRemovesOnlyMemberAndSendsCancelWindowLikeJava()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001, 1002, 1003]);
		var registry = new RecordingConnectionRegistry([1001, 1002, 1003]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = await service.CancelRegistrationAsync(1002, 107, autoGroups, registry);

		Assert.Equal(AutoGroupCancelRegistrationStatus.MemberRemoved, result.Status);
		Assert.True(result.RemovedMemberOnly);
		Assert.Equal([1002], result.NotifiedMemberObjectIds);
		Assert.Equal(1, result.SentPackets);
		Assert.Equal(1, service.GetLookingPartyCount(107));
		Assert.True(service.IsSearching(1001, 107));
		Assert.False(service.IsSearching(1002, 107));
		Assert.True(service.IsSearching(1003, 107));
		var delivery = Assert.Single(registry.SentPackets);
		AssertCancelWindow(delivery, 1002);
	}

	[Fact]
	public async Task CancelRegistration_MissingEntryIsNoOpLikeJavaNullSearchEntry()
	{
		var service = new AutoGroupLookingPartyRegistrationService();
		service.RegisterLookingParty(107, [1001]);
		var registry = new RecordingConnectionRegistry([1001, 1002]);
		var autoGroups = new AutoGroupTable([CreateAutoGroup(107, 300110000)]);

		var result = await service.CancelRegistrationAsync(1002, 107, autoGroups, registry);

		Assert.Equal(AutoGroupCancelRegistrationStatus.NoRegistration, result.Status);
		Assert.Empty(result.NotifiedMemberObjectIds);
		Assert.Equal(0, result.SentPackets);
		Assert.Equal(1, service.GetLookingPartyCount(107));
		Assert.Empty(registry.SentPackets);
	}

	private static AutoGroupSummary CreateAutoGroup(
		int maskId,
		int worldId,
		bool registerQuick = true,
		bool registerGroup = true,
		bool registerNew = true)
	{
		return new AutoGroupSummary(
			maskId,
			worldId,
			NameId: 140000 + maskId,
			TitleId: 150000 + maskId,
			MinLevel: 46,
			MaxLevel: 65,
			RegisterQuick: registerQuick,
			RegisterGroup: registerGroup,
			RegisterNew: registerNew,
			NpcIds: []);
	}

	private static Player CreatePlayer(int objectId, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = $"Player{objectId}",
			Race = "ELYOS",
			Level = level,
		};
	}

	private static void AssertCancelWindow(PacketDelivery delivery, int playerObjectId)
	{
		Assert.Equal(playerObjectId, delivery.PlayerObjectId);
		var packet = Assert.IsType<SmAutoGroup>(delivery.Packet);
		Assert.Equal(107, packet.MaskId);
		Assert.Equal(2, packet.WindowId);
	}

	private sealed class RecordingConnectionRegistry(IReadOnlyCollection<int> onlineObjectIds) : IGameClientConnectionRegistry
	{
		public List<PacketDelivery> SentPackets { get; } = [];

		public void RegisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public void UnregisterPlayerConnection(int playerObjectId, GameServerConnection connection)
		{
		}

		public bool TryGetOnlinePlayerByName(string playerName, out Player? player)
		{
			player = null;
			return false;
		}

		public void ForEachOnlinePlayer(Action<Player> action)
		{
		}

		public Task<bool> SendPacketToPlayerAsync(int playerObjectId, GameServerPacket packet)
		{
			if (!onlineObjectIds.Contains(playerObjectId))
				return Task.FromResult(false);

			SentPackets.Add(new PacketDelivery(playerObjectId, packet));
			return Task.FromResult(true);
		}

		public Task<int> BroadcastToWorldAsync(GameServerPacket packet, Func<Player, bool>? filter = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> BroadcastToVisiblePlayersAsync(
			WorldPosition sourcePosition,
			int sourceObjectId,
			GameServerPacket packet,
			bool includeSourcePlayer = false,
			Func<Player, bool>? filter = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> RefreshHousingVisibilityAsync(
			IReadOnlyList<WorldHouse> houses,
			HousingTemplateTable? housingTemplates,
			int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> RefreshNpcVisibilityAsync(IReadOnlyList<IWorldNpcObject> npcs, int? playerObjectId = null)
		{
			throw new NotSupportedException();
		}

		public Task<int> BroadcastHouseUpdateAsync(WorldHouse house, HousingTemplateTable? housingTemplates)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyMailReceivedAsync(int recipientObjectId, PlayerMail mail)
		{
			throw new NotSupportedException();
		}

		public Task<bool> NotifyBrokerSettledAsync(int sellerObjectId, long settledKinah)
		{
			throw new NotSupportedException();
		}
	}

	private sealed record PacketDelivery(int PlayerObjectId, GameServerPacket Packet);
}
