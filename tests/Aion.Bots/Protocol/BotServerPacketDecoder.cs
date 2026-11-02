using System.Buffers.Binary;
using System.Collections.Frozen;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.Protocol;

/// <summary>Partial, bot-facing decoders for the server packets an honest client must perceive.</summary>
public sealed partial class BotServerPacketDecoder
{
	private delegate IReadOnlyDictionary<string, object?> DecodeBody(ReadOnlySpan<byte> body);

	private static readonly FrozenDictionary<Type, DecodeBody> Decoders =
		new Dictionary<Type, DecodeBody>
		{
			[typeof(SM_KEY)] = DecodeKey,
			[typeof(SM_VERSION_CHECK)] = DecodeVersionCheck,
			[typeof(SM_L2AUTH_LOGIN_CHECK)] = DecodeLoginCheck,
			[typeof(SM_CHARACTER_LIST)] = DecodeCharacterList,
			[typeof(SM_CREATE_CHARACTER)] = DecodeCreateCharacter,
			[typeof(SM_DELETE_CHARACTER)] = DecodeDeleteCharacter,
			[typeof(SM_RESTORE_CHARACTER)] = DecodeRestoreCharacter,
			[typeof(SM_TITLE_INFO)] = DecodeTitleInfo,
			[typeof(SM_MACRO_LIST)] = DecodeMacroList,
			[typeof(SM_MACRO_RESULT)] = DecodeMacroResult,
			[typeof(SM_UI_SETTINGS)] = DecodeUiSettings,
			[typeof(SM_PLASTIC_SURGERY)] = DecodePlasticSurgery,
			[typeof(SM_QUIT_RESPONSE)] = DecodeQuitResponse,
			[typeof(SM_RECONNECT_KEY)] = DecodeReconnectKey,
			[typeof(SM_CHARACTER_SELECT)] = DecodeCharacterSelect,
			[typeof(SM_PET)] = DecodePet,
			[typeof(SM_ATREIAN_PASSPORT)] = DecodeAtreianPassport,
			[typeof(SM_UPDATE_PLAYER_APPEARANCE)] = DecodeEquipmentAppearance,
			[typeof(SM_ENTER_WORLD_CHECK)] = DecodeEnterWorldCheck,
			[typeof(SM_PLAYER_SPAWN)] = DecodePlayerSpawn,
			[typeof(SM_CHANNEL_INFO)] = DecodeChannelInfo,
			[typeof(SM_INSTANCE_INFO)] = DecodeInstanceInfo,
			[typeof(SM_PLAY_MOVIE)] = DecodePlayMovie,
			[typeof(SM_CHAT_INIT)] = DecodeChatInit,
			[typeof(SM_PLAYER_INFO)] = DecodePlayerInfo,
			[typeof(SM_EMOTION)] = DecodeEmotion,
			[typeof(SM_USE_OBJECT)] = DecodeUseObject,
			[typeof(SM_NPC_INFO)] = DecodeNpcInfo,
			[typeof(SM_GATHERABLE_INFO)] = DecodeGatherableInfo,
			[typeof(SM_GATHER_UPDATE)] = DecodeGatherUpdate,
			[typeof(SM_CRAFT_UPDATE)] = DecodeCraftUpdate,
			[typeof(SM_ITEM_USAGE_ANIMATION)] = DecodeItemUsage,
			[typeof(SM_TUNE_RESULT)] = DecodeTuneResult,
			[typeof(SM_UNWRAP_ITEM)] = DecodeUnwrapItem,
			[typeof(SM_SHOW_DECOMPOSABLE)] = DecodeDecomposable,
			[typeof(SM_SELECT_DECOMPOSABLE_RESULT)] = DecodeDecomposableResult,
			[typeof(SmAttackStatus)] = DecodeAttackStatus,
			[typeof(SM_ATTACK)] = DecodeAttack,
			[typeof(SM_MOVE)] = DecodeMove,
			[typeof(SM_FORCED_MOVE)] = DecodeForcedMove,
			[typeof(SM_POSITION)] = DecodePosition,
			[typeof(SM_SHOW_BRAND)] = DecodeShowBrand,
			[typeof(SM_DELETE)] = DecodeDelete,
			[typeof(SM_TELEPORT_LOC)] = DecodeTeleport,
			[typeof(SM_DIALOG_WINDOW)] = DecodeDialog,
			[typeof(SM_QUESTION_WINDOW)] = DecodeQuestion,
			[typeof(SM_CLOSE_QUESTION_WINDOW)] = DecodeCloseQuestion,
			[typeof(SM_MESSAGE)] = DecodeMessage,
			[typeof(SM_SYSTEM_MESSAGE)] = DecodeSystemMessage,
			[typeof(SM_STATS_INFO)] = DecodeStats,
			[typeof(SM_STATUPDATE_HP)] = DecodeHp,
			[typeof(SM_STATUPDATE_MP)] = DecodeMp,
			[typeof(SM_STATUPDATE_DP)] = DecodeDp,
			[typeof(SM_STATUPDATE_EXP)] = DecodeExp,
			[typeof(SM_FLY_TIME)] = DecodeFlyTime,
			[typeof(SM_ABNORMAL_STATE)] = DecodeAbnormalState,
			[typeof(SM_ABNORMAL_EFFECT)] = DecodeAbnormalEffect,
			[typeof(SM_WINDSTREAM)] = DecodeWindstream,
			[typeof(SM_WINDSTREAM_ANNOUNCE)] = DecodeWindstreamAnnounce,
			[typeof(SM_DIE)] = DecodeDie,
			[typeof(SM_RESURRECT)] = DecodeResurrect,
			[typeof(SM_BIND_POINT_INFO)] = DecodeBindPoint,
			[typeof(SM_KISK_UPDATE)] = DecodeKiskUpdate,
			[typeof(SM_INVENTORY_INFO)] = DecodeInventoryInfo,
			[typeof(SM_INVENTORY_ADD_ITEM)] = DecodeInventoryAdd,
			[typeof(SM_INVENTORY_UPDATE_ITEM)] = DecodeInventoryUpdate,
			[typeof(SM_DELETE_ITEM)] = DecodeDeleteItem,
			[typeof(SM_WAREHOUSE_INFO)] = DecodeWarehouseInfo,
			[typeof(SM_WAREHOUSE_ADD_ITEM)] = DecodeWarehouseAdd,
			[typeof(SM_WAREHOUSE_UPDATE_ITEM)] = DecodeWarehouseUpdate,
			[typeof(SM_DELETE_WAREHOUSE_ITEM)] = DecodeWarehouseDelete,
			[typeof(SM_BROKER_SERVICE)] = DecodeBroker,
			[typeof(SM_PRIVATE_STORE)] = DecodePrivateStore,
			[typeof(SM_PRIVATE_STORE_NAME)] = DecodePrivateStoreName,
			[typeof(SM_CUBE_UPDATE)] = DecodeCubeUpdate,
			[typeof(SM_SKILL_LIST)] = DecodeSkillList,
			[typeof(SM_SKILL_REMOVE)] = DecodeSkillRemove,
			[typeof(SM_SKILL_ACTIVATION)] = DecodeSkillActivation,
			[typeof(SM_RIDE_ROBOT)] = DecodeRideRobot,
			[typeof(SM_SUMMON_PANEL)] = DecodeSummonPanel,
			[typeof(SM_SUMMON_PANEL_REMOVE)] = DecodeSummonPanelRemove,
			[typeof(SM_SUMMON_OWNER_REMOVE)] = DecodeSummonOwnerRemove,
			[typeof(SM_SUMMON_USESKILL)] = DecodeSummonUseSkill,
			[typeof(SM_RECIPE_LIST)] = DecodeRecipeList,
			[typeof(SM_LEARN_RECIPE)] = DecodeLearnRecipe,
			[typeof(SM_RECIPE_DELETE)] = DecodeRecipeDelete,
			[typeof(SM_SKILL_COOLDOWN)] = DecodeSkillCooldown,
			[typeof(SM_CASTSPELL)] = DecodeCastSpell,
			[typeof(SM_CASTSPELL_RESULT)] = DecodeCastSpellResult,
			[typeof(SM_SKILL_CANCEL)] = DecodeSkillCancel,
			[typeof(SM_QUEST_LIST)] = DecodeQuestList,
			[typeof(SM_QUEST_ACTION)] = DecodeQuestAction,
			[typeof(SM_QUEST_COMPLETED_LIST)] = DecodeQuestCompletedList,
			[typeof(SM_LOOT_STATUS)] = DecodeLootStatus,
			[typeof(SM_GAME_TIME)] = DecodeGameTime,
			[typeof(SM_LOOT_ITEMLIST)] = DecodeLootItemList,
			[typeof(SM_TRADELIST)] = DecodeTradeList,
			[typeof(SM_TRADE_IN_LIST)] = DecodeTradeInList,
			[typeof(SM_PRICES)] = DecodePrices,
			[typeof(SM_SELL_ITEM)] = DecodeSellItem,
			[typeof(SM_REPURCHASE)] = DecodeRepurchase,
			[typeof(SM_GROUP_INFO)] = DecodeGroupInfo,
			[typeof(SM_GROUP_LOOT)] = DecodeGroupLoot,
			[typeof(SM_GROUP_MEMBER_INFO)] = DecodeGroupMember,
			[typeof(SM_ALLIANCE_INFO)] = DecodeAllianceInfo,
			[typeof(SM_ALLIANCE_MEMBER_INFO)] = DecodeAllianceMember,
			[typeof(SM_LEAVE_GROUP_MEMBER)] = DecodeLeaveGroup,
			[typeof(SM_DUEL)] = DecodeDuel,
			[typeof(SM_ABYSS_RANK)] = DecodeAbyssRank,
			[typeof(SM_FRIEND_LIST)] = DecodeFriendList,
			[typeof(SM_FRIEND_UPDATE)] = DecodeFriendUpdate,
			[typeof(SM_FRIEND_NOTIFY)] = DecodeFriendNotify,
			[typeof(SM_FRIEND_RESPONSE)] = DecodeSocialResponse,
			[typeof(SM_BLOCK_LIST)] = DecodeBlockList,
			[typeof(SM_BLOCK_RESPONSE)] = DecodeSocialResponse,
			[typeof(SM_LEGION_INFO)] = DecodeLegionInfo,
			[typeof(SM_LEGION_ADD_MEMBER)] = DecodeLegionAddMember,
			[typeof(SM_LEGION_MEMBERLIST)] = DecodeLegionMembers,
			[typeof(SM_FIND_GROUP)] = DecodeFindGroup,
			[typeof(SM_RECALLED_BY_OTHER)] = DecodeRecall,
			[typeof(SM_LEGION_UPDATE_EMBLEM)] = DecodeLegionEmblem,
			[typeof(SM_LEGION_HISTORY)] = DecodeLegionHistory,
			[typeof(SM_LEGION_EDIT)] = DecodeLegionEdit,
			[typeof(SM_MAIL_SERVICE)] = DecodeMailService,
			[typeof(SM_EXCHANGE_REQUEST)] = DecodeExchangeRequest,
			[typeof(SM_EXCHANGE_CONFIRMATION)] = DecodeExchangeConfirmation,
			[typeof(SM_EXCHANGE_ADD_KINAH)] = DecodeExchangeKinah,
			[typeof(SM_EXCHANGE_ADD_ITEM)] = DecodeExchangeItem,
		}.ToFrozenDictionary();

