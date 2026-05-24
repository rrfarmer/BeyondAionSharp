using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerExperienceRecoveryServiceTests
{
	private const int KinahItemId = 182400001;

	[Theory]
	[InlineData(100_000, 23_500)]
	[InlineData(500_000, 87_500)]
	[InlineData(1_000_000, 100_000)]
	[InlineData(2_000_000, 200_000)]
	public void CalculateRecoveryPrice_UsesJavaDialogServiceFormula(long recoverableExp, int expectedPrice)
	{
		Assert.Equal(expectedPrice, PlayerExperienceRecoveryService.CalculateRecoveryPrice(recoverableExp));
	}

	[Fact]
	public void RequestDialog_RegistersResponseRequesterAndQuestionWindow()
	{
		var player = CreatePlayer(recoverableExp: 500_000, kinah: 100_000);

		var result = PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		Assert.Equal(PlayerExperienceRecoveryDialogStatus.Requested, result.Status);
		Assert.NotNull(result.QuestionWindow);
		Assert.Equal(SmQuestionWindow.AskRecoverExperience, result.QuestionWindow.Code);
		Assert.Equal(1, player.ResponseRequester.Count);
		Assert.Equal(87_500, player.PendingExperienceRecoveryRequest?.Price);
	}

	[Fact]
	public void RequestDialog_NoRecoverableExperienceSendsJavaMessage()
	{
		var player = CreatePlayer(recoverableExp: 0, kinah: 100_000);

		var result = PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		Assert.Equal(PlayerExperienceRecoveryDialogStatus.NoRecoverableExperience, result.Status);
		Assert.IsType<SmSystemMessage>(result.ResponsePacket);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestDialog_DuplicateQuestionUsesBusyMessageAndLeavesOriginalPendingRequest()
	{
		var player = CreatePlayer(recoverableExp: 500_000, kinah: 100_000);
		var first = PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		var duplicate = PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7002);

		Assert.Equal(PlayerExperienceRecoveryDialogStatus.Requested, first.Status);
		Assert.Equal(PlayerExperienceRecoveryDialogStatus.DuplicateQuestion, duplicate.Status);
		Assert.IsType<SmSystemMessage>(duplicate.ResponsePacket);
		Assert.Equal(7001, player.PendingExperienceRecoveryRequest?.NpcObjectId);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_DenyConsumesPendingRequestWithoutChangingExpOrKinah()
	{
		var player = CreatePlayer(recoverableExp: 500_000, kinah: 100_000);
		PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		var result = PlayerExperienceRecoveryService.HandleResponse(
			player,
			SmQuestionWindow.AskRecoverExperience,
			response: 0,
			CreateItemTemplates(),
			CreateExperienceTable());

		Assert.True(result.Handled);
		Assert.Equal(PlayerExperienceRecoveryResponseStatus.Denied, result.Status);
		Assert.Empty(result.Packets);
		Assert.Null(player.PendingExperienceRecoveryRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(1_000_000, player.Exp);
		Assert.Equal(500_000, player.RecoverableExp);
		Assert.Equal(100_000, Assert.Single(player.InventoryItems).Count);
	}

	[Fact]
	public void HandleResponse_NotEnoughKinahConsumesQuestionButKeepsRecoverableExp()
	{
		var player = CreatePlayer(recoverableExp: 500_000, kinah: 10);
		PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		var result = PlayerExperienceRecoveryService.HandleResponse(
			player,
			SmQuestionWindow.AskRecoverExperience,
			response: 1,
			CreateItemTemplates(),
			CreateExperienceTable());

		Assert.True(result.Handled);
		Assert.Equal(PlayerExperienceRecoveryResponseStatus.NotEnoughKinah, result.Status);
		Assert.Single(result.Packets);
		Assert.Null(player.PendingExperienceRecoveryRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Equal(1_000_000, player.Exp);
		Assert.Equal(500_000, player.RecoverableExp);
		Assert.Equal(10, Assert.Single(player.InventoryItems).Count);
	}

	[Fact]
	public void HandleResponse_AcceptRestoresExpClearsRecoverableAndDecreasesKinah()
	{
		var player = CreatePlayer(recoverableExp: 500_000, kinah: 100_000);
		PlayerExperienceRecoveryService.RequestDialog(player, npcObjectId: 7001);

		var result = PlayerExperienceRecoveryService.HandleResponse(
			player,
			SmQuestionWindow.AskRecoverExperience,
			response: 1,
			CreateItemTemplates(),
			CreateExperienceTable());

		Assert.True(result.Handled);
		Assert.Equal(PlayerExperienceRecoveryResponseStatus.Recovered, result.Status);
		Assert.Equal(1_500_000, player.Exp);
		Assert.Equal(0, player.RecoverableExp);
		Assert.Equal(12_500, Assert.Single(player.InventoryItems).Count);
		Assert.Null(player.PendingExperienceRecoveryRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Contains(result.Packets, packet => packet is SmSystemMessage);
		Assert.Contains(result.Packets, packet => packet is SmStatUpdateExp);
		Assert.Contains(result.Packets, packet => packet is SmInventoryUpdateItem);
	}

	private static Player CreatePlayer(long recoverableExp, long kinah)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Recoverer",
			Exp = 1_000_000,
			RecoverableExp = recoverableExp,
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 9001,
					ItemId = KinahItemId,
					Count = kinah,
					Location = 0,
					OwnerId = 1001,
				},
			],
		};
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(
				KinahItemId,
				"Kinah",
				DescriptionId: 0,
				Mask: 0,
				Level: 1,
				ItemGroup: "MONEY",
				ItemType: "NORMAL",
				Quality: "COMMON",
				Race: "PC_ALL",
				MaxStackCount: 1000000,
				Price: 0,
				ValidEquipmentSlots: 0),
		]);
	}

	private static PlayerExperienceTable CreateExperienceTable()
	{
		return new PlayerExperienceTable([0, 1_000_000, 2_000_000]);
	}
}
