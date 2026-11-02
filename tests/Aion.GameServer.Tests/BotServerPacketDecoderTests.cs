using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public sealed class BotServerPacketDecoderTests
{
	private readonly BotServerPacketDecoder decoder = new();

	[Theory]
	[InlineData(7, true)]
	[InlineData(23, true)]
	[InlineData(71, true)]
	[InlineData(8, true)]
	[InlineData(24, true)]
	[InlineData(1, false)]
	[InlineData(65, false)]
	[InlineData(11, false)]
	public void FreshNpcObservationDistinguishesCorpsesWithoutPriorKillHistory(ushort state, bool corpse)
	{
		string root = Aion.GameServer.TestKit.RealStaticData.RepoRoot();
		using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "parity-artifacts/golden/packets/SM_NPC_INFO.json")));
		byte[] body = Convert.FromHexString(golden.RootElement.GetProperty("cases")[0].GetProperty("payloadHex").GetString()!);
		// Java SM_NPC_INFO writes state and heading immediately after creatureType.
		System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(25), state);
		body[27] = 42;
		var decoded = decoder.Decode(typeof(SM_NPC_INFO), body);
		var world = new Aion.Bots.World.BotWorldModel();
		world.Apply(decoded);
		var npc = Assert.Single(world.Objects).Value;
		Assert.Equal(state, npc.State);
		Assert.Equal((byte)42, npc.Position.Heading);
		Assert.Equal(corpse, npc.IsCorpse);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_NPC_INFO), body[..27]));
	}

	[Fact]
	public void DecoderInventoryContainsExpectedBotPerceptionPackets()
	{
		Assert.Equal(128, decoder.PacketTypes.Count);
		Assert.Contains(typeof(SM_POSITION), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SHOW_BRAND), decoder.PacketTypes);
		Assert.Contains(typeof(SM_RESURRECT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_INSTANCE_INFO), decoder.PacketTypes);
		Assert.Contains(typeof(SM_GAME_TIME), decoder.PacketTypes);
		Assert.Contains(typeof(SM_ATTACK), decoder.PacketTypes);
		Assert.Contains(typeof(SM_RECONNECT_KEY), decoder.PacketTypes);
		Assert.Contains(typeof(SM_BIND_POINT_INFO), decoder.PacketTypes);
		Assert.Contains(typeof(SM_KISK_UPDATE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_UNWRAP_ITEM), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SHOW_DECOMPOSABLE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SELECT_DECOMPOSABLE_RESULT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_TUNE_RESULT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SKILL_REMOVE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SKILL_ACTIVATION), decoder.PacketTypes);
		Assert.Contains(typeof(SM_RIDE_ROBOT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SUMMON_PANEL), decoder.PacketTypes);
		Assert.Contains(typeof(SM_RESET_SKILL_COOLDOWN), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SUMMON_OWNER_REMOVE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SUMMON_USESKILL), decoder.PacketTypes);
		Assert.Contains(typeof(SmAttackStatus), decoder.PacketTypes);
		Assert.Contains(typeof(SM_MESSAGE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_EMOTION), decoder.PacketTypes);
		Assert.Contains(typeof(SM_USE_OBJECT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_SYSTEM_MESSAGE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_PLAYER_SPAWN), decoder.PacketTypes);
		Assert.Contains(typeof(SM_INVENTORY_ADD_ITEM), decoder.PacketTypes);
		Assert.Contains(typeof(SM_WINDSTREAM), decoder.PacketTypes);
		Assert.Contains(typeof(SM_WINDSTREAM_ANNOUNCE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_ABNORMAL_STATE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_ABNORMAL_EFFECT), decoder.PacketTypes);
		Assert.Contains(typeof(SM_GATHER_UPDATE), decoder.PacketTypes);
		Assert.Contains(typeof(SM_FORCED_MOVE), decoder.PacketTypes);
	}

	[Fact]
	public void ForcedMoveNamesTheMovedCreatureAndWhereItLanded()
	{
		// Java SM_FORCED_MOVE golden payload (parity-artifacts/golden/packets/SM_FORCED_MOVE.json).
		byte[] body = Convert.FromHexString("61AE0A0002350C001000509A440028D44500407A43");
		var forced = decoder.Decode(typeof(SM_FORCED_MOVE), body);
		Assert.Equal(700001, forced.Get<int>("effectorObjectId"));
		Assert.Equal(800002, forced.Get<int>("objectId"));
		Assert.Equal(1234.5f, forced.Get<float>("x"));
		Assert.Equal(6789f, forced.Get<float>("y"));
		Assert.Equal(250.25f, forced.Get<float>("z"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_FORCED_MOVE), body[..^1]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_FORCED_MOVE), [.. body, 0]));
	}

	[Fact]
	public void UseObjectCompletionDistinguishesThreeSecondFinishFromAttackAbort()
	{
		// Java ce54b7931 SM_USE_OBJECT / ActionItemNpcAI. These body shapes
		// were also observed in the NI-07 Mau sack traces.
		var completed = decoder.Decode(typeof(SM_USE_OBJECT),
			Convert.FromHexString("8B060200E4400000B80B000002"));
		Assert.Equal(132747, completed.Get<int>("playerObjectId"));
		Assert.Equal(16612, completed.Get<int>("targetObjectId"));
		Assert.Equal(3000, completed.Get<int>("durationMs"));
		Assert.Equal((byte)2, completed.Get<byte>("actionType"));
		var aborted = decoder.Decode(typeof(SM_USE_OBJECT),
			Convert.FromHexString("8B060200E44000000000000002"));
		Assert.Equal(0, aborted.Get<int>("durationMs"));
		for (int length = 0; length < 13; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_USE_OBJECT),
				Convert.FromHexString("8B060200E4400000B80B000002")[..length]));
	}

	[Theory]
	[InlineData(typeof(SM_DELETE_CHARACTER), 12)]
	[InlineData(typeof(SM_RESTORE_CHARACTER), 8)]
	public void CharacterDeletionAndRestorationDecodeBothGoldenOutcomesAndRejectBadLengths(Type packetType, int size)
	{
		using var fixture = LoadFixture(packetType.Name + ".json");
		var cases = fixture.RootElement.GetProperty("cases");
		Assert.Equal(2, cases.GetArrayLength());
		for (int i = 0; i < cases.GetArrayLength(); i++)
		{
			byte[] body = Convert.FromHexString(cases[i].GetProperty("payloadHex").GetString()!);
			Assert.Equal(size, body.Length);
			var packet = decoder.Decode(packetType, body);
			Assert.Equal(i == 0 ? 0 : 0x10, packet.Get<int>("responseCode"));
			Assert.Equal(2, AssertPrimitiveInputs(cases[i].GetProperty("inputs"), packet.Fields));
			for (int length = 0; length < size; length++)
				Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, body[..length]));
			Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, [.. body, 0]));
		}
	}

	[Fact]
	public void ChannelInfoReadsJavaGoldenAndTracksObservedInstanceWithoutInferringFromMap()
	{
		using var fixture = LoadFixture("SM_CHANNEL_INFO.json");
		var cases = fixture.RootElement.GetProperty("cases");
		Assert.Equal(1, cases.GetArrayLength());
		byte[] golden = Convert.FromHexString(cases[0].GetProperty("payloadHex").GetString()!);
		var fallback = decoder.Decode(typeof(SM_CHANNEL_INFO), golden);
		Assert.Equal(1, fallback.Get<int>("currentChannel")); // Java's null-position fallback is intentionally 1/1.
		Assert.Equal(1, fallback.Get<int>("instanceCount"));
		var world = new Aion.Bots.World.BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_CHANNEL_INFO), Convert.FromHexString("0400000005000000")));
		Assert.Equal((4, 5), world.ChannelInfo);
		world.BeginWorldReload();
		Assert.Null(world.ChannelInfo);
		world.Apply(decoder.Decode(typeof(SM_CHANNEL_INFO), Convert.FromHexString("0000000005000000")));
		Assert.Equal((0, 5), world.ChannelInfo);
		for (int size = 0; size < 8; size++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_CHANNEL_INFO), golden[..size]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_CHANNEL_INFO), [.. golden, 0]));
	}

	[Fact]
	public void CubeExpansionDecodesAuditedCompleteBodyAndIgnoresOtherStorageAndStigmaUpdates()
	{
		// SM_CUBE_UPDATE.writeImpl at ce54b7931: C action, C storage, D item count, C NPC/quest/item expands.
		byte[] body = Convert.FromHexString("000044332211010203");
		var world = new Aion.Bots.World.BotWorldModel();
		var packet = decoder.Decode(typeof(SM_CUBE_UPDATE), body);
		Assert.Equal(0x11223344, packet.Get<int>("itemsCount"));
		world.Apply(packet);
		Assert.Equal(new Aion.Bots.World.BotCubeExpansion(1, 2, 3), world.CubeExpansion);
		Assert.Equal(81, world.CubeExpansion!.Capacity);
		body[1] = 1;
		world.Apply(decoder.Decode(typeof(SM_CUBE_UPDATE), body));
		world.Apply(decoder.Decode(typeof(SM_CUBE_UPDATE), [6, 4]));
		Assert.Equal(new Aion.Bots.World.BotCubeExpansion(1, 2, 3), world.CubeExpansion);
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_CUBE_UPDATE), body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_CUBE_UPDATE), [.. body, 0]));
	}

	[Theory]
	[InlineData(typeof(SM_TITLE_INFO), 4)]
	[InlineData(typeof(SM_MACRO_LIST), 2)]
	[InlineData(typeof(SM_MACRO_RESULT), 2)]
	[InlineData(typeof(SM_PLASTIC_SURGERY), 2)]
	[InlineData(typeof(SM_QUIT_RESPONSE), 2)]
	[InlineData(typeof(SM_CHARACTER_SELECT), 4)]
	public void CharacterSettingsPacketsPreserveGoldenFieldsAndRejectMalformedLengths(Type packetType, int count)
	{
		using var fixture = LoadFixture(packetType.Name + ".json");
		var cases = fixture.RootElement.GetProperty("cases");
		Assert.Equal(count, cases.GetArrayLength());
		foreach (var example in cases.EnumerateArray())
		{
			byte[] body = Convert.FromHexString(example.GetProperty("payloadHex").GetString()!);
			var packet = decoder.Decode(packetType, body);
			Assert.True(AssertPrimitiveInputs(example.GetProperty("inputs"), packet.Fields) > 0);
			if (packetType == typeof(SM_MACRO_LIST))
			{
				var rows = example.GetProperty("inputs").GetProperty("macros");
				var macros = packet.Get<BotMacro[]>("macros");
				Assert.Equal(rows.GetArrayLength(), macros.Length);
				for (int i = 0; i < macros.Length; i++) Assert.Equal(new BotMacro(rows[i][0].GetByte(), rows[i][1].GetString()!), macros[i]);
			}
			if (packetType == typeof(SM_PLASTIC_SURGERY))
			{
				Assert.Equal(example.GetProperty("inputs").GetProperty("objectId").GetInt32(), packet.Get<int>("playerObjId"));
				Assert.Equal(body[4] == 1, packet.Get<bool>("hasTicket"));
			}
			for (int length = 0; length < body.Length; length++)
				Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, body[..length]));
			Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, [.. body, 0]));
		}
	}

	[Fact]
	public void TitleCatalogPreservesExpirationAndMentorAndUnsetTitleForms()
	{
		var catalog = decoder.Decode(typeof(SM_TITLE_INFO), Convert.FromHexString("000002002A000000FFFFFFFF07000000B4000000"));
		Assert.Equal(new[] { new BotTitle(42, -1), new BotTitle(7, 180) }, catalog.Get<BotTitle[]>("titles"));
		Assert.Equal(ushort.MaxValue, decoder.Decode(typeof(SM_TITLE_INFO), [1, 255, 255]).Get<ushort>("titleId"));
		Assert.Equal((ushort)1, decoder.Decode(typeof(SM_TITLE_INFO), [4, 1, 0]).Get<ushort>("titleId"));
		var broadcast = decoder.Decode(typeof(SM_TITLE_INFO), [5, 12, 0, 0, 0, 1, 0]);
		Assert.Equal(12, broadcast.Get<int>("playerObjId")); Assert.Equal((ushort)1, broadcast.Get<ushort>("titleId"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_TITLE_INFO), [2]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_MACRO_LIST), [1, 0, 0, 0, 1, 1, 0]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PLASTIC_SURGERY), [1, 0, 0, 0, 0, 0]));
	}

	[Fact]
	public void UiSettingsPreservePayloadAndWirePaddingIncludingTheServersOverlengthForm()
	{
		using var fixture = LoadFixture("SM_UI_SETTINGS.json");
		var cases = fixture.RootElement.GetProperty("cases");
		Assert.True(cases.GetArrayLength() > 0);
		foreach (var example in cases.EnumerateArray())
		{
			byte[] body = Convert.FromHexString(example.GetProperty("payloadHex").GetString()!);
			var input = example.GetProperty("inputs");
			byte[] expected = input.GetProperty("data").EnumerateArray().Select(b => b.GetByte()).ToArray();
			var decoded = decoder.Decode(typeof(SM_UI_SETTINGS), body);
			Assert.Equal(input.GetProperty("type").GetByte(), decoded.Get<byte>("type"));
			Assert.Equal((ushort)0x1C00, decoded.Get<ushort>("declaredSize"));
			var padded = decoded.Get<byte[]>("paddedData");
			Assert.Equal(0x1C00, padded.Length); Assert.Equal(expected, padded[..expected.Length]);
			Assert.All(padded.Skip(expected.Length), b => Assert.Equal((byte)0, b));
			foreach (int length in new[] { 0, 1, 2, 3, body.Length - 1 })
				Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_UI_SETTINGS), body[..length]));
			Assert.Equal((byte)0xA5, decoder.Decode(typeof(SM_UI_SETTINGS), [.. body, 0xA5]).Get<byte[]>("paddedData")[^1]);
		}
	}

	[Theory]
	[InlineData(typeof(SM_SHOW_DECOMPOSABLE), 2)]
	[InlineData(typeof(SM_UNWRAP_ITEM), 1)]
	public void UnwrapAndBoxPreviewsMatchGoldenInputsAndRejectMalformedLengths(Type packetType, int caseCount)
	{
		using var fixture = LoadFixture(packetType.Name + ".json");
		Assert.Equal(caseCount, fixture.RootElement.GetProperty("cases").GetArrayLength());
		foreach (var example in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var input = example.GetProperty("inputs");
			byte[] body = Convert.FromHexString(example.GetProperty("payloadHex").GetString()!);
			var packet = decoder.Decode(packetType, body);
			Assert.Equal(input.GetProperty("objectId").GetInt32(), packet.Get<int>("objectId"));
			if (packetType == typeof(SM_UNWRAP_ITEM))
				Assert.Equal(input.GetProperty("count").GetByte(), packet.Get<byte>("count"));
			else
			{
				Assert.Equal(0, packet.Get<int>("reserved"));
				var expected = input.GetProperty("items");
				var choices = packet.Get<BotDecomposableChoice[]>("choices");
				Assert.Equal(expected.GetArrayLength(), choices.Length);
				for (int i = 0; i < choices.Length; i++)
					Assert.Equal(new BotDecomposableChoice((byte)i, expected[i][0].GetInt32(), expected[i][1].GetInt32(), 0, 0, 0, 1), choices[i]);
			}
			var world = new Aion.Bots.World.BotWorldModel();
			world.Apply(packet);
			Assert.Empty(world.Inventory); // Receipt/preview is not an inventory update.
			for (int length = 0; length < body.Length; length++)
				Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, body[..length]));
			Assert.Throws<InvalidDataException>(() => decoder.Decode(packetType, [.. body, 0]));
		}
	}

	/// <summary>
	/// Upstream d254ea172 answers a pick with this instead of an empty second preview: the box's object id and one byte
	/// saying whether the chosen item was handed out. It carries no items, so it is not part of the preview theory above.
	/// </summary>
	[Fact]
	public void SelectionResultDecodesBothVerdictsWithoutApplyingThemToInventory()
	{
		using var fixture = LoadFixture("SM_SELECT_DECOMPOSABLE_RESULT.json");
		Assert.Equal(2, fixture.RootElement.GetProperty("cases").GetArrayLength());
		foreach (var example in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var input = example.GetProperty("inputs");
			byte[] body = Convert.FromHexString(example.GetProperty("payloadHex").GetString()!);
			var packet = decoder.Decode(typeof(SM_SELECT_DECOMPOSABLE_RESULT), body);
			Assert.Equal(input.GetProperty("objectId").GetInt32(), packet.Get<int>("objectId"));
			Assert.Equal(0, packet.Get<int>("reserved"));
			Assert.Equal(input.GetProperty("result").GetInt32() == 0, packet.Get<bool>("granted"));
			var world = new Aion.Bots.World.BotWorldModel();
			world.Apply(packet);
			Assert.Empty(world.Inventory); // The verdict is not an inventory update; the item arrives in its own packet.
			for (int length = 0; length < body.Length; length++)
				Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_SELECT_DECOMPOSABLE_RESULT), body[..length]));
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_SELECT_DECOMPOSABLE_RESULT), [.. body, 0]));
		}
	}

	[Fact]
	public void TuningPreviewDecodesBothGoldenModesWithoutApplyingThemToInventory()
	{
		using var fixture = LoadFixture("SM_TUNE_RESULT.json");
		Assert.Equal(2, fixture.RootElement.GetProperty("cases").GetArrayLength());
		foreach (var example in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var input = example.GetProperty("inputs");
			byte[] body = Convert.FromHexString(example.GetProperty("payloadHex").GetString()!);
			var packet = decoder.Decode(typeof(SM_TUNE_RESULT), body);
			var enchantment = packet.Get<Aion.Bots.World.BotItemEnchantment>("enchantment");
			Assert.Equal(input.GetProperty("objectId").GetInt32(), packet.Get<int>("objectId"));
			Assert.Equal(input.GetProperty("tuningScrollItemId").GetInt32(), packet.Get<int>("tuningScrollItemId"));
			Assert.Equal(input.GetProperty("statBonusId").GetByte(), packet.Get<byte>("statBonusId"));
			Assert.Equal(input.GetProperty("optionalSockets").GetByte(), enchantment.OptionalSockets);
			Assert.Equal(input.GetProperty("enchantBonus").GetByte(), enchantment.EnchantBonus);
			Assert.Equal(!input.GetProperty("attributeOnly").GetBoolean(), packet.Get<bool>("showManastoneSlots"));
			Assert.Equal(!input.GetProperty("attributeOnly").GetBoolean(), packet.Get<bool>("tuneCancelPossible"));
			var world = new Aion.Bots.World.BotWorldModel();
			world.Apply(packet);
			Assert.Empty(world.Inventory); // This is a proposal; only the later inventory update commits a choice.
			for (int length = 0; length < body.Length; length++)
				Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_TUNE_RESULT), body[..length]));
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_TUNE_RESULT), [.. body, 0]));
			body[^1] = 2;
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_TUNE_RESULT), body));
		}
	}

	[Fact]
	public void SkillRemovalPreservesUnsignedIdProfessionFlagAndTypeAndRejectsBadLengths()
	{
		byte[] body = Convert.FromHexString("FFFFFE03");
		var packet = decoder.Decode(typeof(SM_SKILL_REMOVE), body);
		Assert.Equal((ushort)65535, packet.Get<ushort>("skillId"));
		Assert.Equal((byte)254, packet.Get<byte>("levelOrProfessionFlag"));
		Assert.Equal((byte)3, packet.Get<byte>("skillType"));
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_SKILL_REMOVE), body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_SKILL_REMOVE), [.. body, 0]));
	}

	[Fact]
	public void WindstreamDecodersExposeStateAndAnnouncementIdentity()
	{
		using var stateFixture = LoadFixture("SM_WINDSTREAM.json");
		var stateCase = stateFixture.RootElement.GetProperty("cases")[0];
		var state = decoder.Decode(typeof(SM_WINDSTREAM),
			Convert.FromHexString(stateCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(stateCase.GetProperty("inputs").GetProperty("unk1").GetInt32(), state.Get<int>("state"));

		using var announceFixture = LoadFixture("SM_WINDSTREAM_ANNOUNCE.json");
		var announceCase = announceFixture.RootElement.GetProperty("cases")[0];
		var announce = decoder.Decode(typeof(SM_WINDSTREAM_ANNOUNCE),
			Convert.FromHexString(announceCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(announceCase.GetProperty("inputs").GetProperty("mapId").GetInt32(), announce.Get<int>("mapId"));
		Assert.Equal(announceCase.GetProperty("inputs").GetProperty("streamId").GetInt32(), announce.Get<int>("streamId"));
	}

	[Fact]
	public void MovementSpeedComesFromPlayerInfoAndEmotionRatherThanStatsInfo()
	{
		using var playerFixture = LoadFixture("SM_PLAYER_INFO.json");
		var playerCase = playerFixture.RootElement.GetProperty("cases")[0];
		var player = decoder.Decode(typeof(SM_PLAYER_INFO),
			Convert.FromHexString(playerCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(6f, player.Get<float>("movementSpeed"));

		using var emotionFixture = LoadFixture("SM_EMOTION.json");
		var speedCase = emotionFixture.RootElement.GetProperty("cases")[2];
		var emotion = decoder.Decode(typeof(SM_EMOTION),
			Convert.FromHexString(speedCase.GetProperty("payloadHex").GetString()!));
		Assert.Equal(6f, emotion.Get<float>("movementSpeed"));
		Assert.Equal((ushort)1000, emotion.Get<ushort>("currentAttackSpeed"));
	}

	[Fact]
	public void EveryGoldenCaseForEveryDecodedPacketDecodesToRecordedPrimitiveInputs()
	{
		foreach (var packetType in decoder.PacketTypes.OrderBy(type => type.Name, StringComparer.Ordinal))
		{
			if (BotSocialPacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotAlliancePacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotExtendedSocialPacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotBrokerPacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotPrivateStorePacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotTradeInPacketTests.AssertAuditedWireContract(packetType)) continue;
			if (BotResurrectionPacketTests.AssertAuditedWireContract(packetType)) continue;
			if (NaturalAltgardHaramelContractTests.AssertAuditedWireContract(packetType)) continue;
			if (packetType == typeof(SM_UPDATE_PLAYER_APPEARANCE))
			{
				BotPlayerCommandPacketTests.AssertAppearanceWireContract();
				continue;
			}
			if (packetType == typeof(SM_GAME_TIME))
			{
				// AK-00: no Java-generated fixture, since Java's SM_GAME_TIME reads GameTimeService's singleton, which loads
				// from the database. Pin its audited one-field layout here; the AK-00 SIM probe checks the value against the
				// running GameTimeService.
				AssertGameTimeWireContract();
				continue;
			}
			if (packetType == typeof(SM_SUMMON_PANEL))
			{
				// NR-110a: no Java-generated fixture, since Java's SM_SUMMON_PANEL reads a live spirit. Pin its audited layout
				// here; the NR-110a probe row checks the object against the running server's spirit.
				AssertSummonPanelWireContract();
				continue;
			}
			if (packetType == typeof(SM_PRICES))
			{
				// No existing Java-generated fixture for this connection-dependent packet. Pin its complete,
				// audited three-byte layout here; SIM E3 also checks the values against the running PricesService.
				AssertPricesWireContract();
				continue;
			}
			using var fixture = LoadFixture((packetType == typeof(SmAttackStatus) ? "SM_ATTACK_STATUS" : packetType.Name) + ".json");
			var root = fixture.RootElement;
			Assert.Equal("Java", root.GetProperty("source").GetString());
			foreach (var fixtureCase in root.GetProperty("cases").EnumerateArray())
			{
				var body = Convert.FromHexString(fixtureCase.GetProperty("payloadHex").GetString()!);
				var decoded = decoder.Decode(packetType, body);
				var comparisons = AssertPrimitiveInputs(fixtureCase.GetProperty("inputs"), decoded.Fields);
				// A packet that is a list and nothing else: each Java case is one mark, and its inputs are that row.
				if (packetType == typeof(SM_SHOW_BRAND))
				{
					var brand = Assert.Single(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("brands"));
					Assert.Equal(1, brand["type"]);
					comparisons += AssertPrimitiveInputs(fixtureCase.GetProperty("inputs"), brand);
				}
				AssertCaseWithoutTopLevelPrimitive(packetType, decoded, comparisons);
			}
		}
	}

	[Fact]
	public void PricesPacketHasExactlyThreeUnsignedPercentages() => AssertPricesWireContract();

	[Fact]
	public void AttackStatusPreservesSignedDamageAndUnsignedSkillAndRejectsBadLengths()
	{
		byte[] body = Convert.FromHexString("78563412DAFFFFFF0764FFFF190C");
		var packet = decoder.Decode(typeof(SmAttackStatus), body);
		Assert.Equal(0x12345678, packet.Get<int>("objectId"));
		Assert.Equal(-38, packet.Get<int>("writtenValue"));
		Assert.Equal((byte)7, packet.Get<byte>("typeId"));
		Assert.Equal((byte)100, packet.Get<byte>("hpOrMp"));
		Assert.Equal(ushort.MaxValue, packet.Get<ushort>("skillId"));
		Assert.Equal((byte)25, packet.Get<byte>("logId"));
		Assert.Equal((byte)12, packet.Get<byte>("criticalDisplayCode"));
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SmAttackStatus), body.AsSpan(0, length)));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SmAttackStatus), [.. body, 0]));
	}

	[Fact]
	public void AttackGoldenPacketsExposeClientVisibleAttackerAndTarget()
	{
		using var fixture = LoadFixture("SM_ATTACK.json");
		foreach (JsonElement sample in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			byte[] body = Convert.FromHexString(sample.GetProperty("payloadHex").GetString()!);
			DecodedBotServerPacket attack = decoder.Decode(typeof(SM_ATTACK), body);
			Assert.Equal(sample.GetProperty("inputs").GetProperty("attackerObjId").GetInt32(),
				attack.Get<int>("attackerObjId"));
			Assert.Equal(sample.GetProperty("inputs").GetProperty("targetObjId").GetInt32(),
				attack.Get<int>("targetObjId"));
		}
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ATTACK), new byte[12]));
	}

	[Fact]
	public void AbyssRankGoldenPacketUpdatesClientRewardAndKillCounters()
	{
		using var fixture = LoadFixture("SM_ABYSS_RANK.json");
		byte[] body = Convert.FromHexString(fixture.RootElement.GetProperty("cases")[0].GetProperty("payloadHex").GetString()!);
		var world = new Aion.Bots.World.BotWorldModel();
		world.Apply(decoder.Decode(typeof(SM_ABYSS_RANK), body));
		Assert.Equal(new Aion.Bots.World.BotAbyssRank(1_000_000, 50_000, 1, 12_345, 1234, 7,
			new(12, 3000, 400), new(56, 80_000, 9000), new(7, 200, 30)), world.AbyssRank);
		for (int length = 0; length < body.Length; length++)
			Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ABYSS_RANK), body[..length]));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_ABYSS_RANK), [.. body, 0]));
		// AP counters are Q fields, not truncated to int even for a rich player.
		System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(body, 5_000_000_000L);
		world.Apply(decoder.Decode(typeof(SM_ABYSS_RANK), body));
		Assert.Equal(5_000_000_000L, world.AbyssRank!.Ap);
	}

	[Fact]
	public void GameTimePacketIsTheMinutesSinceYearZero() => AssertGameTimeWireContract();

	// Java SM_GAME_TIME.writeImpl: writeD(GameTimeService.getInstance().getGameTime().getTime()), the minutes since
	// 01.01.0000 00:00. 529,803 minutes is 22:03 on day 367.
	private void AssertGameTimeWireContract()
	{
		var packet = decoder.Decode(typeof(SM_GAME_TIME), BitConverter.GetBytes(529_803));
		Assert.Equal(529_803, packet.Get<int>("minutes"));
		Assert.Equal(22, 529_803 / 60 % 24);
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_GAME_TIME), [1, 2, 3]));
	}

	// Java SM_SUMMON_PANEL.writeImpl: D object, H level, D 0, D 0, D HP, D most HP, D attack, D physical defence,
	// D magical defence, H 0, D the time it lives.
	private void AssertSummonPanelWireContract()
	{
		byte[] body =
		[
			.. BitConverter.GetBytes(700_123), .. BitConverter.GetBytes((ushort)16), .. BitConverter.GetBytes(0), .. BitConverter.GetBytes(0),
			.. BitConverter.GetBytes(1_500), .. BitConverter.GetBytes(1_800), .. BitConverter.GetBytes(90), .. BitConverter.GetBytes(300),
			.. BitConverter.GetBytes(200), .. BitConverter.GetBytes((ushort)0), .. BitConverter.GetBytes(0),
		];
		Assert.Equal(40, body.Length);
		var packet = decoder.Decode(typeof(SM_SUMMON_PANEL), body);
		Assert.Equal(700_123, packet.Get<int>("objectId"));
		Assert.Equal((ushort)16, packet.Get<ushort>("level"));
		Assert.Equal(1_500, packet.Get<int>("currentHp"));
		Assert.Equal(1_800, packet.Get<int>("maxHp"));
		Assert.Equal(90, packet.Get<int>("attack"));
		Assert.Equal(300, packet.Get<int>("physicalDefence"));
		Assert.Equal(200, packet.Get<int>("magicalDefence"));
		Assert.Equal(0, packet.Get<int>("liveTime"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_SUMMON_PANEL), [.. body, 0]));
	}

	private void AssertPricesWireContract()
	{
		var packet = decoder.Decode(typeof(SM_PRICES), [125, 107, 109]);
		Assert.Equal((byte)125, packet.Get<byte>("globalPrices"));
		Assert.Equal((byte)107, packet.Get<byte>("globalModifier"));
		Assert.Equal((byte)109, packet.Get<byte>("taxes"));
		Assert.Throws<InvalidDataException>(() => decoder.Decode(typeof(SM_PRICES), [125, 107]));
	}

	[Fact]
	public void RepurchaseDecoderReadsItemBlobBeforeItsPrice()
	{
		using var fixture = LoadFixture("SM_REPURCHASE.json");
		var entry = fixture.RootElement.GetProperty("cases")[0];
		var packet = decoder.Decode(typeof(SM_REPURCHASE), Convert.FromHexString(entry.GetProperty("payloadHex").GetString()!));
		var item = Assert.Single(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
		Assert.Equal(entry.GetProperty("inputs").GetProperty("objectId").GetInt32(), item["objectId"]);
		// The fixture's top-level itemCount=1 counts rows; its generator's ITEM_COUNT=7 is the stack size.
		Assert.Equal(7L, item["itemCount"]);
		Assert.Equal(entry.GetProperty("inputs").GetProperty("repurchasePrice").GetInt64(), item["repurchasePrice"]);
	}

	[Fact]
	public void ItemPacketDecodersRespectTheJavaLengthPrefixedBlobBoundary()
	{
		using var addFixture = LoadFixture("SM_INVENTORY_ADD_ITEM.json");
		var addCase = addFixture.RootElement.GetProperty("cases")[0];
		var add = decoder.Decode(typeof(SM_INVENTORY_ADD_ITEM), Convert.FromHexString(addCase.GetProperty("payloadHex").GetString()!));
		var addItems = add.Get<List<IReadOnlyDictionary<string, object?>>>("items");
		Assert.Single(addItems);
		Assert.Equal(268700001, addItems[0]["objectId"]);
		Assert.Equal(1L, addItems[0]["itemCount"]);
		Assert.Equal("Smith", addItems[0]["itemCreator"]);
		Assert.NotEmpty(Assert.IsType<byte[]>(addItems[0]["blob"]));

		using var updateFixture = LoadFixture("SM_INVENTORY_UPDATE_ITEM.json");
		foreach (var fixtureCase in updateFixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var update = decoder.Decode(typeof(SM_INVENTORY_UPDATE_ITEM),
				Convert.FromHexString(fixtureCase.GetProperty("payloadHex").GetString()!));
			Assert.NotEmpty(update.Get<byte[]>("blob"));
			Assert.Equal(7L, update.Get<long>("itemCount"));
			Assert.True(update.Fields.ContainsKey("updateMask"));
		}
	}

	[Fact]
	public void GatherableDecoderDistinguishesGatherablesAndStaticDoors()
	{
		using var fixture = LoadFixture("SM_GATHERABLE_INFO.json");
		var body = Convert.FromHexString(fixture.RootElement.GetProperty("cases")[0].GetProperty("payloadHex").GetString()!);

		var gatherable = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.False(gatherable.Get<bool>("isStatic"));
		Assert.Null(gatherable.Fields["open"]);

		body[24] = 0x09;
		var openDoor = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.True(openDoor.Get<bool>("isStatic"));
		Assert.True(openDoor.Get<bool>("open"));

		body[24] = 0x0A;
		var closedDoor = decoder.Decode(typeof(SM_GATHERABLE_INFO), body);
		Assert.True(closedDoor.Get<bool>("isStatic"));
		Assert.False(closedDoor.Get<bool>("open"));
	}

	[Fact]
	public void CharacterSelectionDecodersExposeIdentityAndPersistedPosition()
	{
		var summary = CreateCharacterSummary(100_001, "Aelivea", 210010000, 121.25f, 132.5f, 144.75f);
		using var listBody = new MemoryStream();
		using (var writer = new BinaryWriter(listBody, Encoding.Unicode, leaveOpen: true))
		{
			writer.Write(0x11223344);
			writer.Write((byte)1);
			writer.Write(summary);
		}

		var list = decoder.Decode(typeof(SM_CHARACTER_LIST), listBody.ToArray());
		var characters = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters");
		var character = Assert.Single(characters);
		Assert.Equal(100_001, character["objectId"]);
		Assert.Equal("Aelivea", character["name"]);
		Assert.Equal(210010000, character["mapId"]);
		Assert.Equal(121.25f, character["x"]);
		Assert.Equal(132.5f, character["y"]);
		Assert.Equal(144.75f, character["z"]);

		using var createBody = new MemoryStream();
		using (var writer = new BinaryWriter(createBody, Encoding.Unicode, leaveOpen: true))
		{
			writer.Write(0);
			writer.Write(summary);
		}
		var create = decoder.Decode(typeof(SM_CREATE_CHARACTER), createBody.ToArray());
		var created = create.Get<IReadOnlyDictionary<string, object?>>("character");
		Assert.Equal(100_001, created["objectId"]);
		Assert.Equal("Aelivea", created["name"]);
	}

	[Fact]
	public void SystemMessageNameTableIsCurrentWithTheFactoryCatalog()
	{
		var methods = typeof(SM_SYSTEM_MESSAGE).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
			.Where(method => method.Name.StartsWith("STR_", StringComparison.Ordinal))
			.ToArray();
		Assert.Equal(4_116, methods.Length); // upstream d254ea172 added STR_MSG_CAN_NOT_DISASSEMBLE_LORE_ITEM
		Assert.Equal(SystemMessageNames.FactoryCount, methods.Length);
		Assert.Equal(4_111, SystemMessageNames.All.Count);

		var source = File.ReadAllText(Path.Combine(RepoRoot(),
			"src", "Aion.GameServer", "Network", "Aion", "ServerPackets", "SM_SYSTEM_MESSAGE.cs"));
		var normalizedSource = source.Replace("\r\n", "\n", StringComparison.Ordinal);
		var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSource)));
		Assert.Equal(SystemMessageNames.SourceSha256, sourceHash);
		Assert.Equal("STR_MOVE_PORTAL_ERROR_INVALID_RACE", SystemMessageNames.GetNameOrNull(901354));
		Assert.Equal("STR_SKILL_NOT_ENOUGH_DISTANCE", SystemMessageNames.GetNameOrNull(1402920));
	}

	private static int AssertPrimitiveInputs(JsonElement inputs, IReadOnlyDictionary<string, object?> fields)
	{
		var comparisons = 0;
		foreach (var property in inputs.EnumerateObject())
		{
			if (!fields.TryGetValue(property.Name, out var actual) || actual == null)
				continue;
			switch (property.Value.ValueKind)
			{
				case JsonValueKind.True:
				case JsonValueKind.False:
					Assert.Equal(property.Value.GetBoolean(), Assert.IsType<bool>(actual));
					comparisons++;
					break;
				case JsonValueKind.Number when IsNumeric(actual):
					Assert.Equal(property.Value.GetDecimal(), Convert.ToDecimal(actual, System.Globalization.CultureInfo.InvariantCulture));
					comparisons++;
					break;
				case JsonValueKind.String when actual is string text:
					Assert.Equal(property.Value.GetString(), text);
					comparisons++;
					break;
				case JsonValueKind.Array when actual is byte[] bytes:
					Assert.Equal(property.Value.EnumerateArray().Select(value => value.GetByte()), bytes);
					comparisons++;
					break;
				case JsonValueKind.Array when actual is int[] integers:
					Assert.Equal(property.Value.EnumerateArray().Select(value => value.GetInt32()), integers);
					comparisons++;
					break;
			}
		}
		return comparisons;
	}

	private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

	private static byte[] CreateCharacterSummary(int objectId, string name, int mapId, float x, float y, float z)
	{
		using var output = new MemoryStream();
		using var writer = new BinaryWriter(output, Encoding.Unicode, leaveOpen: true);
		writer.Write(objectId);
		WriteFixedString(writer, name, 25);
		writer.Write(0); // gender
		writer.Write(0); // Elyos
		writer.Write(0); // warrior
		for (var i = 0; i < 5; i++) writer.Write(0); // voice and colours
		writer.Write(new byte[52]);
		writer.Write(1f);
		writer.Write(0); // template
		writer.Write(mapId);
		writer.Write(x);
		writer.Write(y);
		writer.Write(z);
		writer.Write(32); // heading
		writer.Write((ushort)1);
		writer.Write((ushort)0);
		writer.Write(0); // title
		writer.Write(0); // legion id
		WriteFixedString(writer, "", 40);
		writer.Write((ushort)0);
		writer.Write(1_700_000_000); // last online
		writer.Write(new byte[16 * 13]);
		writer.Write(new byte[(6 * sizeof(int)) + 68]);
		writer.Write(0); // deletion time
		writer.Write(new byte[2 * sizeof(ushort)]);
		writer.Write(new byte[4 * sizeof(int)]);
		writer.Write(0L); // broker proceeds
		writer.Write(new byte[7 * sizeof(int)]);
		writer.Write((ushort)0); // empty ban reason
		return output.ToArray();
	}

	private static void WriteFixedString(BinaryWriter writer, string value, int characters)
	{
		for (var i = 0; i < characters; i++)
			writer.Write(i < value.Length ? value[i] : '\0');
		writer.Write('\0');
	}

	private static void AssertCaseWithoutTopLevelPrimitive(Type packetType, DecodedBotServerPacket decoded, int comparisons)
	{
		if (comparisons > 0)
			return;
		if (packetType == typeof(SM_KEY))
		{
			Assert.Equal(0x1A3D5948, decoded.Get<int>("encodedKey"));
			return;
		}
		if (packetType == typeof(SM_CHANNEL_INFO))
		{
			// The existing Java fixture takes a null position, not primitive constructor arguments.
			Assert.Equal(1, decoded.Get<int>("currentChannel"));
			Assert.Equal(1, decoded.Get<int>("instanceCount"));
			return;
		}
		if (packetType == typeof(SM_LEAVE_GROUP_MEMBER))
		{
			Assert.Equal(0, decoded.Get<int>("groupId"));
			Assert.Equal((byte)0, decoded.Get<byte>("reserved"));
			Assert.Equal(63, decoded.Get<int>("teamType"));
			Assert.Equal(0, decoded.Get<int>("teamSubType"));
			Assert.Equal((ushort)0, decoded.Get<ushort>("reserved2"));
			return;
		}
		if (packetType == typeof(SM_SKILL_LIST))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("skills"));
			return;
		}
		if (packetType == typeof(SM_SKILL_COOLDOWN))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("cooldowns"));
			return;
		}
		if (packetType == typeof(SM_QUEST_LIST))
		{
			Assert.Empty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("quests"));
			return;
		}
		if (packetType == typeof(SM_FRIEND_LIST) || packetType == typeof(SM_BLOCK_LIST))
		{
			Assert.Empty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>(packetType == typeof(SM_FRIEND_LIST) ? "friends" : "blocks"));
			return;
		}
		if (packetType == typeof(SM_ENTER_WORLD_CHECK))
		{
			Assert.InRange(decoded.Get<byte>("msg"), (byte)0, (byte)6);
			return;
		}
		if (packetType == typeof(SM_INVENTORY_ADD_ITEM))
		{
			Assert.NotEmpty(decoded.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
			return;
		}
		if (packetType == typeof(SM_QUEST_ACTION))
		{
			Assert.True(decoded.Get<bool>("suppressed"));
			return;
		}
		Assert.Fail($"{packetType.Name} fixture did not exercise a decoded primitive field.");
	}

	private static JsonDocument LoadFixture(string fileName)
	{
		var path = Path.Combine(RepoRoot(), "parity-artifacts", "golden", "packets", fileName);
		Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}");
		return JsonDocument.Parse(File.ReadAllText(path));
	}

	private static string RepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "AionServer.slnx")))
				return directory.FullName;
			directory = directory.Parent;
		}
		throw new DirectoryNotFoundException("Could not find the repository root above " + AppContext.BaseDirectory);
	}
}
