using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class BindPointTeleportKinahInventoryUpdatePacketPlanServiceTests
{
	[Theory]
	[InlineData(BindPointTeleportKinahPersistenceStatus.MissingRow)]
	[InlineData(BindPointTeleportKinahPersistenceStatus.Failed)]
	public void CreatePlan_StoppedPersistenceDecisionProducesNoPacket(
		BindPointTeleportKinahPersistenceStatus persistenceStatus)
	{
		var decision = CreateDecision(currentKinah: 2_000, requiredPrice: 1_000, persistenceStatus);

		var plan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			decision,
			CreateKinahTemplate());

		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.NoPacket, plan.Status);
		Assert.False(plan.IsLive);
		Assert.False(plan.ShouldSendPacket);
		Assert.Null(plan.Packet);
		Assert.Null(plan.UpdateType);
	}

	[Fact]
	public void CreatePlan_NonPositivePriceDecisionProducesNoPacket()
	{
		var decision = CreateDecision(
			currentKinah: 2_000,
			requiredPrice: 0,
			persistenceStatus: null);

		var plan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			decision,
			CreateKinahTemplate());

		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.NoPacket, plan.Status);
		Assert.False(plan.ShouldSendPacket);
		Assert.Null(plan.Packet);
	}

	[Fact]
	public void CreatePlan_SavedDecisionWithoutTemplateProducesMissingTemplate()
	{
		var decision = CreateDecision(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved);

		var plan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			decision,
			kinahTemplate: null);

		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.MissingTemplate, plan.Status);
		Assert.False(plan.ShouldSendPacket);
		Assert.Null(plan.Packet);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, plan.UpdateType);
	}

	[Fact]
	public void CreatePlan_SavedDecisionCreatesDecreaseKinahFlyPacketIntent()
	{
		var decision = CreateDecision(
			currentKinah: 2_000,
			requiredPrice: 1_000,
			BindPointTeleportKinahPersistenceStatus.Saved);

		var plan = BindPointTeleportKinahInventoryUpdatePacketPlanService.CreatePlan(
			decision,
			CreateKinahTemplate());

		Assert.Equal(BindPointTeleportKinahInventoryUpdatePacketPlanStatus.PacketReady, plan.Status);
		Assert.False(plan.IsLive);
		Assert.True(plan.ShouldSendPacket);
		Assert.NotNull(plan.Packet);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, plan.UpdateType);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahFly, ReadInventoryUpdateType(plan.Packet));
	}

	private static BindPointTeleportKinahPersistenceDecision CreateDecision(
		long currentKinah,
		long requiredPrice,
		BindPointTeleportKinahPersistenceStatus? persistenceStatus)
	{
		var callbackPlan = CreateCallbackPlan(currentKinah, requiredPrice);
		var persistenceResult = persistenceStatus == null || callbackPlan.KinahItemUpdate == null
			? null
			: new BindPointTeleportKinahPersistenceResult(
				persistenceStatus.Value,
				PlayerObjectId: callbackPlan.KinahItemUpdate.OwnerId,
				KinahObjectId: callbackPlan.KinahItemUpdate.ObjectId,
				KinahCount: callbackPlan.KinahItemUpdate.Count,
				ShouldRollbackInMemoryMutation: persistenceStatus != BindPointTeleportKinahPersistenceStatus.Saved,
				"InventoryDAO.store(player) dirty item persistence planned as owner-checked C# count update",
				IsLive: false);

		return BindPointTeleportKinahPersistenceDecisionBridgeService.CreateDecision(
			callbackPlan,
			persistenceResult);
	}

	private static BindPointTeleportScheduledCallbackPlan CreateCallbackPlan(long currentKinah, long requiredPrice)
	{
		var playerObjectId = 7001;
		var locId = 6001;
		var kinahPlan = BindPointTeleportScheduledKinahPlanService.CreatePlan(requiredPrice, currentKinah);
		var mutationPlan = BindPointTeleportScheduledKinahMutationPlanService.CreatePlan(
			new Player
			{
				ObjectId = playerObjectId,
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 1824,
						OwnerId = playerObjectId,
						ItemId = BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
						Count = currentKinah,
						Location = BindPointTeleportScheduledKinahMutationPlanService.CubeStorageId,
					},
				],
			},
			requiredPrice);
		var cooldownPlan = BindPointTeleportRuntimeStatePlanService.CreateAddCooldownPlan(
			playerObjectId,
			locId,
			currentTimeMillis: 1_000);
		var fanoutPlan = BindPointTeleportFanoutPlanService.CreatePlan(
			BindPointTeleportFanoutSource.TeleportCooldownBroadcast,
			playerObjectId,
			SmBindPointTeleport.Cooldown(playerObjectId, locId, cooldownSeconds: 600));
		var movementPlan = BindPointTeleportFinalMovementPlanService.CreatePlan(
			new BindPointTeleportDestinationFact(210010000, 1, 2, 3, 0, 210010000, 1),
			playerIsDead: false,
			playerIsAboutToDie: false);
		return BindPointTeleportScheduledCallbackPlanService.CreatePlan(
			kinahPlan,
			cooldownPlan,
			fanoutPlan,
			movementPlan,
			kinahMutationPlan: mutationPlan);
	}

	private static ItemTemplateSummary CreateKinahTemplate()
	{
		return new ItemTemplateSummary(
			BindPointTeleportScheduledKinahMutationPlanService.KinahItemId,
			"Kinah",
			0,
			0,
			1,
			"NONE",
			"NORMAL",
			"COMMON",
			"PC_ALL",
			1,
			0,
			0);
	}

	private static int ReadInventoryUpdateType(SmInventoryUpdateItem packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
		reader.ReadD();
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		return reader.ReadH();
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
