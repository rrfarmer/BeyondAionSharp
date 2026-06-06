using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Aion.Commons.Configuration;

namespace Aion.GameServer.Configuration;

public sealed class GameServerOptions
{
	public GameServerCoreOptions Core { get; init; } = new();

	public GameServerNetworkOptions Network { get; init; } = new();

	public GeoDataOptions GeoData { get; init; } = new();

	public GameServerAiOptions Ai { get; init; } = new();

	public GameServerHousingOptions Housing { get; init; } = new();

	public GameServerCustomOptions Custom { get; init; } = new();

	public CleaningOptions Cleaning { get; init; } = new();

	public GameThreadOptions Threads { get; init; } = new();

	public GameServerNameOptions Names { get; init; } = new();

	public GameServerMembershipOptions Membership { get; init; } = new();

	public GameServerInstanceOptions Instance { get; init; } = new();

	public GameServerGroupOptions Group { get; init; } = new();

	public GameServerAutoGroupOptions AutoGroup { get; init; } = new();

	public GameServerAdministrationOptions Administration { get; init; } = new();

	public GameServerRateOptions Rates { get; init; } = new();

	public GameServerPriceOptions Prices { get; init; } = new();

	public GameServerPeriodicSaveOptions PeriodicSave { get; init; } = new();

	public int LoadedPropertyCount { get; init; }

