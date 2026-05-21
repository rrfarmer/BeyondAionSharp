using Aion.Commons.Network;
using System.Buffers.Binary;
using System.Text;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public class GamePacketTests
{
	[Fact]
	public void SmKey_SerializesJavaShapedUnencryptedFirstFrame()
	{
		var crypt = new GameCrypt(() => 0x01020304);

		var frame = new SmKey().SerializeFrame(crypt);

		Assert.Equal(Convert.FromHexString("0B00C8014437FEAAB4830C"), frame);
		Assert.True(crypt.IsEnabled);
	}

	[Fact]
	public void SmMayLoginIntoGame_WritesExpectedPayloadWhenUnencrypted()
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();

		var frame = new SmMayLoginIntoGame().SerializeFrame(crypt);

		Assert.Equal(Convert.FromHexString("0B0087014478FE00000000"), frame);
	}

	[Fact]
	public void SmMessage_WritesGoldenYellowSystemChatPayload()
	{
		var payload = SerializeUnencryptedPayload(new SmMessage("You cannot use broker with this item."));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(25, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, reader.ReadS());
		Assert.Equal("You cannot use broker with this item.", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmMessage_WritesPlayerPublicChatPayload()
	{
		var player = new Player
		{
			ObjectId = 7001,
			Name = "Kahrun",
			Race = "ELYOS",
			Position = new WorldPosition(210010000, 11, 22, 33, 0),
		};

		var payload = SerializeUnencryptedPayload(new SmMessage(player, "For Atreia", 0));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(7001, reader.ReadD());
		Assert.Equal("Kahrun", reader.ReadS());
		Assert.Equal("For Atreia", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmMessage_WritesShoutCoordinates()
	{
		var player = new Player
		{
			ObjectId = 7002,
			Name = "Marchutan",
			Race = "ASMODIANS",
			Position = new WorldPosition(220010000, 11.5f, 22.5f, 33.5f, 0),
		};

		var payload = SerializeUnencryptedPayload(new SmMessage(player, "Stand firm", 3));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(2, (int)reader.ReadC());
		Assert.Equal(7002, reader.ReadD());
		Assert.Equal("Marchutan", reader.ReadS());
		Assert.Equal("Stand firm", reader.ReadS());
		Assert.Equal(11.5f, reader.ReadF());
		Assert.Equal(22.5f, reader.ReadF());
		Assert.Equal(33.5f, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmChatWindow_WritesPlayerInfoAndNoGroupPayloads()
	{
		var target = new Player
		{
			ObjectId = 7003,
			Name = "Yustiel",
			PlayerClass = "CLERIC",
			Exp = 1500,
		};
		var experienceTable = new PlayerExperienceTable([0, 1000, 3000]);

		var playerInfoPayload = SerializeUnencryptedPayload(new SmChatWindow(target, isGroup: false, experienceTable));
		using var playerInfoReader = new PacketBuffer(playerInfoPayload);
		Assert.Equal(1, (int)playerInfoReader.ReadC());
		Assert.Equal("Yustiel", playerInfoReader.ReadS());
		Assert.Equal(string.Empty, playerInfoReader.ReadS());
		Assert.Equal(2, (int)playerInfoReader.ReadC());
		Assert.Equal(10, playerInfoReader.ReadH());
		Assert.Equal(string.Empty, playerInfoReader.ReadS());
		Assert.Equal(1, playerInfoReader.ReadD());
		Assert.Equal(0, (int)playerInfoReader.ReadC());
		Assert.Equal(0, playerInfoReader.Remaining);

		var groupInfoPayload = SerializeUnencryptedPayload(new SmChatWindow(target, isGroup: true, experienceTable));
		using var groupInfoReader = new PacketBuffer(groupInfoPayload);
		Assert.Equal(4, (int)groupInfoReader.ReadC());
		Assert.Equal("Yustiel", groupInfoReader.ReadS());
		Assert.Equal(0, groupInfoReader.ReadD());
		Assert.Equal(10, (int)groupInfoReader.ReadC());
		Assert.Equal(2, (int)groupInfoReader.ReadC());
		Assert.Equal(0, (int)groupInfoReader.ReadC());
		Assert.Equal(0, groupInfoReader.Remaining);
	}

	[Fact]
	public void CharacterSelectionServerPackets_WriteJavaShapedPayloads()
	{
		Assert.Equal(
			Convert.FromHexString("0000000000000000000000000000000000000000000000000004000000"),
			SerializeUnencryptedPayload(new SmAccountProperties()));
		Assert.Equal(
			Convert.FromHexString("0100000000000000000000000000000000000000000000000004000000"),
			SerializeUnencryptedPayload(new SmAccountProperties(gmPanelEnabled: true)));
		Assert.Equal(
			Convert.FromHexString("0000"),
			SerializeUnencryptedPayload(new SmPong()));
		Assert.Equal(
			Convert.FromHexString("04"),
			SerializeUnencryptedPayload(new SmPingResponse()));
		Assert.Equal(
			Convert.FromHexString("D20400002A000000"),
			SerializeUnencryptedPayload(new SmTimeCheck(42, () => 1234)));
		Assert.Equal(
			Convert.FromHexString("03000000010203"),
			SerializeUnencryptedPayload(new SmChatInit([1, 2, 3])));
		Assert.Equal(
			Convert.FromHexString("03"),
			SerializeUnencryptedPayload(new SmFriendStatus(3)));
		Assert.Equal(
			Convert.FromHexString("D204000000"),
			SerializeUnencryptedPayload(new SmCharacterList(playOk2: 1234)));
		Assert.Equal(
			Convert.FromHexString("16000000"),
			SerializeUnencryptedPayload(new SmCreateCharacter(SmCreateCharacter.ResponseOpenCreationWindow)));
		Assert.Equal(
			Convert.FromHexString("00"),
			SerializeUnencryptedPayload(new SmCharacterSelect(0)));
		Assert.Equal(
			Convert.FromHexString("020300010200000005000000"),
			SerializeUnencryptedPayload(new SmCharacterSelect(type: 2, messageType: 3, wrongCount: 2)));
		Assert.Equal(
			Convert.FromHexString("000000"),
			SerializeUnencryptedPayload(new SmEnterWorldCheck()));
		Assert.Equal(
			Convert.FromHexString("060000"),
			SerializeUnencryptedPayload(new SmEnterWorldCheck(EnterWorldCheckMessage.ReentryTime)));
		Assert.Equal(
			Convert.FromHexString("01000125000100000040E201000000000000"),
			SerializeUnencryptedPayload(
				new SmSkillList(
					[new PlayerSkill { SkillId = 37, SkillLevel = 1 }],
					() => DateTimeOffset.FromUnixTimeSeconds(123456))));
		Assert.Equal(
			Convert.FromHexString("01000025000A000000E8030000"),
			SerializeUnencryptedPayload(
				new SmSkillCooldown(
					[new PlayerSkill { SkillId = 37, SkillLevel = 1 }],
					new Dictionary<int, long> { [700] = 20_000 },
					new SkillTemplateTable(
						[
							new SkillTemplateSummary(
								37,
								"Basic Sword Training",
								281815,
								1,
								"P_EQUIP_ENHANCEDSWORD",
								"P_EQUIP_ENHANCEDSWORD",
								"PHYSICAL",
								"NONE",
								700,
								10),
						]),
					notify: false,
					() => DateTimeOffset.FromUnixTimeMilliseconds(10_000))));
		Assert.Equal(
			Convert.FromHexString("01007B00140000003C000000"),
			SerializeUnencryptedPayload(
				new SmItemCooldown(
					new Dictionary<int, PlayerItemCooldown> { [123] = new(30_000, 60) },
					() => DateTimeOffset.FromUnixTimeMilliseconds(10_000))));
		Assert.Equal(
			Convert.FromHexString("0100FFFF64000000032200000205"),
			SerializeUnencryptedPayload(
				new SmQuestList([new PlayerQuestState(100, "START", 0x22, 2, 5)])));
		Assert.Equal(
			Convert.FromHexString("0100FFFF640000000201"),
			SerializeUnencryptedPayload(
				new SmQuestCompletedList(0, [new PlayerQuestState(100, "COMPLETE", 0, 0, 2)])));
		Assert.Equal(
			Convert.FromHexString("01000000"),
			SerializeUnencryptedPayload(SmQuestCompletedList.CreateLoginPackets(Array.Empty<PlayerQuestState>())[0]));
		Assert.Equal(
			Convert.FromHexString("014D00"),
			SerializeUnencryptedPayload(new SmTitleInfo(77)));
		Assert.Equal(
			Convert.FromHexString("03E90300004D00"),
			SerializeUnencryptedPayload(new SmTitleInfo(new Player { ObjectId = 1001 }, 77)));
		Assert.Equal(
			Convert.FromHexString("060500"),
			SerializeUnencryptedPayload(new SmTitleInfo(6, 5)));
		Assert.Equal(
			Convert.FromHexString("00000100050000000A000000"),
			SerializeUnencryptedPayload(
				new SmTitleInfo(
					[new PlayerTitle(5, 1010)],
					() => DateTimeOffset.FromUnixTimeSeconds(1000))));
		Assert.Equal(
			Convert.FromHexString("0101000B000A00000001"),
			SerializeUnencryptedPayload(
				new SmMotion(
					[new PlayerMotion(11, 1010, true)],
					() => DateTimeOffset.FromUnixTimeSeconds(1000))));
		Assert.Equal(
			Convert.FromHexString("050B0001"),
			SerializeUnencryptedPayload(new SmMotion(11, 1)));
		Assert.Equal(
			Convert.FromHexString("07E90300000B0000000D0000000000"),
			SerializeUnencryptedPayload(
				new SmMotion(
					1001,
					[
						new PlayerMotion(11, 1010, true),
						new PlayerMotion(12, 1010, false),
						new PlayerMotion(13, 1010, true),
					],
					() => DateTimeOffset.FromUnixTimeSeconds(1000))));
		Assert.Equal(
			Convert.FromHexString("0001000A0000000A00"),
			SerializeUnencryptedPayload(
				new SmEmotionList(
					0,
					[new PlayerEmotion(10, 1010)],
					() => DateTimeOffset.FromUnixTimeSeconds(1000))));
		Assert.Equal(
			Convert.FromHexString("646464"),
			SerializeUnencryptedPayload(new SmPrices()));
		Assert.Equal(
			Convert.FromHexString("010100070000000A000000"),
			SerializeUnencryptedPayload(
				new SmRecipeCooldown(
					new Dictionary<int, long> { [7] = 20_000 },
					mode: 1,
					() => DateTimeOffset.FromUnixTimeMilliseconds(10_000))));

		Assert.Equal(
			Convert.FromHexString("000400030001000100"),
			SerializeUnencryptedPayload(
				new SmMailService(
					[
						new PlayerMail(1, 1001, "A", "N", "m", true, 0, 0, 0, 0, DateTime.Now),
						new PlayerMail(2, 1001, "B", "E", "m", true, 0, 0, 0, 1, DateTime.Now),
						new PlayerMail(3, 1001, "C", "B", "m", true, 0, 0, 0, 2, DateTime.Now),
						new PlayerMail(4, 1001, "D", "R", "m", false, 0, 0, 0, 1, DateTime.Now),
					])));

		var mailReceivedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
		var mailListPackets = SmMailService.CreateListPackets(
			1001,
			[
				new PlayerMail(10, 1001, "Old", "Normal", "m", true, 90, 100000, 123, 0, mailReceivedAt.AddMinutes(-2)),
				new PlayerMail(11, 1001, "Read", "Express", "m", false, 0, 0, 55, 1, mailReceivedAt.AddMinutes(-1)),
				new PlayerMail(12, 1001, "Cloud", "BC", "m", true, 0, 0, 999, 2, mailReceivedAt),
			],
			expressOnly: false);
		Assert.Single(mailListPackets);
		var mailListPayload = SerializeUnencryptedPayload(mailListPackets[0]);
		using var mailListReader = new PacketBuffer(mailListPayload);
		Assert.Equal(2, (int)mailListReader.ReadC());
		Assert.Equal(1001, mailListReader.ReadD());
		Assert.Equal(0, (int)mailListReader.ReadC());
		Assert.Equal(65533, mailListReader.ReadH());
		Assert.Equal(12, mailListReader.ReadD());
		Assert.Equal("Cloud", mailListReader.ReadS());
		Assert.Equal("BC", mailListReader.ReadS());
		Assert.Equal(0, (int)mailListReader.ReadC());
		Assert.Equal(0, mailListReader.ReadD());
		Assert.Equal(0, mailListReader.ReadD());
		Assert.Equal(999, mailListReader.ReadQ());
		Assert.Equal(2, (int)mailListReader.ReadC());
		Assert.Equal(11, mailListReader.ReadD());
		Assert.Equal("Read", mailListReader.ReadS());
		Assert.Equal("Express", mailListReader.ReadS());
		Assert.Equal(1, (int)mailListReader.ReadC());
		Assert.Equal(0, mailListReader.ReadD());
		Assert.Equal(0, mailListReader.ReadD());
		Assert.Equal(55, mailListReader.ReadQ());
		Assert.Equal(1, (int)mailListReader.ReadC());
		Assert.Equal(10, mailListReader.ReadD());
		Assert.Equal("Old", mailListReader.ReadS());
		Assert.Equal("Normal", mailListReader.ReadS());
		Assert.Equal(0, (int)mailListReader.ReadC());
		Assert.Equal(90, mailListReader.ReadD());
		Assert.Equal(100000, mailListReader.ReadD());
		Assert.Equal(123, mailListReader.ReadQ());
		Assert.Equal(0, (int)mailListReader.ReadC());
		Assert.Equal(0, mailListReader.Remaining);

		var expressMailPayload = SerializeUnencryptedPayload(
			SmMailService.CreateListPackets(
				1001,
				[
					new PlayerMail(10, 1001, "Old", "Normal", "m", true, 90, 100000, 123, 0, mailReceivedAt.AddMinutes(-2)),
					new PlayerMail(11, 1001, "Read", "Express", "m", false, 0, 0, 55, 1, mailReceivedAt.AddMinutes(-1)),
					new PlayerMail(12, 1001, "Cloud", "BC", "m", true, 0, 0, 999, 2, mailReceivedAt),
				],
				expressOnly: true)[0]);
		using var expressMailReader = new PacketBuffer(expressMailPayload);
		Assert.Equal(2, (int)expressMailReader.ReadC());
		Assert.Equal(1001, expressMailReader.ReadD());
		Assert.Equal(0, (int)expressMailReader.ReadC());
		Assert.Equal(65535, expressMailReader.ReadH());
		Assert.Equal(12, expressMailReader.ReadD());

		var readMail = new PlayerMail(20, 1001, "Sender", "Subject", "Body", true, 0, 0, 500, 1, mailReceivedAt);
		var readMailPayload = SerializeUnencryptedPayload(SmMailService.CreateReadPacket([readMail], readMail, itemTemplates: null));
		using var readMailReader = new PacketBuffer(readMailPayload);
		Assert.Equal(3, (int)readMailReader.ReadC());
		Assert.Equal(1001, readMailReader.ReadD());
		Assert.Equal(65537, readMailReader.ReadD());
		Assert.Equal(1, readMailReader.ReadD());
		Assert.Equal(20, readMailReader.ReadD());
		Assert.Equal(1001, readMailReader.ReadD());
		Assert.Equal("Sender", readMailReader.ReadS());
		Assert.Equal("Subject", readMailReader.ReadS());
		Assert.Equal("Body", readMailReader.ReadS());
		Assert.Equal(0, readMailReader.ReadQ());
		Assert.Equal(0, readMailReader.ReadQ());
		Assert.Equal(0, readMailReader.ReadD());
		Assert.Equal(500, readMailReader.ReadD());
		Assert.Equal(0, readMailReader.ReadD());
		Assert.Equal(0, (int)readMailReader.ReadC());
		Assert.Equal((int)new DateTimeOffset(mailReceivedAt).ToUnixTimeSeconds(), readMailReader.ReadD());
		Assert.Equal(1, (int)readMailReader.ReadC());
		Assert.Equal(0, readMailReader.Remaining);

		var attachedTemplate = new ItemTemplateSummary(
			100000,
			"mail_item",
			40000,
			0x1234,
			1,
			"MATERIAL",
			"NORMAL",
			"COMMON",
			"ALL",
			100,
			10,
			0);
		var attachedItem = new InventoryItem
		{
			ObjectId = 90,
			ItemId = 100000,
			Count = 2,
			OwnerId = 1001,
			Location = 127,
		};
		var attachedMail = new PlayerMail(21, 1001, "ItemSender", "ItemSubject", "ItemBody", true, 90, 100000, 77, 0, mailReceivedAt, attachedItem);
		var attachedMailPayload = SerializeUnencryptedPayload(
			SmMailService.CreateReadPacket(
				[attachedMail, new PlayerMail(22, 1001, "Cloud", "BC", "m", true, 0, 0, 0, 2, mailReceivedAt)],
				attachedMail,
				new ItemTemplateTable([attachedTemplate])));
		using var attachedMailReader = new PacketBuffer(attachedMailPayload);
		Assert.Equal(3, (int)attachedMailReader.ReadC());
		Assert.Equal(1001, attachedMailReader.ReadD());
		Assert.Equal(131074, attachedMailReader.ReadD());
		Assert.Equal(1, attachedMailReader.ReadD());
		Assert.Equal(21, attachedMailReader.ReadD());
		Assert.Equal(1001, attachedMailReader.ReadD());
		Assert.Equal("ItemSender", attachedMailReader.ReadS());
		Assert.Equal("ItemSubject", attachedMailReader.ReadS());
		Assert.Equal("ItemBody", attachedMailReader.ReadS());
		Assert.Equal(90, attachedMailReader.ReadD());
		Assert.Equal(100000, attachedMailReader.ReadD());
		Assert.Equal(1, attachedMailReader.ReadD());
		Assert.Equal(0, attachedMailReader.ReadD());
		Assert.Equal(attachedTemplate.GetClientName(), attachedMailReader.ReadS());
		var attachedBlobSize = attachedMailReader.ReadH();
		Assert.True(attachedBlobSize > 0);
		attachedMailReader.ReadB(attachedBlobSize);
		Assert.Equal(77, attachedMailReader.ReadD());
		Assert.Equal(0, attachedMailReader.ReadD());
		Assert.Equal(0, (int)attachedMailReader.ReadC());
		Assert.Equal((int)new DateTimeOffset(mailReceivedAt).ToUnixTimeSeconds(), attachedMailReader.ReadD());
		Assert.Equal(0, (int)attachedMailReader.ReadC());
		Assert.Equal(0, attachedMailReader.Remaining);

		var deleteItemPayload = SerializeUnencryptedPayload(new SmDeleteItem(90));
		using var deleteItemReader = new PacketBuffer(deleteItemPayload);
		Assert.Equal(90, deleteItemReader.ReadD());
		Assert.Equal(0, (int)deleteItemReader.ReadC());
		Assert.Equal(0, deleteItemReader.Remaining);

		var inventoryUpdatePayload = SerializeUnencryptedPayload(
			new SmInventoryUpdateItem(attachedItem, attachedTemplate, SmInventoryUpdateItem.DecreaseItemUse));
		using var inventoryUpdateReader = new PacketBuffer(inventoryUpdatePayload);
		Assert.Equal(90, inventoryUpdateReader.ReadD());
		Assert.Equal(attachedTemplate.GetClientName(), inventoryUpdateReader.ReadS());
		var updateBlobSize = inventoryUpdateReader.ReadH();
		Assert.True(updateBlobSize > 0);
		inventoryUpdateReader.ReadB(updateBlobSize);
		Assert.Equal(SmInventoryUpdateItem.DecreaseItemUse, inventoryUpdateReader.ReadH());
		Assert.Equal(0, inventoryUpdateReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("0100"),
			SerializeUnencryptedPayload(SmMailService.CreateMailMessage(SmMailService.MailSendSuccess)));

		var systemMessagePayload = SerializeUnencryptedPayload(SmSystemMessage.NotEnoughMoney());
		using var systemMessageReader = new PacketBuffer(systemMessagePayload);
		Assert.Equal(25, (int)systemMessageReader.ReadC());
		Assert.Equal(0, (int)systemMessageReader.ReadC());
		Assert.Equal(0, systemMessageReader.ReadD());
		Assert.Equal(1300388, systemMessageReader.ReadD());
		Assert.Equal(0, (int)systemMessageReader.ReadC());
		Assert.Equal(0, (int)systemMessageReader.ReadC());
		Assert.Equal(0, systemMessageReader.Remaining);
		AssertSystemMessage(SmSystemMessage.FullInventory(), 1300762);
		AssertSystemMessage(SmSystemMessage.ExchangeFullInventory(), 1300366);
		AssertSystemMessage(SmSystemMessage.MailTakeAllCancel(), 1402251);
		AssertSystemMessage(SmSystemMessage.NoSuchUser("Kahrun"), 1300627, "Kahrun");
		AssertSystemMessage(SmSystemMessage.YouExcluded("Kahrun"), 1300628, "Kahrun");
		AssertSystemMessage(SmSystemMessage.WhisperRefuse("Kahrun"), 1300629, "Kahrun");
		AssertSystemMessage(SmSystemMessage.CantWhisperLevel(10), 1310004, "10");
		AssertSystemMessage(SmSystemMessage.CantWhisperOtherRace(), 1401174);
		AssertSystemMessage(SmSystemMessage.BuddyListNotInList(), 1300889);
		AssertSystemMessage(SmSystemMessage.BlockListNotInList(), 1300897);
		AssertSystemMessage(SmSystemMessage.BlockListNoBuddy(), 1300891);
		AssertSystemMessage(SmSystemMessage.BlockListAlreadyBlocked(), 1300894);
		AssertSystemMessage(SmSystemMessage.BuddyListBusy(), 900847);
		AssertSystemMessage(SmSystemMessage.BuddyCantAddWhenAskedQuestion("Friend"), 1300795, "Friend");
		AssertSystemMessage(SmSystemMessage.BuddyListNoBlockedCharacter(), 1300884);
		AssertSystemMessage(SmSystemMessage.RejectedFriend("Friend"), 1390119, "Friend");
		AssertSystemMessage(SmSystemMessage.NotEnoughKinah(12345), 901285, "12345");
		AssertSystemMessage(SmSystemMessage.HousingBidSuccess(6001), 1401265, "6001");
		AssertSystemMessage(SmSystemMessage.HousingBidFail(), 1401348);
		AssertSystemMessage(SmSystemMessage.HousingCantOwnNotCompleteQuest(18802), 1401277, "18802");
		AssertSystemMessage(SmSystemMessage.HousingCantBidTimeout(), 1401274);
		AssertSystemMessage(SmSystemMessage.HousingCantBidMyHouse(), 1401221);
		AssertSystemMessage(SmSystemMessage.HousingCantBidGraceHouse(), 1401224);
		AssertSystemMessage(SmSystemMessage.HousingCantBidOverdue(), 1401349);
		AssertSystemMessage(SmSystemMessage.HousingCantBidLowLevel(21), 1401225, "21");
		AssertSystemMessage(SmSystemMessage.HousingCantBidAlreadyHighest(), 1401222);
		AssertSystemMessage(SmSystemMessage.HousingCantBidOtherHouse(), 1401223);
		AssertSystemMessage(SmSystemMessage.HousingCantBidNotEnoughMoney(500000), 1401283, "500000");
		AssertSystemMessage(SmSystemMessage.HousingCantBidExcessAccount(), 1401497);
		AssertSystemMessage(SmSystemMessage.HousingCantBidLower(), 1401307);
		AssertSystemMessage(SmSystemMessage.HousingPriceChange(750000), 1401324, "750000");
		AssertSystemMessage(SmSystemMessage.HousingCantAuctionTimeout(), 1401308);
		AssertSystemMessage(SmSystemMessage.HousingCantAuctionOverdue(), 1401317);
		AssertSystemMessage(SmSystemMessage.HousingAuctionMyHouse(6001), 1401268, "6001");
		AssertSystemMessage(SmSystemMessage.HousingAuctionAlreadyRegistered(), 1401372);
		AssertSystemMessage(SmSystemMessage.HousingFeeFree(), 1401445);
		AssertSystemMessage(SmSystemMessage.HousingOrderOpenDoor(), 1401379);
		AssertSystemMessage(SmSystemMessage.HousingOrderCloseDoorWithoutFriends(), 1401380);
		AssertSystemMessage(SmSystemMessage.HousingOrderCloseDoorAll(), 1401381);

		var attachmentStatePayload = SerializeUnencryptedPayload(SmMailService.CreateAttachmentState(letterId: 123, attachmentType: 1));
		using var attachmentStateReader = new PacketBuffer(attachmentStatePayload);
		Assert.Equal(5, (int)attachmentStateReader.ReadC());
		Assert.Equal(123, attachmentStateReader.ReadD());
		Assert.Equal(1, (int)attachmentStateReader.ReadC());
		Assert.Equal(1, (int)attachmentStateReader.ReadC());
		Assert.Equal(0, attachmentStateReader.Remaining);

		var deleteMailPayload = SerializeUnencryptedPayload(
			SmMailService.CreateDeletePacket(
				[
					new PlayerMail(30, 1001, "A", "N", "m", true, 0, 0, 0, 0, mailReceivedAt),
					new PlayerMail(31, 1001, "B", "E", "m", false, 0, 0, 0, 1, mailReceivedAt),
					new PlayerMail(32, 1001, "C", "B", "m", true, 0, 0, 0, 2, mailReceivedAt),
				],
				[10, 11]));
		using var deleteMailReader = new PacketBuffer(deleteMailPayload);
		Assert.Equal(6, (int)deleteMailReader.ReadC());
		Assert.Equal(131075, deleteMailReader.ReadD());
		Assert.Equal(1, deleteMailReader.ReadD());
		Assert.Equal(2, deleteMailReader.ReadH());
		Assert.Equal(10, deleteMailReader.ReadD());
		Assert.Equal(11, deleteMailReader.ReadD());
		Assert.Equal(0, deleteMailReader.Remaining);

		var brokerPayload = SerializeUnencryptedPayload(new SmBrokerService(123456));
		using var brokerReader = new PacketBuffer(brokerPayload);
		Assert.Equal(5, (int)brokerReader.ReadC());
		Assert.Equal(123456, brokerReader.ReadQ());
		Assert.Equal(0, brokerReader.ReadD());
		Assert.Equal(0, brokerReader.ReadH());
		Assert.Equal(1, brokerReader.ReadH());
		Assert.Equal(0, (int)brokerReader.ReadC());
		Assert.Equal(0, brokerReader.Remaining);

		var brokerSearchPayload = SerializeUnencryptedPayload(SmBrokerService.CreateEmptySearchedItems(totalItemCount: 12, startPage: 3));
		using var brokerSearchReader = new PacketBuffer(brokerSearchPayload);
		Assert.Equal(0, (int)brokerSearchReader.ReadC());
		Assert.Equal(12, brokerSearchReader.ReadD());
		Assert.Equal(0, (int)brokerSearchReader.ReadC());
		Assert.Equal(3, brokerSearchReader.ReadH());
		Assert.Equal(0, brokerSearchReader.ReadH());
		Assert.Equal(0, brokerSearchReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("01000000000000"),
			SerializeUnencryptedPayload(SmBrokerService.CreateEmptyRegisteredItems()));

		var brokerSettledPayload = SerializeUnencryptedPayload(SmBrokerService.CreateEmptySettledItems(totalItemCount: 0, pageIndex: 2, settledKinah: 77));
		using var brokerSettledReader = new PacketBuffer(brokerSettledPayload);
		Assert.Equal(5, (int)brokerSettledReader.ReadC());
		Assert.Equal(77, brokerSettledReader.ReadQ());
		Assert.Equal(0, brokerSettledReader.ReadD());
		Assert.Equal(2, brokerSettledReader.ReadH());
		Assert.Equal(0, (int)brokerSettledReader.ReadC());
		Assert.Equal(0, brokerSettledReader.ReadH());
		Assert.Equal(0, brokerSettledReader.Remaining);

		var brokerSellWindowPayload = SerializeUnencryptedPayload(SmBrokerService.CreateSellWindow(90));
		using var brokerSellWindowReader = new PacketBuffer(brokerSellWindowPayload);
		Assert.Equal(7, (int)brokerSellWindowReader.ReadC());
		Assert.Equal(0, (int)brokerSellWindowReader.ReadC());
		Assert.Equal(90, brokerSellWindowReader.ReadD());
		Assert.Equal(0, brokerSellWindowReader.ReadD());
		Assert.Equal(0, brokerSellWindowReader.ReadD());
		Assert.Equal(3, (int)brokerSellWindowReader.ReadC());
		Assert.Equal(0, brokerSellWindowReader.ReadQ());
		Assert.Equal(0, brokerSellWindowReader.ReadQ());
		Assert.Equal(0, brokerSellWindowReader.Remaining);
		Assert.Equal(
			Convert.FromHexString("04005A000000"),
			SerializeUnencryptedPayload(SmBrokerService.CreateCancelRegisteredItem(90)));
		Assert.Equal(Convert.FromHexString("0600"), SerializeUnencryptedPayload(SmBrokerService.CreateRemoveSettledIcon()));

		var brokerReturnPayload = SerializeUnencryptedPayload(
			SmInventoryAddItem.CreateBrokerReturn(
				new InventoryItem { ObjectId = 90, ItemId = 100000, Count = 2, OwnerId = 1001, Location = 0, Slot = 65535 },
				attachedTemplate));
		using var brokerReturnReader = new PacketBuffer(brokerReturnPayload);
		Assert.Equal(SmInventoryAddItem.BrokerReturn, brokerReturnReader.ReadH());
		Assert.Equal(1, brokerReturnReader.ReadH());
		var brokerReturnItem = ReadInventoryItemHeader(brokerReturnReader);
		Assert.Equal(90, brokerReturnItem.ObjectId);
		Assert.Equal(100000, brokerReturnItem.ItemId);
		Assert.True(brokerReturnItem.BlobSize > 0);
		Assert.Equal(65535, brokerReturnItem.EquipmentSlot);
		Assert.Equal(0, brokerReturnItem.IsCloth);
		Assert.Equal(0, brokerReturnReader.Remaining);
		Assert.Equal(
			Convert.FromHexString("000001000000020304"),
			SerializeUnencryptedPayload(
				SmCubeUpdate.CubeSize(
					new Player
					{
						NpcExpands = 2,
						QuestExpands = 3,
						ItemExpands = 4,
						InventoryItems =
						[
							new InventoryItem { ObjectId = 77, ItemId = 182400001, Count = 10, Location = 0 },
							new InventoryItem { ObjectId = 90, ItemId = 100000, Count = 2, Location = 0 },
						],
					})));

		var brokerItem = new PlayerBrokerItem(
			90,
			100000,
			2,
			"Maker",
			50,
			1001,
			"Seller",
			"ELYOS",
			IsSold: false,
			IsSettled: false,
			DateTime.Now.AddDays(7),
			DateTime.Now,
			SplittingAvailable: true,
			new InventoryItem { ObjectId = 90, ItemId = 100000, Count = 2 });
		var brokerRegisteredPayload = SerializeUnencryptedPayload(SmBrokerService.CreateRegisteredItems([brokerItem]));
		using var brokerRegisteredReader = new PacketBuffer(brokerRegisteredPayload);
		Assert.Equal(1, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(0, brokerRegisteredReader.ReadD());
		Assert.Equal(1, brokerRegisteredReader.ReadH());
		Assert.Equal(90, brokerRegisteredReader.ReadD());
		Assert.Equal(100000, brokerRegisteredReader.ReadD());
		Assert.Equal(100, brokerRegisteredReader.ReadQ());
		Assert.Equal(2, brokerRegisteredReader.ReadQ());
		Assert.Equal(2, brokerRegisteredReader.ReadQ());
		Assert.InRange((int)brokerRegisteredReader.ReadC(), 6, 7);
		brokerRegisteredReader.ReadB(138);
		Assert.Equal("Maker", brokerRegisteredReader.ReadS());
		Assert.Equal(0, brokerRegisteredReader.ReadH());
		Assert.Equal(0, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(0x11, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(0, brokerRegisteredReader.ReadD());
		Assert.Equal(0x12, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(0, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(1, (int)brokerRegisteredReader.ReadC());
		Assert.Equal(0, brokerRegisteredReader.Remaining);

		var brokerRegisterSuccessPayload = SerializeUnencryptedPayload(SmBrokerService.CreateRegisterItem(brokerItem, registeredItemsCount: 2));
		using var brokerRegisterSuccessReader = new PacketBuffer(brokerRegisterSuccessPayload);
		Assert.Equal(3, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(0, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(3, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(90, brokerRegisterSuccessReader.ReadD());
		Assert.Equal(100000, brokerRegisterSuccessReader.ReadD());
		Assert.Equal(100, brokerRegisterSuccessReader.ReadQ());
		Assert.Equal(2, brokerRegisterSuccessReader.ReadQ());
		Assert.Equal(2, brokerRegisterSuccessReader.ReadQ());
		Assert.InRange((int)brokerRegisterSuccessReader.ReadC(), 6, 7);
		brokerRegisterSuccessReader.ReadB(138);
		Assert.Equal("Maker", brokerRegisterSuccessReader.ReadS());
		Assert.Equal(0, brokerRegisterSuccessReader.ReadH());
		Assert.Equal(0, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(0x11, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(0, brokerRegisterSuccessReader.ReadD());
		Assert.Equal(0x12, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(0, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(1, (int)brokerRegisterSuccessReader.ReadC());
		Assert.Equal(0, brokerRegisterSuccessReader.Remaining);

		var brokerRegisterErrorPayload = SerializeUnencryptedPayload(SmBrokerService.CreateRegisterMessage(5));
		using var brokerRegisterErrorReader = new PacketBuffer(brokerRegisterErrorPayload);
		Assert.Equal(3, (int)brokerRegisterErrorReader.ReadC());
		Assert.Equal(5, (int)brokerRegisterErrorReader.ReadC());
		Assert.All(brokerRegisterErrorReader.ReadB(174), value => Assert.Equal(0, value));
		Assert.Equal(255, brokerRegisterErrorReader.ReadH());
		Assert.All(brokerRegisterErrorReader.ReadB(7), value => Assert.Equal(0, value));
		Assert.Equal(0, brokerRegisterErrorReader.Remaining);

		var brokerSearchedPayload = SerializeUnencryptedPayload(
			SmBrokerService.CreateSearchedItems(
				new PlayerBrokerItemPage([brokerItem with { AveragePrice = 75 }], 1, 0, 0)));
		using var brokerSearchedReader = new PacketBuffer(brokerSearchedPayload);
		Assert.Equal(0, (int)brokerSearchedReader.ReadC());
		Assert.Equal(1, brokerSearchedReader.ReadD());
		Assert.Equal(0, (int)brokerSearchedReader.ReadC());
		Assert.Equal(0, brokerSearchedReader.ReadH());
		Assert.Equal(1, brokerSearchedReader.ReadH());
		Assert.Equal(90, brokerSearchedReader.ReadD());
		Assert.Equal(100000, brokerSearchedReader.ReadD());
		Assert.Equal(100, brokerSearchedReader.ReadQ());
		Assert.Equal(75, brokerSearchedReader.ReadQ());
		Assert.Equal(2, brokerSearchedReader.ReadQ());
		brokerSearchedReader.ReadB(138);
		Assert.Equal("Seller", brokerSearchedReader.ReadS());
		Assert.Equal("Maker", brokerSearchedReader.ReadS());
		Assert.Equal(0, brokerSearchedReader.ReadH());
		Assert.Equal(0, (int)brokerSearchedReader.ReadC());
		Assert.Equal(0x11, (int)brokerSearchedReader.ReadC());
		Assert.Equal(0, brokerSearchedReader.ReadD());
		Assert.Equal(0x12, (int)brokerSearchedReader.ReadC());
		Assert.Equal(0, (int)brokerSearchedReader.ReadC());
		Assert.Equal(1, (int)brokerSearchedReader.ReadC());
		Assert.Equal(0, brokerSearchedReader.Remaining);

		var settleTime = new DateTime(2026, 1, 1, 10, 30, 0, DateTimeKind.Local);
		var brokerSettledSoldPayload = SerializeUnencryptedPayload(
			SmBrokerService.CreateSettledItems(
				new PlayerBrokerItemPage(
					[
						brokerItem with
						{
							IsSold = true,
							IsSettled = true,
							SettleTime = settleTime,
							Item = null,
						},
					],
					1,
					0,
					100)));
		using var brokerSettledSoldReader = new PacketBuffer(brokerSettledSoldPayload);
		Assert.Equal(5, (int)brokerSettledSoldReader.ReadC());
		Assert.Equal(100, brokerSettledSoldReader.ReadQ());
		Assert.Equal(1, brokerSettledSoldReader.ReadD());
		Assert.Equal(0, brokerSettledSoldReader.ReadH());
		Assert.Equal(0, (int)brokerSettledSoldReader.ReadC());
		Assert.Equal(1, brokerSettledSoldReader.ReadH());
		Assert.Equal(100000, brokerSettledSoldReader.ReadD());
		Assert.Equal(100, brokerSettledSoldReader.ReadQ());
		Assert.Equal(2, brokerSettledSoldReader.ReadQ());
		Assert.Equal(2, brokerSettledSoldReader.ReadQ());
		Assert.Equal((int)(new DateTimeOffset(settleTime).ToUnixTimeSeconds() / 60), brokerSettledSoldReader.ReadD());
		Assert.All(brokerSettledSoldReader.ReadB(138), value => Assert.Equal(0, value));
		Assert.Equal("Maker", brokerSettledSoldReader.ReadS());
		Assert.Equal(0, brokerSettledSoldReader.Remaining);

		var houseNow = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
		var housePayload = SerializeUnencryptedPayload(
			new SmHouseOwnerInfo(
				new Player
				{
					Race = "ELYOS",
					Houses =
					[
						new PlayerHouse(50, 700100, 900100, houseNow.AddDays(-30), houseNow.AddDays(14), false),
						new PlayerHouse(51, 700200, 900200, houseNow, null, true),
					],
				},
				() => houseNow));
		using var houseReader = new PacketBuffer(housePayload);
		Assert.Equal(700100, houseReader.ReadD());
		Assert.Equal(900100, houseReader.ReadD());
		Assert.Equal(5, (int)houseReader.ReadC());
		Assert.Equal(0, (int)houseReader.ReadC());
		Assert.Equal(3, houseReader.ReadD());
		Assert.Equal(700200, houseReader.ReadD());
		Assert.Equal(900200, houseReader.ReadD());
		Assert.Equal(1209600, houseReader.ReadD());
		Assert.Equal(0, houseReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("00000000"),
			SerializeUnencryptedPayload(new SmReceiveBids(0)));
		Assert.Equal(
			Convert.FromHexString("0002"),
			SerializeUnencryptedPayload(new SmHousePayRent(2)));
		var houseAcquirePayload = SerializeUnencryptedPayload(new SmHouseAcquire(1001, 6001, acquire: true));
		using var houseAcquireReader = new PacketBuffer(houseAcquirePayload);
		Assert.Equal(1001, houseAcquireReader.ReadD());
		Assert.Equal(6001, houseAcquireReader.ReadD());
		Assert.Equal(1, houseAcquireReader.ReadD());
		Assert.Equal(0, houseAcquireReader.Remaining);

		var emptyHouseBidsPayload = SerializeUnencryptedPayload(SmHouseBids.CreateEmpty());
		using var emptyHouseBidsReader = new PacketBuffer(emptyHouseBidsPayload);
		Assert.Equal(1, (int)emptyHouseBidsReader.ReadC());
		Assert.Equal(1, (int)emptyHouseBidsReader.ReadC());
		Assert.Equal(0, emptyHouseBidsReader.ReadD());
		Assert.Equal(0, emptyHouseBidsReader.ReadQ());
		Assert.Equal(0, emptyHouseBidsReader.ReadD());
		Assert.Equal(0, emptyHouseBidsReader.ReadQ());
		Assert.Equal(0, emptyHouseBidsReader.ReadH());
		Assert.Equal(0, emptyHouseBidsReader.Remaining);

		var houseBidRefresh = SmReceiveBids.CreateLoginPacket(
			new Player
			{
				LastOnline = houseNow.AddMinutes(-5),
				Mailbox =
				[
					new PlayerMail(500, 1001, "$$HS_AUCTION_MAIL", "4,0", "body,700100", true, 0, 0, 0, 0, houseNow),
				],
			});
		Assert.NotNull(houseBidRefresh);

		var houseBidSystemMessages = SmReceiveBids.CreateLoginSystemMessages(
			new Player
			{
				LastOnline = houseNow.AddMinutes(-5),
				Mailbox =
				[
					new PlayerMail(502, 1001, "$$HS_AUCTION_MAIL", "4,0", "body,700100", true, 0, 0, 0, 0, houseNow),
					new PlayerMail(503, 1001, "$$HS_AUCTION_MAIL", "2,0", "body,700200", true, 0, 0, 0, 0, houseNow),
					new PlayerMail(504, 1001, "$$HS_AUCTION_MAIL", "7,0", "body,700300", true, 0, 0, 0, 0, houseNow),
					new PlayerMail(505, 1001, "$$HS_AUCTION_MAIL", "0,0", "body,0", true, 0, 0, 0, 0, houseNow),
				],
			});
		Assert.Collection(
			houseBidSystemMessages,
			message => AssertSystemMessage(message, 1401267, "700100"),
			message => AssertSystemMessage(message, 1401270, "700200"),
			message => AssertSystemMessage(message, 1401269, "700300"),
			message => AssertSystemMessage(message, 1401266));

		var oldHouseBidRefresh = SmReceiveBids.CreateLoginPacket(
			new Player
			{
				LastOnline = houseNow.AddMinutes(5),
				Mailbox =
				[
					new PlayerMail(501, 1001, "$$HS_AUCTION_MAIL", "4,0", "body,700100", true, 0, 0, 0, 0, houseNow),
				],
			});
		Assert.Null(oldHouseBidRefresh);

		var macroPackets = SmMacroList.CreateLoginPackets(1001, [new PlayerMacro(1, "<m/>"), new PlayerMacro(12, "two")]);
		Assert.Single(macroPackets);
		var macroPayload = SerializeUnencryptedPayload(macroPackets[0]);
		using var macroReader = new PacketBuffer(macroPayload);
		Assert.Equal(1001, macroReader.ReadD());
		Assert.Equal(1, (int)macroReader.ReadC());
		Assert.Equal(65534, macroReader.ReadH());
		Assert.Equal(1, (int)macroReader.ReadC());
		Assert.Equal("<m/>", macroReader.ReadS());
		Assert.Equal(12, (int)macroReader.ReadC());
		Assert.Equal("two", macroReader.ReadS());
		Assert.Equal(0, macroReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("E9030000010000"),
			SerializeUnencryptedPayload(SmMacroList.CreateLoginPackets(1001, Array.Empty<PlayerMacro>())[0]));

		var friendListPayload = SerializeUnencryptedPayload(
			new SmFriendList(
				[new PlayerFriend(44, "Friend", 1000, "RANGER", "FEMALE", 210010000, null, "note", "memo", false)],
				new PlayerExperienceTable([0, 1000, 3000])));
		using var friendListReader = new PacketBuffer(friendListPayload);
		Assert.Equal(65535, friendListReader.ReadH());
		Assert.Equal(0, (int)friendListReader.ReadC());
		Assert.Equal(44, friendListReader.ReadD());
		Assert.Equal("Friend", friendListReader.ReadS());
		Assert.Equal(2, friendListReader.ReadD());
		Assert.Equal(5, friendListReader.ReadD());
		Assert.Equal(1, (int)friendListReader.ReadC());
		Assert.Equal(210010000, friendListReader.ReadD());
		Assert.Equal(0, friendListReader.ReadD());
		Assert.Equal("note", friendListReader.ReadS());
		Assert.Equal(0, (int)friendListReader.ReadC());
		Assert.Equal(0, friendListReader.ReadD());
		Assert.Equal(0, (int)friendListReader.ReadC());
		Assert.Equal("memo", friendListReader.ReadS());
		Assert.Equal(0, friendListReader.Remaining);

		var friendUpdatePayload = SerializeUnencryptedPayload(
			new SmFriendUpdate(
				new PlayerFriend(44, "Friend", 3000, "RANGER", "FEMALE", 210010000, null, "note", "memo", true),
				status: 3,
				new PlayerExperienceTable([0, 1000, 3000])));
		using var friendUpdateReader = new PacketBuffer(friendUpdatePayload);
		Assert.Equal("Friend", friendUpdateReader.ReadS());
		Assert.Equal(2, friendUpdateReader.ReadD());
		Assert.Equal(5, friendUpdateReader.ReadD());
		Assert.Equal(1, (int)friendUpdateReader.ReadC());
		Assert.Equal(210010000, friendUpdateReader.ReadD());
		Assert.Equal(0, friendUpdateReader.ReadD());
		Assert.Equal("note", friendUpdateReader.ReadS());
		Assert.Equal(3, (int)friendUpdateReader.ReadC());
		Assert.Equal(0, friendUpdateReader.Remaining);

		var blockListPayload = SerializeUnencryptedPayload(
			new SmBlockList([new PlayerBlockedUser(55, "Blocked", "reason")]));
		using var blockListReader = new PacketBuffer(blockListPayload);
		Assert.Equal(65535, blockListReader.ReadH());
		Assert.Equal(0, (int)blockListReader.ReadC());
		Assert.Equal("Blocked", blockListReader.ReadS());
		Assert.Equal("reason", blockListReader.ReadS());
		Assert.Equal(0, blockListReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("E9030000010000"),
			SerializeUnencryptedPayload(new SmMarkFriendList(1001)));

		var friendResponsePayload = SerializeUnencryptedPayload(new SmFriendResponse(SmFriendResponse.TargetRemoved, "Friend"));
		using var friendResponseReader = new PacketBuffer(friendResponsePayload);
		Assert.Equal("Friend", friendResponseReader.ReadS());
		Assert.Equal(SmFriendResponse.TargetRemoved, friendResponseReader.ReadC());
		Assert.Equal(0, friendResponseReader.Remaining);

		var blockResponsePayload = SerializeUnencryptedPayload(new SmBlockResponse(SmBlockResponse.EditNote, "Blocked"));
		using var blockResponseReader = new PacketBuffer(blockResponsePayload);
		Assert.Equal("Blocked", blockResponseReader.ReadS());
		Assert.Equal(SmBlockResponse.EditNote, blockResponseReader.ReadC());
		Assert.Equal(0, blockResponseReader.Remaining);

		var friendNotifyPayload = SerializeUnencryptedPayload(new SmFriendNotify(SmFriendNotify.Deleted, "Kahrun"));
		using var friendNotifyReader = new PacketBuffer(friendNotifyPayload);
		Assert.Equal("Kahrun", friendNotifyReader.ReadS());
		Assert.Equal(SmFriendNotify.Deleted, friendNotifyReader.ReadC());
		Assert.Equal(0, friendNotifyReader.Remaining);

		var questionWindowPayload = SerializeUnencryptedPayload(
			new SmQuestionWindow(SmQuestionWindow.BuddyListAddBuddyRequest, 1001, 0, "Kahrun", "hello"));
		using var questionWindowReader = new PacketBuffer(questionWindowPayload);
		Assert.Equal(SmQuestionWindow.BuddyListAddBuddyRequest, questionWindowReader.ReadD());
		Assert.Equal("Kahrun", questionWindowReader.ReadS());
		Assert.Equal("hello", questionWindowReader.ReadS());
		Assert.Equal(string.Empty, questionWindowReader.ReadS());
		Assert.Equal(0, questionWindowReader.ReadD());
		Assert.Equal(0, (int)questionWindowReader.ReadC());
		Assert.Equal(1001, questionWindowReader.ReadD());
		Assert.Equal(0, questionWindowReader.ReadD());
		Assert.Equal(0, questionWindowReader.Remaining);

		var instanceInfoPayload = SerializeUnencryptedPayload(
			new SmInstanceInfo(
				2,
				new Player
				{
					ObjectId = 1001,
					Name = "Character",
					Race = "ELYOS",
					PortalCooldowns = new Dictionary<int, PlayerPortalCooldown>
					{
						[300030000] = new(300030000, 20_000, 2),
					},
				},
				new InstanceCooltimeTable(
					[
						new InstanceCooltimeSummary(8, 300030000, "PC_ALL", 5),
						new InstanceCooltimeSummary(9, 300040000, "ASMODIANS", 1),
					]),
				() => DateTimeOffset.FromUnixTimeMilliseconds(10_000)));
		using var instanceInfoReader = new PacketBuffer(instanceInfoPayload);
		Assert.Equal(2, (int)instanceInfoReader.ReadC());
		Assert.Equal(0, instanceInfoReader.ReadD());
		Assert.Equal(0, (int)instanceInfoReader.ReadC());
		Assert.Equal(1, instanceInfoReader.ReadH());
		Assert.Equal(1001, instanceInfoReader.ReadD());
		Assert.Equal(2, instanceInfoReader.ReadH());
		Assert.Equal(8, instanceInfoReader.ReadD());
		Assert.Equal(0, instanceInfoReader.ReadD());
		Assert.Equal(10, instanceInfoReader.ReadD());
		Assert.Equal(5, instanceInfoReader.ReadD());
		Assert.Equal(-2, instanceInfoReader.ReadD());
		Assert.Equal(1, (int)instanceInfoReader.ReadC());
		Assert.Equal(9, instanceInfoReader.ReadD());
		Assert.Equal(0, instanceInfoReader.ReadD());
		Assert.Equal(0, instanceInfoReader.ReadD());
		Assert.Equal(1, instanceInfoReader.ReadD());
		Assert.Equal(0, instanceInfoReader.ReadD());
		Assert.Equal(0, (int)instanceInfoReader.ReadC());
		Assert.Equal("Character", instanceInfoReader.ReadS());
		Assert.Equal(0, instanceInfoReader.Remaining);

		var abyssRankPayload = SerializeUnencryptedPayload(
			new SmAbyssRank(new PlayerAbyssRank(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15)));
		using var abyssRankReader = new PacketBuffer(abyssRankPayload);
		Assert.Equal(3, abyssRankReader.ReadQ());
		Assert.Equal(6, abyssRankReader.ReadD());
		Assert.Equal(7, abyssRankReader.ReadD());
		Assert.Equal(15, abyssRankReader.ReadD());
		Assert.Equal(0, abyssRankReader.ReadD());
		Assert.Equal(10, abyssRankReader.ReadD());
		Assert.Equal(11, abyssRankReader.ReadD());
		Assert.Equal(8, abyssRankReader.ReadD());
		Assert.Equal(1, abyssRankReader.ReadQ());
		Assert.Equal(4, abyssRankReader.ReadD());
		Assert.Equal(9, abyssRankReader.ReadD());
		Assert.Equal(2, abyssRankReader.ReadQ());
		Assert.Equal(5, abyssRankReader.ReadD());
		Assert.Equal(12, abyssRankReader.ReadD());
		Assert.Equal(13, abyssRankReader.ReadQ());
		Assert.Equal(14, abyssRankReader.ReadD());
		Assert.Equal(0, (int)abyssRankReader.ReadC());
		Assert.Equal(0, abyssRankReader.Remaining);

		var recipeListPayload = SerializeUnencryptedPayload(new SmRecipeList([155000001, 155000002]));
		using var recipeListReader = new PacketBuffer(recipeListPayload);
		Assert.Equal(2, recipeListReader.ReadH());
		Assert.Equal(155000001, recipeListReader.ReadD());
		Assert.Equal(0, (int)recipeListReader.ReadC());
		Assert.Equal(155000002, recipeListReader.ReadD());
		Assert.Equal(0, (int)recipeListReader.ReadC());
		Assert.Equal(0, recipeListReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("010000000000"),
			SerializeUnencryptedPayload(new SmAfterTimeCheck475()));

		var uiSettings = SerializeUnencryptedPayload(new SmUiSettings([0xAA, 0xBB], type: 1));
		Assert.Equal(0x1c00 + 3, uiSettings.Length);
		Assert.True(uiSettings.AsSpan(0, 5).SequenceEqual(Convert.FromHexString("01001CAABB")));
		Assert.True(uiSettings.AsSpan(5).SequenceEqual(new byte[0x1c00 - 2]));

		var inventoryPackets = SmInventoryInfo.CreateLoginPackets(
			new Player
			{
				NpcExpands = 2,
				QuestExpands = 3,
				ItemExpands = 4,
				InventoryItems =
				[
					new InventoryItem { ObjectId = 88, ItemId = 100, Count = 1, IsEquipped = true, Location = 0, Slot = 1 },
				],
			},
			new ItemTemplateTable(
				[
					new ItemTemplateSummary(182400001, "Kinah", 0, 12350, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
					new ItemTemplateSummary(100, "Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3),
				]),
			() => 77);
		Assert.Equal(2, inventoryPackets.Count);

		var inventoryPayload = SerializeUnencryptedPayload(inventoryPackets[0]);
		using var inventoryReader = new PacketBuffer(inventoryPayload);
		Assert.Equal(1, (int)inventoryReader.ReadC());
		Assert.Equal(2, (int)inventoryReader.ReadC());
		Assert.Equal(3, (int)inventoryReader.ReadC());
		Assert.Equal(4, (int)inventoryReader.ReadC());
		Assert.Equal(2, inventoryReader.ReadH());
		Assert.Equal((77, 182400001, 34, 65535, 0), ReadInventoryItemHeader(inventoryReader));
		Assert.Equal((88, 100, 203, 1, 0), ReadInventoryItemHeader(inventoryReader));
		Assert.Equal(0, inventoryReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("000203040000"),
			SerializeUnencryptedPayload(inventoryPackets[1]));

		var warehousePackets = SmWarehouseInfo.CreateLoginPackets(
			new Player
			{
				WarehouseNpcExpands = 1,
				WarehouseBonusExpands = 2,
				WarehouseItems =
				[
					new InventoryItem { ObjectId = 88, ItemId = 100, Count = 1, Location = 1, Slot = 1 },
				],
				AccountWarehouseItems =
				[
					new InventoryItem { ObjectId = 89, ItemId = 182400001, Count = 10, Location = 2, Slot = 0 },
					new InventoryItem { ObjectId = 90, ItemId = 100, Count = 2, Location = 2, Slot = 2 },
				],
			},
			new ItemTemplateTable(
				[
					new ItemTemplateSummary(182400001, "Kinah", 0, 12350, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
					new ItemTemplateSummary(100, "Sword", 0, 1, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3),
				]),
			includeAuxiliaryStoragePlaceholders: false);
		Assert.Equal(4, warehousePackets.Count);

		var regularWarehousePayload = SerializeUnencryptedPayload(warehousePackets[0]);
		using var regularWarehouseReader = new PacketBuffer(regularWarehousePayload);
		Assert.Equal(1, (int)regularWarehouseReader.ReadC());
		Assert.Equal(1, (int)regularWarehouseReader.ReadC());
		Assert.Equal(3, (int)regularWarehouseReader.ReadC());
		Assert.Equal(1, (int)regularWarehouseReader.ReadC());
		Assert.Equal(0, (int)regularWarehouseReader.ReadC());
		Assert.Equal(1, regularWarehouseReader.ReadH());
		Assert.Equal((88, 100, 0, 203, 1), ReadWarehouseItemHeader(regularWarehouseReader));
		Assert.Equal(0, regularWarehouseReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("01000300000000"),
			SerializeUnencryptedPayload(warehousePackets[1]));

		var accountWarehousePayload = SerializeUnencryptedPayload(warehousePackets[2]);
		using var accountWarehouseReader = new PacketBuffer(accountWarehousePayload);
		Assert.Equal(2, (int)accountWarehouseReader.ReadC());
		Assert.Equal(1, (int)accountWarehouseReader.ReadC());
		Assert.Equal(0, (int)accountWarehouseReader.ReadC());
		Assert.Equal(0, accountWarehouseReader.ReadH());
		Assert.Equal(2, accountWarehouseReader.ReadH());
		Assert.Equal((90, 100, 0, 203, 2), ReadWarehouseItemHeader(accountWarehouseReader));
		Assert.Equal((89, 182400001, 0, 34, 0), ReadWarehouseItemHeader(accountWarehouseReader));
		Assert.Equal(0, accountWarehouseReader.Remaining);

		Assert.Equal(
			Convert.FromHexString("02000000000000"),
			SerializeUnencryptedPayload(warehousePackets[3]));
		Assert.Equal(
			Convert.FromHexString("0000000005000000"),
			SerializeUnencryptedPayload(new SmChannelInfo(new WorldPosition(210010000, 1, 2, 3, 32), [new WorldMapSummary(210010000, false, 5)])));
		Assert.Equal(
			Convert.FromHexString("7B000000"),
			SerializeUnencryptedPayload(new SmGameTime(123)));

		var bindPointPayload = SerializeUnencryptedPayload(new SmBindPointInfo(210010000, 1.5f, 2.5f, 3.5f));
		using var bindPointReader = new PacketBuffer(bindPointPayload);
		Assert.Equal(0, (int)bindPointReader.ReadC());
		Assert.Equal(1, (int)bindPointReader.ReadC());
		Assert.Equal(210010000, bindPointReader.ReadD());
		Assert.Equal(1.5f, bindPointReader.ReadF());
		Assert.Equal(2.5f, bindPointReader.ReadF());
		Assert.Equal(3.5f, bindPointReader.ReadF());
		Assert.Equal(0, bindPointReader.ReadD());
		Assert.Equal(0, bindPointReader.Remaining);

		var spawnPayload = SerializeUnencryptedPayload(
			new SmPlayerSpawn(
				new Player
				{
					Position = new WorldPosition(210010000, 1.5f, 2.5f, 3.5f, 32),
				}));
		using var spawnReader = new PacketBuffer(spawnPayload);
		Assert.Equal(210010000, spawnReader.ReadD());
		Assert.Equal(210010000, spawnReader.ReadD());
		spawnReader.ReadD();
		Assert.Equal(0, (int)spawnReader.ReadC());
		Assert.Equal(1.5f, spawnReader.ReadF());
		Assert.Equal(2.5f, spawnReader.ReadF());
		Assert.Equal(3.5f, spawnReader.ReadF());
		Assert.Equal(32, (int)spawnReader.ReadC());

		var postmanTemplate = new NpcTemplateSummary(
			798100,
			"<zephyr deliveryman>",
			350579,
			15,
			"DISCIPLINED",
			"NORMAL",
			"BROWNIE",
			"GENERAL",
			"GENERAL",
			Height: 1.16875f,
			AttackSpeed: 2000,
			MaxHp: 2256,
			RunSpeed: 4.23f,
			BoundRadius: 0.595f);
		var postman = PostmanNpc.Create(
			new Player
			{
				ObjectId = 1001,
				Name = "Owner",
				Race = "ELYOS",
				Position = new WorldPosition(210010000, 1, 2, 3, 30),
			},
			9001,
			postmanTemplate);
		Assert.Equal(1, postman.Position.X, precision: 4);
		Assert.Equal(9, postman.Position.Y, precision: 4);

		var npcPayload = SerializeUnencryptedPayload(new SmNpcInfo(postman));
		using var npcReader = new PacketBuffer(npcPayload);
		Assert.Equal(postman.Position.X, npcReader.ReadF(), precision: 4);
		Assert.Equal(postman.Position.Y, npcReader.ReadF(), precision: 4);
		Assert.Equal(3, npcReader.ReadF());
		Assert.Equal(9001, npcReader.ReadD());
		Assert.Equal(798100, npcReader.ReadD());
		Assert.Equal(798100, npcReader.ReadD());
		Assert.Equal(38, (int)npcReader.ReadC());
		Assert.Equal(1, npcReader.ReadH());
		Assert.Equal(0, (int)npcReader.ReadC());
		Assert.Equal(350579, npcReader.ReadD());

		var deletePayload = SerializeUnencryptedPayload(new SmDelete(9001));
		using var deleteReader = new PacketBuffer(deletePayload);
		Assert.Equal(9001, deleteReader.ReadD());
		Assert.Equal(1, (int)deleteReader.ReadC());
	}

	[Fact]
	public void SmInventoryInfo_WritesItemStoneAndIdianDetailsInItemBlobs()
	{
		var inventoryPackets = SmInventoryInfo.CreateLoginPackets(
			new Player
			{
				InventoryItems =
				[
					new InventoryItem
					{
						ObjectId = 88,
						ItemId = 100,
						Count = 1,
						IsEquipped = true,
						IsSoulBound = true,
						Location = 0,
						Slot = 1,
						Enchant = 5,
						EnchantBonus = 2,
						OptionalSocket = 3,
						FusionedItem = 101,
						OptionalFusionSocket = 2,
						Tempering = 9,
						IsAmplified = true,
						BuffSkill = 55,
						ManaStones = [new ItemStoneSocket(167000001, 0), new ItemStoneSocket(167000002, 2)],
						FusionStones = [new ItemStoneSocket(167000003, 1)],
						Godstone = new PlayerGodstone(168000001, 7),
						IdianStone = new PlayerIdianStone(168000385, 4, 123456),
					},
				],
			},
			new ItemTemplateTable(
				[
					new ItemTemplateSummary(182400001, "Kinah", 0, 12350, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
					new ItemTemplateSummary(100, "Sword", 0, 1 << 17, 1, "SWORD", "NORMAL", "COMMON", "PC_ALL", 1, 0, 3),
				]),
			() => 77);

		var inventoryPayload = SerializeUnencryptedPayload(inventoryPackets[0]);
		using var inventoryReader = new PacketBuffer(inventoryPayload);
		inventoryReader.ReadC();
		inventoryReader.ReadC();
		inventoryReader.ReadC();
		inventoryReader.ReadC();
		Assert.Equal(2, inventoryReader.ReadH());
		ReadInventoryItemWithBlob(inventoryReader);
		var sword = ReadInventoryItemWithBlob(inventoryReader);
		Assert.Equal((88, 100, 1, 0), (sword.ObjectId, sword.ItemId, sword.EquipmentSlot, sword.IsCloth));

		using var blobReader = new PacketBuffer(sword.Blob);
		Assert.Equal(0x0e, (int)blobReader.ReadC());
		Assert.Equal(101, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(167000003, blobReader.ReadD());
		for (var i = 0; i < 4; i++)
			Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(2, (int)blobReader.ReadC());
		Assert.Equal(0, (int)blobReader.ReadC());

		Assert.Equal(0x06, (int)blobReader.ReadC());
		blobReader.ReadQ();
		Assert.Equal(0x01, (int)blobReader.ReadC());
		blobReader.ReadQ();
		blobReader.ReadQ();

		Assert.Equal(0x0b, (int)blobReader.ReadC());
		Assert.Equal(1, (int)blobReader.ReadC());
		Assert.Equal(5, (int)blobReader.ReadC());
		Assert.Equal(100, blobReader.ReadD());
		Assert.Equal(3, (int)blobReader.ReadC());
		Assert.Equal(2, (int)blobReader.ReadC());
		Assert.Equal(167000001, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(167000002, blobReader.ReadD());
		for (var i = 0; i < 3; i++)
			Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(168000001, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, (int)blobReader.ReadC());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(168000385, blobReader.ReadD());
		Assert.Equal(4, (int)blobReader.ReadC());
		Assert.Equal(9, (int)blobReader.ReadC());
		blobReader.ReadB(70);
		Assert.Equal(1, (int)blobReader.ReadC());
		Assert.Equal(55, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());
		Assert.Equal(0, blobReader.ReadD());

		Assert.Equal(0x11, (int)blobReader.ReadC());
		Assert.Equal(123456, blobReader.ReadD());
	}

	[Fact]
	public void SmInventoryInfo_WritesConditioningBlobForConditionableTemplateAtZeroCharge()
	{
		var inventoryPackets = SmInventoryInfo.CreateLoginPackets(
			new Player
			{
				InventoryItems =
				[
					new InventoryItem { ObjectId = 88, ItemId = 100, Count = 1, IsEquipped = true, Location = 0, Slot = 1 },
				],
			},
			new ItemTemplateTable(
				[
					new ItemTemplateSummary(182400001, "Kinah", 0, 12350, 1, "NONE", "NORMAL", "COMMON", "PC_ALL", 1, 0, 0),
					new ItemTemplateSummary(
						100,
						"Sword",
						0,
						1,
						1,
						"SWORD",
						"NORMAL",
						"COMMON",
						"PC_ALL",
						1,
						0,
						3,
						ConditioningMaxLevel: 1),
				]),
			() => 77);

		var inventoryPayload = SerializeUnencryptedPayload(inventoryPackets[0]);
		using var reader = new PacketBuffer(inventoryPayload);
		reader.ReadC();
		reader.ReadC();
		reader.ReadC();
		reader.ReadC();
		Assert.Equal(2, reader.ReadH());
		ReadInventoryItemWithBlob(reader);
		var sword = ReadInventoryItemWithBlob(reader);
		using var blobReader = new PacketBuffer(sword.Blob);

		Assert.Equal(0x06, (int)blobReader.ReadC());
		blobReader.ReadQ();
		Assert.Equal(0x01, (int)blobReader.ReadC());
		blobReader.ReadQ();
		blobReader.ReadQ();
		Assert.Equal(0x0b, (int)blobReader.ReadC());
		blobReader.ReadB(138);
		Assert.Equal(0x0f, (int)blobReader.ReadC());
		Assert.Equal(0, blobReader.ReadD());
	}

	[Fact]
	public void SmStatsInfo_WritesJavaShapedBaselineStats()
	{
		var payload = SerializeUnencryptedPayload(
			new SmStatsInfo(
				new Player
				{
					ObjectId = 1001,
					PlayerClass = "WARRIOR",
					Exp = 0,
					RecoverableExp = 7,
					Dp = 300,
					ReposeEnergy = 99,
					NpcExpands = 1,
					QuestExpands = 2,
					ItemExpands = 3,
					LifeStats = new PlayerLifeStats(111, 205, 55),
					InventoryItems =
					[
						new InventoryItem { ObjectId = 1, ItemId = 100, Location = 0 },
						new InventoryItem { ObjectId = 2, ItemId = 200, Location = 0, IsEquipped = true },
						new InventoryItem { ObjectId = 3, ItemId = 182400001, Location = 0 },
						new InventoryItem { ObjectId = 4, ItemId = 300, Location = 1 },
					],
				},
				new PlayerExperienceTable([0, 400]),
				gameMinutes: 321));

		using var reader = new PacketBuffer(payload);
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(321, reader.ReadD());
		AssertPrimaryStats(reader, 110, 110, 100, 100, 90, 90);
		AssertElementalResists(reader);
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(400, reader.ReadQ());
		Assert.Equal(7, reader.ReadQ());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(244, reader.ReadD());
		Assert.Equal(111, reader.ReadD());
		Assert.Equal(210, reader.ReadD());
		Assert.Equal(205, reader.ReadD());
		Assert.Equal(4000, reader.ReadH());
		Assert.Equal(300, reader.ReadH());
		Assert.Equal(60, reader.ReadD());
		Assert.Equal(55, reader.ReadD());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		AssertCombatStats(reader, blockEvasionParry: 74, physicalAccuracy: 198, magicalAccuracy: 14, strikeResist: 0, spellResist: 0);
		Assert.Equal(81, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadQ());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		AssertPrimaryStats(reader, 110, 110, 100, 100, 90, 90);
		AssertElementalResists(reader);
		Assert.Equal(244, reader.ReadD());
		Assert.Equal(210, reader.ReadD());
		Assert.Equal(4000, reader.ReadH());
		Assert.Equal(21592, reader.ReadH());
		Assert.Equal(60, reader.ReadD());
		AssertBaseCombatStats(reader, blockEvasionParry: 74, physicalAccuracy: 198, magicalAccuracy: 14, strikeResist: 0, spellResist: 0);
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmCharacterList_WritesJavaShapedCharacterSelectionInfo()
	{
		var payload = SerializeUnencryptedPayload(
			new SmCharacterList(
				1234,
				[
					new CharacterSelectionEntry
					{
						ObjectId = 1001,
						Name = "Character",
						GenderId = 1,
						RaceId = 0,
						ClassId = 3,
						Appearance = new CharacterAppearance
						{
							Voice = 7,
							SkinRgb = 0x112233,
							HairRgb = 0x445566,
							EyeRgb = 0x778899,
							LipRgb = 0xAABBCC,
							Face = 1,
							Hair = 2,
							Height = 1.25f,
						},
						TemplateId = 100001,
						MapId = 210010000,
						X = 1.5f,
						Y = 2.5f,
						Z = 3.5f,
						Heading = 90,
						Level = 12,
						TitleId = 5,
						LastOnlineEpochSeconds = 100,
						VisibleItems =
						[
							new VisibleCharacterItem(1, 110101001, 168000001, 0x123456),
						],
						DeletionTimeSeconds = 200,
						Display = 5,
						UnreadMailCount = 1,
						BrokerKinah = 300,
						BanInfo = new CharacterBanInfo(400, 500, "reason"),
					},
				]));

		using var reader = new PacketBuffer(payload);
		Assert.Equal(1234, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal("Character", ReadFixedS(reader, 25));
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(3, reader.ReadD());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal(0x112233, reader.ReadD());
		Assert.Equal(0x445566, reader.ReadD());
		Assert.Equal(0x778899, reader.ReadD());
		Assert.Equal(unchecked((int)0xAABBCC), reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(2, (int)reader.ReadC());
		reader.ReadB(50);
		Assert.Equal(1.25f, reader.ReadF());
		Assert.Equal(100001, reader.ReadD());
		Assert.Equal(210010000, reader.ReadD());
		Assert.Equal(1.5f, reader.ReadF());
		Assert.Equal(2.5f, reader.ReadF());
		Assert.Equal(3.5f, reader.ReadF());
		Assert.Equal(90, reader.ReadD());
		Assert.Equal(12, (int)reader.ReadH());
		reader.ReadH();
		Assert.Equal(5, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(string.Empty, ReadFixedS(reader, 40));
		reader.ReadH();
		Assert.Equal(100, reader.ReadD());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(110101001, reader.ReadD());
		Assert.Equal(168000001, reader.ReadD());
		Assert.Equal(Convert.FromHexString("01123456"), reader.ReadB(4));
		reader.ReadB(15 * 13 + 24 + 68);
		Assert.Equal(200, reader.ReadD());
		Assert.Equal(5, (int)reader.ReadH());
		reader.ReadH();
		reader.ReadD();
		Assert.Equal(1, reader.ReadD());
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(300, reader.ReadQ());
		reader.ReadB(20);
		Assert.Equal(400, reader.ReadD());
		Assert.Equal(500, reader.ReadD());
		Assert.Equal("reason", reader.ReadS());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmL2AuthLoginCheck_WritesMapSummariesAndAccountName()
	{
		var payload = SerializeUnencryptedPayload(
			new SmL2AuthLoginCheck(
				ok: true,
				"test",
				[
					new WorldMapSummary(210010000, IsInstance: false, TwinCount: 5),
					new WorldMapSummary(300030000, IsInstance: true, TwinCount: 9),
				]));

		Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(0, 4)));
		Assert.Contains(Convert.FromHexString("0200907F840C05003018E2110000"), payload);
		var accountName = Encoding.Unicode.GetBytes("test\0");
		Assert.True(payload.AsSpan(payload.Length - accountName.Length).SequenceEqual(accountName));
	}

	[Fact]
	public void SmQuitResponse_WritesJavaShapedPayload()
	{
		Assert.Equal(Convert.FromHexString("0100000000FFFFFFFF"), SerializeUnencryptedPayload(new SmQuitResponse()));
		Assert.Equal(Convert.FromHexString("0200000000FFFFFFFF"), SerializeUnencryptedPayload(new SmQuitResponse(editMode: true)));
	}

	[Fact]
	public void SmMove_WritesJavaShapedMovementPayload()
	{
		var player = new Player
		{
			ObjectId = 1234,
			Position = new WorldPosition(210010000, 1.25f, 2.5f, 3.75f, 9),
		};
		player.Movement.Mask = (byte)(MovementMask.Position | MovementMask.Manual);
		player.Movement.VectorX = 4.25f;
		player.Movement.VectorY = 5.5f;
		player.Movement.VectorZ = 6.75f;

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmMove(player)));

		Assert.Equal(1234, reader.ReadD());
		Assert.Equal(1.25f, reader.ReadF());
		Assert.Equal(2.5f, reader.ReadF());
		Assert.Equal(3.75f, reader.ReadF());
		Assert.Equal(9, (int)reader.ReadC());
		Assert.Equal(MovementMask.Position | MovementMask.Manual, (int)reader.ReadC());
		Assert.Equal(4.25f, reader.ReadF());
		Assert.Equal(5.5f, reader.ReadF());
		Assert.Equal(6.75f, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmMove_WritesAbsoluteGlideAndVehicleTails()
	{
		var player = new Player
		{
			ObjectId = 4321,
			Position = new WorldPosition(210010000, 10, 20, 30, 40),
		};
		player.Movement.Mask = (byte)(MovementMask.Position | MovementMask.Manual | MovementMask.Absolute | MovementMask.Glide | MovementMask.Vehicle);
		player.Movement.TargetX = 11;
		player.Movement.TargetY = 22;
		player.Movement.TargetZ = 33;
		player.Movement.GlideFlag = GlideFlag.Geyser;
		player.Movement.GeyserLocationId = 7;
		player.Movement.VehicleUnk1 = 101;
		player.Movement.VehicleUnk2 = 202;
		player.Movement.VectorX = 1;
		player.Movement.VectorY = 2;
		player.Movement.VectorZ = 3;

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmMove(player)));

		Assert.Equal(4321, reader.ReadD());
		Assert.Equal(10, reader.ReadF());
		Assert.Equal(20, reader.ReadF());
		Assert.Equal(30, reader.ReadF());
		Assert.Equal(40, (int)reader.ReadC());
		Assert.Equal(MovementMask.Position | MovementMask.Manual | MovementMask.Absolute | MovementMask.Glide | MovementMask.Vehicle, (int)reader.ReadC());
		Assert.Equal(11, reader.ReadF());
		Assert.Equal(22, reader.ReadF());
		Assert.Equal(33, reader.ReadF());
		Assert.Equal(GlideFlag.Geyser, reader.ReadC());
		Assert.Equal(7, (int)reader.ReadC());
		Assert.Equal(101, reader.ReadD());
		Assert.Equal(202, reader.ReadD());
		Assert.Equal(1, reader.ReadF());
		Assert.Equal(2, reader.ReadF());
		Assert.Equal(3, reader.ReadF());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmPlayerInfo_WritesJavaShapedBaseline()
	{
		var player = new Player
		{
			ObjectId = 1001,
			Name = "Visible",
			Race = "ELYOS",
			Gender = "FEMALE",
			PlayerClass = "RANGER",
			TitleId = 12,
			Dp = 300,
			Position = new WorldPosition(210010000, 10, 20, 30, 40),
			Appearance = new CharacterAppearance
			{
				SkinRgb = 0x112233,
				HairRgb = 0x445566,
				EyeRgb = 0x778899,
				LipRgb = 0xAABBCC,
				Face = 1,
				Hair = 2,
				Voice = 7,
				Height = 1.2f,
			},
			InventoryItems =
			[
				new InventoryItem { ObjectId = 2001, ItemId = 100000001, ItemSkin = 100000099, IsEquipped = true, Location = 0, Slot = 1, Enchant = 5 },
			],
		};

		using var reader = new PacketBuffer(SerializeUnencryptedPayload(new SmPlayerInfo(player)));

		Assert.Equal(10, reader.ReadF());
		Assert.Equal(20, reader.ReadF());
		Assert.Equal(30, reader.ReadF());
		Assert.Equal(1001, reader.ReadD());
		Assert.Equal(100001, reader.ReadD());
		reader.ReadD();
		Assert.Equal(100001, reader.ReadD());
		reader.ReadC();
		reader.ReadD();
		Assert.Equal(0x26, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(5, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		reader.ReadH();
		reader.ReadD();
		reader.ReadD();
		Assert.Equal(40, (int)reader.ReadC());
		Assert.Equal("Visible", reader.ReadS());
		Assert.Equal(12, reader.ReadH());
	}

	[Fact]
	public void ClientPacketFactory_ParsesL2AuthLoginCheck()
	{
		var payload = CreateClientPayload(
			149,
			buffer =>
			{
				buffer.WriteD(11);
				buffer.WriteD(22);
				buffer.WriteD(33);
				buffer.WriteD(44);
				buffer.WriteD(55);
				buffer.WriteD(66);
			});

		var packet = Assert.IsType<CmL2AuthLoginCheck>(GameClientPacketFactory.TryCreatePacket(payload, GameConnectionState.Connected));

		Assert.Equal(149, packet.OpCode);
		Assert.Equal(11, packet.PlayOk2);
		Assert.Equal(22, packet.PlayOk1);
		Assert.Equal(33, packet.AccountId);
		Assert.Equal(44, packet.LoginOk);
		Assert.Equal(55, packet.Unknown1);
		Assert.Equal(66, packet.Unknown2);
	}

	[Fact]
	public void ClientPacketFactory_ParsesMacAddress()
	{
		var payload = CreateClientPayload(
			189,
			buffer =>
			{
				buffer.WriteC(7);
				buffer.WriteH(1);
				buffer.WriteD(0x0100007F);
				buffer.WriteS("AA-BB-CC-DD-EE-FF");
				buffer.WriteS("disk-1");
				buffer.WriteD(0x0200007F);
			});

		var packet = Assert.IsType<CmMacAddress>(GameClientPacketFactory.TryCreatePacket(payload, GameConnectionState.Connected));

		Assert.Equal(7, (int)packet.Unknown);
		Assert.Equal([0x0100007F], packet.RouteIps);
		Assert.Equal("AA-BB-CC-DD-EE-FF", packet.MacAddress);
		Assert.Equal("disk-1", packet.HddSerial);
		Assert.Equal(0x0200007F, packet.LocalIp);
	}

	[Fact]
	public void ClientPacketFactory_ParsesPingAndTimeCheckPackets()
	{
		var timeCheck = Assert.IsType<CmTimeCheck>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(18, buffer => buffer.WriteD(123456)),
				GameConnectionState.Connected));
		var pingAuthed = Assert.IsType<CmPing>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(44, buffer => buffer.WriteH(7)),
				GameConnectionState.Authed));

		Assert.Equal(123456, timeCheck.NanoTime);
		Assert.Equal(7, pingAuthed.Unknown);
		Assert.IsType<CmTimeCheck>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(18, buffer => buffer.WriteD(234567)),
				GameConnectionState.InGame));
		Assert.IsType<CmPing>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(44, buffer => buffer.WriteH(8)),
				GameConnectionState.InGame));
		Assert.IsType<CmPingRequest>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(103, _ => { }),
				GameConnectionState.InGame));
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(44, buffer => buffer.WriteH(1)),
				GameConnectionState.Connected));
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(103, _ => { }),
				GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesPublicChatPacket()
	{
		var chatMessage = Assert.IsType<CmChatMessagePublic>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(27, buffer =>
				{
					buffer.WriteC(3);
					buffer.WriteS("Anyone there?");
				}),
				GameConnectionState.InGame));

		Assert.Equal(3, (int)chatMessage.ChatType);
		Assert.Equal("Anyone there?", chatMessage.Message);
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(27, buffer =>
				{
					buffer.WriteC(0);
					buffer.WriteS("too soon");
				}),
				GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesWhisperChatPacket()
	{
		var whisper = Assert.IsType<CmChatMessageWhisper>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(28, buffer =>
				{
					buffer.WriteS("kahrun");
					buffer.WriteS("Need aid?");
				}),
				GameConnectionState.InGame));

		Assert.Equal("kahrun", whisper.RecipientName);
		Assert.Equal("Need aid?", whisper.Message);
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(28, buffer =>
				{
					buffer.WriteS("kahrun");
					buffer.WriteS("too soon");
				}),
				GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesChatInfoPackets()
	{
		var playerInfo = Assert.IsType<CmChatPlayerInfo>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(39, buffer => buffer.WriteS("yustiel")),
				GameConnectionState.InGame));
		var groupInfo = Assert.IsType<CmChatGroupInfo>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(61, buffer =>
				{
					buffer.WriteS("marchutan");
					buffer.WriteD(7);
				}),
				GameConnectionState.InGame));

		Assert.Equal("yustiel", playerInfo.PlayerName);
		Assert.Equal("marchutan", groupInfo.PlayerName);
		Assert.Equal(7, groupInfo.Unknown);
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(39, buffer => buffer.WriteS("yustiel")),
				GameConnectionState.Authed));
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(61, buffer =>
				{
					buffer.WriteS("marchutan");
					buffer.WriteD(7);
				}),
				GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesChatAuthPacket()
	{
		var chatAuth = Assert.IsType<CmChatAuth>(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(174, buffer =>
				{
					buffer.WriteD(7001);
					buffer.WriteB([0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF]);
				}),
				GameConnectionState.InGame));

		Assert.Equal(7001, chatAuth.ObjectId);
		Assert.Equal([0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF], chatAuth.MacAddress);
		Assert.Null(
			GameClientPacketFactory.TryCreatePacket(
				CreateClientPayload(174, buffer =>
				{
					buffer.WriteD(7001);
					buffer.WriteB([0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF]);
				}),
				GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesTargetSelect()
	{
		var targetSelect = Assert.IsType<CmTargetSelect>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(31, b =>
			{
				b.WriteD(7001);
				b.WriteC(1);
			}), GameConnectionState.InGame));

		Assert.Equal(7001, targetSelect.TargetObjectId);
		Assert.True(targetSelect.SelectTargetOfTarget);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(31, b => b.WriteD(7001)), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesMovementPackets()
	{
		var move = Assert.IsType<CmMove>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(48, b =>
			{
				b.WriteF(10);
				b.WriteF(20);
				b.WriteF(30);
				b.WriteC(4);
				b.WriteC(MovementMask.Position | MovementMask.Manual | MovementMask.Glide | MovementMask.Vehicle);
				b.WriteF(1);
				b.WriteF(2);
				b.WriteF(3);
				b.WriteC(GlideFlag.Geyser);
				b.WriteC(9);
				b.WriteD(100);
				b.WriteD(200);
				b.WriteF(11);
				b.WriteF(22);
				b.WriteF(33);
			}), GameConnectionState.InGame));
		var absoluteMove = Assert.IsType<CmMove>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(48, b =>
			{
				b.WriteF(5);
				b.WriteF(6);
				b.WriteF(7);
				b.WriteC(8);
				b.WriteC(MovementMask.Position | MovementMask.Manual | MovementMask.Absolute);
				b.WriteF(55);
				b.WriteF(66);
				b.WriteF(77);
			}), GameConnectionState.InGame));
		var moveInAir = Assert.IsType<CmMoveInAir>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(49, b =>
			{
				b.WriteD(210010000);
				b.WriteF(15);
				b.WriteF(25);
				b.WriteF(35);
				b.WriteC(16);
				b.WriteD(900);
			}), GameConnectionState.InGame));

		Assert.Equal(10, move.X);
		Assert.Equal(20, move.Y);
		Assert.Equal(30, move.Z);
		Assert.Equal(4, (int)move.Heading);
		Assert.Equal(MovementMask.Position | MovementMask.Manual | MovementMask.Glide | MovementMask.Vehicle, (int)move.Type);
		Assert.Equal(1, move.VectorX);
		Assert.Equal(2, move.VectorY);
		Assert.Equal(3, move.VectorZ);
		Assert.Equal(11, move.TargetX);
		Assert.Equal(22, move.TargetY);
		Assert.Equal(33, move.TargetZ);
		Assert.Equal(GlideFlag.Geyser, move.GlideFlag);
		Assert.Equal(9, move.GeyserLocationId);
		Assert.Equal(100, move.VehicleUnk1);
		Assert.Equal(200, move.VehicleUnk2);
		Assert.Equal(11, move.VehicleX);
		Assert.Equal(22, move.VehicleY);
		Assert.Equal(33, move.VehicleZ);
		Assert.Equal(55, absoluteMove.TargetX);
		Assert.Equal(66, absoluteMove.TargetY);
		Assert.Equal(77, absoluteMove.TargetZ);
		Assert.Equal(210010000, moveInAir.WorldId);
		Assert.Equal(15, moveInAir.X);
		Assert.Equal(25, moveInAir.Y);
		Assert.Equal(35, moveInAir.Z);
		Assert.Equal(16, (int)moveInAir.Heading);
		Assert.Equal(900, moveInAir.Distance);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(48, b => b.WriteF(1)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(49, b => b.WriteD(1)), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesCharacterSelectionPackets()
	{
		var quit = Assert.IsType<CmQuit>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(3, b => b.WriteC(1)), GameConnectionState.Authed));
		var characterList = Assert.IsType<CmCharacterList>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(150, b => b.WriteD(1234)), GameConnectionState.Authed));
		var createCharacter = Assert.IsType<CmCreateCharacter>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(151, WriteCreateCharacterPayload), GameConnectionState.Authed));
		var enterWorld = Assert.IsType<CmEnterWorld>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(8, b => b.WriteD(5678)), GameConnectionState.Authed));
		var deleteCharacter = Assert.IsType<CmDeleteCharacter>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(152, b =>
			{
				b.WriteD(1234);
				b.WriteD(5678);
			}), GameConnectionState.Authed));
		var restoreCharacter = Assert.IsType<CmRestoreCharacter>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(153, b =>
			{
				b.WriteD(2233);
				b.WriteD(6677);
			}), GameConnectionState.Authed));
		var passkey = Assert.IsType<CmCharacterPasskey>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(210, b =>
			{
				b.WriteH(2);
				WriteFixedUtf16Bytes(b, "old-pass");
				WriteFixedUtf16Bytes(b, "new-pass");
			}), GameConnectionState.Authed));

		Assert.Equal(1234, characterList.PlayOk2);
		Assert.True(quit.StayConnected);
		Assert.Equal(99, createCharacter.AccountId);
		Assert.Equal("account-name", createCharacter.AccountName);
		Assert.Equal("Character", createCharacter.CharacterName);
		Assert.Equal(1, createCharacter.GenderId);
		Assert.Equal(0, createCharacter.RaceId);
		Assert.Equal(3, createCharacter.ClassId);
		Assert.Equal(7, createCharacter.Appearance.Voice);
		Assert.Equal(0x112233, createCharacter.Appearance.SkinRgb);
		Assert.Equal(2, createCharacter.Appearance.Hair);
		Assert.Equal(1.25f, createCharacter.Appearance.Height);
		Assert.Equal(1, createCharacter.Type);
		Assert.Equal(5678, enterWorld.ObjectId);
		Assert.Equal(1234, deleteCharacter.PlayOk2);
		Assert.Equal(5678, deleteCharacter.CharacterObjectId);
		Assert.Equal(2233, restoreCharacter.PlayOk2);
		Assert.Equal(6677, restoreCharacter.CharacterObjectId);
		Assert.Equal(2, passkey.Type);
		Assert.Equal("old-pass", passkey.Passkey);
		Assert.Equal("new-pass", passkey.NewPasskey);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(4, _ => { }), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesMayQuitInGame()
	{
		Assert.IsType<CmMayQuit>(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(4, _ => { }), GameConnectionState.InGame));
	}

	[Fact]
	public void ClientPacketFactory_ParsesLevelReadyInGame()
	{
		Assert.IsType<CmLevelReady>(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(9, _ => { }), GameConnectionState.InGame));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(9, _ => { }), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesMotionPacket()
	{
		var motion = Assert.IsType<CmMotion>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(71, b =>
			{
				b.WriteC(4);
				b.WriteH(11);
				b.WriteC(1);
			}), GameConnectionState.InGame));

		Assert.Equal(4, motion.Unknown);
		Assert.Equal(11, motion.MotionId);
		Assert.Equal(1, motion.MotionType);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(71, _ => { }), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesTitlePackets()
	{
		var titleSet = Assert.IsType<CmTitleSet>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(139, b => b.WriteH(77)), GameConnectionState.InGame));
		var bonusTitle = Assert.IsType<CmBonusTitle>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(233, b => b.WriteH(5)), GameConnectionState.InGame));

		Assert.Equal(77, titleSet.TitleId);
		Assert.Equal(5, bonusTitle.BonusTitleId);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(139, b => b.WriteH(77)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(233, b => b.WriteH(5)), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesSocialListPackets()
	{
		var questionResponse = Assert.IsType<CmQuestionResponse>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(50, buffer =>
			{
				buffer.WriteD(SmQuestionWindow.BuddyListAddBuddyRequest);
				buffer.WriteC(1);
				buffer.WriteC(0);
				buffer.WriteH(0);
				buffer.WriteD(1001);
				buffer.WriteD(0);
				buffer.WriteH(0);
			}), GameConnectionState.InGame));
		Assert.IsType<CmMarkFriendList>(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(110, _ => { }), GameConnectionState.InGame));
		var friendAdd = Assert.IsType<CmFriendAdd>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(111, buffer =>
			{
				buffer.WriteS("Friend");
				buffer.WriteS("hello");
			}), GameConnectionState.InGame));
		var friendDelete = Assert.IsType<CmFriendDelete>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(112, buffer => buffer.WriteS("Friend")), GameConnectionState.InGame));
		Assert.IsType<CmShowBlockList>(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(158, _ => { }), GameConnectionState.InGame));
		var blockAdd = Assert.IsType<CmBlockAdd>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(166, buffer =>
			{
				buffer.WriteS("Blocked");
				buffer.WriteS("reason");
			}), GameConnectionState.InGame));
		var blockDelete = Assert.IsType<CmBlockDelete>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(167, buffer => buffer.WriteS("Blocked")), GameConnectionState.InGame));
		Assert.IsType<CmShowFriendList>(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(230, _ => { }), GameConnectionState.InGame));
		var friendStatus = Assert.IsType<CmFriendStatus>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(170, buffer => buffer.WriteC(3)), GameConnectionState.InGame));
		var blockSetReason = Assert.IsType<CmBlockSetReason>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(179, buffer =>
			{
				buffer.WriteS("Blocked");
				buffer.WriteS("new reason");
			}), GameConnectionState.InGame));
		var friendSetMemo = Assert.IsType<CmFriendSetMemo>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(239, buffer =>
			{
				buffer.WriteS("Friend");
				buffer.WriteS("new memo");
			}), GameConnectionState.InGame));

		Assert.Equal(SmQuestionWindow.BuddyListAddBuddyRequest, questionResponse.QuestionId);
		Assert.Equal(1, (int)questionResponse.Response);
		Assert.Equal(1001, questionResponse.SenderObjectId);
		Assert.Equal("Friend", friendAdd.TargetName);
		Assert.Equal("hello", friendAdd.Message);
		Assert.Equal("Friend", friendDelete.TargetName);
		Assert.Equal("Blocked", blockAdd.TargetName);
		Assert.Equal("reason", blockAdd.Reason);
		Assert.Equal("Blocked", blockDelete.TargetName);
		Assert.Equal(3, (int)friendStatus.Status);
		Assert.Equal("Blocked", blockSetReason.TargetName);
		Assert.Equal("new reason", blockSetReason.Reason);
		Assert.Equal("Friend", friendSetMemo.TargetName);
		Assert.Equal("new memo", friendSetMemo.Memo);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(50, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(110, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(111, buffer => buffer.WriteS("Friend")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(112, buffer => buffer.WriteS("Friend")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(158, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(166, buffer => buffer.WriteS("Blocked")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(167, buffer => buffer.WriteS("Blocked")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(230, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(170, buffer => buffer.WriteC(1)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(179, buffer => buffer.WriteS("Blocked")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(239, buffer => buffer.WriteS("Friend")), GameConnectionState.Authed));
	}

	[Fact]
	public void SmHouseBids_WritesJavaAuctionRows()
	{
		var bidPage = new HouseAuctionBidPage(
			[
				new HouseAuctionBidSummary(
					ListIndex: 7,
					HouseObjectId: 9001,
					LandId: 326001,
					AddressId: 6001,
					BuildingId: 353000,
					HouseTypeId: 1,
					HighestBidKinah: 12_000_000,
					BidCount: 2,
					RemainingAuctionSeconds: 3600),
			],
			LastBidListIndex: 7,
			LastBidKinah: 12_000_000,
			RegisteredHouseListIndex: 8,
			RegisteredHouseStartingPrice: 9_000_000);

		var payload = SerializeUnencryptedPayload(SmHouseBids.CreatePackets(bidPage)[0]);
		using var reader = new PacketBuffer(payload);

		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(1, (int)reader.ReadC());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal(12_000_000, reader.ReadQ());
		Assert.Equal(8, reader.ReadD());
		Assert.Equal(9_000_000, reader.ReadQ());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(7, reader.ReadD());
		Assert.Equal(326001, reader.ReadD());
		Assert.Equal(6001, reader.ReadD());
		Assert.Equal(353000, reader.ReadD());
		Assert.Equal(1, reader.ReadD());
		Assert.Equal(12_000_000, reader.ReadQ());
		Assert.Equal(100000, reader.ReadQ());
		Assert.Equal(2, reader.ReadD());
		Assert.Equal(3600, reader.ReadD());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void ClientPacketFactory_ParsesHousingAuctionPackets()
	{
		var houseSettings = Assert.IsType<CmHouseSettings>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(73, b =>
			{
				b.WriteC(PlayerHouse.DoorClosedExceptFriends);
				b.WriteC(0);
				b.WriteS("Visitors welcome");
			}), GameConnectionState.InGame));
		Assert.IsType<CmGetHouseBids>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(218, _ => { }), GameConnectionState.InGame));
		var registerHouse = Assert.IsType<CmRegisterHouse>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(219, b =>
			{
				b.WriteQ(500000);
				b.WriteQ(100000);
			}), GameConnectionState.InGame));
		var placeBid = Assert.IsType<CmPlaceBid>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(221, b =>
			{
				b.WriteD(12);
				b.WriteQ(750000);
			}), GameConnectionState.InGame));
		var payRent = Assert.IsType<CmHousePayRent>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(223, b => b.WriteC(2)), GameConnectionState.InGame));

		Assert.Equal(PlayerHouse.DoorClosedExceptFriends, houseSettings.DoorState);
		Assert.False(houseSettings.ShowOwnerName);
		Assert.Equal("Visitors welcome", houseSettings.SignNotice);
		Assert.Equal(500000, registerHouse.BidKinah);
		Assert.Equal(100000, registerHouse.ClientFixedValue);
		Assert.Equal(12, placeBid.ListIndex);
		Assert.Equal(750000, placeBid.BidOffer);
		Assert.Equal(2, payRent.WeekCount);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(73, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(218, _ => { }), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(223, b => b.WriteC(1)), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_ParsesBrokerPackets()
	{
		var sellWindow = Assert.IsType<CmBrokerSellWindow>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(117, b => b.WriteD(90)), GameConnectionState.InGame));
		var brokerList = Assert.IsType<CmBrokerList>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(123, b =>
			{
				b.WriteD(7001);
				b.WriteC(4);
				b.WriteH(2);
				b.WriteH(32);
			}), GameConnectionState.InGame));
		var brokerSearch = Assert.IsType<CmBrokerSearch>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(124, b =>
			{
				b.WriteD(7002);
				b.WriteC(6);
				b.WriteH(3);
				b.WriteH(64);
				b.WriteH(2);
				b.WriteD(1001);
				b.WriteD(1002);
			}), GameConnectionState.InGame));
		var registered = Assert.IsType<CmBrokerRegistered>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(125, b => b.WriteD(7003)), GameConnectionState.InGame));
		var buy = Assert.IsType<CmBuyBrokerItem>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(126, b =>
			{
				b.WriteD(7004);
				b.WriteD(8004);
				b.WriteQ(9);
			}), GameConnectionState.InGame));
		var register = Assert.IsType<CmRegisterBrokerItem>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(127, b =>
			{
				b.WriteD(7005);
				b.WriteD(8005);
				b.WriteQ(123456);
				b.WriteQ(3);
				b.WriteC(1);
			}), GameConnectionState.InGame));
		var cancel = Assert.IsType<CmBrokerCancelRegistered>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(128, b =>
			{
				b.WriteD(7006);
				b.WriteD(8006);
			}), GameConnectionState.InGame));
		var settleList = Assert.IsType<CmBrokerSettleList>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(129, b =>
			{
				b.WriteD(7007);
				b.WriteH(5);
			}), GameConnectionState.InGame));
		var settleAccount = Assert.IsType<CmBrokerSettleAccount>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(130, b => b.WriteD(7008)), GameConnectionState.InGame));

		Assert.Equal(90, sellWindow.ItemObjectId);
		Assert.Equal(7001, brokerList.BrokerObjectId);
		Assert.Equal(4, brokerList.SortType);
		Assert.Equal(2, brokerList.Page);
		Assert.Equal(32, brokerList.ListMask);
		Assert.Equal(7002, brokerSearch.BrokerObjectId);
		Assert.Equal(6, brokerSearch.SortType);
		Assert.Equal(3, brokerSearch.Page);
		Assert.Equal(64, brokerSearch.Mask);
		Assert.Equal([1001, 1002], brokerSearch.ItemIds);
		Assert.Equal(7003, registered.BrokerObjectId);
		Assert.Equal(7004, buy.BrokerObjectId);
		Assert.Equal(8004, buy.BrokerItemObjectId);
		Assert.Equal(9, buy.ItemCount);
		Assert.Equal(7005, register.BrokerObjectId);
		Assert.Equal(8005, register.ItemObjectId);
		Assert.Equal(123456, register.Price);
		Assert.Equal(3, register.ItemCount);
		Assert.True(register.SplittingAvailable);
		Assert.Equal(7006, cancel.BrokerObjectId);
		Assert.Equal(8006, cancel.BrokerItemObjectId);
		Assert.Equal(7007, settleList.BrokerObjectId);
		Assert.Equal(5, settleList.StartPageIndex);
		Assert.Equal(7008, settleAccount.BrokerObjectId);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(123, b => b.WriteD(1)), GameConnectionState.Authed));
	}

	[Fact]
	public void BrokerItemMaskMatcher_MatchesJavaNumericMasks()
	{
		var sword = CreateBrokerTemplate(100000001);
		var gatheredMaterial = CreateBrokerTemplate(152001234);
		var enchantStone = CreateBrokerTemplate(166001234);

		Assert.True(BrokerItemMaskMatcher.Matches(9010, sword));
		Assert.True(BrokerItemMaskMatcher.Matches(1000, sword));
		Assert.False(BrokerItemMaskMatcher.Matches(1001, sword));
		Assert.True(BrokerItemMaskMatcher.Matches(6030, gatheredMaterial));
		Assert.False(BrokerItemMaskMatcher.Matches(6031, gatheredMaterial));
		Assert.True(BrokerItemMaskMatcher.Matches(1660, enchantStone));
		Assert.True(BrokerItemMaskMatcher.Matches(8060, enchantStone));
	}

	[Fact]
	public void BrokerItemMaskMatcher_MatchesJavaClassAndRecipeMasks()
	{
		var rangerStigma = CreateBrokerTemplate(140000001, classRestrictions: new HashSet<string>(StringComparer.Ordinal) { "RANGER" });
		var priestSkillManual = CreateBrokerTemplate(169500001, classRestrictions: new HashSet<string>(StringComparer.Ordinal) { "PRIEST" });
		var weaponRecipe = CreateBrokerTemplate(152200001, craftLearnRecipeId: 155000001);
		var recipes = new RecipeTemplateTable(
		[
			new RecipeTemplateSummary(155000001, 0, 40002, "PC_ALL", 0, 0, 0, 100000001, 1),
		]);

		Assert.True(BrokerItemMaskMatcher.Matches(6013, rangerStigma));
		Assert.False(BrokerItemMaskMatcher.Matches(6012, rangerStigma));
		Assert.True(BrokerItemMaskMatcher.Matches(6026, priestSkillManual));
		Assert.False(BrokerItemMaskMatcher.Matches(6020, priestSkillManual));
		Assert.True(BrokerItemMaskMatcher.Matches(6040, weaponRecipe, recipes));
		Assert.False(BrokerItemMaskMatcher.Matches(6041, weaponRecipe, recipes));
		Assert.False(BrokerItemMaskMatcher.Matches(6040, weaponRecipe));
	}

	[Fact]
	public void InventoryCapacity_MatchesJavaCubeLimitAndIgnoresKinahAndEquippedRows()
	{
		var player = new Player
		{
			NpcExpands = 1,
			QuestExpands = 2,
			ItemExpands = 3,
			InventoryItems = Enumerable.Range(1, 80)
				.Select(id => new InventoryItem { ObjectId = id, ItemId = 100000000 + id, Location = 0 })
				.Concat(
				[
					new InventoryItem { ObjectId = 1000, ItemId = 182400001, Location = 0 },
					new InventoryItem { ObjectId = 1001, ItemId = 100000099, Location = 0, IsEquipped = true },
				])
				.ToArray(),
		};

		Assert.Equal(81, InventoryCapacity.GetCubeLimit(player));
		Assert.Equal(80, InventoryCapacity.GetUsedCubeSlots(player));
		Assert.True(InventoryCapacity.HasFreeCubeSlot(player));

		player.InventoryItems = player.InventoryItems
			.Concat([new InventoryItem { ObjectId = 1002, ItemId = 100000100, Location = 0 }])
			.ToArray();
		Assert.False(InventoryCapacity.HasFreeCubeSlot(player));
	}

	[Fact]
	public void WorldVisibility_MatchesKnownListDefaultRange()
	{
		var player = new Player
		{
			Position = new WorldPosition(210010000, 0, 0, 0, 0),
		};

		Assert.True(WorldVisibility.IsVisibleTo(player, new WorldPosition(210010000, 95, 0, 0, 0)));
		Assert.True(WorldVisibility.IsVisibleTo(player, new WorldPosition(210010000, 30, 40, 80, 0)));
		Assert.False(WorldVisibility.IsVisibleTo(player, new WorldPosition(210010000, 96, 0, 0, 0)));
		Assert.False(WorldVisibility.IsVisibleTo(player, new WorldPosition(220010000, 0, 0, 0, 0)));
	}

	[Fact]
	public void ClientPacketFactory_ParsesMailPackets()
	{
		var sendMail = Assert.IsType<CmSendMail>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(132, b =>
			{
				b.WriteS("Recipient");
				b.WriteS("Title");
				b.WriteS("Message");
				b.WriteD(90);
				b.WriteQ(2);
				b.WriteQ(500);
				b.WriteC(1);
			}), GameConnectionState.InGame));
		var checkMail = Assert.IsType<CmCheckMailList>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(133, b => b.WriteC(1)), GameConnectionState.InGame));
		var readMail = Assert.IsType<CmReadMail>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(134, b => b.WriteD(1234)), GameConnectionState.InGame));
		var getAttachment = Assert.IsType<CmGetMailAttachment>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(136, b =>
			{
				b.WriteD(1234);
				b.WriteC(1);
			}), GameConnectionState.InGame));
		var deleteMail = Assert.IsType<CmDeleteMail>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(137, b =>
			{
				b.WriteH(2);
				b.WriteD(1234);
				b.WriteC(0);
				b.WriteD(5678);
				b.WriteC(1);
			}), GameConnectionState.InGame));
		var readExpressMail = Assert.IsType<CmReadExpressMail>(
			GameClientPacketFactory.TryCreatePacket(CreateClientPayload(162, b => b.WriteC(1)), GameConnectionState.InGame));

		Assert.Equal("Recipient", sendMail.RecipientName);
		Assert.Equal("Title", sendMail.Title);
		Assert.Equal("Message", sendMail.Message);
		Assert.Equal(90, sendMail.ItemObjectId);
		Assert.Equal(2, sendMail.ItemCount);
		Assert.Equal(500, sendMail.KinahCount);
		Assert.Equal(1, sendMail.LetterTypeId);
		Assert.True(checkMail.ExpressOnly);
		Assert.Equal(1234, readMail.MailObjectId);
		Assert.Equal(1234, getAttachment.MailObjectId);
		Assert.Equal(1, (int)getAttachment.AttachmentType);
		Assert.Equal([1234, 5678], deleteMail.MailObjectIds);
		Assert.Equal(1, readExpressMail.Action);
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(132, b => b.WriteS("Recipient")), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(133, b => b.WriteC(0)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(134, b => b.WriteD(1234)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(136, b => b.WriteD(1234)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(137, b => b.WriteH(0)), GameConnectionState.Authed));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(CreateClientPayload(162, b => b.WriteC(1)), GameConnectionState.Authed));
	}

	[Fact]
	public void ClientPacketFactory_RejectsInvalidStateAndBadHeader()
	{
		var characterListPayload = CreateClientPayload(150, b => b.WriteD(1234));
		var badHeaderPayload = (byte[])characterListPayload.Clone();
		badHeaderPayload[2] = 0x44;

		Assert.Null(GameClientPacketFactory.TryCreatePacket(characterListPayload, GameConnectionState.Connected));
		Assert.Null(GameClientPacketFactory.TryCreatePacket(badHeaderPayload, GameConnectionState.Authed));
	}

	private static byte[] CreateClientPayload(int opcode, Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		var encodedOpcode = EncodeClientPacketOpcode(opcode);
		buffer.WriteH(encodedOpcode);
		buffer.WriteC(0x65);
		buffer.WriteH(~encodedOpcode);
		writePayload(buffer);
		return buffer.ToArray();
	}

	private static int EncodeClientPacketOpcode(int opcode)
	{
		return ((((opcode + 207) ^ 0xEF) + 0x0C) ^ 0xEF) & 0xffff;
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static void AssertSystemMessage(GameServerPacket packet, int expectedMessageId, params string[] expectedParameters)
	{
		var payload = SerializeUnencryptedPayload(packet);
		using var reader = new PacketBuffer(payload);
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

	private static (int ObjectId, int ItemId, int BlobSize, int EquipmentSlot, int IsCloth) ReadInventoryItemHeader(PacketBuffer reader)
	{
		var objectId = reader.ReadD();
		var itemId = reader.ReadD();
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		var equipmentSlot = reader.ReadH();
		var isCloth = reader.ReadC();
		return (objectId, itemId, blobSize, equipmentSlot, isCloth);
	}

	private static (int ObjectId, int ItemId, byte[] Blob, int EquipmentSlot, int IsCloth) ReadInventoryItemWithBlob(PacketBuffer reader)
	{
		var objectId = reader.ReadD();
		var itemId = reader.ReadD();
		reader.ReadS();
		var blobSize = reader.ReadH();
		var blob = reader.ReadB(blobSize);
		var equipmentSlot = reader.ReadH();
		var isCloth = reader.ReadC();
		return (objectId, itemId, blob, equipmentSlot, isCloth);
	}

	private static (int ObjectId, int ItemId, int ItemInfo, int BlobSize, int EquipmentSlot) ReadWarehouseItemHeader(PacketBuffer reader)
	{
		var objectId = reader.ReadD();
		var itemId = reader.ReadD();
		var itemInfo = reader.ReadC();
		reader.ReadS();
		var blobSize = reader.ReadH();
		reader.ReadB(blobSize);
		var equipmentSlot = reader.ReadH();
		return (objectId, itemId, itemInfo, blobSize, equipmentSlot);
	}

	private static void AssertPrimaryStats(PacketBuffer reader, int power, int health, int accuracy, int agility, int knowledge, int will)
	{
		Assert.Equal(power, reader.ReadH());
		Assert.Equal(health, reader.ReadH());
		Assert.Equal(accuracy, reader.ReadH());
		Assert.Equal(agility, reader.ReadH());
		Assert.Equal(knowledge, reader.ReadH());
		Assert.Equal(will, reader.ReadH());
	}

	private static void AssertElementalResists(PacketBuffer reader)
	{
		for (var i = 0; i < 6; i++)
			Assert.Equal(0, reader.ReadH());
	}

	private static void AssertCombatStats(
		PacketBuffer reader,
		int blockEvasionParry,
		int physicalAccuracy,
		int magicalAccuracy,
		int strikeResist,
		int spellResist)
	{
		Assert.Equal(18, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(1.5f, reader.ReadF());
		Assert.Equal(1500, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(2, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(physicalAccuracy, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(1, reader.ReadH());
		Assert.Equal(magicalAccuracy, reader.ReadH());
		Assert.Equal(50, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(1.0f, reader.ReadF());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(strikeResist, reader.ReadH());
		Assert.Equal(spellResist, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
	}

	private static void AssertBaseCombatStats(
		PacketBuffer reader,
		int blockEvasionParry,
		int physicalAccuracy,
		int magicalAccuracy,
		int strikeResist,
		int spellResist)
	{
		Assert.Equal(18, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadD());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(1.5f, reader.ReadF());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(blockEvasionParry, reader.ReadH());
		Assert.Equal(2, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(50, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(physicalAccuracy, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(magicalAccuracy, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(strikeResist, reader.ReadH());
		Assert.Equal(spellResist, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
		Assert.Equal(0, reader.ReadH());
	}

	private static void WriteCreateCharacterPayload(PacketBuffer buffer)
	{
		buffer.WriteD(99);
		buffer.WriteS("account-name");
		WriteFixedS(buffer, "Character", 25);
		buffer.WriteD(1);
		buffer.WriteD(0);
		buffer.WriteD(3);
		buffer.WriteD(7);
		buffer.WriteD(0x112233);
		buffer.WriteD(0x445566);
		buffer.WriteD(0x778899);
		buffer.WriteD(unchecked((int)0xAABBCC));
		for (var i = 1; i <= 52; i++)
			buffer.WriteC(i);
		buffer.WriteF(1.25f);
		buffer.WriteC(1);
	}

	private static string ReadFixedS(PacketBuffer buffer, int fixedLength)
	{
		var builder = new StringBuilder();
		for (var i = 0; i < fixedLength; i++)
		{
			var value = buffer.ReadH();
			if (value != 0)
				builder.Append((char)value);
		}

		buffer.ReadH();
		return builder.ToString();
	}

	private static void WriteFixedS(PacketBuffer buffer, string value, int fixedLength)
	{
		for (var i = 0; i < fixedLength; i++)
			buffer.WriteH(i < value.Length ? value[i] : 0);
		buffer.WriteH(0);
	}

	private static void WriteFixedUtf16Bytes(PacketBuffer buffer, string value)
	{
		var bytes = new byte[48];
		Encoding.Unicode.GetBytes(value, bytes);
		buffer.WriteB(bytes);
	}

	private static ItemTemplateSummary CreateBrokerTemplate(
		int templateId,
		IReadOnlySet<string>? classRestrictions = null,
		int craftLearnRecipeId = 0)
	{
		return new ItemTemplateSummary(
			templateId,
			$"item_{templateId}",
			0,
			0,
			1,
			string.Empty,
			string.Empty,
			string.Empty,
			string.Empty,
			1,
			0,
			0,
			ClassRestrictions: classRestrictions,
			CraftLearnRecipeId: craftLearnRecipeId);
	}
}
