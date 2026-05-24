using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class CraftSkillUpdateServiceTests
{
	[Fact]
	public void RequestLearnSkill_RegistersQuestionForProfessionNpc()
	{
		var player = CreatePlayer();
		var npc = CreateNpc(templateId: 203784);
		var service = new CraftSkillUpdateService();

		var result = service.RequestLearnSkill(player, npc, CreateSkillTemplates());

		Assert.True(result.Handled);
		Assert.Equal(CraftSkillLearnRequestStatus.Requested, result.Status);
		Assert.Equal(1, player.ResponseRequester.Count);
		Assert.NotNull(player.PendingCraftSkillLearnRequest);
		Assert.Equal(40001, player.PendingCraftSkillLearnRequest.SkillId);
		Assert.Equal(3500, player.PendingCraftSkillLearnRequest.Price);
		Assert.Equal(1, player.PendingCraftSkillLearnRequest.TargetSkillLevel);
		Assert.NotNull(result.QuestionWindow);
		Assert.Equal(SmQuestionWindow.CraftAddSkillConfirm, result.QuestionWindow.Code);
	}

	[Fact]
	public void RequestLearnSkill_DuplicateQuestionKeepsOriginalPendingRequest()
	{
		var player = CreatePlayer();
		var service = new CraftSkillUpdateService();
		var first = service.RequestLearnSkill(player, CreateNpc(templateId: 203784), CreateSkillTemplates());

		var duplicate = service.RequestLearnSkill(player, CreateNpc(templateId: 203788, objectId: 9002), CreateSkillTemplates());

		Assert.Equal(CraftSkillLearnRequestStatus.Requested, first.Status);
		Assert.False(duplicate.Handled);
		Assert.Equal(CraftSkillLearnRequestStatus.DuplicateQuestion, duplicate.Status);
		Assert.Equal(40001, player.PendingCraftSkillLearnRequest?.SkillId);
		Assert.Equal(1, player.ResponseRequester.Count);
	}

	[Fact]
	public void RequestLearnSkill_NotUpgradableUsesJavaRankMessages()
	{
		var player = CreatePlayer();
		player.Skills = [new PlayerSkill { SkillId = 30002, SkillLevel = 400 }];
		var service = new CraftSkillUpdateService();

		var result = service.RequestLearnSkill(player, CreateNpc(templateId: 203780), CreateSkillTemplates());

		Assert.True(result.Handled);
		Assert.Equal(CraftSkillLearnRequestStatus.NotUpgradable, result.Status);
		var intent = Assert.Single(result.PacketIntents);
		Assert.Equal(player.ObjectId, intent.RecipientObjectId);
		Assert.IsType<SmSystemMessage>(intent.Packet);
		Assert.Equal(0, player.ResponseRequester.Count);
		Assert.Null(player.PendingCraftSkillLearnRequest);
	}

	[Fact]
	public void HandleResponse_DenyConsumesPendingRequestWithoutMutation()
	{
		var player = CreatePlayer();
		var service = new CraftSkillUpdateService();
		service.RequestLearnSkill(player, CreateNpc(templateId: 203784), CreateSkillTemplates());

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.CraftAddSkillConfirm,
			response: 0,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(CraftSkillLearnResponseStatus.Denied, result.Status);
		Assert.Empty(result.Packets);
		Assert.Empty(player.Skills);
		Assert.Equal(10_000, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.Null(player.PendingCraftSkillLearnRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_AcceptDecreasesKinahAndAddsProfessionSkill()
	{
		var player = CreatePlayer();
		var service = new CraftSkillUpdateService();
		service.RequestLearnSkill(player, CreateNpc(templateId: 203784), CreateSkillTemplates());

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.CraftAddSkillConfirm,
			response: 1,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(CraftSkillLearnResponseStatus.Accepted, result.Status);
		Assert.Equal(6500, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		var skill = Assert.Single(player.Skills);
		Assert.Equal(40001, skill.SkillId);
		Assert.Equal(1, skill.SkillLevel);
		Assert.Equal(SmInventoryUpdateItem.DecreaseKinahLearn, ReadInventoryUpdateType(result.Packets.OfType<SmInventoryUpdateItem>().Single()));
		Assert.Contains(result.Packets, packet => packet is SmSkillList);
		Assert.Null(player.PendingCraftSkillLearnRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	[Fact]
	public void HandleResponse_NotEnoughKinahConsumesPendingRequestWithoutSkillMutation()
	{
		var player = CreatePlayer(kinah: 100);
		var service = new CraftSkillUpdateService();
		service.RequestLearnSkill(player, CreateNpc(templateId: 203784), CreateSkillTemplates());

		var result = service.HandleResponse(
			player,
			SmQuestionWindow.CraftAddSkillConfirm,
			response: 1,
			CreateItemTemplates());

		Assert.True(result.Handled);
		Assert.Equal(CraftSkillLearnResponseStatus.NotEnoughKinah, result.Status);
		Assert.Empty(player.Skills);
		Assert.Equal(100, player.InventoryItems.Single(item => item.ItemId == 182400001).Count);
		Assert.Single(result.Packets);
		Assert.IsType<SmSystemMessage>(result.Packets[0]);
		Assert.Null(player.PendingCraftSkillLearnRequest);
		Assert.Equal(0, player.ResponseRequester.Count);
	}

	private static Player CreatePlayer(long kinah = 10_000)
	{
		return new Player
		{
			ObjectId = 1001,
			Name = "Crafty",
			Level = 10,
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 1, 2, 3, 0),
			InventoryItems =
			[
				new InventoryItem
				{
					ObjectId = 5001,
					ItemId = 182400001,
					Count = kinah,
					Location = 0,
				},
			],
		};
	}

	private static WorldNpc CreateNpc(int templateId, int objectId = 9001)
	{
		return new WorldNpc(
			objectId,
			templateId,
			new NpcTemplateSummary(
				templateId,
				"Craft Master",
				0,
				1,
				"NORMAL",
				"NORMAL",
				"PC_ALL",
				"",
				"NPC",
				FunctionDialogIds: [CmDialogSelect.CombineTask],
				HasTalkInfo: true,
				IsDialogNpc: true),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}

	private static SkillTemplateTable CreateSkillTemplates()
	{
		return new SkillTemplateTable(
		[
			new SkillTemplateSummary(30002, "Essencetapping", 30002, 1, "", "", "", "", 0, 0),
			new SkillTemplateSummary(40001, "Cooking", 40001, 1, "", "", "", "", 0, 0),
			new SkillTemplateSummary(40002, "Weaponsmithing", 40002, 1, "", "", "", "", 0, 0),
		]);
	}

	private static ItemTemplateTable CreateItemTemplates()
	{
		return new ItemTemplateTable(
		[
			new ItemTemplateSummary(182400001, "Kinah", 0, 0, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
		]);
	}

	private static int ReadInventoryUpdateType(SmInventoryUpdateItem packet)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new Aion.Commons.Network.PacketBuffer(payload);
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