	public static GameServerOptions LoadFromJavaConfig(string startDirectory)
	{
		// Java parity: configs/main GSConfig, NetworkConfig, GeoDataConfig, AIConfig, CustomConfig, MembershipConfig.
		var loader = LoadProperties(startDirectory);
		var clientSocket = GetWithEnvironment(loader, "gameserver.network.client.socket_address", "0.0.0.0:7777");
		var clientConnect = GetWithEnvironment(loader, "gameserver.network.client.connect_address", clientSocket);

		return new GameServerOptions
		{
			Core = new GameServerCoreOptions
			{
				ServerCountryCode = GetIntWithEnvironment(loader, "gameserver.country.code", 99),
				PlayerMaxLevel = GetIntWithEnvironment(loader, "gameserver.players.max.level", 65),
				TimeZoneId = ParseTimeZoneId(GetWithEnvironment(loader, "gameserver.timezone", string.Empty)),
				EnableChatServer = GetBoolWithEnvironment(loader, "gameserver.chatserver.enable", false),
				ChatServerMinLevel = GetByteWithEnvironment(loader, "gameserver.chatserver.min_level", 10),
				CharacterCreationMode = GetIntWithEnvironment(loader, "gameserver.character.creation.mode", 0),
				CharacterLimitCount = GetIntWithEnvironment(loader, "gameserver.character.limit.count", 8),
				CharacterFactionLimitationMode = GetIntWithEnvironment(loader, "gameserver.character.faction.limitation.mode", 0),
				EnableRatioLimitation = GetBoolWithEnvironment(loader, "gameserver.ratio.limitation.enable", false),
				RatioMinValue = GetIntWithEnvironment(loader, "gameserver.ratio.min.value", 60),
				RatioMinRequiredLevel = GetIntWithEnvironment(loader, "gameserver.ratio.min.required.level", 10),
				RatioMinCharactersCount = GetIntWithEnvironment(loader, "gameserver.ratio.min.characters_count", 50),
				RatioHighPlayerCountDisabling = GetIntWithEnvironment(loader, "gameserver.ratio.high_player_count.disabling", 500),
				CharacterReentryTimeSeconds = GetIntWithEnvironment(loader, "gameserver.character.reentry.time", 20),
				MinimumSkillCastIntervalMillis = GetIntWithEnvironment(loader, "gameserver.min_skill_cast_interval_millis", 350),
				ItemWrapLimit = GetIntWithEnvironment(loader, "gameserver.item_wrap_limit", 0),
				EnableWebRewards = GetBoolWithEnvironment(loader, "gameserver.web_rewards.enable", false),
				AnalyzeQuestHandlers = GetBoolWithEnvironment(loader, "gameserver.analysis.quest_handlers", true),
				QuestHandlerDirectory = GetWithEnvironment(loader, "gameserver.quest.handler_directory", "./data/handlers/quest"),
			},
			Network = new GameServerNetworkOptions
			{
				ClientEndPoint = ParseEndPoint(clientSocket),
				ClientConnectEndPoint = ParseEndPoint(clientConnect),
				LoginEndPoint = ParseEndPoint(GetWithEnvironment(loader, "gameserver.network.login.address", "localhost:9014")),
				ChatEndPoint = ParseEndPoint(GetWithEnvironment(loader, "gameserver.network.chat.address", "localhost:9021")),
				ChatPassword = GetWithEnvironment(loader, "gameserver.network.chat.password", string.Empty),
				GameServerId = GetIntWithEnvironment(loader, "gameserver.network.login.gsid", 1),
				LoginPassword = GetWithEnvironment(loader, "gameserver.network.login.password", string.Empty),
				MinimumAccessLevel = GetIntWithEnvironment(loader, "gameserver.network.login.min_accesslevel", 0),
				MaxOnlinePlayers = GetIntWithEnvironment(loader, "gameserver.network.login.max_players", 100),
				NioReadWriteThreads = GetIntWithEnvironment(loader, "gameserver.network.nio.threads", 1),
				NioReadWriteThreadsUnsafeAllow = GetBoolWithEnvironment(loader, "gameserver.network.nio.threads.unsafe.allow", false),
				PacketProcessorMinThreads = GetIntWithEnvironment(loader, "gameserver.network.packet.processor.threads.min", 4),
				PacketProcessorMaxThreads = GetIntWithEnvironment(loader, "gameserver.network.packet.processor.threads.max", 4),
				PacketProcessorThreadKillThreshold = GetIntWithEnvironment(loader, "gameserver.network.packet.processor.threshold.kill", 3),
				PacketProcessorThreadSpawnThreshold = GetIntWithEnvironment(loader, "gameserver.network.packet.processor.threshold.spawn", 50),
				LogUnknownPackets = GetBoolWithEnvironment(loader, "gameserver.network.logging.unknown_packets", false),
				LogIgnoredPackets = GetBoolWithEnvironment(loader, "gameserver.network.logging.ignored_packets", false),
				EnableFloodConnections = GetBoolWithEnvironment(loader, "gameserver.network.flood.connections", false),
				FloodTickMillis = GetIntWithEnvironment(loader, "gameserver.network.flood.tick", 1000),
				FloodShortWarn = GetIntWithEnvironment(loader, "gameserver.network.flood.short.warn", 10),
				FloodShortReject = GetIntWithEnvironment(loader, "gameserver.network.flood.short.reject", 20),
				FloodShortTick = GetIntWithEnvironment(loader, "gameserver.network.flood.short.tick", 10),
				FloodLongWarn = GetIntWithEnvironment(loader, "gameserver.network.flood.long.warn", 30),
				FloodLongReject = GetIntWithEnvironment(loader, "gameserver.network.flood.long.reject", 60),
				FloodLongTick = GetIntWithEnvironment(loader, "gameserver.network.flood.long.tick", 60),
			},
			GeoData = new GeoDataOptions
			{
				Enabled = GetBoolWithEnvironment(loader, "gameserver.geodata.enable", true),
				CanSeeEnabled = GetBoolWithEnvironment(loader, "gameserver.geodata.cansee.enable", true),
				FearEnabled = GetBoolWithEnvironment(loader, "gameserver.geodata.fear.enable", true),
				NpcMoveEnabled = GetBoolWithEnvironment(loader, "gameserver.geodata.npc.move", true),
				MaterialsEnabled = GetBoolWithEnvironment(loader, "gameserver.geodata.materials.enable", true),
				MaterialsShowDetails = GetBoolWithEnvironment(loader, "gameserver.geodata.materials.showdetails", false),
				ShieldsEnabled = GetBoolWithEnvironment(loader, "gameserver.geodata.shields.enable", true),
			},
			Ai = new GameServerAiOptions
			{
				NpcMovementEnabled = GetBoolWithEnvironment(loader, "gameserver.npcmovement.enable", true),
				NpcMovementMinimumDelaySeconds = GetIntWithEnvironment(loader, "gameserver.npcmovement.delay.minimum", 3),
				NpcMovementMaximumDelaySeconds = GetIntWithEnvironment(loader, "gameserver.npcmovement.delay.maximum", 15),
				NpcShoutsEnabled = GetBoolWithEnvironment(loader, "gameserver.npcshouts.enable", false),
				HandlerDirectory = GetWithEnvironment(loader, "gameserver.ai.handler_directory", "./data/handlers/ai"),
			},
			Housing = new GameServerHousingOptions
			{
				VisibilityDistance = GetFloatWithEnvironment(loader, "gameserver.housing.visibility.distance", 200f),
				AuctionsEnabled = GetBoolWithEnvironment(loader, "gameserver.housing.auction.enable", true),
				PayEnabled = GetBoolWithEnvironment(loader, "gameserver.housing.pay.enable", true),
				AuctionEndTime = GetWithEnvironment(loader, "gameserver.housing.auction.end_time", "0 0 12 ? * SUN"),
				AuctionRegisterDays = GetIntListWithEnvironment(loader, "gameserver.housing.auction.register_days", "1, 5"),
				MaintenanceTime = GetWithEnvironment(loader, "gameserver.housing.maintain.time", "0 0 0 ? * MON"),
				AuctionRegistrationFeePercent = GetFloatWithEnvironment(loader, "gameserver.housing.auction.registration_fee", 0.3f),
				AuctionSalesCommissionPercent = GetFloatWithEnvironment(loader, "gameserver.housing.auction.sales_commission", 0.1f),
				AuctionBidStepLimit = GetFloatWithEnvironment(loader, "gameserver.housing.auction.steplimit", 100f),
				HouseMinBidLevel = GetIntWithEnvironment(loader, "gameserver.housing.auction.bidding.min_level.house", 0),
				MansionMinBidLevel = GetIntWithEnvironment(loader, "gameserver.housing.auction.bidding.min_level.mansion", 0),
				EstateMinBidLevel = GetIntWithEnvironment(loader, "gameserver.housing.auction.bidding.min_level.estate", 0),
				PalaceMinBidLevel = GetIntWithEnvironment(loader, "gameserver.housing.auction.bidding.min_level.palace", 0),
			},
			Custom = new GameServerCustomOptions
			{
				ChallengeTasksEnabled = GetBoolWithEnvironment(loader, "gameserver.challenge.tasks.enabled", false),
				EnableEnchantAnnounce = GetBoolWithEnvironment(loader, "gameserver.enchant.announce.enable", true),
				SpeakingBetweenFactions = GetBoolWithEnvironment(loader, "gameserver.chat.factions.enable", false),
				LevelToWhisper = GetIntWithEnvironment(loader, "gameserver.chat.whisper.level", 10),
				BrokerRegistrationExpirationDays = GetIntWithEnvironment(loader, "gameserver.broker.registration_expiration_days", 8),
				FactionsSearchMode = GetBoolWithEnvironment(loader, "gameserver.search.factions.mode", false),
				SearchGmList = GetBoolWithEnvironment(loader, "gameserver.search.gm.list", false),
				LevelToSearch = GetIntWithEnvironment(loader, "gameserver.search.player.level", 10),
				EnableCrossFactionBinding = GetBoolWithEnvironment(loader, "gameserver.cross.faction.binding", false),
				EnableSimpleSecondClass = GetBoolWithEnvironment(loader, "gameserver.simple.secondclass.enable", false),
				SkillChainDisableTriggerRate = GetBoolWithEnvironment(loader, "gameserver.skill.chain.disable_triggerrate", false),
				BaseFlyTime = GetIntWithEnvironment(loader, "gameserver.base.flytime", 60),
				FriendListGmRestrict = GetBoolWithEnvironment(loader, "gameserver.friendlist.gm_restrict", false),
				FriendListSize = GetIntWithEnvironment(loader, "gameserver.friendlist.size", 90),
				BasicQuestSizeLimit = GetIntWithEnvironment(loader, "gameserver.basic.questsize.limit", 40),
				CubeExpansionLimit = GetIntWithEnvironment(loader, "gameserver.cube.expansion_limit", 11),
				NpcCubeExpandsSizeLimit = GetIntWithEnvironment(loader, "gameserver.npcexpands.limit", 5),
				EnableKinahCap = GetBoolWithEnvironment(loader, "gameserver.enable.kinah.cap", false),
				KinahCapValue = GetLongWithEnvironment(loader, "gameserver.kinah.cap.value", 999999999),
				EnableApCap = GetBoolWithEnvironment(loader, "gameserver.enable.ap.cap", false),
				ApCapValue = GetLongWithEnvironment(loader, "gameserver.ap.cap.value", 1000000),
				MentorGroupAp = GetBoolWithEnvironment(loader, "gameserver.noap.mentor.group", false),
				FactionUsePrice = GetIntWithEnvironment(loader, "gameserver.faction.price", 10000),
				FactionCommandChannel = GetBoolWithEnvironment(loader, "gameserver.faction.cmdchannel", true),
				FactionChatChannel = GetBoolWithEnvironment(loader, "gameserver.faction.chatchannels", false),
				PvpDayDuration = GetLongWithEnvironment(loader, "gameserver.pvp.dayduration", 86400000),
				MaxDailyPvpKills = GetIntWithEnvironment(loader, "gameserver.pvp.maxkills", 5),
				EnableKillReward = GetBoolWithEnvironment(loader, "gameserver.kill.reward.enable", false),
				KeepBuffsInColiseum = GetBoolWithEnvironment(loader, "gameserver.coliseum.keep_buffs", false),
				EnableKiskRestriction = GetBoolWithEnvironment(loader, "gameserver.kisk.restriction.enable", true),
				RiftEnabled = GetBoolWithEnvironment(loader, "gameserver.rift.enable", true),
				RiftDuration = GetIntWithEnvironment(loader, "gameserver.rift.duration", 1),
				VortexEnabled = GetBoolWithEnvironment(loader, "gameserver.vortex.enable", true),
				VortexBrusthoninSchedule = GetWithEnvironment(loader, "gameserver.vortex.brusthonin.schedule", "0 0 16 ? * SAT"),
				VortexTheobomosSchedule = GetWithEnvironment(loader, "gameserver.vortex.theobomos.schedule", "0 0 16 ? * SUN"),
				VortexDuration = GetIntWithEnvironment(loader, "gameserver.vortex.duration", 1),
				ConquerorAndProtectorSystemEnabled = GetBoolWithEnvironment(loader, "gameserver.cp.enable", true),
				ConquerorAndProtectorWorlds = GetIntSetWithEnvironment(loader, "gameserver.cp.worlds", "210020000,210040000,210050000,210070000,220020000,220040000,220070000,220080000"),
				ConquerorAndProtectorLevelDiff = GetIntWithEnvironment(loader, "gameserver.cp.level.diff", 5),
				ConquerorAndProtectorKillsDecreaseIntervalMinutes = GetIntWithEnvironment(loader, "gameserver.cp.kills.decrease_interval_minutes", 10),
				ConquerorAndProtectorKillsDecreaseCount = GetIntWithEnvironment(loader, "gameserver.cp.kills.decrease_count", 1),
				ConquerorAndProtectorKillsRank1 = GetIntWithEnvironment(loader, "gameserver.cp.kills.rank1", 1),
				ConquerorAndProtectorKillsRank2 = GetIntWithEnvironment(loader, "gameserver.cp.kills.rank2", 10),
				ConquerorAndProtectorKillsRank3 = GetIntWithEnvironment(loader, "gameserver.cp.kills.rank3", 20),
				LimitsEnabled = GetBoolWithEnvironment(loader, "gameserver.limits.enable", true),
				LimitsEnableDynamicCap = GetBoolWithEnvironment(loader, "gameserver.limits.enable_dynamic_cap", false),
				LimitsUpdate = GetWithEnvironment(loader, "gameserver.limits.update", "0 0 0 ? * *"),
				AbyssTransformLogout = GetBoolWithEnvironment(loader, "gameserver.abyssxform.afterlogout", false),
				TopRankingXformMinRank = GetAbyssRankIdWithEnvironment(loader, "gameserver.topranking.xform.min_rank", "STAR5_OFFICER"),
				EnableRideRestriction = GetBoolWithEnvironment(loader, "gameserver.ride.restriction.enable", true),
				SellingApItemsEnabled = GetBoolWithEnvironment(loader, "gameserver.selling.apitems.enabled", true),
				CharacterDeletionTimeMinutes = GetIntWithEnvironment(loader, "character.deletion.time.minutes", 5),
				IgnorePotionsAtFullHealth = GetBoolWithEnvironment(loader, "gameserver.items.ignore_potions_at_full_health", false),
				EnableStarterKit = GetBoolWithEnvironment(loader, "gameserver.custom.starter_kit.enable", false),
				PvpMapEnabled = GetBoolWithEnvironment(loader, "gameserver.pvpmap.enable", false),
				PvpMapApMultiplier = GetFloatWithEnvironment(loader, "gameserver.pvpmap.apmultiplier", 2f),
				PvpMapPveApMultiplier = GetFloatWithEnvironment(loader, "gameserver.pvpmap.pve.apmultiplier", 1f),
				PvpMapRandomBossBaseRate = GetIntWithEnvironment(loader, "gameserver.pvpmap.random_boss.rate", 40),
				PvpMapRandomBossSchedule = GetWithEnvironment(loader, "gameserver.pvpmap.random_boss.time", "0 30 14,18,21 ? * *"),
				GodstoneActivationRate = GetFloatWithEnvironment(loader, "gameserver.rates.godstone.activation.rate", 1.0f),
				GodstoneEvaluationCooldownMillis = GetIntWithEnvironment(loader, "gameserver.rates.godstone.evaluation.cooldown_millis", 750),
				DisabledEventNames = GetStringSetWithEnvironment(loader, "gameserver.event.service.disabled_events", string.Empty),
				CountSummonEffectsForCumulativeResist = GetBoolWithEnvironment(loader, "gameserver.pvp.cumulative_resist.count_summon_effects", false),
			},
			Cleaning = new CleaningOptions
			{
				Enabled = GetBoolWithEnvironment(loader, "gameserver.cleaning.enable", false),
				MinimumAccountInactivityDays = GetIntWithEnvironment(loader, "gameserver.cleaning.min_account_inactivity", 365),
				MaxDeletableCharacterLevel = GetIntWithEnvironment(loader, "gameserver.cleaning.max_level", 25),
			},
			Threads = new GameThreadOptions
			{
				BaseThreadPoolSize = GetIntWithEnvironment(loader, "gameserver.thread.base_pool_size", 0),
				ScheduledThreadPoolSize = GetIntWithEnvironment(loader, "gameserver.thread.scheduled_pool_size", 0),
				MaximumRuntimeWithoutWarningMillis = GetLongWithEnvironment(loader, "gameserver.thread.runtime", 5000),
				UsePriorities = GetBoolWithEnvironment(loader, "gameserver.thread.usepriority", false),
			},
			Names = new GameServerNameOptions
			{
				AllowCustomNames = GetBoolWithEnvironment(loader, "gameserver.name.allow.custom", false),
				CharacterPattern = GetWithEnvironment(loader, "gameserver.name.character_pattern", "[a-zA-Z]{2,16}"),
				PetPattern = GetWithEnvironment(loader, "gameserver.name.pet_pattern", "[a-zA-Z]{2,16}"),
				ForbiddenSequencePattern = GetWithEnvironment(loader, "gameserver.name.forbidden_sequences_pattern", string.Empty),
				ForbiddenWords = GetStringListWithEnvironment(loader, "gameserver.name.forbidden_words", string.Empty),
				ReserveOldNameDays = GetIntWithEnvironment(loader, "gameserver.name.reserve_old_name_days", 30),
			},
			Membership = new GameServerMembershipOptions
			{
				CharacterAdditionalEnable = GetByteWithEnvironment(loader, "gameserver.character.additional.enable", 10),
				CharacterAdditionalCount = GetByteWithEnvironment(loader, "gameserver.character.additional.count", 8),
				StigmaSlotQuest = GetByteWithEnvironment(loader, "gameserver.quest.stigma.slot", 10),
				StigmaAutoLearn = GetByteWithEnvironment(loader, "gameserver.autolearn.stigma", 10),
				InstancesCooldown = GetByteWithEnvironment(loader, "gameserver.instances.cooldown", 10),
				InstancesGroupRequirement = GetByteWithEnvironment(loader, "gameserver.instances.group.requirement", 10),
				QuestLimitDisabled = GetByteWithEnvironment(loader, "gameserver.quest.limit.disable", 10),
			},
			Instance = new GameServerInstanceOptions
			{
				CooldownRate = GetIntWithEnvironment(loader, "gameserver.instance.cooldown_rate", 1),
				CooldownRateExcludedMaps = GetIntSetWithEnvironment(loader, "gameserver.instance.cooldown_rate.excluded_maps", string.Empty),
				DestroyDelaySeconds = GetIntWithEnvironment(loader, "gameserver.instance.destroy_delay_seconds", 600),
				SoloDestroyDelaySeconds = GetIntWithEnvironment(loader, "gameserver.instance.solo.destroy_delay_seconds", 600),
				FormInstanceGroupAnywhere = GetBoolWithEnvironment(loader, "gameserver.instance_group.form_anywhere", false),
			},
			Group = new GameServerGroupOptions
			{
				GroupRemoveTimeSeconds = GetIntWithEnvironment(loader, "gameserver.playergroup.removetime", 600),
				AllianceRemoveTimeSeconds = GetIntWithEnvironment(loader, "gameserver.playeralliance.removetime", 600),
			},
			AutoGroup = new GameServerAutoGroupOptions
			{
				Enabled = GetBoolWithEnvironment(loader, "gameserver.autogroup.enable", true),
			},
			Administration = new GameServerAdministrationOptions
			{
				UnrestrictedItemTradeAccessLevel = GetIntWithEnvironment(loader, "gameserver.administration.unrestricted_itemtrade", 1),
				GmPanelAccessLevel = GetIntWithEnvironment(loader, "gameserver.administration.gm_panel", 2),
				FreeFlightAccessLevel = GetIntWithEnvironment(loader, "gameserver.administration.flight.free_fly", 1),
				InstanceEnterAllAccessLevel = GetIntWithEnvironment(loader, "gameserver.administration.instance.enter_all", 2),
				OperationalItemIds = LoadOperationalItemIds(startDirectory),
			},
			Rates = new GameServerRateOptions
			{
				ManastoneChances = GetFloatListWithEnvironment(loader, "gameserver.rates.manastone_chances", "75.0, 75.0"),
				EnchantmentStoneBaseChances = GetFloatListWithEnvironment(loader, "gameserver.rates.enchantment_stone.base_chances", "65.0, 65.0"),
				EnchantmentStoneAmplifiedChances = GetFloatListWithEnvironment(loader, "gameserver.rates.enchantment_stone.amplified_chances", "50.0, 50.0"),
				TamperingChances = GetFloatListWithEnvironment(loader, "gameserver.rates.tampering_chances", "65.0, 65.0"),
				ApPvpGainRates = GetFloatListWithEnvironment(loader, "gameserver.rates.ap.pvp.gain", "1.0, 2.0"),
				ApPvpLossRates = GetFloatListWithEnvironment(loader, "gameserver.rates.ap.pvp.loss", "1.0, 1.0"),
				ApPveRates = GetFloatListWithEnvironment(loader, "gameserver.rates.ap.pve", "1.0, 2.0"),
				ApQuestRates = GetFloatListWithEnvironment(loader, "gameserver.rates.ap.quest", "1.0, 2.0"),
				ApDredgionRates = GetFloatListWithEnvironment(loader, "gameserver.rates.ap.dredgion", "1.0, 2.0"),
				GpRates = GetFloatListWithEnvironment(loader, "gameserver.rates.gp.gain", "1.0, 2.0"),
				XpQuestRates = GetFloatListWithEnvironment(loader, "gameserver.rates.xp.quest", "1.0, 2.0"),
				QuestKinahRates = GetFloatListWithEnvironment(loader, "gameserver.rates.kinah.quest", "1.0, 2.0"),
				DropRates = GetFloatListWithEnvironment(loader, "gameserver.rates.drop", "1.0, 2.0"),
				PvpArenaDisciplineRewardRates = GetFloatListWithEnvironment(loader, "gameserver.rates.pvparena.discipline", "1.0, 2.0"),
				PvpArenaChaosRewardRates = GetFloatListWithEnvironment(loader, "gameserver.rates.pvparena.chaos", "1.0, 2.0"),
				PvpArenaHarmonyRewardRates = GetFloatListWithEnvironment(loader, "gameserver.rates.pvparena.harmony", "1.0, 2.0"),
				PvpArenaGloryRewardRates = GetFloatListWithEnvironment(loader, "gameserver.rates.pvparena.glory", "1.0, 2.0"),
			},
			Prices = new GameServerPriceOptions
			{
				DefaultPrices = GetIntWithEnvironment(loader, "gameserver.prices.default.prices", 100),
				DefaultModifier = GetIntWithEnvironment(loader, "gameserver.prices.default.modifier", 100),
				DefaultTaxes = GetIntWithEnvironment(loader, "gameserver.prices.default.taxes", 100),
				VendorBuyModifier = GetIntWithEnvironment(loader, "gameserver.prices.vendor.buymod", 100),
				VendorSellModifier = GetIntWithEnvironment(loader, "gameserver.prices.vendor.sellmod", 20),
			},
			PeriodicSave = new GameServerPeriodicSaveOptions
			{
				PlayerPetsSeconds = GetIntWithEnvironment(loader, "gameserver.periodicsave.player.pets", 10),
			},
			LoadedPropertyCount = loader.Count,
		};
	}

