using Aion.Bots.Protocol;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Bots.World;

/// <summary>A client-side view of the world derived exclusively from decoded server packets.</summary>
public sealed partial class BotWorldModel
{
	private bool pendingMapReload;
	public const int KinahItemId = 182_400_001;

	private readonly Dictionary<int, BotKnownObject> objects = [];
	// NR-R15a: where a packet has put a creature that is no player, kept beside the object and not applied to it.
	private readonly Dictionary<int, BotInventoryItem> inventory = [];
	private readonly Dictionary<int, BotSkill> skills = [];
	private readonly HashSet<int> recipes = [];
	private readonly Dictionary<int, byte> recipeProductionLimits = [];
	private readonly Dictionary<int, BotSkillCooldown> cooldowns = [];
	private readonly Dictionary<(int PlayerId, int CooldownId), BotInstanceEntry> instanceEntries = [];
	public IReadOnlyDictionary<(int PlayerId, int CooldownId), BotInstanceEntry> InstanceEntries => instanceEntries;
	private readonly Dictionary<int, BotQuestState> quests = [];
	private readonly Dictionary<int, BotCompletedQuest> completedQuests = [];
	private readonly HashSet<int> acceptedQuestIds = [];
	private readonly HashSet<int> completedQuestIds = [];
	private readonly List<BotSystemMessage> systemMessages = [];
	private int systemMessageHistoryLimit = int.MaxValue;
	private readonly Dictionary<int, byte> lootStatuses = [];
	public IReadOnlyDictionary<int, byte> LootStatuses => lootStatuses;
	/// <summary>AK-00: the game time from the last SM_GAME_TIME, in minutes since 01.01.0000 00:00 (Java GameTime.getTime()).
	/// The server sends it on entering the world and on its periodic broadcast; the clock runs one game minute per 5 s.</summary>
	public int? GameMinutes { get; private set; }
	/// <summary>How many SM_GAME_TIME packets were seen, so a caller can note when a new one arrived.</summary>
	public int GameTimeUpdates { get; private set; }
	/// <summary>The game hour (0-23) of the last SM_GAME_TIME (Java GameTime.getHour()).</summary>
	public int? GameHour => GameMinutes is int minutes ? minutes / 60 % 24 : null;
	/// <summary>AK-04: when the last SM_GAME_TIME arrived, on the bot's clock (the transport's receive time). Dating it by when the
	/// bot applied it put the client's estimate 25 game minutes behind after a long wait in SIM.</summary>
	public DateTimeOffset? GameTimeObservedAt { get; set; }

	/// <summary>The game minutes at <paramref name="now"/>: the last SM_GAME_TIME run on at one minute per real 5 s.</summary>
	public long? GameMinutesAt(DateTimeOffset now) => GameMinutes is int minutes && GameTimeObservedAt is DateTimeOffset at
		? minutes + (long)Math.Max(0, (now - at).TotalMilliseconds) / 5000 : GameMinutes;

	public IReadOnlyDictionary<int, BotKnownObject> Objects => objects;
	public IReadOnlyDictionary<int, BotInventoryItem> Inventory => inventory;
	public IReadOnlyDictionary<int, BotSkill> Skills => skills;
	public IReadOnlySet<int> Recipes => recipes;
	/// <summary>Last client-observed remaining productions; zero means unlimited. Refreshed by a recipe request.</summary>
	public IReadOnlyDictionary<int, byte> RecipeProductionLimits => recipeProductionLimits;
	public IReadOnlyDictionary<int, BotSkillCooldown> Cooldowns => cooldowns;
	public IReadOnlyDictionary<int, BotQuestState> Quests => quests;
	public IReadOnlyDictionary<int, BotCompletedQuest> CompletedQuests => completedQuests;
	/// <summary>Login counts plus completed transitions observed since login. Java finishQuest sends
	/// SM_QUEST_ACTION, not a new completed-list packet; repeatability remains known only from its own packet.</summary>
	public IReadOnlyDictionary<int, byte> CompletedQuestCounts
	{
		get
		{
			var counts = completedQuests.ToDictionary(q => q.Key, q => q.Value.CompleteCount);
			foreach (BotQuestState quest in quests.Values.Where(q => q.CompleteCount > 0 || q.Status == 5))
				counts[quest.QuestId] = Math.Max(counts.GetValueOrDefault(quest.QuestId), quest.CompleteCount);
			return counts;
		}
	}
	public IReadOnlySet<int> AcceptedQuestIds => acceptedQuestIds;
	public IReadOnlySet<int> CompletedQuestIds => completedQuestIds;
	public IReadOnlyList<BotSystemMessage> SystemMessages => systemMessages;

	/// <summary>Opt-in diagnostic lookback bound; packet traces and live refusal checking remain complete.</summary>
	public void BoundSystemMessageHistory(int capacity)
	{
		if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
		systemMessageHistoryLimit = capacity;
		TrimSystemMessages();
	}

	private void TrimSystemMessages()
	{
		if (systemMessages.Count > systemMessageHistoryLimit)
			systemMessages.RemoveRange(0, systemMessages.Count - Math.Max(1, systemMessageHistoryLimit / 2));
	}

	public bool QuestJournalObserved { get; private set; }
	public bool CompletedJournalObserved { get; private set; }
	public bool StatsObserved { get; private set; }
	public bool InventoryObserved { get; private set; }
	public bool SkillsObserved { get; private set; }
	public bool LoginStateObserved => SelfObjectId != null && Position != null && MapId != null &&
		QuestJournalObserved && CompletedJournalObserved && StatsObserved && InventoryObserved && SkillsObserved;

