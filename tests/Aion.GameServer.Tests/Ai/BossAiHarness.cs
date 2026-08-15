using System.Reflection;
using System.Runtime.CompilerServices;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.World.Geo;
using Aion.GameServer.World.Knownlist;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Model.Templates.World;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using GameWorld = Aion.GameServer.World.World;
using Spawner = Aion.GameServer.SpawnEngine.SpawnEngine;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// A headless single-map game world for exercising one boss encounter's AI.
/// </summary>
/// <remarks>
/// Boss AI classes are the least test-covered code in the port and the place retail-fidelity work lands
/// (see <c>docs/retail-ai-fidelity.md</c>), yet a rotation can only be observed by playing the fight.
/// This harness stands up the smallest world in which an AI behaves normally — one map, one map instance,
/// the real spawn path, real <c>npc_skills</c> levels — and swaps the thread pool for
/// <see cref="VirtualThreadPool"/> so battle timers can be fast-forwarded.
/// <para>
/// What it deliberately does NOT provide: geodata, the skill engine's execution side, packets to a client,
/// or movement. Assertions therefore observe an AI's <em>decisions</em> — which skills it queued and against
/// which target attribute, which adds it spawned, which HP phases it entered — rather than damage.
/// That is the layer at which the retail patterns are specified, so it is the layer worth pinning.
/// </para>
/// </remarks>
public sealed class BossAiHarness : IDisposable
{
	/// <summary>Beshmundir Temple; any id works, it only has to be consistent within a harness.</summary>
	public const int DefaultMapId = 300100000;

	/// <summary>Side of the synthetic map, in world units. See <see cref="Builder.WithWorldSize"/>.</summary>
	public const int DefaultWorldSize = 1024;

	private readonly DataManager? _previousDataManager;
	private readonly GameWorld? _previousWorld;
	private readonly ThreadPoolManager? _previousThreadPool;
	private readonly bool _previousCanSee;
	private readonly bool _previousGeoEnable;
	private readonly bool _previousGeoNpcMove;
	private readonly bool _previousGeoMaterials;
	private readonly bool _previousInstanceScaling;
	private readonly bool _previousShouts;
	private readonly int _mapId;

	private BossAiHarness(int mapId, DataManager dataManager, GameWorld world, VirtualThreadPool clock)
	{
		_mapId = mapId;
		Data = dataManager;
		World = world;
		Clock = clock;

		_previousDataManager = DataManager.GetRegisteredInstance();
		_previousWorld = TryGetWorld();
		_previousThreadPool = TryGetThreadPool();
		_previousCanSee = GeoDataConfig.CANSEE_ENABLE;
		_previousGeoEnable = GeoDataConfig.GEO_ENABLE;
		_previousGeoNpcMove = GeoDataConfig.GEO_NPC_MOVE;
		_previousGeoMaterials = GeoDataConfig.GEO_MATERIALS_ENABLE;
		_previousInstanceScaling = InstanceConfig.INSTANCE_SCALING_ENABLE;
		_previousShouts = AIConfig.SHOUTS_ENABLE;
	}

	public VirtualThreadPool Clock { get; }

	public DataManager Data { get; }

	public GameWorld World { get; }

	public static Builder For(int mapId = DefaultMapId) => new Builder(mapId);

	/// <summary>Spawns an NPC through the production spawn path, so it is genuinely live in the world.</summary>
	public Npc Spawn(int npcId, float x = 980f, float y = 135f, float z = 242f, sbyte heading = 0)
	{
		var spawn = Spawner.NewSingleTimeSpawn(_mapId, npcId, x, y, z, (byte)heading);
		var spawned = Spawner.SpawnObject(spawn, 1);
		Assert.NotNull(spawned);
		return Assert.IsType<Npc>(spawned);
	}