	public static DatabaseOptions LoadDatabaseOptionsFromJavaConfig(string startDirectory)
	{
		// Java parity: commons/database/DatabaseFactory reads game-server database.properties.
		var loader = LoadProperties(startDirectory);
		var jdbcUrl = GetWithEnvironment(loader, "database.url", "jdbc:mysql://localhost:3306/aion_gs");
		var (server, port, database) = DatabaseOptions.ParseJdbcMysqlUrl(jdbcUrl);
		return new DatabaseOptions
		{
			Server = server,
			Port = port,
			Database = database,
			UserId = GetWithEnvironment(loader, "database.user", "root"),
			Password = GetWithEnvironment(loader, "database.password", string.Empty),
			MaxPoolSize = GetIntWithEnvironment(loader, "database.connectionpool.connections.max", 5),
			ConnectionTimeout = GetIntWithEnvironment(loader, "database.connectionpool.timeout", 5000),
		};
	}

	public static IPEndPoint ParseEndPoint(string value)
	{
		var separator = value.LastIndexOf(':');
		if (separator <= 0 || separator == value.Length - 1)
			throw new FormatException($"Invalid socket address '{value}'. Expected host:port.");

		var host = value[..separator].Trim();
		var port = int.Parse(value[(separator + 1)..].Trim(), CultureInfo.InvariantCulture);
		var address = host == "0.0.0.0" || host == "*"
			? IPAddress.Any
			: Dns.GetHostAddresses(host).First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

		return new IPEndPoint(address, port);
	}