	public int? SelfObjectId { get; private set; }
	public int? MapId { get; private set; }
	public (int Index, int Count)? ChannelInfo { get; private set; }
	private (int MapId, int InstanceId)? spawnIdentity;
	/// <summary>Java SM_PLAYER_SPAWN carries worldId + instanceId - 1 (negative on personal maps).
	/// SM_CHANNEL_INFO can instead carry its unspawned default during teleport. A same-map reload
	/// retains the last actual spawn identity; a different destination cannot use it.</summary>
	public int? InstanceId => spawnIdentity is { } spawn && spawn.MapId == MapId ? spawn.InstanceId : null;
	public BotPosition? Position { get; private set; }
	public ushort Level { get; private set; }
	public int CurrentHp { get; private set; }
	public int MaxHp { get; private set; }
	public int CurrentMp { get; private set; }
	public int MaxMp { get; private set; }
	public ushort CurrentDp { get; private set; }
	public ushort MaxDp { get; private set; }
	public int CurrentFlightTime { get; private set; }
	public int MaxFlightTime { get; private set; }
	public float? MovementSpeed { get; private set; }
	public long CurrentExperience { get; private set; }
	public long RecoverableExperience { get; private set; }
	public long ExperienceNeeded { get; private set; }
	public bool IsDead { get; private set; }
	/// <summary>The Energy of Repose and its full measure, as SM_STATUPDATE_EXP last gave them (Java curBoostExp and
	/// maxBoostExp): a kill pays extra experience out of it.</summary>
	public long ReposeEnergy { get; private set; }
	public long MaxReposeEnergy { get; private set; }
	/// <summary>The power shard mode, as the server last said it: by its messages (started, ended, burnt out) and by the
	/// state every emotion of the character's own carries (Java SM_EMOTION, CreatureState.POWERSHARD). Off after a login:
	/// the server does not keep it across one.</summary>
	public bool PowerShardOn { get; private set; }
	private const int PowerShardState = 1 << 7, PowerShardStarted = 1300491, PowerShardEnded = 1300492, PowerShardBurntOut = 1400075;
	public BotReviveOptions? ReviveOptions { get; private set; }
	public long Kinah => inventory.Values.Where(item => item.ItemId == KinahItemId).Sum(item => item.Count);
	public BotCubeExpansion? CubeExpansion { get; private set; }

	public BotDialogWindow? Dialog { get; private set; }
	public BotQuestionWindow? Question { get; private set; }
	public BotLootWindow? Loot { get; private set; }
	public BotTradeWindow? Trade { get; private set; }
	public BotVendorPrices? VendorPrices { get; private set; }
	public BotQuestShare? PendingQuestShare { get; private set; }
	public string? ExchangeRequestFrom { get; private set; }

	/// <summary>The visible objects before a reload that may not happen (a teleport cast that gets interrupted
	/// sends nothing to rebuild the world with).</summary>
	public IReadOnlyList<BotKnownObject> SnapshotObjects() => objects.Values.ToArray();

	/// <summary>Put a snapshot back after a reload that never came; whatever arrived since stays.</summary>
	public void RestoreObjects(IEnumerable<BotKnownObject> snapshot)
	{
		foreach (BotKnownObject known in snapshot)
			objects.TryAdd(known.ObjectId, known);
	}

	/// <summary>Forget object ids that become invalid when the server rebuilds the player's visible world.</summary>
	public void BeginWorldReload()
	{
		ForgetEffectObservations();
		ChannelInfo = null;
		ForgetWorldObjects();
	}

	/// <summary>Everything bound to the objects of the world the player just left (not its channel, which the
	/// server announces just before the spawn).</summary>
	private void ForgetWorldObjects()
	{
		objects.Clear();
		openPrivateStores.Clear(); privateStoreNames.Clear(); privateStoreListings.Clear();
		lootStatuses.Clear();
		Dialog = null;
		Question = null;
		Loot = null;
		Trade = null;
		TradeIn = null;
		LastKiskUpdate = null;
	}

	public DateTimeOffset ObservationTime { get; private set; }