	/// <summary>
	/// Puts a real <see cref="Player"/> into the harness map: the simulated player an encounter fights.
	/// </summary>
	/// <remarks>
	/// A second NPC would be simpler, but several AI branches (aggro bookkeeping, shout targeting, message
	/// parameters) are typed on <c>Player</c>, and NPC-vs-NPC hostility runs through the tribe graph instead
	/// of the player one. The player has no client connection, which is exactly what makes it usable here:
	/// <c>PacketSendUtility.SendPacket</c> checks <c>IsOnline()</c> and no-ops, so nothing tries to serialise.
	/// </remarks>
	public Player SpawnPlayer(float x = 985f, float y = 135f, float z = 242f, sbyte heading = 0, Race race = Race.ELYOS)
	{
		var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
		common.SetPlayerClass(PlayerClass.WARRIOR);
		common.SetRace(race);
		common.SetGender(Gender.MALE);
		common.SetName("Harness" + common.GetPlayerObjId());
		common.SetNote("");
		var player = new HarnessPlayer(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
		player.SetKnownlist(new KnownList(player));
		// Must be the Player-specific controller: production builds one in Player's own setup, and code that
		// applies an effect to a player casts to it (a plain EffectController throws InvalidCastException).
		player.SetEffectController(new PlayerEffectController(player));

		// Player's ctor leaves position null (production fills it from the DB row on enter-world), and both
		// World.StoreObject and World.SetPosition dereference it, so the first position is set directly.
		player.SetPosition(World.CreatePosition(_mapId, x, y, z, (byte)heading, 1));
		World.StoreObject(player);
		World.Spawn(player);
		return player;
	}

	/// <summary>
	/// Puts <paramref name="npc"/> into combat with <paramref name="attacker"/> without running the
	/// attack/movement managers.
	/// </summary>
	/// <remarks>
	/// <c>AttackEventHandler.OnAttack</c> only enters <c>AttackManager.StartAttacking</c> when it is the call
	/// that flips the AI into <see cref="AIState.FIGHT"/>. Setting FIGHT first therefore delivers the real
	/// <c>AiEventType.Attack</c> to the AI class (so its <c>HandleAttack</c> override runs exactly as in
	/// production) while leaving the auto-attack/move machinery — which needs geodata and a client — out.
	/// </remarks>
	/// <summary>
	/// Puts <paramref name="npc"/> into a fight with <paramref name="attacker"/>.
	/// </summary>
	/// <remarks>
	/// The hate is load-bearing. Without it the AI finds no most-hated target and goes home the moment it
	/// next thinks, and <c>HandleBackHome</c> resets HP, cleans up spawned adds and re-arms
	/// <c>HpPhases</c> — so a phase fires and is wiped before the next assertion, which reads as a fight
	/// that never progressed at all.
	/// <para>
	/// Setting <c>FIGHT</c> before delivering the event is deliberate: <c>AttackEventHandler.OnAttack</c>
	/// only enters <c>AttackManager.StartAttacking</c> when it is the call that flips the state, so this
	/// delivers a genuine Attack event — the AI's own <c>HandleAttack</c> override runs exactly as in
	/// production — while leaving geodata, movement and packets out of it.
	/// </para>
	/// </remarks>
	public void Engage(Npc npc, Creature attacker)
	{
		MakeMutuallyKnown(npc, attacker);
		// Order matters: the state flip has to happen before the hate, or AddHate's own aggro handling
		// flips it first and the Attack event below no longer takes the path that starts the fight.
		npc.GetAi().SetStateIfNot(AIState.FIGHT);
		npc.SetTarget(attacker);
		npc.GetAggroList().AddHate(attacker, InitialHate);
		npc.GetAi().OnCreatureEvent(AiEventType.Attack, attacker);
	}

	/// <summary>Enough hate to keep the boss engaged across a test's worth of ticks.</summary>
	public const int InitialHate = 1000;

	/// <summary>Tops the attacker's hate back up, for tests that drive many ticks.</summary>
	public static void Rehate(Npc npc, Creature attacker) => npc.GetAggroList().AddHate(attacker, InitialHate);

	/// <summary>Makes two objects visible to each other, which aggro and message broadcast both require.</summary>
	public static void MakeMutuallyKnown(VisibleObject a, VisibleObject b)
	{
		a.GetKnownList().Add(b);
		b.GetKnownList().Add(a);
	}

	/// <summary>Drops <paramref name="npc"/> to an exact HP percentage without going through combat.</summary>
	public static void SetHpPercent(Npc npc, int percent) => npc.GetLifeStats().SetCurrentHpPercent(percent);

	/// <summary>Restores the stand-in player to full health.</summary>
	/// <remarks>
	/// Rarely needed now that the stand-in is invulnerable, but harmless for tests that want a known
	/// starting point.
	/// </remarks>
	public static void KeepAlive(Player player) => player.GetLifeStats().SetCurrentHpPercent(100);

	/// <summary>Removes and returns everything the NPC has queued since the last drain, in cast order.</summary>
	public static List<QueuedCast> DrainQueuedSkills(Npc npc)
	{
		var drained = new List<QueuedCast>();
		while (true)
		{
			NpcSkillEntry? next = npc.GetNextQueuedSkill();
			if (next == null)
				break;
			npc.RemoveNextQueuedSkill(next);
			drained.Add(new QueuedCast(next.GetSkillId(), next.GetSkillLevel(), next.GetTemplate().GetTarget()));
		}

		return drained;
	}

	/// <summary>Every NPC currently alive in the harness map instance, boss included.</summary>
	public List<Npc> LiveNpcs()
	{
		var found = new List<Npc>();
		World.GetWorldMap(_mapId).GetMainWorldMapInstance().ForEachObject(o =>
		{
			if (o is Npc npc && npc.IsSpawned())
				found.Add(npc);
		});
		return found;
	}

	public void Dispose()
	{
		GeoDataConfig.CANSEE_ENABLE = _previousCanSee;
		GeoDataConfig.GEO_ENABLE = _previousGeoEnable;
		GeoDataConfig.GEO_NPC_MOVE = _previousGeoNpcMove;
		GeoDataConfig.GEO_MATERIALS_ENABLE = _previousGeoMaterials;
		InstanceConfig.INSTANCE_SCALING_ENABLE = _previousInstanceScaling;
		AIConfig.SHOUTS_ENABLE = _previousShouts;
		DataManager.RestoreInstance(_previousDataManager);
		// The World and ThreadPoolManager bridges have no unregister. Leaving the harness's own instances bound
		// would hand a later test a world full of this encounter's corpses, or a clock that never ticks, so a
		// blank replacement goes in when there was nothing registered before.
		GameWorld.RegisterInstance(_previousWorld ?? new GameWorld(NullLogger<GameWorld>.Instance));
		ThreadPoolManager.RegisterInstance(
			_previousThreadPool ?? new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance));
	}