	private static ConfigLoader LoadProperties(string startDirectory)
	{
		// Java parity: commons/configuration loads game-server config directories then mygs.properties overrides.
		var loader = new ConfigLoader();
		var repoRoot = FindRepoRoot(startDirectory);
		if (repoRoot == null)
			return loader;

		var configRoot = Path.Combine(repoRoot, "game-server", "config");
		loader.LoadFromDirectory(Path.Combine(configRoot, "administration"));
		loader.LoadFromDirectory(Path.Combine(configRoot, "main"));
		loader.LoadFromDirectory(Path.Combine(configRoot, "network"));
		loader.LoadFromFile(Path.Combine(configRoot, "mygs.properties"));
		return loader;
	}

	private static string GetWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var raw = Environment.GetEnvironmentVariable(key)
			?? Environment.GetEnvironmentVariable(key.ToUpperInvariant().Replace('.', '_'))
			?? loader.Get(key, defaultValue);

		return ResolvePropertyReferences(raw, loader, [key]);
	}

	private static int GetIntWithEnvironment(ConfigLoader loader, string key, int defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString(CultureInfo.InvariantCulture));
		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
	}

	private static long GetLongWithEnvironment(ConfigLoader loader, string key, long defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString(CultureInfo.InvariantCulture));
		return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
	}

	private static byte GetByteWithEnvironment(ConfigLoader loader, string key, byte defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString(CultureInfo.InvariantCulture));
		return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
	}

	private static bool GetBoolWithEnvironment(ConfigLoader loader, string key, bool defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString(CultureInfo.InvariantCulture));
		return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
	}

	private static float GetFloatWithEnvironment(ConfigLoader loader, string key, float defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue.ToString(CultureInfo.InvariantCulture));
		return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : defaultValue;
	}

	private static IReadOnlySet<int> GetIntSetWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(v => int.Parse(v, CultureInfo.InvariantCulture))
			.ToHashSet();
	}

	private static IReadOnlyList<int> GetIntListWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(v => int.Parse(v, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static IReadOnlyList<float> GetFloatListWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.Select(v => float.Parse(v, CultureInfo.InvariantCulture))
			.ToArray();
	}

	private static IReadOnlyList<string> GetStringListWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
	}

	private static IReadOnlySet<string> GetStringSetWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		var value = GetWithEnvironment(loader, key, defaultValue);
		return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static int GetAbyssRankIdWithEnvironment(ConfigLoader loader, string key, string defaultValue)
	{
		// Java parity: configs/main/RankingConfig.XFORM_MIN_RANK binds AbyssRankEnum names.
		var value = GetWithEnvironment(loader, key, defaultValue).Trim();
		if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rankId))
			return rankId;

		return value.ToUpperInvariant() switch
		{
			"GRADE9_SOLDIER" => 1,
			"GRADE8_SOLDIER" => 2,
			"GRADE7_SOLDIER" => 3,
			"GRADE6_SOLDIER" => 4,
			"GRADE5_SOLDIER" => 5,
			"GRADE4_SOLDIER" => 6,
			"GRADE3_SOLDIER" => 7,
			"GRADE2_SOLDIER" => 8,
			"GRADE1_SOLDIER" => 9,
			"STAR1_OFFICER" => 10,
			"STAR2_OFFICER" => 11,
			"STAR3_OFFICER" => 12,
			"STAR4_OFFICER" => 13,
			"STAR5_OFFICER" => 14,
			"GENERAL" => 15,
			"GREAT_GENERAL" => 16,
			"COMMANDER" => 17,
			"SUPREME_COMMANDER" => 18,
			_ => 14,
		};
	}

	private static IReadOnlySet<int> LoadOperationalItemIds(string startDirectory)
	{
		// Java parity: services/AdminService.reload reads config/administration/item.restriction.txt.
		var repoRoot = FindRepoRoot(startDirectory);
		if (repoRoot == null)
			return new HashSet<int>();

		var path = Path.Combine(repoRoot, "game-server", "config", "administration", "item.restriction.txt");
		if (!File.Exists(path))
			return new HashSet<int>();

		var ids = new HashSet<int>();
		foreach (var line in File.ReadLines(path))
		{
			var trimmed = line.Split('#', 2)[0].Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
			if (trimmed.Length > 0 && int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var itemId))
				ids.Add(itemId);
		}

		return ids;
	}

	private static string ResolvePropertyReferences(string value, ConfigLoader loader, HashSet<string> visitedKeys)
	{
		var resolved = value;
		var searchStart = 0;
		while (searchStart < resolved.Length)
		{
			var start = resolved.IndexOf("${", searchStart, StringComparison.Ordinal);
			if (start < 0)
				return resolved;

			var end = resolved.IndexOf('}', start + 2);
			if (end < 0)
				return resolved;

			var referenceKey = resolved[(start + 2)..end];
			if (!visitedKeys.Add(referenceKey))
			{
				searchStart = end + 1;
				continue;
			}

			var replacement = Environment.GetEnvironmentVariable(referenceKey)
				?? Environment.GetEnvironmentVariable(referenceKey.ToUpperInvariant().Replace('.', '_'))
				?? loader.Get(referenceKey, string.Empty);
			replacement = ResolvePropertyReferences(replacement, loader, visitedKeys);
			visitedKeys.Remove(referenceKey);

			resolved = resolved[..start] + replacement + resolved[(end + 1)..];
			searchStart = start + replacement.Length;
		}

		return resolved;
	}

	private static string ParseTimeZoneId(string value)
	{
		return string.IsNullOrWhiteSpace(value) ? TimeZoneInfo.Local.Id : value;
	}

	private static string? FindRepoRoot(string startDirectory)
	{
		var directory = new DirectoryInfo(startDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "game-server", "config")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}