	public IReadOnlyCollection<Type> PacketTypes => Decoders.Keys;

	public DecodedBotServerPacket Decode(DecodedGamePacket packet) => Decode(packet.PacketType, packet.Body);

	/// <summary>
	/// Decodes bot-facing packets and preserves known-but-uninteresting server packets as raw bodies so a live
	/// transport can keep the complete wire stream without pretending the packet is unknown to the server.
	/// </summary>
	public DecodedBotServerPacket DecodeOrRaw(DecodedGamePacket packet)
	{
		if (Decoders.TryGetValue(packet.PacketType, out var decoder))
			return new DecodedBotServerPacket(packet.PacketType, decoder(packet.Body))
			{
				Landings = Landings(packet.PacketType, packet.Body),
				// SM_ATTACK's two health percentages are metadata so existing packet traces stay unchanged.
				Health = packet.PacketType == typeof(SM_ATTACK) && packet.Body.Length >= 15
					? [new(BinaryPrimitives.ReadInt32LittleEndian(packet.Body.AsSpan(9)), packet.Body[13]),
					   new(BinaryPrimitives.ReadInt32LittleEndian(packet.Body), packet.Body[14])] : null,
			};
		return new DecodedBotServerPacket(
			packet.PacketType,
			new Dictionary<string, object?>(StringComparer.Ordinal)
			{
				["bodyHex"] = Convert.ToHexString(packet.Body),
			});
	}

	public DecodedBotServerPacket Decode(Type packetType, ReadOnlySpan<byte> body)
	{
		if (!Decoders.TryGetValue(packetType, out var decoder))
			throw new NotSupportedException($"The bot does not decode {packetType.Name}.");
		return new DecodedBotServerPacket(packetType, decoder(body)) { Landings = Landings(packetType, body) };
	}

	/// <summary>
	/// NR-110g: where a packet puts creatures. Java moves the target of a stagger, a stumble, a lift and a pull on the
	/// server (StaggerEffect.startEffect 41, StumbleEffect 42, OpenAerialEffect 62, PulledEffect 65) and sends
	/// SM_FORCED_MOVE for a player only. For any other creature the place is in SM_CASTSPELL_RESULT alone, in the entry
	/// of the effect (writeImpl 140-165). Null when the packet puts none anywhere.
	/// </summary>
	private static IReadOnlyList<BotCreatureLanding>? Landings(Type packetType, ReadOnlySpan<byte> body) =>
		packetType == typeof(SM_CASTSPELL_RESULT) ? DecodeCastSpellLandings(body)
		: packetType == typeof(SM_ATTACK) ? DecodeAttackLanding(body) : null;

	// NR-R15a: a weapon's critical that staggers or stumbles its target. Java SM_ATTACK.writeImpl: D the attacker, C, H, C, C,
	// D the target, C and C the two HP; then H 1025 or 1026 for a critical's effect on a target that is no player (1 or 2 on a
	// player, who is told by SM_FORCED_MOVE), H 0, and for such an effect three F, the target's place after it.
	private static IReadOnlyList<BotCreatureLanding>? DecodeAttackLanding(ReadOnlySpan<byte> body)
	{
		if (body.Length < 31 || BinaryPrimitives.ReadUInt16LittleEndian(body[15..]) is not (1025 or 1026)) return null;
		return [new(BinaryPrimitives.ReadInt32LittleEndian(body[9..]), BinaryPrimitives.ReadSingleLittleEndian(body[19..]),
			BinaryPrimitives.ReadSingleLittleEndian(body[23..]), BinaryPrimitives.ReadSingleLittleEndian(body[27..]))];
	}