	public void Apply(DecodedBotServerPacket packet, DateTimeOffset? observedAt = null)
	{
		ObservationTime = observedAt ?? packet.ReceivedAt ?? ObservationTime;
		var type = packet.PacketType;
		if (type == typeof(SM_STATS_INFO)) StatsObserved = true;
		if (type == typeof(SM_SKILL_LIST)) SkillsObserved = true;
		if (type == typeof(SM_INVENTORY_INFO) && !packet.Get<bool>("firstPacket") &&
			packet.Get<List<IReadOnlyDictionary<string, object?>>>("items").Count == 0) InventoryObserved = true;
		if (type == typeof(SM_PLAYER_SPAWN))
			ApplyPlayerSpawn(packet);
		else if (type == typeof(SM_CHANNEL_INFO))
			ChannelInfo = (packet.Get<int>("currentChannel"), packet.Get<int>("instanceCount"));
		else if (type == typeof(SM_INSTANCE_INFO))
		{
			if (packet.Get<byte>("updateType") == 0) instanceEntries.Clear();
			foreach (BotInstanceEntry entry in packet.Get<List<BotInstanceEntry>>("entries"))
				instanceEntries[(entry.PlayerId, entry.CooldownId)] = entry;
		}
		else if (type == typeof(SM_PLAYER_INFO))
			ApplyPlayerInfo(packet);
		else if (type == typeof(SM_EMOTION))
			ApplyEmotion(packet);
		else if (type == typeof(SM_NPC_INFO))
			ApplyNpcInfo(packet);
		else if (type == typeof(SM_GATHERABLE_INFO))
			ApplyGatherableInfo(packet);
		else if (type == typeof(SM_MOVE))
			ApplyMove(packet);
		else if (type == typeof(SM_FORCED_MOVE))
			ApplyForcedMove(packet);
		else if (type == typeof(SM_POSITION))
			ApplyPosition(packet);
		else if (packet.Landings is { } landings)
			ApplyLandings(landings);
		else if (type == typeof(SM_DELETE))
		{
			objects.Remove(packet.Get<int>("objectId"));
			objectAbnormals.Remove(packet.Get<int>("objectId"));
			objectEffects.Remove(packet.Get<int>("objectId"));
			ForgetPrivateStore(packet.Get<int>("objectId"));
			lootStatuses.Remove(packet.Get<int>("objectId"));
			if (OwnedKiskRemoval is { } removal && removal.ObjectId == packet.Get<int>("objectId"))
				OwnedKiskRemoval = removal with { DeleteObserved = true };
		}
		else if (type == typeof(SM_TELEPORT_LOC))
			ApplyTeleport(packet);
		else if (type == typeof(SM_STATS_INFO))
			ApplyStats(packet);
		else if (type == typeof(SM_STATUPDATE_HP))
			ApplyHp(packet);
		else if (type == typeof(SM_STATUPDATE_MP))
			ApplyMp(packet);
		else if (type == typeof(SM_STATUPDATE_DP))
			CurrentDp = packet.Get<ushort>("currentDp");
		else if (type == typeof(SM_STATUPDATE_EXP))
			ApplyExperience(packet);
		else if (type == typeof(SM_FLY_TIME))
			ApplyFlightTime(packet);
		else if (type == typeof(SM_DIE))
			ApplyDeath(packet);
		else if (type == typeof(SM_BIND_POINT_INFO))
			ApplyBindPoint(packet);
		else if (type == typeof(SM_KISK_UPDATE))
		{
			LastKiskUpdate = new(packet.Get<int>("objectId"), packet.Get<int>("creatorId"), packet.Get<int>("useMask"),
				packet.Get<int>("currentMembers"), packet.Get<int>("maxMembers"), packet.Get<int>("remainingResurrects"),
				packet.Get<int>("maxResurrects"), packet.Get<int>("remainingLifetimeSeconds"));
			if (LastKiskUpdate.CreatorId == SelfObjectId)
			{
				if (OwnedKiskUpdate?.ObjectId != LastKiskUpdate.ObjectId) OwnedKiskRemoval = null;
				OwnedKiskUpdate = LastKiskUpdate;
			}
		}
		else if (type == typeof(SM_INVENTORY_INFO))
			ApplyInventoryInfo(packet);
		else if (type == typeof(SM_INVENTORY_ADD_ITEM))
			UpsertInventoryItems(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
		else if (type == typeof(SM_INVENTORY_UPDATE_ITEM))
			ApplyInventoryUpdate(packet);
		else if (type == typeof(SM_DELETE_ITEM))
			inventory.Remove(packet.Get<int>("itemObjectId"));
		else if (type == typeof(SM_WAREHOUSE_INFO) || type == typeof(SM_WAREHOUSE_ADD_ITEM)
			|| type == typeof(SM_WAREHOUSE_UPDATE_ITEM) || type == typeof(SM_DELETE_WAREHOUSE_ITEM))
			ApplyWarehouse(packet);
		else if (type == typeof(SM_BROKER_SERVICE))
			ApplyBroker(packet);
		else if (type == typeof(SM_PRIVATE_STORE))
			ApplyPrivateStore(packet);
		else if (type == typeof(SM_PRIVATE_STORE_NAME))
			privateStoreNames[packet.Get<int>("sellerObjectId")] = packet.Get<string>("name");
		else if (type == typeof(SM_CUBE_UPDATE) && packet.Get<byte>("action") == 0 && packet.Get<byte>("actionValue") == 0)
			CubeExpansion = new(packet.Get<byte>("npcExpands"), packet.Get<byte>("questExpands"), packet.Get<byte>("itemExpands"));
		else if (type == typeof(SM_SKILL_LIST))
			ApplySkillList(packet);
		else if (type == typeof(SM_SKILL_REMOVE))
			skills.Remove(packet.Get<ushort>("skillId"));
		else if (type == typeof(SM_RECIPE_LIST))
		{
			recipes.Clear();
			recipes.UnionWith(packet.Get<int[]>("recipeIds"));
			recipeProductionLimits.Clear();
			foreach (var pair in packet.Get<IReadOnlyDictionary<int, byte>>("productionLimits")) recipeProductionLimits[pair.Key] = pair.Value;
		}
		else if (type == typeof(SM_LEARN_RECIPE))
		{
			recipes.Add(packet.Get<int>("recipeId"));
			recipeProductionLimits[packet.Get<int>("recipeId")] = packet.Get<byte>("productionLimit");
		}
		else if (type == typeof(SM_RECIPE_DELETE))
		{
			recipes.Remove(packet.Get<int>("recipeId"));
			recipeProductionLimits.Remove(packet.Get<int>("recipeId"));
		}
		else if (type == typeof(SM_SKILL_COOLDOWN))
			ApplySkillCooldowns(packet);
		else if (type == typeof(SM_QUEST_LIST))
			ApplyQuestList(packet);
		else if (type == typeof(SM_QUEST_ACTION))
			ApplyQuestAction(packet);
		else if (type == typeof(SM_QUEST_COMPLETED_LIST))
			ApplyCompletedQuests(packet);
		else if (type == typeof(SM_DIALOG_WINDOW))
			ApplyDialog(packet);
		else if (type == typeof(SM_QUESTION_WINDOW))
			Question = new BotQuestionWindow(packet.Get<int>("code"), packet.Get<string[]>("params"),
				packet.Get<int>("senderId"), packet.Get<int>("rangeOrCooldownSeconds"));
		else if (type == typeof(SM_CLOSE_QUESTION_WINDOW))
			Question = null;
		else if (type == typeof(SM_LOOT_STATUS))
			ApplyLootStatus(packet);
		else if (type == typeof(SM_GAME_TIME))
		{
			GameMinutes = packet.Get<int>("minutes");
			GameTimeUpdates++;
		}
		else if (type == typeof(SM_LOOT_ITEMLIST))
			ApplyLootItems(packet);
		else if (type == typeof(SM_TRADELIST))
			ApplyTrade(packet);
		else if (type == typeof(SM_TRADE_IN_LIST))
			ApplyTradeIn(packet);
		else if (type == typeof(SM_PRICES))
			VendorPrices = new BotVendorPrices(packet.Get<byte>("globalPrices"), packet.Get<byte>("globalModifier"),
				packet.Get<byte>("taxes"));
		else if (type == typeof(SM_SYSTEM_MESSAGE))
			ApplySystemMessage(packet);
		else if (type == typeof(SM_ABNORMAL_STATE))
			ApplyVisibleEffects(packet);
		else if (type == typeof(SM_SKILL_ACTIVATION))
			ApplySkillActivation(packet);
		else if (type == typeof(SM_RIDE_ROBOT))
			ApplyRideRobot(packet);
		else if (type == typeof(SM_ABNORMAL_EFFECT))
			ApplyCreatureEffects(packet);
		else if (type == typeof(SM_SUMMON_PANEL))
			ApplySummonPanel(packet);
		else if (type == typeof(SM_RESET_SKILL_COOLDOWN))
			ForgetSummon();
		else if (type == typeof(SM_SUMMON_OWNER_REMOVE))
			ApplySummonOwnerRemove(packet);
		else if (type == typeof(SM_SUMMON_USESKILL))
			ApplySummonUseSkill(packet);
		else if (type == typeof(SmAttackStatus))
			ApplySummonStatus(packet);
		else if (type == typeof(SM_EXCHANGE_REQUEST))
			ExchangeRequestFrom = packet.Get<string>("receiver");
		else
			ApplySocial(packet);
		ApplyGroupHealth(packet);
	}

	private void ApplyPlayerSpawn(DecodedBotServerPacket packet)
	{
		// CM_LEVEL_READY returns the authoritative full effect list after every entry.
		ForgetEffectObservations();
		int worldId = packet.Get<int>("worldId");
		// Arriving on another map invalidates every object of the old one, even when no caller announced the
		// transition (a bind revive, a quest teleport or a disconnect back to another world).
		if (pendingMapReload || MapId is int previous && previous != worldId) ForgetWorldObjects();
		pendingMapReload = false;
		MapId = worldId;
		spawnIdentity = packet.Fields.GetValueOrDefault("worldChannel") is int channel
			? (worldId, checked((int)(Math.Abs((long)channel) - worldId + 1))) : null;
		Position = ReadPosition(packet.Fields);
		UpdateSelfObjectPosition();
	}

	private void ApplyPlayerInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var position = ReadPosition(packet.Fields);
		var movementSpeed = GetNullableStruct<float>(packet.Fields, "movementSpeed");
		objects[objectId] = new BotKnownObject(objectId, BotKnownObjectKind.Player, position,
			Name: packet.Get<string>("name"), State: packet.Get<ushort>("state"), Race: packet.Get<byte>("race"),
			PlayerClass: packet.Get<byte>("playerClass"), MovementSpeed: movementSpeed);
		if (SelfObjectId == objectId)
		{
			Position = position;
			MovementSpeed = movementSpeed;
		}
	}