public sealed class GameServerCoreOptions
{
	public int ServerCountryCode { get; init; } = 99;

	public int PlayerMaxLevel { get; init; } = 65;

	public string TimeZoneId { get; init; } = TimeZoneInfo.Local.Id;

	public TimeZoneInfo GetTimeZone()
	{
		// Java parity: GSConfig.TIME_ZONE_ID is a ZoneId resolved from gameserver.timezone,
		// falling back to the system time zone when the property is empty.
		return TimeZoneInfo.FindSystemTimeZoneById(
			string.IsNullOrWhiteSpace(TimeZoneId) ? TimeZoneInfo.Local.Id : TimeZoneId);
	}

	public bool EnableChatServer { get; init; }

	public byte ChatServerMinLevel { get; init; } = 10;

	public int CharacterCreationMode { get; init; }

	public int CharacterLimitCount { get; init; } = 8;

	public int CharacterFactionLimitationMode { get; init; }

	public bool EnableRatioLimitation { get; init; }

	public int RatioMinValue { get; init; } = 60;

	public int RatioMinRequiredLevel { get; init; } = 10;

	public int RatioMinCharactersCount { get; init; } = 50;

	public int RatioHighPlayerCountDisabling { get; init; } = 500;

	public int CharacterReentryTimeSeconds { get; init; } = 20;