	// Java SM_CASTSPELL_RESULT.writeImpl after the chain flag: C 2, D, D, C for a skill of an item used in a fight, or
	// C method, C dash and, for a dash, C heading and three F; then H the number of effect entries, and the entries.
	private static IReadOnlyList<BotCreatureLanding>? DecodeCastSpellLandings(ReadOnlySpan<byte> body)
	{
		if (body.Length < 5) return null;
		int at = 5 + (body[4] is 0 or 3 or 4 ? 4 : body[4] == 2 ? 44 : 12) + 11;
		if (body.Length < at + 2) return null;
		if (body[at] == 2) at += 10;
		else at += 2 + (body[at + 1] is 1 or 2 or 3 or 4 or 6 ? 13 : 0);
		if (body.Length < at + 2) return null;
		int entries = BinaryPrimitives.ReadUInt16LittleEndian(body[at..]);
		var landings = new List<BotCreatureLanding>();
		return entries > 0 && TryReadCastEffects(body[(at + 2)..], entries, landings) && landings.Count > 0 ? landings : null;
	}

	/// <summary>
	/// One entry and those after it: D the creature, C the result, C its HP, C the caster's HP, C the spell status, C, H, C
	/// the runes; then by the status the place (1 stumble, 2 stagger, 4 lift, 8 the fall after a lift) or a heading (16);
	/// then C the number of results, each C, D, C, C a shield and, by the shield, three or seven D. A pull and a step back
	/// put their place where a status would, with a status that names none, and nothing in the packet says so: both
	/// readings are tried, and the one that uses the packet up to its last byte holds.
	/// </summary>
	private static bool TryReadCastEffects(ReadOnlySpan<byte> rest, int left, List<BotCreatureLanding> landings)
	{
		if (left == 0) return rest.IsEmpty;
		if (rest.Length < 12) return false;
		int objectId = BinaryPrimitives.ReadInt32LittleEndian(rest);
		byte status = rest[7];
		ReadOnlySpan<byte> after = rest[12..];
		if (status is 1 or 2 or 4 or 8) return Placed(after);
		if (status == 16) return after.Length >= 1 && Results(after[1..]);
		return Results(after) || Placed(after);

		bool Placed(ReadOnlySpan<byte> place)
		{
			if (place.Length < 12) return false;
			int mark = landings.Count;
			landings.Add(new(objectId, BinaryPrimitives.ReadSingleLittleEndian(place), BinaryPrimitives.ReadSingleLittleEndian(place[4..]),
				BinaryPrimitives.ReadSingleLittleEndian(place[8..])));
			if (Results(place[12..])) return true;
			landings.RemoveRange(mark, landings.Count - mark);
			return false;
		}

		bool Results(ReadOnlySpan<byte> results)
		{
			if (results.IsEmpty) return false;
			int count = results[0];
			results = results[1..];
			for (int index = 0; index < count; index++)
			{
				if (results.Length < 7) return false;
				int length = 7 + (results[6] is 0 or 2 ? 0 : results[6] is 8 or 10 ? 12 : 28);
				if (results.Length < length) return false;
				results = results[length..];
			}
			int mark = landings.Count;
			if (TryReadCastEffects(results, left - 1, landings)) return true;
			landings.RemoveRange(mark, landings.Count - mark);
			return false;
		}
	}

