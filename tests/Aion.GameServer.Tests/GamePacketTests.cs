using Aion.Commons.Network;
using System.Buffers.Binary;
using System.Text;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;
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
	public void CharacterSelectionServerPackets_WriteJavaShapedPayloads()
	{
		Assert.Equal(
			Convert.FromHexString("0000000000000000000000000000000000000000000000000004000000"),
			SerializeUnencryptedPayload(new SmAccountProperties()));
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
			Convert.FromHexString("014D00"),
			SerializeUnencryptedPayload(new SmTitleInfo(77)));
		Assert.Equal(
			Convert.FromHexString("0101000B000A00000001"),
			SerializeUnencryptedPayload(
				new SmMotion(
					[new PlayerMotion(11, 1010, true)],
					() => DateTimeOffset.FromUnixTimeSeconds(1000))));
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
	public void ClientPacketFactory_ParsesCharacterSelectionPackets()
	{
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
}