	public int MinimumSkillCastIntervalMillis { get; init; } = 350;

	public int ItemWrapLimit { get; init; }

	public bool EnableWebRewards { get; init; }

	public bool AnalyzeQuestHandlers { get; init; } = true;

	public string QuestHandlerDirectory { get; init; } = "./data/handlers/quest";
}

public sealed class GameServerNameOptions
{
	public bool AllowCustomNames { get; init; }

	public string CharacterPattern { get; init; } = "[a-zA-Z]{2,16}";

	public string PetPattern { get; init; } = "[a-zA-Z]{2,16}";

	public string ForbiddenSequencePattern { get; init; } = string.Empty;

	public IReadOnlyList<string> ForbiddenWords { get; init; } = Array.Empty<string>();

	public int ReserveOldNameDays { get; init; } = 30;

	public Regex CreateCharacterNameRegex()
	{
		return new Regex($"^(?:{CharacterPattern})$", RegexOptions.CultureInvariant);
	}

	public Regex CreatePetNameRegex()
	{
		return new Regex($"^(?:{PetPattern})$", RegexOptions.CultureInvariant);
	}

	public Regex? CreateForbiddenSequenceRegex()
	{
		return string.IsNullOrWhiteSpace(ForbiddenSequencePattern)
			? null
			: new Regex(ForbiddenSequencePattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
	}
}

public sealed class GameServerMembershipOptions
{
	public byte CharacterAdditionalEnable { get; init; } = 10;

	public byte CharacterAdditionalCount { get; init; } = 8;

	public byte StigmaSlotQuest { get; init; } = 10;

	public byte StigmaAutoLearn { get; init; } = 10;

	public byte InstancesCooldown { get; init; } = 10;

	public byte InstancesGroupRequirement { get; init; } = 10;