	private static IReadOnlyDictionary<string, object?> DecodeUnwrapItem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()), ("count", r.ReadByte()));
		if (r.Remaining != 0) throw new InvalidDataException("Unwrap result has unexpected trailing bytes.");
		return fields;
	}

	/// <summary>
	/// Upstream d254ea172 writes the object id as one 64-bit value where the old packet wrote two 32-bit words, which is
	/// the same bytes on the wire: the second word was always zero and is the high half.
	/// </summary>
	private static IReadOnlyDictionary<string, object?> DecodeDecomposable(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32(), reserved = r.ReadInt32();
		var choices = new BotDecomposableChoice[r.ReadByte()];
		for (int i = 0; i < choices.Length; i++)
			choices[i] = new(r.ReadByte(), r.ReadInt32(), r.ReadInt32(), r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
		if (r.Remaining != 0) throw new InvalidDataException("Decomposable preview has unexpected trailing bytes.");
		return Fields(("objectId", objectId), ("reserved", reserved), ("choices", choices));
	}

	/// <summary>The answer to a pick in the decomposable window: the box's object id and whether the item was handed out.</summary>
	private static IReadOnlyDictionary<string, object?> DecodeDecomposableResult(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32(), reserved = r.ReadInt32();
		byte result = r.ReadByte();
		if (r.Remaining != 0 || reserved != 0 || result > 1)
			throw new InvalidDataException("Invalid decomposable selection result.");
		return Fields(("objectId", objectId), ("reserved", reserved), ("granted", result == 0));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTuneResult(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32(), tuningScrollItemId = r.ReadInt32();
		byte statBonusId = r.ReadByte();
		var enchantment = BotItemBlobDecoder.ReadEnchantment(ref r);
		byte hideManastoneSlots = r.ReadByte(), disableCancel = r.ReadByte();
		if (r.Remaining != 0 || hideManastoneSlots > 1 || disableCancel > 1)
			throw new InvalidDataException("Invalid tuning result length or flags.");
		return Fields(("objectId", objectId), ("tuningScrollItemId", tuningScrollItemId), ("statBonusId", statBonusId),
			("enchantment", enchantment), ("showManastoneSlots", hideManastoneSlots == 0), ("tuneCancelPossible", disableCancel == 0));
	}

	private static IReadOnlyDictionary<string, object?> DecodeAttackStatus(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()), ("writtenValue", r.ReadInt32()), ("typeId", r.ReadByte()),
			("hpOrMp", r.ReadByte()), ("skillId", r.ReadUInt16()), ("logId", r.ReadByte()), ("criticalDisplayCode", r.ReadByte()));
		if (r.Remaining != 0) throw new InvalidDataException("Attack status has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeAttack(ReadOnlySpan<byte> body)
	{
		// The hit list is variable-length; the fixed prefix is enough to identify
		// incoming attackers without using server-only aggro state.
		var r = new PacketBodyReader(body);
		int attackerObjId = r.ReadInt32();
		byte attackno = r.ReadByte();
		ushort time = r.ReadUInt16();
		byte attackTypeId = r.ReadByte(), attackHandId = r.ReadByte();
		int targetObjId = r.ReadInt32();
		return Fields(("attackerObjId", attackerObjId), ("attackno", attackno), ("time", time),
			("attackTypeId", attackTypeId), ("attackHandId", attackHandId), ("targetObjId", targetObjId));
	}

	private static IReadOnlyDictionary<string, object?> DecodeItemUsage(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("playerObjId", r.ReadInt32()), ("targetObjId", r.ReadInt32()), ("itemObjId", r.ReadInt32()),
			("itemId", r.ReadInt32()), ("castTime", r.ReadInt32()), ("animationId", r.ReadByte()), ("suppressAnimation", r.ReadByte() != 0));
		int count = r.ReadUInt16();
		r.Skip(checked(count * 4));
		if (r.Remaining != 0) throw new InvalidDataException("Item-use animation has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeRecipeList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var ids = new int[r.ReadUInt16()];
		var limits = new Dictionary<int, byte>();
		for (int i = 0; i < ids.Length; i++)
		{
			ids[i] = r.ReadInt32();
			limits[ids[i]] = r.ReadByte();
		}
		return Fields(("recipeIds", ids), ("productionLimits", limits));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLearnRecipe(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int id = r.ReadInt32();
		return Fields(("recipeId", id), ("productionLimit", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeRecipeDelete(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("recipeId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeKey(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("encodedKey", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeVersionCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("answerId", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLoginCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var ok = r.ReadInt32() == 0;
		r.Skip(4 + 128 * 3 + 64 * 3);
		var maps = new List<IReadOnlyDictionary<string, object?>>();
		var mapCount = r.ReadUInt16();
		for (var i = 0; i < mapCount; i++)
			maps.Add(Fields(("mapId", r.ReadInt32()), ("twinCount", r.ReadUInt16())));
		return Fields(("ok", ok), ("worldMaps", maps), ("accountName", r.ReadString()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCharacterList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var playOk2 = r.ReadInt32();
		var characterCount = r.ReadByte();
		var characters = new List<IReadOnlyDictionary<string, object?>>(characterCount);
		for (var i = 0; i < characterCount; i++)
			characters.Add(DecodeCharacterSummary(ref r));
		return Fields(("playOk2", playOk2), ("characterCount", characterCount), ("characters", characters));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCreateCharacter(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var responseCode = r.ReadInt32();
		return responseCode == 0
			? Fields(("responseCode", responseCode), ("character", DecodeCharacterSummary(ref r)))
			: Fields(("responseCode", responseCode));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDeleteCharacter(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("responseCode", r.ReadInt32()), ("playerObjId", r.ReadInt32()), ("deletionTime", r.ReadInt32()));
		if (r.Remaining != 0) throw new InvalidDataException("Character deletion has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeRestoreCharacter(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int responseCode = r.ReadInt32();
		var fields = Fields(("responseCode", responseCode), ("chaOid", r.ReadInt32()), ("success", responseCode == 0));
		if (r.Remaining != 0) throw new InvalidDataException("Character restoration has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeCharacterSummary(ref PacketBodyReader r)
	{
		var objectId = r.ReadInt32();
		var nameBytes = r.ReadBytes((25 + 1) * sizeof(char));
		var name = System.Text.Encoding.Unicode.GetString(nameBytes).TrimEnd('\0');
		var gender = r.ReadInt32();
		var race = r.ReadInt32();
		var playerClass = r.ReadInt32();
		var appearance = new BotCharacterAppearance(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(),
			Convert.ToHexString(r.ReadBytes(52)), r.ReadSingle());
		var templateId = r.ReadInt32();
		var mapId = r.ReadInt32();
		var x = r.ReadSingle();
		var y = r.ReadSingle();
		var z = r.ReadSingle();
		var heading = r.ReadInt32();
		var level = r.ReadUInt16();
		r.Skip(sizeof(ushort)); // reserved
		int titleId = r.ReadInt32();
		r.Skip(sizeof(int)); // legion id
		r.Skip((40 + 1) * sizeof(char)); // fixed legion name
		r.Skip(sizeof(ushort)); // legion membership flag
		var lastOnlineEpochSeconds = r.ReadInt32();
		r.Skip(16 * (sizeof(byte) + (3 * sizeof(int)))); // visible equipment
		r.Skip(6 * sizeof(int) + 68); // client-reserved blocks
		var deletionTimeSeconds = r.ReadInt32();
		r.Skip(2 * sizeof(ushort)); // helmet display and reserved
		r.Skip(4 * sizeof(int)); // mail counters
		r.Skip(sizeof(long)); // broker proceeds
		r.Skip(7 * sizeof(int)); // reserved values and character-ban timestamps
		_ = r.ReadString(); // character-ban reason
		return Fields(
			("objectId", objectId), ("name", name), ("gender", gender), ("race", race),
			("playerClass", playerClass), ("templateId", templateId), ("mapId", mapId),
			("x", x), ("y", y), ("z", z), ("heading", heading), ("level", level),
			("appearance", appearance), ("titleId", titleId),
			("lastOnlineEpochSeconds", lastOnlineEpochSeconds), ("deletionTimeSeconds", deletionTimeSeconds));
	}

	private static IReadOnlyDictionary<string, object?> DecodeEnterWorldCheck(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("msg", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayerSpawn(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var worldChannel = r.ReadInt32();
		var worldId = r.ReadInt32();
		r.Skip(4);
		var personal = r.ReadByte() != 0;
		var fields = Fields(
			("worldChannel", worldChannel), ("worldId", worldId), ("personal", personal),
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("heading", r.ReadByte()));
		r.Skip(12);
		fields["beginner"] = r.ReadByte() != 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayMovie(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("isMovie", r.ReadByte() != 0), ("objectId", r.ReadInt32()), ("questId", r.ReadInt32()),
			("cutsceneId", r.ReadInt32()));
		r.Skip(1);
		fields["canSkip"] = r.ReadByte() == 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeChatInit(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("token", r.ReadBytes(r.ReadInt32())));
	}

	private static IReadOnlyDictionary<string, object?> DecodeResurrect(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("casterName", r.ReadString()), ("skillId", r.ReadUInt16()), ("reserved", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeMessage(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte chatType = r.ReadByte();
		var fields = Fields(
			("chatType", chatType), ("senderRace", r.ReadByte()), ("senderObjectId", r.ReadInt32()),
			("senderName", r.ReadString()), ("message", r.ReadString()));
		if (chatType == (byte)Aion.GameServer.Model.ChatType.SHOUT)
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
		}
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodePlayerInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()));
		r.Skip(4 + 4 + 4 + 1 + 4 + 1);
		fields["race"] = r.ReadByte();
		fields["playerClass"] = r.ReadByte();
		fields["gender"] = r.ReadByte();
		fields["state"] = r.ReadUInt16();
		r.Skip(8);
		fields["heading"] = r.ReadByte();
		fields["name"] = r.ReadString();
		fields["titleId"] = r.ReadUInt16();
		r.Skip(2 * sizeof(ushort)); // mentor flag, casting skill
		var legionId = r.ReadInt32();
		if (legionId == 0)
			r.Skip(8);
		else
		{
			r.Skip(6); // emblem id, type and ARGB
			_ = r.ReadString();
		}
		r.Skip(sizeof(byte) + sizeof(ushort) + sizeof(byte)); // hp%, dp, reserved
		var equipmentMask = unchecked((uint)r.ReadInt32());
		r.Skip(System.Numerics.BitOperations.PopCount(equipmentMask) * 16);
		int skin = r.ReadInt32(), hair = r.ReadInt32(), eye = r.ReadInt32(), lip = r.ReadInt32();
		byte[] features = r.ReadBytes(50);
		int voice = r.ReadByte();
		fields["appearance"] = new BotCharacterAppearance(voice, skin, hair, eye, lip, Convert.ToHexString([.. features, 0, 0]), r.ReadSingle());
		r.Skip(2 * sizeof(float)); // scale/gravity
		fields["movementSpeed"] = r.ReadSingle();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeEmotion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("senderObjectId", r.ReadInt32()), ("emotionType", r.ReadByte()),
			("state", r.ReadUInt16()), ("movementSpeed", r.ReadSingle()));
		if (fields["emotionType"] is byte emotionType && emotionType == (byte)Aion.GameServer.Model.EmotionType.CHANGE_SPEED)
		{
			fields["baseAttackSpeed"] = r.ReadUInt16();
			fields["currentAttackSpeed"] = r.ReadUInt16();
			fields["reserved"] = r.ReadByte();
		}
		else if ((byte)fields["emotionType"]! == (byte)Aion.GameServer.Model.EmotionType.START_FLYTELEPORT)
			fields["teleportId"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeUseObject(ReadOnlySpan<byte> body)
	{
		// Java ce54b7931 SM_USE_OBJECT.writeImpl: D player, D target,
		// D duration, C action. Action 2 carries duration 0 on observer abort,
		// but the original use duration on successful completion.
		var r = new PacketBodyReader(body);
		return Fields(
			("playerObjectId", r.ReadInt32()),
			("targetObjectId", r.ReadInt32()),
			("durationMs", r.ReadInt32()),
			("actionType", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeNpcInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()),
			("npcId", r.ReadInt32()), ("visualNpcId", r.ReadInt32()), ("creatureType", r.ReadByte()),
			("state", r.ReadUInt16()), ("heading", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeGatherableInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("objectId", r.ReadInt32()),
			("staticId", r.ReadInt32()), ("templateId", r.ReadInt32()));
		var objectState = r.ReadUInt16();
		fields["objectState"] = objectState;
		fields["isStatic"] = objectState is 0x09 or 0x0A;
		fields["open"] = objectState switch
		{
			0x09 => true,
			0x0A => false,
			_ => null,
		};
		fields["heading"] = r.ReadByte();
		fields["l10nId"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeGatherUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("skillId", r.ReadUInt16()), ("action", r.ReadByte()), ("itemId", r.ReadInt32()),
			("success", r.ReadInt32()), ("failure", r.ReadInt32()),
			("executionSpeed", r.ReadInt32()), ("delay", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeMove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("objectId", r.ReadInt32()), ("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()),
			("heading", r.ReadByte()), ("movementMask", r.ReadByte()));
		// Java SM_MOVE.writeImpl: POSITION|MANUAL (0x80|0x40) is followed by three floats. With ABSOLUTE
		// (0x20) they are the move controller's target (an NPC's walk or chase destination, which a real
		// client animates toward); without it they are a player's relative vector.
		byte mask = (byte)fields["movementMask"]!;
		if ((mask & 0xC0) == 0xC0 && r.Remaining >= 12)
		{
			string prefix = (mask & 0x20) != 0 ? "target" : "vector";
			fields[prefix + "X"] = r.ReadSingle();
			fields[prefix + "Y"] = r.ReadSingle();
			fields[prefix + "Z"] = r.ReadSingle();
		}
		return fields;
	}

	// Java SM_FORCED_MOVE.writeImpl: the effector, the creature moved, a constant 16, then where it landed. Sent by
	// knockback, stumble and pull effects (StaggerEffect, StumbleEffect, PulledEffect) and the anti-hack move-back.
	// Java SM_POSITION.writeImpl: D the object, three F its place, C its heading. The server puts the object there at
	// once and the client's own movement of it ends: FearEffect.endEffect sends it to a player whom the fear made run.
	private static IReadOnlyDictionary<string, object?> DecodePosition(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()), ("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()),
			("heading", r.ReadByte()));
		if (r.Remaining != 0) throw new InvalidDataException("Position has unexpected trailing bytes.");
		return fields;
	}

	// Java SM_SHOW_BRAND.writeImpl: H how many marks, then for each D 1, D the mark's icon and D the object it is on
	// (0 takes the mark off). TemporaryPlayerTeam.updateBrand sends one mark to the whole team when its leader sets it;
	// sendBrands sends a member that joins every mark of the team, and sixteen removals when the team has none.
	private static IReadOnlyDictionary<string, object?> DecodeShowBrand(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int count = r.ReadUInt16();
		var brands = new List<IReadOnlyDictionary<string, object?>>(count);
		for (int index = 0; index < count; index++)
			brands.Add(Fields(("type", r.ReadInt32()), ("iconId", r.ReadInt32()), ("targetObjectId", r.ReadInt32())));
		if (r.Remaining != 0) throw new InvalidDataException("Show brand has unexpected trailing bytes.");
		return Fields(("brands", brands));
	}

	private static IReadOnlyDictionary<string, object?> DecodeForcedMove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("effectorObjectId", r.ReadInt32()), ("objectId", r.ReadInt32()), ("unknown", r.ReadByte()),
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()));
		if (r.Remaining != 0) throw new InvalidDataException("Forced move has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeCraftUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("skillId", r.ReadUInt16()), ("action", r.ReadByte()), ("itemId", r.ReadInt32()),
			("success", r.ReadInt32()), ("failure", r.ReadInt32()), ("executionSpeed", r.ReadInt32()),
			("delay", r.ReadInt32()), ("msgId", r.ReadInt32()), ("itemName", r.ReadString()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWindstream(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int state = r.ReadInt32();
		byte accepted = r.ReadByte();
		return Fields(("state", state), ("accepted", accepted), ("unk1", state), ("unk2", accepted));
	}

	private static IReadOnlyDictionary<string, object?> DecodeAbnormalState(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int abnormals = r.ReadInt32();
		r.Skip(2 * sizeof(int));
		byte slot = r.ReadByte();
		ushort effectCount = r.ReadUInt16();
		var effects = new List<IReadOnlyDictionary<string, object?>>(effectCount);
		for (int index = 0; index < effectCount; index++)
		{
			effects.Add(Fields(
				("effectorId", r.ReadInt32()), ("skillId", r.ReadUInt16()), ("skillLevel", r.ReadByte()),
				("targetSlot", r.ReadByte()), ("remainingMillis", r.ReadInt32())));
		}
		if (r.Remaining != 0) throw new InvalidDataException("Abnormal-state snapshot has unexpected trailing bytes.");
		return Fields(("abnormals", abnormals), ("slot", slot), ("effectCount", effectCount), ("effects", effects));
	}

	// Java SM_ABNORMAL_EFFECT.writeImpl: the creature, 1 for a monster or 2 for a player, its abnormal states as the bits
	// of AbnormalState, and its visible effects; a player's rows begin with the effector.
	private static IReadOnlyDictionary<string, object?> DecodeAbnormalEffect(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32();
		byte effectType = r.ReadByte();
		if (effectType is not (1 or 2)) throw new InvalidDataException($"Creature effects of an unknown kind {effectType}.");
		r.Skip(sizeof(int));
		int abnormals = r.ReadInt32();
		r.Skip(sizeof(int));
		byte slots = r.ReadByte();
		ushort effectCount = r.ReadUInt16();
		var effects = new List<IReadOnlyDictionary<string, object?>>(effectCount);
		for (int index = 0; index < effectCount; index++)
		{
			effects.Add(Fields(
				("effectorId", effectType == 2 ? r.ReadInt32() : 0), ("skillId", r.ReadUInt16()), ("skillLevel", r.ReadByte()),
				("targetSlot", r.ReadByte()), ("remainingMillis", r.ReadInt32())));
		}
		if (r.Remaining != 0) throw new InvalidDataException("Creature effects have unexpected trailing bytes.");
		return Fields(("objectId", objectId), ("effectType", effectType), ("abnormals", abnormals), ("slots", slots),
			("effectCount", effectCount), ("effects", effects));
	}

	private static IReadOnlyDictionary<string, object?> DecodeWindstreamAnnounce(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int flyPathType = r.ReadInt32();
		return Fields(("flyPathType", flyPathType), ("bidirectional", flyPathType), ("mapId", r.ReadInt32()),
			("streamId", r.ReadInt32()), ("state", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDelete(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("objectId", r.ReadInt32()), ("animationId", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTeleport(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("portAnimation", r.ReadByte()), ("mapId", r.ReadInt32()), ("destinationId", r.ReadInt32()),
			("x", r.ReadSingle()), ("y", r.ReadSingle()), ("z", r.ReadSingle()), ("heading", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDialog(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("targetObjectId", r.ReadInt32()), ("dialogPageId", r.ReadUInt16()), ("questId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var code = r.ReadInt32();
		var parameters = new[] { r.ReadString(), r.ReadString(), r.ReadString() };
		r.Skip(5);
		return Fields(
			("code", code), ("params", parameters), ("senderId", r.ReadInt32()), ("rangeOrCooldownSeconds", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCloseQuestion(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(4);
		return Fields(("messageId", r.ReadInt32()), ("params", new[] { r.ReadString(), r.ReadString(), r.ReadString() }));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSystemMessage(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var chatType = r.ReadByte();
		r.Skip(1);
		var senderObjectId = r.ReadInt32();
		var messageId = r.ReadInt32();
		var parameters = ReadStrings(ref r, r.ReadByte());
		var specialParameters = ReadStrings(ref r, r.ReadByte());
		return Fields(
			("chatType", chatType), ("senderObjectId", senderObjectId), ("msgId", messageId),
			("name", SystemMessageNames.GetNameOrNull(messageId)), ("params", parameters), ("specialParams", specialParameters));
	}

	private static IReadOnlyDictionary<string, object?> DecodeStats(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("objectId", r.ReadInt32()));
		r.Skip(4 + 12 * 2);
		fields["level"] = r.ReadUInt16();
		r.Skip(3 * 2);
		fields["expNeeded"] = r.ReadInt64();
		fields["expRecoverable"] = r.ReadInt64();
		fields["expShown"] = r.ReadInt64();
		r.Skip(4);
		fields["maxHp"] = r.ReadInt32();
		fields["currentHp"] = r.ReadInt32();
		fields["maxMp"] = r.ReadInt32();
		fields["currentMp"] = r.ReadInt32();
		fields["maxDp"] = r.ReadUInt16();
		fields["dp"] = r.ReadUInt16();
		fields["maxFp"] = r.ReadInt32();
		fields["currentFp"] = r.ReadInt32();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeHp(ReadOnlySpan<byte> body) => DecodePair(body, "currentHp", "maxHp");

	private static IReadOnlyDictionary<string, object?> DecodeMp(ReadOnlySpan<byte> body) => DecodePair(body, "currentMp", "maxMp");

	private static IReadOnlyDictionary<string, object?> DecodePair(ReadOnlySpan<byte> body, string first, string second)
	{
		var r = new PacketBodyReader(body);
		return Fields((first, r.ReadInt32()), (second, r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDp(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("currentDp", r.ReadUInt16()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeExp(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("currentExp", r.ReadInt64()), ("recoverableExp", r.ReadInt64()), ("maxExp", r.ReadInt64()),
			("rep1", r.ReadInt64()), ("rep2", r.ReadInt64()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeFlyTime(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("currentFp", r.ReadInt32()), ("maxFp", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDie(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(
			("allowReviveBySkill", r.ReadByte() != 0), ("allowReviveByItem", r.ReadByte() != 0),
			("remainingKiskTimeSeconds", r.ReadInt32()), ("allowInstanceRevive", r.ReadByte() != 0),
			("invasion", r.ReadByte() == 0x80));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var first = r.ReadByte() != 0;
		var npc = r.ReadByte();
		var quest = r.ReadByte();
		var item = r.ReadByte();
		var items = ReadInventoryItems(ref r, r.ReadUInt16());
		return Fields(("firstPacket", first), ("npcExpands", npc), ("questExpands", quest), ("itemExpands", item), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryAdd(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var mask = r.ReadUInt16();
		return Fields(("addMask", mask), ("items", ReadInventoryItems(ref r, r.ReadUInt16())));
	}

	private static IReadOnlyDictionary<string, object?> DecodeInventoryUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var objectId = r.ReadInt32();
		var description = r.ReadString();
		var blob = r.ReadLengthPrefixedBlob();
		var info = BotItemBlobDecoder.Decode(blob);
		var general = info.General;
		// Full blobs always carry GENERAL_INFO; absence of WRAP_INFO in a full blob means zero wraps.
		var details = general == null ? info.Details : info.Details with { PackCount = info.Details.PackCount ?? 0 };
		return Fields(
			("objectId", objectId), ("desc", description), ("blob", blob),
			("itemMask", general?.ItemMask), ("itemCount", general?.ItemCount), ("itemCreator", general?.Creator),
			("details", details),
			("updateMask", r.Remaining >= 2 ? r.ReadUInt16() : null));
	}

	private static IReadOnlyDictionary<string, object?> DecodeDeleteItem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("itemObjectId", r.ReadInt32()), ("deleteType", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCubeUpdate(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte action = r.ReadByte(), value = r.ReadByte();
		var fields = Fields(("action", action), ("actionValue", value));
		if (action == 0)
		{
			fields["itemsCount"] = r.ReadInt32();
			fields["npcExpands"] = r.ReadByte();
			fields["questExpands"] = r.ReadByte();
			fields["itemExpands"] = r.ReadByte();
		}
		if (r.Remaining != 0) throw new InvalidDataException("Cube update has unexpected trailing bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var count = r.ReadUInt16();
		var silent = r.ReadByte() != 0;
		var skills = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			skills.Add(Fields(
				("skillId", r.ReadUInt16()), ("level", r.ReadUInt16()), ("reserved", r.ReadByte()),
				("professionBarSize", r.ReadByte()), ("flag", r.ReadInt32()), ("skillType", r.ReadByte())));
		}
		return Fields(("silentUpdate", silent), ("skills", skills), ("messageId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillRemove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("skillId", r.ReadUInt16()), ("levelOrProfessionFlag", r.ReadByte()), ("skillType", r.ReadByte()));
		if (body.Length != 4) throw new InvalidDataException("SM_SKILL_REMOVE must contain exactly four bytes.");
		return result;
	}

	// Java SM_SKILL_ACTIVATION.writeImpl: the skill, 0 for a toggle or 1 for a removed stigma, and whether it is on.
	private static IReadOnlyDictionary<string, object?> DecodeSkillActivation(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("skillId", r.ReadUInt16()), ("kind", r.ReadInt32()), ("active", r.ReadByte() != 0));
		if (body.Length != 7) throw new InvalidDataException("SM_SKILL_ACTIVATION must contain exactly seven bytes.");
		return result;
	}

	// Java SM_RIDE_ROBOT.writeImpl: a player, and the mech it rides, 0 for none.
	private static IReadOnlyDictionary<string, object?> DecodeRideRobot(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("objectId", r.ReadInt32()), ("robotId", r.ReadInt32()));
		if (body.Length != 8) throw new InvalidDataException("SM_RIDE_ROBOT must contain exactly eight bytes.");
		return result;
	}

	// Java SM_SUMMON_PANEL.writeImpl: the master's own spirit, with its level, HP, attack, defences and the time it lives.
	private static IReadOnlyDictionary<string, object?> DecodeSummonPanel(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int objectId = r.ReadInt32();
		ushort level = r.ReadUInt16();
		r.Skip(2 * sizeof(int));
		int currentHp = r.ReadInt32(), maxHp = r.ReadInt32(), attack = r.ReadInt32(), physicalDefence = r.ReadInt32(), magicalDefence = r.ReadInt32();
		r.Skip(sizeof(ushort));
		int liveTime = r.ReadInt32();
		if (r.Remaining != 0) throw new InvalidDataException("SM_SUMMON_PANEL has unexpected trailing bytes.");
		return Fields(("objectId", objectId), ("level", level), ("currentHp", currentHp), ("maxHp", maxHp), ("attack", attack),
			("physicalDefence", physicalDefence), ("magicalDefence", magicalDefence), ("liveTime", liveTime));
	}

	// Java SM_SUMMON_PANEL_REMOVE.writeImpl: the skill that summoned the spirit that is gone, and 1 when it names one.
	private static IReadOnlyDictionary<string, object?> DecodeSummonPanelRemove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("skillId", r.ReadUInt16()), ("named", r.ReadByte() != 0));
		if (body.Length != 3) throw new InvalidDataException("SM_SUMMON_PANEL_REMOVE must contain exactly three bytes.");
		return result;
	}

	// Java SM_SUMMON_OWNER_REMOVE.writeImpl: the spirit that is gone.
	private static IReadOnlyDictionary<string, object?> DecodeSummonOwnerRemove(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("summonObjId", r.ReadInt32()));
		if (body.Length != 4) throw new InvalidDataException("SM_SUMMON_OWNER_REMOVE must contain exactly four bytes.");
		return result;
	}

	// Java SM_SUMMON_USESKILL.writeImpl: the spirit, the skill it is to cast with its level, and the target.
	private static IReadOnlyDictionary<string, object?> DecodeSummonUseSkill(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var result = Fields(("summonId", r.ReadInt32()), ("skillId", r.ReadUInt16()), ("skillLvl", r.ReadByte()), ("targetId", r.ReadInt32()));
		if (body.Length != 11) throw new InvalidDataException("SM_SUMMON_USESKILL must contain exactly eleven bytes.");
		return result;
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillCooldown(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var count = r.ReadUInt16();
		var notify = r.ReadByte() != 0;
		var entries = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			entries.Add(Fields(("skillId", r.ReadUInt16()), ("remainingSeconds", r.ReadInt32()), ("durationMillis", r.ReadInt32())));
		return Fields(("notify", notify), ("cooldowns", entries));
	}

	private static IReadOnlyDictionary<string, object?> DecodeCastSpell(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("objectId", r.ReadInt32()), ("spellId", r.ReadUInt16()), ("level", r.ReadByte()), ("targetType", r.ReadByte()));
		var targetType = (byte)fields["targetType"]!;
		if (targetType is 0 or 3 or 4)
			fields["targetObjectId"] = r.ReadInt32();
		else
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
			if (targetType == 2)
				r.Skip(32);
		}
		fields["castDuration"] = r.ReadUInt16();
		r.Skip(1);
		fields["castSpeed"] = r.ReadSingle();
		fields["boost"] = r.ReadByte() != 0;
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeCastSpellResult(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("effectorId", r.ReadInt32()), ("targetType", r.ReadByte()));
		var targetType = (byte)fields["targetType"]!;
		if (targetType is 0 or 3 or 4)
			fields["targetId"] = r.ReadInt32();
		else
		{
			fields["x"] = r.ReadSingle();
			fields["y"] = r.ReadSingle();
			fields["z"] = r.ReadSingle();
			if (targetType == 2)
				r.Skip(32);
		}
		fields["skillId"] = r.ReadUInt16();
		fields["skillLevel"] = r.ReadByte();
		fields["cooldown"] = r.ReadInt32();
		fields["hitTime"] = r.ReadUInt16();
		r.Skip(1);
		fields["flags"] = r.ReadByte();
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeSkillCancel(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("objectId", r.ReadInt32()), ("skillId", r.ReadUInt16()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(2);
		var count = unchecked((ushort)-r.ReadUInt16());
		var quests = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			quests.Add(Fields(("questId", r.ReadInt32()), ("status", r.ReadByte()), ("stepAndFlags", r.ReadInt32()), ("completeCount", r.ReadByte())));
		return Fields(("quests", quests));
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestAction(ReadOnlySpan<byte> body)
	{
		if (body.IsEmpty)
			return Fields(("suppressed", true));
		var r = new PacketBodyReader(body);
		var action = r.ReadByte();
		var fields = Fields(("action", action), ("questId", r.ReadInt32()));
		switch (action)
		{
			case 1:
			case 2:
				fields["status"] = r.ReadByte();
				r.Skip(1);
				fields["stepAndFlags"] = r.ReadInt32();
				break;
			case 4:
				fields["timer"] = r.ReadInt32();
				break;
			case 5:
				fields["sharerId"] = r.ReadInt32();
				fields["shareInAlliance"] = r.ReadInt32() != 0;
				break;
		}
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeQuestCompletedList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		r.Skip(1);
		var updateMode = r.ReadByte();
		var count = unchecked((ushort)-r.ReadUInt16());
		var quests = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
			quests.Add(Fields(("questId", r.ReadInt32()), ("completeCount", r.ReadByte()), ("nonRepeatable", r.ReadByte() != 0)));
		return Fields(("updateMode", updateMode), ("quests", quests));
	}

	// AK-00: Java SM_GAME_TIME writes the game time as minutes since 01.01.0000 00:00 (writeD).
	private static IReadOnlyDictionary<string, object?> DecodeGameTime(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("minutes", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLootStatus(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("targetObjectId", r.ReadInt32()), ("status", r.ReadByte()), ("lootEffectId", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeLootItemList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var target = r.ReadInt32();
		var count = r.ReadByte();
		var items = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			items.Add(Fields(
				("index", r.ReadByte()), ("itemId", r.ReadInt32()), ("count", r.ReadInt32()),
				("optionalSocket", r.ReadByte()), ("reserved1", r.ReadByte()), ("reserved2", r.ReadByte()),
				("requiresConfirmation", r.ReadByte() != 0)));
		}
		return Fields(("targetObjectId", target), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodePrices(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("globalPrices", r.ReadByte()), ("globalModifier", r.ReadByte()), ("taxes", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeChannelInfo(ReadOnlySpan<byte> body)
	{
		if (body.Length != 8) throw new InvalidDataException("SM_CHANNEL_INFO must contain two int32 fields.");
		var r = new PacketBodyReader(body);
		return Fields(("currentChannel", r.ReadInt32()), ("instanceCount", r.ReadInt32()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeSellItem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int target = r.ReadInt32();
		byte type = r.ReadByte();
		int rate = r.ReadInt32();
		bool buy = r.ReadByte() != 0, sell = r.ReadByte() != 0;
		int count = r.ReadUInt16();
		var tabs = new int[count];
		for (int i = 0; i < count; i++) tabs[i] = r.ReadInt32();
		return Fields(("targetObjectId", target), ("objectId", target), ("tradeNpcType", type),
			("buyPriceRate", rate), ("showBuyTab", buy), ("showSellTab", sell), ("tabIds", tabs));
	}

	private static IReadOnlyDictionary<string, object?> DecodeRepurchase(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		int target = r.ReadInt32();
		int action = r.ReadInt32();
		int count = r.ReadUInt16();
		var items = new List<IReadOnlyDictionary<string, object?>>(count);
		for (int i = 0; i < count; i++)
		{
			int objectId = r.ReadInt32(), itemId = r.ReadInt32();
			string desc = r.ReadString();
			byte[] blob = r.ReadLengthPrefixedBlob();
			BotItemGeneralInfo? general = DecodeItemGeneralInfo(blob);
			items.Add(Fields(("objectId", objectId), ("itemId", itemId), ("desc", desc), ("blob", blob),
				("itemCount", general?.ItemCount), ("repurchasePrice", r.ReadInt64())));
		}
		return Fields(("targetObjectId", target), ("action", action), ("items", items));
	}

	private static IReadOnlyDictionary<string, object?> DecodeTradeList(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(
			("targetObjectId", r.ReadInt32()), ("tradeNpcType", r.ReadByte()), ("buyPriceModifier", r.ReadInt32()));
		r.Skip(4);
		fields["showBuyTab"] = r.ReadByte() != 0;
		fields["showSellTab"] = r.ReadByte() != 0;
		var tabCount = r.ReadUInt16();
		var tabs = new int[tabCount];
		for (var i = 0; i < tabCount; i++)
			tabs[i] = r.ReadInt32();
		fields["tabs"] = tabs;
		var limitedCount = r.ReadUInt16();
		var limited = new List<IReadOnlyDictionary<string, object?>>(limitedCount);
		for (var i = 0; i < limitedCount; i++)
			limited.Add(Fields(("itemId", r.ReadInt32()), ("buyCount", r.ReadUInt16()), ("sellLimit", r.ReadUInt16())));
		fields["limitedItems"] = limited;
		if (r.Remaining != 0) throw new InvalidDataException("Trailing trade-list bytes.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeGroupInfo(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var fields = Fields(("groupId", r.ReadInt32()), ("leaderId", r.ReadInt32()), ("mapId", r.ReadInt32()));
		fields["lootRules"] = ReadAllianceLootRules(ref r);
		r.Skip(4 + 1);
		fields["teamType"] = r.ReadInt32();
		fields["teamSubType"] = r.ReadInt32();
		fields["messageId"] = r.ReadInt32(); fields["message"] = r.ReadString();
		RequireSocialEnd(r);
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeMailService(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		var service = r.ReadByte();
		var fields = Fields(("serviceId", service));
		switch (service)
		{
			case 0:
				fields["totalCount"] = r.ReadUInt16(); fields["unreadCount"] = r.ReadUInt16();
				fields["expressCount"] = r.ReadUInt16(); fields["blackCloudCount"] = r.ReadUInt16();
				break;
			case 1:
				fields["messageId"] = r.ReadByte();
				break;
			case 2:
				fields["recipientId"] = r.ReadInt32(); r.Skip(1);
				int signedCount = r.ReadInt16();
				fields["lastPacket"] = signedCount <= 0;
				var letters = new List<IReadOnlyDictionary<string, object?>>();
				for (int i = 0; i < Math.Abs(signedCount); i++)
					letters.Add(Fields(("letterId", r.ReadInt32()), ("sender", r.ReadString()), ("title", r.ReadString()),
						("isRead", r.ReadByte() != 0), ("itemObjectId", r.ReadInt32()), ("itemId", r.ReadInt32()),
						("kinah", r.ReadInt64()), ("letterType", r.ReadByte())));
				fields["letters"] = letters;
				break;
			case 3:
				fields["recipientId"] = r.ReadInt32();
				fields["packedCounts"] = r.ReadInt32(); fields["specialUnreadCount"] = r.ReadInt32();
				fields["letterId"] = r.ReadInt32(); r.Skip(4);
				fields["sender"] = r.ReadString(); fields["title"] = r.ReadString(); fields["message"] = r.ReadString();
				int itemObjectId = r.ReadInt32();
				fields["itemObjectId"] = itemObjectId; fields["itemId"] = r.ReadInt32();
				r.Skip(8);
				if (itemObjectId != 0)
				{
					fields["desc"] = r.ReadString();
					byte[] blob = r.ReadLengthPrefixedBlob();
					fields["blob"] = blob; fields["itemCount"] = DecodeItemGeneralInfo(blob)?.ItemCount;
				}
				else { r.Skip(4); fields["itemCount"] = 0L; }
				// Java deliberately writes only 32 bits here (the list above carries the full 64-bit amount).
				fields["kinah"] = r.ReadInt32(); r.Skip(5);
				fields["timestampSeconds"] = r.ReadInt32(); fields["letterType"] = r.ReadByte();
				break;
			case 5:
				fields["letterId"] = r.ReadInt32(); fields["attachmentType"] = r.ReadByte(); fields["success"] = r.ReadByte();
				break;
			case 6:
				fields["packedCounts"] = r.ReadInt32(); fields["specialUnreadCount"] = r.ReadInt32();
				var deleted = new int[r.ReadUInt16()];
				for (int i = 0; i < deleted.Length; i++) deleted[i] = r.ReadInt32();
				fields["deletedIds"] = deleted;
				break;
			default: throw new InvalidDataException($"Unknown mail service {service}.");
		}
		if (r.Remaining != 0) throw new InvalidDataException($"Mail service {service} left {r.Remaining} bytes unread.");
		return fields;
	}

	private static IReadOnlyDictionary<string, object?> DecodeExchangeRequest(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("receiver", r.ReadString()));
	}

	private static List<IReadOnlyDictionary<string, object?>> ReadInventoryItems(ref PacketBodyReader r, int count)
	{
		var items = new List<IReadOnlyDictionary<string, object?>>(count);
		for (var i = 0; i < count; i++)
		{
			var objectId = r.ReadInt32();
			var itemId = r.ReadInt32();
			var description = r.ReadString();
			var blob = r.ReadLengthPrefixedBlob();
			var info = BotItemBlobDecoder.Decode(blob);
			var general = info.General;
			items.Add(Fields(
				("objectId", objectId), ("itemId", itemId), ("desc", description), ("blob", blob),
				("itemMask", general?.ItemMask), ("itemCount", general?.ItemCount), ("itemCreator", general?.Creator),
				("details", info.Details with { PackCount = info.Details.PackCount ?? 0 }),
				("equipmentSlot", r.ReadUInt16()), ("cloth", r.ReadByte() != 0)));
		}
		return items;
	}

	private static IReadOnlyDictionary<string, object?> DecodeExchangeConfirmation(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("action", r.ReadByte()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeExchangeKinah(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		return Fields(("action", r.ReadByte()), ("kinahCount", r.ReadInt64()));
	}

	private static IReadOnlyDictionary<string, object?> DecodeExchangeItem(ReadOnlySpan<byte> body)
	{
		var r = new PacketBodyReader(body);
		byte action = r.ReadByte();
		int itemId = r.ReadInt32(), objectId = r.ReadInt32();
		string description = r.ReadString();
		var blob = r.ReadLengthPrefixedBlob();
		var general = DecodeItemGeneralInfo(blob);
		return Fields(("action", action), ("itemId", itemId), ("objectId", objectId),
			("desc", description), ("blob", blob), ("itemCount", general?.ItemCount));
	}

	private static BotItemGeneralInfo? DecodeItemGeneralInfo(ReadOnlySpan<byte> blob) => BotItemBlobDecoder.Decode(blob).General;

	private static string[] ReadStrings(ref PacketBodyReader r, int count)
	{
		var values = new string[count];
		for (var i = 0; i < count; i++)
			values[i] = r.ReadString();
		return values;
	}

	private static Dictionary<string, object?> Fields(params (string Name, object? Value)[] values) =>
		values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

}

public sealed record BotDecomposableChoice(byte Index, int ItemId, int MinCount, byte Reserved,
	byte StatBonus, byte EnchantBonus, byte Flags);

/// <summary>NR-110g: a creature and the place a packet puts it at.</summary>
public sealed record BotCreatureLanding(int ObjectId, float X, float Y, float Z);
public sealed record BotCreatureHealth(int ObjectId, byte Percent);

public sealed record DecodedBotServerPacket(Type PacketType, IReadOnlyDictionary<string, object?> Fields)
{
	/// <summary>NR-110g: where the packet puts creatures; null for none. It is no field: a trace writes the fields, and
	/// every recorded trace holds this packet as it was decoded before.</summary>
	public IReadOnlyList<BotCreatureLanding>? Landings { get; init; }
	public IReadOnlyList<BotCreatureHealth>? Health { get; init; }

	/// <summary>AK-04: when the transport received the packet (the virtual clock in SIM, wall time live), so state that runs on
	/// from a packet (the game clock) is not dated by when the bot got round to applying it.</summary>
	public DateTimeOffset? ReceivedAt { get; init; }

	public T Get<T>(string name) => Fields.TryGetValue(name, out var value)
		? (T)value!
		: throw new KeyNotFoundException($"{PacketType.Name} did not decode a '{name}' field.");
}