	private void ApplyEmotion(DecodedBotServerPacket packet)
	{
		ApplyPrivateStoreEmotion(packet);
		var objectId = packet.Get<int>("senderObjectId");
		var movementSpeed = packet.Get<float>("movementSpeed");
		if (movementSpeed <= 0)
			return; // Some state-only SM_EMOTION constructors have no creature and therefore serialize speed 0.
		if (objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { MovementSpeed = movementSpeed };
		if (SelfObjectId == objectId)
		{
			MovementSpeed = movementSpeed;
			if (GetNullableStruct<ushort>(packet.Fields, "state") is ushort state) PowerShardOn = (state & PowerShardState) != 0;
		}
	}

	private void ApplyNpcInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var npcId = packet.Get<int>("npcId");
		// The same monster described again keeps the patrol path already watched.
		var patrolPath = objects.TryGetValue(objectId, out var known) && known.TemplateId == npcId ? known.PatrolPath : null;
		objects[objectId] = new BotKnownObject(objectId, BotKnownObjectKind.Npc,
			ReadPosition(packet.Fields, GetNullableStruct<byte>(packet.Fields, "heading") ?? 0),
			TemplateId: npcId, VisualTemplateId: packet.Get<int>("visualNpcId"),
			State: GetNullableStruct<ushort>(packet.Fields, "state"), PatrolPath: patrolPath);
	}