	public byte QuestLimitDisabled { get; init; } = 10;
}

public sealed class GameServerInstanceOptions
{
	public int CooldownRate { get; init; } = 1;

	public IReadOnlySet<int> CooldownRateExcludedMaps { get; init; } = new HashSet<int>();

	// Java parity: configs/main/InstanceConfig.INSTANCE_DESTROY_DELAY_SECONDS.
	public int DestroyDelaySeconds { get; init; } = 600;

	// Java parity: configs/main/InstanceConfig.SOLO_INSTANCE_DESTROY_DELAY_SECONDS.
	public int SoloDestroyDelaySeconds { get; init; } = 600;

	// Java parity: configs/main/GroupConfig.FORM_INSTANCE_GROUP_ANYWHERE.
	public bool FormInstanceGroupAnywhere { get; init; }
}

public sealed class GameServerGroupOptions
{
	// Java parity: configs/main/GroupConfig.GROUP_REMOVE_TIME.
	public int GroupRemoveTimeSeconds { get; init; } = 600;

	// Java parity: configs/main/GroupConfig.ALLIANCE_REMOVE_TIME.
	public int AllianceRemoveTimeSeconds { get; init; } = 600;
}

public sealed class GameServerAutoGroupOptions
{
	// Java parity: configs/main/AutoGroupConfig.AUTO_GROUP_ENABLE.
	public bool Enabled { get; init; } = true;

	// Java parity: configs/main/AutoGroupConfig.ANNOUNCE_BATTLEGROUND_REGISTRATIONS.
	public bool AnnounceBattlegroundRegistrations { get; init; }
}

public sealed class GameServerRateOptions
{
	public IReadOnlyList<float> ManastoneChances { get; init; } = [75f, 75f];

	public IReadOnlyList<float> EnchantmentStoneBaseChances { get; init; } = [65f, 65f];

	public IReadOnlyList<float> EnchantmentStoneAmplifiedChances { get; init; } = [50f, 50f];

	public IReadOnlyList<float> TamperingChances { get; init; } = [65f, 65f];

	// Java parity: configs/main/RatesConfig AP_*_RATES consumed by model/gameobjects/player/Rates.
	public IReadOnlyList<float> ApPvpGainRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> ApPvpLossRates { get; init; } = [1f, 1f];

	public IReadOnlyList<float> ApPveRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> ApQuestRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> ApDredgionRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> GpRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> XpQuestRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> QuestKinahRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> DropRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> PvpArenaDisciplineRewardRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> PvpArenaChaosRewardRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> PvpArenaHarmonyRewardRates { get; init; } = [1f, 2f];

	public IReadOnlyList<float> PvpArenaGloryRewardRates { get; init; } = [1f, 2f];
}

public sealed class GameServerPriceOptions
{
	// Java parity: configs/main/PricesConfig.DEFAULT_PRICES consumed by PricesService.getGlobalPrices.
	public int DefaultPrices { get; init; } = 100;

	// Java parity: configs/main/PricesConfig.DEFAULT_MODIFIER consumed by PricesService.getGlobalPricesModifier.
	public int DefaultModifier { get; init; } = 100;

	// Java parity: configs/main/PricesConfig.DEFAULT_TAXES consumed by PricesService.getTaxes.
	public int DefaultTaxes { get; init; } = 100;

	// Java parity: configs/main/PricesConfig.VENDOR_BUY_MODIFIER consumed by PricesService.getVendorBuyModifier.
	public int VendorBuyModifier { get; init; } = 100;

	// Java parity: configs/main/PricesConfig.VENDOR_SELL_MODIFIER consumed by PricesService.getVendorSellModifier.
	public int VendorSellModifier { get; init; } = 20;
}

public sealed class GameServerAdministrationOptions
{
	public int UnrestrictedItemTradeAccessLevel { get; init; } = 1;

	public int GmPanelAccessLevel { get; init; } = 2;

	public int FreeFlightAccessLevel { get; init; } = 1;

	public int InstanceEnterAllAccessLevel { get; init; } = 2;

	public IReadOnlySet<int> OperationalItemIds { get; init; } = new HashSet<int>();
}

public sealed class GameServerNetworkOptions
{
	public IPEndPoint ClientEndPoint { get; init; } = new(IPAddress.Any, 7777);

	public IPEndPoint ClientConnectEndPoint { get; init; } = new(IPAddress.Any, 7777);

	public IPEndPoint LoginEndPoint { get; init; } = new(IPAddress.Loopback, 9014);

	public IPEndPoint ChatEndPoint { get; init; } = new(IPAddress.Loopback, 9021);

	public string ChatPassword { get; init; } = string.Empty;

	public int GameServerId { get; init; } = 1;

	public string LoginPassword { get; init; } = string.Empty;

	public int MinimumAccessLevel { get; init; }

	public int MaxOnlinePlayers { get; init; } = 100;

	public int NioReadWriteThreads { get; init; } = 1;

	public bool NioReadWriteThreadsUnsafeAllow { get; init; }

	public int PacketProcessorMinThreads { get; init; } = 4;

	public int PacketProcessorMaxThreads { get; init; } = 4;

	public int PacketProcessorThreadKillThreshold { get; init; } = 3;

	public int PacketProcessorThreadSpawnThreshold { get; init; } = 50;

	public bool LogUnknownPackets { get; init; }

	public bool LogIgnoredPackets { get; init; }

	public bool EnableFloodConnections { get; init; }

	public int FloodTickMillis { get; init; } = 1000;

	public int FloodShortWarn { get; init; } = 10;

	public int FloodShortReject { get; init; } = 20;

	public int FloodShortTick { get; init; } = 10;

	public int FloodLongWarn { get; init; } = 30;

	public int FloodLongReject { get; init; } = 60;

	public int FloodLongTick { get; init; } = 60;
}

public sealed class GeoDataOptions
{
	public bool Enabled { get; init; } = true;

	public bool CanSeeEnabled { get; init; } = true;

	public bool FearEnabled { get; init; } = true;

	public bool NpcMoveEnabled { get; init; } = true;

	public bool MaterialsEnabled { get; init; } = true;

