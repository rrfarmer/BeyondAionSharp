using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class CraftingTaskPacketPlanServiceTests
{
	[Fact]
	public void CreateInteractionStartPlan_UsesJavaInitOrderingForFirstCraftStep()
	{
		var plan = CraftingTaskPacketPlanService.CreateInteractionStartPlan(
			playerObjectId: 7001,
			targetObjectId: 8002,
			skillId: 40001,
			CreateTemplate(),
			isComboStart: false);

		Assert.Equal(CraftingTaskPacketPlanStatus.Planned, plan.Status);
		Assert.Equal(2, plan.SelfPackets.Count);
		Assert.Equal(2, plan.BroadcastPackets.Count);
		Assert.False(plan.IsLive);
		Assert.Contains("action=0", plan.JavaSource, StringComparison.Ordinal);

		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(plan.SelfPackets[0]), expectedSkillId: 40001, expectedAction: 0, expectedSuccess: 1000, expectedFailure: 1000);
		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(plan.SelfPackets[1]), expectedSkillId: 40001, expectedAction: 1, expectedSuccess: 0, expectedFailure: 0);
		AssertCraftAnimation(Assert.IsType<SmCraftAnimation>(plan.BroadcastPackets[0]), 7001, 8002, 40001, 0);
		AssertCraftAnimation(Assert.IsType<SmCraftAnimation>(plan.BroadcastPackets[1]), 7001, 8002, 40001, 1);
	}

	[Fact]
	public void CreateInteractionStartPlan_UsesCritProcActionForComboRestart()
	{
		var plan = CraftingTaskPacketPlanService.CreateInteractionStartPlan(
			playerObjectId: 7001,
			targetObjectId: 8002,
			skillId: 40002,
			CreateTemplate(),
			isComboStart: true);

		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(plan.SelfPackets[0]), expectedSkillId: 40002, expectedAction: 3, expectedSuccess: 1000, expectedFailure: 1000);
		Assert.Contains("combo", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateProgressUpdatePlan_UsesProvidedProgressActionAndTimings()
	{
		var plan = CraftingTaskPacketPlanService.CreateProgressUpdatePlan(
			skillId: 40003,
			itemTemplate: CreateTemplate(),
			success: 345,
			failure: 120,
			progressAction: CraftingTaskPacketPlanService.CritBlueProgressAction,
			executionSpeed: 700,
			showBarDelay: 900);

		Assert.Single(plan.SelfPackets);
		Assert.Empty(plan.BroadcastPackets);
		AssertCraftUpdate(
			Assert.IsType<SmCraftUpdate>(plan.SelfPackets[0]),
			expectedSkillId: 40003,
			expectedAction: 2,
			expectedSuccess: 345,
			expectedFailure: 120,
			expectedExecutionSpeed: 700,
			expectedDelay: 900,
			expectedMessageId: 0);
	}

	[Fact]
	public void CreateAbortAndFinishPlans_MirrorJavaPacketBranches()
	{
		var template = CreateTemplate();
		var abort = CraftingTaskPacketPlanService.CreateAbortPlan(7001, 8002, 40004, template);
		var failure = CraftingTaskPacketPlanService.CreateFailureFinishPlan(7001, 8002, 40004, template, success: 200, failure: 1000);
		var success = CraftingTaskPacketPlanService.CreateSuccessFinishPlan(7001, 8002, 40004, template, success: 1000, failure: 300);

		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(abort.SelfPackets.Single()), 40004, 4, 0, 0, expectedMessageId: 1330051);
		AssertCraftAnimation(Assert.IsType<SmCraftAnimation>(abort.BroadcastPackets.Single()), 7001, 8002, 0, 2);

		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(failure.SelfPackets.Single()), 40004, 6, 200, 1000, expectedMessageId: 1330050);
		AssertCraftAnimation(Assert.IsType<SmCraftAnimation>(failure.BroadcastPackets.Single()), 7001, 8002, 0, 3);

		AssertCraftUpdate(Assert.IsType<SmCraftUpdate>(success.SelfPackets.Single()), 40004, 5, 1000, 300, expectedMessageId: 1330049);
		AssertCraftAnimation(Assert.IsType<SmCraftAnimation>(success.BroadcastPackets.Single()), 7001, 8002, 0, 2);
	}

	private static ItemTemplateSummary CreateTemplate() =>
		new(
			TemplateId: 152200001,
			Name: "Crafted Blade",
			DescriptionId: 901001,
			Mask: 0,
			Level: 1,
			ItemGroup: "SWORD",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 1,
			Price: 0,
			ValidEquipmentSlots: 1);

	private static void AssertCraftAnimation(SmCraftAnimation packet, int expectedPlayerObjectId, int expectedTargetObjectId, int expectedSkillId, int expectedAction)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedPlayerObjectId, reader.ReadD());
		Assert.Equal(expectedTargetObjectId, reader.ReadD());
		Assert.Equal(expectedSkillId, reader.ReadH());
		Assert.Equal(expectedAction, reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	private static void AssertCraftUpdate(
		SmCraftUpdate packet,
		int expectedSkillId,
		int expectedAction,
		int expectedSuccess,
		int expectedFailure,
		int expectedExecutionSpeed = 0,
		int expectedDelay = 0,
		int? expectedMessageId = null)
	{
		using var reader = new PacketBuffer(SerializeUnencryptedPayload(packet));
		Assert.Equal(expectedSkillId, reader.ReadH());
		Assert.Equal(expectedAction, reader.ReadC());
		Assert.Equal(152200001, reader.ReadD());
		Assert.Equal(expectedSuccess, reader.ReadD());
		Assert.Equal(expectedFailure, reader.ReadD());
		Assert.Equal(expectedExecutionSpeed, reader.ReadD());
		Assert.Equal(expectedDelay, reader.ReadD());
		Assert.Equal(expectedMessageId ?? GetExpectedMessageId(expectedAction), reader.ReadD());
		reader.ReadS();
		Assert.Equal(0, reader.Remaining);
	}

	private static int GetExpectedMessageId(int action) =>
		action switch
		{
			0 or 3 => 1330048,
			1 or 2 => 0,
			4 => 1330051,
			5 => 1330049,
			6 or 7 => 1330050,
			_ => 0,
		};

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