	private static GameWorld? TryGetWorld()
	{
		try
		{
			return GameWorld.GetInstance();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	private static ThreadPoolManager? TryGetThreadPool()
	{
		try
		{
			return ThreadPoolManager.GetInstance();
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}

	/// <summary>One entry of an NPC's pending skill queue.</summary>
	/// <summary>
	/// A stand-in player that cannot be killed.
	/// </summary>
	/// <remarks>
	/// Bosses under test cast damaging skills, and a level-1 stand-in dies to them immediately.
	/// <c>PlayerController.OnDie</c> then runs its reward path into <c>PvpService</c>, a lazy singleton
	/// whose constructor reads kill-bounty data and reaches <c>HeadhuntingDAO</c> — a database call that
	/// cannot be stood up headless. Topping the player up between steps does not help, because the death
	/// happens inside the step that damaged it.
	/// <para>
	/// <c>CreatureLifeStats.ReduceHp</c> returns early for an invulnerable owner, so the damage simply
	/// does not land. Nothing under test depends on the stand-in taking damage: these tests observe what
	/// the boss decides to do, not what its skills do to a target.
	/// </para>
	/// </remarks>
	private sealed class HarnessPlayer : Player
	{
		public HarnessPlayer(PlayerAccountData accountData, Account account)
			: base(accountData, account)
		{
		}

		public override bool IsInvulnerable() => true;
	}

	public readonly record struct QueuedCast(int SkillId, int Level, NpcSkillTargetAttribute Target)
	{
		public override string ToString() => $"{SkillId}@{Level}->{Target}";
	}

	public sealed class Builder
	{
		private readonly int _mapId;
		private readonly List<Type> _aiHandlers = new();
		private int _worldSize = DefaultWorldSize;

		internal Builder(int mapId) => _mapId = mapId;

		/// <summary>
		/// Widens the synthetic map so far-flung coordinates resolve to a region.
		/// </summary>
		/// <remarks>
		/// Anything outside the grid makes <c>World.CreatePosition</c> throw, and a boss whose
		/// <c>HandleSpawned</c> places objects at hardcoded world coordinates then fails to finish spawning —
		/// after which <c>HpPhases.TryEnterNextPhase</c> silently does nothing, because it requires
		/// <c>IsSpawned()</c>. Gelkmaros Padmarashka's shield NPCs sit out past 2900, so she needs this.
		/// </remarks>
		public Builder WithWorldSize(int worldSize)
		{
			_worldSize = worldSize;
			return this;
		}

		/// <summary>
		/// Registers the AI handler classes this encounter needs.
		/// </summary>
		/// <remarks>
		/// <c>AIEngine.Init()</c> would discover all 460 of them by reflection, but it also validates that every
		/// <c>ai_name</c> in npc_templates has a handler and is not idempotent, so registering the handful under
		/// test by type is both faster and safe to repeat across test classes.
		/// </remarks>
		public Builder WithAi(params Type[] aiHandlerTypes)
		{
			_aiHandlers.AddRange(aiHandlerTypes);
			return this;
		}

		public BossAiHarness Build()
		{
			GeoDataConfig.CANSEE_ENABLE = false;
			GeoDataConfig.GEO_MATERIALS_ENABLE = false;
			// No geo data is loaded, and GeoService indexes its map dictionary directly. Any NPC that
			// actually moves — an add walking to the player, say — reaches GeoService.GetZ through
			// NpcMoveController and throws KeyNotFoundException on the map id, inside MoveTaskManager's
			// parallel loop where it surfaces as an AggregateException from an unrelated clock advance.
			GeoDataConfig.GEO_ENABLE = false;
			GeoDataConfig.GEO_NPC_MOVE = false;
			InstanceConfig.INSTANCE_SCALING_ENABLE = false;
			AIConfig.SHOUTS_ENABLE = false;

			EnsureIdFactory();
			RegisterAiHandlers(_aiHandlers);

			var staticData = BuildStaticData(_mapId, _worldSize);
			var dataManager = NewDataManager(staticData);
			var clock = new VirtualThreadPool();
			var world = new GameWorld(NullLogger<GameWorld>.Instance);

			var harness = new BossAiHarness(_mapId, dataManager, world, clock);

			// Order mirrors GameServerBootstrapService: pool, ids, data, then the world that reads the data.
			ThreadPoolManager.RegisterInstance(clock);
			DataManager.RegisterInstance(dataManager);
			world.LoadWorldMaps(staticData.WorldMaps2);
			GameWorld.RegisterInstance(world);

			// GeoService indexes its map dictionary directly, and not every caller checks the geo config
			// first — StaggerEffect resolves a knockback position through it unconditionally, so any skill
			// carrying that effect throws KeyNotFoundException on the map id. Init() with GEO_ENABLE off
			// puts an empty GeoMap in for each world map and loads no geo files, which is exactly what a
			// headless fight wants: the lookups resolve and return nothing.
			GeoService.GetInstance().Init();
			return harness;
		}

		private static void EnsureIdFactory()
		{
			try
			{
				_ = IDFactory.GetInstance();
			}
			catch (InvalidOperationException)
			{
				IDFactory.RegisterInstance(new IDFactory());
			}
		}

		/// <summary>
		/// <c>AIEngine.RegisterAI</c> throws on a duplicate name and the engine is a process-wide singleton, so
		/// registration goes through <see cref="TestAiEngine"/>, which tracks what this suite has bound and keeps
		/// it from colliding with the boot tests that load the full handler set.
		/// </summary>
		private static void RegisterAiHandlers(List<Type> types) => TestAiEngine.Register(types);

		private static StaticData BuildStaticData(int mapId, int worldSize)
		{
			// GetUninitializedObject skips StaticData's field initializers (which would build every holder);
			// only the handful the AI/spawn path reads are filled in. Same trick the golden tests use.
			var staticData = (StaticData)RuntimeHelpers.GetUninitializedObject(typeof(StaticData));

			// The four real holders the AI path reads. Using the shipped data rather than hand-built templates
			// means a test spawns the ACTUAL boss — its level, rating, tribe, skill list and skill levels — so
			// claims like "casts Shockwave at the level its npc_skills entry defines" are genuinely checked,
			// and a wrong npc id or a broken ai_name binding fails the test instead of passing silently.
			SetHolder(staticData, nameof(StaticData.NpcDataDh), RealNpcs.Value);
			SetHolder(staticData, nameof(StaticData.NpcSkillDataDh), RealNpcSkills.Value);
			// NpcSkillList silently drops any npc_skill with no skill_templates entry, so this one is load-bearing.
			SetHolder(staticData, nameof(StaticData.SkillDataDh), RealSkills.Value);
			// Aggro (AggroList.IsAware -> Npc.IsEnemy -> TribeRelationService) needs the real tribe graph.
			SetHolder(staticData, nameof(StaticData.TribeRelations), RealTribeRelations.Value);

			// The skill engine's execution side reads this when resolving target relations, so a boss that
			// casts through SkillEngine rather than the queue NREs without it. Empty is correct here: no
			// skill under test is a material skill.
			var materials = new MaterialData { materialTemplates = new List<Aion.GameServer.Model.Templates.Materials.MaterialTemplate>() };
			materials.AfterUnmarshal(null!);
			SetHolder(staticData, nameof(StaticData.Materials), materials);

			var zoneData = new ZoneData { zoneList = new List<Aion.GameServer.Model.Templates.Zone.ZoneTemplate>() };
			zoneData.AfterUnmarshal(null!);
			SetHolder(staticData, nameof(StaticData.ZoneInfo), zoneData);

			var worldMaps = new WorldMapsData();
			worldMaps.SetData(new List<WorldMapTemplate>
			{
				new WorldMapTemplate
				{
					MapId = mapId,
					Name = "harness-map",
					CName = "harness-map",
					// Regions are 128 units square, so the default is a 9x9 grid — plenty for one encounter
					// and cheap to allocate. Coordinates outside it make World.CreatePosition throw; see
					// WithWorldSize for bosses that place objects at real world coordinates.
					WorldSize = worldSize,
					WaterLevel = 0,
					TwinCount = 1,
				},
			});
			SetHolder(staticData, nameof(StaticData.WorldMaps2), worldMaps);
			// Player's ctor builds an AbsoluteStatOwner, which reads this table.
			SetHolder(staticData, nameof(StaticData.AbsoluteStatsDataDh), new AbsoluteStatsData());
			return staticData;
		}

		// Parsed once per test process and shared by every harness (~2 s total, dominated by the 12.5 MB
		// skill table). These are read-only from the harness's point of view.
		private static readonly Lazy<NpcData> RealNpcs = new(() =>
			LoadStaticDataFile<NpcData>("npcs", "npc_templates.xml"));

		private static readonly Lazy<NpcSkillData> RealNpcSkills = new(() =>
			LoadStaticDataFile<NpcSkillData>("npc_skills", "npc_skills.xml"));

		private static readonly Lazy<SkillData> RealSkills = new(() =>
			LoadStaticDataFile<SkillData>("skills", "skill_templates.xml"));

		private static readonly Lazy<TribeRelationsData> RealTribeRelations = new(() =>
			LoadStaticDataFile<TribeRelationsData>("tribe", "tribe_relations.xml"));

		private static T LoadStaticDataFile<T>(params string[] relativePath) where T : class
		{
			string path = Path.Combine(
				new[] { RepoRoot(), "game-server", "data", "static_data" }.Concat(relativePath).ToArray());
			return Aion.GameServer.Dataholders.LoadingUtils.JaxbHolderLoader.LoadFromFile<T>(path);
		}

		private static void SetHolder(StaticData staticData, string propertyName, object value)
		{
			typeof(StaticData).GetProperty(propertyName)!.SetValue(staticData, value);
		}

		private static DataManager NewDataManager(StaticData staticData)
		{
			ConstructorInfo ctor = typeof(DataManager).GetConstructor(
				BindingFlags.Instance | BindingFlags.NonPublic, binder: null, [typeof(StaticData)], modifiers: null)!;
			return (DataManager)ctor.Invoke([staticData]);
		}
	}

	internal static string RepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir != null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "AionServer.slnx")))
				return dir.FullName;
			dir = dir.Parent;
		}

		throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
	}
}