	public bool MaterialsShowDetails { get; init; }

	public bool ShieldsEnabled { get; init; } = true;
}

public sealed class GameServerAiOptions
{
	public bool NpcMovementEnabled { get; init; } = true;

	public int NpcMovementMinimumDelaySeconds { get; init; } = 3;

	public int NpcMovementMaximumDelaySeconds { get; init; } = 15;

	public bool NpcShoutsEnabled { get; init; }

	public string HandlerDirectory { get; init; } = "./data/handlers/ai";
}

public sealed class GameServerHousingOptions
{
	public float VisibilityDistance { get; init; } = 200f;

	public bool AuctionsEnabled { get; init; } = true;

	public bool PayEnabled { get; init; } = true;

	public string AuctionEndTime { get; init; } = "0 0 12 ? * SUN";

	public IReadOnlyList<int> AuctionRegisterDays { get; init; } = [1, 5];

	public string MaintenanceTime { get; init; } = "0 0 0 ? * MON";

	public float AuctionRegistrationFeePercent { get; init; } = 0.3f;

	public float AuctionSalesCommissionPercent { get; init; } = 0.1f;

	public float AuctionBidStepLimit { get; init; } = 100f;

	public int HouseMinBidLevel { get; init; }

	public int MansionMinBidLevel { get; init; }

	public int EstateMinBidLevel { get; init; }

	public int PalaceMinBidLevel { get; init; }
}

public sealed class GameServerCustomOptions
{
	public bool ChallengeTasksEnabled { get; init; }

	public bool EnableEnchantAnnounce { get; init; } = true;

	public bool SpeakingBetweenFactions { get; init; }

	public int LevelToWhisper { get; init; } = 10;

	public int BrokerRegistrationExpirationDays { get; init; } = 8;

	public bool FactionsSearchMode { get; init; }

	public bool SearchGmList { get; init; }

	public int LevelToSearch { get; init; } = 10;

	public bool EnableCrossFactionBinding { get; init; }

	public bool EnableSimpleSecondClass { get; init; }

	public bool SkillChainDisableTriggerRate { get; init; }

	public int BaseFlyTime { get; init; } = 60;

	public bool FriendListGmRestrict { get; init; }

	public int FriendListSize { get; init; } = 90;

	public int BasicQuestSizeLimit { get; init; } = 40;

	public int CubeExpansionLimit { get; init; } = 11;

	public int NpcCubeExpandsSizeLimit { get; init; } = 5;

	public bool EnableKinahCap { get; init; }

	public long KinahCapValue { get; init; } = 999999999;

	public bool EnableApCap { get; init; }

	public long ApCapValue { get; init; } = 1000000;

	public bool MentorGroupAp { get; init; }

	public int FactionUsePrice { get; init; } = 10000;

	public bool FactionCommandChannel { get; init; } = true;

	public bool FactionChatChannel { get; init; }

	public long PvpDayDuration { get; init; } = 86400000;

	public int MaxDailyPvpKills { get; init; } = 5;

	public bool EnableKillReward { get; init; }

	public bool KeepBuffsInColiseum { get; init; }

	public bool EnableKiskRestriction { get; init; } = true;

	public bool RiftEnabled { get; init; } = true;

	public int RiftDuration { get; init; } = 1;

	public bool VortexEnabled { get; init; } = true;

	public string VortexBrusthoninSchedule { get; init; } = "0 0 16 ? * SAT";

	public string VortexTheobomosSchedule { get; init; } = "0 0 16 ? * SUN";

	public int VortexDuration { get; init; } = 1;

	public bool ConquerorAndProtectorSystemEnabled { get; init; } = true;

	public IReadOnlySet<int> ConquerorAndProtectorWorlds { get; init; } = new HashSet<int>();

	public int ConquerorAndProtectorLevelDiff { get; init; } = 5;

	public int ConquerorAndProtectorKillsDecreaseIntervalMinutes { get; init; } = 10;

	public int ConquerorAndProtectorKillsDecreaseCount { get; init; } = 1;

	public int ConquerorAndProtectorKillsRank1 { get; init; } = 1;

	public int ConquerorAndProtectorKillsRank2 { get; init; } = 10;

	public int ConquerorAndProtectorKillsRank3 { get; init; } = 20;

	public bool LimitsEnabled { get; init; } = true;

	public bool LimitsEnableDynamicCap { get; init; }

	public string LimitsUpdate { get; init; } = "0 0 0 ? * *";

	public bool AbyssTransformLogout { get; init; }

	public int TopRankingXformMinRank { get; init; } = 14;

	public bool EnableRideRestriction { get; init; } = true;

	public bool SellingApItemsEnabled { get; init; } = true;

	public int CharacterDeletionTimeMinutes { get; init; } = 5;

	public bool IgnorePotionsAtFullHealth { get; init; }

	public bool EnableStarterKit { get; init; }

	public bool PvpMapEnabled { get; init; }

	public float PvpMapApMultiplier { get; init; } = 2f;

	public float PvpMapPveApMultiplier { get; init; } = 1f;

	public int PvpMapRandomBossBaseRate { get; init; } = 40;

	public string PvpMapRandomBossSchedule { get; init; } = "0 30 14,18,21 ? * *";

	public float GodstoneActivationRate { get; init; } = 1f;

	public int GodstoneEvaluationCooldownMillis { get; init; } = 750;

	public IReadOnlySet<string> DisabledEventNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	public bool CountSummonEffectsForCumulativeResist { get; init; }
}

public sealed class CleaningOptions
{
	public bool Enabled { get; init; }

	public int MinimumAccountInactivityDays { get; init; } = 365;

	public int MaxDeletableCharacterLevel { get; init; } = 25;
}

public sealed class GameThreadOptions
{
	public int BaseThreadPoolSize { get; init; }

	public int ScheduledThreadPoolSize { get; init; }

	public long MaximumRuntimeWithoutWarningMillis { get; init; } = 5000;

	public bool UsePriorities { get; init; }
}

public sealed class GameServerPeriodicSaveOptions
{
	public int PlayerPetsSeconds { get; init; } = 10;
}