	private void ApplyGatherableInfo(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var isStatic = packet.Get<bool>("isStatic");
		objects[objectId] = new BotKnownObject(objectId,
			isStatic ? BotKnownObjectKind.Static : BotKnownObjectKind.Gatherable,
			ReadPosition(packet.Fields), TemplateId: packet.Get<int>("templateId"),
			StaticId: packet.Get<int>("staticId"), State: packet.Get<ushort>("objectState"),
			IsOpen: GetNullableStruct<bool>(packet.Fields, "open"));
	}

	private void ApplyMove(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		var position = ReadPosition(packet.Fields);
		BotPosition? target = packet.Fields.ContainsKey("targetX")
			? new BotPosition(Get<float>(packet.Fields, "targetX"), Get<float>(packet.Fields, "targetY"),
				Get<float>(packet.Fields, "targetZ"), position.Heading)
			: null;
		if (objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with
			{
				Position = position,
				MoveTarget = target,
				PatrolPath = known.Kind == BotKnownObjectKind.Npc
					? BotPatrolPath.Extend(known.PatrolPath, GetNullableStruct<byte>(packet.Fields, "movementMask"), position, target)
					: known.PatrolPath,
			};
		if (SelfObjectId == objectId)
			Position = position;
	}

	// NR-110g: a stagger, a stumble, a lift or a pull moved a creature that is no player; the server says where only in the
	// cast result, and for a weapon's critical in SM_ATTACK (BotServerPacketDecoder.Landings). The creature stands there
	// from the blow on, as a client shows it. A player is told by SM_FORCED_MOVE, and is moved by that.
	private void ApplyLandings(IReadOnlyList<BotCreatureLanding> landings)
	{
		foreach (BotCreatureLanding landing in landings)
			// The fall after a lift names its status and no place: Java's CloseAerialEffect.calculate sets none, and the
			// packet carries three zeros. A creature is not moved to the map's origin by that.
			if (landing is { X: 0, Y: 0, Z: 0 }) continue;
			else if (objects.TryGetValue(landing.ObjectId, out var known) && known.Kind == BotKnownObjectKind.Npc)
				objects[landing.ObjectId] = known with
				{
					Position = new BotPosition(landing.X, landing.Y, landing.Z, known.Position.Heading), MoveTarget = null,
				};
	}

	// The server ran a player somewhere itself and says where it stands now: the end of a fear or of a confusion
	// (Java FearEffect.endEffect, ConfuseEffect.endEffect). A creature that is no player is left where the client has it.
	private void ApplyPosition(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		if (objects.TryGetValue(objectId, out var known) && known.Kind == BotKnownObjectKind.Player)
			objects[objectId] = known with { Position = ReadPosition(packet.Fields), MoveTarget = null };
		if (SelfObjectId == objectId)
			Position = ReadPosition(packet.Fields);
	}

	// A knockback, stumble or pull moved the creature on the server; the client puts it where it landed.
	private void ApplyForcedMove(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		if (objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { Position = ReadPosition(packet.Fields, known.Position.Heading), MoveTarget = null };
		if (SelfObjectId == objectId && Position is BotPosition self)
			Position = ReadPosition(packet.Fields, self.Heading);
	}

	private void ApplyTeleport(DecodedBotServerPacket packet)
	{
		int destination = packet.Get<int>("mapId");
		// A quest kill can teleport without a caller preparing a reload. Its destination is
		// announced before SM_PLAYER_SPAWN, so forget the old map before changing its id.
		if (MapId is int previous && previous != destination)
		{
			BeginWorldReload();
			pendingMapReload = true;
		}
		MapId = destination;
		Position = ReadPosition(packet.Fields);
		UpdateSelfObjectPosition();
	}

	private void ApplyStats(DecodedBotServerPacket packet)
	{
		SelfObjectId = packet.Get<int>("objectId");
		if (objects.TryGetValue(SelfObjectId.Value, out var self))
			MovementSpeed = self.MovementSpeed;
		Level = packet.Get<ushort>("level");
		ExperienceNeeded = packet.Get<long>("expNeeded");
		RecoverableExperience = packet.Get<long>("expRecoverable");
		CurrentExperience = packet.Get<long>("expShown");
		MaxHp = packet.Get<int>("maxHp");
		CurrentHp = packet.Get<int>("currentHp");
		MaxMp = packet.Get<int>("maxMp");
		CurrentMp = packet.Get<int>("currentMp");
		MaxDp = packet.Get<ushort>("maxDp");
		CurrentDp = packet.Get<ushort>("dp");
		MaxFlightTime = packet.Get<int>("maxFp");
		CurrentFlightTime = packet.Get<int>("currentFp");
		IsDead = CurrentHp <= 0;
		if (!IsDead)
			ReviveOptions = null;
	}

	private void ApplyHp(DecodedBotServerPacket packet)
	{
		CurrentHp = packet.Get<int>("currentHp");
		MaxHp = packet.Get<int>("maxHp");
		if (CurrentHp > 0)
		{
			IsDead = false;
			ReviveOptions = null;
		}
	}

	private void ApplyMp(DecodedBotServerPacket packet)
	{
		CurrentMp = packet.Get<int>("currentMp");
		MaxMp = packet.Get<int>("maxMp");
	}

	private void ApplyExperience(DecodedBotServerPacket packet)
	{
		CurrentExperience = packet.Get<long>("currentExp");
		RecoverableExperience = packet.Get<long>("recoverableExp");
		ExperienceNeeded = packet.Get<long>("maxExp");
		// A packet built by hand for a test may leave the two out; the decoder always gives them.
		ReposeEnergy = GetNullableStruct<long>(packet.Fields, "rep1") ?? ReposeEnergy;
		MaxReposeEnergy = GetNullableStruct<long>(packet.Fields, "rep2") ?? MaxReposeEnergy;
	}

	private void ApplyFlightTime(DecodedBotServerPacket packet)
	{
		CurrentFlightTime = packet.Get<int>("currentFp");
		MaxFlightTime = packet.Get<int>("maxFp");
	}

	private void ApplyDeath(DecodedBotServerPacket packet)
	{
		IsDead = true;
		CurrentHp = 0;
		ReviveOptions = new BotReviveOptions(packet.Get<bool>("allowReviveBySkill"),
			packet.Get<bool>("allowReviveByItem"), packet.Get<int>("remainingKiskTimeSeconds"),
			packet.Get<bool>("allowInstanceRevive"), packet.Get<bool>("invasion"));
	}

	private void ApplyInventoryInfo(DecodedBotServerPacket packet)
	{
		if (packet.Get<bool>("firstPacket"))
			inventory.Clear();
		UpsertInventoryItems(packet.Get<List<IReadOnlyDictionary<string, object?>>>("items"));
	}

	private void UpsertInventoryItems(IEnumerable<IReadOnlyDictionary<string, object?>> items)
	{
		foreach (var item in items)
		{
			var objectId = Get<int>(item, "objectId");
			inventory[objectId] = new BotInventoryItem(objectId, Get<int>(item, "itemId"), Get<string>(item, "desc"),
				Get<long>(item, "itemCount"), Get<ushort>(item, "itemMask"), Get<string>(item, "itemCreator"),
				Get<ushort>(item, "equipmentSlot"), Get<bool>(item, "cloth"))
			{
				Details = item.TryGetValue("details", out var details) && details is BotItemDetails parsed
					? parsed : BotItemDetails.Empty
			};
		}
	}

	private void ApplyInventoryUpdate(DecodedBotServerPacket packet)
	{
		var objectId = packet.Get<int>("objectId");
		if (!inventory.TryGetValue(objectId, out var existing))
			return;
		var update = packet.Fields.TryGetValue("details", out var value) && value is BotItemDetails parsed ? parsed : existing.Details;
		bool fullBlob = GetNullableStruct<long>(packet.Fields, "itemCount") != null;
		// The equip-only blob uses a 64-bit mask. Unequipping resets the server's cube position to zero.
		ushort slot = update.EquippedSlot is { } equipment && (equipment != 0 || existing.Details.EquippedSlot is > 0)
			? unchecked((ushort)equipment) : existing.EquipmentSlot;
		inventory[objectId] = existing with
		{
			Description = packet.Get<string>("desc"),
			Count = GetNullableStruct<long>(packet.Fields, "itemCount") ?? existing.Count,
			ItemMask = GetNullableStruct<ushort>(packet.Fields, "itemMask") ?? existing.ItemMask,
			Creator = GetNullableString(packet.Fields, "itemCreator") ?? existing.Creator,
			EquipmentSlot = slot,
			Details = fullBlob ? update : existing.Details.Merge(update),
		};
	}

	private void ApplySkillList(DecodedBotServerPacket packet)
	{
		if (packet.Get<bool>("silentUpdate"))
			skills.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("skills"))
		{
			var skillId = Get<ushort>(entry, "skillId");
			skills[skillId] = new BotSkill(skillId, Get<ushort>(entry, "level"), Get<byte>(entry, "reserved"),
				Get<byte>(entry, "professionBarSize"), Get<int>(entry, "flag"), Get<byte>(entry, "skillType"));
		}
	}

	private void ApplySkillCooldowns(DecodedBotServerPacket packet)
	{
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("cooldowns"))
		{
			var skillId = Get<ushort>(entry, "skillId");
			var remaining = Get<int>(entry, "remainingSeconds");
			if (remaining <= 0)
				cooldowns.Remove(skillId);
			else
				cooldowns[skillId] = new BotSkillCooldown(skillId, remaining, Get<int>(entry, "durationMillis"));
		}
	}

