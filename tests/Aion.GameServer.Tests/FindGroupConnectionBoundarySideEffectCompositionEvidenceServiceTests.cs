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
