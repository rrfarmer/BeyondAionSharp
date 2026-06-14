using System.Reflection;
using System.Text.Json;
using Aion.Commons.Nio;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 of the Port Fidelity &amp; Remediation Plan: the C# half of the golden pipeline.
///
/// Reads the SHARED packet fixtures produced by the Java harness
/// (game-server GoldenPacketFixtureGeneratorTest -> parity-artifacts/golden/packets/*.json)
/// and asserts the C# packet writers emit byte-for-byte identical payloads. The Java bytes
/// are the single source of truth; this is "Java as the oracle" with no live client.
///
/// To add a packet: capture it in the Java generator, then add a reconstruction case below.
/// </summary>
public sealed class GoldenPacketFixtureTests
{
	[Theory]
	// NOTE: SM_GROUP_DATA_EXCHANGE is NOT here — it is an AionServerPacket (no SerializeFrame),
	// so it is captured via the raw WriteImpl path in FaithfulCsharpPayloadMatchesJavaGoldenFixture below.
	[InlineData("SM_GF_WEBSHOP_TOKEN_RESPONSE.json")]
	[InlineData("SM_QUIT_RESPONSE.json")]
	[InlineData("SM_DELETE_ITEM.json")]
	[InlineData("SM_DELETE_WAREHOUSE_ITEM.json")]
	[InlineData("SM_DELETE_HOUSE_OBJECT.json")]
	[InlineData("SM_DELETE_HOUSE.json")]
	[InlineData("SM_RECIPE_DELETE.json")]
	[InlineData("SM_CRAFT_ANIMATION.json")]
	[InlineData("SM_BLOCK_RESPONSE.json")]
	[InlineData("SM_FRIEND_RESPONSE.json")]
	[InlineData("SM_CLOSE_QUESTION_WINDOW.json")]
	[InlineData("SM_DELETE_CHARACTER.json")]
	[InlineData("SM_RESTORE_CHARACTER.json")]
	[InlineData("SM_NICKNAME_CHECK_RESPONSE.json")]
	[InlineData("SM_LEARN_RECIPE.json")]
	[InlineData("SM_SUMMON_OWNER_REMOVE.json")]
	[InlineData("SM_SUMMON_PANEL_REMOVE.json")]
	[InlineData("SM_DP_INFO.json")]
	[InlineData("SM_FLY_TIME.json")]
	[InlineData("SM_STATUPDATE_DP.json")]
	[InlineData("SM_STATUPDATE_HP.json")]
	[InlineData("SM_STATUPDATE_MP.json")]
	[InlineData("SM_STATUPDATE_EXP.json")]
	[InlineData("SM_UNWRAP_ITEM.json")]
	[InlineData("SM_WINDSTREAM.json")]
	[InlineData("SM_FRIEND_NOTIFY.json")]
	[InlineData("SM_BIND_POINT_TELEPORT.json")]
	public void CsharpPayloadMatchesJavaGoldenFixture(string fixtureFile)
	{
		var fixture = LoadFixture(fixtureFile);
		var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

		foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var caseName = caseElement.GetProperty("name").GetString()!;
			var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
			var inputs = caseElement.GetProperty("inputs");

			var packet = Reconstruct(packetName, inputs);
			var actual = SerializeUnencryptedPayload(packet);
			var actualHex = Convert.ToHexString(actual);

			Assert.True(expectedHex == actualHex,
				$"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
				$"  Java : {expectedHex}\n  C#   : {actualHex}");
		}
	}

	private static GameServerPacket Reconstruct(string packetName, JsonElement inputs) => packetName switch
	{
		"SM_GF_WEBSHOP_TOKEN_RESPONSE" => new SmGfWebshopTokenResponse(inputs.GetProperty("token").GetString()!),
		"SM_QUIT_RESPONSE" => new SmQuitResponse(inputs.GetProperty("editMode").GetBoolean()),
		"SM_DELETE_ITEM" => new SmDeleteItem(inputs.GetProperty("itemObjectId").GetInt32(), inputs.GetProperty("deleteType").GetInt32()),
		"SM_DELETE_WAREHOUSE_ITEM" => new SmDeleteWarehouseItem(inputs.GetProperty("warehouseType").GetInt32(), inputs.GetProperty("itemObjectId").GetInt32(), inputs.GetProperty("deleteType").GetInt32()),
		"SM_DELETE_HOUSE_OBJECT" => new SmDeleteHouseObject(inputs.GetProperty("itemObjectId").GetInt32()),
		"SM_DELETE_HOUSE" => new SmDeleteHouse(inputs.GetProperty("addressId").GetInt32()),
		"SM_RECIPE_DELETE" => new SmRecipeDelete(inputs.GetProperty("recipeId").GetInt32()),
		"SM_CRAFT_ANIMATION" => new SmCraftAnimation(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("targetObjectId").GetInt32(), inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("action").GetInt32()),
		"SM_BLOCK_RESPONSE" => new SmBlockResponse((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("playerName").GetString()!),
		"SM_FRIEND_RESPONSE" => new SmFriendResponse((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("playerName").GetString()!),
		"SM_CLOSE_QUESTION_WINDOW" => ReconstructCloseQuestionWindow(inputs),
		"SM_DELETE_CHARACTER" => new SmDeleteCharacter(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("deletionTime").GetInt32()),
		"SM_RESTORE_CHARACTER" => new SmRestoreCharacter(inputs.GetProperty("chaOid").GetInt32(), inputs.GetProperty("success").GetBoolean()),
		"SM_NICKNAME_CHECK_RESPONSE" => new SmNicknameCheckResponse(inputs.GetProperty("value").GetInt32()),
		"SM_LEARN_RECIPE" => new SmLearnRecipe(inputs.GetProperty("recipeId").GetInt32()),
		"SM_SUMMON_OWNER_REMOVE" => new SmSummonOwnerRemove(inputs.GetProperty("summonObjId").GetInt32()),
		"SM_SUMMON_PANEL_REMOVE" => new SmSummonPanelRemove(inputs.GetProperty("skillId").GetInt32()),
		"SM_DP_INFO" => new SmDpInfo(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("currentDp").GetInt32()),
		"SM_FLY_TIME" => new SmFlyTime(inputs.GetProperty("currentFp").GetInt32(), inputs.GetProperty("maxFp").GetInt32()),
		"SM_STATUPDATE_DP" => new SmStatUpdateDp(inputs.GetProperty("currentDp").GetInt32()),
		"SM_STATUPDATE_HP" => new SmStatUpdateHp(inputs.GetProperty("currentHp").GetInt32(), inputs.GetProperty("maxHp").GetInt32()),
		"SM_STATUPDATE_MP" => new SmStatUpdateMp(inputs.GetProperty("currentMp").GetInt32(), inputs.GetProperty("maxMp").GetInt32()),
		"SM_STATUPDATE_EXP" => new SmStatUpdateExp(inputs.GetProperty("currentExp").GetInt64(), inputs.GetProperty("recoverableExp").GetInt64(), inputs.GetProperty("maxExp").GetInt64(), inputs.GetProperty("rep1").GetInt64(), inputs.GetProperty("rep2").GetInt64()),
		"SM_UNWRAP_ITEM" => new SmUnwrapItem(inputs.GetProperty("objectId").GetInt32(), inputs.GetProperty("count").GetInt32()),
		"SM_WINDSTREAM" => new SmWindstream(inputs.GetProperty("unk1").GetInt32(), inputs.GetProperty("unk2").GetInt32()),
		"SM_FRIEND_NOTIFY" => new SmFriendNotify((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("name").GetString()!),
		"SM_BIND_POINT_TELEPORT" => new SmBindPointTeleport((byte)inputs.GetProperty("action").GetInt32(), inputs.GetProperty("playerId").GetInt32(), inputs.GetProperty("locId").GetInt32(), inputs.GetProperty("cooldown").GetInt32()),
		_ => throw new NotSupportedException($"No C# reconstruction registered for {packetName}"),
	};

	// ----- New batch: faithful SM_* packets (AionServerPacket-derived). -----
	// These extend AionServerPacket, not GameServerPacket, so they have no SerializeFrame.
	// We capture the raw writeImpl payload exactly like the Java harness does: a LITTLE_ENDIAN
	// ByteBuffer, invoke WriteImpl reflectively, read Position() bytes. No opcode, no crypt.
	[Theory]
	[InlineData("SM_GROUP_DATA_EXCHANGE.json")]
	[InlineData("SM_RECONNECT_KEY.json")]
	[InlineData("SM_PLAYER_STATE.json")]
	[InlineData("SM_TARGET_SELECTED_LIVE.json")]
	[InlineData("SM_EMOTION.json")]
	[InlineData("SM_GATHER_ANIMATION.json")]
	[InlineData("SM_SHOW_BRAND.json")]
	[InlineData("SM_CUBE_UPDATE.json")]
	[InlineData("SM_TELEPORT_MAP.json")]
	[InlineData("SM_LOOT_STATUS.json")]
	[InlineData("SM_TARGET_SELECTED.json")]
	[InlineData("SM_RIFT_ANNOUNCE.json")]
	[InlineData("SM_RECIPE_LIST.json")]
	public void FaithfulCsharpPayloadMatchesJavaGoldenFixture(string fixtureFile)
	{
		var fixture = LoadFixture(fixtureFile);
		var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

		foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var caseName = caseElement.GetProperty("name").GetString()!;
			var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
			var inputs = caseElement.GetProperty("inputs");

			var packet = ReconstructFaithful(packetName, inputs);
			var actual = CaptureWriteImplPayload(packet);
			var actualHex = Convert.ToHexString(actual);

			Assert.True(expectedHex == actualHex,
				$"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
				$"  Java : {expectedHex}\n  C#   : {actualHex}");
		}
	}

	private static AionServerPacket ReconstructFaithful(string packetName, JsonElement inputs) => packetName switch
	{
		"SM_GROUP_DATA_EXCHANGE" => ReconstructGroupDataExchange(inputs),
		"SM_PLAYER_STATE" => new SM_PLAYER_STATE(BuildHarnessCreatureForState(inputs)),
		"SM_TARGET_SELECTED_LIVE" => new SM_TARGET_SELECTED(BuildHarnessCreatureForTarget(inputs)),
		"SM_EMOTION" => ReconstructEmotion(inputs),
		"SM_RECONNECT_KEY" => new SM_RECONNECT_KEY(inputs.GetProperty("key").GetInt32()),
		"SM_GATHER_ANIMATION" => new SM_GATHER_ANIMATION(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("gatherableObjId").GetInt32(), inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("action").GetInt32()),
		"SM_SHOW_BRAND" => new SM_SHOW_BRAND(inputs.GetProperty("iconId").GetInt32(), inputs.GetProperty("targetObjectId").GetInt32()),
		"SM_CUBE_UPDATE" => SM_CUBE_UPDATE.StigmaSlots(inputs.GetProperty("actionValue").GetInt32()),
		"SM_TELEPORT_MAP" => new SM_TELEPORT_MAP(inputs.GetProperty("targetObjId").GetInt32(), inputs.GetProperty("teleportId").GetInt32()),
		"SM_LOOT_STATUS" => new SM_LOOT_STATUS(inputs.GetProperty("targetObjectId").GetInt32(), (SM_LOOT_STATUS.Status)inputs.GetProperty("status").GetInt32()),
		"SM_TARGET_SELECTED" => new SM_TARGET_SELECTED(null!),
		"SM_RIFT_ANNOUNCE" => ReconstructRiftAnnounce(inputs),
		"SM_RECIPE_LIST" => new SM_RECIPE_LIST(new HashSet<int>(inputs.GetProperty("recipeIds").EnumerateArray().Select(e => e.GetInt32()))),
		_ => throw new NotSupportedException($"No faithful C# reconstruction registered for {packetName}"),
	};

	private static SM_RIFT_ANNOUNCE ReconstructRiftAnnounce(JsonElement inputs)
	{
		var actionId = inputs.GetProperty("actionId").GetInt32();
		return actionId switch
		{
			1 => new SM_RIFT_ANNOUNCE(inputs.GetProperty("gelkmaros").GetBoolean(), inputs.GetProperty("inggison").GetBoolean()),
			4 => new SM_RIFT_ANNOUNCE(inputs.GetProperty("objectId").GetInt32()),
			_ => throw new NotSupportedException($"No SM_RIFT_ANNOUNCE faithful ctor for actionId {actionId}"),
		};
	}

	/// <summary>
	/// Capture an AionServerPacket's raw writeImpl payload — the same bytes the Java harness
	/// captures (LITTLE_ENDIAN buffer, writeImpl only, no opcode/crypt frame).
	/// </summary>
	private static byte[] CaptureWriteImplPayload(AionServerPacket packet)
	{
		var buffer = ByteBuffer.Allocate(8192).Order(ByteOrder.LITTLE_ENDIAN);
		packet.SetBuf(buffer);
		var writeImpl = typeof(AionServerPacket).GetMethod("WriteImpl",
			BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(AionConnection) })!;
		writeImpl.Invoke(packet, new object?[] { null });
		var length = buffer.Position();
		var payload = new byte[length];
		buffer.Flip();
		buffer.Get(payload);
		return payload;
	}

	private static SM_GROUP_DATA_EXCHANGE ReconstructGroupDataExchange(JsonElement inputs)
	{
		var byteData = inputs.GetProperty("byteData").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray();
		return inputs.GetProperty("ctor").GetString() switch
		{
			"byteData" => new SM_GROUP_DATA_EXCHANGE(byteData),
			"byteData_action_unk2" => new SM_GROUP_DATA_EXCHANGE(byteData, inputs.GetProperty("action").GetInt32(), inputs.GetProperty("unk2").GetInt32()),
			_ => throw new NotSupportedException("Unknown SM_GROUP_DATA_EXCHANGE ctor"),
		};
	}

	// ----- Live-object packet reconstruction (deterministic HarnessCreature mirrors the Java harness) -----

	private static PacketHarnessCreature BuildHarnessCreatureForState(JsonElement inputs)
	{
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>());
		var visualState = inputs.GetProperty("visualState").GetInt32();
		var seeState = inputs.GetProperty("seeState").GetInt32();
		if (visualState != 0)
			c.SetVisualState((CreatureVisualState)visualState); // OR onto VISIBLE(0) default == the raw id
		if (seeState != 0)
			c.SetSeeState((CreatureSeeState)seeState);
		return c;
	}

	private static PacketHarnessCreature BuildHarnessCreatureForTarget(JsonElement inputs)
	{
		var stats = new Dictionary<StatEnum, int>
		{
			[StatEnum.MAXHP] = inputs.GetProperty("maxHp").GetInt32(),
			[StatEnum.MAXMP] = inputs.GetProperty("maxMp").GetInt32(),
		};
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(),
			(sbyte)inputs.GetProperty("level").GetInt32(), stats);
		c.SetLifeStats(new PacketHarnessLifeStats(c, inputs.GetProperty("currentHp").GetInt32(), inputs.GetProperty("currentMp").GetInt32()));
		return c;
	}

	private static SM_EMOTION ReconstructEmotion(JsonElement inputs)
	{
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>());
		var type = Enum.Parse<EmotionType>(inputs.GetProperty("type").GetString()!);
		if (inputs.TryGetProperty("emotion", out var emotion) && inputs.TryGetProperty("targetObjectId", out var target))
			return new SM_EMOTION(c, type, emotion.GetInt32(), target.GetInt32());
		return new SM_EMOTION(c, type);
	}

	private static SmCloseQuestionWindow ReconstructCloseQuestionWindow(JsonElement inputs)
	{
		var messageId = inputs.GetProperty("messageId").GetInt32();
		var parameters = inputs.GetProperty("params").EnumerateArray().Select(p => p.GetString()!).ToArray();
		return messageId switch
		{
			0 => SmCloseQuestionWindow.CloseQuestionWindow(),
			1300134 => SmCloseQuestionWindow.DuelRequesterWithdrawRequest(parameters[0]),
			1300097 => SmCloseQuestionWindow.DuelHeRejectDuel(parameters[0]),
			_ => throw new NotSupportedException($"No SM_CLOSE_QUESTION_WINDOW factory for messageId {messageId}"),
		};
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		// Mirrors existing SmGroupDataExchangeTests: deterministic key, strip the 7-byte frame header.
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		return packet.SerializeFrame(crypt)[7..];
	}

	private static JsonDocument LoadFixture(string fileName)
	{
		var path = Path.Combine(FixtureRoot(), fileName);
		Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
			"Regenerate with: mvn -pl game-server -am test -Dtest=GoldenPacketFixtureGeneratorTest " +
			"-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false");
		return JsonDocument.Parse(File.ReadAllText(path));
	}

	private static string FixtureRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var candidate = Path.Combine(dir.FullName, "parity-artifacts", "golden", "packets");
			if (Directory.Exists(candidate))
				return candidate;
			dir = dir.Parent;
		}
		throw new DirectoryNotFoundException(
			"Could not locate parity-artifacts/golden/packets above " + AppContext.BaseDirectory);
	}

	// ----- Deterministic harness for live-object packets (mirrors the Java GoldenLivePacketFixtureGeneratorTest) -----

	/// <summary>
	/// Minimal deterministic Creature: fixed objectId/level, harness game-stats + life-stats, no spawn/world.
	/// The only state packet writeImpls read here is set via the base ctor / setters.
	/// </summary>
	internal sealed class PacketHarnessCreature : Creature
	{
		private readonly sbyte _level;
		private readonly CreatureGameStats _gs;

		public PacketHarnessCreature(int objectId, sbyte level, Dictionary<StatEnum, int> statMap)
			: base(objectId, null!, null!, new NpcTemplate(), null!, false)
		{
			_level = level;
			_gs = new PacketHarnessStats(this, statMap);
		}

		public override sbyte GetLevel() => _level;

		public override Race GetRace() => Race.NPC;

		public override CreatureGameStats GetGameStats() => _gs;
	}

	internal sealed class PacketHarnessStats : CreatureGameStats<Creature>
	{
		private readonly Dictionary<StatEnum, int> _statMap;

		public PacketHarnessStats(Creature owner, Dictionary<StatEnum, int> statMap) : base(owner)
		{
			_statMap = statMap;
		}

		public override Stat2 GetStat(StatEnum statEnum, float baseValue, params CalculationType[] calculationTypes)
		{
			float resolved = _statMap.TryGetValue(statEnum, out int v) ? v : baseValue;
			return new AdditionStat(statEnum, resolved, owner);
		}

		public override StatsTemplate GetStatsTemplate() => new StatsTemplate();
		public override Stat2 GetAttackSpeed() => new AdditionStat(StatEnum.ATTACK_SPEED, 1000, owner);
		public override Stat2 GetMovementSpeed() => new AdditionStat(StatEnum.SPEED, 6000, owner);
		public override Stat2 GetAttackRange() => new AdditionStat(StatEnum.ATTACK_RANGE, 1500, owner);
		public override Stat2 GetHpRegenRate() => new AdditionStat(StatEnum.REGEN_HP, 1, owner);
		public override Stat2 GetMpRegenRate() => new AdditionStat(StatEnum.REGEN_MP, 1, owner);
	}

	// Fixed currentHp/currentMp; maxHp/maxMp resolve through the harness game-stats (StatEnum.MAXHP/MAXMP).
	internal sealed class PacketHarnessLifeStats : CreatureLifeStats<Creature>
	{
		public PacketHarnessLifeStats(Creature owner, int currentHp, int currentMp) : base(owner, currentHp, currentMp)
		{
		}
	}
}