	private void ApplyQuestList(DecodedBotServerPacket packet)
	{
		QuestJournalObserved = true;
		quests.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("quests"))
		{
			var questId = Get<int>(entry, "questId");
			byte status = Get<byte>(entry, "status");
			TrackQuestStatus(questId, status);
			quests[questId] = new BotQuestState(questId, status,
				Get<int>(entry, "stepAndFlags"), Get<byte>(entry, "completeCount"), null);
		}
	}

	private void ApplyQuestAction(DecodedBotServerPacket packet)
	{
		if (packet.Fields.ContainsKey("suppressed"))
			return;
		var action = packet.Get<byte>("action");
		var questId = packet.Get<int>("questId");
		switch (action)
		{
			case 1:
			case 2:
				quests.TryGetValue(questId, out var existing);
				byte completeCount = Math.Max(existing?.CompleteCount ?? 0, completedQuests.GetValueOrDefault(questId)?.CompleteCount ?? 0);
				byte status = packet.Get<byte>("status");
				// Java QuestState.setStatus increments only on a transition to COMPLETE. Duplicate updates
				// must not count twice; the wire count saturates at 255 (SM_QUEST_LIST/COMPLETED_LIST).
				if (status == 5 && existing?.Status != 5 && (existing != null || !completedQuests.ContainsKey(questId)))
					completeCount = (byte)Math.Min(255, completeCount + 1);
				TrackQuestStatus(questId, status);
				quests[questId] = new BotQuestState(questId, status,
					packet.Get<int>("stepAndFlags"), completeCount, existing?.TimerSeconds);
				break;
			case 3:
				quests.Remove(questId);
				break;
			case 4:
				var state = quests.TryGetValue(questId, out var timed) ? timed : new BotQuestState(questId, 0, 0, 0, null);
				quests[questId] = state with { TimerSeconds = packet.Get<int>("timer") };
				break;
			case 5:
				PendingQuestShare = new BotQuestShare(questId, packet.Get<int>("sharerId"), packet.Get<bool>("shareInAlliance"));
				break;
		}
	}

	private void ApplyCompletedQuests(DecodedBotServerPacket packet)
	{
		if (packet.Get<byte>("updateMode") == 0) CompletedJournalObserved = true;
		if (packet.Get<byte>("updateMode") == 0)
			completedQuests.Clear();
		foreach (var entry in packet.Get<List<IReadOnlyDictionary<string, object?>>>("quests"))
		{
			var questId = Get<int>(entry, "questId");
			acceptedQuestIds.Add(questId);
			completedQuestIds.Add(questId);
			completedQuests[questId] = new BotCompletedQuest(questId, Get<byte>(entry, "completeCount"),
				Get<bool>(entry, "nonRepeatable"));
		}
	}

	private void TrackQuestStatus(int questId, byte status)
	{
		if (status is >= 3 and <= 5)
			acceptedQuestIds.Add(questId);
		if (status == 5)
			completedQuestIds.Add(questId);
	}

	private void ApplyDialog(DecodedBotServerPacket packet)
	{
		var pageId = packet.Get<ushort>("dialogPageId");
		if (pageId == 0)
		{
			Dialog = null;
			Trade = null;
			TradeIn = null;
			return;
		}
		Dialog = new BotDialogWindow(packet.Get<int>("targetObjectId"), pageId, packet.Get<int>("questId"));
	}

	private void ApplyLootStatus(DecodedBotServerPacket packet)
	{
		var status = packet.Get<byte>("status");
		lootStatuses[packet.Get<int>("targetObjectId")] = status;
		// DropService sends the item list before OPEN_DROP_LIST. Preserve that list for this corpse.
		if (status == 2 && Loot?.TargetObjectId != packet.Get<int>("targetObjectId"))
			Loot = new BotLootWindow(packet.Get<int>("targetObjectId"), []);
		else if (status == 3)
			Loot = null;
	}

	private void ApplyLootItems(DecodedBotServerPacket packet)
	{
		var items = packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.Select(item => new BotLootItem(Get<byte>(item, "index"), Get<int>(item, "itemId"),
				Get<int>(item, "count"), Get<bool>(item, "requiresConfirmation")))
			.ToArray();
		Loot = new BotLootWindow(packet.Get<int>("targetObjectId"), items);
	}

	private void ApplyTrade(DecodedBotServerPacket packet)
	{
		var limited = packet.Get<List<IReadOnlyDictionary<string, object?>>>("limitedItems")
			.Select(item => new BotLimitedTradeItem(Get<int>(item, "itemId"), Get<ushort>(item, "buyCount"),
				Get<ushort>(item, "sellLimit")))
			.ToArray();
		Trade = new BotTradeWindow(packet.Get<int>("targetObjectId"), packet.Get<byte>("tradeNpcType"),
			packet.Get<int>("buyPriceModifier"), packet.Get<bool>("showBuyTab"), packet.Get<bool>("showSellTab"),
			packet.Get<int[]>("tabs"), limited);
	}

	private void ApplySystemMessage(DecodedBotServerPacket packet)
	{
		string? name = GetNullableString(packet.Fields, "name");
		if (name is "STR_BINDSTONE_IS_REMOVED" or "STR_BINDSTONE_IS_DESTROYED" &&
			OwnedKiskUpdate is { } owned && KiskBindPoint?.KiskObjectId == owned.ObjectId)
			OwnedKiskRemoval = new(owned.ObjectId, name == "STR_BINDSTONE_IS_DESTROYED",
				OwnedKiskRemoval is { DeleteObserved: true } previous && previous.ObjectId == owned.ObjectId);
		// STR_WEAPON_BOOST_BOOST_MODE_STARTED; STR_WEAPON_BOOST_BOOST_MODE_ENDED and STR_MSG_WEAPON_BOOST_MODE_BURN_OUT.
		int messageId = packet.Get<int>("msgId");
		if (messageId == PowerShardStarted) PowerShardOn = true;
		else if (messageId is PowerShardEnded or PowerShardBurntOut) PowerShardOn = false;
		systemMessages.Add(new BotSystemMessage(packet.Get<int>("msgId"), GetNullableString(packet.Fields, "name"),
			packet.Get<string[]>("params"), packet.Get<string[]>("specialParams"), packet.Get<int>("senderObjectId")));
		TrimSystemMessages();
	}

	private void UpdateSelfObjectPosition()
	{
		if (SelfObjectId is int objectId && Position is BotPosition position && objects.TryGetValue(objectId, out var known))
			objects[objectId] = known with { Position = position };
	}

	private static BotPosition ReadPosition(IReadOnlyDictionary<string, object?> fields, byte? heading = null) =>
		new(Get<float>(fields, "x"), Get<float>(fields, "y"), Get<float>(fields, "z"),
			heading ?? Get<byte>(fields, "heading"));

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded packet field '{name}' was missing or was not {typeof(T).Name}.");

	private static T? GetNullableStruct<T>(IReadOnlyDictionary<string, object?> fields, string name) where T : struct =>
		fields.TryGetValue(name, out var value) && value is T typed ? typed : null;

	private static string? GetNullableString(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out var value) ? value as string : null;
}

