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
