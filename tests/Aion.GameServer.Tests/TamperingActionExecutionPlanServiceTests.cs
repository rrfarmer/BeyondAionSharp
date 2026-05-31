using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class TamperingActionExecutionPlanServiceTests
{
	[Fact]
	public void CreateStartPlan_WritesJavaDelayAnimation()
	{
		var plan = TamperingActionExecutionPlanService.CreateStartPlan(1001, 2001, 166030005);

		Assert.Equal(5000, plan.DelayMilliseconds);
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(plan.BroadcastPacket));
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(2001, reader.ReadD());
		Assert.Equal(166030005, reader.ReadD());
		Assert.Equal(5000, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
	}

	[Fact]
	public void CreateMutationPlan_SuccessAtZeroTemperingRaisesLevelAndBuildsSuccessMessage()
	{
		var targetTemplate = new ItemTemplateSummary(110100001, "Tunable Sword", 0, 0, 65, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0, MaxTampering: 5);
		var targetItem = new InventoryItem
		{
			ObjectId = 5001,
			ItemId = 110100001,
			OwnerId = 1001,
			Location = 0,
			Tempering = 0,
			PersistentState = InventoryItemPersistentState.Updated,
		};

		var plan = TamperingActionExecutionPlanService.CreateMutationPlan(
			targetItem,
			targetTemplate,
			membershipLevel: 0,
			tamperingChances: [65f, 65f],
			enableEnchantAnnounce: false,
			playerName: "Tester",
			nextChanceRoll: () => 99f);

		Assert.Equal(TamperingActionMutationStatus.Succeeded, plan.Status);
		Assert.Equal(1, plan.TargetItemUpdate.Tempering);
		Assert.Equal(InventoryItemPersistentState.UpdateRequired, plan.TargetItemUpdate.PersistentState);
		Assert.Equal(1402148, plan.ResultMessage.MessageId);
		Assert.Null(plan.AnnouncementPacket);
	}

	[Fact]
	public void CreateMutationPlan_FailedPlumeResetsBonusBuildsDestroyMessageAndAnnouncementAtTen()
	{
		var targetTemplate = new ItemTemplateSummary(
			166100001,
			"Ascension Plume",
			0,
			0,
			65,
			"PLUME",
			"NORMAL",
			"MYTHIC",
			"PC_ALL",
			1,
			0,
			0,
			TemperingName: "TSHIRT_PHYSICAL",
			MaxTampering: 10);
		var successPlan = TamperingActionExecutionPlanService.CreateMutationPlan(
			new InventoryItem
			{
				ObjectId = 5002,
				ItemId = 166100001,
				OwnerId = 1001,
				Location = 0,
				Tempering = 9,
				RandomPlumeBonus = 7,
			},
			targetTemplate,
			membershipLevel: 0,
			tamperingChances: [65f, 65f],
			enableEnchantAnnounce: true,
			playerName: "Tester",
			nextChanceRoll: () => 0f,
			nextInclusiveRandom: (_, _) => 1);
		var failurePlan = TamperingActionExecutionPlanService.CreateMutationPlan(
			new InventoryItem
			{
				ObjectId = 5003,
				ItemId = 166100001,
				OwnerId = 1001,
				Location = 0,
				Tempering = 5,
				RandomPlumeBonus = 9,
			},
			targetTemplate,
			membershipLevel: 0,
			tamperingChances: [65f, 65f],
			enableEnchantAnnounce: false,
			playerName: "Tester",
			nextChanceRoll: () => 99f);

		Assert.Equal(TamperingActionMutationStatus.Succeeded, successPlan.Status);
		Assert.Equal(10, successPlan.TargetItemUpdate.Tempering);
		Assert.NotNull(successPlan.AnnouncementPacket);
		Assert.Equal(1402154, successPlan.AnnouncementPacket!.MessageId);

		Assert.Equal(TamperingActionMutationStatus.FailedDestroyed, failurePlan.Status);
		Assert.Equal(0, failurePlan.TargetItemUpdate.Tempering);
		Assert.Equal(0, failurePlan.TargetItemUpdate.RandomPlumeBonus);
		Assert.Equal(1402447, failurePlan.ResultMessage.MessageId);
	}

	[Fact]
	public void CalculateChance_UsesJavaPlumeCurveAndMembershipRates()
	{
		var plumeTemplate = new ItemTemplateSummary(166100001, "Plume", 0, 0, 65, "PLUME", "NORMAL", "MYTHIC", "PC_ALL", 1, 0, 0, MaxTampering: 10);
		var weaponTemplate = new ItemTemplateSummary(110100001, "Sword", 0, 0, 65, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0, MaxTampering: 5);

		Assert.Equal(100f, TamperingActionExecutionPlanService.CalculateChance(new InventoryItem { Tempering = 0 }, weaponTemplate, 0, [65f, 70f]));
		Assert.Equal(90f, TamperingActionExecutionPlanService.CalculateChance(new InventoryItem { Tempering = 1 }, plumeTemplate, 0, [65f, 70f]));
		Assert.Equal(25f, TamperingActionExecutionPlanService.CalculateChance(new InventoryItem { Tempering = 9 }, plumeTemplate, 0, [65f, 70f]));
		Assert.Equal(70f, TamperingActionExecutionPlanService.CalculateChance(new InventoryItem { Tempering = 3 }, weaponTemplate, 1, [65f, 70f]));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