public enum BotKnownObjectKind
{
	Player,
	Npc,
	Gatherable,
	Static,
}

public readonly record struct BotPosition(float X, float Y, float Z, byte Heading);

public sealed record BotKnownObject(int ObjectId, BotKnownObjectKind Kind, BotPosition Position,
	int? TemplateId = null, int? VisualTemplateId = null, int? StaticId = null, string? Name = null,
	ushort? State = null, bool? IsOpen = null, byte? Race = null, byte? PlayerClass = null,
	float? MovementSpeed = null, BotPosition? MoveTarget = null, IReadOnlyList<BotPosition>? PatrolPath = null)
{
	/// <summary>Where the object stands once its last observed move ends: the SM_MOVE target when one was
	/// sent (a walking or chasing NPC), else its reported position. A walker that has not sent SM_MOVE for a
	/// while has normally arrived there.</summary>
	public BotPosition SettledPosition => MoveTarget ?? Position;

	/// <summary>Java CreatureState stance bits; walk/weapon flags do not make a corpse attackable.</summary>
	public bool IsCorpse => Kind is BotKnownObjectKind.Npc or BotKnownObjectKind.Player &&
		State is ushort state && (state & 15) is 7 or 8;
}

public sealed record BotInventoryItem(int ObjectId, int ItemId, string Description, long Count, ushort ItemMask,
	string Creator, ushort EquipmentSlot, bool Cloth)
{
	public BotItemDetails Details { get; init; } = BotItemDetails.Empty;
}

