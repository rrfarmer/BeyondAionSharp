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
	// ----- New batch: faithful SM_* packets (AionServerPacket-derived). -----
	// These extend AionServerPacket, not GameServerPacket, so they have no SerializeFrame.
	// We capture the raw writeImpl payload exactly like the Java harness does: a LITTLE_ENDIAN
	// ByteBuffer, invoke WriteImpl reflectively, read Position() bytes. No opcode, no crypt.
	[Theory]
	[InlineData("SM_ATTACK_STATUS.json")]
	[InlineData("SM_ITEM_USAGE_ANIMATION.json")]
	[InlineData("SM_QUIT_RESPONSE.json")]
	[InlineData("SM_DELETE_WAREHOUSE_ITEM.json")]
	[InlineData("SM_BLOCK_RESPONSE.json")]
	[InlineData("SM_FRIEND_RESPONSE.json")]
	[InlineData("SM_CLOSE_QUESTION_WINDOW.json")]
	[InlineData("SM_STATUPDATE_DP.json")]
	[InlineData("SM_FRIEND_NOTIFY.json")]
	[InlineData("SM_BIND_POINT_TELEPORT.json")]
	[InlineData("SM_SKILL_CANCEL.json")]
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
	[InlineData("SM_PET.json")]
	[InlineData("SM_SKILL_ACTIVATION.json")]
	[InlineData("SM_CASTSPELL.json")]
	[InlineData("SM_ATTACK_RESPONSE.json")]
	[InlineData("SM_ABNORMAL_STATE.json")]
	[InlineData("SM_ABNORMAL_EFFECT.json")]
	[InlineData("SM_USE_OBJECT.json")]
	[InlineData("SM_PONG.json")]
	[InlineData("SM_PING_RESPONSE.json")]
	[InlineData("SM_POSITION_SELF.json")]
	[InlineData("SM_SUMMON_USESKILL.json")]
	[InlineData("SM_ICON_INFO.json")]
	[InlineData("SM_ASCENSION_MORPH.json")]
	[InlineData("SM_QUEST_REPEAT.json")]
	[InlineData("SM_SECURITY_TOKEN.json")]
	[InlineData("SM_MOTION_SCALAR.json")]
	[InlineData("SM_MACRO_RESULT.json")]
	[InlineData("SM_DUEL.json")]
	[InlineData("SM_SIEGE_LOCATION_STATE.json")]
	[InlineData("SM_MAY_LOGIN_INTO_GAME.json")]
	[InlineData("SM_QUESTION_WINDOW.json")]
	[InlineData("SM_INSTANCE_STAGE_INFO.json")]
	[InlineData("SM_FORTRESS_INFO.json")]
	[InlineData("SM_LEAVE_GROUP_MEMBER.json")]
	[InlineData("SM_SHIELD_EFFECT.json")]
	[InlineData("SM_TOLL_INFO.json")]
	[InlineData("SM_INSTANCE_COUNT_INFO.json")]
	[InlineData("SM_STATS_STATUS_UNK.json")]
	[InlineData("SM_PACKAGE_INFO_NOTIFY.json")]
	[InlineData("SM_ACTION_ANIMATION.json")]
	[InlineData("SM_GAMEGUARD.json")]
	[InlineData("SM_CAPTCHA.json")]
	[InlineData("SM_MEGAPHONE.json")]
	[InlineData("SM_QUESTIONNAIRE.json")]
	[InlineData("SM_FORCED_MOVE.json")]
	[InlineData("SM_WEATHER.json")]
	[InlineData("SM_GROUP_LOOT.json")]
	[InlineData("SM_WINDSTREAM_ANNOUNCE.json")]
	[InlineData("SM_MANTRA_EFFECT.json")]
	[InlineData("SM_DELETE.json")]
	[InlineData("SM_SKILL_REMOVE.json")]
	[InlineData("SM_HEADING_UPDATE.json")]
	[InlineData("SM_POSITION.json")]
	[InlineData("SM_LOOKATOBJECT.json")]
	[InlineData("SM_RESURRECT.json")]
	[InlineData("SM_TRANSFORM.json")]
	[InlineData("SM_CRAFT_UPDATE.json")]
	[InlineData("SM_CONQUEROR_PROTECTOR.json")]
	[InlineData("SM_LEGION_EDIT.json")]
	[InlineData("SM_UPGRADE_ARCADE.json")]
	[InlineData("SM_GM_BOOKMARK_ADD.json")]
	[InlineData("SM_ALLIANCE_READY_CHECK.json")]
	[InlineData("SM_BIND_POINT_INFO.json")]
	[InlineData("SM_CHAT_INIT.json")]
	[InlineData("SM_RECEIVE_BIDS.json")]
	[InlineData("SM_CUSTOM_SETTINGS.json")]
	[InlineData("SM_ATTACK.json")]
	[InlineData("SM_FLY_TIME.json")]
	[InlineData("SM_WINDSTREAM.json")]
	[InlineData("SM_UNWRAP_ITEM.json")]
	[InlineData("SM_CRAFT_ANIMATION.json")]
	[InlineData("SM_GF_WEBSHOP_TOKEN_RESPONSE.json")]
	[InlineData("SM_DELETE_HOUSE.json")]
	[InlineData("SM_DELETE_HOUSE_OBJECT.json")]
	[InlineData("SM_DELETE_CHARACTER.json")]
	[InlineData("SM_RESTORE_CHARACTER.json")]
	[InlineData("SM_NICKNAME_CHECK_RESPONSE.json")]
	[InlineData("SM_STATUPDATE_HP.json")]
	[InlineData("SM_STATUPDATE_MP.json")]
	[InlineData("SM_DELETE_ITEM.json")]
	[InlineData("SM_RECIPE_DELETE.json")]
	[InlineData("SM_LEARN_RECIPE.json")]
	[InlineData("SM_SUMMON_OWNER_REMOVE.json")]
	[InlineData("SM_SUMMON_PANEL_REMOVE.json")]
	[InlineData("SM_DP_INFO.json")]
	[InlineData("SM_STATUPDATE_EXP.json")]
	// ----- Batch 10: pure scalar / simple-DTO con-null-safe SM_* packets -----
	[InlineData("SM_CHARACTER_SELECT.json")]
	[InlineData("SM_AFTER_SIEGE_LOCINFO_475.json")]
	[InlineData("SM_NEARBY_QUESTS.json")]
	[InlineData("SM_MACRO_LIST.json")]
	[InlineData("SM_FIRST_SHOW_DECOMPOSABLE.json")]
	[InlineData("SM_SECONDARY_SHOW_DECOMPOSABLE.json")]
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
		"SM_ATTACK_STATUS" => ReconstructAttackStatus(inputs),
		"SM_ITEM_USAGE_ANIMATION" => ReconstructItemUsageAnimation(inputs),
		"SM_QUIT_RESPONSE" => new SM_QUIT_RESPONSE(inputs.GetProperty("editMode").GetBoolean()),
		"SM_DELETE_WAREHOUSE_ITEM" => new SM_DELETE_WAREHOUSE_ITEM(inputs.GetProperty("warehouseType").GetInt32(), inputs.GetProperty("itemObjectId").GetInt32(), ResolveItemDeleteType(inputs.GetProperty("deleteType").GetInt32())),
		"SM_BLOCK_RESPONSE" => new SM_BLOCK_RESPONSE(inputs.GetProperty("code").GetInt32(), inputs.GetProperty("playerName").GetString()!),
		"SM_FRIEND_RESPONSE" => new SM_FRIEND_RESPONSE(inputs.GetProperty("playerName").GetString()!, inputs.GetProperty("code").GetInt32()),
		"SM_CLOSE_QUESTION_WINDOW" => ReconstructCloseQuestionWindow(inputs),
		"SM_STATUPDATE_DP" => new SM_STATUPDATE_DP(inputs.GetProperty("currentDp").GetInt32()),
		"SM_FRIEND_NOTIFY" => new SM_FRIEND_NOTIFY((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("name").GetString()!),
		"SM_BIND_POINT_TELEPORT" => new SM_BIND_POINT_TELEPORT(inputs.GetProperty("action").GetInt32(), inputs.GetProperty("playerId").GetInt32(), inputs.GetProperty("locId").GetInt32(), inputs.GetProperty("cooldown").GetInt32()),
		"SM_SKILL_CANCEL" => new SM_SKILL_CANCEL(new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>()), inputs.GetProperty("skillId").GetInt32()),
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
		"SM_PET" => ReconstructPet(inputs),
		"SM_SKILL_ACTIVATION" => ReconstructSkillActivation(inputs),
		"SM_CASTSPELL" => ReconstructCastSpell(inputs),
		"SM_ATTACK_RESPONSE" => ReconstructAttackResponse(inputs),
		"SM_ABNORMAL_STATE" => new SM_ABNORMAL_STATE(new List<Aion.GameServer.SkillEngine.Model.Effect>(), inputs.GetProperty("abnormals").GetInt32(), inputs.GetProperty("slot").GetInt32()),
		"SM_ABNORMAL_EFFECT" => new SM_ABNORMAL_EFFECT(new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>()), inputs.GetProperty("abnormals").GetInt32(), new List<Aion.GameServer.SkillEngine.Model.Effect>(), inputs.GetProperty("slots").GetInt32()),
		"SM_USE_OBJECT" => new SM_USE_OBJECT(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("targetObjId").GetInt32(), inputs.GetProperty("time").GetInt32(), inputs.GetProperty("actionType").GetInt32()),
		"SM_PONG" => new SM_PONG(),
		"SM_PING_RESPONSE" => new SM_PING_RESPONSE(),
		"SM_POSITION_SELF" => new SM_POSITION_SELF(inputs.GetProperty("x").GetSingle(), inputs.GetProperty("y").GetSingle(), inputs.GetProperty("z").GetSingle(), (byte)inputs.GetProperty("heading").GetInt32()),
		"SM_SUMMON_USESKILL" => new SM_SUMMON_USESKILL(inputs.GetProperty("summonId").GetInt32(), inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("skillLvl").GetInt32(), inputs.GetProperty("targetId").GetInt32()),
		"SM_ICON_INFO" => new SM_ICON_INFO(inputs.GetProperty("buffId").GetInt32(), inputs.GetProperty("display").GetBoolean()),
		"SM_ASCENSION_MORPH" => new SM_ASCENSION_MORPH(inputs.GetProperty("inascension").GetInt32()),
		"SM_QUEST_REPEAT" => new SM_QUEST_REPEAT(inputs.GetProperty("repeatableQuests").EnumerateArray().Select(e => e.GetInt32()).ToList()),
		"SM_SECURITY_TOKEN" => new SM_SECURITY_TOKEN(inputs.GetProperty("token").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray()),
		"SM_MOTION" => ReconstructMotionScalar(inputs),
		"SM_MACRO_RESULT" => inputs.GetProperty("code").GetInt32() == 0 ? SM_MACRO_RESULT.SM_MACRO_CREATED : SM_MACRO_RESULT.SM_MACRO_DELETED,
		"SM_DUEL" => ReconstructDuel(inputs),
		"SM_SIEGE_LOCATION_STATE" => new SM_SIEGE_LOCATION_STATE(inputs.GetProperty("locationId").GetInt32(), inputs.GetProperty("state").GetInt32()),
		"SM_MAY_LOGIN_INTO_GAME" => new SM_MAY_LOGIN_INTO_GAME(),
		"SM_QUESTION_WINDOW" => new SM_QUESTION_WINDOW(inputs.GetProperty("code").GetInt32(), inputs.GetProperty("senderId").GetInt32(), inputs.GetProperty("rangeOrCooldownSeconds").GetInt32(), inputs.GetProperty("params").EnumerateArray().Select(e => (object)e.GetString()!).ToArray()),
		"SM_INSTANCE_STAGE_INFO" => new SM_INSTANCE_STAGE_INFO(inputs.GetProperty("type").GetInt32(), inputs.GetProperty("event").GetInt32(), inputs.GetProperty("unk").GetInt32()),
		"SM_FORTRESS_INFO" => new SM_FORTRESS_INFO(inputs.GetProperty("locationId").GetInt32(), inputs.GetProperty("teleportStatus").GetBoolean()),
		"SM_LEAVE_GROUP_MEMBER" => new SM_LEAVE_GROUP_MEMBER(),
		"SM_SHIELD_EFFECT" => new SM_SHIELD_EFFECT(new List<Aion.GameServer.Model.Siege.SiegeLocation>()),
		"SM_TOLL_INFO" => new SM_TOLL_INFO(inputs.GetProperty("tollCount").GetInt64()),
		"SM_INSTANCE_COUNT_INFO" => new SM_INSTANCE_COUNT_INFO(inputs.GetProperty("mapId").GetInt32(), inputs.GetProperty("instanceId").GetInt32()),
		"SM_STATS_STATUS_UNK" => new SM_STATS_STATUS_UNK(inputs.GetProperty("lvl").GetInt32(), inputs.GetProperty("points").GetInt32()),
		"SM_PACKAGE_INFO_NOTIFY" => new SM_PACKAGE_INFO_NOTIFY(),
		"SM_ACTION_ANIMATION" => new SM_ACTION_ANIMATION(inputs.GetProperty("targetObjectId").GetInt32(), Enum.Parse<Aion.GameServer.Model.Animations.ActionAnimation>(inputs.GetProperty("actionAnimation").GetString()!), inputs.GetProperty("levelOrObjectId").GetInt32()),
		"SM_GAMEGUARD" => new SM_GAMEGUARD(inputs.GetProperty("size").GetInt32()),
		"SM_CAPTCHA" => ReconstructCaptcha(inputs),
		"SM_MEGAPHONE" => new SM_MEGAPHONE(ResolveFactionLabel(inputs.GetProperty("faction").GetString()!), inputs.GetProperty("senderName").GetString()!, inputs.GetProperty("message").GetString()!, inputs.GetProperty("itemId").GetInt32()),
		"SM_QUESTIONNAIRE" => new SM_QUESTIONNAIRE(inputs.GetProperty("messageId").GetInt32(), (byte)inputs.GetProperty("chunk").GetInt32(), (byte)inputs.GetProperty("count").GetInt32(), inputs.GetProperty("html").GetString()!),
		"SM_FORCED_MOVE" => new SM_FORCED_MOVE(new PacketHarnessCreature(inputs.GetProperty("creatureObjectId").GetInt32(), 1, new Dictionary<StatEnum, int>()), inputs.GetProperty("objectId").GetInt32(), inputs.GetProperty("x").GetSingle(), inputs.GetProperty("y").GetSingle(), inputs.GetProperty("z").GetSingle()),
		"SM_WEATHER" => new SM_WEATHER(inputs.GetProperty("codes").EnumerateArray().Select(e => new Aion.GameServer.Model.Templates.World.WeatherEntry(0, e.GetInt32())).ToArray()),
		"SM_GROUP_LOOT" => new SM_GROUP_LOOT(inputs.GetProperty("groupId").GetInt32(), inputs.GetProperty("playerId").GetInt32(), inputs.GetProperty("itemId").GetInt32(), inputs.GetProperty("itemCount").GetInt32(), inputs.GetProperty("lootCorpseId").GetInt32(), inputs.GetProperty("distributionId").GetInt32(), inputs.GetProperty("luck").GetInt64(), inputs.GetProperty("index").GetInt32()),
		"SM_WINDSTREAM_ANNOUNCE" => new SM_WINDSTREAM_ANNOUNCE(inputs.GetProperty("bidirectional").GetInt32(), inputs.GetProperty("mapId").GetInt32(), inputs.GetProperty("streamId").GetInt32(), inputs.GetProperty("state").GetInt32()),
		"SM_MANTRA_EFFECT" => new SM_MANTRA_EFFECT(new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>()), inputs.GetProperty("subEffectId").GetInt32()),
		"SM_DELETE" => ReconstructDelete(inputs),
		"SM_SKILL_REMOVE" => new SM_SKILL_REMOVE(new Aion.GameServer.Model.Skill.PlayerSkillEntry(inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("skillLvl").GetInt32(), inputs.GetProperty("skillType").GetInt32(), Aion.GameServer.Model.GameObjects.IPersistable.PersistentState.NOACTION)),
		"SM_HEADING_UPDATE" => new SM_HEADING_UPDATE(PositionedHarness(inputs.GetProperty("objectId").GetInt32())),
		"SM_POSITION" => new SM_POSITION(PositionedHarness(inputs.GetProperty("objectId").GetInt32())),
		"SM_LOOKATOBJECT" => new SM_LOOKATOBJECT(PositionedHarness(inputs.GetProperty("objectId").GetInt32())),
		"SM_RESURRECT" => new SM_RESURRECT(new PacketHarnessCreature(700001, 1, new Dictionary<StatEnum, int>()), inputs.GetProperty("skillId").GetInt32()),
		"SM_TRANSFORM" => ReconstructTransform(inputs),
		"SM_CRAFT_UPDATE" => new SM_CRAFT_UPDATE(inputs.GetProperty("skillId").GetInt32(), new Aion.GameServer.Model.Templates.Items.ItemTemplate(), inputs.GetProperty("success").GetInt32(), inputs.GetProperty("failure").GetInt32(), inputs.GetProperty("action").GetInt32(), inputs.GetProperty("executionSpeed").GetInt32(), inputs.GetProperty("delay").GetInt32()),
		"SM_CONQUEROR_PROTECTOR" => new SM_CONQUEROR_PROTECTOR(inputs.GetProperty("type").GetInt32(), inputs.GetProperty("buffLvl").GetInt32(), inputs.GetProperty("cooldown").GetInt32()),
		"SM_LEGION_EDIT" => ReconstructLegionEdit(inputs),
		"SM_UPGRADE_ARCADE" => ReconstructUpgradeArcade(inputs),
		"SM_GM_BOOKMARK_ADD" => new SM_GM_BOOKMARK_ADD(inputs.GetProperty("name").GetString()!, inputs.GetProperty("worldId").GetInt32(), inputs.GetProperty("x").GetSingle(), inputs.GetProperty("y").GetSingle(), inputs.GetProperty("z").GetSingle()),
		"SM_ALLIANCE_READY_CHECK" => new SM_ALLIANCE_READY_CHECK(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("statusCode").GetInt32()),
		"SM_BIND_POINT_INFO" => new SM_BIND_POINT_INFO(inputs.GetProperty("mapId").GetInt32(), inputs.GetProperty("x").GetSingle(), inputs.GetProperty("y").GetSingle(), inputs.GetProperty("z").GetSingle()),
		"SM_CHAT_INIT" => new SM_CHAT_INIT(inputs.GetProperty("token").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray()),
		"SM_RECEIVE_BIDS" => new SM_RECEIVE_BIDS(inputs.GetProperty("unk").GetInt32()),
		"SM_CUSTOM_SETTINGS" => new SM_CUSTOM_SETTINGS(inputs.GetProperty("objectId").GetInt32(), inputs.GetProperty("unk").GetInt32(), inputs.GetProperty("display").GetInt32(), inputs.GetProperty("deny").GetInt32()),
		"SM_ATTACK" => ReconstructAttack(inputs),
		"SM_FLY_TIME" => new SM_FLY_TIME(inputs.GetProperty("currentFp").GetInt32(), inputs.GetProperty("maxFp").GetInt32()),
		"SM_WINDSTREAM" => new SM_WINDSTREAM(inputs.GetProperty("unk1").GetInt32(), inputs.GetProperty("unk2").GetInt32()),
		"SM_UNWRAP_ITEM" => new SM_UNWRAP_ITEM(inputs.GetProperty("objectId").GetInt32(), inputs.GetProperty("count").GetInt32()),
		"SM_CRAFT_ANIMATION" => new SM_CRAFT_ANIMATION(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("targetObjectId").GetInt32(), inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("action").GetInt32()),
		"SM_GF_WEBSHOP_TOKEN_RESPONSE" => new SM_GF_WEBSHOP_TOKEN_RESPONSE(inputs.GetProperty("token").GetString()!),
		"SM_DELETE_HOUSE" => new SM_DELETE_HOUSE(inputs.GetProperty("addressId").GetInt32()),
		"SM_DELETE_HOUSE_OBJECT" => new SM_DELETE_HOUSE_OBJECT(inputs.GetProperty("itemObjectId").GetInt32()),
		"SM_DELETE_CHARACTER" => new SM_DELETE_CHARACTER(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("deletionTime").GetInt32()),
		"SM_RESTORE_CHARACTER" => new SM_RESTORE_CHARACTER(inputs.GetProperty("chaOid").GetInt32(), inputs.GetProperty("success").GetBoolean()),
		"SM_NICKNAME_CHECK_RESPONSE" => new SM_NICKNAME_CHECK_RESPONSE(inputs.GetProperty("value").GetInt32()),
		"SM_STATUPDATE_HP" => new SM_STATUPDATE_HP(inputs.GetProperty("currentHp").GetInt32(), inputs.GetProperty("maxHp").GetInt32()),
		"SM_STATUPDATE_MP" => new SM_STATUPDATE_MP(inputs.GetProperty("currentMp").GetInt32(), inputs.GetProperty("maxMp").GetInt32()),
		"SM_DELETE_ITEM" => new SM_DELETE_ITEM(inputs.GetProperty("itemObjectId").GetInt32(), ResolveItemDeleteType(inputs.GetProperty("deleteType").GetInt32())),
		"SM_RECIPE_DELETE" => new SM_RECIPE_DELETE(inputs.GetProperty("recipeId").GetInt32()),
		"SM_LEARN_RECIPE" => new SM_LEARN_RECIPE(inputs.GetProperty("recipeId").GetInt32()),
		"SM_SUMMON_OWNER_REMOVE" => new SM_SUMMON_OWNER_REMOVE(inputs.GetProperty("summonObjId").GetInt32()),
		"SM_SUMMON_PANEL_REMOVE" => new SM_SUMMON_PANEL_REMOVE(inputs.GetProperty("skillId").GetInt32()),
		"SM_DP_INFO" => new SM_DP_INFO(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("currentDp").GetInt32()),
		"SM_STATUPDATE_EXP" => new SM_STATUPDATE_EXP(inputs.GetProperty("currentExp").GetInt64(), inputs.GetProperty("recoverableExp").GetInt64(), inputs.GetProperty("maxExp").GetInt64(), inputs.GetProperty("rep1").GetInt64(), inputs.GetProperty("rep2").GetInt64()),
		// ----- Batch 10 -----
		"SM_CHARACTER_SELECT" => ReconstructCharacterSelect(inputs),
		"SM_AFTER_SIEGE_LOCINFO_475" => new SM_AFTER_SIEGE_LOCINFO_475(),
		"SM_NEARBY_QUESTS" => new SM_NEARBY_QUESTS(ReconstructIntIntMap(inputs.GetProperty("entries"))),
		"SM_MACRO_LIST" => new SM_MACRO_LIST(inputs.GetProperty("playerObjectId").GetInt32(), ReconstructMacros(inputs.GetProperty("macros")), inputs.GetProperty("clearList").GetBoolean()),
		"SM_FIRST_SHOW_DECOMPOSABLE" => new SM_FIRST_SHOW_DECOMPOSABLE(inputs.GetProperty("objectId").GetInt32(), ReconstructResultedItems(inputs.GetProperty("items"))),
		"SM_SECONDARY_SHOW_DECOMPOSABLE" => new SM_SECONDARY_SHOW_DECOMPOSABLE(inputs.GetProperty("objectId").GetInt32(), ReconstructResultedItems(inputs.GetProperty("items"))),
		_ => throw new NotSupportedException($"No faithful C# reconstruction registered for {packetName}"),
	};

	// ----- Batch 10 reconstruct helpers -----

	// SM_CHARACTER_SELECT: 1-arg (type) vs 3-arg (type,messageType,wrongCount) ctor selected by the "ctor" tag.
	// Pin SecurityConfig.PASSKEY_WRONG_MAXCOUNT=5 (the faithful @Property default) to match the Java oracle fixture.
	private static SM_CHARACTER_SELECT ReconstructCharacterSelect(JsonElement inputs)
	{
		Aion.GameServer.Configs.Main.SecurityConfig.PASSKEY_WRONG_MAXCOUNT = 5;
		if (inputs.GetProperty("ctor").GetString() == "type")
			return new SM_CHARACTER_SELECT(inputs.GetProperty("type").GetInt32());
		return new SM_CHARACTER_SELECT(inputs.GetProperty("type").GetInt32(),
			(short)inputs.GetProperty("messageType").GetInt32(), inputs.GetProperty("wrongCount").GetInt32());
	}

	// Insertion-ordered int->int map mirroring the Java LinkedHashMap (C# Dictionary preserves insertion order).
	private static Dictionary<int, int> ReconstructIntIntMap(JsonElement entries)
	{
		var map = new Dictionary<int, int>();
		foreach (var e in entries.EnumerateArray())
			map[e[0].GetInt32()] = e[1].GetInt32();
		return map;
	}

	private static List<Aion.GameServer.Model.GameObjects.Players.Macros.Macro> ReconstructMacros(JsonElement macros)
	{
		var list = new List<Aion.GameServer.Model.GameObjects.Players.Macros.Macro>();
		foreach (var m in macros.EnumerateArray())
			list.Add(new Aion.GameServer.Model.GameObjects.Players.Macros.Macro(m[0].GetInt32(), m[1].GetString()!));
		return list;
	}

	// ResultedItem is XML-built; set itemId/minCount directly (public XML fields), bypassing afterUnmarshal (no DataManager).
	private static List<Aion.GameServer.Model.Templates.Items.ResultedItem> ReconstructResultedItems(JsonElement items)
	{
		var list = new List<Aion.GameServer.Model.Templates.Items.ResultedItem>();
		foreach (var it in items.EnumerateArray())
			list.Add(new Aion.GameServer.Model.Templates.Items.ResultedItem { itemId = it[0].GetInt32(), minCount = it[1].GetInt32() });
		return list;
	}

	// SM_TRANSFORM custom (testing) ctor: creature objId + state (harness ACTIVE=1) + scalars + TransformType.
	private static SM_TRANSFORM ReconstructTransform(JsonElement inputs)
	{
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>());
		var type = Enum.Parse<Aion.GameServer.SkillEngine.Model.TransformType>(inputs.GetProperty("type").GetString()!);
		return new SM_TRANSFORM(c, inputs.GetProperty("modelId").GetInt32(), inputs.GetProperty("unk7").GetInt32(), type,
			inputs.GetProperty("unk1").GetInt32(), inputs.GetProperty("unk2").GetInt32(), inputs.GetProperty("unk3").GetInt32(),
			inputs.GetProperty("unk4").GetInt32(), inputs.GetProperty("unk5").GetInt32(), inputs.GetProperty("unk6").GetInt32(),
			inputs.GetProperty("panelId").GetInt32());
	}

	// SM_DELETE_ITEM: map the fixture's raw delete-type mask to the matching ItemDeleteType class-enum
	// instance (Java parity: SM_DELETE_ITEM writes deleteType.getMask()). Fixture uses 0 (DEFAULT) / 0x15 (DISCARD).
	private static global::Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType ResolveItemDeleteType(int mask)
	{
		var t = typeof(global::Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType);
		foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Static))
		{
			var value = (global::Aion.GameServer.Services.Items.ItemPacketService.ItemDeleteType)field.GetValue(null)!;
			if (value.GetMask() == mask)
				return value;
		}
		throw new NotSupportedException($"No ItemDeleteType with mask 0x{mask:X}");
	}

	// SM_LEGION_EDIT: type 0x07 via (int) ctor; type 0x06 via (int type, int unixTime) ctor.
	private static SM_LEGION_EDIT ReconstructLegionEdit(JsonElement inputs)
	{
		return inputs.GetProperty("type").GetInt32() switch
		{
			0x07 => new SM_LEGION_EDIT(0x07),
			0x06 => new SM_LEGION_EDIT(0x06, inputs.GetProperty("unixTime").GetInt32()),
			var t => throw new NotSupportedException($"No SM_LEGION_EDIT faithful ctor for type {t}"),
		};
	}

	// SM_UPGRADE_ARCADE deterministic action branches: 0 (showIcon), 2 (no-arg), 6 (itemId,count), 7 (frenzy).
	private static SM_UPGRADE_ARCADE ReconstructUpgradeArcade(JsonElement inputs)
	{
		return inputs.GetProperty("action").GetInt32() switch
		{
			0 => new SM_UPGRADE_ARCADE(inputs.GetProperty("showIcon").GetBoolean()),
			2 => new SM_UPGRADE_ARCADE(),
			6 => new SM_UPGRADE_ARCADE(inputs.GetProperty("rewardItemId").GetInt32(), inputs.GetProperty("rewardItemCount").GetInt64()),
			7 => new SM_UPGRADE_ARCADE(inputs.GetProperty("frenzyDurationSeconds").GetInt32()),
			var a => throw new NotSupportedException($"No SM_UPGRADE_ARCADE faithful ctor for action {a}"),
		};
	}

	// SM_CAPTCHA: type 1 (count,data) and type 3 (isCorrect,banTime) ctors.
	private static SM_CAPTCHA ReconstructCaptcha(JsonElement inputs)
	{
		return inputs.GetProperty("ctor").GetString() switch
		{
			"count_data" => new SM_CAPTCHA(inputs.GetProperty("count").GetInt32(), inputs.GetProperty("data").EnumerateArray().Select(e => (byte)e.GetInt32()).ToArray()),
			"isCorrect_banTime" => new SM_CAPTCHA(inputs.GetProperty("isCorrect").GetBoolean(), inputs.GetProperty("banTime").GetInt32()),
			_ => throw new NotSupportedException("Unknown SM_CAPTCHA ctor"),
		};
	}

	// SM_MEGAPHONE.FactionLabel: resolve the static instance by name (id derived from Race.getRaceId()).
	private static SM_MEGAPHONE.FactionLabel ResolveFactionLabel(string name) => name switch
	{
		"NONE" => SM_MEGAPHONE.FactionLabel.NONE,
		"ELYOS" => SM_MEGAPHONE.FactionLabel.ELYOS,
		"ASMODIANS" => SM_MEGAPHONE.FactionLabel.ASMODIANS,
		_ => throw new NotSupportedException($"No FactionLabel for {name}"),
	};

	// SM_PET: the deterministic scalar branches (RENAME / DISMISS / SPECIAL_FUNCTION).
	// Mirror the exact ctor the Java generator used so the action + payload selection matches bilaterally.
	private static SM_PET ReconstructPet(JsonElement inputs)
	{
		var action = inputs.GetProperty("action").GetString()!;
		switch (action)
		{
			case "RENAME":
				return new SM_PET(inputs.GetProperty("petObjectId").GetInt32(), inputs.GetProperty("petName").GetString()!);
			case "DISMISS":
				var anim = Enum.Parse<Aion.GameServer.Model.Animations.ObjectDeleteAnimation>(inputs.GetProperty("animation").GetString()!);
				return new SM_PET(inputs.GetProperty("petObjectId").GetInt32(), anim);
			case "SPECIAL_FUNCTION":
				// DOPING uses the (dopeAction,itemId,slot) ctor; AUTOLOOT/AUTOSELL use the (PetSpecialFunction,active,npcObjId) ctor.
				if (inputs.TryGetProperty("dopeAction", out var dope))
					return new SM_PET(dope.GetInt32(), inputs.GetProperty("itemId").GetInt32(), inputs.GetProperty("slot").GetInt32());
				var sf = Enum.Parse<PetSpecialFunction>(inputs.GetProperty("specialFunction").GetString()!);
				return new SM_PET(sf, inputs.GetProperty("active").GetBoolean(), inputs.GetProperty("npcObjId").GetInt32());
			default:
				throw new NotSupportedException($"No SM_PET reconstruction for action {action}");
		}
	}

	// SM_ATTACK: attacker + target are non-Player harness Creatures (avoids the instanceof Player branch).
	// Each carries MAXHP via the harness game-stats + currentHp via harness life-stats, so getHpPercentage()
	// matches the Java HarnessAttackLifeStats (currentHp*100/maxHp). Per-hit AttackResult list mirrors Java 1:1.
	private static SM_ATTACK ReconstructAttack(JsonElement inputs)
	{
		var attacker = BuildAttackCreature(inputs.GetProperty("attackerObjId").GetInt32(),
			inputs.GetProperty("attackerMaxHp").GetInt32(), inputs.GetProperty("attackerCurrentHp").GetInt32());
		var target = BuildAttackCreature(inputs.GetProperty("targetObjId").GetInt32(),
			inputs.GetProperty("targetMaxHp").GetInt32(), inputs.GetProperty("targetCurrentHp").GetInt32());
		var type = Enum.Parse<Aion.GameServer.Model.Animations.AttackTypeAnimation>(inputs.GetProperty("attackType").GetString()!);
		var hand = Enum.Parse<Aion.GameServer.Model.Animations.AttackHandAnimation>(inputs.GetProperty("attackHand").GetString()!);
		var hits = new List<Aion.GameServer.Controllers.Attack.AttackResult>();
		foreach (var hit in inputs.GetProperty("hits").EnumerateArray())
		{
			var status = Enum.Parse<Aion.GameServer.Controllers.Attack.AttackStatus>(hit.GetProperty("status").GetString()!);
			var r = new Aion.GameServer.Controllers.Attack.AttackResult(hit.GetProperty("damage").GetSingle(), status);
			if (hit.TryGetProperty("shieldType", out var st) && st.GetInt32() != 0)
				r.SetShieldType(st.GetInt32());
			if (hit.TryGetProperty("protectorId", out var pid))
				r.SetProtectorId(pid.GetInt32());
			if (hit.TryGetProperty("protectedDamage", out var pd))
				r.SetProtectedDamage(pd.GetInt32());
			if (hit.TryGetProperty("protectedSkillId", out var psid))
				r.SetProtectedSkillId(psid.GetInt32());
			hits.Add(r);
		}
		return new SM_ATTACK(attacker, target, inputs.GetProperty("attackno").GetInt32(),
			inputs.GetProperty("time").GetInt32(), type, hand, hits);
	}

	private static PacketHarnessCreature BuildAttackCreature(int objectId, int maxHp, int currentHp)
	{
		var stats = new Dictionary<StatEnum, int> { [StatEnum.MAXHP] = maxHp };
		var c = new PacketHarnessCreature(objectId, 50, stats);
		c.SetLifeStats(new PacketHarnessLifeStats(c, currentHp, 0));
		return c;
	}

	private static SM_ATTACK_RESPONSE ReconstructAttackResponse(JsonElement inputs)
	{
		var count = inputs.GetProperty("attackCount").GetInt32();
		return inputs.GetProperty("factory").GetString() switch
		{
			"TARGET_IN_DIFFERENT_AREA" => SM_ATTACK_RESPONSE.TARGET_IN_DIFFERENT_AREA(count),
			"STOP_INVALID_TARGET" => SM_ATTACK_RESPONSE.STOP_INVALID_TARGET(count),
			"TARGET_TOO_FAR_AWAY" => SM_ATTACK_RESPONSE.TARGET_TOO_FAR_AWAY(count),
			"STOP_OBSTACLE_IN_THE_WAY" => SM_ATTACK_RESPONSE.STOP_OBSTACLE_IN_THE_WAY(count),
			"STOP_TOO_CLOSE_TO_ATTACK" => SM_ATTACK_RESPONSE.STOP_TOO_CLOSE_TO_ATTACK(count),
			"STOP_WITHOUT_MESSAGE" => SM_ATTACK_RESPONSE.STOP_WITHOUT_MESSAGE(count),
			_ => throw new NotSupportedException("Unknown SM_ATTACK_RESPONSE factory"),
		};
	}

	private static SM_SKILL_ACTIVATION ReconstructSkillActivation(JsonElement inputs)
	{
		return inputs.GetProperty("ctor").GetString() switch
		{
			"toggle" => new SM_SKILL_ACTIVATION(inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("isActive").GetBoolean()),
			"stigma" => new SM_SKILL_ACTIVATION(inputs.GetProperty("skillId").GetInt32()),
			_ => throw new NotSupportedException("Unknown SM_SKILL_ACTIVATION ctor"),
		};
	}

	// SM_CASTSPELL: targetType 0/3/4 use the object-id ctor; 1/2 use the (x,y,z) ground-point ctor.
	private static SM_CASTSPELL ReconstructCastSpell(JsonElement inputs)
	{
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>());
		var spellId = inputs.GetProperty("spellId").GetInt32();
		var level = inputs.GetProperty("level").GetInt32();
		var targetType = inputs.GetProperty("targetType").GetInt32();
		var castDuration = inputs.GetProperty("castDuration").GetInt32();
		var castSpeed = inputs.GetProperty("castSpeed").GetSingle();
		var boost = inputs.GetProperty("boost").GetBoolean();
		if (targetType == 1 || targetType == 2)
			return new SM_CASTSPELL(c, spellId, level, targetType, inputs.GetProperty("x").GetSingle(), inputs.GetProperty("y").GetSingle(), inputs.GetProperty("z").GetSingle(), castDuration, castSpeed, boost);
		return new SM_CASTSPELL(c, spellId, level, targetType, inputs.GetProperty("targetObjectId").GetInt32(), castDuration, castSpeed, boost);
	}

	// SM_MOTION scalar branches: action 2 = (motionId, remainingTime); 5 = (motionId, type); 6 = (motionId).
	private static SM_MOTION ReconstructMotionScalar(JsonElement inputs)
	{
		var motionId = (short)inputs.GetProperty("motionId").GetInt32();
		return inputs.GetProperty("ctor").GetString() switch
		{
			"motionId_remainingTime" => new SM_MOTION(motionId, inputs.GetProperty("remainingTime").GetInt32()),
			"motionId_type" => new SM_MOTION(motionId, (byte)inputs.GetProperty("type").GetInt32()),
			"motionId" => new SM_MOTION(motionId),
			_ => throw new NotSupportedException("Unknown SM_MOTION scalar ctor"),
		};
	}

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

	// SM_DUEL: started (type 0) via the requesterObjId factory; result (type 1) via the (DuelResult, name) factory.
	private static SM_DUEL ReconstructDuel(JsonElement inputs)
	{
		return inputs.GetProperty("type").GetInt32() switch
		{
			0 => SM_DUEL.SM_DUEL_STARTED(inputs.GetProperty("requesterObjId").GetInt32()),
			1 => SM_DUEL.SM_DUEL_RESULT(Enum.Parse<DuelResult>(inputs.GetProperty("result").GetString()!), inputs.GetProperty("playerName").GetString()!),
			_ => throw new NotSupportedException("Unknown SM_DUEL type"),
		};
	}

	// SM_DELETE: exercise the matching public ctor keyed on the captured animationId.
	// 1 = SM_DELETE(obj) default FADE_OUT (inRange true); 0 = SM_DELETE(obj, inRange=false) -> NONE;
	// 11 = SM_DELETE(obj, JUMP_IN); 19 = SM_DELETE(obj, DELAYED).
	private static SM_DELETE ReconstructDelete(JsonElement inputs)
	{
		var obj = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, new Dictionary<StatEnum, int>());
		return inputs.GetProperty("animationId").GetInt32() switch
		{
			1 => new SM_DELETE(obj),
			0 => new SM_DELETE(obj, false),
			11 => new SM_DELETE(obj, Aion.GameServer.Model.Animations.ObjectDeleteAnimation.JUMP_IN),
			19 => new SM_DELETE(obj, Aion.GameServer.Model.Animations.ObjectDeleteAnimation.DELAYED),
			var a => throw new NotSupportedException($"No SM_DELETE ctor mapping for animationId {a}"),
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

	// Deterministic fixed WorldPosition values. MUST mirror the Java side exactly
	// (GoldenLivePacketFixtureGeneratorTest.HARNESS_*). A bare WorldPosition needs no live World/MapRegion.
	private const int HARNESS_WORLD_ID = 210010000;
	private const float HARNESS_X = 100.0f;
	private const float HARNESS_Y = 200.0f;
	private const float HARNESS_Z = 300.0f;
	private const byte HARNESS_HEADING = 0;

	// Harness creature carrying the deterministic WorldPosition (objId + position only; no live world).
	private static PacketHarnessCreature PositionedHarness(int objectId)
	{
		var c = new PacketHarnessCreature(objectId, 50, new Dictionary<StatEnum, int>());
		c.SetPosition(new Aion.GameServer.World.WorldPosition(
			HARNESS_WORLD_ID, HARNESS_X, HARNESS_Y, HARNESS_Z, HARNESS_HEADING));
		return c;
	}

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

	private static SM_CLOSE_QUESTION_WINDOW ReconstructCloseQuestionWindow(JsonElement inputs)
	{
		var messageId = inputs.GetProperty("messageId").GetInt32();
		var parameters = inputs.GetProperty("params").EnumerateArray().Select(p => p.GetString()!).ToArray();
		return messageId switch
		{
			0 => SM_CLOSE_QUESTION_WINDOW.CLOSE_QUESTION_WINDOW(),
			1300134 => SM_CLOSE_QUESTION_WINDOW.STR_DUEL_REQUESTER_WITHDRAW_REQUEST(parameters[0]),
			1300097 => SM_CLOSE_QUESTION_WINDOW.STR_DUEL_HE_REJECT_DUEL(parameters[0]),
			_ => throw new NotSupportedException($"No SM_CLOSE_QUESTION_WINDOW factory for messageId {messageId}"),
		};
	}

	// SM_ITEM_USAGE_ANIMATION: select the ctor matching the Java generator (time==0 paths only).
	// Faithful SM_ITEM_USAGE_ANIMATION : AionServerPacket (captured via WriteImpl path).
	private static SM_ITEM_USAGE_ANIMATION ReconstructItemUsageAnimation(JsonElement inputs)
	{
		var ctor = inputs.GetProperty("ctor").GetString()!;
		switch (ctor)
		{
			case "player_itemObj_itemId":
				return new SM_ITEM_USAGE_ANIMATION(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("itemObjId").GetInt32(), inputs.GetProperty("itemId").GetInt32());
			case "player_itemObj_itemId_time_end_unk":
				return new SM_ITEM_USAGE_ANIMATION(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("itemObjId").GetInt32(), inputs.GetProperty("itemId").GetInt32(), inputs.GetProperty("time").GetInt32(), inputs.GetProperty("end").GetInt32(), inputs.GetProperty("unk3").GetInt32());
			case "player_target_itemObj_itemId_time_end_unk":
				return new SM_ITEM_USAGE_ANIMATION(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("targetObjId").GetInt32(), inputs.GetProperty("itemObjId").GetInt32(), inputs.GetProperty("itemId").GetInt32(), inputs.GetProperty("time").GetInt32(), inputs.GetProperty("end").GetInt32(), inputs.GetProperty("unk3").GetInt32());
			case "full":
				return new SM_ITEM_USAGE_ANIMATION(inputs.GetProperty("playerObjId").GetInt32(), inputs.GetProperty("targetObjId").GetInt32(), inputs.GetProperty("itemObjId").GetInt32(), inputs.GetProperty("itemId").GetInt32(), inputs.GetProperty("time").GetInt32(), inputs.GetProperty("end").GetInt32(), inputs.GetProperty("unk").GetInt32(), inputs.GetProperty("unk1").GetInt32(), inputs.GetProperty("unk2").GetInt32(), inputs.GetProperty("unk3").GetInt32());
			default:
				throw new NotSupportedException($"No SM_ITEM_USAGE_ANIMATION ctor for {ctor}");
		}
	}

	// SM_ATTACK_STATUS: the faithful (Creature, TYPE, skillId, value[, LOG]) ctor — exercises the same
	// getHpPercentage()/getMpPercentage() harness path Java reads. The deterministic HarnessCreature/HarnessLifeStats
	// supply MAXHP/MAXMP (game-stats) + currentHp/currentMp (life-stats).
	private static SmAttackStatus ReconstructAttackStatus(JsonElement inputs)
	{
		var stats = new Dictionary<StatEnum, int>
		{
			[StatEnum.MAXHP] = inputs.GetProperty("maxHp").GetInt32(),
			[StatEnum.MAXMP] = inputs.GetProperty("maxMp").GetInt32(),
		};
		var c = new PacketHarnessCreature(inputs.GetProperty("objectId").GetInt32(), 50, stats);
		c.SetLifeStats(new PacketHarnessLifeStats(c, inputs.GetProperty("currentHp").GetInt32(), inputs.GetProperty("currentMp").GetInt32()));
		var type = ResolveAttackStatusType(inputs.GetProperty("type").GetString()!);
		var log = Enum.Parse<SmAttackStatus.LOG>(inputs.GetProperty("log").GetString()!);
		var skillId = inputs.GetProperty("skillId").GetInt32();
		var value = inputs.GetProperty("value").GetInt32();
		// The 2-arg convenience ctor (creature, value) is REGULAR/skill 0/LOG.REGULAR; route there to mirror Java.
		if (inputs.GetProperty("type").GetString() == "REGULAR" && skillId == 0 && log == SmAttackStatus.LOG.REGULAR)
			return new SmAttackStatus(c, value);
		return new SmAttackStatus(c, type, skillId, value, log);
	}

	private static SmAttackStatus.TYPE ResolveAttackStatusType(string name) => name switch
	{
		"DAMAGE" => SmAttackStatus.TYPE.DAMAGE,
		"MP" => SmAttackStatus.TYPE.MP,
		"USED_MP" => SmAttackStatus.TYPE.USED_MP,
		"REGULAR" => SmAttackStatus.TYPE.REGULAR,
		_ => throw new NotSupportedException($"No SM_ATTACK_STATUS TYPE for {name}"),
	};

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