public sealed record BotSkill(ushort SkillId, ushort Level, byte Reserved, byte ProfessionBarSize, int Flag, byte SkillType);

public sealed record BotSkillCooldown(ushort SkillId, int RemainingSeconds, int DurationMillis);

public sealed record BotQuestState(int QuestId, byte Status, int StepAndFlags, byte CompleteCount, int? TimerSeconds);

public sealed record BotCompletedQuest(int QuestId, byte CompleteCount, bool NonRepeatable);

public sealed record BotQuestShare(int QuestId, int SharerId, bool InAlliance);

public sealed record BotReviveOptions(bool BySkill, bool ByItem, int RemainingKiskTimeSeconds, bool InInstance, bool Invasion);

public sealed record BotDialogWindow(int TargetObjectId, ushort PageId, int QuestId);

public sealed record BotQuestionWindow(int Code, IReadOnlyList<string> Parameters, int SenderId, int RangeOrCooldownSeconds);

public sealed record BotLootItem(byte Index, int ItemId, int Count, bool RequiresConfirmation);

public sealed record BotLootWindow(int TargetObjectId, IReadOnlyList<BotLootItem> Items);

public sealed record BotLimitedTradeItem(int ItemId, ushort BuyCount, ushort SellLimit);

public sealed record BotTradeWindow(int TargetObjectId, byte NpcType, int BuyPriceModifier, bool ShowBuyTab,
	bool ShowSellTab, IReadOnlyList<int> Tabs, IReadOnlyList<BotLimitedTradeItem> LimitedItems);

public sealed record BotSystemMessage(int MessageId, string? Name, IReadOnlyList<string> Parameters,
	IReadOnlyList<string> SpecialParameters, int SenderObjectId);

/// <summary>PricesService's live race/influence/tax percentages, as reported in SM_PRICES.</summary>
public sealed record BotVendorPrices(int GlobalPrices, int GlobalModifier, int Taxes)
{
	public long ServicePrice(long basePrice) =>
		(long)((long)((long)(basePrice * GlobalPrices / 100D) * GlobalModifier / 100D) * Taxes / 100D);

	// Java PricesService.getBuyPrice truncates after EACH percentage, not just the final product.
	public long BuyPrice(long basePrice, int vendorBuyModifier) =>
		(long)((long)((long)((long)(basePrice * vendorBuyModifier / 100D) * GlobalPrices / 100D)
			* GlobalModifier / 100D) * Taxes / 100D);

	// TradeList.calculateBuyListPrice applies the NPC's sell rate AFTER the individually truncated
	// vendor/global/tax calculations. SM_TRADELIST combines two modifiers for display, not this rounding.
	public long BuyListPrice(long basePrice, int vendorBuyModifier, int npcSellRate, long count) =>
		checked(BuyPrice(basePrice, vendorBuyModifier) * count * npcSellRate / 100);

	public static long SellPrice(long basePrice, int vendorSellModifier) => (long)(basePrice * vendorSellModifier / 100D);
}
