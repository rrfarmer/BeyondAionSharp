using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Timing;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.Network.Aion.ServerPackets;

using Require = Aion.Bots.Scenarios.NaturalJourneyRequirements;

namespace Aion.Bots.Scenarios;

public sealed partial class NaturalIshalgenJourney(INaturalJourneySession session, NaturalJourneyRuntime runtime, NaturalJourneyOptions options)
{
	private enum TemplatePhase { Full, Work, Claim }
	private sealed class NaturalJourneyCheckpointStopException : Exception;
	private sealed class NaturalEarlyAscensionRequiredException : Exception;
	private sealed class NaturalCapitalCheckpointStopException(string stage) : Exception
	{
		public string Stage { get; } = stage;
	}
	private sealed record CapitalPayment(int QuestId, long Experience, long Kinah);
	private sealed class NaturalCombatApproachBlockedException(string message) : IOException(message);
	private sealed class NaturalGuardedObjectiveRevivedException(string message) : IOException(message);
	private sealed class NaturalHaramelSourcesExhaustedException(string message) : IOException(message);
	private sealed class NaturalHaramelGroundApproachUnavailableException(string message) : IOException(message);
	private sealed class NaturalBookCollectionMapChangedException : IOException;

	/// <summary>NL-4: a death in an instance room revived the bot at its bind outside; the leg plans the way back in.</summary>
	private sealed class NaturalLeftInstanceRoomException() : IOException("A death took the bot out of the instance room.");

	/// <summary>AC-06: the Leg 3 escort's clear areas hold grave robbers with respawn_time 295 s (the Altgard spawn data);
	/// a clear holds that long after its first kill.</summary>
	private const long EscortClearRespawnMillis = 295_000;
	// The general quest-loot sweep opens lootable corpses this close after a kill (a Cleric fights at spell range, 25 m).
	private const float LootSweepReach = 60f;
	// AB-08: how far inside the Q24013 poison zone's edge the poison is used.
	private const float ZoneMargin = 3f;
	// AG-07: an Altgard leg walks its road toward an approach from farther than this, and leaves the last stretch to the navigator.
	private const float FarApproachMetres = 100f, FarApproachStop = 40f;

	/// <summary>
	/// MR-f (docs/natural-muster.md): the legs played before the bag cleanup was a rule. Their receipts count the items a
	/// leg ends with (the bridge's shop targets, the coin leg's loadout, Haramel's ledger), and none was replayed with the
	/// cleanup, so it stays off in them. It is on in the muster and in every leg that is not named here.
	/// </summary>
	private static readonly HashSet<string> LegsBeforeTheBagCleanup =
		["l1", "l2", "l3", "l4", "l5", "l6", "l7", "l8", "l9", "l10", "l11", "cg", "l12", NaturalAbyssEntry.Leg];
	/// <summary>CP-07: the obelisks of Ishalgen's two quest hubs, Aldelle Village and the outpost (bind_points.xml calls it
	/// Anturoon Crossing), and how near the outpost obelisk counts as standing at the outpost.</summary>
	private const int IshalgenVillageObelisk = 700063, IshalgenOutpostObelisk = 700064;
	private const float IshalgenOutpostRadius = 100f;
	/// <summary>The navigator's reason when every checked route to the destination is closed.</summary>
	private const string NoCheckedRoute = "No collision-checked route to the current destination.";
	/// <summary>CP-15: which character this run plays; the accepted Priest and Cleric line when the caller names none.</summary>
	private NaturalClassLine ClassLine => options.ClassLine ?? NaturalClassLine.Default;

	private static BotPosition GroundRoadGoal(BotNavigationGeometry geometry, int map, BotPosition destination) =>
		geometry.GroundAround(map, destination, [3f, 5f, 8f, 12f]).FirstOrDefault() is { } ground && ground != default
			? ground : geometry.SnapToGround(map, destination with { Z = destination.Z + 2 }) ?? destination;

	/// <summary>A disposable probe walks the same checked campaign-zone route with labelled remembered death spots.</summary>
	public async Task<bool> RunObservedZoneApproachAsync(BotPosition destination,
		IReadOnlyList<BotPosition> deathSpots, CancellationToken token,
		Func<CancellationToken, Task>? afterSegment = null)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Campaign map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [], geometry),
			geometry, runtime, stopOnDeath: false)
		{
			AvoidHostileAggro = true, AvoidSpots = deathSpots, Planner = BotTravelPlanner.For(map, geometry, runtime.Data),
		};
		BotPosition goal = GroundRoadGoal(geometry, map, destination);
		var progress = new NaturalApproachProgress();
		for (int attempt = 0; progress.CanRetry(attempt) && progress.StalledAttempts < 3; attempt++)
		{
			BotPosition before = session.CurrentPosition;
			bool avoidRememberedDeathSpots = true;
			IReadOnlyList<BotPosition> route = await NaturalCampaignZoneRoute.FindAsync(
				() => navigator.FindRouteAsync(session.CurrentPosition, goal, token), async () =>
				{
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(NaturalPatrolPolicy.WaitMillis), token);
					await navigator.SynchronizeAsync(token);
					return !session.Api.World.IsDead;
				}, token);
			if (route.Count == 0)
			{
				route = navigator.FindCampaignMemoryPreferenceRoute(session.CurrentPosition, goal, token);
				avoidRememberedDeathSpots = false;
			}
			foreach (BotPosition[] segment in route.Chunk(16))
			{
				if (navigator.IsSegmentStale(segment)) break;
				if (!navigator.IsSegmentSafe(segment, null, goal, avoidRememberedDeathSpots)) return false;
				await navigator.MoveAsync(segment, token);
				await navigator.SynchronizeAsync(token);
				if (afterSegment != null) await afterSegment(token);
				if (session.Api.World.IsDead) return false;
			}
			if (Distance(session.CurrentPosition, goal) <= 3) return true;
			progress.Observe(Distance(before, session.CurrentPosition), clearedGuard: false);
		}
		return false;
	}

	/// <summary>ND-05: a controlled probe uses the journey's ordinary rest, buffs and combat against
	/// a target produced by a real quest dialog. No loot action follows a kill that leaves this map.</summary>
	public async Task<NaturalCombatDiagnosticResult> RunObservedCombatAsync(
		Func<CancellationToken, Task<int>> startEncounter, CancellationToken token,
		Func<CancellationToken, Task>? afterKill = null, Action? afterBindRevive = null, bool avoidHostileAggro = false)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Combat map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var graph = BotNavigationGraphFactory.Build(runtime.Data, [], geometry);
		var navigator = new NaturalJourneyNavigator(session, graph, geometry, runtime, stopOnDeath: false)
		{
			AvoidHostileAggro = avoidHostileAggro,
		};
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine)
		{
			ApproachMapId = map, AfterKillAsync = afterKill, AfterBindRevive = afterBindRevive,
		};
		await combat.RestAsync(token);
		long started = runtime.NowMillis;
		int target = await startEncounter(token);
		bool killed = await combat.TryKillAsync(target, token, session.CurrentPosition);
		if (killed && session.Api.World.MapId != map)
		{
			await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
				packet => packet.Get<int>("objectId") == session.CharacterId);
			session.AcceptTeleportPosition();
			await session.SynchronizeAsync(token);
		}
		return new(killed, combat.ReviveCount, combat.CompletedRetreats, runtime.NowMillis - started);
	}

	/// <summary>NR-04: a controlled probe runs the equipment check of the observed class's gear rules and nothing else:
	/// the same requests the journey's own check sends after a turn-in, over the whole observed inventory.</summary>
	public Task<IReadOnlyList<NaturalGearUpgrade>> RunObservedEquipmentCheckAsync(CancellationToken token)
	{
		BotWorldModel world = session.Api.World;
		BotKnownObject? self = world.SelfObjectId is int selfId ? world.Objects.GetValueOrDefault(selfId) : null;
		NaturalGearRules gear = NaturalClassProfiles.For(self?.PlayerClass, ClassLine, runtime.Data).Gear;
		var race = self?.Race is byte raceId ? (Race)raceId : Race.ASMODIANS;
		return NaturalInventoryCheck.EquipAsync(session, world.Inventory.Values,
			itemId => NaturalInventoryCheck.Describe(runtime.Data.ItemDataDh.GetItemTemplate(itemId), gear.Class, race),
			(long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF, [], token, gear);
	}

	/// <summary>NR-110f: a controlled probe asks who joins a fight at a monster's own place, as the journey's fight counts
	/// them for a class that walks in and for a spirit that is sent (CP-56a). Nothing is sent.</summary>
	public IReadOnlyList<int> ObservedPackOf(int targetObjectId)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Pack map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [], geometry), geometry, runtime,
			stopOnDeath: false);
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine)
		{
			ApproachMapId = map,
		};
		return combat.PackOf(targetObjectId);
	}

	/// <summary>NR-70a: a controlled probe runs the journey's ordinary buff check before a pull and nothing else, for the
	/// class line of the options: the profile's buffs, its kept toggles, and the help scrolls it owns.</summary>
	public async Task RunObservedBuffCheckAsync(CancellationToken token)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Buff-check map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [], geometry), geometry, runtime,
			stopOnDeath: false);
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine)
		{
			ApproachMapId = map,
		};
		await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
	}

	/// <summary>CP-42: a controlled probe runs the journey's ordinary rest and nothing else, for the class line of the
	/// options. A rest that ends in death revives at the bind point, as in a journey.</summary>
	public async Task RunObservedRestAsync(CancellationToken token)
	{
		int map = session.Api.World.MapId ?? throw new InvalidDataException("Rest map unobserved.");
		BotNavigationGeometry geometry = runtime.CreateGeometry();
		var navigator = new NaturalJourneyNavigator(session, BotNavigationGraphFactory.Build(runtime.Data, [], geometry), geometry, runtime,
			stopOnDeath: false);
		var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry, stopOnDeath: false,
			conservativeRangedHold: false, NaturalMauPolicyParameters.Baseline, ClassLine)
		{
			ApproachMapId = map,
		};
		await combat.RestAsync(token);
	}

	public async Task RunAsync(CancellationToken token)
	{
		NaturalCapitalDecisionEngine.ValidateScope(options, runtime.Profile);
		NaturalLaterCapitalContract? laterCapital = options.LaterCapital ? NaturalLaterCapitalContract.LoadDefault() : null;
		laterCapital?.ValidateScope(options, runtime.Profile);
		bool continuousAltgard = options.AltgardLegId == "all";
		if (continuousAltgard && (!runtime.Profile.StartsWith("SIM-", StringComparison.Ordinal) || !options.AscensionBridge ||
			options.StopAfterQuest != null || options.StopAt != null || options.RelogAt != null || options.AltgardOnlyQuests != null ||
			options.Course != null || options.ClericEncounter || options.CoinGearReceiptPath != null || options.HaramelProgressPath != null))
			throw new InvalidOperationException("The complete SIM journey requires creation, Ascension and every approved leg without diagnostic shortcuts or saved receipts.");
		session.IdentityAltgardLegId = options.GroupRoster != null && options.GroupStage == "field"
			? NaturalJourneyIdentityRules.FieldGroupLeg : options.AltgardLegId;
		if (options.MauPolicy != null && options.Course == null)
			throw new InvalidOperationException("A tuned Mau policy is restricted to the focused SIM course.");
		NaturalMauPolicyParameters mauPolicy = options.MauPolicy ?? NaturalMauPolicyParameters.Baseline;
		mauPolicy.Validate();
		bool stopAfterQ2004 = options.StopAfterQuest == 2004, stopAfterQ2005 = options.StopAfterQuest == 2005,
			stopAfterQ2006 = options.StopAfterQuest == 2006, stopAfterQ2007 = options.StopAfterQuest == 2007;
		bool fightRouteOpen = false;
		int emptySpawnWaits = 0;
		BotPosition? fightRouteOpenAt = null;
		var quietNeighbours = new HashSet<int>();
		var combatTrace = runtime.Trace;
		string combatTracePath = session.CombatTracePath ?? throw new InvalidOperationException("Natural journey requires a trace path.");
		var dashboard = runtime.Dashboard;
		NaturalIshalgenContract contract = NaturalIshalgenContract.LoadDefault();
		NaturalCapitalContract capitalContract = NaturalCapitalContract.ForLine(ClassLine);
		// CP-20: what the server decides for the line's starter (the Q2132 var and trainer).
		NaturalStarterClass starterFacts = NaturalClassLineContract.LoadDefault().Starter(ClassLine.Starter);
		// NR-13: every class's profile is generated from the shipped data.
		NaturalClassProfiles.Supply(runtime.Data);
		// CP-26: the line's Ascension bridge, loaded once when the run first needs it. The accepted line's is the reviewed
		// contract; a line that takes no second class has none and is refused there by name.
		NaturalAscensionContract? lineBridge = null;
		NaturalAscensionContract LineBridge()
		{
			if (lineBridge != null) return lineBridge;
			NaturalAscensionContract bridge = NaturalAscensionContract.ForLine(ClassLine);
			// NR-33, NR-35: the kept accessories are the reviewed pair's own, by item id. Another pair names none: its endpoint
			// asks the gear rule instead, which wears the best accessories it owns at the shop stop.
			if (!bridge.ReviewedPair) bridge = bridge with { KeptAccessories = [] };
			return lineBridge = bridge;
		}
		NaturalJourneyCheckpoint? checkpoint = null;
		NaturalCoinGearProgress? coinGearProgress = null;
		NaturalHaramelProgress? haramelProgress = null;
		long HaramelNow() => checked(runtime.Epoch.ToUnixTimeMilliseconds() + runtime.NowMillis);
		NaturalJourneyRunContext RunContext() => new(combatTrace.Run, runtime.Profile, runtime.Seed,
			typeof(NaturalIshalgenJourney).Assembly.GetCustomAttributes(false)
				.OfType<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion,
			typeof(NaturalIshalgenJourney).Module.ModuleVersionId.ToString(), runtime.NowMillis);
		var runContext = new Dictionary<string, object?> { ["context"] = RunContext() };
		// CP-15: the record names the class line only when it is not the accepted one.
		if (ClassLine != NaturalClassLine.Default) runContext["classLine"] = ClassLine.Id;
		combatTrace.WriteAction("ni08-run", "natural-run-context", runContext);
		if (options.Course != null)
			combatTrace.WriteAction("ni08-run", "phase2-policy", new Dictionary<string, object?>
			{
				["policyId"] = mauPolicy.Id, ["parameters"] = mauPolicy,
				["baseline"] = options.MauPolicy == null,
			});
		void WriteFailure(string kind, Exception failure)
		{
			// Diagnosis must never replace the original failure, including an incomplete or wrong-character login.
			try
			{
				if (session.Api.World.LoginStateObserved && session.Api.World.SelfObjectId == session.CharacterId)
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				string path = new NaturalJourneyFailure(kind, session.CurrentStep, session.CurrentAction,
					session.ConnectionGeneration, failure.GetType().FullName!, failure.Message, failure.StackTrace,
					checkpoint, combatTracePath, session.PacketHistory.TakeLast(64).Select(p => p.PacketType.Name).ToArray(), RunContext())
					{ ServerProblems = System.Text.Json.JsonSerializer.SerializeToElement(runtime.SnapshotProblems()) }
					.Write(Path.GetDirectoryName(combatTracePath)!);
				Console.WriteLine($"NI-08 failure package: {path}");
			}
			catch (Exception diagnosisFailure)
			{
				Console.Error.WriteLine($"NI-08 could not save diagnosis for {failure}: {diagnosisFailure}");
			}
		}
		try
		{
			bool resuming = await runtime.EnterAsync(token);
			if (options.Companion != null)
			{
				await RunCompanionAsync(token);
				return;
			}
			if (options.GroupRoster != null)
			{
				await RunGroupQuestAsync(token);
				return;
			}
			if (options.Role == "healer")
			{
				using var healer = new NaturalGroupHealDriver(session, runtime, NaturalClassProfiles.For(
					session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass, ClassLine, runtime.Data));
				await healer.RunAsync(token);
				return;
			}
			if (options.SupportWatch)
			{
				while (true) { await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token); await session.SynchronizeAsync(token); }
			}
			if (options.CapitalStage == "first")
				Require.True(resuming && session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2008, 2009 }),
					"The contained capital pass must resume the retained post-ceremony character.");
			Require.True(!continuousAltgard || !resuming, "The complete SIM journey must create a fresh character.");
			if (options.Course != null || options.ClericEncounter)
			{
				if (resuming || runtime.PrepareCourseAsync == null)
					throw new InvalidOperationException("A Mau course or Cleric encounter requires a fresh SIM character and explicit preparation.");
				await runtime.PrepareCourseAsync(token);
			}
			// The LIVE movie-end reply can arrive after the first time-check barrier: the
			// reflex is sent while draining login packets. Observe completion explicitly.
			if (!resuming && !session.Api.World.CompletedQuestIds.Contains(2000))
				await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
					packet => packet.Get<int>("questId") == 2000 && packet.Get<byte>("status") == 5);
			checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
				session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
			await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "login-observation.json"),
				System.Text.Json.JsonSerializer.Serialize(checkpoint), token);
			Require.Contains(2000, session.Api.World.CompletedQuestIds);

			IReadOnlyDictionary<int, QuestRunPlan> templatePlans = QuestRunPlan.LoadDirectory(
				Path.Combine(runtime.RepoRoot, "parity-artifacts", "e2e",
					"natural-ishalgen-plans")).ToDictionary(plan => plan.Id);
			Require.Equal(26, templatePlans.Count);
			Require.All(templatePlans.Keys, id => Require.Contains(id, contract.Quests.Select(quest => quest.Id)));
			NaturalDecision decision = NaturalIshalgenDecisionEngine.Decide(contract,
				ObserveNaturalJourney(session), 1, earlyAscension: options.AscensionBridge, line: ClassLine);
			if (!resuming && options.Course == null && !options.ClericEncounter)
			{
				Require.Equal("find-quest-starter", decision.SelectedAction);
				Require.Equal(2101, decision.SelectedQuestId);
			}

			BotNavigationGeometry geometry = runtime.CreateGeometry();
			// A stumble into a rock face (Java stops the knockback at the first collision, at the old height) lands
			// off walkable ground: stand on the ground at its foot, as the client does, instead of where no step is legal.
			// NR-54e: a landing on the mesh can still be on a face steeper than 45 degrees. Java StaggerEffect.calculate takes
			// the ground there without the slope rule, and no checked edge starts from such a face: stand on the mesh's
			// ground beside it.
			session.ResolveForcedLanding = landed =>
			{
				if (geometry.NavMesh?.NavMeshes.Get(contract.MapId) is not { } mesh) return landed;
				BotPosition? beside = mesh.Snap(landed, BotNavQuery.Default with { SnapHorizontal = 0.5f, SnapVertical = 2 });
				if (beside != null && geometry.StaticGroundAt(contract.MapId, landed) != null) return landed;
				if ((beside ?? mesh.Snap(landed, BotNavQuery.Default with { SnapHorizontal = 3, SnapVertical = 4 })) is not BotPosition ground)
					return landed;
				session.TraceDiagnostic("forced-landing-on-ground", new Dictionary<string, object?>
				{
					["landed"] = landed,
					["standing"] = ground,
				});
				return ground with { Heading = landed.Heading };
			};
			// AF-08: Altgard Leg 1 walks to its own NPCs and hunts on the Ice Lake; they join the waypoint graph.
			// AM-06/07: the same runner plays any Altgard leg ("l1" the fortress, "l2" Moslan Crossroad).
			string? altgardLegId = continuousAltgard ? null : options.AltgardLegId ?? (options.AltgardLeg1 ? "l1" : null);
			// NR-32: a leg's reward picks are those of the class its contract was written for (start.class). A character of
			// another class takes, at the same quests, what its gear rules choose from the list the server offers it, decided
			// when the leg is taken up, from what it then owns.
			// NR-33: what the leg's coin-gear, Haramel and Abyss-entry scopes protect by item id is that class's too. Another
			// class protects its own picks and what it wears when the leg is taken up, beside what every class carries.
			// NR-40: the character is of the class the leg's contract was written for; an unobserved class is taken for it.
			bool OfContractClass(NaturalAltgardContract leg) =>
				session.Api.World.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass is not { } observedId ||
				PlayerClassExtensions.GetPlayerClassById(observedId, true)?.ToString() == leg.Start.Class;
			NaturalAltgardContract WithObservedClassRewards(NaturalAltgardContract leg)
			{
				BotWorldModel world = session.Api.World;
				byte? classId = world.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass;
				if (classId is not { } observedId || PlayerClassExtensions.GetPlayerClassById(observedId, true) is not { } observedClass ||
					observedClass.ToString() == leg.Start.Class) return leg;
				// NR-R18a: where the server's handler branches on the class, the leg has that class's own entries of the quest.
				leg = leg.ForClassPath(observedClass.ToString());
				NaturalGearRules rules = NaturalClassProfiles.For(classId, ClassLine, runtime.Data).Gear;
				int[] contractItems = [.. leg.CoinGear?.ProtectedItemIds ?? [], .. leg.Haramel?.ProtectedItemIds ?? [], .. leg.AbyssEntry?.ProtectedItemIds ?? []];
				NaturalIshalgenInventoryPolicy rewards = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
					world.Inventory.Values.Select(item => item.ItemId).Concat(contractItems), ClassLine);
				NaturalAltgardContract picked = leg.WithRewardChoices(choice =>
					rewards.RewardChoiceFor(choice, world.Level, world.Inventory.Values, rules));
				if (leg.RewardChoiceList.Length > 0)
					session.TraceDiagnostic("leg-reward-picks", new Dictionary<string, object?>
					{ ["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["contractClass"] = leg.Start.Class, ["picks"] = picked.RewardChoiceList });
				Dictionary<int, int> picks = leg.RewardChoiceList.ToDictionary(pin => pin.ItemId,
					pin => picked.RewardChoiceList.Single(choice => choice.QuestId == pin.QuestId).ItemId);
				int[] worn = NaturalAltgardContinuation.EquippedItemIds(world);
				NaturalAltgardContract own = picked.WithProtectedItems(ids =>
					NaturalAltgardContract.ProtectedFor(ids, picks, id => rewards.Item(id).IsEquipment, worn));
				// NR-56a: a step at which a pinned reward is handed over expects the class's own pick.
				own = own with
				{
					Steps = [.. own.Steps.Select(step => step.ReceivesItemId is int handed && picks.TryGetValue(handed, out int mine)
						? step with { ReceivesItemId = mine } : step)],
				};
				// NR-38a: the coin-gear scope's vendor and purchases are the class's own, by its manifest and what it wears.
				if (own.CoinGear is { } coins)
				{
					IReadOnlyList<int> vendors = NaturalCoinManifests.VendorsBeside(runtime.Data, leg.Hub.MapId, coins.VendorNpcId, coins.CoinItemId);
					NaturalIshalgenInventoryPolicy sold = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
						world.Inventory.Values.Select(item => item.ItemId).Concat(vendors.SelectMany(npc => NaturalCoinManifests.Sold(runtime.Data, npc))), ClassLine);
					int tier = runtime.Data.ItemDataDh.GetItemTemplate(coins.Purchases[0].ItemId).GetLevel();
					NaturalCoinManifest manifest = NaturalCoinManifests.For(runtime.Data, vendors, coins.CoinItemId, tier, rules, sold.Item);
					int tab = manifest.Armor.Count == 0 ? coins.GoodsListId
						: runtime.Data.TradeListDataDh.GetTradeListTemplate(manifest.VendorNpcId).GetTradeTabIds()
							.First(id => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList().Contains(manifest.Armor[0].ItemId) == true);
					// NR-54a: the shield's own tab.
					int shieldTab = manifest.Shield is not { } coinShield ? 0
						: runtime.Data.TradeListDataDh.GetTradeListTemplate(manifest.VendorNpcId).GetTradeTabIds()
							.First(id => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList().Contains(coinShield.ItemId) == true);
					NaturalCoinGear classGear = coins.ForClass(manifest,
						[.. world.Inventory.Values.Select(item => new NaturalJourneyItem(item.ObjectId, item.ItemId, item.Count, item.EquipmentSlot))],
						id => rules.Score(sold.Item(id)), tab, shieldTab);
					// NR-39: the stigma skill the leg must not find learned is the class's own stone's.
					classGear = classGear with
					{
						ForbiddenStigmaSkillId = NaturalAltgardContract.LoadLeg("l11").Destiny!.ForClass(observedClass, runtime.Data).StigmaSkillId,
					};
					own = own with { CoinGear = classGear };
					session.TraceDiagnostic("leg-coin-manifest", new Dictionary<string, object?>
					{
						["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["vendor"] = classGear.VendorNpcId, ["tab"] = classGear.GoodsListId,
						["manifest"] = manifest.Pieces, ["purchases"] = classGear.Purchases, ["weapon"] = classGear.StaffItemId,
						["coins"] = new[] { classGear.IncomingCoins, classGear.RewardCoins, classGear.EndpointCoins },
					});
				}
				// NR-40: the Haramel scope's chest, kept weapon, coin counts, armor and stigma facts are the class's own.
				if (own.Haramel is { } haramelScope)
				{
					BotInventoryItem held = world.Inventory.Values.SingleOrDefault(item => item.EquipmentSlot is 1 or 3)
						?? throw new InvalidDataException("The Haramel leg needs a weapon held in the main hand.");
					// The armor it prefers: the kind whose torso its gear rules score highest.
					string armorPrefix = new[] { "PL", "CH", "LT", "RB", "CL" }.Where(prefix => rules.GearGroups.Contains(prefix + "_TORSO"))
						.OrderByDescending(prefix => rules.Score(new NaturalItem(0, prefix + "_TORSO", [], [], "PC_ALL", 0, 1, 0, 0, 0, 0, ItemLevel: 1))).First();
					NaturalHaramel classHaramel = haramelScope.ForClass(observedClass,
						new NaturalJourneyItem(held.ObjectId, held.ItemId, held.Count, held.EquipmentSlot),
						world.Inventory.Values.Where(item => item.ItemId == haramelScope.IronItemId).Sum(item => item.Count),
						world.Inventory.Values.Where(item => item.ItemId == haramelScope.BronzeItemId).Sum(item => item.Count), armorPrefix,
						NaturalAltgardContract.LoadLeg("l11").Destiny!.ForClass(observedClass, runtime.Data));
					own = own with { Haramel = classHaramel };
					session.TraceDiagnostic("leg-haramel-scope", new Dictionary<string, object?>
					{
						["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["chest"] = classHaramel.ChestNpcId, ["weapon"] = classHaramel.StaffItemId,
						["weaponSlot"] = classHaramel.WeaponSlot, ["iron"] = classHaramel.IronCount, ["bronze"] = classHaramel.BronzeCount,
						["armor"] = classHaramel.UpgradeArmorPrefix, ["stigmaSkill"] = classHaramel.StigmaSkillId,
					});
				}
				// NR-39: the destiny campaign's stone, the skill it grants and the reward never given are the class's own.
				if (own.Destiny is { } destiny)
				{
					NaturalAltgardDestiny classDestiny = destiny.ForClass(observedClass, runtime.Data);
					// NR-54g: the step at which Heimdall hands the stone over expects the class's stone as well.
					own = own with
					{
						Destiny = classDestiny,
						Steps = [.. own.Steps.Select(step => step.ReceivesItemId == destiny.StoneItemId
							? step with { ReceivesItemId = classDestiny.StoneItemId } : step)],
					};
					session.TraceDiagnostic("leg-destiny-stone", new Dictionary<string, object?>
					{
						["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["stone"] = classDestiny.StoneItemId,
						["skill"] = classDestiny.StigmaSkillId, ["legacyReward"] = classDestiny.LegacyRewardId,
					});
				}
				// NR-38b: the Abyss entry's two coin tiers are the class's own: its vendor, its pieces and its weapon.
				if (own.AbyssEntry is { } abyss)
				{
					NaturalAbyssCoinArmor classArmor = abyss.CoinArmor.ForClass(runtime.Data, NaturalAbyssEntry.Morheim, rules,
						ids => NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, ids, ClassLine).Item, observedClass.ToString());
					// NR-56b: the leg's level is a receipt of the accepted run, as its start facts are (NR-41). For another class
					// it is the level the leg starts from, which is the plan's finish; a tier above it is asked only of a
					// character that has reached it.
					int floor = Math.Min(abyss.Level.Minimum, own.Start.Level);
					own = own with
					{
						AbyssEntry = abyss with { CoinArmor = classArmor, Level = abyss.Level with { Minimum = floor } },
						Endpoint = own.Endpoint with { MinimumLevel = Math.Min(own.Endpoint.MinimumLevel, floor) },
					};
					session.TraceDiagnostic("leg-coin-manifest", new Dictionary<string, object?>
					{
						["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["vendor"] = classArmor.VendorNpcId,
						["tabs"] = new[] { classArmor.GoodsListId, classArmor.StaffGoodsListId, classArmor.ShieldGoodsListId }, ["tiers"] = classArmor.Tiers,
					});
				}
				// NR-41: the leg's incoming journal names the dispatch quest of the contract's class; another class did its own.
				if (!observedClass.IsStartingClass())
				{
					NaturalClassLineContract classLines = NaturalClassLineContract.LoadDefault();
					int contractDispatch = classLines.Second(Enum.Parse<PlayerClass>(leg.Start.Class)).Dispatch.QuestId;
					int ownDispatch = classLines.Second(observedClass).Dispatch.QuestId;
					if (ownDispatch != contractDispatch && own.Start.CompletedQuestIds.Contains(contractDispatch))
						own = own with
						{
							Start = own.Start with { CompletedQuestIds = [.. own.Start.CompletedQuestIds.Select(id => id == contractDispatch ? ownDispatch : id)] },
						};
				}
				if (!ReferenceEquals(own, picked))
					session.TraceDiagnostic("leg-protected-items", new Dictionary<string, object?>
					{
						["leg"] = leg.Leg, ["class"] = observedClass.ToString(), ["contractClass"] = leg.Start.Class, ["worn"] = worn,
						["coinGear"] = own.CoinGear?.ProtectedItemIds, ["haramel"] = own.Haramel?.ProtectedItemIds, ["abyssEntry"] = own.AbyssEntry?.ProtectedItemIds,
					});
				return own;
			}
			// NR-R15c: a leg is taken up without the quests the class line leaves out (NR-Q4).
			NaturalAltgardContract? altgardLeg = altgardLegId is { } legId ? NaturalAltgardContract.LoadLeg(legId).Without(ClassLine.LeftOutQuestIds) : null;
			// NR-41: a leg's incoming journal is a receipt of the accepted run. A character of another class is given the leg
			// as its own first, and then the journal is bound to what it has completed, so every count the leg checks is a
			// minimum met. The contract's class keeps the order it had: bound only on the revised route.
			if (altgardLeg != null && (laterCapital != null || !OfContractClass(altgardLeg)))
			{
				bool contractClass = OfContractClass(altgardLeg);
				altgardLeg = NaturalAltgardContinuation.BindIncoming(WithObservedClassRewards(altgardLeg), session.Api.World.CompletedQuestIds,
					session.Api.World.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
					NaturalAltgardContinuation.EquippedItemIds(session.Api.World), contractClass);
			}
			coinGearProgress = altgardLeg?.CoinGear == null ? null : NaturalCoinGearProgress.Empty;
			if (altgardLeg?.Haramel is { } haramel)
				haramelProgress = options.HaramelProgressPath is { Length: > 0 } savedHaramel
					? NaturalHaramelProgress.Read(savedHaramel, session.CharacterId, HaramelNow(), session.Api.World, altgardLeg)
					: NaturalHaramelProgress.Begin(session.CharacterId, HaramelNow(), session.Api.World, haramel);
			else if (options.HaramelProgressPath is { Length: > 0 })
				throw new InvalidDataException("Haramel receipts require the l12 leg.");
			if (options.CoinGearReceiptPath is { Length: > 0 } receiptPath)
			{
				NaturalCoinGear gear = altgardLeg?.CoinGear ?? throw new InvalidDataException("Coin receipts require the CG leg.");
				coinGearProgress = NaturalCoinGearProgress.ReadVerifiedEndpoint(receiptPath, session.CharacterId, gear,
					NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition));
			}
			NaturalJourneyItem[] coinIncomingLoadout = [];
			int[] destinyIncomingSkills = altgardLeg?.Destiny is { } incomingDestiny
				? session.Api.World.Skills.Keys.Where(id => id != incomingDestiny.StigmaSkillId).ToArray() : [];
			IReadOnlyDictionary<int, QuestRunPlan> altgardPlans = altgardLegId is { } planLeg
				? options.Muster ? NaturalAltgardContract.LoadMusterPlans() : NaturalAltgardContract.LoadPlans(planLeg)
				: new Dictionary<int, QuestRunPlan>();
			var collectionLimits = altgardPlans.Values.SelectMany(plan => plan.Steps
				.Where(step => step.Kind == "collect" && step.ItemId > 0 && step.Count > 0)
				.Select(step => (plan.Id, step.ItemId, step.Count)))
				.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.ToArray());
			bool QuestDropStillNeeded(int itemId) => !collectionLimits.TryGetValue(itemId, out var needs) ||
				needs.Any(need => !session.Api.World.CompletedQuestIds.Contains(need.Id) &&
					ItemCount(session.Api.World, itemId) < need.Count);
			// The maintainer's 2026-10-01 note: hubs have flight transporters; their routes come from the client (generated).
			IReadOnlyList<NaturalAirlineRoute> airlines = NaturalAirlineRoutes.Load(runtime.RepoRoot);
			// AG-07: an Altgard leg's road walk toward a far approach (set by the leg's runner; see ApproachShippedSpawnAsync).
			Func<BotPosition, int, Func<bool>?, Task>? farApproach = null;
			// M2: how a leg with flight rules gets out of a place it has no road from (Morheim's ice lake): it flies to the
			// hub. False when it cannot take off there or no flight leads to the hub.
			Func<Task<bool>>? flyOutOfStrand = null;
			// M5: how a Morheim leg reaches ground it has no road to inside a FLY zone (the Fire Temple's crater): it walks into
			// the zone, flies and lands beside the goal. False when no flight across a zone leads there.
			Func<BotPosition, Task<bool>>? flyAcross = null;
			// NL-4: how a leg with instance rooms (Q24023's Sky Temple Interior) goes down to a floor no walk joins: a glide.
			Func<BotPosition, Task<bool>>? glideDown = null;
			Func<BotPosition, bool>? preferRecordedGlide = null;
			Func<Task<bool>>? prepareRecordedFireFight = null;
			Func<Task<int?>>? approachRecordedFireExit = null;
			Func<Task<int?>>? approachRecordedBulagan = null;
			Func<BotPosition, BotPosition, float, IReadOnlyList<BotPosition>>? recordedFireBossRecovery = null;
			// M4: the quests this bot left for the group step on a Morheim leg (no progress on one alone for an hour).
			var deferredToGroup = new HashSet<int>();
			Func<int, float, Func<int?>?, Task<int>>? haramelApproach = null;
			Func<IReadOnlyList<int>, int>? haramelKind = null;
			bool farApproachUnderway = false;
			var provenWarlockSources = new HashSet<int>();
			int provenWarlockPacketCursor = 0;
			bool bookCollectionUnderway = false;
			int[] altgardNpcs = continuousAltgard ? NaturalAltgardContinuation.Order.SelectMany(id =>
				NaturalAltgardContract.LoadLeg(id).GraphNpcIds(NaturalAltgardContract.LoadPlans(id)))
				.Concat(airlines.Select(route => route.NpcId)).Distinct().ToArray()
				: altgardLeg == null ? [] : altgardLeg.GraphNpcIds(altgardPlans)
					.Concat(airlines.Where(route => route.MapId == altgardLeg.Hub.MapId).Select(route => route.NpcId)).Distinct().ToArray();
			if (laterCapital != null) altgardNpcs = altgardNpcs
				.Concat(NaturalLaterCapitalSteps.HeritagePickup.Concat(NaturalLaterCapitalSteps.BookPreparation).Select(step => step.NpcId))
				.Concat(NaturalLaterCapitalSteps.HeritageCity.Concat(NaturalLaterCapitalSteps.RobePreparation)
					.Concat(NaturalLaterCapitalSteps.Juice).Append(NaturalLaterCapitalSteps.MaternalReturn)
					.Append(NaturalLaterCapitalSteps.RobeBerth).Concat(NaturalLaterCapitalSteps.Leg7City)
					.Append(NaturalLaterCapitalSteps.RobeHeart).Concat(NaturalLaterCapitalSteps.Leg9City)
					.Append(NaturalLaterCapitalSteps.LibraryPermission).Concat(NaturalLaterCapitalSteps.FinalCity).Select(step => step.NpcId))
				.Append(700211)
				.Concat(new[] { 210404, 203679, 203581, 204191 }).Concat(airlines.Select(route => route.NpcId)).Distinct().ToArray();
			if (options.SurveyNpcId is int surveyNpcId) altgardNpcs = [.. altgardNpcs.Append(surveyNpcId).Distinct()];
			// AK-08: items an open Altgard quest still needs (its collect items, the ring carriers' rings): never worn as gear and
			// never sold. The leg 5 smoke run wore Q2292's level 16 rings as upgrades, which its hand-in would not have found.
			IReadOnlySet<int> QuestNeededItems() => (altgardLeg == null ? Enumerable.Empty<int>() : altgardPlans.Values
				.Where(plan => !session.Api.World.CompletedQuestIds.Contains(plan.Id))
				.SelectMany(plan => plan.Steps.Where(step => step.Kind == "collect").Select(step => step.ItemId))
				.Concat(altgardLeg.TimedSpawnList.Where(carrier => !session.Api.World.CompletedQuestIds.Contains(carrier.QuestId))
					.Select(carrier => carrier.ItemId))
				.Concat(altgardLeg.Destiny is { } destiny ? new[] { destiny.StoneItemId, destiny.RewardBundleId, destiny.LegacyRewardId } : [])
				.Concat(altgardLeg.CoinGear?.ProtectedItemIds ?? [])
				.Concat(altgardLeg.Haramel?.ProtectedItemIds ?? [])
				.Concat(altgardLeg.Haramel?.CleanupItemIds ?? [])
				.Concat(altgardLeg.Haramel?.TowerChestKeys?.Select(key => key.ItemId) ?? [])
				.Where(item => item > 0)).Concat(capitalContract.ProtectedItemIds)
				.Concat(laterCapital?.ProtectedItemIds ?? new HashSet<int>()).ToHashSet();
			BotNavigationGraph graph = BotNavigationGraphFactory.Build(runtime.Data, altgardNpcs.Concat(new[] {
				203500, 203504, 203501, 203502, 203516, 203518,
				203519, 203534, 790002, 210377, 210378, 700045, 203538,
				203539, 210592, 700047, 203550, 210402, 210403, starterFacts.NewSkill.TrainerNpcId, 203535, 203551, 203547,
				203540, 210395, 210396, 210750, 700095,
				203552, 203554, 700085, 700086, 700087, 203517,
				203533, 210734, 203514, 203543, 203532, 203531, 700128,
					210363, 210367, 210369, 700124, 700093,
					700063, 700064, 203513, 203545, 203679}),
				geometry);
			var navigator = new NaturalJourneyNavigator(session, graph, geometry, runtime, options.StopOnDeath)
			{
				// Level-aware roads-and-branches planning for long legs; null (grid/navmesh chain only)
				// when the map has no baked navmesh or travel graph.
				Planner = BotTravelPlanner.For(NaturalIshalgenRoads.MapId, geometry, runtime.Data),
			};
			// NA-06: navigation for whichever map the client entered. Ishalgen keeps the journey's own navigator;
			// any other map (an instance, Pandaemonium, Altgard) gets geometry, graph and planner of its own.
			var mapNavigators = new NaturalJourneyMapContexts<NaturalJourneyNavigator>(key =>
			{
				if (key.MapId == contract.MapId) return navigator;
				BotNavigationGeometry mapGeometry = runtime.CreateGeometry();
				BotNavigationGraph mapGraph = BotNavigationGraphFactory.Build(runtime.Data,
					key.MapId == 320010000 ? [205020] : altgardLeg != null || continuousAltgard || laterCapital != null ? altgardNpcs : [], mapGeometry);
				return new NaturalJourneyNavigator(session, mapGraph, mapGeometry, runtime, options.StopOnDeath)
				{
					Planner = BotTravelPlanner.For(key.MapId, mapGeometry, runtime.Data),
				};
			});
			BotPosition? easternRoadIngressStart = null;
			BotPosition[] easternRoadIngress = [];
			NaturalJourneyCombat? navigationDefense = null;
			navigator.DefendOnAttackAsync = async (attackers, refuge, packetStart, defendToken) =>
			{
				if (navigationDefense?.InCombat != false) return;
				foreach (int attacker in attackers)
				{
					if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? npc) ||
						npc.Kind != BotKnownObjectKind.Npc ||
						Distance(session.CurrentPosition, npc.Position) > 30) continue;
					session.TraceDiagnostic("defend-navigation", new Dictionary<string, object?>
					{
						["attacker"] = attacker,
						["npcId"] = npc.TemplateId,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
						["retreatAnchor"] = refuge,
					});
					try
					{
						if (await navigationDefense.TryKillAsync(attacker, defendToken, refuge, packetStart))
							navigator.UnavailableObjects.Add(attacker);
						else return; // Re-observe after a retreat or lost target before another pull.
					}
					catch (NaturalCombatApproachBlockedException blocked) when (altgardLeg?.Haramel?.MapId == session.Api.World.MapId)
					{
						session.TraceDiagnostic("haramel-navigation-defense-replan", new Dictionary<string, object?>
						{ ["attacker"] = attacker, ["position"] = session.CurrentPosition, ["reason"] = blocked.Message });
						return;
					}
					catch (NaturalCombatApproachBlockedException blocked)
					{
						// NR-54d: the walk to this attacker is refused: it shoots from inside its neighbours' circles. That
						// is no end of the journey. The next attacker is fought, and the walk or the rest that was
						// interrupted goes on and observes again.
						session.TraceDiagnostic("navigation-defense-approach-blocked", new Dictionary<string, object?>
						{
							["attacker"] = attacker, ["npcId"] = npc.TemplateId, ["hp"] = session.Api.World.CurrentHp,
							["position"] = session.CurrentPosition, ["reason"] = blocked.Message,
						});
					}
				}
			};
			var combat = new NaturalJourneyCombat(session, navigator, runtime, geometry,
				options.StopOnDeath, options.OptimizeHubs, mauPolicy, ClassLine, haramelProgress?.Revives ?? 0);
			navigationDefense = combat;
			// The general quest-loot rule: after any kill, open every corpse near the Cleric that the server marked lootable
			// for it, and take its quest items, as a player does. A quest item drops only while its quest needs it, so every
			// one is wanted. Before this only the monster a step aimed at was looted: in Leg 4, Q2230 got 6 tusks from 21
			// mosbears (85% drop) and spent three timers, because the mosbears that died as adds or on the way were left.
			var lootSwept = new HashSet<int>();
			bool sweeping = false;
			bool maintainingInventory = false;
			void WithQuestLoot(NaturalJourneyCombat fighter)
			{
				fighter.AfterKillAsync = SweepQuestItemsWhenSafeAsync;
				var stock = new NaturalHelpStockCheck(session, fighter.ClassProfile.HelpItems,
					() => runtime.SupplyHelpItemAsync != null,
					() =>
					{
						var engaged = Engaged();
						return !maintainingInventory && !fighter.InCombat && engaged.Attackers.Length == 0 && engaged.Pursuers.Length == 0;
					},
					(trigger, safe) => TopUpHelpItemsAsync(trigger, safe: safe));
				fighter.CheckHelpStockAsync = stock.CheckAsync;
			}
			async Task SweepQuestItemsWhenSafeAsync(CancellationToken sweepToken)
			{
				if (sweeping || Engaged().Attackers.Length > 0) return;
				sweeping = true;
				try { await LootQuestItemsAroundAsync(sweepToken); }
				finally { sweeping = false; }
			}
			var workedTemplates = new HashSet<int>();
			long lastReturnMillis = long.MinValue / 2;
			long failedRestockKinah = -1;
			var refusedGear = new HashSet<int>();
			// MR-f: the bag cleanup, what the server would not let go, and what went.
			bool cleanBags = options.Muster || altgardLegId is { } playedLeg && !LegsBeforeTheBagCleanup.Contains(playedLeg);
			var keptByServer = new HashSet<int>();
			var bagDiscards = new List<NaturalBagDiscard>();
			// What the leg being played names, for the cleanup. Unset, it is what the leg protects and an open plan quest
			// needs (QuestNeededItems); the muster, which follows legs that are over, names its own.
			HashSet<int>? bagNeeds = null;
			combat.UsesBoosts = cleanBags;
			PlayerClass? gearClass = null;
			bool bridgeShopVisited = false; // NA-16: the Altgard shop stop is done
			var helpSupplied = new List<NaturalHelpSupplied>(); // NA-21: every approved help item supplied
			int helpCheckedAtLevel = -1;
			WithQuestLoot(combat);
			bool earlyAscensionUnderway = false;
			NaturalJourneyCheckpoint? capitalBefore = null;
			var capitalPayments = new Dictionary<int, CapitalPayment>();
			int capitalSequences = 0;
			long capitalFares = 0;
			var capitalTravel = new NaturalCapitalTravel(session, runtime.RepoRoot, () => runtime.NowMillis);
			bool CapitalPending() => capitalContract.CompletedQuestIds.Any(id => !session.Api.World.CompletedQuestIds.Contains(id));
			bool IshalgenPending() => contract.Quests.Any(quest => !session.Api.World.CompletedQuestIds.Contains(quest.Id));
			bool EarlyAscensionNeeded() => options.AscensionBridge && altgardLegId == null && IshalgenPending() &&
				(session.Api.World.Level >= contract.AscensionLevel || AscensionBridgeStarted()) &&
				(!session.Api.World.CompletedQuestIds.Contains(2009) || CapitalPending() || session.Api.World.MapId is 120010000 or 120020000);

			// Wear the best gear in the bag (the recorded human put on four unused quest rewards at Nalto).
			// The client knows each item's slots, level and class/race limits from its tooltip; the server
			// still checks every equip, and an item it refuses is never asked for again.
			async Task<IReadOnlyList<NaturalGearUpgrade>> EquipUpgradesAsync(CancellationToken gearToken)
			{
				// CG authorizes exactly three explicit armour equips and freezes the incoming loadout.
				if (altgardLeg?.CoinGear != null) return [];
				BotWorldModel world = session.Api.World;
				if (world.IsDead) return [];
				// NA-09: the observed class and race decide what can be worn; after Ascension the Cleric's new
				// masteries (chain, shield, staff) make items the Priest was refused wearable, so refusals reset.
				// CP-23: the gear rules of the observed class say which class the tooltip is read for.
				BotKnownObject? self = world.SelfObjectId is int selfId ? world.Objects.GetValueOrDefault(selfId) : null;
				NaturalGearRules gear = combat.ClassProfile.Gear;
				var playerClass = gear.Class;
				var race = self?.Race is byte raceId ? (Race)raceId : Race.ASMODIANS;
				if (playerClass != gearClass)
				{
					gearClass = playerClass;
					refusedGear.Clear();
				}
				// AX-04: the description carries what the staff rule needs, a staff from a mace or a shield and each weapon's magic boost.
				NaturalGearInfo? Describe(int itemId) =>
					NaturalInventoryCheck.Describe(runtime.Data.ItemDataDh.GetItemTemplate(itemId), playerClass, race);
				IReadOnlySet<int> questNeeded = QuestNeededItems();
				// NR-R23b: what is worn is always in view. A worn piece the leg protects from sale was left out with the rest of
				// what it protects, its slot looked empty, and the first piece in the cube for that slot took its place: at
				// Haramel's start the level-16 coin breeches came off for breeches of level 8. The leg's protection and
				// Haramel's rule of what may be put on are for the pieces in the cube.
				IReadOnlyList<NaturalGearUpgrade> wornNow = await NaturalInventoryCheck.EquipAsync(session,
					world.Inventory.Values.Where(item => item.Details.EquippedSlot.GetValueOrDefault() != 0 ||
						!questNeeded.Contains(item.ItemId) && (altgardLeg?.Haramel == null || altgardLeg.Haramel.CanUpgrade(
							runtime.Data.ItemDataDh.GetItemTemplate(item.ItemId)?.GetItemGroup().ToString()))),
					Describe, (long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF, refusedGear, gearToken, gear);
				await CleanBagsAsync("gear-check", gearToken);
				return wornNow;
			}
			// MR-f (docs/natural-muster.md; the operator, 2026-10-12): "trash anything no longer needed from all inventories
			// any time we do a gear check or restock supplies." What that is, is NaturalBagCleanup's; this carries it out
			// with the client's drag to the trash. The server keeps an item that is not breakable (Java CM_DELETE_ITEM);
			// such a stack is not asked for again.
			NaturalBagPlan BagPlan()
			{
				BotWorldModel world = session.Api.World;
				if (!cleanBags || world.Level <= NaturalHelpItemAllowlist.StarterMaxLevel) return new([], []);
				IReadOnlySet<int> needed = (IReadOnlySet<int>?)bagNeeds ?? QuestNeededItems();
				NaturalInventoryPlan bags = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
					.Decide(world, needed, altgardLeg?.CoinGear, altgardLeg?.Haramel);
				// The quests being worked: started or ready to hand in, and not one the class line leaves out.
				HashSet<int> worked = [.. world.Quests.Values.Where(quest => quest.Status is 3 or 4 && !ClassLine.LeftOutQuestIds.Contains(quest.QuestId))
					.Select(quest => quest.QuestId)];
				NaturalBagPlan plan = NaturalBagCleanup.Plan([.. world.Inventory.Values], world.Level, combat.ClassProfile.HelpItems, bags.Decisions, needed,
					NaturalPowerShardPolicy.Describer(runtime.Data, combat.ClassProfile.Gear.Class), NaturalQuestItemIndex.Load(runtime.RepoRoot), worked);
				return plan with { Discards = [.. plan.Discards.Where(discard => !keptByServer.Contains(discard.ObjectId))] };
			}
			async Task CleanBagsAsync(string trigger, CancellationToken cleanToken)
			{
				BotWorldModel world = session.Api.World;
				if (!cleanBags || world.IsDead || combat.InCombat) return;
				NaturalBagPlan bag = BagPlan();
				IReadOnlyList<NaturalBagDiscard> plan = bag.Discards;
				if (plan.Count == 0) return;
				var gone = new List<NaturalBagDiscard>();
				foreach (NaturalBagDiscard discard in plan)
				{
					await session.SendPacketAsync(GameClientPackets.DeleteItem(discard.ObjectId), cleanToken);
					await session.SynchronizeAsync(cleanToken);
					if (world.Inventory.ContainsKey(discard.ObjectId)) keptByServer.Add(discard.ObjectId);
					else gone.Add(discard);
				}
				bagDiscards.AddRange(gone);
				session.TraceDiagnostic(NaturalBagCleanup.Diagnostic, new Dictionary<string, object?>
				{
					["trigger"] = trigger, ["discarded"] = gone.Select(discard => $"{discard.ItemId}x{discard.Count}:{discard.Reason}").ToArray(),
					["keptByServer"] = plan.Except(gone).Select(discard => discard.ItemId).ToArray(), ["stacks"] = world.Inventory.Count,
					["kept"] = bag.Kept.GroupBy(stack => stack.Reason).ToDictionary(group => group.Key, group => group.Select(stack => stack.ItemId).ToArray()),
				});
			}
			combat.MaintainInventoryAsync = MaintainInventoryAsync;
			// Every aggressive spawn spot, plus every step of a patrol's route (a walker stands anywhere on it).
			var hostileSpawns = graph.GetMap(contract.MapId)!.Waypoints
				.Select(waypoint => (waypoint, template: waypoint.TemplateId is int id
					? runtime.Data.NpcDataDh.GetNpcTemplate(id) : null))
				.Where(entry => runtime.IsAggressive(entry.template))
				.Select(entry => new BotNavigationHazard(entry.waypoint.Position, entry.template!.GetAggroRange())).ToList();
			foreach (var group in runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId))
			{
				var template = runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId());
				if (!runtime.IsAggressive(template)) continue;
				foreach (var spot in group.GetSpawnTemplates())
				{
					if (spot.GetWalkerId() is not { Length: > 0 } routeId) continue;
					var route = runtime.Data.WalkerDataDh.GetWalkerTemplate(routeId);
					if (route == null) continue;
					foreach (var step in route.GetRouteSteps())
						hostileSpawns.Add(new BotNavigationHazard(new BotPosition(step.GetX(), step.GetY(), step.GetZ(), 0),
							template!.GetAggroRange()));
				}
			}
			combat.HostileSpawns = hostileSpawns;
			async Task MaintainInventoryAsync(CancellationToken maintenanceToken)
			{
				await EquipUpgradesAsync(maintenanceToken);
				BotWorldModel world = session.Api.World;
				// CP-24: the profile's restock table says whether to go, what to buy and how many.
				NaturalRestockRules restock = combat.ClassProfile.Restock;
				if (maintainingInventory || restock.Needed(world.Inventory.Values) is not { } line ||
					world.Kinah == failedRestockKinah) return;
				maintainingInventory = true;
				try
				{
					long stock = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, line.ItemId);
					long totalStock = restock.Stock(line, world.Inventory.Values);
					string root = runtime.RepoRoot;
					var inventoryPolicy = NaturalIshalgenInventoryPolicy.Load(root,
						world.Inventory.Values.Select(item => item.ItemId), ClassLine);
					NaturalInventoryPlan plan = inventoryPolicy.Decide(world, QuestNeededItems(), altgardLeg?.CoinGear, altgardLeg?.Haramel);
					long basePrice = runtime.Data.ItemDataDh.GetItemTemplate(line.ItemId).GetPrice();
					if (restock.Spendable(world.Kinah) < basePrice && plan.Sales.Count == 0)
					{
						failedRestockKinah = world.Kinah;
						return;
					}
					foreach (var candidate in NaturalIshalgenPotionPolicy.Vendors
						.OrderBy(vendor => Distance(session.CurrentPosition, vendor.Position)))
					{
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, candidate.NpcId, candidate.Position, navigator, maintenanceToken);
						if (!approach.Arrived) continue;
						int vendor = Require.IsType<int>(approach.TargetObjectId);
						await NaturalDialogProtocol.OpenAsync(session, vendor, maintenanceToken);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), maintenanceToken);
						if (plan.Sales.Count > 0)
						{
							await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 3), maintenanceToken);
							await session.WaitForPacketAsync(typeof(SM_SELL_ITEM), maintenanceToken);
							foreach (NaturalInventoryDecision sale in plan.Sales)
							{
								await session.SendPacketAsync(session.Api.Sell(vendor,
									[(sale.ObjectId, sale.Count)]), maintenanceToken);
								await session.SynchronizeAsync(maintenanceToken);
							}
						}
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 2), maintenanceToken);
						await session.WaitForPacketAsync(typeof(SM_TRADELIST), maintenanceToken);
						BotTradeWindow trade = world.Trade ?? throw new InvalidDataException("Vendor sent no trade window.");
						if (trade.TargetObjectId != vendor || !trade.Tabs.Contains(line.TradeListId))
							throw new InvalidDataException($"Ishalgen vendor {candidate.NpcId} did not offer the shipped elixir list.");
						BotVendorPrices prices = world.VendorPrices ??
							throw new InvalidDataException("No client-observed vendor prices.");
						long unitPrice = prices.BuyPrice(basePrice, trade.BuyPriceModifier);
						long count = restock.PurchaseCount(line, totalStock, world.Kinah, unitPrice);
						if (count > 0)
						{
							await session.SendPacketAsync(session.Api.Buy(vendor, [(line.ItemId, count)]), maintenanceToken);
							await session.SynchronizeAsync(maintenanceToken);
							long observed = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, line.ItemId);
							if (observed != stock + count)
								throw new InvalidDataException($"Elixir purchase not observed: {stock}+{count}, got {observed}.");
							if (totalStock + count <= line.AtOrBelow)
								failedRestockKinah = world.Kinah;
						}
						else failedRestockKinah = world.Kinah;
						await session.SendPacketAsync(session.Api.CloseDialog(vendor), maintenanceToken);
						// CP-06: a vendor visit is a stock check for the level 1-9 kit too.
						if (world.Level <= NaturalHelpItemAllowlist.StarterMaxLevel) await TopUpHelpItemsAsync("town");
						session.TraceDiagnostic("inventory-maintenance", new Dictionary<string, object?>
						{
							["vendorId"] = candidate.NpcId, ["sold"] = plan.Sales.Count,
							["elixirBefore"] = stock, ["elixirsBought"] = count,
							["totalHealingBefore"] = totalStock,
							["elixirAfter"] = NaturalIshalgenPotionPolicy.Count(world.Inventory.Values, line.ItemId), ["unitPrice"] = unitPrice,
							["kinah"] = world.Kinah,
						});
						return;
					}
					// Deep in a camp, every checked way to both vendors can be closed. A player keeps going on heals and
					// shops later: record it and retry once the purse changes (the same back-off as an empty purse).
					failedRestockKinah = world.Kinah;
					session.TraceDiagnostic("restock-unreachable", new Dictionary<string, object?>
					{
						["totalHealing"] = totalStock,
						["kinah"] = world.Kinah,
						["position"] = session.CurrentPosition,
					});
				}
				finally { maintainingInventory = false; }
			}
			int QuestStatus(int questId) => session.Api.World.CompletedQuestIds.Contains(questId) ? 5 :
				session.Api.World.Quests.TryGetValue(questId, out var q) ? q.Status : 0;
			int QuestVar(int questId) => session.Api.World.Quests.TryGetValue(questId, out var q) ? q.StepAndFlags & 0x00FFFFFF : 0;
			bool AtQuestStep(int questId, int step) => QuestStatus(questId) == 3 && QuestVar(questId) == step;
			var progress = new NaturalJourneyProgress(TimeSpan.FromMinutes(60),
				templatePlans.Values.Concat(altgardPlans.Values).SelectMany(p => p.Steps).Where(s => s.ItemId > 0).Select(s => s.ItemId).ToHashSet(),
				haramelProgress?.StallBudget);
			long journeyStart = haramelProgress is { } originalHaramel
				? originalHaramel.ProgressClockOriginMillis(runtime.Epoch.ToUnixTimeMilliseconds()) : runtime.NowMillis;
			// M3: a bounded wait the bot cannot hurry (a respawn, a 5% drop among other bots) counts as progress (NaturalJourneyProgress.Excuse).
			void ExcuseBoundedWait() => progress.Excuse(TimeSpan.FromMilliseconds(runtime.NowMillis - journeyStart));
			void EnforceCourseGameClock()
			{
				if (options.Course != null && runtime.NowMillis - journeyStart >= TimeSpan.FromHours(1).TotalMilliseconds)
					throw new TimeoutException($"Mau course exceeded its 3,600-second game-clock cap ({runtime.NowMillis - journeyStart} ms).");
			}
			long lastProgressObservation = -1000;
			// NL-4: a kill in a solo instance's rooms is progress toward the room's key: its monsters never respawn, so the excuse
			// is bounded by their number (the Fire Room's two lower tiers hold about a hundred elites).
			(NaturalJourneyNavigator? Navigator, int Kills) roomKills = (null, 0);
			session.AfterSynchronize = () =>
			{
				if (altgardLeg?.InstanceRooms is { } killRooms && session.Api.World.MapId == killRooms.MapId)
				{
					if (!ReferenceEquals(roomKills.Navigator, navigator)) roomKills = (navigator, navigator.UnavailableObjects.Count);
					else if (navigator.UnavailableObjects.Count > roomKills.Kills)
					{
						roomKills = (navigator, navigator.UnavailableObjects.Count);
						ExcuseBoundedWait();
					}
				}
				EnforceCourseGameClock();
				if (!session.Api.World.LoginStateObserved || runtime.NowMillis - lastProgressObservation < 1000) return;
				lastProgressObservation = runtime.NowMillis;
				checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				progress.Observe(checkpoint, TimeSpan.FromMilliseconds(runtime.NowMillis - journeyStart));
				if (haramelProgress != null)
				{
					haramelProgress = haramelProgress with { StallBudget = progress.State, Revives = combat.ReviveCount, LastObservedAtMillis = HaramelNow() };
					haramelProgress.Write(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-progress.json"));
				}
			};
			const float DeathSpotAvoidance = 12f;
			int reconnects = 0;
			string? relogAt = options.RelogAt;
			string? stopAt = options.StopAt;
			if (string.IsNullOrWhiteSpace(relogAt)) relogAt = null;
			if (string.IsNullOrWhiteSpace(stopAt)) stopAt = null;
			bool relogInjected = false;
			bool stopInjected = false;
			int[]? ParseBoundary(string? value) => value == null ? null : value.Split(':').Select(int.Parse).ToArray();
			// NA-17: several relog boundaries may be listed ("2008:3:52,2009:3:2"); each is injected once, in order met.
			int[][] relogBoundaries = relogAt == null ? [] : relogAt.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries)
				.Select(value => ParseBoundary(value.Trim())!).ToArray();
			var relogsInjected = new HashSet<int>();
			int[]? stopBoundary = ParseBoundary(stopAt);
			if (relogBoundaries.Any(boundary => boundary.Length != 3) || stopBoundary is { Length: not 3 })
				throw new ArgumentException("NI08_RELOG_AT and NI08_STOP_AT must be questId:status:packedVars.");
			session.BeforeSend = () =>
			{
				EnforceCourseGameClock();
				if (!stopInjected && stopBoundary is { } stop && QuestStatus(stop[0]) == stop[1] && QuestVar(stop[0]) == stop[2])
				{
					stopInjected = true;
					throw new NaturalJourneyCheckpointStopException();
				}
				for (int index = 0; index < relogBoundaries.Length; index++)
				{
					int[] at = relogBoundaries[index];
					if (relogsInjected.Contains(index) || QuestStatus(at[0]) != at[1] || QuestVar(at[0]) != at[2]) continue;
					relogsInjected.Add(index);
					relogInjected = relogsInjected.Count == relogBoundaries.Length;
					throw new EndOfStreamException($"NI-08 injected connection interruption at Q{at[0]} status {at[1]} vars {at[2]}.");
				}
				// Stop before the next ordinary action once level 9 is observed. Finish any current
				// combat/cast/movie first; the outer loop reconstructs the interrupted quest from its journal.
				if (!earlyAscensionUnderway && options.StopAfterQuest == null && EarlyAscensionNeeded() &&
					session.Api.World.MapId == contract.MapId && !session.Api.World.IsDead && !combat.InCombat &&
					session.Api.Timing.BlockingActivities.Count == 0 && Engaged() is var engaged &&
					engaged.Attackers.Length == 0 && engaged.Pursuers.Length == 0)
					throw new NaturalEarlyAscensionRequiredException();
			};
			if (altgardLeg?.AbyssEntry != null)
			{
				await RunAbyssEntryAsync();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			if (altgardLegId != null)
			{
				await RunAltgardLeg1Async();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			if (options.ClericEncounter)
			{
				await RunClericEncounterAsync();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			if (options.Course is NaturalMauCourse course)
			{
				navigator.AvoidHostileAggro = true;
				NaturalMauEncounter? encounter = options.Encounter;
				int questId = course == NaturalMauCourse.GeneratorToRae ? 2007 : 2129;
				int expectedVar = encounter == NaturalMauEncounter.BlockedGeneratorRejoin ? 8 :
					course == NaturalMauCourse.GeneratorToRae ? 6 : 1;
				Require.True(AtQuestStep(questId, expectedVar),
					$"Mau course requires Q{questId} START/{expectedVar} observed from client packets.");
				Require.Equal((ushort)9, session.Api.World.Level);
				session.TraceDiagnostic("phase0-course-start", new Dictionary<string, object?>
				{
					["course"] = course.ToString(), ["encounter"] = encounter?.ToString(),
					["seed"] = runtime.Seed,
					["checkpoint"] = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine),
				});
				if (encounter != null)
				{
					session.BeginStep($"phase1-{encounter}", "shared-mau-navigation-and-combat");
					session.TraceDiagnostic("phase1-encounter-start", new Dictionary<string, object?>
					{
						["encounter"] = encounter.ToString(), ["position"] = session.CurrentPosition,
					});
					if (encounter == NaturalMauEncounter.BlockedGeneratorRejoin)
					{
						await CompleteWheresRaeThisTimeAsync();
						Require.Equal(8, QuestVar(2007));
					}
					else
					{
						int templateId = encounter switch
						{
							NaturalMauEncounter.IsolatedStalker => 210750,
							NaturalMauEncounter.TwoAttackerPull => 211284,
							NaturalMauEncounter.MovingPatrol => 210407,
							NaturalMauEncounter.HatataAlone or NaturalMauEncounter.HatataWithAdd => 210409,
							_ => throw new ArgumentOutOfRangeException(nameof(encounter)),
						};
						await KillShippedSpawnAsync(templateId);
						if (templateId == 210409)
							Require.True(QuestVar(2129) >= 65, "Hatata objective update was not client-observed.");
					}
				}
				else if (course == NaturalMauCourse.GeneratorToRae)
					await CompleteWheresRaeThisTimeAsync();
				else
				{
					session.BeginStep("phase0-rae-to-hatata", "kill-hatata-with-current-pull-and-combat-policy");
					await KillShippedSpawnAsync(210409);
					Require.True(QuestVar(2129) >= 65, "Hatata objective update was not client-observed.");
				}
				await DefendAgainstEngagedAsync("phase0-terminal-disengagement");
				await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
				await session.SynchronizeAsync(token);
				var (remainingAttackers, remainingPursuers) = Engaged();
				Require.True(!session.Api.World.IsDead && remainingAttackers.Length == 0 && remainingPursuers.Length == 0,
					$"Mau course ended with attackers [{string.Join(',', remainingAttackers)}] and pursuers [{string.Join(',', remainingPursuers)}].");
				string outcome = Path.Combine(Path.GetDirectoryName(combatTracePath)!, "course-completion.json");
				await File.WriteAllTextAsync(outcome, System.Text.Json.JsonSerializer.Serialize(new
				{
					Course = course.ToString(), Encounter = encounter?.ToString(), runtime.Seed,
					EndGameMillis = runtime.NowMillis,
					Position = session.CurrentPosition, Quest = session.Api.World.Quests[questId],
					Deaths = combat.ReviveCount, Disengaged = true,
					Checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine),
				}), token);
				session.TraceDiagnostic("phase0-course-complete", new Dictionary<string, object?>
				{
					["course"] = course.ToString(), ["encounter"] = encounter?.ToString(),
					["deaths"] = combat.ReviveCount,
					["position"] = session.CurrentPosition, ["disengaged"] = true,
				});
				await session.QuitAsync(token);
				runtime.AssertClean();
				return;
			}
			for (int sequence = 1; sequence <= 100; sequence++)
			{
				try
				{
					if (options.CapitalStage == "first" || EarlyAscensionNeeded())
					{
						await RunEarlyAscensionAsync();
						continue;
					}
					if (options.AscensionBridge && !IshalgenPending() && AscensionBridgeStarted())
					{
						if (session.Api.World.CompletedQuestIds.Contains(2009) && session.Api.World.MapId == contract.MapId)
						{
							await CompleteJourneyAsync();
							return;
						}
						// NA-17: a login (fresh, or after an interruption) past the Munin stop resumes the bridge.
						await RunAscensionBridgeAsync();
						if (continuousAltgard) await RunAllAltgardLegsAsync();
						session.PublishDashboard("completed", force: true);
						await session.QuitAsync(token);
						runtime.AssertClean();
						return;
					}
					if (session.Api.World.IsDead) await RestSafelyAsync(token);
					// CP-06: the level 1-9 kit's stock checks, as the later legs have them: at the run's start, after a
					// level-up and at each checkpoint (a decision is one). Below level 10 only: the Cleric who returns to
					// finish Ishalgen is supplied by the bridge runner, as before.
					if (session.Api.World.Level <= NaturalHelpItemAllowlist.StarterMaxLevel)
						await TopUpHelpItemsAsync(sequence == 1 ? "run-start"
							: session.Api.World.Level != helpCheckedAtLevel ? "level-up" : "checkpoint");
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, sequence, earlyAscension: options.AscensionBridge, line: ClassLine);
					progress.Observe(checkpoint, TimeSpan.FromMilliseconds(runtime.NowMillis - journeyStart));
					decision = checkpoint.Next;
					if (options.OptimizeHubs && NaturalIshalgenHubPolicy.MayReorder(decision) && session.Api.World.MapId == contract.MapId &&
						!session.Api.World.IsDead)
					{
						await AcceptAllAtCurrentHubAsync();
						checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
							session.ConnectionGeneration, contract, session.CurrentPosition, sequence, earlyAscension: options.AscensionBridge, line: ClassLine);
						decision = checkpoint.Next;
						int[] safeGroup = NaturalIshalgenHubPolicy.CurrentSafeGroup(
							session.Api.World.CompletedQuestIds);
						int[] available = NaturalIshalgenHubPolicy.Order(
							decision.Quests.Where(quest => safeGroup.Contains(quest.QuestId) &&
								NaturalIshalgenHubPolicy.CanWorkNow(
								quest.QuestId, session.Api.World.CompletedQuestIds) &&
								(quest.Verdict == "active" ||
								quest.Verdict == "candidate" &&
								(templatePlans.ContainsKey(quest.QuestId) ||
								NaturalIshalgenHubPolicy.CustomNpcStarters.ContainsKey(quest.QuestId) ||
								quest.QuestId == 2114)))
								.Select(quest => quest.QuestId), contract,
							id => runtime.Data.Quests.GetQuestById(id)?.GetCategory() ?? QuestCategory.QUEST,
							NaturalIshalgenHubPolicy.At(session.CurrentPosition), workedTemplates);
						if (safeGroup.Length > 0 && available.Length == 0)
							throw new InvalidDataException($"Safe hub group [{string.Join(',', safeGroup)}] has no available quest at level {session.Api.World.Level}.");
						if (available.Length > 0)
							decision = decision with
							{
								SelectedQuestId = available[0],
								SelectedAction = QuestStatus(available[0]) >= 3 ? "continue-quest" : "find-quest-starter",
								Reason = $"Safe hub group: lowest level, blue before gold, and unfinished local work; selected Q{available[0]}.",
							};
					}
					// Q2005 originally switched this on for the rest of the run. Reconstruct the policy
					// from the journal: a cold login after Q2005 must not walk straight through the Mau camp.
					navigator.AvoidHostileAggro = decision.SelectedQuestId is 2005 or 2006 or 2007 ||
						session.Api.World.CompletedQuestIds.Contains(2005);
					dashboard.PublishDecision("b01", decision);
					session.TraceDiagnostic("natural-resume-observation", new Dictionary<string, object?> { ["checkpoint"] = checkpoint });
					if (decision.SelectedQuestId is not int questId)
					{
						await CompleteJourneyAsync();
						return;
					}
					if (options.OptimizeHubs)
					{
						BotPosition? journeyDestination = null;
						if (templatePlans.TryGetValue(questId, out QuestRunPlan? selectedPlan))
						{
							QuestRunNpc? endpoint = workedTemplates.Contains(questId)
								? selectedPlan.EndNpcs.FirstOrDefault()
								: selectedPlan.StartNpcs.FirstOrDefault();
							QuestRunPosition? place = endpoint?.Positions.FirstOrDefault(position =>
								position.MapId == contract.MapId);
							if (place != null)
								journeyDestination = new(place.X, place.Y, place.Z, checked((byte)place.Heading));
						}
						else if (QuestStatus(questId) < 3)
							journeyDestination = NaturalIshalgenHubPolicy.ForQuest(questId)?.Center;
						if (journeyDestination is BotPosition destination)
						{
							await UseFasterTravelAsync(destination);
							await AcceptAllAtCurrentHubAsync();
						}
					}
					await BindAtIshalgenHubIfNeededAsync(questId);
					switch (questId)
					{
						case 2101: await CompleteQ2101Async(); break;
						case 2102: await CompleteQ2102Async(); break;
						case 2103: await CompleteQ2103Async(); break;
						case 2104: await CompleteQ2104Async(); break;
						case 2100: await FinishCaptainOrderAsync(); break;
						case 2001: await CompleteThinkingAheadAsync(); break;
						case 2002: await AdvanceWheresRaeAsync(); break;
						case 2003: await CompleteTreasureOfTheDeceasedAsync(); break;
						case 2004: await CompleteCharmedCubeAsync(); break;
						case 2005: await CompleteTeachingALessonAsync(); break;
						case 2006: await CompleteHitThemWhereItHurtsAsync(); break;
						case 2007: await CompleteWheresRaeThisTimeAsync(); break;
						case 2105: await CompleteSparkleAndShineAsync(); break;
						case 2106: await CompleteVanarsFlatteryAsync(); break;
						case 2132: await CompleteNewSkillAsync(); break;
						case 2114: await CompleteInsectProblemAsync(); break;
						case 2123: await CompleteImprisonedGourmetAsync(); break;
						case 2125: await CompleteRobberyPlotAsync(); break;
						case 2135: await CompleteForLoveOfNegiAsync(); break;
						default:
							if (!templatePlans.TryGetValue(questId, out var plan))
								throw new InvalidDataException($"Natural Ishalgen Q{questId} has no journey actions.");
						if (!options.OptimizeHubs)
							await CompleteTemplateQuestAsync(plan);
						else if (workedTemplates.Contains(questId))
						{
							await CompleteTemplateQuestAsync(plan, TemplatePhase.Claim);
							workedTemplates.Remove(questId);
						}
						else
						{
							await CompleteTemplateQuestAsync(plan, TemplatePhase.Work);
							if (QuestStatus(questId) != 5) workedTemplates.Add(questId);
						}
						break;
					}
					// CP-23 (CP-Q13): a new class line runs the equipment check after each completed quest. The accepted line
					// keeps its check points, at the end of a rest.
					if (ClassLine.ChecksGearAfterIshalgenTurnIns && session.Api.World.CompletedQuestIds.Contains(questId))
						await EquipUpgradesAsync(token);
					if (questId == 2004 && stopAfterQ2004 || questId == 2005 && stopAfterQ2005 ||
						questId == 2006 && stopAfterQ2006 || questId == 2007 && stopAfterQ2007)
					{
						Require.Contains(questId, session.Api.World.CompletedQuestIds);
						await session.QuitAsync(token);
						runtime.AssertClean();
						return;
					}
				}
				catch (NaturalEarlyAscensionRequiredException)
				{
					// This is a route interruption, not a failure or a reset of the quest/revival budgets.
					continue;
				}
				catch (NaturalCapitalCheckpointStopException stop)
				{
					await CompleteCapitalCheckpointAsync(stop.Stage);
					await session.QuitAsync(token);
					runtime.AssertClean();
					return;
				}
				catch (NaturalJourneyCheckpointStopException)
				{
					if (relogAt != null) Require.True(relogInjected, "Requested NI-08 interruption was never exercised before checkpoint.");
					session.BeforeSend = null;
					await session.QuitAsync(token);
					checkpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
					string receipt = Path.Combine(Path.GetDirectoryName(combatTracePath)!, "resume-receipt.json");
					await File.WriteAllTextAsync(receipt, System.Text.Json.JsonSerializer.Serialize(new
					{
						checkpoint.CharacterId,
						ElapsedMillis = runtime.NowMillis,
						Checkpoint = checkpoint,
					}), token);
					session.PublishDashboard("checkpoint-saved", force: true);
					runtime.AssertClean();
					return; // Deliberately partial: the restart runner must verify phase two completes.
				}
				catch (Exception failure) when (NaturalJourneyReconnect.IsConnectionLoss(failure) && reconnects < 3 && !token.IsCancellationRequested)
				{
					WriteFailure("disconnect", failure);
					reconnects++;
					await session.CloseSelectionAsync(token);
					await session.AdvanceOfflineAsync(TimeSpan.FromSeconds(11), token);
					await session.WaitForReentryAsync(token);
					session.ClearPacketHistory();
					await NaturalJourneyReconnect.RunAsync(async reconnectToken =>
					{
						await session.ReloginExistingCharacterAsync(reconnectToken);
						await session.EnterWorldAsync(reconnectToken);
						await session.SynchronizeAsync(reconnectToken);
					}, session.AdvanceOfflineAsync, (_, reconnectFailure) => WriteFailure("reconnect", reconnectFailure), token);
					navigator.UnavailableObjects.Clear();
					quietNeighbours.Clear();
					fightRouteOpen = false;
					fightRouteOpenAt = null;
					emptySpawnWaits = 0;
					continue;
				}

			}

			throw new TimeoutException("Natural journey exhausted its 100 decision attempts.");

			async Task CompleteQ2101Async()
			{
				session.BeginStep("ni07-q2101-start", "walk-to-asak-and-accept");
				if (QuestStatus(2101) < 3)
				{
					int asak = await ApproachShippedSpawnAsync(203500);
					await session.StartQuestAsync(asak, 2101, token);
				}

				session.BeginStep("ni07-q2101-finish", "walk-to-vandar-and-report");
				NaturalNavigationResult vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, 203504, new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
				Require.True(vandarApproach.Arrived, vandarApproach.Reason);
				int vandar = Require.IsType<int>(vandarApproach.TargetObjectId);
				await session.FinishQuestAsync(vandar, 2101, token);
				Require.Equal(5, session.Api.World.Quests[2101].Status);
			}

			async Task CompleteQ2102Async()
			{
				if (QuestStatus(2102) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203504), 2102, token);
				for (int kill = 0; QuestStatus(2102) == 3 && QuestVar(2102) < 4; kill++)
				{
					session.BeginStep($"ni07-q2102-kill-{kill + 1}", "approach-and-fight-sprigg-worker");
					BotPosition anchor = graph.GetMap(contract.MapId)!.Waypoints
						.Where(waypoint => waypoint.TemplateId == 210363)
						.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
						.First().Position;
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, 210363, anchor, navigator, token);
					Require.True(approach.Arrived, approach.Reason);
					int target = Require.IsType<int>(approach.TargetObjectId);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2102].StepAndFlags);
				session.BeginStep("ni07-q2102-finish", "return-to-vandar-and-claim");
				var vandarApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, 203504,
					new BotPosition(526.99f, 2775.67f, 295.751f, 0), navigator, token);
				Require.True(vandarApproach.Arrived, vandarApproach.Reason);
				await session.FinishQuestAsync(Require.IsType<int>(vandarApproach.TargetObjectId), 2102, token);
				Require.Equal(5, session.Api.World.Quests[2102].Status);
			}

			async Task CompleteQ2103Async()
			{
				session.BeginStep("ni07-q2103-start", "accept-sprigg-report-at-vandar");
				if (QuestStatus(2103) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203504), 2103, token);
				session.BeginStep("ni07-q2103-finish", "walk-to-guheitun-and-report");
				NaturalNavigationResult guheitunApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId,
					203501, new BotPosition(223.975f, 2679.86f, 295.25f, 0), navigator, token);
				Require.True(guheitunApproach.Arrived, guheitunApproach.Reason);
				await session.FinishQuestAsync(Require.IsType<int>(guheitunApproach.TargetObjectId), 2103, token);
				Require.Equal(5, session.Api.World.Quests[2103].Status);
			}

			async Task CompleteQ2104Async()
			{
				session.BeginStep("ni07-q2104-start", "walk-to-vanar-and-accept");
				if (QuestStatus(2104) < 3)
					await session.StartQuestAsync(await ApproachShippedSpawnAsync(203502), 2104, token);
				for (int basket = 0; QuestStatus(2104) == 3 && ItemCount(session.Api.World, 182203104) < 3; basket++)
				{
					session.BeginStep($"ni07-q2104-basket-{basket + 1}", "walk-and-loot-shipped-basket");
					int objectId = await UseAndLootQuestObjectAsync(700124, 182203104);
					navigator.UnavailableObjects.Add(objectId);
				}
				if (QuestStatus(2104) == 3) Require.Equal(3, ItemCount(session.Api.World, 182203104));
				session.BeginStep("ni07-q2104-finish", "return-to-vanar-and-turn-in");
				int vanar = await ApproachAsync(203502, new BotPosition(220.15f, 2678.81f, 295.25f, 0));
				await FinishNaturalItemQuestAsync(vanar, 2104);
				Require.Equal(5, session.Api.World.Quests[2104].Status);
				Require.Equal(0, ItemCount(session.Api.World, 182203104));
			}

			async Task CompleteJourneyAsync()
			{
				if (stopAt != null) Require.True(stopInjected, "Requested NI-08 checkpoint was never exercised.");
				// With the bridge on, its endpoint checks that every requested interruption happened (NA-17).
				if (relogAt != null && !options.AscensionBridge) Require.True(relogInjected, "Requested NI-08 interruption was never exercised.");
				Require.Equal(41, contract.Quests.Length);
				Require.All(contract.Quests, quest => Require.Contains(quest.Id, session.Api.World.CompletedQuestIds));
				if (options.AscensionBridge && session.Api.World.CompletedQuestIds.Contains(2009))
				{
					Require.True(session.Api.World.Level >= 10 && combat.IsLineSecondClass,
						$"Ishalgen must finish as the ceremony-proven {ClassLine.SecondName}; the character is {combat.ObservedCharacter}.");
					Require.Contains(2008, session.Api.World.CompletedQuestIds);
					session.BeginStep("ni07-ascended-ishalgen-complete", "finish-all-ishalgen-quests-after-the-early-ceremony");
				}
				else
				{
					Require.Equal((ushort)9, session.Api.World.Level);
					Require.DoesNotContain(contract.AscensionQuestId, session.Api.World.CompletedQuestIds);
					Require.Equal(3, session.Api.World.Quests[contract.AscensionQuestId].Status);
					Require.Equal(0, session.Api.World.Quests[contract.AscensionQuestId].StepAndFlags);
					session.BeginStep("ni07-munin-stop", "stand-at-munin-without-ascension-dialogue");
					await ApproachShippedSpawnAsync(contract.AscensionNpcId);
				}
				decision = NaturalIshalgenDecisionEngine.Decide(contract, ObserveNaturalJourney(session), 57, earlyAscension: options.AscensionBridge, line: ClassLine);
				Require.Equal("journey-complete", decision.SelectedAction);
				Require.Equal("complete", decision.Outcome);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "completion.json"),
					System.Text.Json.JsonSerializer.Serialize(NaturalJourneyCheckpoint.Capture(session.Api.World,
						session.CharacterId, session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine)), token);
				// NA-03: a saved Munin snapshot restores with this clock so game time keeps moving forward.
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "completion-clock.json"),
					System.Text.Json.JsonSerializer.Serialize(new { session.CharacterId, ElapsedMillis = runtime.NowMillis }), token);
				// NA-11/12: with the bridge enabled, the Ascension bridge (docs/natural-ascension-altgard.md) starts where
				// Ishalgen ends instead of quitting here.
				if (options.AscensionBridge)
				{
					if (session.Api.World.CompletedQuestIds.Contains(2009) && session.Api.World.MapId == contract.MapId)
						await TakeCeremonyTeleporterAsync(toIshalgen: false);
					await RunAscensionBridgeAsync();
				}
				if (continuousAltgard) await RunAllAltgardLegsAsync();
				session.PublishDashboard("completed", force: true);
				await session.QuitAsync(token);
				runtime.AssertClean();
			}

			// NA-12: the Ascension bridge runner. The NA-11 engine picks each move from the client's view; contract "talk"
			// steps are played by one generic handler; moves not built yet stop the run with a precise receipt.
			async Task RunAscensionBridgeAsync(bool ceremonyOnly = false)
			{
				NaturalAscensionContract bridge = LineBridge();
				await TopUpHelpItemsAsync("run-start");
				await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token); // NA-19: a fresh login or a resume
				string? previous = null;
				int repeats = 0;
				for (int sequence = 1; sequence <= 80; sequence++)
				{
					await session.SynchronizeAsync(token);
					if (!ceremonyOnly && CapitalPending() && session.Api.World.CompletedQuestIds.Contains(2009) &&
						session.Api.World.MapId == capitalContract.MapId && AtQuestStep(capitalContract.DispatchQuestId, 0))
						await RunCapitalPassAsync();
					if (!ceremonyOnly && laterCapital != null && !IshalgenPending() && session.Api.World.Level >= 13 &&
						session.Api.World.MapId == capitalContract.MapId && AtQuestStep(capitalContract.DispatchQuestId, 0))
						await PrepareLaterCapitalBookAsync();
					if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					NaturalAscensionDecision next = NaturalAscensionDecisionEngine.Decide(bridge,
						NaturalAscensionObservation.Observe(session.Api.World, bridgeShopVisited), sequence, ceremonyOnly);
					session.TraceDiagnostic("ascension-bridge-decision", new Dictionary<string, object?>
					{
						["sequence"] = sequence, ["action"] = next.Action, ["step"] = next.StepKey, ["quest"] = next.QuestId,
						["outcome"] = next.Outcome, ["reason"] = next.Reason, ["map"] = session.Api.World.MapId,
						["checks"] = next.Checks.Select(check => $"{check.Rule}:{check.Verdict}").ToArray(),
					});
					session.PublishDashboard();
					string signature = $"{next.Action}/{next.StepKey}/{next.Reason}";
					repeats = signature == previous ? repeats + 1 : 0;
					previous = signature;
					Require.True(repeats < 3, $"The Ascension bridge made no progress on {signature}: {next.Reason}");
					if (next.Outcome != "planned")
					{
						await WriteBridgeStopAsync(next);
						if (next.Outcome == "complete" && !ceremonyOnly) await CompleteAscensionLegAsync(bridge);
						return;
					}
					NaturalAscensionStep? step = next.StepKey is string key ? bridge.Steps.Single(s => s.Key == key) : null;
					if (next.Action == "talk" && step != null && PlaysBridgeStep(bridge, step))
						await PlayBridgeTalkAsync(step);
					else if (next.Action == "fight-trial")
						await FightAscensionTrialAsync(bridge);
					else if (next.Action == "wait-flight")
						await session.AdvanceAsync(TimeSpan.FromSeconds(20), token);
					else if (next.Action == "teleport")
						await TakeBridgeTeleporterAsync(bridge);
					else if (next.Action == "bind")
						await BindInAltgardAsync(bridge);
					else if (next.Action == "shop")
						await ShopInAltgardAsync(bridge);
					else
					{
						await WriteBridgeStopAsync(next with { Outcome = "awaiting-capability", Reason = "Not built yet: " + next.Reason });
						return;
					}
				}
				throw new InvalidDataException("The Ascension bridge exceeded 80 decisions.");
			}

			long ObservedExperience() => session.Api.World.CurrentExperience +
				runtime.Data.PlayerExperienceTable.GetStartExpForLevel(session.Api.World.Level);

			// NL-3: a talk or a use within reach of something that stands where no navmesh lies near it (Kasir behind the
			// Pandaemonium library's desk, the Artifact of the Inception on its base on Leibo Island): the bot walks to the ground
			// of its own island nearest it, then steps straight on over the server's ground, short of what blocks sight, until it
			// is within reach, as a player walks up to a counter.
			async Task<bool> StepWithinReachAsync(BotNavigationGeometry ground, NaturalJourneyNavigator walker, int map, BotPosition target,
				float reach, string purpose)
			{
				bool Refused(string reason)
				{
					session.TraceDiagnostic("step-within-reach-refused", new Dictionary<string, object?>
					{ ["purpose"] = purpose, ["reason"] = reason, ["position"] = session.CurrentPosition });
					return false;
				}
				if (Distance(session.CurrentPosition, target) <= reach) return true;
				BotPosition? nearest = ground.IslandGround(map, session.CurrentPosition, at => Distance(at, target) <= reach + 4)
					.OrderBy(at => Distance(at, target)).Cast<BotPosition?>().FirstOrDefault();
				if (nearest is not { } edge) return Refused("no ground of this island near it");
				if (Distance(session.CurrentPosition, edge) > 0.5f)
				{
					IReadOnlyList<BotPosition> toEdge = ground.FindJourneyPath(map, session.CurrentPosition, edge);
					if (toEdge.Count == 0) return Refused($"no path to {edge}");
					await walker.MoveAsync(toEdge, token);
					await walker.SynchronizeAsync(token);
				}
				var steps = new List<BotPosition>();
				BotPosition last = session.CurrentPosition;
				while (Distance(last, target) > reach && steps.Count < 40)
				{
					float gap = Distance(last, target);
					BotPosition ahead = new(last.X + (target.X - last.X) * 0.25f / gap, last.Y + (target.Y - last.Y) * 0.25f / gap, last.Z, last.Heading);
					if (ground.StaticGroundAt(map, ahead) is not { } footing || MathF.Abs(footing.Z - last.Z) > 0.8f ||
						!ground.HasLineOfSight(map, last, footing with { X = footing.X + (target.X - last.X) * 0.3f / gap, Y = footing.Y + (target.Y - last.Y) * 0.3f / gap }))
						break;
					steps.Add(footing);
					last = footing;
				}
				if (Distance(last, target) > reach) return Refused($"the straight steps end {Distance(last, target):F1} m away at {last}");
				if (steps.Count > 0)
				{
					await walker.MoveAsync(steps, token);
					await walker.SynchronizeAsync(token);
				}
				session.TraceDiagnostic("step-within-reach", new Dictionary<string, object?>
				{
					["purpose"] = purpose, ["reach"] = reach, ["steps"] = steps.Count, ["position"] = session.CurrentPosition,
					["distance"] = Distance(session.CurrentPosition, target),
				});
				return Distance(session.CurrentPosition, target) <= reach;
			}

			async Task<int> ApproachCapitalNpcAsync(int npcId)
			{
				int map = session.Api.World.MapId!.Value;
				BotPosition anchor = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Where(group => group.GetNpcId() == npcId)
					.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading()))
					.OrderBy(at => Distance(session.CurrentPosition, at)).First();
				BotNavigationGeometry mapGeometry = runtime.CreateGeometry();
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				combat.EnterMap(here, mapGeometry, map);
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, anchor));
				if (Distance(session.CurrentPosition, anchor) > 5 &&
					mapGeometry.FindInteractionPath(map, session.CurrentPosition, anchor).Count == 0)
					await capitalTravel.ConnectColiseumAsync(mapGeometry, anchor, token);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(map, npcId, anchor, here, token);
				if (!approach.Arrived && laterCapital != null &&
					approach.Reason == "Reached the static area anchor but no NPC was observed.")
				{
					// Lusena can be away from her spawn anchor. Search the shipped patrol hints in reverse
					// to meet the walker, then let ordinary client observations identify and approach her.
					var patrols = runtime.Data.SpawnsDh.GetSpawnsByWorldId(map).Where(g => g.GetNpcId() == npcId)
						.SelectMany(g => g.GetSpawnTemplates()).Select(s => s.GetWalkerId()).OfType<string>().Distinct();
					foreach (string patrol in patrols)
					{
						var route = runtime.Data.WalkerDataDh.GetWalkerTemplate(patrol);
						if (route == null) continue;
						foreach (var point in route.GetRouteSteps().AsEnumerable().Reverse())
						{
							BotPosition hint = new(point.GetX(), point.GetY(), point.GetZ(), 0);
							session.TraceDiagnostic("later-capital-walker-search", new Dictionary<string, object?>
							{ ["npc"] = npcId, ["route"] = patrol, ["hint"] = hint });
							approach = await NaturalIshalgenNavigator.ApproachNpcAsync(map, npcId, hint, here, token);
							if (approach.Arrived) break;
						}
						if (approach.Arrived) break;
					}
				}
				if (!approach.Arrived && (laterCapital != null || altgardLeg?.AbyssEntry != null ||
					options.AltgardLegId is { } capitalLeg && NaturalAltgardContract.MusteredLegs.Contains(capitalLeg)))
				{
					// The generic navigator uses a three-metre interaction radius. City NPCs
					// can have reachable ground farther out but still inside their shipped talk range.
					// NL-3: Kasir (Q24044) stands in the museum hall 11.7 m past Kushar's desk, where the navmesh ends
					// (nl3-ro-d5).
					BotKnownObject? seen = session.Api.World.Objects.Values.FirstOrDefault(o => o.TemplateId == npcId && !o.IsCorpse);
					BotPosition target = seen?.SettledPosition ?? anchor;
					float range = Math.Min(5, runtime.Data.NpcDataDh.GetNpcTemplate(npcId)!.GetTalkDistance());
					IReadOnlyList<BotPosition> route = NaturalCapitalTravel.FindGroundApproachPath(mapGeometry, map, session.CurrentPosition, target, range);
					if (route.Count > 0 && here.IsSegmentSafe(route, seen?.ObjectId))
					{
						await here.MoveAsync(route, token);
						await here.SynchronizeAsync(token);
						seen = session.Api.World.Objects.Values.Where(o => o.TemplateId == npcId && !o.IsCorpse)
							.OrderBy(o => Distance(session.CurrentPosition, o.SettledPosition)).FirstOrDefault();
						if (seen != null && Distance(session.CurrentPosition, seen.SettledPosition) <= range)
						{
							session.TraceDiagnostic("capital-talk-range-approach", new Dictionary<string, object?>
							{ ["npc"] = npcId, ["range"] = range, ["checkedPoints"] = route.Count, ["position"] = session.CurrentPosition });
							return seen.ObjectId;
						}
					}
				}
				if (!approach.Arrived && options.AltgardLegId is { } counterLeg && NaturalAltgardContract.MusteredLegs.Contains(counterLeg) &&
					await ApproachOverCounterAsync() is int across)
					return across;
				Require.True(approach.Arrived, $"Capital NPC {npcId}: {approach.Reason}");
				return Require.IsType<int>(approach.TargetObjectId);

				// NL-3: Kasir (Q24044) stands behind the Pandaemonium library's desk (pr_d_library_in_03c), on ground the bake
				// cut off 2.5 m from the hall's, 6.5 m from him (nl3-ro-d6). The bot walks to the hall's ground nearest him and
				// steps straight on to the desk, on the server's ground and short of what blocks sight, until he is in talk
				// range, as a player leans on a counter (nl3-ro-d8: the desk stops it 6.3 m from him).
				async Task<int?> ApproachOverCounterAsync()
				{
					BotKnownObject? behind = session.Api.World.Objects.Values.Where(o => o.TemplateId == npcId && !o.IsCorpse)
						.OrderBy(o => Distance(session.CurrentPosition, o.SettledPosition)).FirstOrDefault();
					if (behind == null) return null;
					// Java isInTalkRange: talk distance plus 1 m between the bodies' edges (isInRange, centerToCenter false),
					// the NPC's bound radius and the player's added (0.25 m for every player, PlayerAccountData), less
					// 0.15 m in hand.
					var counterNpc = runtime.Data.NpcDataDh.GetNpcTemplate(npcId)!;
					float talk = counterNpc.GetTalkDistance() + 1 + counterNpc.GetBoundRadius().GetMaxOfFrontAndSide() + 0.25f - 0.15f;
					return await StepWithinReachAsync(mapGeometry, here, map, behind.SettledPosition, talk, $"talk-{npcId}") ? behind.ObjectId : null;
				}
			}

			async Task PlayLaterCapitalStepAsync(NaturalAltgardStep step)
			{
				session.BeginStep("rc-" + step.Key, "later-capital-dialog");
				int npc = session.Api.World.MapId == capitalContract.MapId ? await ApproachCapitalNpcAsync(step.NpcId)
					: await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
				for (int attempt = 0; ; attempt++)
				{
					try
					{
						long xp = ObservedExperience(), kinah = session.Api.World.Kinah;
						bool completedBefore = session.Api.World.CompletedQuestIds.Contains(step.QuestId);
						if (step.NpcId == 700212) await NaturalLaterCapitalSteps.ReadQuestBookAsync(session, step, npc, token);
						else session.TraceDiagnostic("later-capital-step", new Dictionary<string, object?>
							{ ["change"] = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token) });
						if (!completedBefore && session.Api.World.CompletedQuestIds.Contains(step.QuestId))
							session.TraceDiagnostic("later-capital-payment", new Dictionary<string, object?>
							{ ["quest"] = step.QuestId, ["experience"] = ObservedExperience() - xp, ["kinah"] = session.Api.World.Kinah - kinah });
						return;
					}
					catch (NaturalDialogTooFarException) when (attempt < NaturalLaterCapitalSteps.DialogRetryLimit(step.NpcId))
					{
						// A walker can already be past its cached move start. Intercept her
						// announced waypoint and retry at a player cadence until she arrives.
						BotKnownObject seen = session.Api.World.Objects[npc];
						BotPosition destination = NaturalLaterCapitalSteps.DialogReapproachPosition(step.NpcId, seen);
						session.TraceDiagnostic("later-capital-dialog-reapproach", new Dictionary<string, object?>
						{ ["npc"] = step.NpcId, ["position"] = seen.Position, ["nextWaypoint"] = seen.MoveTarget, ["destination"] = destination });
						NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
						IReadOnlyList<BotPosition> route = await here.FindRouteAsync(session.CurrentPosition, destination, token);
						Require.True(route.Count > 0 || Distance(session.CurrentPosition, destination) <= 1,
							$"Capital NPC {step.NpcId}: no checked dialogue reapproach.");
						if (route.Count > 0) await here.MoveAsync(route, token);
						await here.SynchronizeAsync(token);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					}
				}
			}

			async Task PrepareLaterCapitalBookAsync()
			{
				if (laterCapital == null || session.Api.World.CompletedQuestIds.Contains(2919) || session.Api.World.Level < 13) return;
				NaturalIshalgenContract savedContract = contract;
				NaturalJourneyNavigator savedNavigator = navigator;
				BotNavigationGeometry savedGeometry = geometry;
				var savedFarApproach = farApproach;
				Action? savedRevive = combat.AfterBindRevive;
				void EnterBookMap()
				{
					NaturalMapKey key = NaturalMapKey.Observe(session.Api.World);
					var defend = navigator.DefendOnAttackAsync;
					navigator = mapNavigators.Enter(key, newEntry: false);
					navigator.DefendOnAttackAsync = defend;
					navigator.AvoidHostileAggro = true;
					geometry = runtime.CreateGeometry();
					contract = contract with { MapId = key.MapId };
					combat.EnterMap(navigator, geometry, key.MapId);
					navigator.AvoidSpots = combat.DeathSpots;
				}
				try
				{
					farApproach = null;
					combat.AfterBindRevive = EnterBookMap;
					EnterBookMap();
					if (!await NaturalLaterCapitalSteps.PrepareBookAsync(session, PlayLaterCapitalStepAsync)) return;
					if (NaturalAltgardQuestSteps.State(session.Api.World, 2919) is (3, 4))
					{
						bookCollectionUnderway = true;
						await new NaturalLaterCapitalBookTravel(session, runtime,
							id => session.Api.World.MapId == capitalContract.MapId ? ApproachCapitalNpcAsync(id) : ApproachShippedSpawnAsync(id),
							EnterBookMap, async (id, collection) =>
							{
								try { return await KillShippedSpawnAsync(id, collection); }
								catch (NaturalBookCollectionMapChangedException) { return 0; }
							}, async id => { await TryLootCorpseItemAsync(session, id, 182207011, token); })
							.CollectAmphaAndReturnAsync(token);
					}
				}
				finally
				{
					bookCollectionUnderway = false;
					contract = savedContract;
					navigator = savedNavigator;
					geometry = savedGeometry;
					farApproach = savedFarApproach;
					combat.AfterBindRevive = savedRevive;
					combat.EnterMap(navigator, geometry, contract.MapId);
				}
			}

			async Task RunCapitalPassAsync()
			{
				capitalBefore ??= NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true, line: ClassLine);
				while (++capitalSequences <= 80)
				{
					await session.SynchronizeAsync(token);
					NaturalCapitalDecision next = NaturalCapitalDecisionEngine.Decide(capitalContract,
						NaturalAscensionObservation.Observe(session.Api.World, false), ClassLine);
					session.BeginStep(next.Step?.Key ?? $"pc-{next.Action}", next.Action);
					session.TraceDiagnostic("capital-decision", new Dictionary<string, object?>
					{ ["sequence"] = capitalSequences, ["decision"] = next, ["map"] = session.Api.World.MapId });
					session.PublishDashboard();
					try
					{
						switch (next.Action)
						{
							case "complete": VerifyCapitalPass(); return;
							case "recover": await RestSafelyAsync(token); break;
							case "travel-city": await TakeCeremonyTeleporterAsync(toIshalgen: false); break;
							case "observe": await session.AdvanceAsync(TimeSpan.FromMilliseconds(100), token); break;
							case "read-book":
								await NaturalCapitalSteps.ReadBookAsync(session, runtime.Data.ItemDataDh.GetItemTemplate(182212217), token);
								break;
							case "portal":
								await NaturalCapitalSteps.PortalAsync(session, next.Portal!, await ApproachCapitalNpcAsync(next.Portal!.NpcId), token);
								break;
							case "talk":
								NaturalAltgardStep step = next.Step!;
								long? xp = null;
								long kinah = 0;
								try
								{
									for (int attempt = 1; ; attempt++)
									{
										try
										{
											int npc = await ApproachCapitalNpcAsync(step.NpcId);
											xp = ObservedExperience();
											kinah = session.Api.World.Kinah;
											string outcome = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
											session.TraceDiagnostic("capital-talk", new Dictionary<string, object?> { ["outcome"] = outcome });
											break;
										}
										catch (NaturalDialogTooFarException) when (attempt < 3) { }
									}
								}
								finally
								{
									// Even an injected disconnect on closing the reward dialog keeps the observed payment.
									if (xp != null && session.Api.World.CompletedQuestIds.Contains(step.QuestId) && !capitalPayments.ContainsKey(step.QuestId))
									{
										var payment = new CapitalPayment(step.QuestId, ObservedExperience() - xp.Value, session.Api.World.Kinah - kinah);
										capitalPayments.Add(step.QuestId, payment);
										session.TraceDiagnostic("capital-payment", new Dictionary<string, object?> { ["payment"] = payment });
									}
								}
								await EquipUpgradesAsync(token);
								break;
							default: throw new InvalidDataException(next.Reason);
						}
						if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					}
					catch (Exception) when (session.Api.World.IsDead && !token.IsCancellationRequested)
					{
						await RestSafelyAsync(token); // Record/recover the outcome; keep the shared counters and clock.
					}
				}
				throw new TimeoutException("The capital pass exceeded 80 decisions across recovery attempts.");
			}

			void VerifyCapitalPass()
			{
				Require.NotNull(capitalBefore);
				BotWorldModel world = session.Api.World;
				Require.All(capitalContract.CompletedQuestIds, id => Require.Contains(id, world.CompletedQuestIds));
				Require.All(capitalBefore.CompletedQuestIds, id => Require.Contains(id, world.CompletedQuestIds));
				Require.All(capitalBefore.Quests.Where(q => contract.Quests.Any(entry => entry.Id == q.QuestId) && q.Status is 3 or 4), old =>
					Require.True(world.Quests.TryGetValue(old.QuestId, out BotQuestState? current) &&
						current.Status == old.Status && current.StepAndFlags == old.StepAndFlags, $"Capital pass changed retained Ishalgen Q{old.QuestId}."));
				Require.True(AtQuestStep(capitalContract.DispatchQuestId, 0), "The capital pass advanced Q2904.");
				// NR-52: the weapon the line took at the ceremony; the Karmic Staff for the accepted line.
				int ceremonyWeapon = LineBridge().CeremonyReward.ItemId;
				NaturalJourneyItem staff = capitalBefore.Inventory.Single(item => item.ItemId == ceremonyWeapon && item.EquipmentSlot > 0);
				Require.True(world.Inventory.TryGetValue(staff.ObjectId, out BotInventoryItem? stillWorn) &&
					stillWorn.ItemId == staff.ItemId && stillWorn.Details.EquippedSlot is > 0, "The capital pass replaced the ceremony staff.");
				Require.True(capitalBefore.BindPoint == null || world.ObeliskBindPoint is { } bound &&
					bound.MapId == capitalBefore.BindPoint.MapId && Distance(bound.Position, capitalBefore.BindPoint.Position) < .1f,
					"The capital pass changed the bind point.");
				Require.All(world.Skills.Values.Where(skill => skill.SkillType is 1 or 3), skill =>
					Require.Contains(skill.SkillId, capitalBefore.Skills.Where(old => old.SkillType is 1 or 3).Select(old => old.SkillId)));
				NaturalCapitalQuest[] paid = capitalContract.Quests.Where(q => !capitalBefore.CompletedQuestIds.Contains(q.Id)).ToArray();
				Require.True(paid.Select(q => q.Id).Order().SequenceEqual(capitalPayments.Keys.Order()), "A capital reward payment was not observed.");
				Require.All(paid, quest =>
				{
					Require.Equal((long)quest.Experience, capitalPayments[quest.Id].Experience);
					Require.Equal(quest.Kinah, capitalPayments[quest.Id].Kinah);
				});
				Require.All(new[] { 122000870, 188508000, 169600066, 169600084, 169600085, 190000055 }, id => Require.True(ItemCount(world, id) > 0,
					$"Optional capital reward {id} was not retained."));
				Require.True(ItemCount(world, 164000074) >= 2, "The Lost Love running scrolls were not retained.");
				Require.True(ItemCount(world, 182207039) == 0 && ItemCount(world, 182212217) == 0,
					"The supply request or manual was not consumed at its proper hand-in.");
			}

			async Task CompleteCapitalCheckpointAsync(string stage)
			{
				if (relogAt != null) Require.True(relogInjected, "Requested capital interruption was not exercised.");
				if (stage == "first") { VerifyCapitalPass(); Require.Equal(contract.MapId, session.Api.World.MapId!.Value); }
				else Require.True(session.Api.World.Level == 10 && session.Api.World.MapId == capitalContract.MapId &&
					AtQuestStep(capitalContract.DispatchQuestId, 0), "The post-ceremony capital start is outside its contract.");
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true, line: ClassLine);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true, line: ClassLine);
				NaturalJourneyPersistence.Verify(before, after);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "capital-stage-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						Stage = stage, before, after, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis,
						CapitalBefore = capitalBefore, Payments = capitalPayments.Values.OrderBy(p => p.QuestId).ToArray(),
						TransportFares = capitalFares, Deaths = combat.ReviveCount, Retreats = combat.CompletedRetreats,
						Reconnects = reconnects, StigmaSkills = after.Skills.Where(skill => skill.SkillType is 1 or 3).ToArray(),
						RegularSkills = after.Skills.Where(skill => skill.SkillType == 0).ToArray(),
					}), token);
				session.PublishDashboard("capital-checkpoint-complete", force: true);
			}

			async Task RunEarlyAscensionAsync()
			{
				earlyAscensionUnderway = true;
				try
				{
					NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true, line: ClassLine);
					session.TraceDiagnostic("early-ascension-start", new Dictionary<string, object?>
					{ ["level"] = before.Level, ["completed"] = before.CompletedQuestIds, ["position"] = before.Position });
					if (!session.Api.World.CompletedQuestIds.Contains(2009)) await RunAscensionBridgeAsync(ceremonyOnly: true);
					// CP-27, NR-31: the early ceremony ends as the line's second class, and the legs after it ask for the same class.
					Require.True(session.Api.World.Level >= 10 && combat.IsLineSecondClass &&
						session.Api.World.CompletedQuestIds.IsSupersetOf(new[] { 2008, 2009 }), "The early ceremony did not complete.");
					if (options.CapitalStage == "start")
					{
						Require.True(IshalgenPending() && session.Api.World.MapId == capitalContract.MapId &&
							capitalContract.CompletedQuestIds.All(id => !session.Api.World.CompletedQuestIds.Contains(id)),
							"The capital start must retain unfinished Ishalgen and precede every first-pass completion.");
						throw new NaturalCapitalCheckpointStopException("start");
					}
					long capitalStartExperience = ObservedExperience();
					await RunCapitalPassAsync();
					if (continuousAltgard && laterCapital != null)
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "capital-pass-completion.json"),
							System.Text.Json.JsonSerializer.Serialize(new { verified = true, session.CharacterId, before = capitalBefore,
								after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId, session.ConnectionGeneration,
									contract, session.CurrentPosition, earlyAscension: true, line: ClassLine),
								StartingExperience = capitalStartExperience, Experience = ObservedExperience(),
								Payments = capitalPayments.Values.OrderBy(p => p.QuestId).ToArray(), TransportFares = capitalFares }), token);
					if (session.Api.World.MapId == 120010000) await TakeCeremonyTeleporterAsync(toIshalgen: true);
					Require.Equal(contract.MapId, session.Api.World.MapId!.Value);
					geometry = runtime.CreateGeometry();
					combat.EnterMap(navigator, geometry, contract.MapId);
					navigator.UnavailableObjects.Clear();
					quietNeighbours.Clear();
					fightRouteOpen = false;
					fightRouteOpenAt = null;
					await TopUpHelpItemsAsync("level-up");
					await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
					NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
						session.ConnectionGeneration, contract, session.CurrentPosition, earlyAscension: true, line: ClassLine);
					Require.All(before.CompletedQuestIds, id => Require.Contains(id, after.CompletedQuestIds));
					Require.Equal(3, session.Api.World.Quests[LineBridge().Dispatch.QuestId].Status);
					Require.Equal(0, QuestVar(LineBridge().Dispatch.QuestId));
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "early-ascension-completion.json"),
						System.Text.Json.JsonSerializer.Serialize(new { before, after, verified = true, session.CharacterId,
							ElapsedMillis = runtime.NowMillis, Deaths = combat.ReviveCount }), token);
					if (options.CapitalStage == "first") throw new NaturalCapitalCheckpointStopException("first");
				}
				finally { earlyAscensionUnderway = false; }
			}

			async Task TakeCeremonyTeleporterAsync(bool toIshalgen)
			{
				NaturalAscensionContract bridge = LineBridge();
				int npc;
				if (toIshalgen)
				{
					session.BeginStep("early-ascension-return", "doman-teleporter-back-to-unfinished-ishalgen");
					NaturalAscensionStep doman = bridge.Step(NaturalAscensionStepRole.DispatchStart);
					NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
					npc = await ApproachBridgeNpcAsync(doman, new(doman.Position[0], doman.Position[1], doman.Position[2], 0), here);
				}
				else
				{
					session.BeginStep("early-ascension-dispatch", "ishalgen-teleporter-to-pandaemonium-after-all-ishalgen-quests");
					BotPosition anchor = graph.GetMap(contract.MapId)!.Waypoints.First(waypoint => waypoint.TemplateId == 203679).Position;
					await UseFasterTravelAsync(anchor, forceHubFlight: true);
					npc = await ApproachShippedSpawnAsync(203679);
				}
				long fareBefore = session.Api.World.Kinah;
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(npc,
					session.Api.World.Objects[npc].Position, 5, toIshalgen ? 8 : 7, 100,
					toIshalgen ? contract.MapId : 120010000, token);
				Require.True(result.IsDone, result.Reason);
				if (capitalBefore != null) capitalFares += fareBefore - session.Api.World.Kinah;
				mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
			}

			// NA-13: Q2008's trial in Ataxiar. Its NPCs deal 1 damage (AscensationNpcAI) and the instance has no exit, so the
			// Priest fights as if cornered: no swarm retreat, heals and potions still on. One kill per decision.
			async Task FightAscensionTrialAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-q2008-trial", "scripted-ascension-trial");
				var trial = bridge.Instance.Trial.Select(group => group.NpcId).ToHashSet();
				int? target = null;
				for (int wait = 0; wait < 30 && target == null; wait++)
				{
					target = session.Api.World.Objects.Values
						.Where(npc => npc.Kind == BotKnownObjectKind.Npc && npc.TemplateId is int id && trial.Contains(id))
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).Select(npc => (int?)npc.ObjectId).FirstOrDefault();
					if (target == null)
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
						await session.SynchronizeAsync(token);
					}
				}
				Require.True(target != null, "No trial opponent became visible in Ataxiar.");
				combat.ScriptedTrial = true;
				// NR-52: the journey's fight was made for Ishalgen. A class that walks to its opponent walks by this map's
				// navigator and geometry; the Priest casts from where it stands and never asks for them.
				combat.EnterObservedMap = () =>
				{
					NaturalMapKey key = NaturalMapKey.Observe(session.Api.World);
					combat.EnterMap(mapNavigators.Enter(key, newEntry: false), runtime.CreateGeometry(), key.MapId);
				};
				try
				{
					bool killed = await combat.TryKillAsync(target!.Value, token, retreatAnchor: session.CurrentPosition);
					session.TraceDiagnostic("ascension-trial-kill", new Dictionary<string, object?>
					{
						["target"] = target, ["killed"] = killed, ["hp"] = session.Api.World.CurrentHp,
						["quest"] = session.Api.World.Quests.TryGetValue(2008, out BotQuestState? q) ? q.StepAndFlags : null,
					});
				}
				finally
				{
					combat.ScriptedTrial = false;
					combat.EnterObservedMap = null;
				}
			}

			// NA-21: the stock check for the approved help items (OD-13): the Cleric from level 10 on and (CP-06, CP-Q12)
			// every character below level 10, and only when the runtime may supply (SIM, or the isolated LIVE stack's
			// director). Every supply is checked from the client's inventory, traced, and listed in help-items.json beside
			// the run's other evidence.
			async Task TopUpHelpItemsAsync(string trigger, bool full = false, Func<bool>? safe = null)
			{
				BotWorldModel world = session.Api.World;
				helpCheckedAtLevel = world.Level;
				NaturalHelpItemRules help = combat.ClassProfile.HelpItems;
				if (runtime.SupplyHelpItemAsync is not { } supply || !help.Supplied(world.Level) || world.IsDead) return;
				var owned = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				IReadOnlyList<NaturalHelpTopUp> plan = NaturalHelpItemSupply.Plan(world.Level, owned, help.Kit, full);
				foreach (NaturalHelpTopUp topUp in plan)
				{
					if (safe != null && !safe()) break;
					NaturalHelpItemSupply.RequireApproved(topUp.ItemId, topUp.Count);
					await supply(topUp.ItemId, topUp.Count, token);
					await session.SynchronizeAsync(token);
					long after = ItemCount(world, topUp.ItemId);
					Require.True(after >= topUp.Owned + topUp.Count, $"Help item {topUp.ItemId} did not arrive: {topUp.Owned} -> {after}.");
					helpSupplied.Add(new NaturalHelpSupplied(trigger, topUp.ItemId, topUp.Family, topUp.Count, topUp.Owned, after,
						world.Level, runtime.NowMillis));
					session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
					{
						["trigger"] = trigger, ["itemId"] = topUp.ItemId, ["family"] = topUp.Family, ["count"] = topUp.Count,
						["before"] = topUp.Owned, ["after"] = after, ["level"] = world.Level,
					});
				}
				if (plan.Count > 0)
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "help-items.json"),
						NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
				if (safe == null || safe()) await CleanBagsAsync($"restock-{trigger}", token);
			}

			// NR-R17b: a character that logs in above the ground falls to it, as a client does (Java CM_MOVE's FALL flag and
			// PlayerMoveController.stopFalling, which deals the fall's damage). A bot that stopped is not logged out, so its
			// snapshot holds the place the server last saved for it, and that can be in the air of a flight. Such a place is
			// off the navmesh, with static ground under it.
			async Task FallFromLoginPlaceAsync()
			{
				BotPosition hung = session.CurrentPosition;
				if (session.Api.World.MapId is not int map || geometry.SnapToGround(map, hung) != null ||
					geometry.StaticGroundBelow(map, hung, 60) is not { } below || hung.Z - below.Z <= 2) return;
				session.TraceDiagnostic("login-fall", new Dictionary<string, object?>
				{
					["from"] = hung, ["ground"] = below, ["metres"] = hung.Z - below.Z, ["hp"] = session.Api.World.CurrentHp,
				});
				await NaturalFlightProtocol.FallAsync(session, hung, below.Z, token);
				await session.SynchronizeAsync(token);
			}

			async Task RunAllAltgardLegsAsync()
			{
				int characterId = session.CharacterId;
				var stages = new List<object>();
				stages.Add(new { Stage = "ishalgen-ascension", CharacterId = characterId, ElapsedMillis = runtime.NowMillis,
					Deaths = combat.ReviveCount, Completed = session.Api.World.CompletedQuestIds.Count,
					Level = session.Api.World.Level, Experience = ObservedExperience(), Kinah = session.Api.World.Kinah });
				foreach (string id in NaturalAltgardContinuation.Order)
				{
					await session.SynchronizeAsync(token);
					Require.Equal(characterId, session.CharacterId);
					altgardLegId = id;
					session.IdentityAltgardLegId = id;
					NaturalAltgardContract shipped = NaturalAltgardContract.LoadLeg(id).Without(ClassLine.LeftOutQuestIds);
					altgardLeg = NaturalAltgardContinuation.BindIncoming(WithObservedClassRewards(shipped), session.Api.World.CompletedQuestIds,
						session.Api.World.Inventory.Values.Select(i => new NaturalJourneyItem(i.ObjectId, i.ItemId, i.Count, i.EquipmentSlot)).ToArray(),
						NaturalAltgardContinuation.EquippedItemIds(session.Api.World), OfContractClass(shipped));
					altgardPlans = NaturalAltgardContract.LoadPlans(id);
					collectionLimits = altgardPlans.Values.SelectMany(plan => plan.Steps
						.Where(step => step.Kind == "collect" && step.ItemId > 0 && step.Count > 0)
						.Select(step => (plan.Id, step.ItemId, step.Count)))
						.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.ToArray());
					coinGearProgress = altgardLeg.CoinGear == null ? null : NaturalCoinGearProgress.Empty;
					coinIncomingLoadout = [];
					destinyIncomingSkills = altgardLeg.Destiny is { } destiny
						? session.Api.World.Skills.Keys.Where(skill => skill != destiny.StigmaSkillId).ToArray() : [];
					haramelProgress = altgardLeg.Haramel is { } rules
						? NaturalHaramelProgress.Begin(characterId, HaramelNow(), session.Api.World, rules) with { Revives = combat.ReviveCount }
						: null;
					haramelApproach = null;
					haramelKind = null;
					long started = runtime.NowMillis;
					int deathsBefore = combat.ReviveCount;
					ushort startedLevel = session.Api.World.Level;
					long startedExperience = ObservedExperience(), startedKinah = session.Api.World.Kinah;
					session.TraceDiagnostic("continuous-leg-start", new Dictionary<string, object?>
					{ ["leg"] = id, ["characterId"] = characterId, ["elapsedMillis"] = started, ["deaths"] = deathsBefore });
					Console.WriteLine($"Continuous SIM: starting {id}, level {session.Api.World.Level}, {session.Api.World.CompletedQuestIds.Count} completions.");
					await RunAltgardLeg1Async();
					Require.All(altgardLeg.Start.CompletedQuestIds, quest => Require.Contains(quest, session.Api.World.CompletedQuestIds));
					stages.Add(new { Stage = id, CharacterId = characterId, StartedMillis = started, ElapsedMillis = runtime.NowMillis,
						Deaths = combat.ReviveCount, NewDeaths = combat.ReviveCount - deathsBefore, Completed = session.Api.World.CompletedQuestIds.Count,
						StartedLevel = startedLevel, StartedExperience = startedExperience, StartedKinah = startedKinah,
						Level = session.Api.World.Level, Experience = ObservedExperience(), Kinah = session.Api.World.Kinah });
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "continuous-progress.json"),
						System.Text.Json.JsonSerializer.Serialize(new { CharacterId = characterId, stages }), token);
				}
				Require.Equal(1, session.Api.World.CompletedQuestCounts.GetValueOrDefault(2217));
				if (laterCapital != null)
					Require.All(laterCapital.CompletedQuestIds, id => Require.Equal(1, session.Api.World.CompletedQuestCounts.GetValueOrDefault(id)));
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "continuous-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new { verified = true, CreatedCharacter = true, CharacterId = characterId,
						ElapsedMillis = runtime.NowMillis, Deaths = combat.ReviveCount, stages, LaterCapital = laterCapital != null,
						Experience = ObservedExperience(), ExperienceToNextLevel = runtime.Data.PlayerExperienceTable.GetStartExpForLevel(session.Api.World.Level + 1) - ObservedExperience(),
						ExperienceToLevel32 = Math.Max(0, runtime.Data.PlayerExperienceTable.GetStartExpForLevel(32) - ObservedExperience()),
						Endpoint = NaturalJourneyCheckpoint.Capture(session.Api.World, characterId, session.ConnectionGeneration,
							contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine) }), token);
			}

			// AF-08: Altgard Leg 1 (docs/natural-altgard-leveling.md), from the `altgard` snapshot to the fortress endpoint.
			// The journey is rebound to Altgard as for NA-23; the Leg 1 engine picks each move from the client's view. Template
			// quests run on the Ishalgen runner, scripted steps on NaturalAltgardQuestSteps, flight and the air kills on the
			// AF-04..AF-06 code. Every decision is traced; a move that makes no progress three times stops the run.
			// AX-03..AX-12: the Morheim and Abyss-entry leg. It proves its incoming contract from the client's view, then takes one
			// decision of NaturalAbyssEntryDecisionEngine at a time. The fortresses and the capital are safe hubs: every approach is
			// the city approach, on whichever map the client is on. The segment ends at the rule's frontier.
			async Task RunAbyssEntryAsync()
			{
				NaturalAltgardContract leg = altgardLeg ?? throw new InvalidOperationException("The Abyss-entry leg needs its contract.");
				NaturalAbyssEntry scope = leg.AbyssEntry ?? throw new InvalidOperationException("The Abyss-entry leg needs its scope.");
				BotWorldModel world = session.Api.World;
				string folder = Path.GetDirectoryName(combatTracePath)!;
				var services = new NaturalServiceSteps(session);
				var weaponNumbers = new Dictionary<int, int>();
				Require.True(combat.IsLineSecondClass, $"The Abyss-entry leg needs the {ClassLine.SecondName}; the character is {combat.ObservedCharacter}.");
				NaturalAltgardObservation Observed() => NaturalAltgardObservation.Observe(world, session.CurrentPosition);
				// AX-13: quit, log back in, and require that the character survived as it was. A relog gives the session a new world
				// model, so everything after it reads session.Api.World.
				async Task<(NaturalJourneyCheckpoint Before, NaturalJourneyCheckpoint After)> RelogAtEndpointAsync()
				{
					NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId, session.ConnectionGeneration, contract,
						session.CurrentPosition, earlyAscension: options.AscensionBridge, line: ClassLine);
					session.BeforeSend = null;
					await session.QuitAsync(token);
					await session.WaitForReentryAsync(token);
					await session.ReloginExistingCharacterAsync(token);
					await session.EnterWorldAsync(token);
					await session.SynchronizeAsync(token);
					NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId, session.ConnectionGeneration, contract,
						session.CurrentPosition, earlyAscension: options.AscensionBridge, line: ClassLine);
					NaturalJourneyPersistence.Verify(before, after);
					if (laterCapital != null)
						await laterCapital.WriteCheckpointAsync(folder, leg.Leg, before, after, token, distinctSegment: continuousAltgard);
					return (before, after);
				}
				NaturalAbyssEntryEndpoint ObservedEndpoint() => NaturalAbyssEntryLeg.VerifyEndpoint(leg,
					NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition), session.CharacterId, StaffMagicBoost, PhysicalDefence);

				// The muster (the operator, 2026-10-12; docs/natural-muster.md): what a character that stands at the Abyss
				// entry's end still needs to be ready for the legs after it, played in the one world that holds every
				// character. Its level: the fortress's repeatable coin quest, until the leg's level is reached ("just do the
				// skipped repeatable coin quest that is given in Morheim fortress, until they reach level 26"). Its gear: the
				// best coin tier it can wear, with the weapon, the shield and the second weapon, from coins supplied without
				// a limit. Its bags: the containers opened and the gear its class rules call surplus sold. Its kit: every
				// row at its full count. Then a relog, and the receipt.
				async Task RunMusterAsync()
				{
					session.BeginStep("muster-start", "take-up-the-character-at-the-abyss-entry-end");
					// The shipped leg's level; a class's own copy of the leg holds the level it started the leg at (NR-56b).
					// MR-i: a check-in is for a character that has reached its level; its tier is that level's Silver Coin gear,
					// the class's own from the fortress's vendors of that coin.
					int? checkIn = options.CheckInLevel;
					Require.True(checkIn is not { } checkInLevel || NaturalAbyssEntry.SilverCoinCheckInLevels.Contains(checkInLevel) && world.Level >= checkInLevel,
						$"A check-in at level {checkIn} is for a character of that level in a Silver Coin tier; this one is level {world.Level}.");
					int minimum = checkIn ?? NaturalAltgardContract.LoadLeg(leg.Leg).AbyssEntry!.Level.Minimum;
					QuestRunPlan coinQuest = altgardPlans.Values.Single();
					NaturalAbyssCoinArmor armor = checkIn is { } tierLevel ? (scope.CoinArmor with
					{
						CoinItemId = NaturalAbyssEntry.SilverCoinItemId, IncomingCoins = 0, VendorNpcId = NaturalAbyssEntry.SilverCoinVendorNpcId,
						VendorPosition = NaturalAbyssEntry.SilverCoinVendorPosition, Tiers = [new(tierLevel, "check-in", $"the level-{tierLevel} coin gear", [])],
					}).ForClass(runtime.Data, NaturalAbyssEntry.Morheim, combat.ClassProfile.Gear,
						ids => NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, ids, ClassLine).Item, combat.ObservedCharacter)
						: scope.CoinArmor;
					BotPosition hubAt = new(leg.Hub.Anchor[0], leg.Hub.Anchor[1], leg.Hub.Anchor[2], 0);
					Require.True(world.MapId == leg.Hub.MapId && !world.IsDead && Distance(session.CurrentPosition, hubAt) <= leg.Hub.Radius,
						"The muster starts in the fortress the Abyss entry ends in.");
					int startLevel = world.Level, startDeaths = combat.ReviveCount;
					long startExperience = ObservedExperience(), startKinah = world.Kinah, startCoins = ItemCount(world, armor.CoinItemId),
						startedMillis = runtime.NowMillis;
					int[] Worn() => [.. session.Api.World.Inventory.Values.Where(item => (item.Details.EquippedSlot ?? 0) > 0).Select(item => item.ItemId).Order()];
					int[] wornAtStart = Worn();
					int stacksAtStart = world.Inventory.Count;
					var turns = new List<Dictionary<string, object?>>();
					var manifests = new List<NaturalAbyssCoinManifest>();
					var purchases = new List<NaturalAbyssCoinPurchase>();
					var openedNow = new List<NaturalOpenedContainer>();
					var notOpenedNow = new List<int>();
					var sold = new List<NaturalSale>();
					long coinsSuppliedNow = 0, saleKinah = 0;
					session.TraceDiagnostic("muster-start", new Dictionary<string, object?>
					{
						["class"] = combat.ObservedCharacter, ["level"] = startLevel, ["minimum"] = minimum, ["experience"] = startExperience,
						["kinah"] = startKinah, ["bronzeCoins"] = startCoins, ["worn"] = wornAtStart, ["stacks"] = stacksAtStart,
						["position"] = session.CurrentPosition,
					});
					// The hunts are in the open field: walk around what would attack, and defend on the way.
					void EnterFortressMap()
					{
						EnterObservedMap();
						navigator.AvoidHostileAggro = true;
						navigationDefense = combat;
					}
					await FallFromLoginPlaceAsync();
					EnterFortressMap();
					combat.AfterBindRevive = EnterFortressMap;
					// The legs behind the muster are over: what they protected and what the Abyss entry kept sealed is of use
					// only if a rule uses it (MR-f). The muster itself needs its coins and the containers it is about to open.
					NaturalAbyssInventory rules = scope.Inventory with { Open = [.. scope.Inventory.Open, .. NaturalAbyssEntry.MusterOpen], KeepSealed = [] };
					bagNeeds = [armor.CoinItemId, .. rules.Open];
					// MR-g: the bundle of Q2900 and the stone taken from it are kept until the stone is in its slot.
					if (NaturalStigma.Choices.TryGetValue(combat.ClassProfile.Class, out NaturalStigma.Choice? ownStone))
						bagNeeds.UnionWith([NaturalStigma.BundleItemId, ownStone.StoneItemId]);
					if (NaturalStigma.Seconds.TryGetValue(combat.ClassProfile.Class, out NaturalStigma.Second? secondStone))
						bagNeeds.Add(secondStone.StoneItemId);
					combat.UsesBoosts = true;
					async Task CheckAsync(string trigger)
					{
						session.BeginStep("muster-inventory-check", trigger);
						NaturalInventoryCheckResult checkedNow = await NaturalInventoryCheck.RunAsync(session, trigger, rules, EquipUpgradesAsync,
							runtime.Data.ItemDataDh.GetItemTemplate,
							() => NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
								.Decide(world, QuestNeededItems()).FreeSlots, token);
						openedNow.AddRange(checkedNow.Opened);
						notOpenedNow.AddRange(checkedNow.NotOpened);
					}
					async Task<int> ApproachInFortressAsync(int npcId)
					{
						// From the field the way home is the Return every class has: it lands at the bind, inside the fortress.
						if (Distance(session.CurrentPosition, hubAt) > leg.Hub.Radius)
						{
							await UseLearnedReturnToBindAsync();
							await session.SynchronizeAsync(token);
							await RestSafelyAsync(token);
						}
						return await ApproachCapitalNpcAsync(npcId);
					}
					// The bags: what no rule uses (MR-f) is sold to the fortress's nearest merchant, as far as a merchant takes it:
					// the gear and accessories the character has outgrown, the reward weapons the legs behind it protected, and
					// what it cannot use. It is done first, before anything is supplied or opened: a character can arrive with a
					// full cube (the Spirit Master, the Bard), and the cleanup, which runs at every gear check and restock, would
					// throw away what a merchant three steps off pays for. It is done again after the coin gear, for the pieces
					// that gear replaced.
					async Task SellSurplusAsync(string when)
					{
						session.BeginStep("muster-sale", when);
						// What the cleanup would discard, as far as a merchant takes it: the client knows from the item's flags.
						NaturalIshalgenInventoryPolicy catalog = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine);
						NaturalBagPlan bag = BagPlan();
						List<NaturalSale> sales = [.. bag.Discards.Where(discard => world.Inventory.TryGetValue(discard.ObjectId, out BotInventoryItem? stack) &&
								(stack.ItemMask & 4) != 0 && catalog.Item(discard.ItemId).Sellable)
							.Select(discard => new NaturalSale(discard.ObjectId, discard.ItemId, discard.Count))];
						session.TraceDiagnostic("muster-sale-plan", new Dictionary<string, object?>
						{
							["when"] = when, ["sell"] = sales.Select(sale => sale.ItemId).ToArray(),
							["discardLater"] = bag.Discards.Where(discard => sales.All(sale => sale.ObjectId != discard.ObjectId))
								.Select(discard => $"{discard.ItemId}:{discard.Reason}").ToArray(),
							["kept"] = bag.Kept.GroupBy(stack => stack.Reason).ToDictionary(group => group.Key, group => group.Select(stack => stack.ItemId).ToArray()),
						});
						if (sales.Count == 0) return;
						int merchantId = runtime.Data.SpawnsDh.GetSpawnsByWorldId(leg.Hub.MapId)
							.Where(group => runtime.Data.TradeListDataDh.GetTradeListTemplate(group.GetNpcId()) is { } list &&
								list.GetTradeNpcType() == Aion.GameServer.Model.Templates.Tradelist.TradeNpcType.NORMAL)
							.SelectMany(group => group.GetSpawnTemplates().Select(spot => (Npc: group.GetNpcId(), At: new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))))
							.Where(spot => Distance(spot.At, hubAt) <= leg.Hub.Radius)
							.OrderBy(spot => Distance(spot.At, session.CurrentPosition)).ThenBy(spot => spot.Npc).Select(spot => spot.Npc).First();
						int merchant = await ApproachInFortressAsync(merchantId);
						long before = world.Kinah;
						NaturalVendorResult trade = await services.TradeAsync(merchant, sales, [],
							id => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList() ?? [],
							id => runtime.Data.ItemDataDh.GetItemTemplate(id).GetPrice(), token);
						sold.AddRange(trade.Sold);
						saleKinah += world.Kinah - before;
						session.TraceDiagnostic("muster-sale", new Dictionary<string, object?>
						{
							["when"] = when, ["merchant"] = merchantId, ["sold"] = trade.Sold.Select(sale => sale.ItemId).ToArray(),
							["notSold"] = sales.Except(trade.Sold).Select(sale => sale.ItemId).ToArray(), ["kinah"] = world.Kinah - before,
						});
					}
					await SellSurplusAsync("sell-what-is-not-needed");
					await TopUpHelpItemsAsync("run-start");
					await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
					await CheckAsync("muster-start");

					// 1. The level. The world keeps a completed quest in its completed list for good, so a repeatable quest is
					// read from its journal entry: 3 while it is worked, 4 on Nott's reward page, 5 once it is paid.
					QuestRunBook book = QuestRunBook.Build(coinQuest);
					int Live() => world.Quests.TryGetValue(coinQuest.Id, out BotQuestState? entry) ? entry.Status : 0;
					int Remaining(QuestRunOperation kill)
					{
						if (!world.Quests.TryGetValue(coinQuest.Id, out BotQuestState? entry) || entry.Status != 3) return 0;
						QuestRunStep step = coinQuest.Steps.Single(counted => counted.Kind == "kill" && counted.Sequence == kill.Sequence);
						return Math.Max(0, kill.Count - NaturalQuestProgress.KillCount(entry, step.Data.GetProperty("var").GetInt32(), kill.Count));
					}
					for (int turn = 1; world.Level < minimum; turn++)
					{
						Require.True(turn <= NaturalAbyssEntry.MusterMaxTurns,
							$"The muster did not reach level {minimum} in {NaturalAbyssEntry.MusterMaxTurns} turns of Q{coinQuest.Id}.");
						long xp = ObservedExperience(), coinsBefore = ItemCount(world, armor.CoinItemId), began = runtime.NowMillis;
						int deaths = combat.ReviveCount, levelBefore = world.Level;
						foreach (QuestRunOperation operation in book.Operations)
						{
							session.BeginStep($"muster-q{coinQuest.Id}-turn-{turn}-{operation.Kind}", $"repeatable-coin-quest-{operation.Kind}");
							switch (operation.Kind)
							{
								case QuestRunOperationKind.Prepare:
								case QuestRunOperationKind.Report:
									break;
								case QuestRunOperationKind.StartAtNpc:
								{
									if (Live() is 3 or 4) break;
									int giverId = operation.Npcs?.FirstOrDefault()?.Id ?? throw new InvalidDataException($"Q{coinQuest.Id} has no shipped NPC starter.");
									int giver = await ApproachInFortressAsync(giverId);
									for (int attempt = 1; ; attempt++)
									{
										try { await session.StartQuestAsync(giver, coinQuest.Id, token); break; }
										catch (NaturalDialogTooFarException) when (attempt < 3) { giver = await ApproachInFortressAsync(giverId); }
									}
									Require.Equal(3, Live());
									break;
								}
								case QuestRunOperationKind.Kill:
								{
									// Quest data lists kinds that have no spawn on the map: hunt what is there, the nearest kind first.
									int[] kinds = [.. (operation.Npcs?.Select(npc => npc.Id).Distinct() ?? []).Where(SpawnsOnMap)];
									if (kinds.Length == 0) throw new InvalidDataException($"Q{coinQuest.Id} kill has no shipped target.");
									while (Remaining(operation) > 0)
									{
										int target = await KillShippedSpawnAsync(NearestKind(kinds), objectiveDone: () => Remaining(operation) == 0);
										if (target != 0) navigator.UnavailableObjects.Add(target);
										await RestSafelyAsync(token);
									}
									break;
								}
								case QuestRunOperationKind.ClaimReward:
								{
									// Java MonsterHunt: with every kill counted the quest is still worked (3); Nott's reward page turns it (4).
									Require.True(Live() is 3 or 4 && book.Operations.Where(kill => kill.Kind == QuestRunOperationKind.Kill).All(kill => Remaining(kill) == 0),
										$"Q{coinQuest.Id} is not ready to hand in.");
									int payerId = operation.Npcs?.FirstOrDefault()?.Id ?? throw new InvalidDataException($"Q{coinQuest.Id} has no shipped reward NPC.");
									int payer = await ApproachInFortressAsync(payerId);
									for (int attempt = 1; ; attempt++)
									{
										try { await FinishStandardQuestAsync(session, payer, coinQuest.Id, token); break; }
										catch (NaturalDialogTooFarException) when (attempt < 3) { payer = await ApproachInFortressAsync(payerId); }
									}
									Require.Equal(5, Live());
									break;
								}
								default:
									throw new InvalidDataException($"Q{coinQuest.Id} needs natural {operation.Kind} capability.");
							}
						}
						await CheckAsync($"turn-in-q{coinQuest.Id}");
						if (world.Level != helpCheckedAtLevel && !world.IsDead) await TopUpHelpItemsAsync("level-up");
						var row = new Dictionary<string, object?>
						{
							["turn"] = turn, ["quest"] = coinQuest.Id, ["experience"] = ObservedExperience() - xp, ["levelBefore"] = levelBefore,
							["level"] = (int)world.Level, ["bronzeCoins"] = ItemCount(world, armor.CoinItemId) - coinsBefore,
							["deaths"] = combat.ReviveCount - deaths, ["gameMillis"] = runtime.NowMillis - began,
						};
						turns.Add(row);
						session.TraceDiagnostic("muster-coin-quest-turn", row);
					}

					// 2. The coin gear: the best tier the character can wear. A piece is bought when it beats what is worn, and
					// the coins the manifest is short are supplied, whatever that comes to.
					session.BeginStep("muster-coin-gear", "buy-the-best-coin-tier");
					NaturalAbyssCoinTier tier = armor.Tiers.Where(entry => entry.Level <= world.Level).MaxBy(entry => entry.Level)
						?? throw new InvalidDataException($"No coin tier is for level {world.Level}.");
					NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(armor, tier, Observed().Inventory!, PhysicalDefence, StaffMagicBoost, ItemRank);
					manifests.Add(manifest);
					session.TraceDiagnostic(NaturalAbyssCoinArmorSteps.ManifestDiagnostic, NaturalAbyssCoinArmorSteps.Row(manifest));
					if (manifest.CoinsToSupply > 0)
					{
						Func<int, long, CancellationToken, Task> supply = runtime.SupplyHelpItemAsync
							?? throw new InvalidDataException($"The manifest is {manifest.CoinsToSupply} Bronze Coins short and this run supplies no help items.");
						NaturalHelpLegSupply approved = NaturalHelpItemAllowlist.LegApproved.Single(entry => entry.Leg == leg.Leg && entry.ItemId == armor.CoinItemId);
						NaturalHelpItemSupply.RequireApproved(armor.CoinItemId, manifest.CoinsToSupply, leg.Leg, coinsSuppliedNow);
						long owned = ItemCount(world, armor.CoinItemId);
						await supply(armor.CoinItemId, manifest.CoinsToSupply, token);
						await session.SynchronizeAsync(token);
						long after = ItemCount(world, armor.CoinItemId);
						Require.True(after == owned + manifest.CoinsToSupply, $"The supplied Bronze Coins did not arrive: {owned} -> {after}.");
						coinsSuppliedNow += manifest.CoinsToSupply;
						helpSupplied.Add(new NaturalHelpSupplied($"coin-armor-{tier.Level}", armor.CoinItemId, approved.Family, manifest.CoinsToSupply, owned,
							after, world.Level, runtime.NowMillis));
						session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
						{
							["trigger"] = $"coin-armor-{tier.Level}", ["itemId"] = armor.CoinItemId, ["family"] = approved.Family,
							["count"] = manifest.CoinsToSupply, ["before"] = owned, ["after"] = after, ["level"] = world.Level, ["decision"] = approved.Decision,
						});
						await File.WriteAllTextAsync(Path.Combine(folder, "help-items.json"), NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
					}
					if (manifest.Buys.Length > 0)
					{
						int vendor = await ApproachInFortressAsync(armor.VendorNpcId);
						purchases.AddRange(await NaturalAbyssCoinArmorSteps.BuyAsync(session, runtime.Data, armor, manifest, vendor, token));
					}
					await CheckAsync($"coin-armor-{tier.Level}");
					foreach (NaturalAbyssCoinTier wearable in armor.Tiers.Where(entry => entry.Level <= world.Level))
					{
						NaturalAbyssCoinManifest left = NaturalAbyssCoinArmorPolicy.Plan(armor, wearable, Observed().Inventory!, PhysicalDefence, StaffMagicBoost, ItemRank);
						Require.True(left.Done, $"A {wearable.Name} piece still beats what is worn: " +
							string.Join(", ", left.Slots.Where(slot => slot.Action != "keep").Select(slot => $"{slot.CoinItemId} ({slot.Action})")));
					}

					// 3. The bags again: the pieces the coin gear replaced.
					await SellSurplusAsync("sell-the-gear-replaced");
					await CheckAsync("muster-end");

					// MR-g: a stone is put in at the fortress's nearest Stigma Master, in his stigma window, for the service fee.
					async Task<(long Fee, long Due, int Master)> SocketStoneAsync(int stoneItemId, long slot)
					{
						int masterNpc = runtime.Data.SpawnsDh.GetSpawnsByWorldId(leg.Hub.MapId)
							.Where(group => runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId())?.GetTitleId() == NaturalStigma.MasterTitleId)
							.SelectMany(group => group.GetSpawnTemplates().Select(spot => (Npc: group.GetNpcId(), At: new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))))
							.Where(spot => Distance(spot.At, hubAt) <= leg.Hub.Radius)
							.OrderBy(spot => Distance(spot.At, session.CurrentPosition)).ThenBy(spot => spot.Npc).Select(spot => spot.Npc).First();
						int master = await ApproachInFortressAsync(masterNpc);
						await NaturalDialogProtocol.OpenAsync(session, master, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == master);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(master,
							checked((ushort)NaturalAscensionContract.DialogActionId("OPEN_STIGMA_WINDOW"))), token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<ushort>("dialogPageId") == 1);
						BotInventoryItem stone = world.Inventory.Values.Single(item => item.ItemId == stoneItemId);
						long due = world.VendorPrices?.ServicePrice(NaturalStigma.InstallationBaseFee) ?? NaturalStigma.InstallationBaseFee, before = world.Kinah;
						Require.True(before >= due, $"The stone costs {due} Kinah to put in and the character has {before}.");
						await session.SendPacketAsync(GameClientPackets.EquipItem(0, slot, stone.ObjectId), token);
						await session.SynchronizeAsync(token);
						await session.SendPacketAsync(session.Api.CloseDialog(master), token);
						return (before - world.Kinah, due, masterNpc);
					}
					// L2 (docs/level-31-profiles.md): the second stone is bought in Pandaemonium, from the vendor of its class's stones,
					// with the character's own Kinah: by the fortress's teleporter there, and home by Return.
					async Task<long> BuyStoneInPandaemoniumAsync(NaturalStigma.Second second)
					{
						NaturalAltgardMapTrip there = leg.MapTripList.Single(trip => trip.FromMapId == NaturalAbyssEntry.Morheim && trip.MapId == NaturalAbyssEntry.Pandaemonium);
						int teleporter = await ApproachInFortressAsync(there.TeleporterNpcId);
						NaturalServiceOutcome travelled = await new NaturalServiceSteps(session).TeleportAsync(teleporter, world.Objects[teleporter].Position,
							there.TalkRange, there.LocationId, there.Fare, there.MapId, token);
						Require.True(travelled.IsDone, travelled.Reason);
						NaturalJourneyNavigator city = mapNavigators.Enter(NaturalMapKey.Observe(world));
						BotPosition vendorAt = runtime.Data.SpawnsDh.GetSpawnsByWorldId(NaturalAbyssEntry.Pandaemonium)
							.Where(group => group.GetNpcId() == second.VendorNpcId).SelectMany(group => group.GetSpawnTemplates())
							.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)).First();
						NaturalNavigationResult reached = await NaturalIshalgenNavigator.ApproachNpcAsync(NaturalAbyssEntry.Pandaemonium, second.VendorNpcId,
							vendorAt, city, token);
						Require.True(reached.Arrived, $"Stigma vendor {second.VendorNpcId}: {reached.Reason}");
						int vendor = Require.IsType<int>(reached.TargetObjectId);
						await NaturalDialogProtocol.OpenAsync(session, vendor, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, p => p.Get<int>("targetObjectId") == vendor);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vendor, 2), token);
						await session.WaitForPacketAsync(typeof(SM_TRADELIST), token);
						long before = world.Kinah;
						await session.SendPacketAsync(session.Api.Buy(vendor, [(second.StoneItemId, 1)]), token);
						await session.SynchronizeAsync(token);
						await session.SendPacketAsync(session.Api.CloseDialog(vendor), token);
						long paid = before - world.Kinah;
						session.TraceDiagnostic("muster-stigma-bought", new Dictionary<string, object?>
						{ ["stone"] = second.StoneItemId, ["vendor"] = second.VendorNpcId, ["paid"] = paid, ["kinahAfter"] = world.Kinah });
						Require.True(ItemCount(world, second.StoneItemId) == 1 && paid > 0, $"The stone {second.StoneItemId} was not bought ({paid} Kinah, Kinah {before}).");
						await UseLearnedReturnToBindAsync();
						await session.SynchronizeAsync(token);
						navigator = mapNavigators.Enter(NaturalMapKey.Observe(world));
						Require.Equal(leg.Hub.MapId, world.MapId ?? 0);
						return paid;
					}

					// 3b. The stigma (MR-g): the class's stone out of the Stigma Support Bundle, put into the one slot a character
					// has below level 30, at the fortress's Stigma Master. Until 2026-10-13 the cleanup threw the bundle of Q2900
					// away, as no rule used it; a character that lost its bundle that way is supplied one.
					Dictionary<string, object?>? stigmaRow = null;
					if (NaturalStigma.Choices.TryGetValue(combat.ClassProfile.Class, out NaturalStigma.Choice? stigma))
					{
						session.BeginStep("muster-stigma", "socket-the-class-stone");
						int[] taught = [.. NaturalStigma.Skills(runtime.Data, combat.ClassProfile.Class).Where(skill => skill.Value <= world.Level)
							.Select(skill => skill.Key).Order()];
						bool Socketed() => world.Inventory.Values.Any(item => item.ItemId == stigma.StoneItemId && item.Details.EquippedSlot == NaturalStigma.Slot);
						bool bundleSupplied = false, bundleOpened = false;
						long stigmaFee = 0;
						int stigmaMaster = 0;
						if (!Socketed())
						{
							if (ItemCount(world, stigma.StoneItemId) == 0)
							{
								if (ItemCount(world, NaturalStigma.BundleItemId) == 0)
								{
									Func<int, long, CancellationToken, Task> supply = runtime.SupplyHelpItemAsync
										?? throw new InvalidDataException("The Stigma Support Bundle is gone and this run supplies no help items.");
									NaturalHelpLegSupply approved = NaturalHelpItemAllowlist.LegApproved.Single(entry => entry.Leg == leg.Leg && entry.ItemId == NaturalStigma.BundleItemId);
									NaturalHelpItemSupply.RequireApproved(NaturalStigma.BundleItemId, 1, leg.Leg, 0);
									await supply(NaturalStigma.BundleItemId, 1, token);
									await session.SynchronizeAsync(token);
									Require.True(ItemCount(world, NaturalStigma.BundleItemId) == 1, "The supplied Stigma Support Bundle did not arrive.");
									bundleSupplied = true;
									helpSupplied.Add(new NaturalHelpSupplied("stigma", NaturalStigma.BundleItemId, approved.Family, 1, 0, 1, world.Level, runtime.NowMillis));
									session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
									{
										["trigger"] = "stigma", ["itemId"] = NaturalStigma.BundleItemId, ["family"] = approved.Family, ["count"] = 1,
										["before"] = 0, ["after"] = 1, ["level"] = world.Level, ["decision"] = approved.Decision,
									});
									await File.WriteAllTextAsync(Path.Combine(folder, "help-items.json"), NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
								}
								// The bundle asks which stone before it is used up (Java DecomposeAction, a selectable decomposable).
								BotInventoryItem bundle = world.Inventory.Values.First(item => item.ItemId == NaturalStigma.BundleItemId);
								await session.SendPacketAsync(session.Api.UseItem(bundle.ObjectId, runtime.Data.ItemDataDh.GetItemTemplate(bundle.ItemId)), token);
								DecodedBotServerPacket offered = await session.WaitForPacketAsync(typeof(SM_SHOW_DECOMPOSABLE), token, p => p.Get<int>("objectId") == bundle.ObjectId);
								BotDecomposableChoice pick = offered.Get<BotDecomposableChoice[]>("choices").SingleOrDefault(choice => choice.ItemId == stigma.StoneItemId)
									?? throw new InvalidDataException($"The bundle does not offer a {combat.ClassProfile.Class} the stone {stigma.StoneItemId}.");
								await session.SendPacketAsync(GameClientPackets.SelectDecomposable(bundle.ObjectId, pick.Index), token);
								await session.SynchronizeAsync(token);
								Require.True(ItemCount(world, stigma.StoneItemId) == 1 && ItemCount(world, NaturalStigma.BundleItemId) == 0,
									$"The bundle did not give the stone {stigma.StoneItemId}.");
								bundleOpened = true;
							}
							(stigmaFee, long due, stigmaMaster) = await SocketStoneAsync(stigma.StoneItemId, NaturalStigma.Slot);
							Require.True(Socketed() && taught.All(skill => world.Skills.ContainsKey(skill)) && stigmaFee == due,
								$"The stone {stigma.StoneItemId} was not put in: socketed {Socketed()}, skills [{string.Join(", ", taught.Where(skill => world.Skills.ContainsKey(skill)))}] of " +
								$"[{string.Join(", ", taught)}], fee {stigmaFee} of {due}.");
						}
						stigmaRow = new Dictionary<string, object?>
						{
							["class"] = combat.ObservedCharacter, ["stone"] = stigma.StoneItemId, ["role"] = stigma.Role, ["skills"] = taught, ["slot"] = NaturalStigma.Slot,
							["bundleSupplied"] = bundleSupplied, ["bundleOpened"] = bundleOpened, ["master"] = stigmaMaster, ["fee"] = stigmaFee,
						};
						session.TraceDiagnostic("muster-stigma", stigmaRow);
						await CheckAsync("muster-stigma");
						Require.True(Socketed() && taught.All(skill => world.Skills.ContainsKey(skill)), "The gear check took the stigma out.");
					}

					// 3c. The second stone (L2, docs/level-31-profiles.md): level 30 opens the second regular slot (Java
					// StigmaService.getPossibleStigmaCount); the class's second stone, bought in Pandaemonium, goes into it.
					if (world.Level >= NaturalStigma.SecondSlotLevel && NaturalStigma.Seconds.TryGetValue(combat.ClassProfile.Class, out NaturalStigma.Second? second))
					{
						session.BeginStep("muster-stigma-second", "buy-and-socket-the-second-stone");
						int[] secondTaught = [.. NaturalStigma.StoneSkills(runtime.Data, combat.ClassProfile.Class, second.StoneItemId)
							.Where(skill => skill.Value <= world.Level).Select(skill => skill.Key).Order()];
						bool SecondSocketed() => world.Inventory.Values.Any(item => item.ItemId == second.StoneItemId && item.Details.EquippedSlot == NaturalStigma.SecondSlot);
						long stonePrice = 0, secondFee = 0, secondDue = 0;
						int secondMaster = 0;
						if (!SecondSocketed())
						{
							if (ItemCount(world, second.StoneItemId) == 0) stonePrice = await BuyStoneInPandaemoniumAsync(second);
							(secondFee, secondDue, secondMaster) = await SocketStoneAsync(second.StoneItemId, NaturalStigma.SecondSlot);
							Require.True(SecondSocketed() && secondTaught.All(skill => world.Skills.ContainsKey(skill)) && secondFee == secondDue,
								$"The second stone {second.StoneItemId} was not put in: socketed {SecondSocketed()}, skills " +
								$"[{string.Join(", ", secondTaught.Where(skill => world.Skills.ContainsKey(skill)))}] of [{string.Join(", ", secondTaught)}], fee {secondFee} of {secondDue}.");
						}
						var secondRow = new Dictionary<string, object?>
						{
							["class"] = combat.ObservedCharacter, ["stone"] = second.StoneItemId, ["role"] = second.Role, ["skills"] = secondTaught,
							["slot"] = NaturalStigma.SecondSlot, ["vendor"] = second.VendorNpcId, ["price"] = stonePrice, ["master"] = secondMaster, ["fee"] = secondFee,
						};
						session.TraceDiagnostic("muster-stigma-second", secondRow);
						if (stigmaRow != null) stigmaRow["second"] = secondRow;
						await CheckAsync("muster-stigma-second");
						Require.True(SecondSocketed() && secondTaught.All(skill => world.Skills.ContainsKey(skill)), "The gear check took the second stone out.");
					}

					// 4. The kit, every row at its full count.
					session.BeginStep("muster-stock", "fill-the-kit");
					await TopUpHelpItemsAsync("muster", full: true);
					await session.SynchronizeAsync(token);
					// The shards supplied go into their slots and the mode on (MR-e); the gear check then finds the old ones.
					await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRest, token);
					await CheckAsync("muster-stocked");
					NaturalHelpSupply[] kitNow = [.. combat.ClassProfile.HelpItems.Kit.Where(row => row.FromLevel <= world.Level && world.Level <= row.ToLevel)];
					if (runtime.SupplyHelpItemAsync != null)
						Require.All(kitNow, row => Require.True(ItemCount(world, row.ItemId) >= row.TopUpTo,
							$"The kit's {row.Family} {row.ItemId} is at {ItemCount(world, row.ItemId)} of {row.TopUpTo}."));
					Require.True(world.Level >= minimum && !world.IsDead, $"The character is level {world.Level}, below {minimum}.");
					// MR-e: the kit's shard in both slots, and the mode on. MR-f: nothing left that the cleanup discards.
					var shardsWorn = world.Inventory.Values.Where(NaturalPowerShardPolicy.Worn).OrderBy(item => item.Details.EquippedSlot)
						.Select(item => new { Slot = item.Details.EquippedSlot, item.ItemId, item.Count }).ToArray();
					if (runtime.SupplyHelpItemAsync != null && kitNow.FirstOrDefault(row => row.Family == "power-shard") is { } shardRow)
						Require.True(world.PowerShardOn && NaturalPowerShardPolicy.Slots.All(slot => shardsWorn.Any(worn =>
							worn.ItemId == shardRow.ItemId && (worn.Slot.GetValueOrDefault() & slot) != 0)),
							$"The kit's power shard {shardRow.ItemId} is not in both slots with the mode on: [{string.Join(", ", shardsWorn.Select(worn => $"{worn.Slot}:{worn.ItemId}"))}], mode {world.PowerShardOn}.");
					NaturalBagPlan cube = BagPlan();
					IReadOnlyList<NaturalBagDiscard> leftToDiscard = cube.Discards;
					Require.True(leftToDiscard.Count == 0, "The cube still holds what the cleanup discards: " +
						string.Join(", ", leftToDiscard.Select(discard => $"{discard.ItemId} ({discard.Reason})")));
					// What is left in the cube, each stack with the rule that keeps it.
					var inCube = cube.Kept.Select(stack => new { stack.ItemId, stack.Count, stack.Reason }).ToArray();
					// Room for what the next legs pay: the free slots a quest's rewards are kept for.
					int freeSlots = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
						.Decide(world, QuestNeededItems()).FreeSlots;
					Require.True(freeSlots >= NaturalInventoryPlan.QuestFreeSlotReserve, $"The cube has {freeSlots} free slots at the muster's end.");

					// 5. The end: the character as the fortress leaves it, checked again across a relog.
					session.BeginStep("muster-endpoint", "verify-and-relog-at-the-muster-end");
					int[] wornBefore = Worn();
					Dictionary<int, long> kitBefore = kitNow.ToDictionary(row => row.ItemId, row => ItemCount(world, row.ItemId));
					int level = world.Level;
					long experience = ObservedExperience(), kinah = world.Kinah, coinsLeft = ItemCount(world, armor.CoinItemId);
					int deathsNow = combat.ReviveCount - startDeaths, stacks = world.Inventory.Count;
					long elapsed = runtime.NowMillis;
					(NaturalJourneyCheckpoint musterBefore, NaturalJourneyCheckpoint musterAfter) = await RelogAtEndpointAsync();
					BotWorldModel again = session.Api.World;
					Require.True(stigmaRow?["skills"] is not int[] stigmaSkills || stigmaSkills.All(skill => again.Skills.ContainsKey(skill)),
						"The stigma's skills did not come back with the relog.");
					Require.True(again.Level == level && Worn().SequenceEqual(wornBefore) &&
						kitBefore.All(row => ItemCount(again, row.Key) == row.Value) && ItemCount(again, armor.CoinItemId) == coinsLeft,
						"The mustered character changed across a relog.");
					foreach (NaturalAbyssCoinTier wearable in armor.Tiers.Where(entry => entry.Level <= again.Level))
						Require.True(NaturalAbyssCoinArmorPolicy.Plan(armor, wearable,
							NaturalAltgardObservation.Observe(again, session.CurrentPosition).Inventory!, PhysicalDefence, StaffMagicBoost, ItemRank).Done,
							$"A {wearable.Name} piece beats what is worn after the relog.");
					await File.WriteAllTextAsync(Path.Combine(folder, NaturalAbyssEntry.MusterReceipt), System.Text.Json.JsonSerializer.Serialize(new
					{
						verified = true, session.CharacterId, Class = combat.ObservedCharacter, Line = ClassLine.Id, StartLevel = startLevel, Level = level,
						Experience = experience, ExperienceGained = experience - startExperience, Turns = turns,
						CoinTier = tier.Level, CoinManifest = NaturalAbyssCoinArmorSteps.Row(manifest), CoinPurchases = purchases, CoinsSupplied = coinsSuppliedNow,
						BronzeCoinsAtStart = startCoins, BronzeCoins = coinsLeft, Opened = openedNow, NotOpened = notOpenedNow,
						Sold = sold, SaleKinah = saleKinah, Kinah = kinah, KinahAtStart = startKinah, WornAtStart = wornAtStart, Worn = wornBefore,
						Kit = kitBefore, StacksAtStart = stacksAtStart, Stacks = stacks, FreeSlots = freeSlots, Deaths = deathsNow, GameMillis = elapsed - startedMillis,
						PowerShards = shardsWorn, PowerShardMode = true, Discarded = bagDiscards, KeptByServer = keptByServer.Count, InCube = inCube,
						Stigma = stigmaRow,
						ElapsedMillis = elapsed, before = musterBefore, after = musterAfter,
					}), token);
					session.TraceDiagnostic("muster-completed", new Dictionary<string, object?>
					{
						["class"] = combat.ObservedCharacter, ["startLevel"] = startLevel, ["level"] = level, ["turns"] = turns.Count,
						["experienceGained"] = experience - startExperience, ["coinsSupplied"] = coinsSuppliedNow,
						["bought"] = purchases.Select(purchase => purchase.ItemId).ToArray(), ["sold"] = sold.Count, ["opened"] = openedNow.Count,
						["worn"] = wornBefore, ["kit"] = kitBefore, ["freeSlots"] = freeSlots, ["deaths"] = deathsNow, ["generation"] = musterAfter.ConnectionGeneration,
						["powerShards"] = shardsWorn.Select(worn => $"{worn.Slot}:{worn.ItemId}x{worn.Count}").ToArray(), ["discarded"] = bagDiscards.Count,
						["stigma"] = stigmaRow?["stone"],
					});
				}
				if (options.SurveyNpcId is int surveyTarget)
				{
					Require.True(runtime.Profile.StartsWith("SIM-", StringComparison.Ordinal) && runtime.Seed == 1 && resuming &&
						world.Level >= 26 && world.MapId == leg.Hub.MapId && leg.Order.All(world.CompletedQuestIds.Contains),
						// L3 (docs/level-31-profiles.md): and a checked one of 31, whose new skills a survey shows.
						"GH-01 surveys resume a mustered character of level 26 or above in SIM at seed 1.");
					session.BeginStep("gh-survey-start", "measure-the-ordinary-fighter");
					await FallFromLoginPlaceAsync();
					EnterObservedMap();
					navigator.AvoidHostileAggro = true;
					navigationDefense = combat;
					combat.AfterBindRevive = EnterObservedMap;
					await RestSafelyAsync(token);
					session.TraceDiagnostic("gh-survey-start", new Dictionary<string, object?>
					{
						["npcId"] = surveyTarget, ["level"] = world.Level, ["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp,
						["quest2307Completed"] = world.CompletedQuestIds.Contains(2307), ["quest2430Completed"] = world.CompletedQuestIds.Contains(2430),
					});
					session.BeginStep("gh-survey-approach", "walk-to-the-shipped-monster");
					int target = await ApproachShippedSpawnAsync(surveyTarget, withinRange: 23);
					await RestSafelyAsync(token);
					session.BeginStep("gh-survey-fight", "ordinary-combat-one-attempt");
					bool killed = await combat.TryKillAsync(target, token, session.CurrentPosition);
					session.TraceDiagnostic("gh-survey-end", new Dictionary<string, object?>
					{
						["npcId"] = surveyTarget, ["targetObjectId"] = target, ["killed"] = killed, ["deaths"] = combat.ReviveCount,
						["retreats"] = combat.CompletedRetreats, ["hp"] = world.CurrentHp, ["maxHp"] = world.MaxHp,
					});
					await File.WriteAllTextAsync(Path.Combine(folder, "gh-survey-completion.json"), System.Text.Json.JsonSerializer.Serialize(new
					{
						verified = true, session.CharacterId, NpcId = surveyTarget, TargetObjectId = target, Killed = killed,
						Deaths = combat.ReviveCount, Retreats = combat.CompletedRetreats, ElapsedMillis = runtime.NowMillis,
					}), token);
					return;
				}
				if (options.Muster)
				{
					Require.True(leg.Order.All(world.CompletedQuestIds.Contains), "The muster is for a character that stands at the Abyss entry's end.");
					await RunMusterAsync();
					return;
				}

				// AX-14: a run resumed on the endpoint snapshot has nothing left to play. The endpoint is checked from this fresh login
				// and again across one more relog, and the resume receipt is written.
				if (leg.Order.All(world.CompletedQuestIds.Contains))
				{
					session.BeginStep("ax-endpoint-resume", "verify-the-restored-endpoint-and-relog");
					NaturalAbyssEntryEndpoint restored = ObservedEndpoint();
					(NaturalJourneyCheckpoint resumedBefore, NaturalJourneyCheckpoint resumedAfter) = await RelogAtEndpointAsync();
					NaturalAbyssEntryEndpoint again = ObservedEndpoint();
					Require.True(NaturalAbyssEntryLeg.SameEndpoint(restored, again), "The restored endpoint changed across a relog.");
					await File.WriteAllTextAsync(Path.Combine(folder, NaturalAbyssEntryLeg.EndpointResumeReceipt), System.Text.Json.JsonSerializer.Serialize(new
					{
						before = resumedBefore, after = resumedAfter, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis, endpoint = again,
					}), token);
					session.TraceDiagnostic(NaturalAbyssEntryLeg.EndpointResumeDiagnostic, new Dictionary<string, object?>
					{
						["level"] = again.Level, ["map"] = again.MapId, ["kinah"] = again.Kinah, ["bronzeCoins"] = again.BronzeCoins, ["staff"] = again.StaffItemId,
						["torso"] = again.TorsoItemId, ["completed"] = again.CompletedLegQuestIds, ["recoverableExperience"] = session.Api.World.RecoverableExperience,
						["generation"] = resumedAfter.ConnectionGeneration,
					});
					return;
				}

				session.BeginStep("ax-start", "verify-the-abyss-entry-start-contract");
				NaturalAbyssEntryStart start = NaturalAbyssEntryLeg.VerifyStart(leg, Observed(), session.CharacterId, runtime.NowMillis, OfContractClass(leg));
				await File.WriteAllTextAsync(Path.Combine(folder, NaturalAbyssEntryLeg.StartReceipt), System.Text.Json.JsonSerializer.Serialize(start), token);
				session.TraceDiagnostic(NaturalAbyssEntryLeg.StartDiagnostic, new Dictionary<string, object?>
				{
					["level"] = start.Level, ["map"] = start.MapId, ["completed"] = start.CompletedQuests, ["started"] = start.StartedQuestIds,
					["kinah"] = start.Kinah, ["bronzeCoins"] = start.BronzeCoins, ["staffObjectId"] = start.StaffObjectId,
				});
				long experienceAtStart = ObservedExperience(), fares = 0, bindPaid = 0, coinsSupplied = 0;
				var payments = new List<NaturalAbyssPayment>();
				var coinManifests = new List<NaturalAbyssCoinManifest>();
				var coinPurchases = new List<NaturalAbyssCoinPurchase>();
				var opened = new List<NaturalOpenedContainer>();
				var discarded = new List<NaturalJourneyItem>();
				int inventoryChecks = 0, otherInventoryChecks = 0, notOpened = 0;
				// AX-08: Garm's arena. A try begins when Garm's talk lands the Cleric inside (D35) and ends at ten kills, at the
				// timer's end or at a death (D36). Every try goes on the outcome ledger; a failed one is an outcome.
				NaturalAbyssArena arena = scope.Arena;
				int[] spiritIds = arena.Spirits.Select(spirit => spirit.NpcId).ToArray();
				var attempts = new List<NaturalAbyssAttempt>();
				(int Number, long StartedMillis, long Experience, int Revives)? arenaTry = null;
				long arenaDeadline = 0, arenaExperience = 0;
				int arenaKillsSeen = 0;
				var searchedGroups = new HashSet<string>();
				var openedDoors = new HashSet<int>();
				int ArenaKills() => world.Quests.TryGetValue(arena.QuestId, out BotQuestState? trial) ? NaturalAbyssEntryDecisionEngine.ArenaKills(arena, trial) : 0;
				// Navigation and combat for whichever map the client is on now: the arena on entry, the bind map after a death.
				void EnterObservedMap()
				{
					NaturalMapKey key = NaturalMapKey.Observe(world);
					var defend = navigator.DefendOnAttackAsync;
					navigator = mapNavigators.Enter(key, newEntry: true);
					navigator.DefendOnAttackAsync = defend;
					geometry = runtime.CreateGeometry();
					contract = contract with { MapId = key.MapId };
					combat.EnterMap(navigator, geometry, key.MapId);
					navigator.AvoidSpots = combat.DeathSpots;
				}
				void EndArenaTry(string outcome, string reason)
				{
					if (arenaTry is not { } running) return;
					var attempt = new NaturalAbyssAttempt(NaturalAbyssAttempts.Arena, running.Number, outcome, running.StartedMillis, runtime.NowMillis,
						Math.Max(arenaKillsSeen, ArenaKills()), reason);
					attempts.Add(attempt);
					arenaExperience += ObservedExperience() - running.Experience;
					arenaTry = null;
					session.TraceDiagnostic(NaturalAbyssAttempts.Diagnostic(NaturalAbyssAttempts.Arena), NaturalAbyssAttempts.Row(attempt));
				}
				async Task FollowTeleportAsync(int toMap)
				{
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == toMap);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					await session.SynchronizeAsync(token);
					EnterObservedMap();
				}
				// AX-12b: what the course's deaths took and their soul healings gave back. The healings themselves are the death
				// rule's, counted by the combat observer for every leg.
				long courseExperience = 0;
				IReadOnlyList<NaturalSoulHeal> soulHeals = combat.SoulHeals;
				combat.AfterBindRevive = EnterObservedMap;
				// The death rule is the combat observer's, for every leg: in the arena it takes the instance revive the prompt offers
				// (D38), and after an obelisk resurrection it soul heals at the nearest Soul Healer.
				// AX-10: Yornduf's ring course. A try begins at his talk, which starts the 70 s, and ends when the sixth ring is
				// passed, when the timer fails it (var 9), or at a death. Every try goes on the outcome ledger.
				NaturalAbyssRingCourse course = scope.RingCourse;
				NaturalAbyssSupply scrollSupply = scope.Supplies.Single(supply => supply.Family == "flight-speed");
				(int Number, long StartedMillis, long Experience, int Revives)? ringTry = null;
				long ringDeadline = 0, scrollsSupplied = 0, scrollsUsed = 0, landedMillis = long.MinValue / 2;
				int ringsSeen = 0;
				bool airborne = false;
				float flightSpeed = 0;
				BotPosition? takeoffSpot = null;
				int RingVar() => world.Quests.TryGetValue(course.QuestId, out BotQuestState? flown) ? flown.StepAndFlags & 0x3F : -1;
				int RingsPassed() => Math.Clamp(RingVar() - course.StartVar, 0, course.Rings.Length);
				void EndRingTry(string outcome, string reason)
				{
					if (ringTry is not { } running) return;
					var attempt = new NaturalAbyssAttempt(NaturalAbyssAttempts.RingCourse, running.Number, outcome, running.StartedMillis, runtime.NowMillis,
						Math.Max(ringsSeen, RingVar() == course.FailedVar ? 0 : RingsPassed()), reason);
					attempts.Add(attempt);
					courseExperience += ObservedExperience() - running.Experience;
					ringTry = null;
					session.TraceDiagnostic(NaturalAbyssAttempts.Diagnostic(NaturalAbyssAttempts.RingCourse), NaturalAbyssAttempts.Row(attempt));
				}
				int PhysicalDefence(int itemId) => NaturalAbyssCoinArmorPolicy.PhysicalDefence(runtime.Data.ItemDataDh.GetItemTemplate(itemId));
				// L2 (docs/level-31-profiles.md): a piece's level and grade, which the muster and its check-ins compare first.
				(int Level, int Grade) ItemRank(int itemId) => runtime.Data.ItemDataDh.GetItemTemplate(itemId) is { } ranked
					? (ranked.GetLevel(), (int)ranked.GetItemQuality()) : (0, 0);
				// AX-12c: a staff's magic boost, the stat the staff rule and the coin staff are decided by.
				// NR-38b: a class other than the contract's is asked for its own weapon number, the one its gear rules wear by.
				int StaffMagicBoost(int itemId)
				{
					if (scope.CoinArmor.StaffBetter != NaturalAbyssCoinArmorPolicy.ClassWeaponStat)
						return runtime.Data.ItemDataDh.GetItemTemplate(itemId) is { } template && template.GetItemGroup().ToString() == "STAFF"
							? template.GetWeaponStats()?.GetBoostMagicalSkill() ?? 0 : 0;
					if (!weaponNumbers.TryGetValue(itemId, out int number))
						weaponNumbers[itemId] = number = combat.ClassProfile.Gear.WeaponNumber(
							NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, [itemId], ClassLine).Item(itemId));
					return number;
				}
				// AX-04: the inventory check the operator asked for after every quest turn-in (2026-10-06), and once at the start so
				// the leg begins with the best owned gear worn. AX-06: also after a coin armor purchase, to wear it.
				async Task InventoryCheckAsync(string trigger, bool turnIn = true)
				{
					session.BeginStep("ax-inventory-check", trigger);
					NaturalInventoryCheckResult checkedNow = await NaturalInventoryCheck.RunAsync(session, trigger, scope.Inventory, EquipUpgradesAsync,
						runtime.Data.ItemDataDh.GetItemTemplate,
						() => NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
							.Decide(world, QuestNeededItems()).FreeSlots, token);
					opened.AddRange(checkedNow.Opened);
					discarded.AddRange(checkedNow.Discarded);
					notOpened += checkedNow.NotOpened.Length;
					if (turnIn) inventoryChecks++;
					else otherInventoryChecks++;
				}
				await FallFromLoginPlaceAsync();
				await TopUpHelpItemsAsync("run-start");
				await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
				await InventoryCheckAsync("leg-start");

				string? previous = null;
				int repeats = 0;
				for (int sequence = 1; sequence <= 200; sequence++)
				{
					await session.SynchronizeAsync(token);
					// A death the combat already revived from: the try is over (D36). The revive is inside the arena (D38); off its map
					// below ten kills is the older case, a revive at the bind.
					if (arenaTry is { } fought && !world.IsDead && ArenaKills() < arena.RequiredKills &&
						(world.MapId != arena.MapId || combat.ReviveCount > fought.Revives))
						EndArenaTry("death", world.MapId == arena.MapId
							? "The Cleric died in the arena and revived inside it; the server failed the attempt at once (D36, D38)."
							: "The Cleric died in the arena; the server failed the attempt at once (D36).");
					// A death on the course fails it, as in Java (var 9): the try ends once the Cleric is back on its feet.
					if (ringTry is { } flying && !world.IsDead && !airborne && RingVar() == course.FailedVar)
						EndRingTry(combat.ReviveCount > flying.Revives ? "death" : "timeout", combat.ReviveCount > flying.Revives
							? "The Cleric died on the course; the server failed it at once (var 9)."
							: "The course failed (var 9) before the sixth ring.");
					NaturalAbyssEntryDecision next = NaturalAbyssEntryDecisionEngine.Decide(leg, Observed(), sequence, PhysicalDefence, StaffMagicBoost, attempts);
					// The fight's own progress is the kill count and the clock, so a fight decision never looks like a stall.
					string signature = $"{next.Action}|{next.StepKey}|{next.Reason}|{(next.Action == "arena-fight" ? runtime.NowMillis : 0)}";
					repeats = signature == previous ? repeats + 1 : 0;
					previous = signature;
					// A decision that does not change the client's view is a stall; waiting for the journal may take a few looks.
					Require.True(repeats < (next.Action == "refresh-observation" ? 40 : 3), $"The Abyss-entry leg stalled on: {next.Reason}");
					session.BeginStep(next.StepKey ?? $"ax-{next.Action}", next.Action);
					session.TraceDiagnostic("abyss-entry-decision", new Dictionary<string, object?>
					{
						["sequence"] = next.Sequence, ["phase"] = next.Phase, ["action"] = next.Action, ["step"] = next.StepKey, ["quest"] = next.QuestId,
						["toMap"] = next.MapId, ["reason"] = next.Reason, ["map"] = world.MapId, ["position"] = session.CurrentPosition,
					});
					session.PublishDashboard();
					switch (next.Action)
					{
						case "frontier":
						{
							// AX-13: at the endpoint the consumables are topped up first, as at every earlier leg's checkpoint, so the
							// receipt and the relog see the character the endpoint snapshot will hold.
							bool endpoint = next.Phase == NaturalAbyssEntryDecisionEngine.EndpointPhase;
							if (endpoint)
							{
								session.BeginStep("ax-endpoint", "verify-and-relog-at-the-leg-endpoint");
								await TopUpHelpItemsAsync("checkpoint");
								await session.SynchronizeAsync(token);
							}
							var ledger = new NaturalAbyssLedger(ObservedExperience() - experienceAtStart, fares, bindPaid, payments, inventoryChecks, otherInventoryChecks,
								coinManifests, coinPurchases, coinsSupplied, opened, notOpened, attempts, arenaExperience, combat.ReviveCount, discarded,
								scrollsSupplied, scrollsUsed, soulHeals, combat.BindReviveCount, courseExperience);
							NaturalAbyssEntryProgress progress = NaturalAbyssEntryLeg.VerifyProgress(leg, start, Observed(), next.Phase, ledger,
								StaffMagicBoost, PhysicalDefence, runtime.NowMillis);
							await File.WriteAllTextAsync(Path.Combine(folder, NaturalAbyssEntryLeg.ProgressReceipt),
								System.Text.Json.JsonSerializer.Serialize(progress), token);
							session.TraceDiagnostic(NaturalAbyssEntryLeg.ProgressDiagnostic, new Dictionary<string, object?>
							{
								["frontier"] = progress.Frontier, ["map"] = progress.MapId, ["level"] = progress.Level, ["kinah"] = progress.Kinah,
								["fares"] = progress.Fares, ["bindPaid"] = progress.BindPaid, ["experienceGained"] = progress.ExperienceGained,
								["completed"] = progress.CompletedLegQuestIds, ["started"] = progress.StartedQuestIds, ["locked"] = progress.LockedQuestIds,
								["staff"] = progress.StaffItemId,
								["torso"] = progress.TorsoItemId, ["inventoryChecks"] = progress.InventoryChecks, ["bronzeCoins"] = progress.BronzeCoins,
								["coinsSupplied"] = progress.CoinsSupplied, ["coinPurchases"] = coinPurchases.Select(purchase => purchase.ItemId).ToArray(),
								["opened"] = opened.Select(container => container.ItemId).ToArray(),
								["paid"] = payments.Select(payment => payment.QuestId).ToArray(),
								["attempts"] = attempts.Select(attempt => $"{attempt.Kind} {attempt.Number}: {attempt.Outcome}, {attempt.Progress}").ToArray(),
								["arenaExperience"] = arenaExperience, ["deaths"] = combat.ReviveCount,
								["discarded"] = discarded.Select(item => item.ItemId).ToArray(),
								["scrollsSupplied"] = scrollsSupplied, ["scrollsUsed"] = scrollsUsed,
								["obeliskRevives"] = combat.BindReviveCount, ["courseExperience"] = courseExperience,
								["soulHeals"] = soulHeals.Select(heal => $"{heal.Recovered} XP for {heal.Price} Kinah").ToArray(),
								["reason"] = next.Reason,
							});
							if (!endpoint) return;
							// AX-13: the endpoint. It is verified from the client's view above. Then the relog, and the same checks from the
							// fresh login: the frontier check with the leg's ledger, and the ledger-free endpoint check a restored snapshot
							// has to pass (AX-14). The completion receipt carries the clock and the leg's record for that snapshot.
							(NaturalJourneyCheckpoint before, NaturalJourneyCheckpoint after) = await RelogAtEndpointAsync();
							BotWorldModel relogged = session.Api.World;
							long experienceAfter = relogged.CurrentExperience + runtime.Data.PlayerExperienceTable.GetStartExpForLevel(relogged.Level);
							NaturalAbyssEntryProgress resumed = NaturalAbyssEntryLeg.VerifyProgress(leg, start,
								NaturalAltgardObservation.Observe(relogged, session.CurrentPosition), next.Phase,
								ledger with { ExperienceGained = experienceAfter - experienceAtStart }, StaffMagicBoost, PhysicalDefence, runtime.NowMillis);
							Require.True(resumed.Kinah == progress.Kinah && resumed.BronzeCoins == progress.BronzeCoins && resumed.ExperienceGained == progress.ExperienceGained &&
								resumed.StaffItemId == progress.StaffItemId && resumed.TorsoItemId == progress.TorsoItemId && resumed.Level == progress.Level,
								"The relogged character differs from the one that reached the endpoint.");
							NaturalAbyssEntryEndpoint left = ObservedEndpoint();
							await File.WriteAllTextAsync(Path.Combine(folder, NaturalAbyssEntryLeg.CompletionReceipt), System.Text.Json.JsonSerializer.Serialize(new
							{
								before, after, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis, Deaths = combat.ReviveCount,
								progress = resumed, endpoint = left, helpItems = helpSupplied, recoverableExperience = relogged.RecoverableExperience,
							}), token);
							session.TraceDiagnostic(NaturalAbyssEntryLeg.CompletionDiagnostic, new Dictionary<string, object?>
							{
								["level"] = resumed.Level, ["map"] = resumed.MapId, ["completed"] = resumed.CompletedLegQuestIds, ["kinah"] = resumed.Kinah,
								["experienceGained"] = resumed.ExperienceGained, ["deaths"] = combat.ReviveCount, ["staff"] = resumed.StaffItemId,
								["recoverableExperience"] = relogged.RecoverableExperience, ["generation"] = after.ConnectionGeneration,
								["elapsedMillis"] = runtime.NowMillis,
							});
							return;
						}
						case "inventory-check":
							await InventoryCheckAsync(next.Phase, turnIn: false);
							break;
						case "arena-fight":
						{
							// A try the runner did not see begin (a resumed run): count it from here.
							if (arenaTry == null)
							{
								arenaTry = (attempts.Count(attempt => attempt.Kind == NaturalAbyssAttempts.Arena) + 1, runtime.NowMillis, ObservedExperience(), combat.ReviveCount);
								arenaDeadline = runtime.NowMillis + arena.Seconds * 1000L;
								arenaKillsSeen = ArenaKills();
								searchedGroups.Clear();
								openedDoors.Clear();
								EnterObservedMap();
							}
							long left = arenaDeadline - runtime.NowMillis;
							if (left <= 8_000)
							{
								// The 240 s are all but up: no kill fits. Java's timer sets var 6 and teleports the Cleric to Garm.
								arenaKillsSeen = Math.Max(arenaKillsSeen, ArenaKills());
								world.BeginWorldReload();
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(left, 0) + 1_000), token);
								await FollowTeleportAsync(arena.DoneTeleport.MapId);
								EndArenaTry("timeout", $"The {arena.Seconds} s ran out with {arenaKillsSeen} of {arena.RequiredKills} spirits counted.");
								break;
							}
							BotPosition At(float[] at) => new(at[0], at[1], at[2], 0);
							// A recorded outcome on request (AX_ARENA_FIRST_TRY), to prove the failure path: the first try is lost by
							// ordinary play. "timeout": the Cleric waits at the entry until the timer is all but out. "death": it opens
							// the nearest room, walks in among the spirits and does not fight.
							if (arenaTry is { Number: 1 } && options.AbyssArenaFirstTry is { } lose)
							{
								Require.True(lose is "timeout" or "death", $"AX_ARENA_FIRST_TRY is '{lose}', not timeout or death.");
								if (lose == "timeout")
								{
									session.TraceDiagnostic("arena-first-try-idle", new Dictionary<string, object?> { ["mode"] = lose, ["seconds"] = (left - 8_000) / 1000 });
									await session.AdvanceAsync(TimeSpan.FromMilliseconds(left - 8_000), token);
									break;
								}
								NaturalAbyssSpiritGroup room = arena.Groups.OrderBy(entry => Distance(session.CurrentPosition, At(entry.DoorStand))).First();
								if (openedDoors.Contains(room.DoorId))
								{
									var defend = navigator.DefendOnAttackAsync;
									navigator.DefendOnAttackAsync = null;
									try { await NaturalIshalgenNavigator.ExploreAnchorAsync(arena.MapId, 0, At(room.Center), navigator, "arena-idle", token); }
									finally { navigator.DefendOnAttackAsync = defend; }
									for (int second = 0; second < 60 && !world.IsDead && arenaDeadline - runtime.NowMillis > 8_000; second++)
									{
										await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
										await session.SynchronizeAsync(token);
									}
									session.TraceDiagnostic("arena-first-try-idle", new Dictionary<string, object?>
									{
										["mode"] = lose, ["room"] = room.Key, ["dead"] = world.IsDead, ["hp"] = world.CurrentHp, ["position"] = session.CurrentPosition,
									});
									break;
								}
							}
							// The spirits stand in three rooms behind closed doors. Work one room at a time, the nearest first: click its
							// door open from the hall, then take its spirits from the nearest on.
							NaturalAbyssSpiritGroup? group = arena.Groups.Where(entry => !searchedGroups.Contains(entry.Key))
								.OrderBy(entry => Distance(session.CurrentPosition, At(entry.DoorStand))).FirstOrDefault();
							Require.True(group != null, $"Every room was cleared with {ArenaKills()} of {arena.RequiredKills} spirits counted.");
							if (!openedDoors.Contains(group!.DoorId))
							{
								NaturalNavigationResult atDoor = await NaturalIshalgenNavigator.ExploreAnchorAsync(arena.MapId, 0, At(group.DoorStand), navigator,
									"arena-door", token);
								float fromDoor = Distance(session.CurrentPosition, At(group.DoorPosition));
								Require.True(fromDoor <= 8, $"The Cleric is {fromDoor:F1} m from door {group.DoorId} of the {group.Key} room: {atDoor.Reason}");
								await session.SendPacketAsync(GameClientPackets.OpenStaticDoor(group.DoorId), token);
								await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet => packet.Get<int>("senderObjectId") == group.DoorId &&
									packet.Get<byte>("emotionType") == (byte)Aion.GameServer.Model.EmotionType.OPEN_DOOR);
								await session.SynchronizeAsync(token);
								openedDoors.Add(group.DoorId);
								session.TraceDiagnostic("arena-door-opened", new Dictionary<string, object?>
								{
									["room"] = group.Key, ["doorId"] = group.DoorId, ["metresFromDoor"] = fromDoor, ["position"] = session.CurrentPosition,
									["secondsLeft"] = (arenaDeadline - runtime.NowMillis) / 1000,
								});
								break;
							}
							BotKnownObject? spirit = world.Objects.Values.Where(known => known.Kind == BotKnownObjectKind.Npc && !known.IsCorpse &&
									known.TemplateId is int id && spiritIds.Contains(id) && !navigator.UnavailableObjects.Contains(known.ObjectId) &&
									Distance(known.Position, At(group.Center)) <= 30)
								.OrderBy(known => Distance(session.CurrentPosition, known.Position)).FirstOrDefault();
							if (spirit == null)
							{
								// The room is empty, or its last spirits are ones the fight could not finish: the next room.
								searchedGroups.Add(group.Key);
								session.TraceDiagnostic("arena-room-cleared", new Dictionary<string, object?>
								{
									["room"] = group.Key, ["counted"] = ArenaKills(), ["secondsLeft"] = (arenaDeadline - runtime.NowMillis) / 1000,
								});
								break;
							}
							if (Distance(session.CurrentPosition, spirit.Position) > NaturalPullPlanner.SpellRange + 8)
							{
								int sought = spirit.ObjectId;
								NaturalNavigationResult toward = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(arena.MapId, spirit.TemplateId!.Value,
									spirit.Position, NaturalPullPlanner.SpellRange, navigator, "arena-spirit", token,
									stopWhen: () => world.IsDead || !world.Objects.TryGetValue(sought, out BotKnownObject? seen) || seen.IsCorpse ||
										Distance(session.CurrentPosition, seen.Position) <= NaturalPullPlanner.SpellRange + 4);
								// A walk that did not bring the spirit into range is not tried again in this try.
								if (world.Objects.TryGetValue(sought, out BotKnownObject? still) && !still.IsCorpse &&
									Distance(session.CurrentPosition, still.Position) > NaturalPullPlanner.SpellRange + 8)
								{
									navigator.UnavailableObjects.Add(sought);
									session.TraceDiagnostic("arena-spirit-unreached", new Dictionary<string, object?>
									{
										["objectId"] = sought, ["reason"] = toward.Reason, ["position"] = session.CurrentPosition,
									});
								}
								break;
							}
							int killsBefore = ArenaKills();
							bool killed;
							combat.ScriptedTrial = true;
							try { killed = await combat.TryKillAsync(spirit.ObjectId, token, retreatAnchor: session.CurrentPosition); }
							finally { combat.ScriptedTrial = false; }
							await session.SynchronizeAsync(token);
							arenaKillsSeen = Math.Max(arenaKillsSeen, ArenaKills());
							// Killed, or one the fight could not finish: either way it is not chosen again in this try. A spirit leaves no
							// loot, so the client keeps its body in view, unmarked, until the server removes it.
							navigator.UnavailableObjects.Add(spirit.ObjectId);
							session.TraceDiagnostic("arena-kill", new Dictionary<string, object?>
							{
								["try"] = arenaTry?.Number, ["room"] = group.Key, ["npcId"] = spirit.TemplateId, ["objectId"] = spirit.ObjectId, ["killed"] = killed,
								["counted"] = ArenaKills(), ["before"] = killsBefore, ["hp"] = world.CurrentHp, ["mp"] = world.CurrentMp, ["map"] = world.MapId,
								["secondsLeft"] = (arenaDeadline - runtime.NowMillis) / 1000,
							});
							if (ArenaKills() >= arena.RequiredKills)
							{
								// The tenth kill ends the timer and plays movie 168; its end teleports the Cleric to Garm. A client that
								// skips movies has answered it inside the fight and followed the teleport already.
								long spare = (arenaDeadline - runtime.NowMillis) / 1000;
								if (world.MapId == arena.MapId)
								{
									await NaturalMovieGate.FinishAsync(session, token);
									await FollowTeleportAsync(arena.DoneTeleport.MapId);
								}
								else
								{
									NaturalMovieGate.RecordSkipped(session);
									Require.Equal(arena.DoneTeleport.MapId, world.MapId ?? 0);
									session.AcceptTeleportPosition();
									EnterObservedMap();
								}
								EndArenaTry(NaturalAbyssAttempts.Done, $"Ten spirits counted with {spare} s to spare.");
							}
							// Off the arena's map below ten kills: died, and revived at the bind. The top of the loop ends the try.
							break;
						}
						case "ring-course-start":
						{
							NaturalAltgardStep step = leg.Steps.Single(entry => entry.Key == next.StepKey);
							// Full flight time and a ready take-off before the clock starts.
							long wait = Math.Max(NaturalFlightPolicy.RestoreMillis(world.CurrentFlightTime, world.MaxFlightTime, world.MaxFlightTime),
								landedMillis + NaturalFlightPolicy.TakeoffReuseMillis + 1_000 - runtime.NowMillis);
							if (wait > 0)
							{
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(wait), token);
								await session.SynchronizeAsync(token);
							}
							int yornduf = await ApproachCapitalNpcAsync(step.NpcId);
							// AX-Q3: one Greater Raging Wind Scroll may be supplied, and is used standing still when the timed flight
							// first starts. It is the leg's approved supply and goes in the run profile like every help item.
							if (scrollsUsed == 0)
							{
								if (ItemCount(world, scrollSupply.ItemId) == 0)
								{
									Func<int, long, CancellationToken, Task> supply = runtime.SupplyHelpItemAsync
										?? throw new InvalidDataException("This run supplies no help items, so the flight-speed scroll is not supplied.");
									NaturalHelpItemSupply.RequireApproved(scrollSupply.ItemId, 1, leg.Leg, scrollsSupplied);
									await supply(scrollSupply.ItemId, 1, token);
									await session.SynchronizeAsync(token);
									Require.True(ItemCount(world, scrollSupply.ItemId) == 1, "The supplied flight-speed scroll did not arrive.");
									scrollsSupplied++;
									helpSupplied.Add(new NaturalHelpSupplied(scrollSupply.Trigger, scrollSupply.ItemId, scrollSupply.Family, 1, 0, 1, world.Level, runtime.NowMillis));
									session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
									{
										["trigger"] = scrollSupply.Trigger, ["itemId"] = scrollSupply.ItemId, ["family"] = scrollSupply.Family, ["count"] = 1,
										["before"] = 0, ["after"] = 1, ["level"] = world.Level, ["decision"] = scrollSupply.Decision,
									});
									await File.WriteAllTextAsync(Path.Combine(folder, "help-items.json"), NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
								}
								BotInventoryItem scroll = world.Inventory.Values.First(item => item.ItemId == scrollSupply.ItemId);
								var scrollTemplate = runtime.Data.ItemDataDh.GetItemTemplate(scrollSupply.ItemId)
									?? throw new InvalidDataException($"Scroll {scrollSupply.ItemId} has no item template.");
								long owned = ItemCount(world, scrollSupply.ItemId);
								await session.SendPacketAsync(session.Api.UseItem(scroll.ObjectId, scrollTemplate), token);
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(scrollTemplate.GetCastingDelay() + 500), token);
								await session.SynchronizeAsync(token);
								Require.True(ItemCount(world, scrollSupply.ItemId) == owned - 1, "The flight-speed scroll was not used.");
								scrollsUsed++;
								session.TraceDiagnostic("flight-scroll-used", new Dictionary<string, object?>
								{
									["itemId"] = scrollSupply.ItemId, ["left"] = owned - 1, ["position"] = session.CurrentPosition,
								});
							}
							for (int attempt = 1; ; attempt++)
							{
								try
								{
									string outcome = await NaturalAltgardQuestSteps.TalkAsync(session, step, yornduf, token);
									session.TraceDiagnostic("abyss-entry-talk", new Dictionary<string, object?> { ["step"] = step.Key, ["outcome"] = outcome });
									break;
								}
								catch (NaturalDialogTooFarException) when (attempt < 3) { yornduf = await ApproachCapitalNpcAsync(step.NpcId); }
							}
							ringTry = (attempts.Count(attempt => attempt.Kind == NaturalAbyssAttempts.RingCourse) + 1, runtime.NowMillis, ObservedExperience(), combat.ReviveCount);
							ringDeadline = runtime.NowMillis + course.Seconds * 1000L;
							ringsSeen = 0;
							takeoffSpot = session.CurrentPosition;
							session.TraceDiagnostic("ring-course-attempt-started", new Dictionary<string, object?>
							{
								["try"] = ringTry?.Number, ["step"] = step.Key, ["seconds"] = course.Seconds, ["flightTime"] = world.CurrentFlightTime,
								["maxFlightTime"] = world.MaxFlightTime, ["position"] = session.CurrentPosition,
							});
							break;
						}
						case "ring-course-fly":
						{
							int map = leg.Hub.MapId;
							// A try the runner did not see begin (a resumed run): count it from here.
							if (ringTry == null)
							{
								ringTry = (attempts.Count(attempt => attempt.Kind == NaturalAbyssAttempts.RingCourse) + 1, runtime.NowMillis, ObservedExperience(), combat.ReviveCount);
								ringDeadline = runtime.NowMillis + course.Seconds * 1000L;
								ringsSeen = RingsPassed();
							}
							BotPosition spot = takeoffSpot ??= session.CurrentPosition;
							// A recorded outcome on request (AX_RING_FIRST_TRY=timeout), to prove the failure path: the first try stays
							// on the ground until the 70 s are over.
							bool fallOnThisTry = ringTry is { Number: 1 } && options.AbyssRingFirstTry == "death";
							if (ringTry is { Number: 1 } && !fallOnThisTry && options.AbyssRingFirstTry is { } grounded)
							{
								Require.True(grounded == "timeout", $"AX_RING_FIRST_TRY is '{grounded}', not timeout or death.");
								session.TraceDiagnostic("ring-course-first-try-idle", new Dictionary<string, object?> { ["seconds"] = (ringDeadline - runtime.NowMillis) / 1000 });
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(ringDeadline - runtime.NowMillis, 0) + 1_500), token);
								await session.SynchronizeAsync(token);
								Require.Equal(course.FailedVar, RingVar());
								EndRingTry("timeout", $"The {course.Seconds} s ran out on the ground.");
								break;
							}
							BotNavigationGeometry air = runtime.CreateGeometry();
							if (!airborne)
							{
								flightSpeed = await NaturalFlightProtocol.TakeOffAsync(session, token);
								airborne = true;
								session.TraceDiagnostic("ring-course-takeoff", new Dictionary<string, object?>
								{
									["try"] = ringTry?.Number, ["speed"] = flightSpeed, ["flightTime"] = world.CurrentFlightTime,
									["secondsLeft"] = (ringDeadline - runtime.NowMillis) / 1000.0,
								});
							}
							// Never within two metres of the FLY zone's ceiling: leaving the zone ends the flight. Ring 5's centre is
							// 3.44 m under it, and a ring is six metres wide, so the Cleric passes it at its centre or below.
							float ceiling = course.ZoneTop - 2, floor = course.ZoneBottom + 2;
							int misses = 0;
							while (!world.IsDead && RingVar() >= course.StartVar && RingVar() < course.DoneVar && misses < 2)
							{
								// A recorded outcome on request (AX_RING_FIRST_TRY=death), to prove the death path: a fall, the course's own
								// way to die. With the first ring passed the Cleric climbs to sixty metres over the ground below it, ends
								// the flight in the air and falls. The server deals all of the HP from 50 m; Q2042's die hook fails the course.
								if (fallOnThisTry && RingVar() > course.StartVar)
								{
									BotPosition here = session.CurrentPosition;
									// The ground below: the lowest height the server's geometry lets a line from here reach.
									float blocked = course.ZoneBottom - 40, ground = here.Z;
									Require.True(!NaturalFlightProtocol.IsClear(air, map, here, here with { Z = blocked }), "No ground was found below the first ring.");
									for (int halving = 0; halving < 14; halving++)
									{
										float middle = (blocked + ground) / 2;
										if (NaturalFlightProtocol.IsClear(air, map, here, here with { Z = middle })) ground = middle;
										else blocked = middle;
									}
									BotPosition summit = here with { Z = Math.Min(ground + 60, ceiling) };
									Require.True(summit.Z - ground >= 55 && NaturalFlightProtocol.IsClear(air, map, here, summit), "There is no clear 55 m over the ground below the first ring.");
									await NaturalFlightProtocol.FlyAsync(session, map, here, [summit], flightSpeed, token);
									await NaturalFlightProtocol.LandAsync(session, token);
									airborne = false;
									await NaturalFlightProtocol.FallAsync(session, summit, ground, token);
									await session.SynchronizeAsync(token);
									// The server sends SM_DIE half a second after the death.
									if (!world.IsDead && world.CurrentHp == 0)
									{
										await session.AdvanceAsync(TimeSpan.FromMilliseconds(600), token);
										await session.WaitForPacketAsync(typeof(SM_DIE), token);
										await session.SynchronizeAsync(token);
									}
									landedMillis = runtime.NowMillis;
									session.TraceDiagnostic("ring-course-first-try-fall", new Dictionary<string, object?>
									{
										["from"] = summit.Z, ["ground"] = ground, ["metres"] = summit.Z - ground, ["dead"] = world.IsDead, ["var"] = RingVar(),
										["hp"] = world.CurrentHp, ["position"] = session.CurrentPosition,
									});
									Require.True(world.IsDead, "The fall did not kill the Cleric.");
									break; // the rule revives it next, and the top of the loop ends the try
								}
								int index = RingVar() - course.StartVar;
								NaturalAbyssRing ring = course.Rings[index];
								BotPosition origin = session.CurrentPosition;
								BotPosition centre = new(ring.Center[0], ring.Center[1], Math.Clamp(ring.Center[2], floor, ceiling), 0);
								// The way to the ring: straight when nothing is in the line; otherwise around what is, by climbing or sinking
								// to the ring's height first, or over the top. The last stretch is never vertical, so it crosses the ring.
								float top = Math.Min(ceiling, Math.Max(origin.Z, centre.Z) + 20);
								float flat = MathF.Sqrt(MathF.Pow(centre.X - origin.X, 2) + MathF.Pow(centre.Y - origin.Y, 2));
								BotPosition shortOf = flat <= 15 ? origin : new(centre.X - (centre.X - origin.X) / flat * 15, centre.Y - (centre.Y - origin.Y) / flat * 15, top, 0);
								BotPosition[][] ways =
								[
									[],
									[origin with { Z = centre.Z }],
									[origin with { Z = top }, shortOf, shortOf with { Z = centre.Z }],
								];
								bool Clear(BotPosition[] via)
								{
									BotPosition at = origin;
									foreach (BotPosition point in via.Append(centre))
									{
										if (Distance(at, point) > 0.01f && !NaturalFlightProtocol.IsClear(air, map, at, point)) return false;
										at = point;
									}
									return true;
								}
								BotPosition[]? way = ways.FirstOrDefault(Clear);
								bool clear = way != null;
								way ??= [];
								BotPosition last = way.Length == 0 ? origin : way[^1];
								float approach = Distance(last, centre), length = way.Append(centre).Aggregate((At: origin, Metres: 0f),
									(sum, point) => (point, sum.Metres + Distance(sum.At, point))).Metres;
								// Through the centre and four metres on, so the flight crosses the ring's plane whatever its tilt.
								float ux = (centre.X - last.X) / approach, uy = (centre.Y - last.Y) / approach, uz = (centre.Z - last.Z) / approach;
								BotPosition beyond = new(centre.X + 4 * ux, centre.Y + 4 * uy, Math.Clamp(centre.Z + 4 * uz, floor, ceiling), 0);
								await NaturalFlightProtocol.FlyAsync(session, map, origin, [.. way, centre, beyond], flightSpeed, token);
								await session.SynchronizeAsync(token);
								bool passed = RingVar() != course.StartVar + index;
								ringsSeen = Math.Max(ringsSeen, RingVar() == course.FailedVar ? ringsSeen : RingsPassed());
								session.TraceDiagnostic("ring-course-ring", new Dictionary<string, object?>
								{
									["try"] = ringTry?.Number, ["ring"] = index + 1, ["name"] = ring.Name, ["passed"] = passed, ["var"] = RingVar(), ["metres"] = length,
									["clear"] = clear, ["via"] = way, ["flightTime"] = world.CurrentFlightTime, ["secondsLeft"] = (ringDeadline - runtime.NowMillis) / 1000.0,
									["position"] = session.CurrentPosition,
								});
								if (!passed)
								{
									// Not counted: back through the centre from the far side, then on again.
									misses++;
									BotPosition before = new(centre.X - 6 * ux, centre.Y - 6 * uy, Math.Clamp(centre.Z - 6 * uz, floor, ceiling), 0);
									await NaturalFlightProtocol.FlyAsync(session, map, session.CurrentPosition, [centre, before], flightSpeed, token);
									await session.SynchronizeAsync(token);
								}
							}
							if (world.IsDead) break; // the top of the loop ends the try once the Cleric is revived
							// Down to where the Cleric took off, and land.
							BotPosition landing = spot with { Z = spot.Z + 0.5f };
							bool clearDown = NaturalFlightProtocol.IsClear(air, map, session.CurrentPosition, landing);
							await NaturalFlightProtocol.FlyAsync(session, map, session.CurrentPosition, [landing], flightSpeed, token);
							await NaturalFlightProtocol.LandAsync(session, token);
							airborne = false;
							landedMillis = runtime.NowMillis;
							await session.SynchronizeAsync(token);
							session.TraceDiagnostic("ring-course-landed", new Dictionary<string, object?>
							{
								["try"] = ringTry?.Number, ["var"] = RingVar(), ["clear"] = clearDown, ["flightTime"] = world.CurrentFlightTime,
								["secondsLeft"] = (ringDeadline - runtime.NowMillis) / 1000.0, ["position"] = session.CurrentPosition,
							});
							if (RingVar() == course.DoneVar)
								EndRingTry(NaturalAbyssAttempts.Done, $"Six rings passed with {(ringDeadline - landedMillis) / 1000} s on the clock at the landing.");
							else
							{
								// A ring was missed twice, or the timer ran out in the air: the course fails when the 70 s are over.
								if (RingVar() != course.FailedVar)
								{
									await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(ringDeadline - runtime.NowMillis, 0) + 1_500), token);
									await session.SynchronizeAsync(token);
								}
								EndRingTry(misses >= 2 ? "missed-ring" : "timeout", $"The course failed with {ringsSeen} of {course.Rings.Length} rings passed.");
							}
							break;
						}
						case "arena-leave":
						{
							// Inside with nothing to count: out through the exit beside the entry, as a player would.
							BotPosition exit = new(arena.ExitPosition[0], arena.ExitPosition[1], arena.ExitPosition[2], 0);
							NaturalNavigationResult reached = await NaturalIshalgenNavigator.ApproachNpcAsync(arena.MapId, arena.ExitNpcId, exit, navigator, token);
							Require.True(reached.Arrived, $"Arena exit {arena.ExitNpcId}: {reached.Reason}");
							int portal = Require.IsType<int>(reached.TargetObjectId);
							Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, portal, null, token, reloadWorld: true), "The arena exit use was interrupted.");
							// The exit is a teleport to another map: stand where the server put the Cleric before planning any route.
							Require.True(world.MapId != arena.MapId, "The arena exit did not lead out of the arena.");
							session.AcceptTeleportPosition();
							await session.SynchronizeAsync(token);
							EnterObservedMap();
							EndArenaTry(ArenaKills() >= arena.RequiredKills ? NaturalAbyssAttempts.Done : "left", "The Cleric left the arena by its exit.");
							break;
						}
						case "coin-armor":
						{
							// AX-06: the manifest is decided from the client's view and traced before a coin is spent. Coins the Cleric lacks
							// are the leg's approved supply (the AX-Q5 revision), listed in the run profile like every help item.
							NaturalAbyssCoinArmor armor = scope.CoinArmor;
							NaturalAbyssCoinTier tier = NaturalAbyssEntryDecisionEngine.CoinTier(armor, next.Phase);
							NaturalAbyssCoinManifest manifest = NaturalAbyssCoinArmorPolicy.Plan(armor, tier, Observed().Inventory!, PhysicalDefence, StaffMagicBoost);
							coinManifests.Add(manifest);
							session.TraceDiagnostic(NaturalAbyssCoinArmorSteps.ManifestDiagnostic, NaturalAbyssCoinArmorSteps.Row(manifest));
							if (manifest.CoinsToSupply > 0)
							{
								Func<int, long, CancellationToken, Task> supply = runtime.SupplyHelpItemAsync
									?? throw new InvalidDataException($"The manifest is {manifest.CoinsToSupply} Bronze Coins short and this run supplies no help items.");
								NaturalHelpLegSupply approved = NaturalHelpItemAllowlist.LegApproved.Single(entry => entry.Leg == leg.Leg && entry.ItemId == armor.CoinItemId);
								NaturalHelpItemSupply.RequireApproved(armor.CoinItemId, manifest.CoinsToSupply, leg.Leg, coinsSupplied);
								long owned = ItemCount(world, armor.CoinItemId);
								await supply(armor.CoinItemId, manifest.CoinsToSupply, token);
								await session.SynchronizeAsync(token);
								long after = ItemCount(world, armor.CoinItemId);
								Require.True(after == owned + manifest.CoinsToSupply, $"The supplied Bronze Coins did not arrive: {owned} -> {after}.");
								coinsSupplied += manifest.CoinsToSupply;
								helpSupplied.Add(new NaturalHelpSupplied($"coin-armor-{tier.Level}", armor.CoinItemId, approved.Family, manifest.CoinsToSupply, owned,
									after, world.Level, runtime.NowMillis));
								session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
								{
									["trigger"] = $"coin-armor-{tier.Level}", ["itemId"] = armor.CoinItemId, ["family"] = approved.Family,
									["count"] = manifest.CoinsToSupply, ["before"] = owned, ["after"] = after, ["level"] = world.Level, ["decision"] = approved.Decision,
								});
								await File.WriteAllTextAsync(Path.Combine(folder, "help-items.json"), NaturalHelpItemSupply.ProfileJson(helpSupplied), token);
							}
							int vendor = await ApproachCapitalNpcAsync(armor.VendorNpcId);
							coinPurchases.AddRange(await NaturalAbyssCoinArmorSteps.BuyAsync(session, runtime.Data, armor, manifest, vendor, token));
							await InventoryCheckAsync($"coin-armor-{tier.Level}", turnIn: false);
							break;
						}
						case "refresh-observation":
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
							break;
						case "revive":
							await RestSafelyAsync(token);
							break;
						case "travel":
						{
							NaturalAltgardMapTrip trip = leg.MapTripList.Single(entry => entry.MapId == next.MapId && entry.FromMapId == world.MapId);
							int teleporter = await ApproachCapitalNpcAsync(trip.TeleporterNpcId);
							long before = world.Kinah;
							NaturalServiceOutcome travelled = await services.TeleportAsync(teleporter, world.Objects[teleporter].Position, trip.TalkRange,
								trip.LocationId, trip.Fare, trip.MapId, token);
							Require.True(travelled.IsDone, travelled.Reason);
							fares += before - world.Kinah;
							break;
						}
						case "bind":
						{
							NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
							int stone = await ApproachCapitalNpcAsync(bind.NpcId);
							long before = world.Kinah;
							NaturalServiceOutcome bound = await services.BindAsync(stone, world.Objects[stone].Position, leg.Hub.MapId, bind.Price,
								bind.AcceptRange, token);
							Require.True(bound.IsDone, bound.Reason);
							bindPaid += before - world.Kinah;
							break;
						}
						case "talk":
						{
							NaturalAltgardStep step = leg.Steps.Single(entry => entry.Key == next.StepKey);
							long xp = ObservedExperience(), kinah = world.Kinah;
							bool wasComplete = world.CompletedQuestIds.Contains(step.QuestId);
							for (int attempt = 1; ; attempt++)
							{
								try
								{
									int npc = await ApproachCapitalNpcAsync(step.NpcId);
									xp = ObservedExperience();
									kinah = world.Kinah;
									long experienceBeforeTalk = ObservedExperience();
									string outcome = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
									session.TraceDiagnostic("abyss-entry-talk", new Dictionary<string, object?> { ["step"] = step.Key, ["outcome"] = outcome });
									if (step.Teleport?.MapId == arena.MapId)
									{
										// Garm sent the Cleric in (D35). Java starts the 240 s on this world entry and again when the client
										// reports the end of movie 167 (hazard 5): the try's clock runs from the later one.
										arenaTry = (attempts.Count(attempt => attempt.Kind == NaturalAbyssAttempts.Arena) + 1, runtime.NowMillis, experienceBeforeTalk, combat.ReviveCount);
										arenaKillsSeen = 0;
										searchedGroups.Clear();
										openedDoors.Clear();
										EnterObservedMap();
										await NaturalMovieGate.FinishAsync(session, token);
										await session.SynchronizeAsync(token);
										arenaDeadline = runtime.NowMillis + arena.Seconds * 1000L;
										session.TraceDiagnostic("arena-attempt-started", new Dictionary<string, object?>
										{
											["try"] = arenaTry?.Number, ["step"] = step.Key, ["position"] = session.CurrentPosition, ["seconds"] = arena.Seconds,
											["hp"] = world.CurrentHp, ["mp"] = world.CurrentMp,
										});
									}
									break;
								}
								catch (NaturalDialogTooFarException) when (attempt < 3) { }
							}
							if (!wasComplete && world.CompletedQuestIds.Contains(step.QuestId))
							{
								var payment = new NaturalAbyssPayment(step.QuestId, ObservedExperience() - xp, world.Kinah - kinah, world.Level);
								payments.Add(payment);
								session.TraceDiagnostic("abyss-entry-payment", new Dictionary<string, object?>
								{
									["quest"] = payment.QuestId, ["experience"] = payment.Experience, ["kinah"] = payment.Kinah, ["level"] = payment.Level,
								});
								await InventoryCheckAsync($"turn-in-q{step.QuestId}");
							}
							break;
						}
						default:
							throw new InvalidDataException($"The Abyss-entry leg cannot go on ({next.Action}): {next.Reason}");
					}
					// The approved help kit is by level band: top it up when a turn-in brings a new level.
					if (world.Level != helpCheckedAtLevel && !world.IsDead && !airborne) await TopUpHelpItemsAsync("level-up");
				}
				throw new TimeoutException("The Abyss-entry leg exceeded 200 decisions.");
			}

			async Task RunAltgardLeg1Async()
			{
				NaturalAltgardContract leg = altgardLeg ?? throw new InvalidOperationException("An Altgard leg needs its contract.");
				IReadOnlySet<int>? only = options.AltgardOnlyQuests?.ToHashSet();
				Require.True(combat.IsLineSecondClass, $"An Altgard leg needs the {ClassLine.SecondName}; the character is {combat.ObservedCharacter}.");
				// NL-3: a round that goes on from a world where a bot stopped on a trip of its leg (the five in Morheim at
				// Choirinererk's spot, nl3-rb-c1) starts there; the decision engine plays the steps of the map it stands on.
				Require.True(session.Api.World.MapId == leg.Hub.MapId || leg.Haramel?.MapId == session.Api.World.MapId || leg.Destiny?.AllowedMaps.Contains(session.Api.World.MapId ?? 0) == true ||
					leg.MapTripList.Any(trip => trip.MapId == session.Api.World.MapId),
					"The retained character is outside the approved leg maps.");
				NaturalJourneyNavigator here = mapNavigators.Enter(LegMapKey());
				here.DefendOnAttackAsync = navigator.DefendOnAttackAsync;
				here.AvoidHostileAggro = true;
				navigator = here;
				geometry = runtime.CreateGeometry();
				contract = contract with { MapId = session.Api.World.MapId!.Value };
				// NL-3: the FLY zones of the map the bot stands on (EnterLegMap keeps them), not only the hub's.
				var flyZonesByMap = new Dictionary<int, IReadOnlyList<NaturalFlyZone>>();
				IReadOnlyList<NaturalFlyZone> FlyZonesOf(int mapId)
				{
					if (!flyZonesByMap.TryGetValue(mapId, out IReadOnlyList<NaturalFlyZone>? known))
						flyZonesByMap[mapId] = known = NaturalFlyZone.ForMap(runtime.RepoRoot, runtime.Data.WorldMaps2.GetTemplate(mapId), mapId);
					return known;
				}
				IReadOnlyList<NaturalFlyZone> zones = FlyZonesOf(contract.MapId);
				var flightRings = new NaturalFlightRings(runtime.RepoRoot);
				// NL-3: the spawn spots of a map's aggressive monsters, which a hop's stone keeps away from.
				var hostileByMap = new Dictionary<int, BotPosition[]>();
				BotPosition[] HostileSpotsOf(int mapId)
				{
					if (!hostileByMap.TryGetValue(mapId, out BotPosition[]? known))
						hostileByMap[mapId] = known = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(mapId)
							.Where(group => runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId())))
							.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))];
					return known;
				}
				// NL-a (docs/natural-leg-36.md): a Cleric companion goes with the bot on its solo quests.
				if (options.SoloCompanion is string soloCompanion) await AskSoloCompanionAsync(soloCompanion, token);
				combat = new NaturalJourneyCombat(session, here, runtime, geometry, stopOnDeath: false, options.OptimizeHubs, mauPolicy,
					ClassLine, continuousAltgard ? combat.ReviveCount : haramelProgress?.Revives ?? 0)
				{
					ApproachMapId = contract.MapId,
				};
				if (leg.Destiny != null || leg.Haramel != null || leg.InstanceRooms != null) combat.AfterBindRevive = () => EnterLegMap(newEntry: true);
				here.AvoidSpots = combat.DeathSpots;
				navigationDefense = combat;
				WithQuestLoot(combat);
				var haramelHints = new HashSet<(int Id, BotPosition At)>();
				int haramelKillHistoryStart = session.PacketHistory.Count;
				int haramelEntryHistoryStart = haramelKillHistoryStart;
				IReadOnlyDictionary<int, NaturalTemplateObjective> objectives = NaturalTemplateObjective.From(altgardPlans);
				// A quest the leg was written holding that has been handed in since is no longer the leg's to keep: the campaign's
				// rest in M6 started from a world where Q2319 and Q2356, held at the leg's own start, were done in M5.
				var preservedQuests = (leg.Start.StartedQuestIds ?? []).Concat(leg.Start.LockedQuestIds).Except(leg.Order)
					.Where(id => !session.Api.World.CompletedQuestIds.Contains(id))
					.ToDictionary(id => id, id => session.Api.World.Quests.TryGetValue(id, out BotQuestState? state)
						? (state.Status, state.StepAndFlags) : throw new InvalidDataException($"Incoming Q{id} is missing."));
				// Leg 1's flight: Borender's rock is the landing (AF-05); a leg with no flight step never flies.
				NaturalAltgardStep? borender = leg.Steps.FirstOrDefault(step => step.Flight);
				BotPosition? rockTop = borender == null ? null : geometry.SnapToGround(leg.Hub.MapId, new BotPosition(borender.Position[0] - 2.5f,
					borender.Position[1], borender.Position[2] + 3, 0)) ?? throw new InvalidDataException("No rock top beside Borender.");
				float[] home = leg.Town?.Anchor ?? leg.Hub.Anchor;
				BotPosition ground = geometry.SnapToGround(leg.Hub.MapId, new BotPosition(home[0] - 3, home[1], home[2] + 1, 0))
					?? throw new InvalidDataException("No ground beside the Altgard obelisk.");
				float cruise = (rockTop?.Z ?? 0) + 8;
				BotPosition Rock() => rockTop ?? throw new InvalidDataException($"{leg.Leg} has no flight landing.");
				long? lastTakeoff = null;
				// NL-3: the speed the server announced with the last takeoff (a hop's range is flight time by speed).
				float lastFlightSpeed = 9f;
				// NL-3: what this leg's approved supply has given so far, by item (its limits count the whole leg).
				var legItemsSupplied = new Dictionary<int, long>();
				// The operator, 2026-10-13: "If stuck on lake, can always FLY back."
				flyOutOfStrand = leg.Flight == null ? null : async () =>
				{
					NaturalFlightDecision able = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, session.CurrentPosition, false,
						leg.Flight.WaterLevel, runtime.NowMillis, lastTakeoff, false, false, false), zones);
					if (!able.Allowed) return false;
					// NL-3: in Reshanta the hub lies farther than one flight from the islands (the Gladiator, 1544 m from Primum
					// on Gjalf's island): the hops of a fly-zone crossing.
					if (flyAcross != null && Distance(session.CurrentPosition, ground) >
						(session.Api.World.MaxFlightTime - NaturalFlightPolicy.LandingReserveFp - 5) * lastFlightSpeed)
						return await flyAcross(ground);
					float? height = new float?[] { null, MathF.Max(cruise, session.CurrentPosition.Z) + 25 }.FirstOrDefault(over =>
						NaturalFlightProtocol.Plan(geometry, leg.Hub.MapId, session.CurrentPosition, ground, over ?? cruise).IsUsable, -1);
					if (height == -1) return false;
					// NL-3: a flight the speed of its takeoff cannot carry is a crossing by hops instead.
					return await FlyToAsync(ground, height, mayRefuse: true) || flyAcross != null && await flyAcross(ground);
				};
				// The operator, 2026-10-14: "around Fire temple is a FLY ZONE". Ground the navmesh cannot walk to from here but a
				// FLY zone holds (Sarinerk's ledge, the crater floor, the Fire Temple's gate) is reached as a player reaches it: the
				// zone's ground nearest the goal on foot, a flight, and a landing beside the goal; and out the same way.
				bool crossingFlyZone = false;
				int flyDowns = 0;
				bool approachingAirline = false;
				flyAcross = leg.Flight == null || altgardLegId == null || !NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) ? null : async goal =>
				{
					// NL-3: on whichever map the bot stands (the Abyss Gate stands in Morheim's fortress for a leg from Brusthonin).
					if (crossingFlyZone || session.Api.World.MapId is not int map || session.Api.World.IsDead) return false;
					// NL-4: the Sky Temple Interior has no FLY zone; its floors are glided down to instead.
					if (leg.InstanceRooms?.MapId == map) return false;
					// NL-3: Reshanta's upper layer lies 1,200 m above the lower, past what one flight climbs; a flight transporter
					// carries the bot between them as players go (Mure up to Hwiath, Hwiath down), the one whose pad lies on the
					// bot's layer and whose landing on the goal's, nearest the two, and of the bot's own race (the Elyos have their
					// own, Echo to Theon, which do not talk to an Asmodian). The way to its pad and from its landing is this crossing
					// again.
					if (!approachingAirline && MathF.Abs(goal.Z - session.CurrentPosition.Z) > 600 && airlines
						.Where(route => route.MapId == map &&
							runtime.Data.NpcDataDh.GetNpcTemplate(route.NpcId)?.GetRace().ToString() == leg.Start.Race &&
							MathF.Abs(route.Departure.Z - session.CurrentPosition.Z) < 400 &&
							MathF.Abs(route.Landing.Z - goal.Z) < 400)
						.OrderBy(route => Distance(session.CurrentPosition, route.Departure) + Distance(route.Landing, goal))
						.FirstOrDefault() is { } lift)
					{
						session.TraceDiagnostic("fly-zone-lift", new Dictionary<string, object?>
						{
							["route"] = lift.Route, ["npc"] = lift.NpcId, ["from"] = session.CurrentPosition, ["goal"] = goal,
						});
						approachingAirline = true;
						try
						{
							int transporter = await ApproachShippedSpawnAsync(lift.NpcId);
							if (session.Api.World.IsDead) return false;
							NaturalServiceOutcome lifted = await new NaturalServiceSteps(session).FlyAsync(transporter,
								session.Api.World.Objects[transporter].Position, 6, lift, token);
							Require.True(lifted.IsDone, lifted.Reason);
						}
						finally { approachingAirline = false; }
						if (Distance(session.CurrentPosition, goal) <= 8) return true;
					}
					// Ground the bot can walk to lies within a few metres of the goal: no flight. A goal on no island of the mesh
					// (Sarinerk past his ledge's edge) is near no walkable ground of this one.
					if (geometry.IslandGround(map, session.CurrentPosition, at =>
						MathF.Abs(at.X - goal.X) <= 8 && MathF.Abs(at.Y - goal.Y) <= 8 && MathF.Abs(at.Z - goal.Z) <= 6).Count > 0) return false;
					bool Open(NaturalFlyZone zone, BotPosition at) => zone.Contains(at.X, at.Y, at.Z + 2) &&
						!zones.Any(other => other.Forbids && other.Contains(at.X, at.Y, at.Z + 2));
					NaturalFlyZone? zone = zones.FirstOrDefault(entry => !entry.Forbids && Open(entry, goal))
						?? zones.FirstOrDefault(entry => !entry.Forbids && Open(entry, session.CurrentPosition));
					if (zone == null) return false;
					bool Inside(BotPosition at) => Open(zone, at) && at.Z + 12 < zone.Top;
					// The landing: the zone's ground on the goal's island nearest it, or the goal's own static ground when the
					// navmesh has none there (Sarinerk stands past the edge of his ledge's mesh).
					BotPosition? landing = geometry.IslandGround(map, goal, Inside).OrderBy(at => Distance(at, goal)).Cast<BotPosition?>().FirstOrDefault();
					if (landing is not { } near || Distance(near, goal) > 6) landing = Inside(goal) ? geometry.StaticGroundAt(map, goal) ?? landing : landing;
					if (landing is not { } land) return false;
					// The takeoff: here inside the zone, or the zone's ground on this island nearest the landing. NL-3: when no checked
					// flight leaves from that place, the nearest ground of this island inside the zone from which one does: under
					// the Morheim fortress's roof, by its teleporter, the climb to the Abyss Gate's platform is blocked.
					BotPosition here = session.CurrentPosition;
					float HeightFrom(BotPosition at) => MathF.Min(zone.Top - 6, MathF.Max(at.Z, land.Z) + 15);
					bool Flies(BotPosition at) => NaturalFlightProtocol.Plan(geometry, map, at, land, HeightFrom(at)).IsUsable;
					// NL-3: how far one flight takes the bot (its flight time by its speed, less the landing reserve).
					float Range() => MathF.Max(60, (session.Api.World.MaxFlightTime - NaturalFlightPolicy.LandingReserveFp - 5) * lastFlightSpeed);
					IReadOnlyList<BotPosition> ground = geometry.IslandGround(map, here, Inside);
					BotPosition? takeoff = Inside(here) ? here : ground.OrderBy(at => Distance(at, land)).Cast<BotPosition?>().FirstOrDefault();
					// A landing past one flight is reached by hops (below), from here or the nearest ground with open sky above it
					// (the bot stands under Primum's roofs by Jebal); only a near one looks for another takeoff.
					bool SkyAbove(BotPosition at) => NaturalFlightProtocol.IsClear(geometry, map, at, at with { Z = MathF.Min(zone.Top - 6, at.Z + 30) });
					if (takeoff is { } open && Distance(open, land) > Range() && !SkyAbove(open))
					{
						BotPosition[] nearby = [.. ground.Where(at => Distance(at, here) <= 200).OrderBy(at => Distance(at, here))];
						int spread = Math.Max(1, nearby.Length / 200);
						takeoff = nearby.Where((_, index) => index % spread == 0).Where(SkyAbove).Cast<BotPosition?>().FirstOrDefault() ?? open;
					}
					if (takeoff is { } first && Distance(first, land) <= Range() && !Flies(first))
					{
						// The fortress's courtyard lies under roofs 4 m up, and the teleporter's pad is a scrap of mesh of its own: the
						// ground of every island within 150 m of the landing, nearest to the bot first, about 150 of it spread
						// evenly (the open ground by the obelisk is 40 m from the gate). The walk there steps off the scrap.
						BotPosition[] nearLanding = [.. geometry.GroundNear(map, land, 150, Inside).Where(at => Distance(at, here) <= 200)
							.OrderBy(at => Distance(at, here))];
						int every = Math.Max(1, nearLanding.Length / 150);
						BotPosition[] tried = [.. nearLanding.Where((_, index) => index % every == 0)];
						// NL-3: the bot's own island first. Horu stands on a platform of Beluslan Fortress that only a flight joins
						// to the ground, and a takeoff on the ground below could not be walked to (nl3-rb-d1).
						takeoff = ground.OrderBy(at => Distance(at, here)).Take(150)
							.Where(at => NaturalFlightProtocol.DirectlyClear(geometry, map, at, land, HeightFrom(at))).Cast<BotPosition?>().FirstOrDefault()
							?? tried.Where(at => NaturalFlightProtocol.DirectlyClear(geometry, map, at, land, HeightFrom(at)))
							.Cast<BotPosition?>().FirstOrDefault() ?? first;
						session.TraceDiagnostic("fly-zone-takeoff-search", new Dictionary<string, object?>
						{
							["ownIsland"] = ground.Count, ["nearLanding"] = nearLanding.Length, ["tried"] = tried.Length, ["found"] = takeoff,
						});
					}
					session.TraceDiagnostic("fly-zone-crossing", new Dictionary<string, object?>
					{
						["zone"] = zone.Name, ["goal"] = goal, ["position"] = here, ["takeoff"] = takeoff, ["landing"] = land,
					});
					if (takeoff is not { } off) return false;
					// NL-3: Horu's platform in Beluslan Fortress has no way down on foot and no clear flight to a far landing, and the
					// takeoff chosen lies on another island (nl3-rb-d2, d4). The bot flies down to the ground of another island a
					// checked flight reaches from here, on the takeoff's island and nearest first (it came up 75 m from the courtyard
					// below; ground of any other island led it from scrap to scrap of the platform, nl3-rb-d6), and crosses from there.
					if (Distance(here, off) > 3 && !geometry.OnSameIsland(map, here, off) && Inside(here) && flyDowns < 2 &&
						geometry.GroundNear(map, here, 200, Inside).Where(at => geometry.OnSameIsland(map, at, off))
							.OrderBy(at => Distance(at, here)).Take(40)
							.SelectMany(at => new[] { (At: at, Height: MathF.Max(here.Z, at.Z) + 3), (At: at, Height: HeightFrom(here)) })
							.FirstOrDefault(down => NaturalFlightProtocol.Plan(geometry, map, here, down.At, down.Height).IsUsable) is { At: var downAt } downTo &&
						downAt != default)
					{
						session.TraceDiagnostic("fly-zone-fly-down", new Dictionary<string, object?>
						{
							["from"] = here, ["to"] = downTo.At, ["height"] = downTo.Height, ["takeoff"] = off,
						});
						flyDowns++;
						if (!await FlyToAsync(downTo.At, downTo.Height, mayRefuse: true) || session.Api.World.IsDead) return false;
						return await flyAcross!(goal);
					}
					crossingFlyZone = true;
					try
					{
						if (Distance(here, off) > 3 && (!await WalkRoadDefendingAsync(off, "fly-zone-takeoff", within: 3) ||
							session.Api.World.IsDead || !Inside(session.CurrentPosition))) return false;
						// NL-3: Reshanta's islands stand farther apart than one flight's time: the bot hops by the island ground in
						// range nearest the landing, lands, lets its flight time come back (FlyToAsync waits for it) and flies on.
						BotPosition[] hostile = HostileSpotsOf(map);
						// NL-3 (the operator, 2026-10-17): a landing past every stone (Tigraki Island, 705 m from any other island) is
						// flown to on Greater Wind Serums, drunk on the move once their whole restore fits, with a flight speed scroll
						// read on the ground first. Both are the leg's approved supply; a leg without it does not try.
						bool SerumsApproved() => runtime.SupplyHelpItemAsync != null &&
							NaturalHelpItemAllowlist.LegApproved.Any(entry => entry.Leg == altgardLegId && entry.Family == "wind-serum");
						async Task<bool> SerumCrossingAsync(BotPosition from, BotPosition target, float height)
						{
							if (NaturalHelpItemAllowlist.LegApproved.FirstOrDefault(entry => entry.Leg == altgardLegId && entry.Family == "wind-serum") is not { } serumApproval ||
								runtime.SupplyHelpItemAsync is not { } supply || !NaturalFlightProtocol.Plan(geometry, map, from, target, height).IsUsable)
								return false;
							async Task<BotInventoryItem?> SupplyAsync(NaturalHelpLegSupply approval, long wanted)
							{
								long owned = ItemCount(session.Api.World, approval.ItemId);
								if (owned < wanted)
								{
									long already = legItemsSupplied.GetValueOrDefault(approval.ItemId);
									NaturalHelpItemSupply.RequireApproved(approval.ItemId, wanted - owned, altgardLegId, already);
									await supply(approval.ItemId, wanted - owned, token);
									await session.SynchronizeAsync(token);
									legItemsSupplied[approval.ItemId] = already + wanted - owned;
									session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
									{
										["trigger"] = "serum-crossing", ["itemId"] = approval.ItemId, ["family"] = approval.Family, ["count"] = wanted - owned,
										["before"] = owned, ["after"] = ItemCount(session.Api.World, approval.ItemId), ["decision"] = approval.Decision,
									});
								}
								return session.Api.World.Inventory.Values.FirstOrDefault(item => item.ItemId == approval.ItemId);
							}
							var serumTemplate = runtime.Data.ItemDataDh.GetItemTemplate(serumApproval.ItemId)
								?? throw new InvalidDataException($"Serum {serumApproval.ItemId} has no item template.");
							await SupplyAsync(serumApproval, 4);
							if (NaturalHelpItemAllowlist.LegApproved.FirstOrDefault(entry => entry.Leg == altgardLegId && entry.Family == "flight-speed") is { } scrollApproval &&
								await SupplyAsync(scrollApproval, 1) is { } scroll && runtime.Data.ItemDataDh.GetItemTemplate(scrollApproval.ItemId) is { } scrollTemplate)
							{
								await session.SendPacketAsync(session.Api.UseItem(scroll.ObjectId, scrollTemplate), token);
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(scrollTemplate.GetCastingDelay() + 500), token);
								await session.SynchronizeAsync(token);
							}
							// Skill 9911 restores 12 flight time a skill level (procfphealinstant delta 12); the Greater serum is level 3.
							int restore = 12 * 3;
							long lastDrink = long.MinValue / 2;
							async Task DrinkWhenDueAsync()
							{
								BotWorldModel world = session.Api.World;
								if (world.CurrentFlightTime > world.MaxFlightTime - restore || runtime.NowMillis - lastDrink < 54_000 ||
									world.Inventory.Values.FirstOrDefault(item => item.ItemId == serumApproval.ItemId) is not { } serum) return;
								int before = world.CurrentFlightTime;
								await session.SendPacketAsync(session.Api.UseItem(serum.ObjectId, serumTemplate), token);
								lastDrink = runtime.NowMillis;
								session.TraceDiagnostic("wind-serum-drunk", new Dictionary<string, object?>
								{
									["fp"] = before, ["position"] = session.CurrentPosition, ["left"] = ItemCount(world, serumApproval.ItemId) - 1,
								});
							}
							int serums = (int)Math.Min(ItemCount(session.Api.World, serumApproval.ItemId), 2);
							session.TraceDiagnostic("serum-crossing", new Dictionary<string, object?>
							{
								["from"] = session.CurrentPosition, ["target"] = target, ["landing"] = land, ["serums"] = serums, ["extraFp"] = restore * serums,
							});
							return await FlyToAsync(target, height, mayRefuse: true, extraFp: restore * serums, inFlight: DrinkWhenDueAsync);
						}
						// A ring chain can carry the whole crossing without landing on intermediate islands or drinking serums.
						if (map == NaturalFlightRings.AbyssMap && await FlyToAsync(land, HeightFrom(session.CurrentPosition), mayRefuse: true)) return true;
						for (int hop = 0; ; hop++)
						{
							if (session.Api.World.MapId != map || session.Api.World.IsDead || !Inside(session.CurrentPosition)) return false;
							// On a stone the bot waits for its flight time as a player does, answering what attacks it: the Cleric sat
							// out the wait beside Sinjah's tower while ashikar assaulters killed it.
							long refill = NaturalFlightPolicy.RestoreMillis(session.Api.World.CurrentFlightTime, session.Api.World.MaxFlightTime,
								session.Api.World.MaxFlightTime);
							if (hop > 0 && refill > 0 && !await WaitBeforePullDefendingAsync((int)refill + 100, "fly-hop-refill")) return false;
							// Soul Sickness slows a flight for its short while after a revive: a player waits it out before a long crossing.
							if (session.Api.World.VisibleEffects?.FirstOrDefault(effect => effect.SkillId == 8291) is { RemainingMillis: > 0 } sick &&
								Distance(session.CurrentPosition, land) > Range() &&
								!await WaitBeforePullDefendingAsync((int)Math.Min(sick.RemainingMillis + 1000, 600_000), "fly-hop-soul-sickness")) return false;
							BotPosition from = session.CurrentPosition;
							float height = HeightFrom(from);
							float range = Range();
							NaturalFlightRoute? route = Distance(from, land) <= range ? NaturalFlightProtocol.Plan(geometry, map, from, land, height) : null;
							if (route is { IsUsable: true } && route.Meters <= range && await FlyToAsync(land, height, mayRefuse: true)) return true;
							if (hop >= 12 || session.Api.World.IsDead) return false;
							if (route is { IsUsable: true } && route.Meters <= range) continue;
							float Across(BotPosition a, BotPosition b) => MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
							float Over(BotPosition a, BotPosition b, float rise) => MathF.Min(zone.Top - 6, MathF.Max(a.Z, b.Z) + rise);
							// What the flight to a stone flies: the climb, the cruise and the descent (the fourth hop toward Amundi was
							// 587 m across and 790 m flown).
							float Flown(BotPosition a, BotPosition b, float rise) => Across(a, b) + 2 * Over(a, b, rise) - a.Z - b.Z;
							// The stone: island ground nearer the landing whose flight fits one flight time, nearest the landing first,
							// confirmed by its planned route; from Ganglot's island nothing clears 15 m over the higher end, so a higher
							// cruise is tried next. It has open sky above it: a ledge 100 m under Primum is nearest Primum's landing
							// and no flight leaves it upward. Ground 30 m or more from every aggressive monster's spawn comes first.
							(BotPosition At, float Rise)? FindStone(float reach)
							{
								foreach ((bool calm, float rise) in new[] { (true, 15f), (true, 60f), (false, 15f), (false, 60f) })
									foreach (BotPosition at in geometry.GroundNear(map, from, reach, Inside)
										.Where(at => Across(at, land) + 30 < Across(from, land) && Flown(from, at, rise) <= reach * 0.95f)
										.OrderBy(at => Across(at, land)).Take(400)
										.Where(at => (!calm || !hostile.Any(spot => Distance(spot, at) < 30)) && SkyAbove(at) &&
											NaturalFlightProtocol.DirectlyClear(geometry, map, from, at, Over(from, at, rise))).Take(4))
									{
										NaturalFlightRoute planned = NaturalFlightProtocol.Plan(geometry, map, from, at, Over(from, at, rise));
										if (planned.IsUsable && planned.Meters <= reach) return (at, rise);
									}
								return null;
							}
							(BotPosition At, float Rise)? stone = FindStone(range);
							session.TraceDiagnostic("fly-hop", new Dictionary<string, object?>
							{
								["hop"] = hop, ["from"] = from, ["landing"] = land, ["range"] = range, ["meters"] = route is { IsUsable: true } ? route.Meters : null,
								["stone"] = stone?.At, ["rise"] = stone?.Rise,
							});
							if (stone is not { } next)
							{
								// Past every stone: the landing, or the stone nearest it, within what two serums add to one flight.
								if (!SerumsApproved()) return false;
								float reach = range + 2 * 12 * 3 * lastFlightSpeed;
								(BotPosition At, float Height)? target = Distance(from, land) <= reach ? (land, height)
									: FindStone(reach) is { } far ? (far.At, Over(from, far.At, far.Rise)) : null;
								if (target is not { } go || !await SerumCrossingAsync(from, go.At, go.Height)) return false;
								if (Distance(session.CurrentPosition, land) < 3) return true;
								continue;
							}
							await FlyToAsync(next.At, Over(from, next.At, next.Rise), mayRefuse: true);
						}
					}
					finally { crossingFlyZone = false; }
				};
				var floorGlide = new NaturalFloorGlide(session, () => geometry);
				var fireTravel = leg.InstanceRooms?.Rooms.Any(room => room.PortalNpcId == 730045) == true
					? new NaturalFireRoomTravel(session, runtime, () => geometry) : null;
				combat.RecordedFightRecovery = fireTravel == null ? null : fireTravel.RecoveryForObservedTarget;
				combat.IsRetreatGround = fireTravel == null ? null : fireTravel.IsRetreatGround;
				approachRecordedBulagan = fireTravel == null ? null : async () =>
				{
					if (session.Api.World.MapId != 220020000 ||
						new[] { 182215366, 182215367, 182215368 }.Any(item => ItemCount(session.Api.World, item) < 1)) return null;
					int? Observed() => session.Api.World.Objects.Values.Where(npc => npc.Kind == BotKnownObjectKind.Npc &&
						npc.TemplateId == 204408 && Distance(session.CurrentPosition, npc.Position) <= TalkRange(204408))
						.Select(npc => (int?)npc.ObjectId).FirstOrDefault();
					if (Observed() is int arrived) return arrived;
					// The recording continues after the exit: fly up to Bulagan's platform,
					// land beside him and hand over the three native keys.
					if (!await fireTravel.ReturnToBulaganAsync(WalkFireTravelAsync, SettleFireTravelAsync, token))
						throw new InvalidOperationException("The recorded return flight to Bulagan was not completed safely.");
					return Observed() ?? throw new InvalidOperationException("Bulagan was not observed within his native talk range after the return flight.");
				};
				approachRecordedFireExit = fireTravel == null ? null : async () =>
				{
					if (!fireTravel.OnLowerFloor) return null;
					int? instance = session.Api.World.InstanceId;
					int revives = combat.ReviveCount;
					bool Survived() => fireTravel.OnLowerFloor && session.Api.World.InstanceId == instance &&
						!session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && combat.ReviveCount == revives;
					if (!await fireTravel.ApproachExitAsync(WalkFireGroundAsync, SettleFireTravelAsync, Survived, token))
						throw new InvalidOperationException("The recorded Fire exit walk was not completed safely.");
					return session.Api.World.Objects.Values.Where(npc => npc.Kind == BotKnownObjectKind.Npc && npc.TemplateId == 730046 &&
						Distance(session.CurrentPosition, npc.Position) <= TalkRange(730046)).Select(npc => (int?)npc.ObjectId).FirstOrDefault()
						?? throw new InvalidOperationException("The recorded Fire exit was not observed within its native talk range.");
				};
				recordedFireBossRecovery = fireTravel == null ? null : (start, target, reach) =>
					fireTravel.FindFightRecoveryRoute(fireTravel.BossFight, start, target, reach);
				preferRecordedGlide = fireTravel == null ? null : goal =>
					fireTravel.HandlesDescent(session.Api.World.MapId ?? 0, goal);
				prepareRecordedFireFight = fireTravel == null ? null : async () =>
				{
					if (!fireTravel.OnLowerFloor) return true; // The recorded descent is still ahead.
					int? instance = session.Api.World.InstanceId;
					int revives = combat.ReviveCount;
					bool Survived() => fireTravel.OnLowerFloor && session.Api.World.InstanceId == instance &&
						!session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && combat.ReviveCount == revives;
					return await fireTravel.ClearBeforeBossAsync(WalkFireGroundAsync, SettleFireTravelAsync, async (target, fight) =>
					{
						try
						{
							bool killed = await combat.TryKillAsync(target, token, session.CurrentPosition,
								rangeRecovery: (start, at, reach) => fireTravel.FindFightRecoveryRoute(fight, start, at, reach));
							if (killed) navigator.UnavailableObjects.Add(target);
							return killed;
						}
						catch (NaturalCombatApproachBlockedException blocked)
						{
							// A partial escape can leave the original target out of reach while a helper
							// still attacks nearby. End this pull so ordinary defense can answer that helper.
							session.TraceDiagnostic("fire-recorded-guard-approach-blocked", new Dictionary<string, object?>
							{ ["target"] = target, ["position"] = session.CurrentPosition, ["reason"] = blocked.Message });
							return false;
						}
					}, Survived, token);
				};
				async Task<bool> SettleFireTravelAsync()
				{
					int? map = session.Api.World.MapId;
					int revives = combat.ReviveCount;
					bool Survived() => session.Api.World.MapId == map && !session.Api.World.IsDead &&
						session.Api.World.CurrentHp > 0 && combat.ReviveCount == revives;
					if (!await DefendAgainstEngagedAsync("fire-route-takeoff") || !Survived()) return false;
					await RestSafelyAsync(token);
					if (!Survived()) return false;
					await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, travelMeters: fireTravel?.DescentMeters ?? 0);
					return Survived();
				}
				async Task<bool> WalkFireGroundAsync(BotPosition at)
				{
					int map = session.Api.World.MapId!.Value;
					int? instance = session.Api.World.InstanceId;
					int revives = combat.ReviveCount, stalled = 0;
					bool Survived() => session.Api.World.MapId == map && session.Api.World.InstanceId == instance &&
						!session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && combat.ReviveCount == revives;
					for (int step = 0; step < 80; step++)
					{
						if (!Survived() || !await DefendAgainstEngagedAsync("fire-recorded-ground") || !Survived()) return false;
						BotPosition before = session.CurrentPosition;
						if (Distance(before, at) <= 0.75f) return true;
						// Loot or rest can leave a nearby ledge. Permit a checked ramp back between the
						// current and destination elevations, while a same-floor walk still rejects the raised deck.
						float lowest = MathF.Min(before.Z, at.Z) - 0.75f, highest = MathF.Max(before.Z, at.Z) + 0.75f;
						bool Supported(BotPosition point) => point.Z >= lowest && point.Z <= highest &&
							geometry.StaticGroundAt(map, point) is { } ground && MathF.Abs(ground.Z - point.Z) <= 0.5f;
						// These are demonstrated ground segments through the guards being cleared. Follow them at
						// ordinary speed, fighting actual engagement; a global hazard detour picks the raised deck.
						IReadOnlyList<BotPosition>? path = geometry.TraceEdge(map, before, at);
						if (path == null || path.Any(point => !Supported(point)))
							path = geometry.FindLocalPath(map, before, at);
						path = [.. path.Where(point => Distance(before, point) > 0.05f)];
						if (path.Count == 0 || path.Any(point => !Supported(point)) ||
							Distance(before, path[0]) > 3 || geometry.TraceEdge(map, before, path[0]) == null)
						{
							session.TraceDiagnostic("fire-ground-edge-blocked", new Dictionary<string, object?>
							{ ["position"] = before, ["goal"] = at, ["pathCount"] = path.Count });
							return false;
						}
						if (map == 320050000 && before.X < 140 && before.Y < 130 && before.Z > 605 && at.Z > 605 &&
							combat.ClassProfile.PullStyle != NaturalPullStyle.WalkIn)
						{
							// The recorded player wore GM armor and walked into the first pair. An ordinary
							// ranged class pulls an observed obstruction before entering its aggro circle.
							// Include a jump or drop immediately after this ground waypoint, while there
							// is still room to pull from; waiting until the jump lands draws both guards.
							BotPosition[] corridor = [.. path, .. fireTravel?.UpperAirAfter(at) ?? []];
							NaturalPullMonster? blocker = ObservedPullMonsters()
								.Where(monster => !navigator.UnavailableObjects.Contains(monster.Npc.ObjectId) &&
									Distance(before, monster.Npc.Position) <= combat.ClassProfile.Ranges.SpellRange &&
									geometry.HasLineOfSight(map, before, monster.Npc.Position))
								.Select(monster => (Monster: monster, First: corridor.Select((point, index) => (point, index))
									.Where(sample => new BotNavigationHazard(monster.Npc.Position, monster.AggroRadius + 1).Contains(sample.point))
									.Select(sample => sample.index).DefaultIfEmpty(-1).First()))
								.Where(entry => entry.First >= 0).OrderBy(entry => entry.First)
								.ThenBy(entry => Distance(before, entry.Monster.Npc.Position)).Select(entry => entry.Monster).FirstOrDefault();
							if (blocker != null)
							{
								// SM_MOVE carries the current point and an announced destination separately.
								// The destination identifies an obstruction; combat needs the current point in reach.
								bool PullReady() => session.Api.World.Objects.TryGetValue(blocker.Npc.ObjectId, out var seen) &&
									!seen.IsCorpse && Distance(session.CurrentPosition, seen.Position) <= combat.ClassProfile.Ranges.SpellRange &&
									geometry.HasLineOfSight(map, session.CurrentPosition, seen.Position);
								long waited = runtime.NowMillis;
								while (!PullReady() && runtime.NowMillis - waited < 30_000 &&
									session.Api.World.Objects.TryGetValue(blocker.Npc.ObjectId, out var walking) && !walking.IsCorpse &&
									!navigator.UnavailableObjects.Contains(blocker.Npc.ObjectId))
								{
									await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
									await session.SynchronizeAsync(token);
									if (!Survived() || !await DefendAgainstEngagedAsync("fire-upper-patrol-wait") || !Survived()) return false;
									if (Distance(before, session.CurrentPosition) > 2) break; // Replan after defense moved us.
								}
								if (Distance(before, session.CurrentPosition) > 2 || navigator.UnavailableObjects.Contains(blocker.Npc.ObjectId) ||
									!session.Api.World.Objects.TryGetValue(blocker.Npc.ObjectId, out var current) || current.IsCorpse) continue;
								session.TraceDiagnostic("fire-upper-route-pull-wait", new Dictionary<string, object?>
								{ ["target"] = blocker.Npc.ObjectId, ["position"] = session.CurrentPosition, ["currentTarget"] = current.Position,
									["announcedTarget"] = blocker.Npc.Position, ["elapsedMillis"] = runtime.NowMillis - waited, ["ready"] = PullReady() });
								if (!PullReady()) return false;
								session.TraceDiagnostic("fire-upper-route-pull", new Dictionary<string, object?>
								{ ["target"] = blocker.Npc.ObjectId, ["template"] = blocker.Npc.TemplateId, ["position"] = before, ["goal"] = at });
								bool killed;
								try { killed = await combat.TryKillAsync(blocker.Npc.ObjectId, token, before, preferPull: true); }
								catch (NaturalCombatApproachBlockedException) { killed = false; }
								if (!Survived()) return false;
								if (killed)
								{
									navigator.UnavailableObjects.Add(blocker.Npc.ObjectId);
									await RestSafelyAsync(token);
								}
								else if (Distance(before, session.CurrentPosition) <= 2) return false;
								continue; // Re-observe the corridor after the fight, loot and rest.
							}
						}
						float speed = session.Api.World.MovementSpeed ?? 0;
						if (!float.IsFinite(speed) || speed <= 0)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
							await session.SynchronizeAsync(token);
							continue;
						}
						await session.ExecuteMovementAsync(new BotMover(session.Api.World, session.Api.Timing)
							.CreateGroundPlan([path[0]], before, speed), token);
						await session.SynchronizeAsync(token);
						if (!Survived()) return false;
						stalled = Distance(before, session.CurrentPosition) < 0.05f ? stalled + 1 : 0;
						if (stalled >= 3) return false;
					}
					return false;
				}
				async Task<bool> WalkFireTravelAsync(BotPosition at)
				{
					int map = session.Api.World.MapId!.Value;
					int revives = combat.ReviveCount;
					var progress = new NaturalApproachProgress();
					for (int clear = 0; progress.CanRetry(clear); clear++)
					{
						if (session.Api.World.MapId != map || session.Api.World.IsDead || combat.ReviveCount != revives) return false;
						NaturalNavigationResult result = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
							map, -1, at, NaturalFireRoomTravel.LaunchRadius, navigator, "fire-route-takeoff", token);
						if (result.Arrived) return true;
						session.TraceDiagnostic("fire-launch-approach-blocked", new Dictionary<string, object?>
						{
							["position"] = session.CurrentPosition, ["launch"] = at, ["reason"] = result.Reason,
							["attempt"] = clear, ["revives"] = combat.ReviveCount - revives,
						});
						if (session.Api.World.MapId != map || session.Api.World.IsDead || combat.ReviveCount != revives) return false;
						BotPosition before = session.CurrentPosition;
						int kills = navigator.UnavailableObjects.Count;
						if (!await TryClearObservedBlockerAsync(at)) return false;
						progress.Observe(Distance(before, session.CurrentPosition), navigator.UnavailableObjects.Count > kills);
					}
					return false;
				}
				glideDown = leg.InstanceRooms is not { } glideRooms ? null : async goal =>
				{
					if (session.Api.World.MapId != glideRooms.MapId || session.Api.World.IsDead || combat.InCombat) return false;
					if (fireTravel?.HandlesDescent(glideRooms.MapId, goal) == true)
					{
						int? instance = session.Api.World.InstanceId;
						int revives = combat.ReviveCount;
						bool Survived() => session.Api.World.MapId == glideRooms.MapId && session.Api.World.InstanceId == instance &&
							!session.Api.World.IsDead && session.Api.World.CurrentHp > 0 && combat.ReviveCount == revives;
						async Task<bool> Approach(BotPosition at)
						{
							for (int retry = 0; retry < 3 && Survived(); retry++)
							{
								if (Distance(session.CurrentPosition, at) <= NaturalFireRoomTravel.LaunchRadius ||
									await fireTravel.ApproachDescentAsync(WalkFireGroundAsync, Survived, token)) return true;
								// Combat can retreat back along the upper room. Rejoin the recorded route
								// from that position instead of resuming a stale waypoint across a higher deck.
								session.TraceDiagnostic("fire-upper-route-rejoin", new Dictionary<string, object?>
								{ ["attempt"] = retry, ["position"] = session.CurrentPosition });
								await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							}
							return false;
						}
						return await fireTravel.DescendAsync(Approach, SettleFireTravelAsync, token);
					}
					BotPosition[] monsters = [.. runtime.Data.SpawnsDh.GetSpawnsByWorldId(glideRooms.MapId)
						.Where(group => group.GetNpcId() != glideRooms.Rooms.FirstOrDefault(room => room.Contains([goal.X, goal.Y, goal.Z]))?.SourceNpcId &&
							runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(group.GetNpcId())))
						.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))];
					return await floorGlide.GlideTowardAsync(glideRooms.MapId, goal,
						async takeoff => (await NaturalIshalgenNavigator.ExploreWithinRangeAsync(glideRooms.MapId, -1, takeoff, 1.5f, navigator,
							"glide-takeoff", token)).Arrived,
						async () =>
						{
							await DefendAgainstEngagedAsync("glide-takeoff");
							await RestSafelyAsync(token);
						}, monsters, token);
				};
				var haramelTravel = new NaturalHaramelTravel(session, runtime, () => geometry,
					async (at, range) =>
					{
						for (int retry = 0; retry < 100 && session.Api.World.MapId == leg.Haramel?.MapId; retry++)
							if (await WalkHaramelAsync(at, range)) return;
						Require.True(session.Api.World.MapId != leg.Haramel?.MapId, "Haramel floor approach exceeded its bounded replans.");
					});
				if (leg.Haramel != null)
				{
					haramelApproach = ApproachHaramelAsync;
					haramelKind = HaramelKind;
				}
				await FallFromLoginPlaceAsync();
				await TopUpHelpItemsAsync("run-start");
				await combat.BuffOurselfAsync(NaturalHelpTrigger.AfterRelog, token);
				// NR-R23a: a second class wears the better gear it owns before the leg begins. The town service was the one place
				// a leg put gear on, and a roomy cube seldom sends a leg there.
				if (combat.ClassProfile.Gear.WearsAtLegStart) await EquipUpgradesAsync(token);
				// The revised roomy cube need not trigger generic inventory maintenance after Q24016.
				// Wear its already earned staff once before CG's explicit loadout freeze and receipts.
				if (leg.CoinGear is { } retainedGear)
					coinIncomingLoadout = await NaturalCoinGearSteps.PrepareRetainedLoadoutAsync(session, retainedGear, token);
				string? previous = null;
				int repeats = 0;
				// AB-08: a decision repeated because the Cleric died on it is a retry, not a stall (OD-12): up to six of them.
				bool itemApproachRetreated = false;
				// NR-R4b: the object whose use the server cut short last, until a use of an object ends by itself.
				int useCutShortObject = 0;
				int revivesAtPrevious = combat.ReviveCount, deathRetries = 0;
				// AC-06: the escort's state across protocol runs (a death ends a run; the attempts and ended followers stay).
				int escortAttempts = 0;
				var escortEndedFollowers = new HashSet<int>();
				long? escortFollowerGoneAt = null, escortClearedUntil = null;
				// AB-08: timers started and when the client expects each to end; Infernus attempts.
				var timedAttempts = new Dictionary<int, int>();
				var timedEnds = new Dictionary<int, long>();
				var spawnAttempts = new Dictionary<string, int>();
				long? destinySpawnedAt = null;
				// AK-08: the cube's free slots as the inventory policy counts them, for a leg whose town has a merchant.
				// AG-07: far approaches take the leg's road first. A walk cut short by a fight (a retreat, or a death and the
				// revive at the bind) walks again from where it left the Cleric, three walks at most; no road at all leaves the
				// approach to the navigator.
				int? warlockRoadRevives = null;
				farApproach = async (at, templateId, sourceComplete) =>
				{
					// AE-06/AO-04: delivery legs take hub flights; approaching the flight pad must not plan another flight. M3: so do the
					// Morheim legs, whose hand-ins stand at other hubs (Q2495 at the fortress, from Kellan's Cabin).
					if ((altgardLegId is "l7" or "l8" or "l9" or "l10" or "cg" or "l12" || NaturalAltgardContract.MusteredLegs.Contains(altgardLegId!)) &&
						session.Api.World.MapId == leg.Hub.MapId && !approachingAirline)
					{
						approachingAirline = true;
						try { await FlyTowardAsync(at); }
						finally { approachingAirline = false; }
					}
					float approachStop = UsesProvenWarlockSpawn(templateId) ? NaturalPullPlanner.SpellRange + 3 : FarApproachStop;
					for (int walk = 0; walk < 3 && Distance(session.CurrentPosition, at) > approachStop; walk++)
					{
						if (sourceComplete?.Invoke() == true) return;
						// A lower-ground fight can revive at the upper Heart bind. Every
						// road retry must repeat the proven pillar flight from the new position.
						await ReachPillarLevelAsync(at);
						if (altgardLegId == "l10" && templateId == 210538 && warlockRoadRevives != combat.ReviveCount)
						{
							// BC-06: the fortress approach repeatedly re-entered the hunter/spellshifter camp,
							// then its avoidance route detoured as far south as Basfelt. Reach the lower
							// warlocks from Heart instead, using learned Return and the existing hub flight.
							NaturalAltgardContract heartLeg = NaturalAltgardContract.LoadLeg("l9");
							NaturalAltgardHub heart = heartLeg.Hub;
							BotPosition heartAt = new(heart.Anchor[0], heart.Anchor[1], heart.Anchor[2], 0);
							if (Distance(session.CurrentPosition, heartAt) > heart.Radius)
							{
								if (Distance(session.CurrentPosition, ground) > 60) await UseLearnedReturnToBindAsync();
								await RestSafelyAsync(token);
								approachingAirline = true;
								try { Require.True(await FlyTowardAsync(heartAt), "The warlock approach needs the fortress-to-Heart hub flight."); }
								finally { approachingAirline = false; }
							}
							int pillarRevives = combat.ReviveCount;
							await ReachPillarLevelAsync(at, heartLeg);
							if (combat.ReviveCount != pillarRevives) continue; // prepare the hub flight again after a death
							warlockRoadRevives = combat.ReviveCount;
							session.TraceDiagnostic("black-claw-warlock-heart-approach", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition, ["revives"] = warlockRoadRevives,
							});
						}
						BotPosition before = session.CurrentPosition;
						int revives = combat.ReviveCount;
						if (await WalkRoadDefendingAsync(at, $"approach-road-{templateId}", stopAt: point => Distance(point, at) <= approachStop,
							sourceComplete: sourceComplete))
							return;
						if (combat.ReviveCount == revives && Distance(before, session.CurrentPosition) < 1)
							return;
					}
				};
				int? FreeCubeSlots() => leg.Town?.VendorNpcId == null ? null
					: NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, session.Api.World.Inventory.Values.Select(item => item.ItemId), ClassLine)
						.Decide(session.Api.World, QuestNeededItems(), leg.CoinGear, leg.Haramel).FreeSlots;
				async Task SaveCoinProgressAsync() => await File.WriteAllTextAsync(
					Path.Combine(Path.GetDirectoryName(combatTracePath)!, "coin-gear-progress.json"),
					System.Text.Json.JsonSerializer.Serialize(new { session.CharacterId, ElapsedMillis = runtime.NowMillis, coinGearProgress }), token);
				if (laterCapital != null && altgardLegId == "l1")
					await NaturalLaterCapitalSteps.PickUpHeritageAsync(session, async step =>
					{
						session.BeginStep("rc-" + step.Key, "later-capital-heritage-pickup");
						int npc = await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
						for (int attempt = 1; ; attempt++)
						{
							try
							{
								string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
								session.TraceDiagnostic("later-capital-step", new Dictionary<string, object?> { ["change"] = change });
								break;
							}
							catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(npc); }
							}
					});
				if (laterCapital != null && altgardLegId == "l5" && NaturalLaterCapitalSteps.Leg5CityNeeded(session.Api.World))
					await VisitLeg5CapitalAsync();
				for (int sequence = 1; sequence <= 400; sequence++)
				{
					await session.SynchronizeAsync(token);
					EnterLegMap();
					if (haramelProgress != null) SaveHaramelProgress();
					if (laterCapital != null && altgardLegId == "l7" && session.Api.World.MapId == capitalContract.MapId)
					{
						var proposal = NaturalAltgardQuestSteps.State(session.Api.World, 2278);
						bool paid = session.Api.World.CompletedQuestIds.Contains(2278);
						// Finish Q2278's Cavalorn contact first, then batch the nearby book/pickups.
						// Leave the market hand-ins until after Balder, so no library return is needed.
						if ((paid || proposal is (3, >= 2) or (4, _)) && NaturalLaterCapitalSteps.Leg7LibraryNeeded(session.Api.World))
							await NaturalLaterCapitalSteps.CompleteLeg7LibraryAsync(session, PlayLaterCapitalStepAsync);
						if ((paid || proposal is (3, >= 3) or (4, _)) && NaturalLaterCapitalSteps.Leg7ErrandsNeeded(session.Api.World))
							await NaturalLaterCapitalSteps.CompleteLeg7ErrandsAsync(session, PlayLaterCapitalStepAsync);
					}
					if (laterCapital != null && altgardLegId == "l9" && session.Api.World.MapId == capitalContract.MapId &&
						!session.Api.World.CompletedQuestIds.Contains(2920))
						await NaturalLaterCapitalSteps.CompleteLeg9CityAsync(session, PlayLaterCapitalStepAsync, ApproachCapitalNpcAsync, token);
					if (laterCapital != null && altgardLegId == "l10" && session.Api.World.MapId == capitalContract.MapId &&
						!session.Api.World.CompletedQuestIds.Contains(2916))
						await NaturalLaterCapitalSteps.CompleteLeg10RobeAsync(session, PlayLaterCapitalStepAsync);
					if (laterCapital != null && altgardLegId == "l11" && session.Api.World.MapId == capitalContract.MapId &&
						(session.Api.World.CompletedQuestIds.Contains(2900) || NaturalAltgardQuestSteps.State(session.Api.World, 2900) is (4, 10)))
						await NaturalLaterCapitalSteps.CompleteLeg11LibraryAsync(session, PlayLaterCapitalStepAsync);
					if (laterCapital != null && altgardLegId == "l6" &&
						NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 3) &&
						Distance(session.CurrentPosition, ground) <= leg.Hub.Radius)
						await NaturalLaterCapitalSteps.RunMatchingStepsAsync(session, [NaturalLaterCapitalSteps.RobeBerth], PlayLaterCapitalStepAsync);
					if (session.Api.World.Level != helpCheckedAtLevel) await TopUpHelpItemsAsync("level-up");
					NaturalAltgardDecision next = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition, session.Api.Timing.Now, FreeCubeSlots(), coinGearProgress, haramelProgress, HaramelNow()), objectives, sequence, only, deferredToGroup);
					// NR-R15c: leg 10's trip to Pandaemonium is Q2283's hand-in, and Deyla's robe is handed in on it. A line that
					// leaves Q2283 out has no quest to take it there: it makes the trip for the robe, once its quests are done.
					if (laterCapital != null && altgardLegId == "l10" && only == null && next.Outcome == "complete" &&
						!session.Api.World.CompletedQuestIds.Contains(2916))
						next = new(sequence, "travel-to-map", null, 2916, "planned",
							"Q2916: no quest of this leg is handed in at Pandaemonium; take the robe to Deyla.", next.Checks, capitalContract.MapId);
					session.TraceDiagnostic($"altgard-{altgardLegId}-decision", new Dictionary<string, object?>
					{
						["sequence"] = sequence, ["action"] = next.Action, ["step"] = next.StepKey, ["quest"] = next.QuestId,
						["outcome"] = next.Outcome, ["reason"] = next.Reason, ["level"] = session.Api.World.Level,
						["position"] = session.CurrentPosition, ["fp"] = session.Api.World.CurrentFlightTime,
						["checks"] = next.Checks.Select(check => $"{check.Rule}:{check.Verdict}").ToArray(),
					});
					session.PublishDashboard();
					string signature = $"{next.Action}/{next.StepKey}/{next.QuestId}/{next.Reason}";
					if (leg.Haramel != null) signature += "/" + string.Join(',', leg.Order.Select(id => $"{id}:{QuestStatus(id)}:{QuestVar(id)}")) +
						"/" + string.Join(',', leg.Haramel.CleanupItemIds.Select(id => ItemCount(session.Api.World, id)));
					bool diedSincePrevious = combat.ReviveCount > revivesAtPrevious;
					revivesAtPrevious = combat.ReviveCount;
					if (signature != previous) { repeats = 0; deathRetries = 0; }
					else if (diedSincePrevious) deathRetries++;
					else if (!itemApproachRetreated) repeats++;
					itemApproachRetreated = false;
					previous = signature;
					Require.True(repeats < 3 && deathRetries <= 6, $"Altgard leg {altgardLegId} made no progress on {signature}");
					if (next.Outcome == "complete")
					{
						if (only != null)
						{
							session.TraceDiagnostic($"altgard-{altgardLegId}-only-complete", new Dictionary<string, object?>
							{
								["quests"] = only.ToArray(), ["level"] = session.Api.World.Level, ["deaths"] = combat.ReviveCount,
							});
							return;
						}
						if (laterCapital != null && altgardLegId == "l5")
						{
							session.BeginStep("rc-book-field-materials", "collect-native-leg-5-book-drops");
							await NaturalLaterCapitalFieldCollection.CollectAsync(session, (kind, operation) => KillShippedSpawnAsync(kind, operation),
								async (source, item) => { await TryLootCorpseItemAsync(session, source, item, token); navigator.UnavailableObjects.Add(source); },
								() => RestSafelyAsync(token));
							if (Distance(session.CurrentPosition, ground) > leg.Hub.Radius) await UseLearnedReturnToBindAsync();
							await RestSafelyAsync(token);
						}
						if (laterCapital != null && altgardLegId == "l10")
						{
							Require.True(session.Api.World.CompletedQuestIds.Contains(2916), "Deyla's robe hand-in is incomplete.");
							await NaturalLaterCapitalSteps.ObtainLibraryPermissionAsync(session, PlayLaterCapitalStepAsync);
							await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId!.Value, withinRange: 5);
						}
						if (laterCapital != null && altgardLegId == "l11")
							Require.True(session.Api.World.CompletedQuestIds.Contains(2938) && ItemCount(session.Api.World, 182207026) == 0,
								"The library finish must consume Suthran's permission once.");
						if (laterCapital != null && altgardLegId == "l6")
							Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 4),
								"Neparinerk must leave the carried robe quest at START/4 for Banatisai.");
						if (laterCapital != null && altgardLegId == "l7")
							Require.True(!NaturalLaterCapitalSteps.Leg7CityNeeded(session.Api.World), "The scheduled Leg 7 capital batch is incomplete.");
						if (laterCapital != null && altgardLegId == "l9")
						{
							Require.True(session.Api.World.CompletedQuestIds.Contains(2920), "Deyla's Leg 9 answer is incomplete.");
							await CollectLeg9ClothingAsync();
							if (Distance(session.CurrentPosition, ground) > leg.Hub.Radius) await UseLearnedReturnToBindAsync();
							await RestSafelyAsync(token);
						}
						foreach (var (id, expected) in preservedQuests)
							Require.True(session.Api.World.Quests.TryGetValue(id, out BotQuestState? state) &&
								(((continuousAltgard || laterCapital != null) && NaturalAltgardContinuation.AllowsAutomaticCampaignUnlock(id, expected, state,
									session.Api.World.Level, session.Api.World.CompletedQuestIds)) ||
								(altgardLegId == "l12" ? NaturalHaramelDecisionEngine.PreservesDeferredQuest(id, expected, state, session.Api.World.Level)
									: (state.Status, state.StepAndFlags) == expected)), $"Deferred Q{id} changed during {leg.Leg}.");
						await CompleteAltgardLeg1Async(leg);
						return;
					}
					if (next.Outcome != "planned")
					{
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-stop.json"),
							System.Text.Json.JsonSerializer.Serialize(next), token);
						throw new InvalidDataException($"Altgard leg {altgardLegId} stopped: {next.Outcome} {next.Action}: {next.Reason}");
					}
					session.BeginStep($"af-{sequence:000}-{next.Action}", next.StepKey ?? next.Action);
					switch (next.Action)
					{
						case "enter-haramel":
							await EnterHaramelAsync();
							break;
						case "observe-haramel-entry":
							await ObserveHaramelEntryAsync();
							break;
						case "leave-haramel":
							await LeaveHaramelAsync();
							break;
						case "wait-haramel-expiry":
							while (HaramelNow() < haramelProgress!.FreshEntryAfterMillis)
							{
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, haramelProgress.FreshEntryAfterMillis!.Value - HaramelNow())), token);
								await session.SynchronizeAsync(token);
								await DefendAgainstEngagedAsync("haramel-empty-expiry");
							}
							break;
						case "haramel-movie":
							await NaturalHaramelQuestSteps.FinishLeadInMovieAsync(session, token);
							break;
						case "haramel-carts":
							while (QuestVar(28510) < 3 && session.Api.World.MapId == leg.Haramel!.MapId)
							{
								await KillShippedSpawnAsync(700950);
								await RestSafelyAsync(token);
							}
							break;
						case "haramel-ginseng":
							while (ItemCount(session.Api.World, 182212022) < 5 && session.Api.World.MapId == leg.Haramel!.MapId)
								await UseAndLootQuestObjectAsync(700954, 182212022, skipBlockedTarget: true);
							break;
						case "haramel-object":
						{
							NaturalAltgardStep use = leg.Steps.Single(step => step.Key == next.StepKey);
							int source = await ApproachShippedSpawnAsync(use.NpcId, withinRange: TalkRange(use.NpcId));
							await NaturalAltgardQuestSteps.UseObjectAsync(session, source, null, token);
							await session.SynchronizeAsync(token);
							break;
						}
						case "haramel-soup":
							await MakeHaramelSoupAsync();
							break;
						case "haramel-boss":
							await KillShippedSpawnAsync(leg.Haramel!.BossNpcId);
							await ObserveHaramelBossAsync();
							break;
						case "haramel-loot":
							await LootHaramelChestAsync();
							break;
						case "travel-to-map":
						{
							if (leg.Destiny != null)
							{
								await TravelDestinyMapAsync(next.MapId!.Value);
								break;
							}
							// NL-4: Return lands at the bind; a bot bound off the hub's map (at Primum, for Morheim's Alsig Village)
							// takes the leg's trips to it instead.
							if (next.MapId == leg.Hub.MapId && (session.Api.World.ObeliskBindPoint?.MapId is not int boundMap ||
								boundMap == leg.Hub.MapId || !TripChainLeads(session.Api.World.MapId!.Value, leg.Hub.MapId)))
								await UseLearnedReturnToBindAsync();
							else
								for (int onMap = session.Api.World.MapId!.Value; onMap != next.MapId; onMap = session.Api.World.MapId!.Value)
								{
									// NL-2: a map no teleporter here reaches (Ishalgen from Brusthonin, for Q4011's Kaindal) is reached
									// by way of the map its own teleporter stands on (Pandaemonium), as a player changes teleporters.
									// NL-3: from Reshanta a capital or a home zone is two or three teleporters away (Primum's Morheim device,
									// Morheim's teleporter to Pandaemonium, Pandaemonium's to Ishalgen): the first trip of the shortest chain.
									NaturalAltgardMapTrip trip = leg.MapTripList.FirstOrDefault(entry => entry.MapId == next.MapId &&
										(entry.FromMapId == null || entry.FromMapId == onMap)) ?? FirstTripOfChain(onMap, next.MapId!.Value);
									EnterLegMap();
									await EnsureOnGroundAsync();
									// Pandaemonium's teleporter is reached by the capital's own ways (its lifts, the Coliseum), as its talks are.
									// NL-3: a portal object is used with its bar; one that hangs in the air out of reach of the ground the bot
									// stands on (the Morheim Abyss Gate) is flown to and used in flight.
									if (trip.UseMillis is int useMillis && runtime.Data.SpawnsDh.GetSpawnsByWorldId(onMap)
										.Where(group => group.GetNpcId() == trip.TeleporterNpcId).SelectMany(group => group.GetSpawnTemplates())
										.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)).FirstOrDefault() is { } door &&
										door != default && geometry.IslandGround(onMap, session.CurrentPosition,
											at => Distance(at, door) <= trip.TalkRange - 2).Count == 0 &&
										// Primum's teleport devices stand on the fortress's ground, on a terrace the navmesh splits off
										// from the bot's (nl3-ro-d2): only a portal with no ground at its feet hangs in the air.
										geometry.StaticGroundAt(onMap, door) == null)
									{
										Require.True(await FlyBesideAsync(door, trip.TalkRange - 3), $"No flight beside portal {trip.TeleporterNpcId}.");
										await session.SynchronizeAsync(token);
										int floating = session.Api.World.Objects.Values.Where(known => known.TemplateId == trip.TeleporterNpcId)
											.OrderBy(known => Distance(known.Position, door)).Select(known => known.ObjectId).FirstOrDefault();
										Require.True(floating != 0, $"Portal {trip.TeleporterNpcId} is not in sight beside it.");
										await UseInstancePortalAsync(floating, useMillis, trip.MapId, trip.Confirm);
										continue;
									}
									// A portal object is used from its talk range (the server's is its talk distance plus 1 m): Primum's
									// teleport devices stand on bases no navmesh covers, and a walk to the ordinary 3 m found no approach
									// point for 43 minutes (nl3-ro-d3), stopping 6.3 m away.
									int teleporter = (laterCapital != null || NaturalAltgardContract.MusteredLegs.Contains(altgardLegId!)) && onMap == capitalContract.MapId
										? await ApproachCapitalNpcAsync(trip.TeleporterNpcId)
										: await ApproachShippedSpawnAsync(trip.TeleporterNpcId, withinRange: trip.UseMillis != null ? trip.TalkRange + 0.5f : null);
									if (trip.UseMillis is int walkedUseMillis)
									{
										await UseInstancePortalAsync(teleporter, walkedUseMillis, trip.MapId, trip.Confirm);
										continue;
									}
									NaturalServiceOutcome travelled = await new NaturalServiceSteps(session).TeleportAsync(teleporter,
										session.Api.World.Objects[teleporter].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
									Require.True(travelled.IsDone, travelled.Reason);
								}
							EnterLegMap();
							Require.Equal(next.MapId, session.Api.World.MapId);
							break;
						}
						case "refresh-observation":
							await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							break;
						case "recover-destiny":
							await RecoverDestinyAsync("observed-state");
							break;
						case "equip-stigma":
						{
							NaturalAltgardStep stoneStep = leg.Steps.Single(step => step.Key == "q2900-stone");
							int skuld = await ApproachDestinyNpcAsync(stoneStep);
							if (skuld == 0 || NaturalAltgardQuestSteps.State(session.Api.World, leg.Destiny!.QuestId) is not (3, 99)) break;
							await NaturalAltgardQuestSteps.EquipDestinyStigmaAsync(session, leg.Destiny!, skuld, token);
							break;
						}
						case "destiny-fight":
							await FightDestinyAsync();
							break;
						case "revive-at-bind":
						case "revive-in-place":
							await RestSafelyAsync(token);
							break;
						case "enter-instance":
						{
							NaturalAltgardInstanceTrip trip = leg.InstanceTripList.Single(entry => entry.QuestId == next.QuestId);
							await PrepareRebirthAsync();
							int portal = await ApproachShippedSpawnAsync(trip.PortalNpcId, withinRange: TalkRange(trip.PortalNpcId));
							await UseInstancePortalAsync(portal, trip.UseMillis, trip.MapId);
							break;
						}
						case "enter-room":
						{
							// NL-4: one room of a solo instance by its own portal on the entrance pad (Q24023). NL-a: the companion
							// does not go in; it is sent away first, and asked for again once the last room is done.
							NaturalAltgardInstanceRooms rooms = leg.InstanceRooms!;
							NaturalAltgardInstanceRoom room = rooms.Rooms.Single(entry => entry.Key == next.StepKey);
							if (options.SoloCompanion != null) await DismissSoloCompanionAsync(token);
							if (room.PortalNpcId == 730045 && fireTravel != null)
							{
								// The shared pad's Wind side is reachable on foot. Fire is reached by flying up and around the structure.
								if (NaturalFlightPolicy.Distance(session.CurrentPosition, fireTravel.EntranceLanding) >= 3)
									await ApproachShippedSpawnAsync(730043, withinRange: TalkRange(730043));
								int revivesBeforeFlight = combat.ReviveCount;
								if (!await fireTravel.EnterAsync(WalkFireTravelAsync, SettleFireTravelAsync, token))
								{
									if (session.Api.World.IsDead || session.Api.World.MapId != leg.Hub.MapId || combat.ReviveCount != revivesBeforeFlight) break;
									throw new InvalidOperationException("The recorded Fire entrance flight could not be taken safely.");
								}
							}
							int portal = await ApproachShippedSpawnAsync(room.PortalNpcId, withinRange: TalkRange(room.PortalNpcId));
							if (session.Api.World.IsDead || session.Api.World.MapId != leg.Hub.MapId) break;
							if (!await UseInstancePortalAsync(portal, rooms.UseMillis, rooms.MapId))
							{
								await DefendAgainstEngagedAsync("room-portal");
								break;
							}
							session.TraceDiagnostic($"altgard-{altgardLegId}-room-entered", new Dictionary<string, object?>
							{
								["room"] = room.Key, ["instance"] = session.Api.World.InstanceId, ["position"] = session.CurrentPosition,
							});
							break;
						}
						case "leave-room":
						{
							NaturalAltgardInstanceRooms rooms = leg.InstanceRooms!;
							NaturalAltgardInstanceRoom? room = next.StepKey is { } key ? rooms.Rooms.Single(entry => entry.Key == key) : null;
							if (room == null)
							{
								// Nowhere in a room's box (a fall off its floor that the bot survived): Return, which the map allows.
								session.TraceDiagnostic($"altgard-{altgardLegId}-room-lost", new Dictionary<string, object?> { ["position"] = session.CurrentPosition });
								await UseLearnedReturnToBindAsync();
								await RestSafelyAsync(token);
								break;
							}
							int exit;
							try { exit = room.ExitNpcId == 730046 && approachRecordedFireExit != null && await approachRecordedFireExit() is int recordedExit
								? recordedExit : await ApproachShippedSpawnAsync(room.ExitNpcId, withinRange: TalkRange(room.ExitNpcId)); }
							catch (NaturalLeftInstanceRoomException) { break; }
							if (session.Api.World.IsDead || session.Api.World.MapId != rooms.MapId) break;
							if (!await UseInstancePortalAsync(exit, rooms.UseMillis, leg.Hub.MapId))
							{
								await DefendAgainstEngagedAsync("room-exit");
								break;
							}
							session.TraceDiagnostic($"altgard-{altgardLegId}-room-left", new Dictionary<string, object?>
							{
								["room"] = room.Key, ["position"] = session.CurrentPosition,
							});
							if (options.SoloCompanion is string companionClass &&
								rooms.Rooms.All(entry => ItemCount(session.Api.World, entry.ItemId) > 0))
								await AskSoloCompanionAsync(companionClass, token);
							break;
						}
						case "leave-instance":
						{
							NaturalAltgardInstanceTrip trip = leg.InstanceTripList.Single(entry => entry.QuestId == next.QuestId);
							// BC-02 proved these intermediate routes; the long direct gate-to-exit query is disconnected.
							NaturalAltgardObjectUse guardian = leg.ObjectUseList.Single(use => use.QuestId == trip.QuestId && use.FromVar == trip.EnterVar);
							await ApproachShippedSpawnAsync(guardian.NpcId, withinRange: TalkRange(guardian.NpcId));
							NaturalNavigationResult entry = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(trip.MapId, -1,
								new(trip.Arrival[0], trip.Arrival[1], trip.Arrival[2], 0), 3, navigator, "instance-entry", token);
							Require.True(entry.Arrived, entry.Reason);
							int exit = await ApproachShippedSpawnAsync(trip.ExitNpcId, withinRange: TalkRange(trip.ExitNpcId));
							await UseInstancePortalAsync(exit, trip.ExitUseMillis, leg.Hub.MapId);
							break;
						}
						case "template-accept":
						{
							await EnsureOnGroundAsync();
							QuestRunPlan plan = altgardPlans[next.QuestId!.Value];
							int starterId = plan.StartTrigger.Npcs.First().Id;
							int starter = await ApproachShippedSpawnAsync(starterId);
							for (int attempt = 1; ; attempt++)
							{
								try { await session.StartQuestAsync(starter, plan.Id, token); break; }
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(starter); }
							}
							break;
						}
						case "template-work":
						{
							await EnsureOnGroundAsync();
							try
							{
								if (next.QuestId == 28509 && leg.Haramel != null) await CollectHaramelKeysAsync();
								if (next.QuestId == 28507 && leg.Haramel != null)
									foreach (int boss in new[] { 216897, 216907, 216915 })
									{
										RecordHaramelDeadHints();
										if (graph.GetMap(leg.Haramel.MapId)!.Waypoints.Any(w => w.TemplateId == boss && !haramelHints.Contains((boss, w.Position))))
											await KillShippedSpawnAsync(boss);
									}
								await CompleteTemplateQuestAsync(altgardPlans[next.QuestId!.Value], TemplatePhase.Work);
								if (leg.Haramel != null) await ObserveHaramelBossAsync();
							}
							catch (NaturalHaramelSourcesExhaustedException source) when (leg.Haramel != null)
							{
								session.TraceDiagnostic("haramel-exhausted-sources-outcome", new Dictionary<string, object?> { ["quest"] = next.QuestId, ["reason"] = source.Message });
								await LeaveHaramelAsync();
							}
							catch (TimeoutException stalled) when (NaturalAltgardContract.MusteredLegs.Contains(altgardLegId!) &&
								stalled.Message.StartsWith("Natural journey made no quest", StringComparison.Ordinal))
							{
								// M4: no progress on this quest alone for the watchdog's hour (the Sorcerer at Chieftain Promakis, an EXPERT): the
								// bot leaves it, and what needs it first, for the group step (M6), and plays on.
								deferredToGroup.Add(next.QuestId!.Value);
								session.TraceDiagnostic($"altgard-{altgardLegId}-deferred-to-group", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["level"] = session.Api.World.Level, ["position"] = session.CurrentPosition,
								});
								ExcuseBoundedWait();
								await RestSafelyAsync(token);
							}
							break;
						}
						case "template-claim":
						{
							await EnsureOnGroundAsync();
							NaturalAltgardObservation? beforeCoinReward = leg.CoinGear == null ? null :
								NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition);
							await CompleteTemplateQuestAsync(altgardPlans[next.QuestId!.Value], TemplatePhase.Claim);
							if (leg.CoinGear is { } rewardGear)
							{
								coinGearProgress = coinGearProgress!.ObserveReward(rewardGear, beforeCoinReward!,
									NaturalAltgardObservation.Observe(session.Api.World, session.CurrentPosition));
								await SaveCoinProgressAsync();
							}
							await TopUpHelpItemsAsync("town");
							break;
						}
						case "coin-purchase":
						case "coin-equip":
						{
							NaturalCoinGear gear = leg.CoinGear!;
							int item = int.Parse(next.StepKey!);
							await EnsureOnGroundAsync();
							var inventory = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
								session.Api.World.Inventory.Values.Select(i => i.ItemId).Concat(gear.ProtectedItemIds), ClassLine);
							var shopping = new NaturalCoinGearSteps(session, runtime.Data, gear, inventory);
							if (next.Action == "coin-purchase")
							{
								int vendor = await ApproachShippedSpawnAsync(gear.VendorNpcId, withinRange: TalkRange(gear.VendorNpcId));
								coinGearProgress = await shopping.PurchaseAsync(vendor, item, coinGearProgress!, token);
								await SaveCoinProgressAsync();
							}
							else await shopping.EquipAsync(item, coinGearProgress!, token);
							break;
						}
						case "talk":
							await PlayContractStepAsync(leg.Steps.Single(candidate => candidate.Key == next.StepKey));
							break;
						case "item-start":
						{
							// M3: a quest started by an item. The item is given by an object (Q2498's worn document, which goes and respawns)
							// or drops from monsters: Q2316's Old Notebook is a 5% drop of the Sprigg Habitat's sparkies, so one decision hunts
							// on until it drops: 150 tries at most, since the other bots of the world take many of the sparkies, and the hunt counts
							// as progress while it lasts (ExcuseBoundedWait), as the respawn waits do.
							await EnsureOnGroundAsync();
							NaturalAltgardItemStart start = leg.ItemStartList.Single(entry => entry.QuestId == next.QuestId);
							if (ItemCount(session.Api.World, start.ItemId) > 0)
							{
								await NaturalAltgardQuestSteps.StartQuestWithItemAsync(session, start.QuestId, start.ItemId,
									runtime.Data.ItemDataDh.GetItemTemplate(start.ItemId), token);
								break;
							}
							if (start.ObjectNpcId is int giver)
							{
								// Q2498's worn document stands at one of its five spots at a time (pool 1) and respawns 2950 s after a use, so in a
								// world of eight the bot that comes after another waits for it, and a use another bot wins is waited for again.
								// One decision waits at the spots, a minute at a time, for six game hours at most.
								BotPosition near = graph.GetMap(leg.Hub.MapId)!.Waypoints.Where(waypoint => waypoint.TemplateId == giver)
									.Select(waypoint => waypoint.Position).MinBy(position => Distance(position, session.CurrentPosition));
								bool Visible() => session.Api.World.Objects.Values.Any(known => known.TemplateId == giver && !known.IsCorpse &&
									!navigator.UnavailableObjects.Contains(known.ObjectId));
								int minutes = 0, uses = 0;
								while (ItemCount(session.Api.World, start.ItemId) == 0 && !session.Api.World.IsDead && minutes < 360)
								{
									if (!Visible())
									{
										if (Distance(session.CurrentPosition, near) > 20) await WalkRoadDefendingAsync(near, "item-start-object", within: 15);
										await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
										await session.SynchronizeAsync(token);
										await DefendAgainstEngagedAsync("item-start-object-wait");
										ExcuseBoundedWait();
										minutes++;
										continue;
									}
									try
									{
										int found = await ApproachShippedSpawnAsync(giver, skipBlockedTarget: true);
										await NaturalAltgardQuestSteps.UseObjectAsync(session, found, null, token);
										navigator.UnavailableObjects.Add(found);
										uses++;
									}
									catch (InvalidDataException) when (!Visible() && !session.Api.World.IsDead) { } // another bot took it first
								}
								session.TraceDiagnostic($"altgard-{altgardLegId}-item-start-object", new Dictionary<string, object?>
								{
									["quest"] = start.QuestId, ["minutes"] = minutes, ["uses"] = uses, ["owned"] = ItemCount(session.Api.World, start.ItemId),
								});
								await RestSafelyAsync(token);
								break;
							}
							int[] sources = start.SourceNpcIds!.Where(SpawnsOnMap).ToArray();
							Require.True(sources.Length > 0, $"No source of item {start.ItemId} spawns here.");
							int kills = 0;
							for (; kills < 150 && ItemCount(session.Api.World, start.ItemId) == 0 && !session.Api.World.IsDead; kills++)
							{
								int source = await KillShippedSpawnAsync(sources[kills % sources.Length]);
								await TryLootCorpseItemAsync(session, source, start.ItemId, token);
								navigator.UnavailableObjects.Add(source);
								await RestSafelyAsync(token);
								ExcuseBoundedWait();
							}
							session.TraceDiagnostic($"altgard-{altgardLegId}-item-start-hunt", new Dictionary<string, object?>
							{
								["quest"] = start.QuestId, ["item"] = start.ItemId, ["kills"] = kills, ["owned"] = ItemCount(session.Api.World, start.ItemId),
							});
							break;
						}
						case "use-item":
						{
							// M4: the use the decision names (a leg can have several).
							NaturalAltgardItemUse use = leg.ItemUseList.Single(entry => $"{entry.QuestId}:{entry.ItemId}" == next.StepKey);
							if (!use.Anywhere && use.ZoneAnchor is { } zone)
							{
								int preparationRetreats = combat.CompletedRetreats, preparationRevives = combat.ReviveCount;
								// AB-08: Q24013's poison works only inside its zone, and the zone is packed with Feral Sharpeyes (18
								// spawns, 295 s respawn): no route keeps clear of every circle. Do what a player does: take the road toward
								// the anchor, fighting what engages, and use the poison at the first step that is well inside the zone,
								// before the next Sharpeye walks up. A death or a retreat leaves the quest at its var, and the next
								// decision starts again from the bind.
								await EnsureOnGroundAsync();
								BotPosition anchor = new(zone[0], zone[1], zone[2], 0);
								bool Inside(BotPosition at) => use.InZone(at.X, at.Y, at.Z, ZoneMargin);
								if (!Inside(session.CurrentPosition) && !await WalkRoadDefendingAsync(anchor, "item-use-zone-road", stopAt: Inside))
								{
									RecordPreparationRetreat();
										break;
								}
								if (!await DefendAgainstEngagedAsync("item-use-zone") || session.Api.World.IsDead || !Inside(session.CurrentPosition))
								{
									await RestSafelyAsync(token);
									RecordPreparationRetreat();
									break;
								}
								session.TraceDiagnostic($"altgard-{altgardLegId}-item-zone", new Dictionary<string, object?>
								{
									["zone"] = use.Zone, ["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
								});

								void RecordPreparationRetreat()
								{
									itemApproachRetreated = combat.CompletedRetreats > preparationRetreats &&
										combat.ReviveCount == preparationRevives;
									if (!itemApproachRetreated) return;
									// The poison was not sent. Resume from the observed retreat position without
									// spending an item-use attempt; actual use retries and the watchdog stay bounded.
									session.TraceDiagnostic("quest-item-approach-retreated", new Dictionary<string, object?>
									{
										["questId"] = use.QuestId, ["itemId"] = use.ItemId, ["itemUseAttempted"] = false,
										["retreats"] = combat.CompletedRetreats - preparationRetreats,
										["position"] = session.CurrentPosition,
									});
								}
							}
							await NaturalAltgardQuestSteps.UseQuestItemAsync(session, use, runtime.Data.ItemDataDh.GetItemTemplate(use.ItemId), token);
							// The use may spawn monsters (Q24013: two Feral Black Claw Sharpeyes): deal with them first.
							await session.AdvanceAsync(TimeSpan.FromSeconds(4), token);
							await DefendAgainstEngagedAsync("item-use-spawns");
							await RestSafelyAsync(token);
							break;
						}
						case "bind":
						{
							// AB-08, the standing bind policy: bind at the obelisk of the hub the leg works out of. AG-07: a hub the
							// Cleric does not stand at (Leg 6: Trader's Berth, from the Basfelt bind) is reached by the hub flights.
							await EnsureOnGroundAsync();
							NaturalAltgardBind bind = leg.Bind ?? throw new InvalidDataException($"{leg.Leg} has no bind.");
							await FlyTowardAsync(new BotPosition(bind.Position[0], bind.Position[1], bind.Position[2], 0));
							int stone = await ApproachShippedSpawnAsync(bind.NpcId);
							NaturalServiceOutcome bound = await new NaturalServiceSteps(session).BindAsync(stone, session.Api.World.Objects[stone].Position,
								leg.Hub.MapId, bind.Price, bind.AcceptRange, token);
							Require.True(bound.IsDone, bound.Reason);
							break;
						}
						case "hunt":
						{
							// AB-08: a custom kill counter, one kill per decision (the var moves, so the next decision differs).
							await EnsureOnGroundAsync();
							int var = NaturalAltgardQuestSteps.State(session.Api.World, next.QuestId!.Value)?.Var ?? 0;
							NaturalAltgardHunt hunt = leg.HuntList.First(entry => entry.QuestId == next.QuestId && var >= entry.FromVar && var < entry.ToVar);
							bool Counted() => (NaturalAltgardQuestSteps.State(session.Api.World, next.QuestId!.Value)?.Var ?? 0) != var;
							try
							{
								if (leg.InstanceTripList.FirstOrDefault(trip => trip.QuestId == next.QuestId && trip.MapId == session.Api.World.MapId && var == trip.SpawnVar) is { } bossTrip)
								{
									NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(bossTrip.MapId, bossTrip.BossNpcId,
										new(bossTrip.BossPosition[0], bossTrip.BossPosition[1], bossTrip.BossPosition[2], 0),
										NaturalPullPlanner.SpellRange + 3, navigator, "quest-spawn", token);
									Require.True(reached.Arrived, reached.Reason);
									int boss = reached.TargetObjectId ?? await session.WaitForNpcAsync(bossTrip.BossNpcId, token);
									await PullAndKillAsync(boss, "instance-boss");
									await RestSafelyAsync(token);
									break;
								}
								// NL-3: a lure (Q24043): the monster is led into the radius of the point and struck there with an auto-attack.
								if (hunt.Lure is { Length: 3 } lure)
								{
									BotPosition point = new(lure[0], lure[1], lure[2], 0);
									for (int attempt = 0; attempt < 6 && !Counted() && !session.Api.World.IsDead; attempt++)
									{
										// aioncodex /48/ (Sinjah): "Once it's badly wounded, bring it close, and we'll capture it." The monster is
										// the one nearest the point, 85 m along the ledge: one drawn from 130 m away (diagnostic nl3-rq-d13) never
										// came more than 30 m from its spawn, and the nearest spawn to the bot is not the nearest to the point
										// (nl3-rq-d14: the Sorcerer's was, the Gladiator's never). So the bot looks from the point first.
										if (Distance(session.CurrentPosition, point) > hunt.LureRadius)
											await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, -1, point, hunt.LureRadius - 3, navigator, "lure-point", token);
										int drawn = 0;
										if (session.Api.World.Objects.Values.Where(known => known.TemplateId is int kind && hunt.NpcIds.Contains(kind) && !known.IsCorpse &&
											!navigator.UnavailableObjects.Contains(known.ObjectId) && Distance(known.Position, point) < 120)
											.MinBy(known => Distance(known.Position, point)) is { } nearest)
										{
											navigator.Led = new HashSet<int> { nearest.ObjectId };
											try
											{
												if ((await NaturalIshalgenNavigator.ApproachNpcObjectAsync(contract.MapId, nearest.TemplateId!.Value, nearest.ObjectId,
													nearest.Position, navigator, token)).Arrived)
													drawn = nearest.ObjectId;
											}
											finally { navigator.Led = new HashSet<int>(); }
										}
										if (drawn == 0) drawn = await ApproachShippedSpawnAsync(hunt.NpcIds.First(SpawnsOnMap), withinRange: 9);
										// The walks would fight the monster the bot leads (diagnostic nl3-rq-d12: the Sorcerer's spells killed it
										// 10 m from its spawn): the navigator leaves it alone until the attempt ends.
										navigator.Led = new HashSet<int> { drawn };
										try
										{
											// Java's AttackManager.checkGiveupDistance turns a monster home once it has neither struck nor been struck
											// for 20 s (Reshanta has no ai_info: chase_home 200); Sinjah stands 85 m from the nearest assaulter, and a
											// follower never catches a bot that runs. Trial nl3-rq-t1: six of eight ran ahead to the point, and the
											// monster turned back 8 m short of them; diagnostic nl3-rq-d11: it took the bot's Cleric companion (NL-a)
											// as its target and stayed beside it, out of the bot's reach. So the bot strikes it first, then leads it
											// in stretches, and strikes it each time it has caught up, a few seconds apart, walking back to it when
											// it hangs back, as a player keeps a long pull its own and from turning home.
											BotPosition seenAt = session.Api.World.Objects.TryGetValue(drawn, out BotKnownObject? atHome) ? atHome.Position : session.CurrentPosition;
											BotPosition spawnedAt = runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId).Where(group => hunt.NpcIds.Contains(group.GetNpcId()))
												.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))
												.DefaultIfEmpty(seenAt).MinBy(spot => Distance(spot, seenAt));
											long? lastStrike = null;
											int swings = 0, strikes = 0;
											float Gap() => session.Api.World.Objects.TryGetValue(drawn, out BotKnownObject? known)
												? Distance(known.Position, session.CurrentPosition) : float.MaxValue;
											float FromPoint() => session.Api.World.Objects.TryGetValue(drawn, out BotKnownObject? known)
												? Distance(known.Position, point) : float.MaxValue;
											bool StrikeDue() => Gap() < 45 && session.Api.World.Objects.TryGetValue(drawn, out BotKnownObject? led) &&
												Distance(led.Position, spawnedAt) > 20 && (lastStrike is not long struck || runtime.NowMillis - struck > 6000);
											// One swing from within reach: the monster comes to the bot or its companion, and the bot goes to it
											// when it stays beside the companion.
											async Task<bool> StrikeAsync()
											{
												for (int wait = 0; wait < 8 && Gap() > 5 && !session.Api.World.IsDead; wait++)
												{
													await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
													await session.SynchronizeAsync(token);
												}
												if (Gap() is > 5 and < 45 && session.Api.World.Objects.TryGetValue(drawn, out BotKnownObject? away))
													await NaturalIshalgenNavigator.ApproachNpcObjectAsync(contract.MapId, away.TemplateId ?? hunt.NpcIds[0], drawn, away.Position, navigator, token);
												if (Gap() > 5 || session.Api.World.IsDead) return false;
												await session.SendPacketAsync(session.Api.Target(drawn), token);
												await session.SendPacketAsync(session.Api.Attack(drawn, 1500, (byte)swings++), token);
												lastStrike = runtime.NowMillis;
												strikes++;
												await session.AdvanceAsync(TimeSpan.FromMilliseconds(1600), token);
												await session.SynchronizeAsync(token);
												return true;
											}
											bool led = await StrikeAsync();
											for (int stretch = 0; led && stretch < 16 && !Counted() && !session.Api.World.IsDead && FromPoint() > hunt.LureRadius - 1 &&
												Distance(session.CurrentPosition, point) > hunt.LureRadius - 3; stretch++)
											{
												await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, -1, point, hunt.LureRadius - 3, navigator,
													"lure-point", token, stopWhen: StrikeDue);
												if (StrikeDue()) led = await StrikeAsync();
											}
											if (led && !Counted() && !session.Api.World.IsDead && Distance(session.CurrentPosition, point) > hunt.LureRadius - 3)
												await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, -1, point, hunt.LureRadius - 3, navigator, "lure-point", token);
											float? closest = null;
											// The blow that counts comes the moment the monster is inside the radius: the guards by Sinjah kill it
											// if it lingers, and a guard's kill does not count. It is an auto-attack: Java's
											// PlayerController.attackTarget calls the quest's attack event before the swing, and skill damage has not
											// reached NpcController.onAttack since Java's 47ec44faf8 (the Cleric's spells at 5 m from Sinjah counted
											// for nothing).
											for (int wait = 0; led && wait < 40 && !Counted() && !session.Api.World.IsDead; wait++)
											{
												if (!session.Api.World.Objects.ContainsKey(drawn)) break;
												float gap = FromPoint();
												closest = MathF.Min(closest ?? gap, gap);
												if (gap > hunt.LureRadius - 1)
												{
													if (StrikeDue()) await StrikeAsync();
													await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
													await session.SynchronizeAsync(token);
													continue;
												}
												for (int swing = 0; swing < 4 && !Counted() && session.Api.World.Objects.ContainsKey(drawn) && FromPoint() <= hunt.LureRadius - 1; swing++)
												{
													lastStrike = null;
													if (!await StrikeAsync()) break;
												}
												if (!Counted()) await DefendAgainstEngagedAsync("lure-strike");
												break;
											}
											session.TraceDiagnostic($"altgard-{altgardLegId}-lure", new Dictionary<string, object?>
											{
												["quest"] = next.QuestId, ["attempt"] = attempt, ["monster"] = drawn, ["closest"] = closest, ["strikes"] = strikes,
												["led"] = led, ["counted"] = Counted(),
											});
										}
										finally { navigator.Led = new HashSet<int>(); }
										if (!Counted()) await RestSafelyAsync(token);
									}
									Require.True(Counted(), $"Q{next.QuestId}: no lured monster was struck within {hunt.LureRadius} m of the point.");
									break;
								}
								int huntKind = hunt.NpcIds.First(SpawnsOnMap);
								int target;
								// NR-R7b: the hunt ends when the quest has counted its kill. Comrade Sumarhon fell as an attacker in his own
								// camp, Q24112 counted him, and the kill step hunted him on for forty minutes and two deaths: it asked only
								// for a kill of its own. The kill step reads the count between its tries (RC-11) when it is given it.
								try { target = await KillShippedSpawnAsync(huntKind, objectiveDone: Counted); }
								catch (InvalidDataException noRoute) when (!Counted() && !session.Api.World.IsDead &&
									noRoute.Message.Contains("No collision-checked route", StringComparison.Ordinal))
								{
									// The navigator found no route from where the last fight or revive left the Cleric (the Leg 4
									// catch-up, toward Sumarhon). Take the planner's road to the nearest spawn, as talk steps do.
									BotPosition spawn = graph.GetMap(contract.MapId)!.Waypoints.Where(waypoint => waypoint.TemplateId == huntKind)
										.Select(waypoint => waypoint.Position).MinBy(position => Distance(position, session.CurrentPosition));
									session.TraceDiagnostic($"altgard-{altgardLegId}-hunt-road", new Dictionary<string, object?>
									{
										["quest"] = next.QuestId, ["kind"] = huntKind, ["spawn"] = spawn, ["reason"] = noRoute.Message,
									});
									if (!await WalkRoadDefendingAsync(spawn, $"hunt-road-{huntKind}", within: 40) && !session.Api.World.IsDead)
										await UseLearnedReturnToBindAsync();
									target = await KillShippedSpawnAsync(huntKind, objectiveDone: Counted);
								}
								// A hunt the count ended names no monster of its own.
								if (target != 0) navigator.UnavailableObjects.Add(target);
							}
							catch (InvalidDataException exception) when (Counted())
							{
								// The kill counted on the way, in a fight-through (Q24112: Sumarhon fell as an attacker in his own
								// camp), and the search then waited for a spawn that was already dead. The var is what counts.
								session.TraceDiagnostic($"altgard-{altgardLegId}-hunt-counted-on-the-way", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["from"] = var, ["reason"] = exception.Message,
								});
							}
							catch (TimeoutException stalled) when (NaturalAltgardContract.MusteredLegs.Contains(altgardLegId!) &&
								stalled.Message.StartsWith("Natural journey made no quest", StringComparison.Ordinal))
							{
								// M4: a kill this bot cannot make alone in the watchdog's hour (Q24022's Protector Karhel, a level-35 EXPERT): the
								// quest, and what needs it first, is left for the group step (M6).
								deferredToGroup.Add(next.QuestId!.Value);
								session.TraceDiagnostic($"altgard-{altgardLegId}-deferred-to-group", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["var"] = var, ["level"] = session.Api.World.Level, ["position"] = session.CurrentPosition,
								});
								ExcuseBoundedWait();
							}
							await RestSafelyAsync(token);
							break;
						}
						case "timed":
							await RunTimedQuestAsync(next.QuestId!.Value);
							break;
						case "spawn-kill":
							await RunSpawnKillAsync(leg.SpawnList.Single(spawn => spawn.Key == next.StepKey));
							break;
						case "air-kills":
						{
							// No fungus is visible from the ground: the fight starts from Borender's rock (AF-06).
							await FlyToAsync(Rock());
							// NR-36: the class's own air attack: a skill of a role its profile names, or its weapon's swing.
							var heldWeapon = session.Api.World.Inventory.Values.SingleOrDefault(item => item.Details.EquippedSlot is 1 or 3) is { } held
								? runtime.Data.ItemDataDh.GetItemTemplate(held.ItemId)?.GetWeaponStats() : null;
							NaturalAirAttack airAttack = NaturalAirCombat.AttackFor(combat.ClassProfile, session.Api.World.Skills.ContainsKey,
								heldWeapon?.GetAttackRange(), heldWeapon?.GetAttackSpeed());
							NaturalAirCombat.Outcome outcome = await NaturalAirCombat.RunAsync(session, geometry, leg.Hub.MapId, zones,
								leg.RequiredFlight.WaterLevel, new NaturalLandingTarget("platform", Rock()), cruise, leg.RequiredAirKills.QuestId,
								(origin, skill, level, target) => runtime.CreateSpellCast(session.Api.World, origin, skill, level, target),
								() => runtime.NowMillis, token, attack: airAttack);
							lastTakeoff = runtime.NowMillis;
							session.TraceDiagnostic($"altgard-{altgardLegId}-air-kills", new Dictionary<string, object?>
							{
								["kills"] = outcome.Kills, ["sorties"] = outcome.Sorties, ["missed"] = outcome.Missed,
								["shootingSeconds"] = outcome.ShootingSeconds, ["landedFp"] = outcome.LandedFp,
							});
							break;
						}
						case "hunt-for-level":
						{
							await EnsureOnGroundAsync();
							int[] hunted = altgardPlans.Values.SelectMany(plan => plan.Steps.Where(step => step.Kind == "kill"))
								.SelectMany(step => step.Npcs.Select(npc => npc.Id)).Where(SpawnsOnMap).Distinct().ToArray();
							int levelBefore = session.Api.World.Level;
							for (int kill = 0; kill < 12 && session.Api.World.Level == levelBefore; kill++)
							{
								int target = await KillShippedSpawnAsync(hunted[kill % hunted.Length]);
								navigator.UnavailableObjects.Add(target);
								await RestSafelyAsync(token);
							}
							break;
						}
						case "use-object":
						{
							// AM-04/05: a quest object in the world (Q2213's Okaru Tree, Q24012's carts), used as the client does.
							await EnsureOnGroundAsync();
							NaturalAltgardObjectUse use = leg.ObjectUseList.Single(candidate => candidate.Key == next.StepKey);
							if (use.TakesItemId is int takes && ItemCount(session.Api.World, takes) == 0)
							{
								// NL-3: the operator, 2026-10-17: "Seed our bots with a temporal stone when needed". No vendor or drop of
								// this server gives it (decision 8); the leg's approval does, once, before the use.
								Func<int, long, CancellationToken, Task> supply = runtime.SupplyHelpItemAsync
									?? throw new InvalidOperationException($"{use.Key}: the use takes item {takes}, and this run supplies nothing.");
								long already = legItemsSupplied.GetValueOrDefault(takes);
								NaturalHelpItemSupply.RequireApproved(takes, 1, altgardLegId, already);
								await supply(takes, 1, token);
								await session.SynchronizeAsync(token);
								legItemsSupplied[takes] = already + 1;
								session.TraceDiagnostic("help-item-supplied", new Dictionary<string, object?>
								{
									["trigger"] = use.Key, ["itemId"] = takes, ["count"] = 1, ["after"] = ItemCount(session.Api.World, takes),
								});
							}
							// NL-3: the Artifact of the Inception (Q24044) stands on a base no navmesh covers, and the walk to the
							// ordinary 3 m found no approach point 9 m from it for an hour (nl3-ro-d11); its talk distance is 5, the mesh ends 5.6 m
							// from it, and the server takes a use from 6.6 m (5 + 1 m between the bodies' edges). In the
							// mustered legs an object is used from its talk range.
							float? useRange = use.MapId != null ? TalkRange(use.NpcId)
								: altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) ? MathF.Max(3, TalkRange(use.NpcId) + 1) : null;
							// The navigator plans to the object itself and finds no approach point where no mesh lies near it
							// (nl3-ro-d12): the bot first steps within reach of it.
							if (useRange is float reach && use.MapId == null &&
								runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId).Where(group => group.GetNpcId() == use.NpcId)
									.SelectMany(group => group.GetSpawnTemplates()).Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0))
									.OrderBy(spot => Distance(spot, session.CurrentPosition)).FirstOrDefault() is { } objectAt && objectAt != default &&
								Distance(session.CurrentPosition, objectAt) is > 0 and < 40)
								await StepWithinReachAsync(geometry, navigator, contract.MapId, objectAt, reach - 0.3f, use.Key);
							int item = await ApproachShippedSpawnAsync(use.NpcId, skipBlockedTarget: true, withinRange: useRange);
							if (altgardLegId == "l10")
							{
								// Java ItemUseObserver aborts the bar on an attack. The last approach can draw
								// another instance enemy even after its earlier guards were cleared.
								await RestSafelyAsync(token);
								if (NaturalAltgardQuestSteps.State(session.Api.World, use.QuestId)?.Var != use.FromVar) break;
								item = await ApproachShippedSpawnAsync(use.NpcId, skipBlockedTarget: true, withinRange: TalkRange(use.NpcId));
							}
							// NR-R4b: the server cuts a use bar short when the player is attacked (Java ItemUseObserver 17-60). The walk
							// to another object defends on its way. The walk to the same object ends where the bot stands, and used
							// again at once the monster that cut the use short cuts every use short, until the leg's count of repeated
							// decisions ends it: the Gladiator, the Assassin and the Bard at Q2213's Okaru Tree in round 4. So before
							// the object whose use was just cut short is used again, the journey's own defence and rest run, as leg
							// 10 has them before every use.
							if (item == useCutShortObject)
							{
								session.TraceDiagnostic("object-use-again-after-cut-short", new Dictionary<string, object?>
								{
									["use"] = use.Key, ["object"] = item, ["hp"] = session.Api.World.CurrentHp, ["position"] = session.CurrentPosition,
								});
								await RestSafelyAsync(token);
								if (NaturalAltgardQuestSteps.State(session.Api.World, use.QuestId)?.Var != use.FromVar) break;
								item = await ApproachShippedSpawnAsync(use.NpcId, skipBlockedTarget: true,
									withinRange: use.MapId == null ? null : TalkRange(use.NpcId));
							}
							// AC-05: the use may open a dialog first (Q2221's safe), answered with the contract's close action.
							int useStart = session.PacketHistory.Count;
							bool used = await NaturalAltgardQuestSteps.UseContractObjectAsync(session, use, item, token);
							useCutShortObject = !used && session.PacketHistory.Skip(useStart).Any(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
								packet.Get<int>("targetObjectId") == item && packet.Get<byte>("actionType") == 2 && packet.Get<int>("durationMs") <= 0)
								? item : 0;
							if (used && use.MovieId != null)
							{
								await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == leg.Hub.MapId);
								await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
								session.AcceptTeleportPosition();
								await session.SynchronizeAsync(token);
							}
							// A used cart dies but stays in view (AM-05): never pick it again.
							if (use.Disappears) navigator.UnavailableObjects.Add(item);
							session.TraceDiagnostic($"altgard-{altgardLegId}-object", new Dictionary<string, object?>
							{
								["use"] = use.Key, ["object"] = item, ["used"] = used,
								["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, use.QuestId)?.ToString(),
							});
							break;
						}
						case "escort":
							await EnsureOnGroundAsync();
							await RunEscortAsync(leg.EscortList.Single(candidate => candidate.Key == next.StepKey));
							break;
						case "enter-zone":
						{
							// Q24012's farmland step: the quest's own objects stand inside the zone, so walking to the nearest one
							// enters it (the hub's NPCs stand outside the zone polygon, AM-05).
							await EnsureOnGroundAsync();
							NaturalAltgardZoneStep zone = leg.ZoneStepList.Single(entry => entry.QuestId == next.QuestId);
							if (zone.Anchor is { } anchor)
							{
								var progress = new NaturalApproachProgress();
								for (int attempt = 0; NaturalAltgardQuestSteps.State(session.Api.World, zone.QuestId)?.Var == zone.FromVar; attempt++)
								{
									Require.True(progress.CanRetry(attempt) && progress.StalledAttempts < 3,
										$"Q{zone.QuestId} zone approach exhausted its bounded progress attempts.");
									BotPosition before = session.CurrentPosition;
									int revives = combat.ReviveCount, kills = navigator.UnavailableObjects.Count;
									bool reached = await WalkRoadDefendingAsync(new(anchor[0], anchor[1], anchor[2], 0), "campaign-zone", within: Math.Min(3, zone.Radius));
									if (session.Api.World.IsDead || combat.ReviveCount != revives) break;
									bool advanced = progress.Observe(Distance(before, session.CurrentPosition), navigator.UnavailableObjects.Count > kills);
									session.TraceDiagnostic("campaign-zone-approach-progress", new Dictionary<string, object?>
									{
										["quest"] = zone.QuestId, ["attempt"] = attempt + 1, ["advanced"] = advanced,
										["stalls"] = progress.StalledAttempts, ["position"] = session.CurrentPosition, ["reached"] = reached,
									});
									if (reached) break;
								}
							}
							else
								await ApproachShippedSpawnAsync(leg.ObjectUseList.First(use => use.QuestId == next.QuestId).NpcId, skipBlockedTarget: true);
							break;
						}
						case "collect":
						{
							// Q24012's hairpins and waist bands, which drop only from var 5 (AM-05). AM-Q3: the SEASONED black claw
							// patrols are not sought out; they are fought only when they block the way.
							await EnsureOnGroundAsync();
							NaturalAltgardCollection collection = leg.CollectionList.Single(entry => entry.QuestId == next.QuestId);
							NaturalAltgardCollectedItem wanted = collection.Items.First(entry => ItemCount(session.Api.World, entry.ItemId) < entry.Count);
							// BC-06: the campaign's boss can be killed and looted while clearing its approach.
							// Hand the observed item back just as the template collection loop does.
							int? CollectedCampaignSource() => altgardLegId == "l10" && ItemCount(session.Api.World, wanted.ItemId) >= wanted.Count &&
								SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? swept)
								? swept.Where(entry => entry.Value.Contains(wanted.ItemId)).Select(entry => (int?)entry.Key).LastOrDefault()
								: null;
							int[] sources = wanted.SourceNpcIds.Where(SpawnsOnMap)
								.Where(id => runtime.Data.NpcDataDh.GetNpcTemplate(id)?.GetRank() != Aion.GameServer.Model.Templates.Npc.NpcRank.SEASONED)
								.ToArray();
							// AB-08: Komu's Horn has one source, a SEASONED one; take it. If he is dead (an hourly respawn, AB-Q3), hunt
							// the grounds around him until he is back.
							if (sources.Length == 0) sources = wanted.SourceNpcIds.Where(SpawnsOnMap).ToArray();
							Require.True(sources.Length > 0, $"No source of item {wanted.ItemId} spawns here.");
							if (leg.AvoidList.FirstOrDefault(avoid => sources.Contains(avoid.NpcId)) is { } boss)
							{
								await ApproachShippedSpawnAsync(boss.NpcId, skipBlockedTarget: true, withinRange: NaturalPullPlanner.SpellRange + 3,
									completedSource: altgardLegId == "l10" ? CollectedCampaignSource : null);
								long waitUntil = runtime.NowMillis + boss.RespawnSeconds * 1000L;
								while (CollectedCampaignSource() == null && !session.Api.World.Objects.Values.Any(known => known.TemplateId == boss.NpcId && !known.IsCorpse) && runtime.NowMillis < waitUntil)
								{
									session.TraceDiagnostic($"altgard-{altgardLegId}-wait-for-respawn", new Dictionary<string, object?> { ["npc"] = boss.NpcId });
									// Komu's neighbours (AB-02): bigfoot and ruthless mosbears and grove malodors.
									if (boss.NpcId == 210566)
									{
										int[] around = [210441, 210581, 210444];
										int prey = await KillShippedSpawnAsync(around.First(SpawnsOnMap));
										navigator.UnavailableObjects.Add(prey);
									}
									else await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
									await RestSafelyAsync(token);
									await ApproachShippedSpawnAsync(boss.NpcId, skipBlockedTarget: true, withinRange: NaturalPullPlanner.SpellRange + 3,
										completedSource: altgardLegId == "l10" ? CollectedCampaignSource : null);
								}
							}
							try
							{
								for (int kill = 0; kill < 6 && ItemCount(session.Api.World, wanted.ItemId) < wanted.Count; kill++)
								{
									int source;
									try
									{
										source = await KillShippedSpawnAsync(sources[kill % sources.Length], altgardLegId == "l10"
											? new(QuestRunOperationKind.CollectQuestDrop, Count: wanted.Count, ItemId: wanted.ItemId) : null);
									}
									// M4: a source of the turn that is not there (Q24021's lone 212415 spectre, which other bots kill too) gives the turn to
									// the next source, as a template collection does; a lone source (Deputy Lamipedon) is waited for again, and the
									// journey's hour leaves the quest for the group step if he never comes to this bot.
									catch (InvalidDataException absent) when (!session.Api.World.IsDead &&
										absent.Message.StartsWith("No client-observed NPC", StringComparison.Ordinal))
									{
										session.TraceDiagnostic("collect-source-absent", new Dictionary<string, object?>
										{
											["quest"] = next.QuestId, ["item"] = wanted.ItemId, ["source"] = sources[kill % sources.Length],
										});
										continue;
									}
									await TryLootCorpseItemAsync(session, source, wanted.ItemId, token);
									navigator.UnavailableObjects.Add(source);
									await RestSafelyAsync(token);
								}
							}
							catch (NaturalLeftInstanceRoomException)
							{
								session.TraceDiagnostic($"altgard-{altgardLegId}-room-death", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["item"] = wanted.ItemId, ["revives"] = combat.ReviveCount, ["position"] = session.CurrentPosition,
								});
							}
							// NL-4: a solo instance's quest has no group step to leave it for; the stall stops the journey as it is.
							catch (TimeoutException stalled) when (NaturalAltgardContract.MusteredLegs.Contains(altgardLegId!) &&
								stalled.Message.StartsWith("Natural journey made no quest", StringComparison.Ordinal) && leg.InstanceRooms?.QuestId != next.QuestId)
							{
								// M4: a drop this bot cannot get alone in the watchdog's hour (Q24021's Deputy Lamipedon, an EXPERT in the middle of
								// the spectres' ground): the quest, and what needs it first, is left for the group step (M6).
								deferredToGroup.Add(next.QuestId!.Value);
								session.TraceDiagnostic($"altgard-{altgardLegId}-deferred-to-group", new Dictionary<string, object?>
								{
									["quest"] = next.QuestId, ["item"] = wanted.ItemId, ["level"] = session.Api.World.Level, ["position"] = session.CurrentPosition,
								});
								ExcuseBoundedWait();
								await RestSafelyAsync(token);
							}
							break;
						}
						case "return-to-endpoint":
						{
							// AM-Q1: Leg 2 ends at Manir's Campsite, beside the NPC who takes its last hand-in.
							// NR-R16b: a leg with no hand-in where it ends (leg 11 has no template quest) ends at its obelisk.
							await EnsureOnGroundAsync();
							float[] end = leg.Endpoint.Anchor ?? leg.Hub.Anchor;
							QuestRunNpc? endNpc = altgardPlans.Values.SelectMany(plan => plan.EndNpcs)
								.FirstOrDefault(npc => npc.Positions.Any(at => MathF.Sqrt(MathF.Pow(at.X - end[0], 2) + MathF.Pow(at.Y - end[1], 2)) <= leg.Endpoint.Radius));
							if (endNpc != null) await ApproachShippedSpawnAsync(endNpc.Id);
							else await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId
								?? throw new InvalidDataException($"{leg.Leg} ends at no hand-in NPC and at no obelisk."), withinRange: 5);
							break;
						}
						case "cube-expansion":
							await VisitLeg5CapitalAsync();
							break;
						case "town-service":
						{
							// AK-08: the cube is too full to loot. At the town's merchant: wear the upgrades, then sell what the inventory
							// policy calls surplus (never buying anything, OD-7), as the bridge's shop stop does.
							await EnsureOnGroundAsync();
							await EquipUpgradesAsync(token);
							await session.SynchronizeAsync(token);
							BotWorldModel world = session.Api.World;
							NaturalInventoryPlan plan = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(item => item.ItemId), ClassLine)
								.Decide(world, QuestNeededItems(), leg.CoinGear, leg.Haramel);
							var sales = plan.Sales.Select(sale => new NaturalSale(sale.ObjectId, sale.ItemId, sale.Count)).ToList();
							int vendor = await ApproachShippedSpawnAsync(leg.Town!.VendorNpcId!.Value);
							NaturalVendorResult trade = await new NaturalServiceSteps(session).TradeAsync(vendor, sales, [],
								id => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList() ?? [],
								item => runtime.Data.ItemDataDh.GetItemTemplate(item).GetPrice(), token);
							session.TraceDiagnostic("town-service", new Dictionary<string, object?>
							{
								["vendor"] = leg.Town.VendorNpcId, ["freeBefore"] = plan.FreeSlots, ["freeAfter"] = FreeCubeSlots(),
								["sold"] = trade.Sold.Select(sold => $"{sold.ItemId}x{sold.Count}").ToArray(), ["kinah"] = world.Kinah,
							});
							break;
						}
						case "carrier-hunt":
						{
							// AK-08: a ring carrier present in its hours (Q2292): walk to its spawn, kill it with the journey's combat,
							// and loot its ring. Gone or dead when the Cleric arrives (another player, a respawn), the next decision
							// asks the policy again with the clock moved on.
							await EnsureOnGroundAsync();
							NaturalAltgardTimedSpawn carrier = leg.TimedSpawnList.Single(candidate => $"{candidate.NpcId}" == next.StepKey);
							int target;
							try { target = await KillShippedSpawnAsync(carrier.NpcId); }
							catch (InvalidDataException missing) when (missing.Message.Contains("No client-observed NPC", StringComparison.Ordinal))
							{
								session.TraceDiagnostic("carrier-missed", new Dictionary<string, object?>
								{
									["carrier"] = carrier.NpcId, ["ring"] = carrier.ItemId, ["gameMinutes"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
									["reason"] = missing.Message,
								});
								break;
							}
							navigator.UnavailableObjects.Add(target);
							bool looted = ItemCount(session.Api.World, carrier.ItemId) > 0 ||
								await TryLootCorpseItemAsync(session, target, carrier.ItemId, token);
							session.TraceDiagnostic("carrier-killed", new Dictionary<string, object?>
							{
								["carrier"] = carrier.NpcId, ["ring"] = carrier.ItemId, ["looted"] = looted,
								["gameMinutes"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
							});
							await RestSafelyAsync(token);
							break;
						}
						case "wait-for-carrier":
						case "wait-for-escort":
						{
							// AK-08 (AK-Q3): nothing else is left before a carrier's window opens (AG-07, AG-Q3 (a): or an escort
							// follower's). Wait at the hub's obelisk, an hour of game time (5 real minutes) per decision, so the clock is
							// read again as it runs.
							await EnsureOnGroundAsync();
							float[] hub = leg.Hub.Anchor;
							if (MathF.Sqrt(MathF.Pow(session.CurrentPosition.X - hub[0], 2) + MathF.Pow(session.CurrentPosition.Y - hub[1], 2)) > leg.Hub.Radius)
								await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId ?? leg.Bind?.NpcId ?? leg.Start.BindNpcId);
							await RestSafelyAsync(token);
							long before = session.Api.World.GameMinutesAt(session.Api.Timing.Now) ?? 0;
							await session.AdvanceAsync(TimeSpan.FromMinutes(5), token);
							await session.SynchronizeAsync(token);
							bool escortWait = next.Action == "wait-for-escort";
							session.TraceDiagnostic(escortWait ? "escort-wait" : "carrier-wait", new Dictionary<string, object?>
							{
								[escortWait ? "escort" : "carrier"] = next.StepKey, ["from"] = before,
								["to"] = session.Api.World.GameMinutesAt(session.Api.Timing.Now),
							});
							break;
						}
						case "return-to-hub":
							await EnsureOnGroundAsync();
							// AB-08: the hub's own obelisk when the leg binds there (Basfelt), not the one the leg started bound at.
							await ApproachShippedSpawnAsync(leg.Endpoint.BindNpcId ?? leg.Bind?.NpcId ?? leg.Start.BindNpcId);
							break;
						default:
							throw new InvalidDataException($"The Altgard leg runner has no executor for {next.Action}.");
					}
				}
				throw new InvalidDataException("An Altgard leg exceeded 400 decisions.");

				// Fly from wherever the Cleric stands (AF-05): wait out the takeoff reuse and a refill, check the policy first.
				// NL-3: with mayRefuse, a flight that the speed the bot took off at cannot carry (Soul Sickness after a revive
				// slows it: 592 m cost 77 FP of 72) lands again where it rose and answers false; the hops plan again at that speed.
				// NL-3: extraFp is the flight time the bot's Wind Serums will add on the way (drunk by inFlight between the flight's
				// pieces), counted when the flight is planned.
				async Task<bool> FlyToAsync(BotPosition destination, float? cruiseHeight = null, NaturalAltgardFlight? flightRules = null, bool land = true,
					bool mayRefuse = false, int extraFp = 0, Func<Task>? inFlight = null)
				{
					// Java PlayerController.updateSoulSickness applies 8291 on bind revival. Its speed penalty
					// doubles the pillar descent's FP cost; wait for the real icon removal before taking off.
					if (altgardLegId is "l9" or "l10" or "l12" && session.Api.World.VisibleEffects?.FirstOrDefault(effect => effect.SkillId == 8291) is { } sickness)
					{
						Require.True(sickness.RemainingMillis > 0, "Soul Sickness has no observed expiry for the flight wait.");
						long until = runtime.NowMillis + sickness.RemainingMillis + 1000L;
						session.TraceDiagnostic("flight-wait-for-soul-sickness", new Dictionary<string, object?>
						{
							["skillId"] = sickness.SkillId, ["remainingMillis"] = sickness.RemainingMillis,
						});
						while (NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291) && runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) { await RestSafelyAsync(token); return false; }
						}
						Require.True(!NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291), "Soul Sickness did not expire after its observed flight wait.");
					}
					BotPosition from = session.CurrentPosition;
					if (Distance(from, destination) < 3) return true;
					BotWorldModel world = session.Api.World;
					long wait = NaturalFlightPolicy.RestoreMillis(world.CurrentFlightTime, world.MaxFlightTime, world.MaxFlightTime);
					if (lastTakeoff is { } last) wait = Math.Max(wait, last + NaturalFlightPolicy.TakeoffReuseMillis - runtime.NowMillis);
					// NL-3: the wait for flight time answers what attacks the bot (the Ranger died waiting on Mokkurkalfi's ledge and
					// then waited for a takeoff while dead), and the flight leaves from where the fight left it.
					int waitingRevives = combat.ReviveCount;
					int? waitingMap = world.MapId;
					if (wait > 0 && !await WaitBeforePullDefendingAsync((int)wait + 100, "flight-wait")) return false;
					await session.SynchronizeAsync(token);
					if (world.IsDead || world.MapId != waitingMap || combat.ReviveCount != waitingRevives)
					{
						session.TraceDiagnostic("flight-wait-replan", new Dictionary<string, object?>
						{ ["from"] = from, ["position"] = session.CurrentPosition, ["revives"] = combat.ReviveCount - waitingRevives });
						return false;
					}
					from = session.CurrentPosition;
					NaturalFlightDecision ready = NaturalFlightPolicy.CanTakeOff(new NaturalTakeoffObservation(true, from, false,
						(flightRules ?? leg.RequiredFlight).WaterLevel, runtime.NowMillis, lastTakeoff, false, false, false), zones);
					if (!ready.Allowed && mayRefuse) return false;
					Require.True(ready.Allowed, $"Cannot take off: {ready.Reason}");
					int flightMap = session.Api.World.MapId ?? leg.Hub.MapId;
					NaturalFlightRoute route = NaturalFlightProtocol.Plan(geometry, flightMap, from, destination, cruiseHeight ?? cruise);
					if (!route.IsUsable && flightMap != NaturalFlightRings.AbyssMap && mayRefuse) return false;
					if (flightMap != NaturalFlightRings.AbyssMap) Require.True(route.IsUsable, $"No flight to {destination}: {route.Refusal}");
					lastTakeoff = runtime.NowMillis;
					float speed = await NaturalFlightProtocol.TakeOffAsync(session, token);
					lastFlightSpeed = speed;
					NaturalFlightDecision go = route.IsUsable
						? NaturalFlightPolicy.CanFly(NaturalFlightProtocol.ToPlan(route, from, speed), world.CurrentFlightTime + extraFp, zones)
						: new(false, route.Refusal ?? "No direct flight.");
					if (!go.Allowed && flightRings.Plan(geometry, flightMap, from, destination, speed,
						world.CurrentFlightTime, world.MaxFlightTime, zones) is { } ringRoute)
					{
						if (!await NaturalFlightRings.FlyAsync(session, geometry, zones, ringRoute, token)) return false;
						if (land) await NaturalFlightProtocol.LandAsync(session, token);
						await session.SynchronizeAsync(token);
						return true;
					}
					session.TraceDiagnostic($"altgard-{altgardLegId}-flight", new Dictionary<string, object?>
					{
						["from"] = from, ["to"] = destination, ["meters"] = route.Meters, ["fp"] = world.CurrentFlightTime, ["decision"] = go.Reason,
					});
					if (!go.Allowed)
					{
						await NaturalFlightProtocol.LandAsync(session, token);
						await session.SynchronizeAsync(token);
						if (mayRefuse) return false;
					}
					Require.True(go.Allowed, go.Reason);
					await NaturalFlightProtocol.FlyAsync(session, flightMap, from, route.Waypoints, speed, token, inFlight);
					if (land) await NaturalFlightProtocol.LandAsync(session, token);
					await session.SynchronizeAsync(token);
					return true;
				}

				// NL-3 (the operator, 2026-10-17: "You fly up to the ring in the middle of the air, and 'use' the door while flying.
				// It takes several seconds to use the door, then you get teleported"): a portal that hangs in the air (the Morheim
				// Abyss Gate, under a canopy over its platform) is used from beside it, in flight. The bot flies from the nearest
				// ground from which a checked flight reaches a point beside the portal within its reach, and stays in the air.
				async Task<bool> FlyBesideAsync(BotPosition goal, float within)
				{
					int map = session.Api.World.MapId!.Value;
					// A retained trip can start at another hub. Take its normal flight transporter
					// before choosing nearby takeoff ground, as the hub-to-hub approaches do.
					if (!approachingAirline)
					{
						approachingAirline = true;
						try { await FlyTowardAsync(goal); }
						finally { approachingAirline = false; }
						if (session.Api.World.IsDead || session.Api.World.MapId != map) return false;
					}
					NaturalFlyZone? zone = zones.FirstOrDefault(entry => !entry.Forbids && entry.Contains(goal.X, goal.Y, goal.Z));
					if (zone == null) return false;
					bool Inside(BotPosition at) => zone.Contains(at.X, at.Y, at.Z + 2) && at.Z + 12 < zone.Top &&
						!zones.Any(other => other.Forbids && other.Contains(at.X, at.Y, at.Z + 2));
					BotPosition[] hovers = [.. new[] { 0f, 4f, 8f, 11f }.SelectMany(radius => radius == 0 ? [goal]
						: Enumerable.Range(0, 8).Select(step => goal with
						{
							X = goal.X + radius * MathF.Cos(step * MathF.PI / 4), Y = goal.Y + radius * MathF.Sin(step * MathF.PI / 4),
						})).Where(at => Distance(at, goal) <= within)];
					float Cruise(BotPosition from, BotPosition to) => MathF.Min(zone.Top - 6, MathF.Max(from.Z, to.Z) + 5);
					BotPosition here = session.CurrentPosition;
					BotPosition[] ground = [.. geometry.GroundNear(map, goal, 150, Inside).OrderBy(at => Distance(at, here))];
					int every = Math.Max(1, ground.Length / 150);
					(BotPosition Takeoff, BotPosition Hover)? chosen = (Inside(here) ? new[] { here } : [])
						.Concat(ground.Where((_, index) => index % every == 0))
						.SelectMany(takeoff => hovers.Select(hover => (Takeoff: takeoff, Hover: hover)))
						.Where(pair => NaturalFlightProtocol.DirectlyClear(geometry, map, pair.Takeoff, pair.Hover, Cruise(pair.Takeoff, pair.Hover)))
						.Cast<(BotPosition, BotPosition)?>().FirstOrDefault();
					session.TraceDiagnostic("fly-beside", new Dictionary<string, object?>
					{
						["goal"] = goal, ["position"] = here, ["ground"] = ground.Length, ["takeoff"] = chosen?.Takeoff, ["hover"] = chosen?.Hover,
					});
					if (chosen is not { } pick) return false;
					if (Distance(here, pick.Takeoff) > 3 && (!await WalkRoadDefendingAsync(pick.Takeoff, "fly-beside-takeoff", within: 3) ||
						session.Api.World.IsDead)) return false;
					float height = Cruise(session.CurrentPosition, pick.Hover);
					if (!NaturalFlightProtocol.Plan(geometry, map, session.CurrentPosition, pick.Hover, height).IsUsable) return false;
					await FlyToAsync(pick.Hover, height, land: false);
					return true;
				}

				// Everything but Borender's steps and the air fight is on foot: come down from the rock first.
				async Task EnsureOnGroundAsync()
				{
					// M2: Leg 1's rock stands far above a flat fortress, so height alone says the bot is on it. A landing on a
					// wall of Morheim Ice Fortress is sixteen metres above its yard: there the bot is on it when it stands at it.
					bool onTheLanding = rockTop is { } top && (leg.AirKills != null ? session.CurrentPosition.Z > top.Z - 20
						: Distance(session.CurrentPosition, top) <= 12 && MathF.Abs(session.CurrentPosition.Z - top.Z) <= 4);
					if (onTheLanding) await FlyToAsync(ground);
				}

				async Task ReachPillarLevelAsync(BotPosition destination, NaturalAltgardContract? pillarLeg = null)
				{
					NaturalAltgardContract flightLeg = pillarLeg ?? leg;
					if (flightLeg.PillarFlight is not { } pillar || session.Api.World.MapId != flightLeg.Hub.MapId ||
						pillar.IsUpper(session.CurrentPosition.Z) == pillar.IsUpper(destination.Z)) return;
					bool goingUp = pillar.IsUpper(destination.Z);
					float[] departure = goingUp ? pillar.Lower : pillar.Upper;
					float[] arrival = goingUp ? pillar.Upper : pillar.Lower;
					BotPosition start = new(departure[0], departure[1], departure[2], 0);
					BotPosition landing = new(arrival[0], arrival[1], arrival[2], 0);
					if (Distance(session.CurrentPosition, start) > 5)
						await WalkRoadDefendingAsync(start, "pillar-takeoff-road", within: 5);
					NaturalNavigationResult atStart = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						leg.Hub.MapId, -1, start, navigator, "pillar-takeoff", token);
					Require.True(atStart.Arrived, atStart.Reason);
					await DefendAgainstEngagedAsync("pillar-takeoff");
					await RestSafelyAsync(token);
					if (pillar.IsUpper(session.CurrentPosition.Z) == goingUp) return; // a revive can already reach the upper bind
					await FlyToAsync(landing, MathF.Max(start.Z, landing.Z) + 12, flightLeg.RequiredFlight);
				}

				// AB-08: walk the Altgard travel planner's road to a point (the navmesh path when it has none), one section of 16 points
				// at a time, defending against whatever engages between sections.
				// The maintainer's 2026-10-01 note: fly between hubs rather than walk. AG-00: take the quickest journey by walks and
				// flight transporters (NaturalAirlineRoutes.Journey), one flight at a time, planning again from each landing, so
				// Basfelt to Trader's Berth is two flights. False when walking is better from the start.
				async Task<bool> FlyTowardAsync(BotPosition destination)
				{
					int map = session.Api.World.MapId!.Value;
					int flown = 0;
					for (NaturalAirlineJourney? journey = NaturalAirlineRoutes.Journey(airlines, map, session.CurrentPosition, destination);
						journey != null && flown < 4;
						journey = NaturalAirlineRoutes.Journey(airlines, map, session.CurrentPosition, destination))
					{
						NaturalAirlineRoute route = journey.Flights.First();
						session.TraceDiagnostic("airline-chosen", new Dictionary<string, object?>
						{
							["route"] = route.Route, ["npc"] = route.NpcId, ["location"] = route.LocationId, ["from"] = session.CurrentPosition,
							["destination"] = destination, ["flights"] = journey.Flights.Select(flight => flight.Route).ToArray(),
							["seconds"] = journey.Seconds, ["walkAllSeconds"] = journey.WalkAllSeconds,
						});
						if (altgardLegId == "l12") await ReachPillarLevelAsync(route.Departure);
						if (Distance(session.CurrentPosition, route.Departure) > 60)
							await WalkRoadDefendingAsync(route.Departure, "airline-road", within: 15);
						int transporter = await ApproachShippedSpawnAsync(route.NpcId);
						if (Distance(session.CurrentPosition, route.Departure) > 3)
						{
							NaturalNavigationResult atPad = await NaturalIshalgenNavigator.ExploreAnchorAsync(
								contract.MapId, -1, route.Departure, navigator, "airline-departure", token);
							Require.True(atPad.Arrived, atPad.Reason);
						}
						NaturalServiceOutcome outcome = await new NaturalServiceSteps(session).FlyAsync(transporter,
							session.Api.World.Objects[transporter].Position, 6, route, token);
						Require.True(outcome.IsDone, outcome.Reason);
						flown++;
					}
					return flown > 0;
				}

				async Task<bool> WalkRoadDefendingAsync(BotPosition destination, string purpose, float within = 12,
					Func<BotPosition, bool>? stopAt = null, Func<bool>? sourceComplete = null)
				{
					int map = session.Api.World.MapId ?? leg.Hub.MapId;
					// The ground nearest the point, as AB-02 found it: a spot on a slope may not snap where it stands.
					BotPosition goal = GroundRoadGoal(geometry, map, destination);
					BotTravelPlanner? planner = BotTravelPlanner.For(map, geometry, runtime.Data);
					// A campaign's small trigger can lie beyond a remembered death spot. The old road ignored
					// those hazards, then repeatedly asked to kill a blocker where no live monster remained.
					bool campaignZone = purpose == "campaign-zone";
					int approachRevives = combat.ReviveCount;
					bool avoidRememberedDeathSpots = true;
					IReadOnlyList<BotPosition> road = campaignZone
						? await NaturalCampaignZoneRoute.FindAsync(
							() => navigator.FindRouteAsync(session.CurrentPosition, goal, token), async () =>
							{
								session.TraceDiagnostic("campaign-zone-patrol-wait", new Dictionary<string, object?>
								{ ["position"] = session.CurrentPosition, ["goal"] = goal, ["milliseconds"] = NaturalPatrolPolicy.WaitMillis });
								return await WaitBeforePullDefendingAsync(NaturalPatrolPolicy.WaitMillis, purpose) &&
									combat.ReviveCount == approachRevives;
							}, token)
						: planner?.PlanJourney(map, session.CurrentPosition, goal, session.Api.World.Level, [])?.Route
							?? geometry.FindJourneyPath(map, session.CurrentPosition, goal);
					bool SafeSegment(IReadOnlyList<BotPosition> segment) => campaignZone
						? navigator.IsSegmentSafe(segment, null, goal, avoidRememberedDeathSpots) : navigator.IsSegmentSafe(segment, null);
					if (road.Count == 0)
					{
						// Distinguish a blocked patrol corridor from disconnected ground. Previously the zone
						// exhausted three identical attempts without a clock tick or ordinary guard recovery.
						session.TraceDiagnostic($"altgard-{altgardLegId}-no-road", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["goal"] = goal, ["outcome"] = BotNavMeshRouter.LastOutcome.ToString(),
							["position"] = session.CurrentPosition,
						});
						if (campaignZone && BotNavMeshRouter.LastOutcome == BotNavRouteOutcome.HazardRejected &&
							!session.Api.World.IsDead && combat.ReviveCount == approachRevives)
						{
							BotPosition beforeClear = session.CurrentPosition;
							int killsBefore = navigator.UnavailableObjects.Count;
							await TryClearObservedBlockerAsync(goal);
							if (session.Api.World.IsDead || combat.ReviveCount != approachRevives ||
								navigator.UnavailableObjects.Count > killsBefore || Distance(beforeClear, session.CurrentPosition) > 2)
								return false; // Re-observe after real combat/movement before changing the route preference.
							// A remembered death may close the only current hostile-free passage after its guards
							// were killed. Like the existing pull policy, keep it a preference when no other way works.
							road = navigator.FindCampaignMemoryPreferenceRoute(session.CurrentPosition, goal, token);
							avoidRememberedDeathSpots = false;
						}
						// NL-3: a flight that lands within reach ends the walk; Junjunbinerk stands where no navmesh lies within
						// 20 m, so the walk after the landing found no road again, and the two called each other without end.
						if (road.Count == 0)
						{
							BotPosition beforeFlight = session.CurrentPosition;
							if (flyAcross == null || !await flyAcross(destination)) return false;
							bool landed = Distance(session.CurrentPosition, destination) <= within;
							return landed || Distance(beforeFlight, session.CurrentPosition) < 2 ? landed
								: await WalkRoadDefendingAsync(destination, purpose, within, stopAt, sourceComplete);
						}
					}
					// Walk the road only as far as its first point where the caller wants to stop.
					if (stopAt != null && road.Select((point, index) => (point, index)).FirstOrDefault(entry => stopAt(entry.point)) is
						{ point: var stop, index: var cut } && stopAt(stop))
						road = road.Take(cut + 1).ToArray();
					for (int at = 0; at < road.Count; at += 16)
					{
						// A navigation fight can fulfill this source's objective before the road finishes.
						// Hand its real zero-HP evidence back rather than crossing more camp for another target.
						if (sourceComplete?.Invoke() == true) return true;
						BotPosition[] segment = road.Skip(at).Take(16).ToArray();
						if (campaignZone && navigator.IsSegmentStale(segment))
						{
							session.TraceDiagnostic("campaign-zone-replan-stale-road", new Dictionary<string, object?>
							{ ["position"] = session.CurrentPosition, ["firstPoint"] = segment[0] });
							return false;
						}
						int roadRevives = combat.ReviveCount;
						if (altgardLegId is "l10" or "l11" or "l12")
						{
							// BC-06: the first Heart descent succeeded, but the unchecked road walked
							// into a pack before defense ran. Clear observed blockers before crossing it.
							var progress = new NaturalApproachProgress();
							for (int clear = 0; Distance(session.CurrentPosition, segment[^1]) > 3 && !SafeSegment(segment); clear++)
							{
								BotPosition beforeClear = session.CurrentPosition;
								int killsBefore = navigator.UnavailableObjects.Count;
								if (!progress.CanRetry(clear) || !await TryClearObservedBlockerAsync(segment[^1])) return false;
								if (sourceComplete?.Invoke() == true) return true;
								if (combat.ReviveCount != roadRevives || session.Api.World.IsDead) { await RestSafelyAsync(token); return false; }
								progress.Observe(Distance(beforeClear, session.CurrentPosition), navigator.UnavailableObjects.Count > killsBefore);
								if (campaignZone && navigator.IsSegmentStale(segment)) return false;
							}
							// Clearing can walk a checked detour all the way to this segment's endpoint. The old
							// points behind us may still cross the guard's circle; do not walk or reject them again.
							if (Distance(session.CurrentPosition, segment[^1]) <= 3) continue;
						}
						await navigator.MoveAsync(segment, token);
						await session.SynchronizeAsync(token);
						if (sourceComplete?.Invoke() == true) return true;
						// Synchronize can defend and bind-revive before the explicit defense below. Its old
						// road no longer starts here; return to the caller so it plans from the new position.
						if (altgardLegId is "l9" or "l10" or "l11" or "l12" && combat.ReviveCount != roadRevives) return false;
						if (!await DefendAgainstEngagedAsync(purpose) || session.Api.World.IsDead)
						{
							await RestSafelyAsync(token);
							return false;
						}
						// A fight on the road leaves the Cleric short: rest before the next section, not at the end of a chain.
						if (session.Api.World.CurrentHp * 100 < session.Api.World.MaxHp * 80) await RestSafelyAsync(token);
					}
					bool reached = stopAt?.Invoke(session.CurrentPosition) ?? Distance(session.CurrentPosition, goal) <= within;
					session.TraceDiagnostic($"altgard-{altgardLegId}-road", new Dictionary<string, object?>
					{
						["purpose"] = purpose, ["points"] = road.Count, ["position"] = session.CurrentPosition,
						["miss"] = Distance(session.CurrentPosition, goal), ["reached"] = reached,
					});
					return reached;
				}

				// AB-08: one contract talk step: approach the NPC (flying for a flight step), talk, and follow a walking NPC.
				float TalkRange(int npcId) => runtime.Data.NpcDataDh.GetNpcTemplate(npcId)?.GetTalkDistance() ?? 3;

				async Task TravelDestinyMapAsync(int destination)
				{
					if (session.Api.World.MapId == destination) return;
					if (destination == leg.Hub.MapId)
					{
						// ND-02 proved Doman's ordinary fallback when the learned Return is cooling down.
						if (session.Api.World.MapId != 120010000 || session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(5))
						{
							await UseLearnedReturnToBindAsync();
							EnterLegMap(newEntry: true);
							return;
						}
					}
					else if (session.Api.World.MapId is 220010000 or 320070000)
					{
						await UseLearnedReturnToBindAsync();
						EnterLegMap(newEntry: true);
					}
					NaturalAltgardMapTrip trip = leg.MapTripList.Single(entry => entry.MapId == destination);
					if (trip.FromMapId is int source && session.Api.World.MapId != source) await TravelDestinyMapAsync(source);
					if (trip.FromMapId is int required && session.Api.World.MapId != required) return; // Re-plan after a road death.
					int revives = combat.ReviveCount;
					int teleporter = await ApproachShippedSpawnAsync(trip.TeleporterNpcId, withinRange: trip.TalkRange);
					if (combat.ReviveCount != revives) return;
					NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(teleporter,
						session.Api.World.Objects[teleporter].Position, trip.TalkRange, trip.LocationId, trip.Fare, trip.MapId, token);
					Require.True(result.IsDone, result.Reason);
					EnterLegMap(newEntry: true);
				}

				async Task<bool> FlyDestinyHubAsync(int transporterId)
				{
					if (session.Api.World.VisibleEffects?.FirstOrDefault(effect => effect.SkillId == 8291) is { } sickness)
					{
						Require.True(sickness.RemainingMillis > 0, "Soul Sickness has no observed expiry for the hub flight wait.");
						long until = runtime.NowMillis + sickness.RemainingMillis + 1000L;
						while (NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291) && runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) { await RestSafelyAsync(token); return false; }
						}
						Require.True(!NaturalAltgardQuestSteps.HasEffect(session.Api.World, 8291), "Soul Sickness did not expire before the hub flight.");
					}
					NaturalAirlineRoute route = airlines.Single(entry => entry.MapId == 220010000 && entry.NpcId == transporterId);
					int revives = combat.ReviveCount;
					if (Distance(session.CurrentPosition, route.Departure) > 40 &&
						!await WalkRoadDefendingAsync(route.Departure, "destiny-airline-road", within: 30)) return false;
					int transporter = await ApproachShippedSpawnAsync(transporterId, withinRange: 6);
					if (combat.ReviveCount != revives || session.Api.World.MapId != route.MapId) return false;
					NaturalNavigationResult pad = await NaturalIshalgenNavigator.ExploreAnchorAsync(route.MapId, -1,
						route.Departure, navigator, "destiny-flight-pad", token);
					Require.True(pad.Arrived, pad.Reason);
					NaturalServiceOutcome result = await new NaturalServiceSteps(session).FlyAsync(transporter,
						session.Api.World.Objects[transporter].Position, 6, route, token);
					Require.True(result.IsDone, result.Reason);
					session.TraceDiagnostic("destiny-hub-flight", new Dictionary<string, object?>
					{
						["npc"] = transporterId, ["route"] = route.Route, ["position"] = session.CurrentPosition,
					});
					return true;
				}

				async Task<int> ApproachDestinyNpcAsync(NaturalAltgardStep step)
				{
					int map = leg.StepMap(step), revives = combat.ReviveCount;
					BotPosition at = new(step.Position[0], step.Position[1], step.Position[2], 0);
					Require.Equal(map, session.Api.World.MapId);
					if (map == 220010000)
					{
						NaturalAirlineRoute alder = airlines.Single(route => route.MapId == map && route.NpcId == 203513);
						// The prison-to-Urd ground graph is 1,947 m: force the proved Anturoon/Aldelle flight.
						if (step.NpcId == 790003 && Distance(session.CurrentPosition, at) > 200)
						{
							if (!await FlyDestinyHubAsync(203545)) return 0;
						}
						else if (step.NpcId is 203546 or 203550 && Distance(session.CurrentPosition, alder.Departure) < 200)
						{
							if (!await FlyDestinyHubAsync(203513)) return 0;
						}
					}
					if (Distance(session.CurrentPosition, at) > 40 &&
						!await WalkRoadDefendingAsync(at, $"destiny-road-{step.Key}", within: 30)) return 0;
					if (combat.ReviveCount != revives || session.Api.World.MapId != map) return 0;
					NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(map, step.NpcId,
						at, step.TalkRange, navigator, "destiny-recipient", token);
					Require.True(reached.Arrived, $"{step.Key}: {reached.Reason}");
					return reached.TargetObjectId ?? await session.WaitForNpcAsync(step.NpcId, token);
				}

				async Task RecoverDestinyAsync(string reason)
				{
					NaturalAltgardDestiny destiny = leg.Destiny!;
					session.TraceDiagnostic("destiny-attempt-lost", new Dictionary<string, object?>
					{
						["reason"] = reason, ["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId),
						["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition, ["deaths"] = combat.ReviveCount,
					});
					if (session.Api.World.MapId == destiny.MapId) await TravelDestinyMapAsync(leg.Hub.MapId);
					await session.SynchronizeAsync(token);
					Require.Equal((byte)3, NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Status);
					// Ordinary defense while Return is interrupted can earn real kill credit. Preserve that progress.
					Require.True(NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Var is var observed &&
						(observed == destiny.ResetVar || observed == destiny.KillVar), "Lost attempt neither reset nor earned kill credit.");
					Require.Equal(0L, ItemCount(session.Api.World, destiny.StoneItemId));
					Require.True(!session.Api.World.Skills.ContainsKey(destiny.StigmaSkillId), "Lost attempt retained the tutorial skill.");
					destinySpawnedAt = null;
				}

				async Task FightDestinyAsync()
				{
					NaturalAltgardDestiny destiny = leg.Destiny!;
					int? enemy = session.Api.World.Objects.Values.FirstOrDefault(npc => npc.TemplateId == destiny.EnemyNpcId && !npc.IsCorpse)?.ObjectId;
					if (enemy == null || destinySpawnedAt is long spawn && runtime.NowMillis - spawn >= destiny.LifetimeSeconds * 1000L)
					{
						await RecoverDestinyAsync("five-minute-window-missed");
						return;
					}
					long started = runtime.NowMillis;
					int revives = combat.ReviveCount, retreats = combat.CompletedRetreats;
					int fightPackets = session.PacketHistory.Count;
					bool killed = await combat.TryKillAsync(enemy.Value, token, session.CurrentPosition,
						Math.Max(0, session.PacketHistory.Count - 400));
					session.TraceDiagnostic("destiny-combat-outcome", new Dictionary<string, object?>
					{
						["killed"] = killed, ["deaths"] = combat.ReviveCount - revives, ["retreats"] = combat.CompletedRetreats - retreats,
						["durationMillis"] = runtime.NowMillis - started, ["target"] = enemy.Value,
					});
					if (NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId) is (3, 9))
					{
						// NR-R19a: the kill's teleport can come while the fight still waits out its last blow's hit time. The spawn is
						// then in the packets the fight has read, and only what has not come yet is waited for.
						int spawnAt = session.PacketHistory.Skip(fightPackets).Select((packet, offset) => (packet, offset))
							.Where(seen => seen.packet.PacketType == typeof(SM_PLAYER_SPAWN) && seen.packet.Get<int>("worldId") == destiny.KillTeleport.MapId)
							.Select(seen => fightPackets + seen.offset).DefaultIfEmpty(-1).First();
						if (spawnAt < 0)
							await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == destiny.KillTeleport.MapId);
						if (spawnAt < 0 || !session.PacketHistory.Skip(spawnAt).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) &&
							packet.Get<int>("objectId") == session.CharacterId))
							await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
						session.AcceptTeleportPosition();
						await session.SynchronizeAsync(token);
						EnterLegMap(newEntry: true);
						Require.True(!session.Api.World.Objects.ContainsKey(enemy.Value) && !session.Api.World.LootStatuses.ContainsKey(enemy.Value), "Old Hellion survived the kill teleport.");
						Require.Equal(0L, ItemCount(session.Api.World, destiny.StoneItemId));
						Require.True(!session.Api.World.Skills.ContainsKey(destiny.StigmaSkillId), "Kill exit retained the tutorial skill.");
						destinySpawnedAt = null;
					}
					else if (session.Api.World.MapId == destiny.MapId) await RecoverDestinyAsync(killed ? "missing-kill-credit" : "retreat-or-expired-target");
				}

				async Task VisitLeg5CapitalAsync()
				{
					// AK-Q4 (a): the Pandaemonium cube expansions, with the Cleric's own kinah. Walk to the fortress teleporter,
					// travel, buy each level as Java CubeExpandService.expandCube offers it (EXTEND_INVENTORY, then the
					// STR_WAREHOUSE_EXPAND_WARNING question with the price). RC-04 batches the city quests,
					// Doman/Arekedil return and Basfelt flight; historical runs retain their learned Return.
					await EnsureOnGroundAsync();
					NaturalAltgardCubeExpansion cube = leg.CubeExpansion!;
					BotWorldModel world = session.Api.World;
					var steps = new NaturalServiceSteps(session);
					// The fortress is 1.1 km from Basfelt, past the navigator's segment budget (smoke run 5): take the travel
					// planner's road to the teleporter's square, then approach it.
					BotPosition teleporterSpawn = graph.GetMap(contract.MapId)!.Waypoints
						.First(waypoint => waypoint.TemplateId == cube.TeleporterNpcId).Position;
					if (!await FlyTowardAsync(teleporterSpawn))
						await WalkRoadDefendingAsync(teleporterSpawn, "cube-teleporter-road", within: 15);
					int teleporter = await ApproachShippedSpawnAsync(cube.TeleporterNpcId);
					NaturalServiceOutcome travelled = await steps.TeleportAsync(teleporter, world.Objects[teleporter].Position,
						cube.TeleporterTalkRange, cube.LocationId, cube.Fare, cube.MapId, token);
					Require.True(travelled.IsDone, travelled.Reason);
					NaturalJourneyNavigator city = mapNavigators.Enter(NaturalMapKey.Observe(world));
					NaturalNavigationResult reached = await NaturalIshalgenNavigator.ApproachNpcAsync(cube.MapId, cube.ExpanderNpcId,
						new BotPosition(cube.ExpanderPosition[0], cube.ExpanderPosition[1], cube.ExpanderPosition[2], 0), city, token);
					Require.True(reached.Arrived, $"Cube expander {cube.ExpanderNpcId}: {reached.Reason}");
					int expander = Require.IsType<int>(reached.TargetObjectId);
					for (int level = world.CubeExpansion?.Npc ?? 0; level < cube.Levels && world.Kinah >= cube.Prices[level];
						level = world.CubeExpansion?.Npc ?? 0)
					{
						long before = world.Kinah;
						await NaturalDialogProtocol.OpenAsync(session, expander, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == expander);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(expander,
							checked((ushort)DialogAction.EXTEND_INVENTORY)), token);
						DecodedBotServerPacket question = await session.WaitForPacketAsync(typeof(SM_QUESTION_WINDOW), token,
							packet => packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_WAREHOUSE_EXPAND_WARNING);
						await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1,
							question.Get<int>("senderId")), token);
						await session.WaitForPacketAsync(typeof(SM_CUBE_UPDATE), token);
						await session.SynchronizeAsync(token);
						await session.SendPacketAsync(session.Api.CloseDialog(expander), token);
						session.TraceDiagnostic("cube-expanded", new Dictionary<string, object?>
						{
							["expander"] = cube.ExpanderNpcId, ["npcExpansions"] = world.CubeExpansion?.Npc,
							["capacity"] = world.CubeExpansion?.Capacity, ["kinahBefore"] = before, ["kinahAfter"] = world.Kinah,
						});
						Require.True((world.CubeExpansion?.Npc ?? 0) == level + 1 && before - world.Kinah == cube.Prices[level],
							$"Cube expansion {level + 1} not observed (level {world.CubeExpansion?.Npc}, Kinah {before} -> {world.Kinah}).");
					}
					if (laterCapital != null && altgardLegId == "l5")
					{
						await NaturalLaterCapitalSteps.PrepareLeg5CityAsync(session, PlayLaterCapitalStepAsync,
							runtime.Data.ItemDataDh.GetItemTemplate(182207009), token);
						await PrepareLaterCapitalBookAsync();
						int doman = await ApproachCapitalNpcAsync(204191);
						NaturalServiceOutcome returned = await steps.TeleportAsync(doman, world.Objects[doman].Position,
							5, 9, 500, leg.Hub.MapId, token);
						Require.True(returned.IsDone, returned.Reason);
						EnterLegMap();
						await NaturalLaterCapitalSteps.CompleteMaternalReturnAsync(session, PlayLaterCapitalStepAsync);
						Require.True(await FlyTowardAsync(ground), "Leg 5 city preparation needs the fortress-to-Basfelt hub flight.");
					}
					else
					{
						await UseLearnedReturnToBindAsync();
						navigator = mapNavigators.Enter(NaturalMapKey.Observe(world));
					}
					Require.Equal(leg.Hub.MapId, world.MapId ?? 0);
				}

				void EnterLegMap(bool newEntry = false)
				{
					NaturalMapKey key = LegMapKey();
					if (!newEntry && mapNavigators.Current == key && contract.MapId == key.MapId) return;
					var defend = navigator.DefendOnAttackAsync;
					navigator = mapNavigators.Enter(key, newEntry);
					navigator.DefendOnAttackAsync = defend;
					navigator.AvoidHostileAggro = true;
					geometry = runtime.CreateGeometry();
					contract = contract with { MapId = key.MapId };
					zones = FlyZonesOf(key.MapId);
					combat.EnterMap(navigator, geometry, key.MapId);
					navigator.AvoidSpots = combat.DeathSpots;
				}

				NaturalMapKey LegMapKey() => leg.Haramel?.MapId == session.Api.World.MapId
					? new(session.Api.World.MapId!.Value, (session.Api.World.InstanceId ?? throw new InvalidDataException("No actual Haramel copy observed.")) - 1)
					: NaturalMapKey.Observe(session.Api.World);
				void SaveHaramelProgress()
				{
					RecordHaramelDeadHints();
					haramelProgress = haramelProgress! with { Revives = combat.ReviveCount, StallBudget = progress.State, LastObservedAtMillis = HaramelNow() };
					haramelProgress.Write(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-progress.json"));
				}
				async Task EnterHaramelAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					BotPosition entrance = new(rules.PortalPosition[0], rules.PortalPosition[1], rules.PortalPosition[2], 0);
					await ReachPillarLevelAsync(entrance);
					while (!await WalkHaramelAsync(entrance, MathF.Min(5, TalkRange(rules.PortalNpcId)))) await RestSafelyAsync(token);
					int portal = await session.WaitForNpcAsync(rules.PortalNpcId, token);
					int start = session.PacketHistory.Count;
					haramelEntryHistoryStart = start;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, portal, null, token, reloadWorld: true), "Haramel entrance use interrupted.");
					await AcceptHaramelTransitionAsync(changedMap: true, start);
					await ObserveHaramelEntryAsync();
				}
				async Task AcceptHaramelTransitionAsync(bool changedMap, int start)
				{
					Type arrival = changedMap ? typeof(SM_PLAYER_SPAWN) : typeof(SM_CHANNEL_INFO);
					if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == arrival)) await session.WaitForPacketAsync(arrival, token);
					if (!session.PacketHistory.Skip(start).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) && packet.Get<int>("objectId") == session.CharacterId))
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					await session.SynchronizeAsync(token);
					EnterLegMap(newEntry: true);
				}
				async Task ObserveHaramelEntryAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					int anchor = await ApproachShippedSpawnAsync(rules.AnchorNpcId, withinRange: 5);
					BotInstanceEntry entry = session.Api.World.InstanceEntries[(session.CharacterId, rules.CooldownId)];
					NaturalHaramelVisit? previousVisit = haramelProgress!.CurrentVisit;
					bool fresh = previousVisit == null || previousVisit.AnchorObjectId != anchor ||
						previousVisit.EntriesUsed != entry.EntriesUsed || haramelProgress.NeedsInstanceObservation &&
						session.Api.World.Objects.Values.Any(n => n.TemplateId == 216897 && !n.IsCorpse);
					if (fresh) { haramelHints.Clear(); haramelKillHistoryStart = haramelEntryHistoryStart; }
					haramelProgress = haramelProgress.ObserveEntry(session.Api.World.InstanceId!.Value, anchor, entry, HaramelNow(),
						session.Api.World.CompletedQuestIds.Contains(28507), fresh);
					SaveHaramelProgress();
					session.TraceDiagnostic("haramel-entry-receipt", new Dictionary<string, object?> { ["visit"] = haramelProgress.CurrentVisit, ["fresh"] = fresh });
				}
				async Task LeaveHaramelAsync()
				{
					NaturalHaramel rules = leg.Haramel!;
					int exitId = haramelProgress!.CurrentVisit?.BossMovieObserved == true ? rules.BossExitNpcId : rules.EntryExitNpcId;
					int exit = await ApproachShippedSpawnAsync(exitId, withinRange: TalkRange(exitId));
					int start = session.PacketHistory.Count;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, exit, null, token, reloadWorld: true), "Haramel exit use interrupted.");
					await AcceptHaramelTransitionAsync(changedMap: true, start);
					haramelProgress = haramelProgress.ObserveExit(HaramelNow(), rules);
					SaveHaramelProgress();
				}
				async Task ObserveHaramelBossAsync()
				{
					if (session.Api.World.MapId != leg.Haramel!.MapId) return;
					await NaturalMovieGate.FinishAsync(session, token);
					bool movie = session.PacketHistory.Skip(haramelKillHistoryStart).Any(packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == leg.Haramel.BossMovieId) &&
						session.PacketHistory.Skip(haramelKillHistoryStart).Any(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
							packet.Get<int>("npcId") == leg.Haramel.BossExitNpcId);
					haramelProgress = haramelProgress!.ObserveBoss(movie, false, HaramelNow());
					SaveHaramelProgress();
				}
				async Task LootHaramelChestAsync()
				{
					int chest = await ApproachShippedSpawnAsync(leg.Haramel!.ChestNpcId, withinRange: TalkRange(leg.Haramel.ChestNpcId));
					int chestStart = session.PacketHistory.Count;
					Require.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, chest, null, token), "Haramel class chest use interrupted.");
					await session.SynchronizeAsync(token);
					Require.True(session.PacketHistory.Skip(chestStart).Any(packet => packet.PacketType == typeof(SM_LOOT_ITEMLIST) &&
						packet.Get<int>("targetObjectId") == chest), "Haramel class chest did not produce its actual loot list.");
					BotLootItem[] offered = session.Api.World.Loot?.Items.ToArray() ?? [];
					foreach (BotLootItem item in offered)
					{
						long before = ItemCount(session.Api.World, item.ItemId);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(450), token);
						await session.SendPacketAsync(session.Api.Loot(chest, item.Index), token);
						await session.SynchronizeAsync(token);
						session.TraceDiagnostic("haramel-class-chest-loot", new Dictionary<string, object?> { ["item"] = item, ["before"] = before, ["after"] = ItemCount(session.Api.World, item.ItemId) });
					}
					await session.SendPacketAsync(session.Api.Loot(chest, close: true), token);
					haramelProgress = haramelProgress!.ObserveBoss(true, true, HaramelNow());
					SaveHaramelProgress();
				}
				async Task MakeHaramelSoupAsync()
				{
					int cauldron = await ApproachShippedSpawnAsync(730359, withinRange: 5);
					await NaturalDialogProtocol.OpenAsync(session, cauldron, token);
					if (haramelProgress!.SoupPayment == null)
					{
						long before = ItemCount(session.Api.World, 182212022);
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(cauldron, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 28511), token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == cauldron && packet.Get<ushort>("dialogPageId") == 1352);
						await session.SynchronizeAsync(token);
						haramelProgress = haramelProgress.ObserveSoupPayment(cauldron, before, ItemCount(session.Api.World, 182212022), 1352, HaramelNow());
						SaveHaramelProgress(); // Persist payment before the item-give action.
					}
					if (ItemCount(session.Api.World, 182212023) == 0 && QuestStatus(28511) == 3)
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(cauldron, DialogAction.SETPRO2, questId: 28511), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1L, ItemCount(session.Api.World, 182212023));
					Require.Equal(4, QuestStatus(28511));
					SaveHaramelProgress();
				}
				async Task CollectHaramelKeysAsync()
				{
					foreach (NaturalHaramelKey key in leg.Haramel!.TowerChestKeys!)
						while (ItemCount(session.Api.World, key.ItemId) < key.Count)
						{
							int npc = await KillShippedSpawnAsync(key.NpcId, new(QuestRunOperationKind.CollectQuestDrop, ItemId: key.ItemId, Count: key.Count));
							await TryLootCorpseItemAsync(session, npc, key.ItemId, token);
							await RestSafelyAsync(token);
						}
				}
				int HaramelKind(IReadOnlyList<int> kinds)
				{
					RecordHaramelDeadHints();
					NaturalNavigationObject? live = navigator.Observe().Npcs.Where(n => kinds.Contains(n.TemplateId))
						.OrderByDescending(n => Distance(session.CurrentPosition, n.Position) <= NaturalPullPlanner.SpellRange + 3 &&
							geometry.HasLineOfSight(leg.Haramel!.MapId, session.CurrentPosition, n.Position))
						.ThenByDescending(n => geometry.OnSameIsland(leg.Haramel!.MapId, session.CurrentPosition, n.Position))
						.ThenBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
					if (live != null) return live.TemplateId;
					int kind = graph.GetMap(leg.Haramel!.MapId)!.Waypoints.Where(w => w.TemplateId is int id && kinds.Contains(id) && !haramelHints.Contains((id, w.Position)))
						.OrderByDescending(w => geometry.OnSameIsland(leg.Haramel.MapId, session.CurrentPosition, w.Position))
						.ThenBy(w => w.TemplateId == leg.Haramel.BossNpcId).ThenBy(w => Distance(session.CurrentPosition, w.Position))
						.Select(w => w.TemplateId!.Value).FirstOrDefault();
					return kind > 0 ? kind : throw new NaturalHaramelSourcesExhaustedException("All original qualifying hints in this copy are exhausted.");
				}
				async Task<int> ApproachHaramelAsync(int templateId, float range, Func<int?>? completed)
				{
					NaturalHaramel rules = leg.Haramel!;
					for (int search = 0; search < 100; search++)
					{
						RecordHaramelDeadHints();
						if (completed?.Invoke() is int done) return done;
						if (session.Api.World.IsDead) await RestSafelyAsync(token);
						if (session.Api.World.MapId != rules.MapId) await EnterHaramelAsync();
						NaturalNavigationObject? live = navigator.Observe().Npcs.Where(n => n.TemplateId == templateId)
							.OrderByDescending(n => geometry.OnSameIsland(rules.MapId, session.CurrentPosition, n.Position)).ThenBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
						BotWaypoint? hint = graph.GetMap(rules.MapId)!.Waypoints.Where(w => w.TemplateId == templateId && !haramelHints.Contains((templateId, w.Position)))
							.OrderByDescending(w => geometry.OnSameIsland(rules.MapId, session.CurrentPosition, w.Position)).ThenBy(w => Distance(session.CurrentPosition, w.Position)).FirstOrDefault();
						// Handler-spawned chest/exit have no static waypoint. Their actual spawn packet remains
						// an observed location after the bot walks out of sight, scoped to this copy's entry.
						DecodedBotServerPacket? seen = hint == null ? session.PacketHistory.Skip(haramelKillHistoryStart).LastOrDefault(packet =>
							packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == templateId &&
							!navigator.UnavailableObjects.Contains(packet.Get<int>("objectId"))) : null;
						if (live == null && hint == null && seen == null) throw new NaturalHaramelSourcesExhaustedException($"Haramel source {templateId} is exhausted in this copy.");
						BotPosition at = live?.Position ?? hint?.Position ?? new(seen!.Get<float>("x"), seen.Get<float>("y"), seen.Get<float>("z"), seen.Get<byte>("heading"));
						session.TraceDiagnostic("haramel-source-search", new Dictionary<string, object?>
						{ ["template"] = templateId, ["search"] = search, ["object"] = live?.ObjectId, ["from"] = session.CurrentPosition, ["to"] = at, ["range"] = range });
						await ReachHaramelFloorAsync(at, templateId);
						if (session.Api.World.MapId != rules.MapId || session.Api.World.IsDead) continue;
						try { if (!await WalkHaramelAsync(at, range, live?.ObjectId)) continue; }
						catch (NaturalHaramelGroundApproachUnavailableException missing)
						{
							if (live != null) navigator.UnavailableObjects.Add(live.ObjectId);
							BotWaypoint? unavailable = graph.GetMap(rules.MapId)!.Waypoints.Where(w => w.TemplateId == templateId)
								.OrderBy(w => Distance(w.Position, at)).FirstOrDefault();
							if (unavailable != null) haramelHints.Add((templateId, unavailable.Position));
							session.TraceDiagnostic("haramel-source-unreachable", new Dictionary<string, object?>
							{ ["template"] = templateId, ["object"] = live?.ObjectId, ["position"] = at, ["reason"] = missing.Message });
							continue;
						}
						if (completed?.Invoke() is int collected) return collected;
						NaturalNavigationObject? arrived = navigator.Observe().Npcs.Where(n => n.TemplateId == templateId && Distance(session.CurrentPosition, n.Position) <= range + 1)
							.OrderBy(n => Distance(session.CurrentPosition, n.Position)).FirstOrDefault();
						if (arrived != null) return arrived.ObjectId;
						if (hint != null) haramelHints.Add((templateId, hint.Position));
					}
					throw new InvalidDataException($"Haramel source {templateId} exceeded its bounded observation search.");
				}
				async Task ReachHaramelFloorAsync(BotPosition at, int templateId)
				{
					int map = leg.Haramel!.MapId;
					BotPosition tower = new(231.031f, 223.091f, 139.82f, 0);
					bool TowerFloor(BotPosition p) => p.Z > 115 && p.X is > 195 and < 250 && p.Y is > 190 and < 250;
					bool onTower = TowerFloor(session.CurrentPosition), onOffice = session.CurrentPosition.Z > 120 && session.CurrentPosition.Y >= 260;
					// Some shipped workers stand above their floor's mesh snap (216913 is at 142.766).
					// Their audited room and elevation still require the same ordinary lift/elevator.
					bool targetTower = TowerFloor(at), targetOffice = at.Z > 120 && at.Y >= 260;
					async Task FinishTowerAscentAsync()
					{
						for (int retry = 0; retry < 100 && session.Api.World.MapId == map && !session.Api.World.IsDead; retry++)
							if (await WalkHaramelAsync(tower, 3)) return;
						Require.True(session.Api.World.MapId != map || session.Api.World.IsDead, "Haramel tower ascent exceeded its guarded route budget.");
					}
					if (onTower && targetTower)
					{
						if (session.CurrentPosition.Z < 134) await FinishTowerAscentAsync();
						return;
					}
					if (onOffice && targetOffice || geometry.NavMesh!.FindInteractionPath(map, session.CurrentPosition, at).Count > 0) return;
					if (onTower && session.CurrentPosition.Z < 134) await FinishTowerAscentAsync();
					if (onTower) await haramelTravel.GlideToLowerFloorAsync(token);
					else if (onOffice) await haramelTravel.RideOfficeElevatorDownAsync(token);
					if (session.Api.World.MapId != map) return;
					if (templateId == 700853 || targetTower)
					{
						int lift = await ApproachHaramelAsync(leg.Haramel.LiftNpcId, 5, null);
						int start = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, lift, token);
						session.Api.World.BeginWorldReload();
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(lift, checked((ushort)leg.Haramel.LiftDialog)), token);
						await AcceptHaramelTransitionAsync(changedMap: false, start);
						await FinishTowerAscentAsync();
					}
					else if (targetOffice) await haramelTravel.RideOfficeElevatorAsync(token);
				}
				async Task<bool> WalkHaramelAsync(BotPosition target, float range, int? objective = null)
				{
					int map = session.Api.World.MapId!.Value, revives = combat.ReviveCount;
					if (Distance(session.CurrentPosition, target) <= range && (range <= 5 || geometry.HasLineOfSight(map, session.CurrentPosition, target))) return true;
					BotPosition from = session.CurrentPosition;
					IReadOnlyList<BotPosition> route = range <= 1 ? geometry.NavMesh!.FindPath(map, from, target)
						: range <= 5 ? geometry.NavMesh!.FindInteractionPath(map, from, target) : [];
					if (route.Count > 0 && Distance(route[^1], target) > range) route = [];
					IEnumerable<BotPosition> InteractionGround()
					{
						BotNavMesh mesh = geometry.NavMesh!.NavMeshes.Get(map)!;
						// HM-02: a portal can block its own tiny island. The adjacent floor is an ordinary
						// dialog approach; it does not need a sight ray through the portal's placeable model.
						foreach (float radius in new[] { MathF.Max(1, range - 1), MathF.Max(1, range - 2), 1f })
							for (int sector = 0; sector < 16; sector++)
							{
								float angle = sector * MathF.PI / 8;
								BotPosition sample = target with { X = target.X + radius * MathF.Cos(angle), Y = target.Y + radius * MathF.Sin(angle) };
								if (mesh.Snap(sample, BotNavQuery.Default with { SnapHorizontal = 1, SnapVertical = 3 }) is BotPosition ground &&
									Distance(ground, target) <= range - .5f) yield return ground;
							}
					}
					// Ranged targets can also occupy a tiny isolated mesh island (the office cages).
					// Keep checked firing points on any reachable neighbouring floor, as for portal dialogs.
					BotPosition[] approaches = new[] { target }.Concat(InteractionGround()).Concat(range > 5 ?
						geometry.GroundAround(map, target, [MathF.Max(1, range - 1), MathF.Max(1, range - 2), 3f, 2f, 1f]) : [])
						.Where(p => Distance(p, target) <= range - MathF.Min(.5f, range / 2) && (range <= 5 || geometry.HasLineOfSight(map, p, target)))
						.OrderBy(p => Distance(from, p)).Distinct().ToArray();
					BotTravelPlanner? planner = BotTravelPlanner.For(map, geometry, runtime.Data);
					foreach (BotPosition point in approaches)
					{
						if (route.Count > 0) break;
						route = range > 1 ? planner?.PlanJourney(map, from, point, session.Api.World.Level, [])?.Route ??
							geometry.NavMesh!.FindPath(map, from, point) : geometry.NavMesh!.FindPath(map, from, point);
					}
					if (route.Count == 0 && approaches.Length > 0) route = geometry.FindJourneyPath(map, from, approaches[0]);
					if (route.Count == 0 && range > 5) throw new NaturalHaramelGroundApproachUnavailableException($"No checked firing approach {from} -> {target}, range {range}.");
					Require.True(route.Count > 0, $"No checked Haramel ground route {from} -> {target}, range {range}.");
					session.TraceDiagnostic("haramel-ground-route", new Dictionary<string, object?>
					{ ["map"] = map, ["from"] = from, ["target"] = target, ["range"] = range, ["points"] = route.Count, ["last"] = route[^1] });
					bool includeRememberedDeaths = true, recoveredGuardFreeRoute = false;
					for (int index = 0; index < route.Count; index += 8)
					{
						BotPosition[] segment = route.Skip(index).Take(8).ToArray();
						if (!navigator.IsSegmentSafe(segment, objective, route[^1], includeRememberedDeaths))
						{
							BotPosition before = session.CurrentPosition;
							int killsBefore = navigator.UnavailableObjects.Count;
							float Aggro(int id)
							{
								var npc = runtime.Data.NpcDataDh.GetNpcTemplate(id);
								return runtime.IsAggressive(npc) ? npc.GetAggroRange() + 1 : 0;
							}
							NaturalNavigationObject? guard = NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(before, segment,
								navigator.Observe().Npcs, Aggro, objective);
							bool cleared = guard != null && await PullAndKillAsync(guard.ObjectId, "haramel-route-guard");
							if (cleared) navigator.UnavailableObjects.Add(guard!.ObjectId);
							if (!cleared && navigator.UnavailableObjects.Count == killsBefore && Distance(before, session.CurrentPosition) < 2)
								cleared = await TryClearObservedBlockerAsync(segment[^1], objective);
							if (combat.ReviveCount != revives || session.Api.World.MapId != map) return false;
							if (!cleared && navigator.UnavailableObjects.Count == killsBefore && Distance(before, session.CurrentPosition) < 2 &&
								!recoveredGuardFreeRoute)
							{
								BotPosition goal = route[^1];
								IReadOnlyList<BotPosition> alternative = await navigator.FindRouteAsync(session.CurrentPosition, goal, token);
								if (combat.ReviveCount != revives || session.Api.World.MapId != map) return false;
								if (alternative.Count == 0)
								{
									alternative = navigator.FindRequiredGroundMemoryPreferenceRoute(session.CurrentPosition, goal, token,
										"haramel-ground", objective);
									includeRememberedDeaths = false;
								}
								if (alternative.Count > 0)
								{
									session.TraceDiagnostic("haramel-ground-replan-cleared-corridor", new Dictionary<string, object?>
										{ ["position"] = session.CurrentPosition, ["goal"] = goal, ["points"] = alternative.Count,
											["includeRememberedDeaths"] = includeRememberedDeaths });
									route = alternative; recoveredGuardFreeRoute = true;
									index = -8; continue; // One checked replan per road; subsequent live blockers still require combat/progress.
								}
								// A later guard may cover the final firing point while the immediate corridor is
								// already clear. Cross only this checked, live-safe prefix; each following segment
								// is checked again and its first reachable guard must still be pulled normally.
								if (navigator.IsSegmentSafe(segment, objective, goal, includeDeathSpots: false))
								{
									session.TraceDiagnostic("haramel-ground-memory-preference-prefix", new Dictionary<string, object?>
										{ ["position"] = session.CurrentPosition, ["goal"] = goal, ["end"] = segment[^1], ["points"] = segment.Length });
									includeRememberedDeaths = false; recoveredGuardFreeRoute = true;
									index -= 8; continue;
								}
							}
							Require.True(cleared || navigator.UnavailableObjects.Count > killsBefore || Distance(before, session.CurrentPosition) >= 2,
								"Haramel ground route remains guarded.");
							return false; // Fight/approach moved us: rebuild the route instead of walking stale points behind us.
						}
						if (Distance(session.CurrentPosition, segment[^1]) > .25f) await navigator.MoveAsync(segment, token);
						await session.SynchronizeAsync(token);
						await DefendAgainstEngagedAsync("haramel-ground");
						if (combat.ReviveCount != revives || session.Api.World.MapId != map) return false;
					}
					return Distance(session.CurrentPosition, target) <= range + 1;
				}
				void RecordHaramelDeadHints()
				{
					if (leg.Haramel == null || session.Api.World.MapId != leg.Haramel.MapId) return;
					var killed = session.PacketHistory.Skip(haramelKillHistoryStart).Where(packet => BotAttackStatus.CarriesHp(packet) &&
						packet.Get<byte>("hpOrMp") == 0)
						.Select(packet => packet.Get<int>("objectId")).ToHashSet();
					foreach (int deadObject in killed) navigator.UnavailableObjects.Add(deadObject);
					foreach (DecodedBotServerPacket spawn in session.PacketHistory.Skip(haramelKillHistoryStart).Where(packet =>
						packet.PacketType == typeof(SM_NPC_INFO) && (killed.Contains(packet.Get<int>("objectId")) ||
							session.Api.World.Objects.GetValueOrDefault(packet.Get<int>("objectId"))?.IsCorpse == true)))
					{
						int id = spawn.Get<int>("npcId");
						BotPosition at = new(spawn.Get<float>("x"), spawn.Get<float>("y"), spawn.Get<float>("z"), 0);
						BotWaypoint? hint = graph.GetMap(leg.Haramel.MapId)!.Waypoints.Where(w => w.TemplateId == id).OrderBy(w => Distance(w.Position, at)).FirstOrDefault();
						if (hint != null) haramelHints.Add((id, hint.Position));
					}
				}

				bool TripChainLeads(int from, int to)
				{
					try { FirstTripOfChain(from, to); return true; }
					catch (InvalidDataException) { return false; }
				}

				NaturalAltgardMapTrip FirstTripOfChain(int from, int to)
				{
					var reached = new Dictionary<int, NaturalAltgardMapTrip> { [from] = null! };
					var frontier = new Queue<int>([from]);
					while (frontier.TryDequeue(out int map))
						foreach (NaturalAltgardMapTrip onward in leg.MapTripList.Where(entry => (entry.FromMapId ?? leg.Hub.MapId) == map))
						{
							if (!reached.TryAdd(onward.MapId, map == from ? onward : reached[map])) continue;
							if (onward.MapId == to) return reached[to];
							frontier.Enqueue(onward.MapId);
						}
					throw new InvalidDataException($"No chain of the leg's map trips leads from map {from} to map {to}.");
				}

				// NL-4: false when the use was cut off (Java ItemUseObserver aborts the bar on an attack, a move or a cast and sends
				// SM_USE_OBJECT with action 0: the round nl4-vs-c1's Templar, hit at the water portal, waited for a teleport that never
				// came); the caller defends and the leg plans the use again.
				async Task<bool> UseInstancePortalAsync(int objectId, int duration, int destination, bool confirm = false)
				{
					int beforeUse = session.PacketHistory.Count;
					await NaturalDialogProtocol.OpenAsync(session, objectId, token);
					DecodedBotServerPacket started = await session.WaitForPacketAsync(typeof(SM_USE_OBJECT), token,
						packet => packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 1);
					Require.Equal(duration, started.Get<int>("durationMs"));
					bool Aborted() => session.PacketHistory.Skip(Math.Min(beforeUse, session.PacketHistory.Count)).Any(packet =>
						packet.PacketType == typeof(SM_USE_OBJECT) && packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 0);
					bool Cut()
					{
						session.TraceDiagnostic("quest-instance-portal-aborted", new Dictionary<string, object?>
						{
							["object"] = objectId, ["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
						});
						return false;
					}
					long barEnd = runtime.NowMillis + duration;
					while (runtime.NowMillis < barEnd - 100)
					{
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(250, barEnd - 100 - runtime.NowMillis)), token);
						await session.SynchronizeAsync(token);
						if (Aborted()) return Cut();
					}
					duration = (int)Math.Max(0, barEnd - runtime.NowMillis);
					if (confirm)
					{
						// Java PortalRequestAI.handleUseItemFinish: the question comes when the bar ends, and yes teleports.
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(duration + 1), token);
						await session.SynchronizeAsync(token);
						if (Aborted()) return Cut();
						static bool Asks(DecodedBotServerPacket packet) => packet.PacketType == typeof(SM_QUESTION_WINDOW) &&
							packet.Get<int>("code") == SM_QUESTION_WINDOW.STR_TELEPORT_NEED_CONFIRM;
						DecodedBotServerPacket question = session.PacketHistory.Skip(Math.Min(beforeUse, session.PacketHistory.Count)).LastOrDefault(Asks)
							?? await session.WaitForPacketAsync(Asks, token);
						session.Api.World.BeginWorldReload();
						await session.SendPacketAsync(GameClientPackets.QuestionResponse(question.Get<int>("code"), 1, question.Get<int>("senderId")), token);
						duration = 0;
					}
					else
						session.Api.World.BeginWorldReload();
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(duration + 1), token);
					DecodedBotServerPacket? spawned = null;
					for (int wait = 0; spawned == null && wait < 20; wait++)
					{
						spawned = await session.WaitForPacketWithinAsync(typeof(SM_PLAYER_SPAWN), TimeSpan.FromSeconds(5), token,
							packet => packet.Get<int>("worldId") == destination);
						if (spawned == null)
						{
							if (Aborted()) return Cut();
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						}
					}
					if (spawned == null) return Cut();
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					await session.SynchronizeAsync(token);
					EnterLegMap(newEntry: true);
					session.TraceDiagnostic("quest-instance-portal", new Dictionary<string, object?>
					{
						["object"] = objectId, ["duration"] = duration, ["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition,
					});
					return true;
				}

				async Task PrepareRebirthAsync()
				{
					// NR-54f: the class's own self-rebirth. A class that has none goes in without one.
					if (combat.ClassProfile.SelfRebirthSkillId is not ushort skillId)
					{
						session.TraceDiagnostic("quest-instance-without-rebirth", new Dictionary<string, object?>
						{
							["class"] = combat.ClassProfile.Class.ToString(),
						});
						return;
					}
					if (NaturalAltgardQuestSteps.HasEffect(session.Api.World, skillId)) return;
					Require.True(session.Api.World.Skills.TryGetValue(skillId, out BotSkill? learned), "Bregirun needs the observed learned Hand of Reincarnation.");
					await RestSafelyAsync(token);
					while (session.Api.Timing.TimeUntilCast(skillId) is { } remaining && remaining > TimeSpan.Zero)
					{
						await DefendAgainstEngagedAsync("rebirth-reuse");
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(5000, remaining.TotalMilliseconds) + 1), token);
						await session.SynchronizeAsync(token);
					}
					await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
					await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World, session.CurrentPosition,
						skillId, checked((byte)learned!.Level), session.CharacterId)), token);
					DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync((predicate, waitToken) =>
						session.WaitForPacketAsync(packet => predicate(packet) || BotCastProtocol.IsStartRejection(packet), waitToken), session.CharacterId, skillId, token);
					Require.Equal(typeof(SM_CASTSPELL), started.PacketType);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
					DecodedBotServerPacket result = await BotCastProtocol.WaitForCompletionAsync(session.WaitForPacketAsync, session.CharacterId, skillId, token);
					Require.Equal(typeof(SM_CASTSPELL_RESULT), result.PacketType);
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
					await session.SynchronizeAsync(token);
					Require.True(NaturalAltgardQuestSteps.HasEffect(session.Api.World, skillId), "Hand of Reincarnation did not produce its observed buff.");
					session.TraceDiagnostic("quest-instance-rebirth-prepared", new Dictionary<string, object?> { ["skill"] = skillId, ["level"] = learned.Level });
				}

				async Task CollectLeg9ClothingAsync()
				{
					for (int attempt = 1; attempt <= 6; attempt++)
					{
						EnterLegMap();
						await NaturalLaterCapitalSteps.RunMatchingStepsAsync(session, [NaturalLaterCapitalSteps.RobeHeart], PlayLaterCapitalStepAsync);
						if (NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6) && ItemCount(session.Api.World, 182207007) == 1) break;
						Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 5 or 6), "Banatisai must precede the clothing visit.");
						session.BeginStep("rc-robe-clothing", "approach-distance-trigger-and-loot");
						int clothing = await ApproachShippedSpawnAsync(700211, withinRange: 2);
						await session.SynchronizeAsync(token);
						if (session.Api.World.IsDead) { await RestSafelyAsync(token); continue; }
						Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6), "The actual five-metre clothing trigger was not observed.");
						bool collected = await NaturalAltgardQuestSteps.UseObjectAsync(session, clothing, 182207007, token);
						session.TraceDiagnostic("later-capital-clothing-use", new Dictionary<string, object?>
						{ ["objectId"] = clothing, ["attempt"] = attempt, ["collected"] = collected });
						await RestSafelyAsync(token);
					}
					Require.True(NaturalAltgardQuestSteps.State(session.Api.World, 2916) is (3, 6) && ItemCount(session.Api.World, 182207007) == 1,
						"The normal clothing loot must remain for Deyla's later city hand-in.");
					BotInventoryItem carried = session.Api.World.Inventory.Values.Single(i => i.ItemId == 182207007);
					session.TraceDiagnostic("later-capital-clothing-carried", new Dictionary<string, object?>
					{ ["objectId"] = carried.ObjectId, ["item"] = carried.ItemId, ["count"] = carried.Count, ["status"] = 3, ["var"] = 6 });
				}

				async Task PlayContractStepAsync(NaturalAltgardStep step)
				{
					// Java Q2252 sets REWARD/1 for the Spirit and REWARD/2 for Drakie.
					// The historic contract carries group 0; observe group 1's page on the alternate native spawn.
					if (step.QuestId == 2252 && step.ExpectedStatus == "REWARD" &&
						NaturalAltgardQuestSteps.State(session.Api.World, 2252) is (4, 2))
					{
						step = step with { Pages = [1352, 6] };
						session.TraceDiagnostic("minushan-reward-group", new Dictionary<string, object?>
						{ ["quest"] = 2252, ["var"] = 2, ["group"] = 1, ["page"] = 6 });
					}
					if (leg.Haramel != null && step.NpcId is 730306 or 730307)
					{
						int source = await ApproachShippedSpawnAsync(step.NpcId, withinRange: step.TalkRange);
						await NaturalHaramelQuestSteps.UseLeadInObjectAsync(session, step, source, token);
						return;
					}
					if (leg.Destiny is { } destiny)
					{
						if (step.Key == destiny.SpawnStep)
						{
							await RestSafelyAsync(token);
							if (NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId) is not (3, 97)) return;
						}
						int recipient = await ApproachDestinyNpcAsync(step);
						if (recipient == 0 || session.Api.World.MapId != leg.StepMap(step) ||
							NaturalAltgardQuestSteps.State(session.Api.World, destiny.QuestId)?.Var != step.Var && step.ExpectedStatus == "START") return;
						string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, recipient, token);
						if (step.Key == destiny.SpawnStep) destinySpawnedAt = runtime.NowMillis;
						EnterLegMap(newEntry: step.Teleport != null);
						session.TraceDiagnostic($"altgard-{altgardLegId}-step", new Dictionary<string, object?> { ["step"] = step.Key, ["change"] = change });
						return;
					}
					int npc;
					int stepMap = leg.StepMap(step);
					Require.Equal(stepMap, session.Api.World.MapId);
					// NL-3: an NPC that keeps hours (Java temporary_spawn: Q2773's Choirinererk in Morheim, 21:00 to 05:00 game time)
					// is waited for at his spot, as a player comes back at night.
					if (step.Hours is [int opens, int closes] && session.Api.World.GameMinutesAt(session.Api.Timing.Now) is long nowMinutes &&
						!NaturalGameClock.Within(nowMinutes, opens, closes))
					{
						BotPosition spot = new(step.Position[0], step.Position[1], step.Position[2], 0);
						if (Distance(session.CurrentPosition, spot) > 40)
							await WalkRoadDefendingAsync(spot, $"hours-road-{step.Key}", within: 25);
						long waitUntil = runtime.NowMillis + (NaturalGameClock.MinutesUntilHour(nowMinutes, opens) + 2L) * NaturalGameClock.MillisPerGameMinute;
						session.TraceDiagnostic($"altgard-{altgardLegId}-hours-wait", new Dictionary<string, object?>
						{
							["step"] = step.Key, ["opens"] = opens, ["closes"] = closes, ["gameHour"] = NaturalGameClock.HourOf(nowMinutes),
							["realMinutes"] = (waitUntil - runtime.NowMillis) / 60000, ["position"] = session.CurrentPosition,
						});
						// The wait is excused as it goes: an hour of it ended four bots' rounds (nl3-rb-c1).
						while (runtime.NowMillis < waitUntil && !session.Api.World.IsDead)
						{
							await DefendAgainstEngagedAsync($"hours-wait-{step.Key}");
							await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
							await session.SynchronizeAsync(token);
							ExcuseBoundedWait();
						}
						if (session.Api.World.IsDead) return;
					}
					// NL-3: the Abyss campaign's errands in Pandaemonium are reached by the capital's own ways, its lifts and the
					// Coliseum, as the capital's talks are.
					if ((laterCapital != null || altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId)) &&
						stepMap == capitalContract.MapId)
						npc = await ApproachCapitalNpcAsync(step.NpcId);
					else if (stepMap != leg.Hub.MapId && leg.Haramel == null)
					{
						// M5: a step far across another map (Luimerk, 900 m east of Altgard Fortress, Q2319) is reached by the
						// travel planner's road, defending on the way. The explorer alone replanned round the monsters on the way
						// until its budget ran out, for all eight; Altgard's transporter toward him lands on Impetusium, 70 m above
						// him with no way down.
						BotPosition far = new(step.Position[0], step.Position[1], step.Position[2], 0);
						// NL-3: a pack that corners the bot on the way can leave it 290 m off its road (Choirinererk, 1.5 km from Morheim's
						// fortress, nl3-rb-d3); the road is rested and walked again from where the fight left it.
						for (int roadTry = 0; roadTry < 3 && altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) &&
							Distance(session.CurrentPosition, far) > (roadTry == 0 ? 80 : 25); roadTry++)
						{
							if (roadTry > 0) await RestSafelyAsync(token);
							if (await WalkRoadDefendingAsync(far, $"talk-road-{step.Key}", within: 12)) break;
							if (session.Api.World.MapId != stepMap) return;
						}
						NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(stepMap, step.NpcId,
							new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0), step.TalkRange, navigator, "NPC", token);
						// NL-3: the packs round Choirinererk shut every route that keeps clear of them (the Ranger 64 m from him and the
						// Spirit Master 271 m, nl3-rb-c2): the bot fights its way along the road, then looks again.
						if (!reached.Arrived && altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) &&
							!session.Api.World.IsDead && session.Api.World.MapId == stepMap)
						{
							await RestSafelyAsync(token);
							await WalkRoadDefendingAsync(far, $"talk-road-through-{step.Key}", within: step.TalkRange);
							reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(stepMap, step.NpcId,
								new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0), step.TalkRange, navigator, "NPC", token);
						}
						Require.True(reached.Arrived, $"{step.Key}: {reached.Reason}");
						// NL-3: an NPC that keeps hours may not stand at his spot yet (the game time unseen, or his window just
						// closing: the Chanter at Choirinererk's spot, nl3-rb-c1). The bot waits for him there, up to a game day.
						for (int wait = 0; reached.TargetObjectId == null && step.Hours != null && wait < 240 && !session.Api.World.IsDead; wait++)
						{
							await DefendAgainstEngagedAsync($"hours-wait-{step.Key}");
							await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
							await session.SynchronizeAsync(token);
							ExcuseBoundedWait();
							if (session.Api.World.Objects.Values.Any(known => known.TemplateId == step.NpcId && !known.IsCorpse))
								reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(stepMap, step.NpcId,
									new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0), step.TalkRange, navigator, "NPC", token);
						}
						npc = Require.IsType<int>(reached.TargetObjectId);
					}
					else if (step.Flight)
					{
						// M2: a giver indoors leaves the bot under a roof. The open ground beside the hub's obelisk is where it
						// takes off then.
						if (leg.AirKills == null && !NaturalFlightProtocol.Plan(geometry, leg.Hub.MapId, session.CurrentPosition, Rock(), cruise).IsUsable)
						{
							NaturalNavigationResult open = await NaturalIshalgenNavigator.ExploreAnchorAsync(leg.Hub.MapId, -1, ground, navigator,
								"flight-takeoff", token);
							Require.True(open.Arrived, open.Reason);
						}
						await FlyToAsync(Rock());
						npc = await session.WaitForNpcAsync(step.NpcId, token);
					}
					else
					{
						await EnsureOnGroundAsync();
						try { npc = await ApproachShippedSpawnAsync(step.NpcId); }
						catch (InvalidDataException exception)
						{
							// The navigator found no route from where the last fight ended (Q24112: off Sumarhon's height toward
							// Brodir). Take the travel planner's road to the NPC, as AB-02 walked it, and look again from there.
							session.TraceDiagnostic($"altgard-{altgardLegId}-talk-road", new Dictionary<string, object?>
							{
								["step"] = step.Key, ["reason"] = exception.Message, ["position"] = session.CurrentPosition,
							});
							BotPosition at = new(step.Position[0], step.Position[1], step.Position[2], 0);
							if (!await WalkRoadDefendingAsync(at, $"talk-road-{step.Key}") && !session.Api.World.IsDead &&
								// NL-3: across the air where the map lets the bot fly (Amundi stands at Siel's Eastern camp, an island
								// 1.2 km from Ganglot's, a siege spawn the shipped graph does not hold).
								!(flyAcross != null && !combat.InCombat && await flyAcross(at)))
							{
								// No road from this ground at all: Return to the hub's obelisk, as a player does (the bind policy,
								// AB-Q5), and walk from there.
								await UseLearnedReturnToBindAsync();
								await WalkRoadDefendingAsync(at, $"talk-road-{step.Key}-after-return");
							}
							// NL-3: an NPC the shipped graph does not hold (a siege spawn) is met where the client sees it.
							if (graph.GetMap(contract.MapId)?.Waypoints.Any(waypoint => waypoint.TemplateId == step.NpcId) != true)
							{
								NaturalNavigationResult met = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, step.NpcId,
									at, step.TalkRange, navigator, "NPC", token);
								Require.True(met.Arrived, $"{step.Key}: {met.Reason}");
								npc = Require.IsType<int>(met.TargetObjectId);
							}
							else npc = await ApproachShippedSpawnAsync(step.NpcId);
						}
					}
					int fightsWaitedOut = 0;
					for (int attempt = 1; ; attempt++)
					{
						try
						{
							int beforeTalk = session.PacketHistory.Count;
							string change = await NaturalAltgardQuestSteps.TalkAsync(session, step, npc, token);
							session.TraceDiagnostic($"altgard-{altgardLegId}-step", new Dictionary<string, object?> { ["step"] = step.Key, ["change"] = change });
							// NL-3: the talk started a scripted quest flight; the client flies its path and lands, as Hagen's flight is
							// flown on the bridge. The start can come back inside the talk itself.
							if (step.FlyPathEnd is { Length: 3 } pathEnd && step.FlightSeconds is int pathSeconds)
							{
								static bool Started(DecodedBotServerPacket packet, int self) => packet.PacketType == typeof(SM_EMOTION) &&
									packet.Get<int>("senderObjectId") == self && packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT;
								if (!session.PacketHistory.Skip(Math.Min(beforeTalk, session.PacketHistory.Count)).Any(packet => Started(packet, session.CharacterId)))
									await session.WaitForPacketAsync(packet => Started(packet, session.CharacterId), token);
								await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(session.CurrentPosition,
									new BotPosition(pathEnd[0], pathEnd[1], pathEnd[2], 0), contract.MapId, TimeSpan.FromSeconds(pathSeconds)), token);
								await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
								await session.SynchronizeAsync(token);
								session.TraceDiagnostic($"altgard-{altgardLegId}-quest-flight", new Dictionary<string, object?>
								{
									["step"] = step.Key, ["landing"] = session.CurrentPosition, ["seconds"] = pathSeconds,
								});
							}
							return;
						}
						catch (NaturalDialogTooFarException) when (attempt < 3 && !step.Flight) { await ReapproachForDialogAsync(npc); }
						// NL-3: Q24042's hand-in at Lakadi after the escort: the server answered the reward page and took no notice of
						// the selection that ends it (no packet at all). The talk is played once more.
						// The second run lost the reward page request the same way: a page awaited for the real-time limit.
						catch (Exception unmoved) when (attempt < 3 && step.ExpectedStatus == "REWARD" &&
							(unmoved is InvalidDataException && unmoved.Message.Contains("did not move", StringComparison.Ordinal) ||
							 unmoved is TimeoutException && unmoved.Message.Contains("SM_DIALOG_WINDOW", StringComparison.Ordinal)))
						{
							session.TraceDiagnostic("reward-talk-again", new Dictionary<string, object?>
							{
								["step"] = step.Key, ["reason"] = unmoved.Message, ["position"] = session.CurrentPosition,
							});
							await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
							await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
							await session.SynchronizeAsync(token);
							await ReapproachForDialogAsync(npc);
						}
						catch (NaturalDialogNpcFightingException fighting) when (fightsWaitedOut < 12)
						{
							// NR-R11b: the NPC is fighting a monster and takes no dialog request. Let the fight end, as a player stands by
							// while a guard finishes his monster: defend when the monster turns on the bot, let the clock run, ask again.
							fightsWaitedOut++;
							attempt--;
							session.TraceDiagnostic("talk-npc-fighting", new Dictionary<string, object?>
							{
								["step"] = step.Key, ["npc"] = fighting.Npc, ["opponent"] = fighting.Opponent, ["wait"] = fightsWaitedOut,
								["position"] = session.CurrentPosition,
							});
							if (!await DefendAgainstEngagedAsync($"talk-{step.Key}") || session.Api.World.IsDead) return;
							await session.AdvanceAsync(TimeSpan.FromSeconds(5), token);
							await session.SynchronizeAsync(token);
						}
					}
				}

				// AB-08: open an NPC's dialog for a quest and send actions, waiting for each page (the steps that move no var:
				// Q2230's expired check and new chance, Lamir's refill incense).
				async Task DialogAsync(int npcId, int questId, (string Action, int? Page)[] actions,
					(byte Status, int Var)? expectedState = null)
				{
					await EnsureOnGroundAsync();
					int npc = await ApproachShippedSpawnAsync(npcId);
					for (int attempt = 1; ; attempt++)
					{
						// A defensive kill on the approach can finish the summoned objective. Lamir only
						// refills incense at START/1; return to the journal decision before sending a stale choice.
						if (expectedState != null && NaturalAltgardQuestSteps.State(session.Api.World, questId) != expectedState)
						{
							session.TraceDiagnostic("altgard-obsolete-dialog", new Dictionary<string, object?>
							{
								["quest"] = questId, ["npcId"] = npcId, ["expected"] = expectedState.ToString(),
								["observed"] = NaturalAltgardQuestSteps.State(session.Api.World, questId)?.ToString(),
							});
							return;
						}
						try
						{
							await NaturalDialogProtocol.OpenAsync(session, npc, token);
							await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
							break;
						}
						catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(npc); }
					}
					foreach ((string action, int? page) in actions)
					{
						await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc,
							checked((ushort)NaturalAscensionContract.DialogActionId(action)), questId: questId), token);
						if (page is int expected)
							await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
								packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == expected);
					}
					await session.SynchronizeAsync(token);
					await session.SendPacketAsync(session.Api.CloseDialog(npc), token);
				}

				// AB-08: a timed quest (Q2288, Q2230) from its offer to its hand-in, on NaturalTimedQuestPolicy. The client sees each
				// timer's length in SM_QUEST_ACTION and the bot notes when it should end. A death ends the timer on the server (AB-Q6:
				// Q1044's and Q2042's hooks), so after a revive Q2230's timer counts as ended and Q2288's as gone.
				async Task RunTimedQuestAsync(int questId)
				{
					int preparedAttempt = -1;
					NaturalAltgardTimer timer = leg.TimerList.Single(entry => entry.QuestId == questId);
					NaturalAltgardQuest quest = leg.Quest(questId);
					NaturalAltgardHunt? hunt = leg.HuntList.FirstOrDefault(entry => entry.QuestId == questId);
					NaturalAltgardCollectedItem? item = leg.CollectionList.FirstOrDefault(entry => entry.QuestId == questId)?.Items.Single();
					NaturalAltgardStep offer = leg.StepsFor(questId).Single(step => step.ExpectedStatus == "OFFER");
					for (int round = 0; round < 300; round++)
					{
						await session.SynchronizeAsync(token);
						BotWorldModel world = session.Api.World;
						if (world.IsDead || world.CurrentHp <= 0)
						{
							await RestSafelyAsync(token);
							if (timedEnds.ContainsKey(questId)) timedEnds[questId] = timer.OnExpiry == "new-chance" ? runtime.NowMillis : long.MinValue;
							continue;
						}
						(byte Status, int Var)? state = NaturalAltgardQuestSteps.State(world, questId);
						bool completed = world.CompletedQuestIds.Contains(questId);
						string? status = completed ? "COMPLETE" : state switch { (3, _) => "START", (4, _) => "REWARD", null => null, _ => "OTHER" };
						int var = state?.Var ?? 0;
						if (status == null) timedEnds.Remove(questId); // abandoned: its timer is gone with it
						long? endsAt = timedEnds.TryGetValue(questId, out long ends) && ends != long.MinValue ? ends : null;
						int unitsLeft = hunt != null ? hunt.ToVar - Math.Max(var, hunt.FromVar) : item!.Count - (int)ItemCount(world, item.ItemId);
						double perKill = hunt != null ? 1 : 0.8;
						BotPosition giver = new(offer.Position[0], offer.Position[1], offer.Position[2], 0);
						bool ready = world.CurrentHp * 100 >= world.MaxHp * 90 && world.CurrentMp * 100 >= world.MaxMp * 80 && Engaged().Attackers.Length == 0;
						NaturalTimedChoice choice = NaturalTimedQuestPolicy.Decide(new NaturalTimedObservation(status, var, runtime.NowMillis, endsAt,
							Math.Max(unitsLeft, 0), perKill, 45, Distance(session.CurrentPosition, giver) / 6, timedAttempts.GetValueOrDefault(questId), ready), timer);
						session.TraceDiagnostic($"altgard-{altgardLegId}-timed", new Dictionary<string, object?>
						{
							["quest"] = questId, ["action"] = choice.Action, ["reason"] = choice.Reason, ["status"] = status, ["var"] = var,
							["slack"] = double.IsFinite(choice.SlackSeconds) ? choice.SlackSeconds : null, ["attempts"] = timedAttempts.GetValueOrDefault(questId),
							["unitsLeft"] = unitsLeft,
						});
						void TimerStarted()
						{
							timedAttempts[questId] = timedAttempts.GetValueOrDefault(questId) + 1;
							int seconds = session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) && quest.TimerSeconds is int seen && seen > 0
								? seen : timer.Seconds;
							timedEnds[questId] = runtime.NowMillis + seconds * 1000L;
						}
						switch (choice.Action)
						{
							case "done":
								return;
							case "give-up":
								await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-timed-{questId}-given-up.json"),
									System.Text.Json.JsonSerializer.Serialize(choice), token);
								throw new InvalidDataException($"Altgard leg {altgardLegId}: Q{questId}'s timer ran out {timedAttempts.GetValueOrDefault(questId)} times (AB-Q2).");
							case "take":
								if (questId == 2263 && preparedAttempt != timedAttempts.GetValueOrDefault(questId))
								{
									// AE-06: clear companions around the three nearest local pollen spots before accepting. Keep the
									// malodors for the collection; all clearing and travel use ordinary movement and combat.
									NaturalAltgardArea area = leg.Area(quest.Area!);
									await FlyTowardAsync(giver); // Q2253 leaves the Cleric at the Berth; return by its hub transporter.
									BotPosition[] spots = graph.GetMap(leg.Hub.MapId)!.Waypoints
										.Where(at => at.TemplateId is int id && item!.SourceNpcIds.Contains(id) && area.Contains(at.Position.X, at.Position.Y, at.Position.Z))
										.Select(at => at.Position).Distinct().OrderBy(at => Distance(at, giver)).Take(3).ToArray();
									int revives = combat.ReviveCount;
									foreach (BotPosition spot in spots)
									{
										if (Distance(session.CurrentPosition, spot) > 45)
											await WalkRoadDefendingAsync(spot, "pollen-preparation", stopAt: at => Distance(at, spot) <= 45);
										await ClearAroundSpotAsync(spot, null, "pollen-preparation", item!.SourceNpcIds);
										if (combat.ReviveCount != revives || session.Api.World.IsDead) break;
									}
									if (combat.ReviveCount != revives || session.Api.World.IsDead) continue;
									await ApproachShippedSpawnAsync(offer.NpcId);
									await RestSafelyAsync(token);
									preparedAttempt = timedAttempts.GetValueOrDefault(questId);
									continue; // re-observe HP/mana before acceptance starts the timer
								}
								await PlayContractStepAsync(offer);
								if (NaturalTimedQuestPolicy.TimerStartsAtAccept(timer)) TimerStarted();
								break;
							case "start-timer":
								await PlayContractStepAsync(leg.Steps.Single(step => step.Key == timer.StartStep));
								TimerStarted();
								break;
							case "hunt":
							{
								await EnsureOnGroundAsync();
								int[] kinds = hunt?.NpcIds ?? item!.SourceNpcIds;
								int target = await KillShippedSpawnAsync(NearestKind(kinds));
								if (item != null) await TryLootCorpseItemAsync(session, target, item.ItemId, token);
								navigator.UnavailableObjects.Add(target);
								break;
							}
							case "turn-in":
								await PlayContractStepAsync(leg.StepsFor(questId).Single(step => step.ExpectedStatus == "START" && step.Var == var &&
									step.Key != timer.StartStep));
								break;
							case "new-chance":
								await DialogAsync(quest.StartNpcId!.Value, questId,
									[("QUEST_SELECT", null), ("CHECK_USER_HAS_QUEST_ITEM", timer.ExpiredPage), (timer.NewChanceAction!, null)]);
								TimerStarted();
								break;
							case "wait-until-ready":
								await RestSafelyAsync(token);
								// The powder rest calls 65% mana recovered; the timer wants 80%. Let it come back, as a player waits.
								await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
								break;
							default: // wait-for-journal
								await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
								break;
						}
					}
					throw new InvalidDataException($"Q{questId}'s timed run exceeded 300 rounds.");
				}

				// AB-08: Q2223's Infernus. Without incense, Lamir gives another (page 1779). With it: rest to full, burn it at the Old
				// Incense Burner (movie 67), and fight Infernus inside his five minutes. Three tries (AB-Q2).
				// AG-07: Q2252's bones raise Minushan's Spirit or Drakie (no movie). AG-06 found the bones' peckus joining the Spirit
				// (48% HP and a retreat), so whatever walks around the trigger is cleared first, and the kill takes the monster whose
				// SM_NPC_INFO follows the use: AG-04 found a gone one can stay in view after a teleport.
				async Task RunSpawnKillAsync(NaturalAltgardSpawn spawn)
				{
					int tries = spawnAttempts.GetValueOrDefault(spawn.Key);
					if (tries >= NaturalTimedQuestPolicy.MaxAttempts)
						throw new InvalidDataException($"Altgard leg {altgardLegId}: {spawn.Key} failed {tries} times (AB-Q2).");
					if (ItemCount(session.Api.World, spawn.RequiresItemId) == 0)
					{
						await DialogAsync(spawn.RefillNpcId, spawn.QuestId, [("QUEST_SELECT", spawn.RefillPage)], (3, spawn.AtVar));
						return;
					}
					await RestSafelyAsync(token);
					await EnsureOnGroundAsync();
					int burner = await ApproachShippedSpawnAsync(spawn.TriggerNpcId, skipBlockedTarget: true);
					if (!session.Api.World.Objects.ContainsKey(burner))
					{
						await session.AdvanceAsync(TimeSpan.FromSeconds(30), token); // the burner respawns 295 s after a use
						return;
					}
					await ClearAroundObjectiveAsync(burner, $"spawn-{spawn.Key}");
					await RestSafelyAsync(token);
					burner = await ApproachShippedSpawnAsync(spawn.TriggerNpcId, skipBlockedTarget: true);
					spawnAttempts[spawn.Key] = tries + 1;
					int start = session.PacketHistory.Count;
					bool used = await NaturalAltgardQuestSteps.UseObjectAsync(session, burner, null, token);
					await NaturalMovieGate.FinishAsync(session, token);
					int[] raised = [spawn.NpcId, .. spawn.AlternateNpcIds ?? []];
					int? monster = null;
					for (int wait = 0; wait < 10 && monster == null; wait++)
					{
						monster = session.PacketHistory.Skip(Math.Min(start, session.PacketHistory.Count)).LastOrDefault(packet =>
							packet.PacketType == typeof(SM_NPC_INFO) && raised.Contains(packet.Get<int>("npcId")))?.Get<int>("objectId");
						if (monster != null) break;
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
						await session.SynchronizeAsync(token);
					}
					bool killed = monster is int raisedId && await combat.TryKillAsync(raisedId, token, session.CurrentPosition);
					await session.SynchronizeAsync(token);
					session.TraceDiagnostic($"altgard-{altgardLegId}-spawn-kill", new Dictionary<string, object?>
					{
						["spawn"] = spawn.Key, ["try"] = tries + 1, ["used"] = used, ["monster"] = monster,
						["template"] = monster is int seen ? session.Api.World.Objects.GetValueOrDefault(seen)?.TemplateId : null, ["killed"] = killed,
						["quest"] = NaturalAltgardQuestSteps.State(session.Api.World, spawn.QuestId)?.ToString(),
					});
					await RestSafelyAsync(token);
				}

				// AC-06: the escort (docs/natural-altgard-leveling.md, "The escort handler"). NaturalEscortProtocol runs it; this
				// gives it the walk, the fight, and the clear: before each start the clear areas are cleared (AC-Q3), the dock
				// end first (121 m from the follower, out of sight from him), then back along the line. A death ends the
				// protocol run; the next decision revives and the escort resumes with the attempts and ended followers kept.
				async Task RunEscortAsync(NaturalAltgardEscort escort)
				{
					int map = leg.Hub.MapId;
					NaturalAltgardStep restart = leg.Steps.Single(step => step.Key == escort.RestartStep);
					BotPosition followerStart = geometry.SnapToGround(map, new BotPosition(restart.Position[0], restart.Position[1], restart.Position[2] + 1, 0))
						?? throw new InvalidDataException($"No ground at the {escort.Key} follower.");
					BotPosition standPoint = NaturalEscortPolicy.GoalStand(escort.Goal, followerStart, escort.GoalRadius);
					BotPosition stand = geometry.SnapToGround(map, standPoint with { Z = standPoint.Z + 1 })
						?? throw new InvalidDataException($"No ground at the {escort.Key} goal stand.");
					IReadOnlyList<BotPosition> escortRoute = geometry.FindJourneyPath(map, followerStart, stand);
					Require.True(escortRoute.Count > 0, $"No route for the {escort.Key} escort.");
					bool InClearArea(BotPosition at) => escort.ClearAreas.Any(key => leg.Area(key).Contains(at.X, at.Y, at.Z));
					Func<bool>? followerOffHours = escort.FollowerSpawnHour is int opens && escort.FollowerDespawnHour is int closes
						? () => session.Api.World.GameMinutesAt(session.Api.Timing.Now) is long minutes &&
							!NaturalGameClock.Within(minutes, opens, closes)
						: null;
					var protocol = new NaturalEscortProtocol(session, leg, escort, () => runtime.NowMillis)
					{
						WalkToAsync = async (to, walkToken) =>
						{
							BotPosition beforeWalk = session.CurrentPosition;
							IReadOnlyList<BotPosition> path = geometry.FindJourneyPath(map, session.CurrentPosition, to);
							// NL-3: a follower on another island (Q24042's prisoners in Sulfur Fortress, two islands from Primum) is
							// flown to before the follow starts; once he follows, the bot only walks.
							if (path.Count == 0 && flyAcross != null && Distance(beforeWalk, to) > 30 &&
								NaturalAltgardQuestSteps.State(session.Api.World, escort.QuestId)?.Var != escort.FollowVar)
								await flyAcross(to);
							else
								await navigator.MoveAsync(path.Count > 0 ? path : [to], walkToken);
							await session.SynchronizeAsync(walkToken);
							// A walk that moved nothing still takes game time: the escort ticks again at once otherwise, and the SIM
							// clock stood still at 2 s while the trace grew.
							if (Distance(beforeWalk, session.CurrentPosition) < 0.5f) await session.AdvanceAsync(TimeSpan.FromSeconds(1), walkToken);
						},
						Route = () => escortRoute,
						FightAsync = async _ => await DefendAgainstEngagedAsync($"escort-{escort.Key}"),
						RetreatAsync = async _ => await DefendAgainstEngagedAsync($"escort-{escort.Key}-retreat"),
						Threat = () => (Engaged().Attackers.Length, false),
						ClearAreasHaveAggressors = () => escortClearedUntil is not long until || until <= runtime.NowMillis ||
							ObservedPullMonsters().Any(monster => InClearArea(monster.Npc.Position)),
						ClearAsync = async _ =>
						{
							long? firstKill = null;
							int kills = 0;
							foreach (int anchorNpc in new[] { escort.GoalNpcId, escort.FollowerNpcId }.Where(id => id != 0))
							{
								// NR-R8d: a follower that keeps hours (Java temporary_spawn; Q2284's, 04:00-21:00) can be gone when the
								// clearing comes back to him: the Bard's clearing ran into the night after another bot's escort had cost it
								// the start of his day. He is then not there to walk to, and that walk ended the leg. Stop clearing: the
								// protocol's next tick ends the escort as off-hours and the leg waits for his window.
								if (anchorNpc == escort.FollowerNpcId && followerOffHours?.Invoke() == true) break;
								try { await ApproachShippedSpawnAsync(anchorNpc, withinRange: 15); }
								catch (InvalidDataException gone) when (anchorNpc == escort.FollowerNpcId && followerOffHours?.Invoke() == true)
								{
									session.TraceDiagnostic("escort-follower-hours-ended", new Dictionary<string, object?>
									{
										["escort"] = escort.Key, ["follower"] = escort.FollowerNpcId, ["reason"] = gone.Message,
										["position"] = session.CurrentPosition,
									});
									break;
								}
								for (int round = 0; round < 16; round++)
								{
									if (!await DefendAgainstEngagedAsync($"escort-{escort.Key}-clear") || session.Api.World.IsDead) return null;
									NaturalPullMonster? robber = ObservedPullMonsters().Where(monster => InClearArea(monster.Npc.Position))
										.OrderBy(monster => Distance(session.CurrentPosition, monster.Npc.Position)).FirstOrDefault();
									if (robber == null) break;
									if (await PullAndKillAsync(robber.Npc.ObjectId, $"escort-{escort.Key}-clear"))
									{
										// RC-11: a looted corpse can remain in view; clear a live source only once.
										navigator.UnavailableObjects.Add(robber.Npc.ObjectId);
										session.TraceDiagnostic("escort-clear-retire-killed-source", new Dictionary<string, object?>
										{
											["escort"] = escort.Key, ["objectId"] = robber.Npc.ObjectId, ["npcId"] = robber.Npc.TemplateId,
										});
										firstKill ??= runtime.NowMillis;
										kills++;
									}
									else if (session.Api.World.IsDead) return null;
									else navigator.UnavailableObjects.Add(robber.Npc.ObjectId);
								}
							}
							escortClearedUntil = (firstKill ?? runtime.NowMillis) + EscortClearRespawnMillis;
							session.TraceDiagnostic($"altgard-{altgardLegId}-escort-clear", new Dictionary<string, object?>
							{
								["escort"] = escort.Key, ["kills"] = kills, ["firstRespawn"] = firstKill is long kill ? kill + EscortClearRespawnMillis : null,
							});
							return firstKill is long first ? first + EscortClearRespawnMillis : null;
						},
						PriorAttempts = escortAttempts,
						EndedFollowerObjectIds = escortEndedFollowers,
						FollowerGoneAtMillis = escortFollowerGoneAt,
						OffHours = followerOffHours,
					};
					NaturalEscortResult result = await protocol.RunAsync(token);
					escortAttempts = result.Attempts;
					escortEndedFollowers.UnionWith(result.EndedFollowers ?? []);
					escortFollowerGoneAt = result.FollowerGoneAtMillis;
					session.TraceDiagnostic($"altgard-{altgardLegId}-escort", new Dictionary<string, object?>
					{
						["escort"] = escort.Key, ["outcome"] = result.Outcome, ["attempts"] = result.Attempts, ["movie"] = result.MovieSeen,
						["followerSpeed"] = result.FollowerSpeed,
						["log"] = result.Log.Select(attempt => $"#{attempt.Number} {attempt.Outcome} {(attempt.EndMillis - attempt.StartMillis) / 1000.0:F0}s " +
							$"gap {attempt.LongestGap:F1} hops {attempt.Hops} {attempt.LossReason}").ToArray(),
					});
					if (result.Outcome == "give-up")
					{
						// AC-Q2: three failed attempts end the leg without the escort (and Q2222, which follows it): a finding to fix.
						await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-escort-given-up.json"),
							System.Text.Json.JsonSerializer.Serialize(result), token);
						throw new InvalidDataException($"Altgard leg {altgardLegId}: the {escort.Key} escort failed {result.Attempts} times (AC-Q2).");
					}
				}
			}

			// AF-08: the Leg 1 endpoint. Assert it from the client's view, then quit, log back in and require that the
			// character survived as it was; altgard-l1-completion.json carries the clock for the `altgard-l12` snapshot.
			async Task CompleteAltgardLeg1Async(NaturalAltgardContract leg)
			{
				// NL-a: the companion is sent away before the endpoint's relog, which would end it anyway.
				if (options.SoloCompanion != null) await DismissSoloCompanionAsync(token);
				session.BeginStep("af-endpoint", "verify-and-relog-at-the-leg-1-endpoint");
				await TopUpHelpItemsAsync("checkpoint");
				BotWorldModel world = session.Api.World;
				// M4: what the bot left for the group step is not done here; the receipt names it.
				// NL-3: and what follows a quest left for the group (the Sorcerer left Q2753, and Q2754 comes after it).
				bool LeftForGroup(int quest) => deferredToGroup.Contains(quest) ||
					leg.Quests.FirstOrDefault(entry => entry.Id == quest)?.PrerequisiteList.Any(LeftForGroup) == true;
				int[] endpointQuests = [.. leg.Endpoint.CompletedQuestIds.Where(quest => !LeftForGroup(quest))];
				Require.All(endpointQuests, quest => Require.Contains(quest, world.CompletedQuestIds));
				foreach (int id in endpointQuests)
					foreach (var item in runtime.Data.Quests.GetQuestById(id).GetQuestWorkItems()?.GetQuestWorkItem() ?? [])
						Require.Equal(0L, ItemCount(world, item.GetItemId()));
				// AK-08 (AK-Q2): the held hand-ins are taken, not handed in, and their objectives are met.
				IReadOnlyDictionary<int, NaturalTemplateObjective> heldObjectives = NaturalTemplateObjective.From(altgardPlans);
				var heldItems = world.Inventory.Values.GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
				Require.All(leg.HeldList, held => Require.True(!world.CompletedQuestIds.Contains(held.QuestId) &&
					world.Quests.TryGetValue(held.QuestId, out BotQuestState? state) && state.Status is 3 or 4 &&
					heldObjectives[held.QuestId].IsDone(state, heldItems), $"Q{held.QuestId} is not held ready for {held.EndNpcId}."));
				// NL-3: a quest played as far as it goes is left taken at its var.
				Require.All(leg.Quests.Where(quest => quest.StopsAtVar != null), quest => Require.True(world.Quests.TryGetValue(quest.Id,
					out BotQuestState? stopped) && stopped.Status == 3 && leg.QuestVar(stopped) >= quest.StopsAtVar,
					$"Q{quest.Id} is not taken at var {quest.StopsAtVar}."));
				Require.Equal(leg.Endpoint.MapId, world.MapId!.Value);
				Require.True(world.Level >= leg.Endpoint.MinimumLevel, $"Endpoint level {world.Level} is below {leg.Endpoint.MinimumLevel}.");
				Require.True(!world.IsDead, "The endpoint character is dead.");
				VerifyDestinyEndpoint();
				VerifyCoinEndpoint();
				VerifyHaramelEndpoint();
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(world, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				if (leg.Haramel != null)
					await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "haramel-endpoint-relog.json"),
						System.Text.Json.JsonSerializer.Serialize(new { before, after }), token);
				// M3: the relog enters the world, and the server starts an invasion daily then (Java KillInWorld.onEnterWorldEvent:
				// Q49020, "[Daily] Aegir's Order", on entering Morheim on a day of its cycle). It was not in the journal before.
				HashSet<int> startedOnEntry = after.Quests.Where(quest => quest.Status == 3 && (quest.StepAndFlags & 0x3F) == 0 &&
					!before.Quests.Any(old => old.QuestId == quest.QuestId && old.Status is 3 or 4) &&
					runtime.Data.XmlQuests.GetQuest(quest.QuestId) is Aion.GameServer.QuestEngine.Handlers.Models.KillInWorldData invasion &&
					invasion.invasionWorld == after.MapId).Select(quest => quest.QuestId).ToHashSet();
				if (startedOnEntry.Count > 0)
					session.TraceDiagnostic("relog-started-on-entry", new Dictionary<string, object?> { ["quests"] = startedOnEntry.Order().ToArray() });
				// M4: Java grants a stigma's next rank only as the player logs in (StigmaService.onPlayerLogin; no level-up hook): the
				// relog after level 30 gave the Assassin Searching Strike II (3447), the rank its stone has from 30.
				var loginClass = PlayerClassExtensions.GetPlayerClassById(after.PlayerClass, true);
				var loginRace = Enum.Parse<Aion.GameServer.Model.Race>(leg.Start.Race);
				HashSet<int> stigmaOnLogin = after.Skills.Where(skill => loginClass is { } owner && !before.Skills.Any(old => old.SkillId == skill.SkillId) &&
					runtime.Data.SkillTreeDataDh.GetTemplatesForSkill(skill.SkillId, owner, loginRace).Any(template => template.IsStigma()))
					.Select(skill => (int)skill.SkillId).ToHashSet();
				if (stigmaOnLogin.Count > 0)
					session.TraceDiagnostic("relog-stigma-rank", new Dictionary<string, object?> { ["skills"] = stigmaOnLogin.Order().ToArray() });
				NaturalJourneyPersistence.Verify(before, after, startedOnEntry, stigmaOnLogin);
				VerifyDestinyEndpoint();
				if (laterCapital != null) await laterCapital.WriteCheckpointAsync(Path.GetDirectoryName(combatTracePath)!,
					altgardLegId!, before, after, token, distinctSegment: continuousAltgard, startedOnEntry, stigmaOnLogin);
				VerifyCoinEndpoint();
				VerifyHaramelEndpoint();
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, $"altgard-{altgardLegId}-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						before, after, verified = true, session.CharacterId, ElapsedMillis = runtime.NowMillis,
						Deaths = combat.ReviveCount, DeferredToGroup = deferredToGroup.Order().ToArray(),
					}), token);
				session.TraceDiagnostic($"altgard-{altgardLegId}-complete", new Dictionary<string, object?>
				{
					["level"] = after.Level, ["map"] = after.MapId, ["completed"] = endpointQuests,
					["deferredToGroup"] = deferredToGroup.Order().ToArray(),
					["deaths"] = combat.ReviveCount, ["kinah"] = session.Api.World.Kinah,
				});

				void VerifyDestinyEndpoint()
				{
					if (leg.Destiny is not { } destiny) return;
					BotWorldModel observed = session.Api.World;
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					IEnumerable<int> expected = leg.Start.CompletedQuestIds.Append(destiny.QuestId);
					if (laterCapital != null) expected = expected.Append(2938);
					Require.True(observed.CompletedQuestIds.SetEquals(expected), "Destiny must preserve the incoming journal and complete only its scheduled finishes.");
					Require.Equal(0L, ItemCount(observed, destiny.StoneItemId));
					Require.Equal(0L, ItemCount(observed, destiny.LegacyRewardId));
					Require.Equal(1L, ItemCount(observed, destiny.RewardBundleId));
					Require.True(!observed.Skills.ContainsKey(destiny.StigmaSkillId), "The tutorial stigma skill survived cleanup.");
					Require.All(destinyIncomingSkills, id => Require.Contains(id, observed.Skills.Keys));
					NaturalAltgardDecision endpoint = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now),
						new Dictionary<int, NaturalTemplateObjective>(), 1);
					Require.Equal("complete", endpoint.Outcome);
				}

				void VerifyCoinEndpoint()
				{
					if (leg.CoinGear is not { } gear) return;
					BotWorldModel observed = session.Api.World;
					Require.Equal(leg.Start.CompletedQuestIds.Length + 1, observed.CompletedQuestIds.Count);
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					Require.Equal(1, observed.CompletedQuestCounts.GetValueOrDefault(gear.QuestId));
					Require.Equal((long)gear.EndpointCoins, ItemCount(observed, gear.CoinItemId));
					Require.Equal(0L, ItemCount(observed, 186000007));
					Require.Equal(1L, ItemCount(observed, gear.SealedBundleId));
					Require.True(!observed.Skills.ContainsKey(gear.ForbiddenStigmaSkillId), "Keep the stigma reward sealed.");
					NaturalCoinGearSteps.VerifyRetainedLoadout(observed, coinIncomingLoadout);
					NaturalInventoryPlan bag = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot,
						observed.Inventory.Values.Select(i => i.ItemId), ClassLine).Decide(observed, QuestNeededItems(), gear);
					NaturalAltgardDecision endpoint = NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now, bag.FreeSlots, coinGearProgress, haramelProgress, HaramelNow()),
						NaturalTemplateObjective.From(altgardPlans), 1);
					Require.Equal("complete", endpoint.Outcome);
				}
				void VerifyHaramelEndpoint()
				{
					if (leg.Haramel is not { } rules) return;
					BotWorldModel observed = session.Api.World;
					Require.Equal(leg.Start.CompletedQuestIds.Union(leg.Endpoint.CompletedQuestIds).Count(), observed.CompletedQuestIds.Count);
					Require.All(leg.Order, id => Require.Equal(1, observed.CompletedQuestCounts.GetValueOrDefault(id)));
					Require.All(leg.Start.CompletedQuestIds, id => Require.Contains(id, observed.CompletedQuestIds));
					Require.Equal((long)rules.IronCount, ItemCount(observed, rules.IronItemId));
					Require.Equal((long)rules.BronzeCount, ItemCount(observed, rules.BronzeItemId));
					Require.All(rules.CleanupItemIds, id => Require.Equal(0L, ItemCount(observed, id)));
					Require.True(observed.Inventory.TryGetValue(rules.StaffObjectId, out BotInventoryItem? staff) && staff.ItemId == rules.StaffItemId && staff.Details.EquippedSlot == rules.WeaponSlot, "Haramel replaced the retained staff.");
					Require.True(haramelProgress!.Visits.Count(visit => visit.BossMovieObserved && visit.ChestResolved) >= 2 &&
						haramelProgress.Visits.Any(visit => visit.PostBossQuests && visit.FreshSpawnsObserved), "Two actual fresh clears and their class chest outcomes must be retained.");
					Require.Equal(combat.ReviveCount, haramelProgress.Revives);
					Require.Equal("complete", NaturalAltgardDecisionEngine.Decide(leg,
						NaturalAltgardObservation.Observe(observed, session.CurrentPosition, session.Api.Timing.Now, haramelProgress: haramelProgress, nowMillis: HaramelNow()),
						NaturalTemplateObjective.From(altgardPlans), 1).Outcome);
				}
			}

			// NA-23: the focused Cleric encounter (diagnostic, SIM only). The fixture prepares a level 10 Cleric outside
			// Altgard Fortress and, per stage, the monsters. The journey's own pull, patrol, combat and rest code then
			// plays each stage on the Altgard map: the navigator, combat and map it captures are rebound to Altgard here.
			async Task RunClericEncounterAsync()
			{
				Require.True(combat.IsCleric, $"The NA-23 encounter needs the level 10 Cleric; the character is {combat.ObservedCharacter}.");
				Require.True(runtime.PrepareEncounterStageAsync != null, "The NA-23 encounter needs its stage setup.");
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
				here.DefendOnAttackAsync = navigator.DefendOnAttackAsync;
				here.AvoidHostileAggro = true;
				navigator = here;
				// The journey's geometry was made for the world instance it started in (Ishalgen); line of sight and
				// routes for the pull planner must come from the instance the Cleric now stands in.
				geometry = runtime.CreateGeometry();
				contract = contract with { MapId = session.Api.World.MapId!.Value };
				combat = new NaturalJourneyCombat(session, here, runtime, geometry, stopOnDeath: false, options.OptimizeHubs, mauPolicy,
					ClassLine)
				{
					ApproachMapId = contract.MapId,
				};
				navigationDefense = combat;
				WithQuestLoot(combat);
				await TopUpHelpItemsAsync("run-start"); // NA-21: the approved help items
				var stages = new List<Dictionary<string, object?>>();
				foreach (string stage in runtime.EncounterStages ?? ["single", "pair", "patrol"])
				{
					session.BeginStep($"na23-{stage}", "cleric-encounter-stage");
					await runtime.PrepareEncounterStageAsync!(stage, token);
					await session.SynchronizeAsync(token);
					BotPosition origin = session.CurrentPosition;
					int revivesBefore = combat.ReviveCount, retreatsBefore = combat.CompletedRetreats, kills = 0, plans = 0;
					var defeated = new HashSet<int>();
					long start = runtime.NowMillis;
					for (int pull = 0; pull < 6; pull++)
					{
						await DefendAgainstEngagedAsync($"na23-{stage}");
						IEnumerable<NaturalNavigationObject> encounterTargets = runtime.EncounterNpcIds is { } kinds
							? navigator.Observe().Npcs.Where(npc => kinds.Contains(npc.TemplateId))
							: ObservedPullMonsters().Select(monster => monster.Npc);
						NaturalNavigationObject[] targets = encounterTargets
							.Where(npc => !defeated.Contains(npc.ObjectId) && Distance(origin, npc.Position) < 60)
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
						if (targets.Length == 0) break;
						NaturalPullPlan? plan = await MoveToPullSpotAsync(targets, [], $"na23-{stage}");
						if (plan == null) break;
						plans++;
						if (await combat.TryKillAsync(plan.Target.Npc.ObjectId, token, retreatAnchor: session.CurrentPosition))
						{
							defeated.Add(plan.Target.Npc.ObjectId);
							kills++;
						}
					}
					await DefendAgainstEngagedAsync($"na23-{stage}-after");
					await RestSafelyAsync(token);
					stages.Add(new()
					{
						["stage"] = stage, ["plans"] = plans, ["kills"] = kills, ["deaths"] = combat.ReviveCount - revivesBefore,
						["retreats"] = combat.CompletedRetreats - retreatsBefore,
						["gameSeconds"] = (runtime.NowMillis - start) / 1000, ["hp"] = session.Api.World.CurrentHp,
						["mp"] = session.Api.World.CurrentMp, ["dp"] = session.Api.World.CurrentDp,
					});
					session.TraceDiagnostic("na23-stage-complete", stages[^1]);
				}
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "na23-summary.json"),
					System.Text.Json.JsonSerializer.Serialize(stages, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), token);
			}

			bool AscensionBridgeStarted() => NaturalAscensionDecisionEngine.BridgeStarted(session.Api.World.CompletedQuestIds,
				session.Api.World.Quests, session.Api.World.MapId);

			// NA-17: the bridge endpoint. Assert the contract from the client's view, then quit, log back in and require
			// that class, level, quests, bind point, equipment, inventory and position all survived.
			async Task CompleteAscensionLegAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-endpoint", "verify-and-relog-at-the-bridge-endpoint");
				await TopUpHelpItemsAsync("checkpoint");
				if (relogAt != null) Require.True(relogInjected, "A requested bridge interruption was never exercised.");
				BotWorldModel world = session.Api.World;
				Require.True(NaturalJourneyIdentityRules.Classify(ClassLine, world.Objects[session.CharacterId].PlayerClass ?? 0, world.Level, world.MapId)
					== NaturalJourneyStage.AscensionCleric, $"The endpoint character is not the bridge's {ClassLine.SecondName}.");
				Require.True(world.Level >= bridge.Endpoint.MinimumLevel, $"Endpoint level {world.Level} is below {bridge.Endpoint.MinimumLevel}.");
				Require.All(bridge.Endpoint.CompletedQuestIds, quest => Require.Contains(quest, world.CompletedQuestIds));
				Require.Equal(bridge.Endpoint.MapId, world.MapId!.Value);
				Require.True(world.ObeliskBindPoint is { } bound && bound.MapId == bridge.Bind.MapId, "The endpoint is not bound in Altgard.");
				Require.All(bridge.Endpoint.EquippedItemIds, item => Require.True(world.Inventory.Values.Any(owned => owned.ItemId == item &&
					(owned.Details.EquippedSlot ?? 0) > 0), $"Endpoint item {item} is not worn."));
				// NA-20a: every kept accessory is worn. The item's full slot mask decides (the belt's WAIST bit, 1<<16, does not
				// fit the inventory packet's 16-bit slot field that the checkpoint records).
				Require.All(bridge.KeptAccessories, item => Require.True(world.Inventory.Values.Any(owned => owned.ItemId == item &&
					(owned.Details.EquippedSlot ?? 0) > 0), $"Kept accessory {item} is not worn at the endpoint."));
				// NR-35: another pair's accessories come from the gear rule: at the endpoint it has none left to put on.
				if (!bridge.ReviewedPair)
				{
					NaturalGearRules gear = combat.ClassProfile.Gear;
					Race race = world.Objects.GetValueOrDefault(session.CharacterId)?.Race is byte raceId ? (Race)raceId : Race.ASMODIANS;
					NaturalGearInfo? Describe(int itemId) =>
						NaturalInventoryCheck.Describe(runtime.Data.ItemDataDh.GetItemTemplate(itemId), gear.Class, race);
					NaturalGearUpgrade[] pending = [.. NaturalGearPolicy.SelectUpgrades(world.Inventory.Values, world.Level, Describe,
						(long)Aion.GameServer.Model.Items.ItemSlot.MAIN_OFF_OR_SUB_OFF, refusedGear, gear,
						world.Skills.Keys.Any(NaturalGearPolicy.DualWieldSkillIds.Contains))
						.Where(upgrade => Describe(upgrade.ItemId)?.Group is "RING" or "EARRING" or "NECKLACE" or "BELT")];
					Require.True(pending.Length == 0,
						$"The gear rule would still put on accessory {pending.FirstOrDefault()?.ItemId} at the endpoint.");
				}
				Require.True(!world.IsDead, "The endpoint character is dead.");
				NaturalJourneyCheckpoint before = NaturalJourneyCheckpoint.Capture(world, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				session.BeforeSend = null;
				await session.QuitAsync(token);
				await session.WaitForReentryAsync(token);
				await session.ReloginExistingCharacterAsync(token);
				await session.EnterWorldAsync(token);
				await session.SynchronizeAsync(token);
				NaturalJourneyCheckpoint after = NaturalJourneyCheckpoint.Capture(session.Api.World, session.CharacterId,
					session.ConnectionGeneration, contract, session.CurrentPosition, coinGearProgress: coinGearProgress, haramelProgress: haramelProgress, earlyAscension: options.AscensionBridge, line: ClassLine);
				NaturalJourneyPersistence.Verify(before, after);
				if (laterCapital != null) await laterCapital.WriteCheckpointAsync(Path.GetDirectoryName(combatTracePath)!,
					"bridge", before, after, token, distinctSegment: continuousAltgard);
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "bridge-completion.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						before, after, verified = true,
						// NA-25: an `altgard` snapshot restores with this clock so game time keeps moving forward.
						session.CharacterId, ElapsedMillis = runtime.NowMillis,
					}), token);
				session.TraceDiagnostic("ascension-bridge-complete", new Dictionary<string, object?>
				{
					["level"] = after.Level, ["class"] = after.PlayerClass, ["map"] = after.MapId, ["bind"] = after.BindPoint?.MapId,
					["completed"] = bridge.Endpoint.CompletedQuestIds, ["kinah"] = session.Api.World.Kinah,
				});
			}

			// NA-16: the Altgard shop stop. Wear the best owned gear (accessories included), sell what the Cleric rules call
			// surplus, buy only the contract's consumables (better potions, powder; never gear, OD-7), drink one Tea of Repose.
			async Task ShopInAltgardAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-shop-altgard", "altgard-shop-stop");
				await TopUpHelpItemsAsync("town");
				BotWorldModel world = session.Api.World;
				await EquipUpgradesAsync(token);
				await session.SynchronizeAsync(token);
				long Owned(int item) => world.Inventory.Values.Where(i => i.ItemId == item).Sum(i => i.Count);
				IReadOnlyCollection<int> Tab(int id) => runtime.Data.GoodsListDataDh.GetGoodsListById(id)?.GetItemIdList() ?? [];
				long Base(int item) => runtime.Data.ItemDataDh.GetItemTemplate(item).GetPrice();
				Require.True(!bridge.Shop.BuysGear, "The bridge never buys gear (OD-7).");
				Require.True(bridge.Shop.Purchases.All(p => runtime.Data.ItemDataDh.GetItemTemplate(p.ItemId).GetItemSlot() == 0),
					"A bridge purchase would be equipment.");
				// NR-35: the contract's purchases are the reviewed pair's. Another pair buys the powder only when its kit has
				// it, and the potion by its own restock rule.
				NaturalAscensionPurchase[] purchases = bridge.ReviewedPair ? bridge.Shop.Purchases
					: NaturalAscensionContract.PurchasesFor(bridge.Shop.Purchases, combat.ClassProfile.HelpItems, combat.ClassProfile.Restock,
						[.. world.Inventory.Values], world.Kinah, Base);
				if (!bridge.ReviewedPair)
					session.TraceDiagnostic("altgard-shop-purchases", new Dictionary<string, object?>
					{
						["class"] = bridge.SecondClass.ToString(), ["reviewed"] = bridge.Shop.Purchases.Select(p => $"{p.ItemId}:{p.Target}").ToArray(),
						["own"] = purchases.Select(p => $"{p.ItemId}:{p.Target}").ToArray(), ["kinah"] = world.Kinah,
					});
				var plan = NaturalIshalgenInventoryPolicy.Load(runtime.RepoRoot, world.Inventory.Values.Select(i => i.ItemId), ClassLine).Decide(world);
				var sales = plan.Sales.Select(sale => new NaturalSale(sale.ObjectId, sale.ItemId, sale.Count)).ToList();
				BotPosition VendorAt(int npc) => runtime.Data.SpawnsDh.GetSpawnsByWorldId(bridge.Bind.MapId)
					.Where(group => group.GetNpcId() == npc).SelectMany(group => group.GetSpawnTemplates())
					.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)).First();
				var steps = new NaturalServiceSteps(session);
				var vendors = new[] { bridge.Shop.SellNpcId, bridge.Shop.PotionNpcId, bridge.Shop.ReagentNpcId }.Distinct()
					.OrderBy(npc => Distance(session.CurrentPosition, VendorAt(npc))).ToArray();
				foreach (int vendorNpc in vendors)
				{
					var wanted = purchases
						.Select(p => new NaturalPurchase(p.ItemId, Math.Max(0, (p.Target ?? p.TargetCombinedLifePotions ?? 0) - Owned(p.ItemId))))
						.Where(p => p.Count > 0).ToList();
					var sell = vendorNpc == bridge.Shop.SellNpcId ? sales : [];
					if (wanted.Count == 0 && sell.Count == 0) continue;
					NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(bridge.Bind.MapId, vendorNpc,
						VendorAt(vendorNpc), here, token);
					Require.True(approach.Arrived, $"Altgard vendor {vendorNpc}: {approach.Reason}");
					NaturalVendorResult trade = await steps.TradeAsync(Require.IsType<int>(approach.TargetObjectId), sell, wanted, Tab, Base, token);
					if (vendorNpc == bridge.Shop.SellNpcId) sales = [];
					session.TraceDiagnostic("altgard-shop-visit", new Dictionary<string, object?>
					{
						["vendor"] = vendorNpc, ["sold"] = trade.Sold.Select(sold => $"{sold.ItemId}x{sold.Count}").ToArray(),
						["bought"] = trade.Bought.Select(b => $"{b.ItemId}x{b.Count}").ToArray(),
						["refused"] = trade.Refused.Select(r => $"{r.Purchase.ItemId}:{r.Reason}").ToArray(), ["kinah"] = world.Kinah,
					});
				}
				// A rule's purchase that the vendor's price put out of reach is an outcome, not a broken contract.
				foreach (NaturalAscensionPurchase purchase in purchases)
					if (bridge.ReviewedPair)
						Require.True(Owned(purchase.ItemId) >= (purchase.Target ?? 0), $"Altgard shop left {purchase.ItemId} below its target.");
					else if (Owned(purchase.ItemId) < (purchase.Target ?? 0))
						session.TraceDiagnostic("altgard-shop-shortfall", new Dictionary<string, object?>
						{ ["itemId"] = purchase.ItemId, ["owned"] = Owned(purchase.ItemId), ["target"] = purchase.Target, ["kinah"] = world.Kinah });
				// Tea of Repose (OD-8): once, out of combat, now that the Cleric is level 10.
				int teaId = bridge.CeremonyReward.TeaItemId;
				if (world.Inventory.Values.FirstOrDefault(item => item.ItemId == teaId) is { } tea)
				{
					long before = Owned(teaId);
					await session.SendPacketAsync(session.Api.UseItem(tea.ObjectId, runtime.Data.ItemDataDh.GetItemTemplate(teaId)), token);
					await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
					await session.SynchronizeAsync(token);
					Require.Equal(before - 1, Owned(teaId));
				}
				session.TraceDiagnostic("altgard-shop-result", new Dictionary<string, object?>
				{
					["worn"] = world.Inventory.Values.Where(item => (item.Details.EquippedSlot ?? 0) > 0).Select(item => item.ItemId).Order().ToArray(),
					["supplies"] = new[] { 162000053, 169300003, 160002273, 162001057, 162000002 }.Select(id => $"{id}x{Owned(id)}").ToArray(),
					["kinah"] = world.Kinah,
				});
				bridgeShopVisited = true;
			}

			// NA-15: Doman's teleporter to Altgard (NA-08 service step: the price-adjusted fare, the map change).
			async Task TakeBridgeTeleporterAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-teleport-altgard", "doman-teleporter-to-altgard");
				NaturalAscensionStep doman = bridge.Step(NaturalAscensionStepRole.DispatchStart);
				var anchor = new BotPosition(doman.Position[0], doman.Position[1], doman.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				int npc = await ApproachBridgeNpcAsync(doman, anchor, here);
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).TeleportAsync(npc,
					session.Api.World.Objects[npc].Position, doman.TalkRange, bridge.Teleporter.LocationId, bridge.Teleporter.BasePrice,
					bridge.Teleporter.Destination.MapId, token);
				Require.True(result.IsDone, result.Reason);
				mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
			}

			// NA-15: the Altgard Fortress obelisk, first thing in Altgard (NA-08 service step: within 5 m, raw price).
			async Task BindInAltgardAsync(NaturalAscensionContract bridge)
			{
				session.BeginStep("na-bind-altgard", "bind-at-altgard-fortress-obelisk");
				var obelisk = new BotPosition(bridge.Bind.Position[0], bridge.Bind.Position[1], bridge.Bind.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(bridge.Bind.MapId, bridge.Bind.NpcId,
					obelisk, here, token);
				Require.True(approach.Arrived, $"Altgard obelisk: {approach.Reason}");
				int stone = Require.IsType<int>(approach.TargetObjectId);
				BotPosition at = session.Api.World.Objects[stone].Position;
				NaturalServiceOutcome result = await new NaturalServiceSteps(session).BindAsync(stone, at, bridge.Bind.MapId,
					bridge.Bind.Price, bridge.Bind.AcceptRange, token);
				Require.True(result.IsDone, result.Reason);
			}

			async Task WriteBridgeStopAsync(NaturalAscensionDecision stop)
			{
				session.TraceDiagnostic("ascension-bridge-stop", new Dictionary<string, object?>
				{
					["action"] = stop.Action, ["step"] = stop.StepKey, ["outcome"] = stop.Outcome, ["reason"] = stop.Reason,
				});
				await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "bridge-stop.json"),
					System.Text.Json.JsonSerializer.Serialize(new
					{
						stop, session.Api.World.MapId, session.Api.World.Level, session.CurrentPosition,
						Quests = session.Api.World.Quests.Values.Where(q => LineBridge().Quests.Any(quest => quest.Id == q.QuestId)).ToArray(),
						Completed = session.Api.World.CompletedQuestIds.Where(q => LineBridge().Quests.Any(quest => quest.Id == q)).ToArray(),
					}), token);
			}

			// One contract talk step: approach the NPC on the current map, open its dialog, send the step's actions,
			// wait for each dialog page, finish any movie, and follow a same-map or cross-map quest teleport.
			async Task PlayBridgeTalkAsync(NaturalAscensionStep step)
			{
				session.BeginStep($"na-{step.Key}", "ascension-bridge-talk");
				var anchor = new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0);
				NaturalJourneyNavigator here = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World), newEntry: false);
				int npc = await ApproachBridgeNpcAsync(step, anchor, here);
				for (int attempt = 1; ; attempt++)
				{
					try
					{
						await NaturalDialogProtocol.OpenAsync(session, npc, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npc);
						break;
					}
					catch (NaturalDialogTooFarException) when (attempt < 3)
					{
						npc = await ApproachBridgeNpcAsync(step, anchor, here);
					}
				}
				string[] actions = step.Actions;
				int pageOffset = 0;
				if (actions[0] == "USE_OBJECT")
				{
					// The talk itself was the first action; its page was the dialog just opened.
					actions = actions[1..];
					pageOffset = 1;
				}
				for (int i = 0; i < actions.Length; i++)
				{
					bool last = i == actions.Length - 1;
					bool teleports = last && step.Teleport != null;
					bool otherMap = teleports && step.Teleport!.MapId != session.Api.World.MapId;
					if (otherMap) session.Api.World.BeginWorldReload();
					ushort action = checked((ushort)NaturalAscensionContract.DialogActionId(actions[i]));
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, action, questId: step.QuestId), token);
					if (step.MovieId != null && actions[i] is "SELECT5_1" or "SELECT2_1" or "SELECT3_1")
						await NaturalMovieGate.FinishAsync(session, token);
					if (pageOffset + i < step.Pages.Length && !teleports)
					{
						int page = step.Pages[pageOffset + i];
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet =>
							packet.Get<int>("targetObjectId") == npc && packet.Get<ushort>("dialogPageId") == page);
					}
					if (!teleports) continue;
					if (otherMap)
					{
						await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token, packet => packet.Get<int>("worldId") == step.Teleport!.MapId);
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					}
					else
					{
						await session.WaitForPacketAsync(typeof(SM_CHANNEL_INFO), token);
						await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					}
					session.AcceptTeleportPosition();
					if (otherMap) mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
				}
				if (step.FlyPathId != null)
				{
					// Hagen's QUEST_SELECT starts the scripted flight (teleport 3001, flypath 3); the client flies the path
					// with CM_MOVE_IN_AIR and lands with LAND_FLYTELEPORT. The trial spawns about 2 s before the landing.
					NaturalAscensionInstance instance = LineBridge().Instance;
					await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
						packet.Get<int>("senderObjectId") == session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);
					var landing = new BotPosition(instance.FlyPathEnd[0], instance.FlyPathEnd[1], instance.FlyPathEnd[2], 0);
					await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(session.CurrentPosition, landing,
						instance.MapId, TimeSpan.FromSeconds(instance.FlightSeconds)), token);
					await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
				}
				await session.SynchronizeAsync(token);
				NaturalMovieGate.RecordSkipped(session);
				if (step.ReceivesItemId is int card)
					Require.True(ItemCount(session.Api.World, card) >= 1 || session.Api.World.Quests[step.QuestId].StepAndFlags > step.Var,
						$"{step.Key} did not hand over item {card}.");
				// Munin's SELECT5_1 takes the three Destiny Cards back before Ataxiar opens.
				if (step.Key == "q2008-v4-munin")
					Require.True(LineBridge().Steps.Where(s => s.QuestId == 2008 && s.ReceivesItemId != null)
						.All(s => ItemCount(session.Api.World, s.ReceivesItemId!.Value) == 0), "Munin did not take the Destiny Cards back.");
				if (step == LineBridge().Step(NaturalAscensionStepRole.Ceremony))
				{
					// NA-14: the ceremony pays level 10, the line's ceremony weapon (the Karmic Staff for the Cleric, OD-5),
					// 250,000 Kinah and five teas; wear the weapon.
					NaturalAscensionCeremonyReward reward = LineBridge().CeremonyReward;
					Require.True(ItemCount(session.Api.World, reward.ItemId) == 1, $"The ceremony did not pay the line's weapon {reward.ItemId}.");
					await EquipUpgradesAsync(token);
					await session.SynchronizeAsync(token);
					Require.True(session.Api.World.Inventory.Values.Any(item => item.ItemId == reward.ItemId &&
						(item.Details.EquippedSlot ?? 0) > 0), $"The ceremony weapon {reward.ItemId} was not equipped after the ceremony.");
				}
				session.TraceDiagnostic("ascension-bridge-step", new Dictionary<string, object?>
				{
					["step"] = step.Key, ["map"] = session.Api.World.MapId, ["position"] = session.CurrentPosition,
					["quest"] = session.Api.World.Quests.TryGetValue(step.QuestId, out BotQuestState? state) ? $"{state.Status}/{state.StepAndFlags}" : "done",
				});
			}

			async Task<int> ApproachBridgeNpcAsync(NaturalAscensionStep step, BotPosition anchor, NaturalJourneyNavigator here)
			{
				// NA-19: a travel leg starts; the straight line is a lower bound of the planned route.
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, anchor));
				NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(step.MapId, step.NpcId, anchor, here, token);
				// CP-07a: level 9 can fall deep in the Mau field, where no checked route to Munin is left. A player casts Return
				// there, as Q2005's fallback and the stranded Return do, and walks from the hub bind. Ishalgen only, and only
				// where the approach would otherwise end the run; a second failure ends it as before.
				if (!approach.Arrived && approach.Reason == NoCheckedRoute && step.MapId == contract.MapId &&
					session.Api.World.MapId == contract.MapId && session.Api.World.ObeliskBindPoint is { MapId: 220010000 } bind &&
					Distance(session.CurrentPosition, bind.Position) > 30)
				{
					session.TraceDiagnostic("bridge-approach-return-to-bind", new Dictionary<string, object?>
					{
						["step"] = step.Key, ["npc"] = step.NpcId, ["reason"] = approach.Reason,
						["position"] = session.CurrentPosition, ["bindPosition"] = bind.Position,
					});
					await UseLearnedReturnToBindAsync();
					await RestSafelyAsync(token);
					await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, anchor));
					approach = await NaturalIshalgenNavigator.ApproachNpcAsync(step.MapId, step.NpcId, anchor, here, token);
				}
				Require.True(approach.Arrived, $"{step.Key}: {approach.Reason}");
				return Require.IsType<int>(approach.TargetObjectId);
			}

			async Task<int> ApproachAsync(int templateId, BotPosition anchor)
			{
				await BuffForStarterTravelLegAsync(anchor);
				NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, templateId, anchor, navigator, token);
				Require.True(result.Arrived, result.Reason);
				return Require.IsType<int>(result.TargetObjectId);
			}

			// CP-06: below level 10 a walk to an NPC or a spawn is a travel leg, so the buff-ourself check runs before it and
			// the Running scroll is used before a long one. The straight line is a lower bound of the planned route. From
			// level 10 on the trigger stays at the sites NA-19 gave it.
			async Task BuffForStarterTravelLegAsync(BotPosition destination)
			{
				if (session.Api.World.Level > NaturalHelpItemAllowlist.StarterMaxLevel || session.Api.World.IsDead) return;
				await combat.BuffOurselfAsync(NaturalHelpTrigger.TravelLeg, token, Distance(session.CurrentPosition, destination));
			}

			// The hub optimizer's village bind (NI07_OPTIMIZE_HUBS). CP-07: the same helper as the default route's binds.
			Task BindAtAldelleIfNeededAsync() => BindAtIshalgenObeliskIfNeededAsync(IshalgenVillageObelisk, "aldelle");

			BotPosition IshalgenObeliskPosition(int obeliskId) =>
				graph.GetMap(contract.MapId)!.Waypoints.First(waypoint => waypoint.TemplateId == obeliskId).Position;

			// CP-07 (CP-Q21, the operator, 2026-10-07): "We should have been binding in Ishalgen the whole time, at each quest
			// hub (the village and the outpost), and soul heal as discussed." Before a quest of the village hub is worked the
			// bot binds at the village obelisk, and before a quest of the outpost's two hubs (mijou and anturoon) at the
			// outpost obelisk. Every line does, and so does the Cleric who returns to finish Ishalgen. Quests of the other
			// hubs bind nowhere. Only the two binds are taken from the hub optimizer: no pickup order, no work groups and no
			// hub flight.
			async Task BindAtIshalgenHubIfNeededAsync(int questId)
			{
				if (session.Api.World.MapId != contract.MapId || session.Api.World.IsDead) return;
				switch (NaturalIshalgenHubPolicy.ForQuest(questId)?.Name)
				{
					case "aldelle": await BindAtIshalgenObeliskIfNeededAsync(IshalgenVillageObelisk, "aldelle"); break;
					case "mijou" or "anturoon": await BindAtIshalgenObeliskIfNeededAsync(IshalgenOutpostObelisk, "outpost"); break;
				}
			}

			async Task BindAtIshalgenObeliskIfNeededAsync(int obeliskId, string hub)
			{
				BotWorldModel world = session.Api.World;
				BotPosition obeliskAt = IshalgenObeliskPosition(obeliskId);
				BotBindPoint? bound = world.ObeliskBindPoint is { MapId: 220010000 } registered ? registered : null;
				// Java ResurrectAI refuses a second bind within 20 m of the present bind point; the client sees it the same way.
				if (bound != null && Distance(bound.Position, obeliskAt) < NaturalServicePolicy.SameObeliskRadius) return;
				// Each bind is made once. Work that goes back to the village after the outpost bind does not move the bind back.
				// A character that never bound still shows a bind point, the map's first spawn point (Java sends it at login),
				// so only a bind at the outpost obelisk counts here.
				if (obeliskId == IshalgenVillageObelisk && bound != null &&
					Distance(bound.Position, IshalgenObeliskPosition(IshalgenOutpostObelisk)) < NaturalServicePolicy.SameObeliskRadius) return;
				// The fee is the bind point's price as shipped (Java BindPointTemplate): 43 Kinah at the village, 134 at the outpost.
				int fee = runtime.Data.BindPointDataDh.GetBindPointTemplate(obeliskId)?.GetPrice() ?? int.MaxValue;
				if (world.Kinah < fee)
				{
					session.TraceDiagnostic("ishalgen-hub-bind-skipped", new Dictionary<string, object?>
					{
						["hub"] = hub, ["obelisk"] = obeliskId, ["fee"] = fee, ["kinah"] = world.Kinah,
						["reason"] = "The purse does not cover the fee.",
					});
					return;
				}
				session.BeginStep($"ni07-bind-{hub}", "bind-at-the-working-hubs-obelisk");
				// The outpost is reached by the eastern road, as its first quest reaches it; the direct valley line is blocked.
				if (obeliskId == IshalgenOutpostObelisk && Distance(session.CurrentPosition, obeliskAt) > IshalgenOutpostRadius)
					await WalkEasternRoadToDerotAsync();
				int obelisk = await ApproachShippedSpawnAsync(obeliskId);
				// NA-08: the map-agnostic bind step (the Altgard Fortress bind uses it too).
				BotPosition obeliskPosition = world.Objects[obelisk].Position;
				NaturalServiceOutcome bindOutcome = await new NaturalServiceSteps(session).BindAsync(obelisk, obeliskPosition,
					contract.MapId, fee, acceptRange: 5, token);
				Require.True(bindOutcome.IsDone, bindOutcome.Reason);
				Require.True(world.ObeliskBindPoint is { MapId: 220010000 } now &&
					Distance(now.Position, obeliskPosition) < NaturalServicePolicy.SameObeliskRadius,
					$"The client did not observe the bind point at the {hub} obelisk.");
				session.TraceDiagnostic("ishalgen-hub-bind", new Dictionary<string, object?>
				{
					["hub"] = hub, ["obelisk"] = obeliskId, ["fee"] = fee, ["kinah"] = world.Kinah,
					["bindPoint"] = world.ObeliskBindPoint!.Position,
					["fromObelisk"] = Distance(world.ObeliskBindPoint.Position, obeliskPosition),
				});
			}

			async Task UseFasterTravelAsync(BotPosition destination, bool forceHubFlight = false)
			{
				if ((!options.OptimizeHubs && !forceHubFlight) || session.Api.World.MapId != contract.MapId ||
					session.Api.World.IsDead) return;
				long fare = session.Api.World.VendorPrices?.ServicePrice(160) ?? 160;
				NaturalTravelChoice choice = NaturalJourneyTravelPolicy.Choose(
					session.CurrentPosition, destination, session.Api.World.ObeliskBindPoint,
					!forceHubFlight && session.Api.World.Skills.ContainsKey(243) &&
						session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(1) &&
						runtime.NowMillis - lastReturnMillis >= TimeSpan.FromMinutes(20).TotalMilliseconds + 1000,
					session.Api.World.Kinah >= fare);
				if (choice == NaturalTravelChoice.Walk) return;
				session.TraceDiagnostic("journey-travel-choice", new Dictionary<string, object?>
				{
					["choice"] = choice.ToString(), ["from"] = session.CurrentPosition,
					["destination"] = destination, ["fare"] = fare,
				});
				if (choice == NaturalTravelChoice.Return)
				{
					await UseLearnedReturnToBindAsync();
					return;
				}
				int npcId = choice == NaturalTravelChoice.FlightToAnturoon ? 203513 : 203545;
				int locationId = choice == NaturalTravelChoice.FlightToAnturoon ? 18 : 17;
				int pathId = choice == NaturalTravelChoice.FlightToAnturoon ? 11 : 12;
				var path = runtime.Data.FlyPathDataDh.GetPathTemplate(pathId)
					?? throw new InvalidDataException($"Java 4.8 flight path {pathId} is missing.");
				BotPosition departure = new(path.GetStartX(), path.GetStartY(), path.GetStartZ(), 0);
				BotPosition arrival = new(path.GetEndX(), path.GetEndY(), path.GetEndZ(), 0);
				session.BeginStep($"ni07-flight-{pathId}", "take-cheaper-faster-flight-transporter");
				int transporter = await ApproachAsync(npcId, departure);
				if (Distance(session.CurrentPosition, departure) > 6)
				{
					NaturalNavigationResult atPath = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, departure, navigator, "flight-departure", token);
					Require.True(atPath.Arrived, atPath.Reason);
				}
				Require.True(Distance(session.CurrentPosition, departure) <= 7,
					"Flight validator requires the client to stand by the path start.");
				await session.SendPacketAsync(session.Api.Teleport(transporter, locationId), token);
				await session.WaitForPacketAsync(typeof(SM_EMOTION), token, packet =>
					packet.Get<int>("senderObjectId") == session.CharacterId &&
					packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT &&
					packet.Get<int>("teleportId") == pathId * 1000 + 1);
				await session.ExecuteMovementAsync(CapitalAscensionScenario.CreateQuestFlight(
					session.CurrentPosition, arrival, contract.MapId,
					TimeSpan.FromMilliseconds(path.GetTimeInMs())), token);
				await session.SendPacketAsync(GameClientPackets.Emotion((byte)EmotionType.LAND_FLYTELEPORT), token);
				await session.SynchronizeAsync(token);
				Require.True(Distance(session.CurrentPosition, arrival) < 8,
					"Flight landing was not client-observed at the destination.");
			}


			bool SpawnsOnMap(int templateId) =>
				graph.GetMap(contract.MapId)!.Waypoints.Any(waypoint => waypoint.TemplateId == templateId);
			bool UsesProvenWarlockSpawn(int templateId) => altgardLegId == "l10" && templateId == 210538;
			bool IsProvenWarlockSource(NaturalNavigationObject npc, BotPosition anchor)
			{
				// Remember the native spawn announcement, not the position of a caster
				// chasing a retreat. Other camps remain visible hazards and defenders.
				for (; provenWarlockPacketCursor < session.PacketHistory.Count; provenWarlockPacketCursor++)
				{
					DecodedBotServerPacket packet = session.PacketHistory[provenWarlockPacketCursor];
					if (packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == 210538 &&
						Distance(new BotPosition(packet.Get<float>("x"), packet.Get<float>("y"), packet.Get<float>("z"), 0), anchor) < 1)
						provenWarlockSources.Add(packet.Get<int>("objectId"));
				}
				return provenWarlockSources.Contains(npc.ObjectId);
			}

			// Of several kinds that serve the same objective, the one to hunt next: a live one in view first, else the kind whose
			// shipped spawn lies nearest (Q2230's tusks drop from six mosbear kinds; always the first meant long walks past others).
			int NearestKind(IReadOnlyList<int> kinds)
			{
				if (haramelKind != null && altgardLeg?.Haramel?.MapId == session.Api.World.MapId) return haramelKind(kinds);
				bool Connected(BotPosition at) => altgardLeg?.PillarFlight is not { } pillar || pillar.IsUpper(session.CurrentPosition.Z) ||
					geometry.GroundAround(contract.MapId, at, [3f, 5f, 8f, 12f]).Any(point => Distance(point, at) <= 6 &&
						geometry.OnSameIsland(contract.MapId, session.CurrentPosition, point));
				BotKnownObject? seen = session.Api.World.Objects.Values
					.Where(known => known.Kind == BotKnownObjectKind.Npc && known.TemplateId is int id && kinds.Contains(id) && !known.IsCorpse &&
						!navigator.UnavailableObjects.Contains(known.ObjectId) && Connected(known.Position))
					.OrderBy(known => Distance(known.Position, session.CurrentPosition)).FirstOrDefault();
				if (seen?.TemplateId is int visible) return visible;
				return graph.GetMap(contract.MapId)!.Waypoints.Where(waypoint => waypoint.TemplateId is int id && kinds.Contains(id) && Connected(waypoint.Position))
					.OrderBy(waypoint => Distance(waypoint.Position, session.CurrentPosition)).Select(waypoint => waypoint.TemplateId!.Value)
					.DefaultIfEmpty(kinds.First(SpawnsOnMap)).First();
			}

			/// <param name="withinRange">Stop this far from the observed NPC instead of at arm's reach, outside its
			/// aggro circle, so the fight can be planned (a pull) rather than started by walking into it.</param>
			async Task<int> ApproachShippedSpawnAsync(int templateId, bool skipBlockedTarget = false, float? withinRange = null,
				bool returnedFromStrand = false, Func<int?>? completedSource = null, bool acceptObservedKill = false, bool returnedForRoute = false)
			{
				if (bookCollectionUnderway && templateId == 210404 && session.Api.World.MapId != 220010000)
					throw new NaturalBookCollectionMapChangedException();
				int sourceHistoryStart = session.PacketHistory.Count;
				HashSet<int> sourceObjects = session.Api.World.Objects.Values.Where(known => known.TemplateId == templateId)
					.Select(known => known.ObjectId).ToHashSet();
				int? SourceKilledOnTheWay()
				{
					foreach (DecodedBotServerPacket packet in session.PacketHistory.Skip(sourceHistoryStart).Where(packet =>
						packet.PacketType == typeof(SM_NPC_INFO) && packet.Get<int>("npcId") == templateId))
						sourceObjects.Add(packet.Get<int>("objectId"));
					// Java SM_ATTACK_STATUS 19..23 carries MP; its zero percentage is not a kill.
					return session.PacketHistory.Skip(sourceHistoryStart).Where(packet => BotAttackStatus.CarriesHp(packet) &&
						packet.Get<byte>("hpOrMp") == 0 && sourceObjects.Contains(packet.Get<int>("objectId")))
						.Select(packet => (int?)packet.Get<int>("objectId")).LastOrDefault();
				}
				int? CompletedApproachSource()
				{
					if (completedSource?.Invoke() is int collectedFrom)
					{
						emptySpawnWaits = 0;
						return collectedFrom;
					}
					// BC-06: navigation defense can kill the requested source before this approach selects it.
					// Return that observed kill to the ordinary objective loop, including a partially collected stack.
					if (acceptObservedKill && SourceKilledOnTheWay() is int killedSource)
					{
						emptySpawnWaits = 0;
						session.TraceDiagnostic("source-killed-during-approach", new Dictionary<string, object?>
						{
							["templateId"] = templateId, ["objectId"] = killedSource,
						});
						return killedSource;
					}
					return null;
				}
				if (haramelApproach != null && altgardLeg?.Haramel?.MapId == session.Api.World.MapId)
					return await haramelApproach(templateId, withinRange ?? MathF.Min(5, runtime.Data.NpcDataDh.GetNpcTemplate(templateId)?.GetTalkDistance() ?? 3), CompletedApproachSource);
				// Dead on entry (a use bar or a walk ended in a death nobody handled): revive and recover first.
				if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0) await RestSafelyAsync(token);
				if (templateId == 204408 && approachRecordedBulagan != null && await approachRecordedBulagan() is int bulagan)
					return bulagan;
				// NL-4: revived outside the instance whose room holds this spawn (any approach that a death interrupted, the
				// guard-clear's own retry too, the trial nl4-vs-d4): the leg's next decision is the way back in.
				if (altgardLeg?.InstanceRooms is { } roomsLeft && session.Api.World.MapId is int standing && standing != roomsLeft.MapId &&
					runtime.Data.SpawnsDh.GetSpawnsByWorldId(roomsLeft.MapId).Any(group => group.GetNpcId() == templateId) &&
					!runtime.Data.SpawnsDh.GetSpawnsByWorldId(standing).Any(group => group.GetNpcId() == templateId))
					throw new NaturalLeftInstanceRoomException();
				bool Connected(BotWaypoint waypoint) => templateId is < 210000 or >= 700000 ||
					altgardLeg?.PillarFlight is not { } pillar || pillar.IsUpper(session.CurrentPosition.Z) ||
					geometry.GroundAround(contract.MapId, waypoint.Position, [3f, 5f, 8f, 12f]).Any(point => Distance(point, waypoint.Position) <= 6 &&
						geometry.OnSameIsland(contract.MapId, session.CurrentPosition, point));
				BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
					.Where(waypoint => waypoint.TemplateId == templateId)
					// BC-07 observed two ordinary kills at this lower camp, including its 300-second respawn.
					// The continuous run repeatedly died re-entering the western camp after its first kill.
					// Farm the proved shipped source; wait for its real respawn rather than cross more camps.
					.Where(waypoint => !UsesProvenWarlockSpawn(templateId) ||
						Distance(waypoint.Position, new BotPosition(2400.88f, 2171.88f, 270.328f, 2)) < 1)
					.OrderByDescending(Connected).ThenBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).ToArray();
				// NL-3: a siege spawn (Q24042's prisoners at Sulfur Fortress) is in no spawn graph; the leg's own steps name where it
				// stands.
				if (anchors.Length == 0 && altgardLeg != null)
					anchors = [.. altgardLeg.Steps.Where(step => step.NpcId == templateId && altgardLeg.StepMap(step) == contract.MapId)
						.Select(step => new BotWaypoint(-1, contract.MapId, new BotPosition(step.Position[0], step.Position[1], step.Position[2], 0),
							BotWaypointSource.QuestNpc, templateId))];
				if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
				await BuffForStarterTravelLegAsync(anchors[0].Position);
				// AG-07: from far off, the navigator's hazard replanning can circle over monster ground for its whole budget (the
				// Leg 6 smoke run: 1,000 segments between Trader's Berth and Gerger, across the angolems). An Altgard leg walks its
				// road there first, fighting what engages, as its talk steps and hunts do when the navigator gives up.
				if (farApproach != null && !farApproachUnderway && (Distance(session.CurrentPosition, anchors[0].Position) > FarApproachMetres ||
					altgardLeg?.PillarFlight is { } pillar && pillar.IsUpper(session.CurrentPosition.Z) != pillar.IsUpper(anchors[0].Position.Z)))
				{
					BotPosition beforeFarApproach = session.CurrentPosition;
					int revivesBeforeFarApproach = combat.ReviveCount;
					farApproachUnderway = true;
					try { await farApproach(anchors[0].Position, templateId, altgardLegId == "l10" ? () => CompletedApproachSource() != null : null); }
					finally { farApproachUnderway = false; }
					if (CompletedApproachSource() is int completedOnRoad) return completedOnRoad;
					if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revivesBeforeFarApproach)
						return await RetryAfterReviveAsync(beforeFarApproach, "far-road");
					anchors = [.. anchors.OrderByDescending(Connected).ThenBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))];
				}
				if (CompletedApproachSource() is int roadSource) return roadSource;
				var reasons = new List<string>();
				foreach (BotWaypoint anchor in anchors.Take(12))
				{
					var progress = new NaturalApproachProgress();
					int islandFlights = 0;
					int floorGlides = 0;
					for (int guardClears = 0; guardClears <= NaturalApproachProgress.MaximumAttempts; guardClears++)
					{
						BotPosition beforeApproach = session.CurrentPosition;
						int revivesBeforeApproach = combat.ReviveCount;
						int killsBeforeApproach = navigator.UnavailableObjects.Count;
						// The demonstrated Fire route bypasses the middle tier. Take it before attempting a ground route,
						// even when the bake puts two tiers on one connected island.
						if (floorGlides < 3 && glideDown != null && !combat.InCombat && !session.Api.World.IsDead &&
							preferRecordedGlide?.Invoke(anchor.Position) == true)
						{
							floorGlides++;
							if (await glideDown(anchor.Position) && Distance(beforeApproach, session.CurrentPosition) > 2) continue;
							if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revivesBeforeApproach)
								return await RetryAfterReviveAsync(beforeApproach, "recorded-glide");
							throw new InvalidOperationException("The preferred recorded Fire descent could not be completed safely.");
						}
						if (templateId == 212876 && prepareRecordedFireFight != null && !await prepareRecordedFireFight())
						{
							if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revivesBeforeApproach)
								return await RetryAfterReviveAsync(beforeApproach, "recorded-fire-clearing");
							if (session.Api.World.MapId == 320050000 && session.CurrentPosition.Z is > 540 and < 565)
							{
								await DefendAgainstEngagedAsync("fire-recorded-guard-retry");
								if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revivesBeforeApproach)
									return await RetryAfterReviveAsync(beforeApproach, "recorded-fire-defense");
								progress.Observe(Distance(beforeApproach, session.CurrentPosition), navigator.UnavailableObjects.Count > killsBeforeApproach);
								session.TraceDiagnostic("fire-recorded-guard-retry", new Dictionary<string, object?>
								{ ["attempt"] = guardClears, ["stalledAttempts"] = progress.StalledAttempts, ["position"] = session.CurrentPosition });
								if (session.Api.World.MapId == 320050000 && session.CurrentPosition.Z is > 540 and < 565 && progress.CanRetry(guardClears + 1))
								{
									await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
									continue;
								}
							}
							throw new InvalidOperationException("The recorded Fire guards were not cleared safely before approaching Bolcan.");
						}
						if (templateId == 212876 && prepareRecordedFireFight != null && session.Api.World.MapId == 320050000 &&
							session.CurrentPosition.Z is > 540 and < 565 && session.Api.World.Objects.Values.FirstOrDefault(known =>
								known.Kind == BotKnownObjectKind.Npc && known.TemplateId == templateId && !known.IsCorpse) is { } recordedBoss)
						{
							// Let the class fight from the recorded origin; a generic arm's-reach approach would undo the pull.
							emptySpawnWaits = 0;
							return recordedBoss.ObjectId;
						}
						NaturalNavigationResult result = withinRange is float range
							? await NaturalIshalgenNavigator.ExploreWithinRangeAsync(contract.MapId, templateId, anchor.Position, range, navigator, "NPC", token,
								stopWhen: () => CompletedApproachSource() != null,
								targetFilter: UsesProvenWarlockSpawn(templateId) ? npc => IsProvenWarlockSource(npc, anchor.Position) : null)
							: await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, templateId, anchor.Position, navigator, token);
						if (CompletedApproachSource() is int collectedFrom) return collectedFrom;
						if (session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revivesBeforeApproach)
						{
							// Navigation defense can revive before the guard-clear branch starts.
							// Its lower-floor route is stale at the upper bind; repeat hub/pillar travel.
							return await RetryAfterReviveAsync(beforeApproach, "navigation");
						}
						if (result.Arrived && result.TargetObjectId is int objectId) { emptySpawnWaits = 0; return objectId; }
						if (result.Arrived) // explore mode reached the hint with nothing in view: same as an empty hint
							result = new(false, $"Reached the spawn hint but no NPC was observed within {withinRange:F0} m.", null, result.RouteSearches, result.Segments);
						// NL-4: a spawn on a lower floor that no walk joins (the Sky Temple Interior's tiers) is glided down to, whatever
						// the route search said (the trial nl4-vs-d3's hazards hid the missing walk and the bot fought a tier for 35 minutes).
						if (glideDown != null && floorGlides < 3 && !result.Arrived && session.Api.World.MapId is int glideMap &&
							anchor.Position.Z < session.CurrentPosition.Z - 10 && !geometry.OnSameIsland(glideMap, session.CurrentPosition, anchor.Position))
						{
							floorGlides++;
							BotPosition beforeGlide = session.CurrentPosition;
							if (await glideDown(anchor.Position) && Distance(beforeGlide, session.CurrentPosition) > 2) continue;
						}
						// NL-3: a spawn on another island is flown to, not cleared toward: no walk joins them (Kuenunerk's jewel
						// boxes stand on scraps of ground 190 m above one another, and the Templar waited at the foot of one).
						if (flyAcross != null && islandFlights < 3 && result.Reason == "No collision-checked route to the current destination." &&
							session.Api.World.MapId is int spawnMap &&
							geometry.IslandGround(spawnMap, session.CurrentPosition, at => Distance(at, anchor.Position) <= (withinRange ?? 25) + 5).Count == 0)
						{
							islandFlights++;
							BotPosition beforeFlight = session.CurrentPosition;
							if (await flyAcross(anchor.Position) && Distance(beforeFlight, session.CurrentPosition) > 2) continue;
						}
						// Monsters on the way are not a wall: fight through to the objective (observed or its
						// shipped anchor) one pull at a time before giving up on this spawn hint.
						if (progress.CanRetry(guardClears) && result.Reason is "No collision-checked route to the current destination." or
								"New client-observed hazards exceeded the bounded replan budget." or NaturalIshalgenNavigator.RepeatedRouteReason)
						{
							NaturalNavigationObject? seen = result.TargetObjectId is int blockedObjectId
								? navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == blockedObjectId) : null;
							int revivesBefore = combat.ReviveCount;
							bool cleared = await TryClearObservedBlockerAsync(seen?.Position ?? anchor.Position, seen?.ObjectId);
							if (combat.ReviveCount > revivesBefore)
							{
								// A death moved the client back to its bind point. None of this hint's
								// failed route observations apply there; recover and plan the journey again.
								await RestSafelyAsync(token);
								return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
							}
							bool progressed = progress.Observe(Distance(beforeApproach, session.CurrentPosition),
								navigator.UnavailableObjects.Count > killsBeforeApproach);
							if (CompletedApproachSource() is int clearedSource) return clearedSource;
							if (cleared || progressed)
							{
								// Returning from bind may need more than eight guards. Count stalls, not successful
								// fights or checked walking progress, against the local recovery budget.
								// A failed pull can still move to a new approach: re-observe and route from there.
								if (!cleared)
									session.TraceDiagnostic("guard-clear-replan-after-progress", new Dictionary<string, object?>
									{
										["templateId"] = templateId, ["from"] = beforeApproach,
										["position"] = session.CurrentPosition, ["attempt"] = guardClears + 1,
									});
							continue;
							}
							// A moving patrol can briefly close every checked route. Re-observe after
							// it has had time to move, but do not wait indefinitely at one hint.
							if (progress.StalledAttempts <= 3)
							{
								session.TraceDiagnostic("guard-clear-wait-and-retry", new Dictionary<string, object?>
								{
									["templateId"] = templateId, ["position"] = session.CurrentPosition,
									["stall"] = progress.StalledAttempts,
								});
								await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
								await session.SynchronizeAsync(token);
								continue;
							}
							// M5: ground inside a FLY zone that the navmesh does not join to this one (the Fire Temple's crater).
							if (flyAcross != null && !combat.InCombat && await flyAcross(anchor.Position))
							{
								await session.SynchronizeAsync(token);
								return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource,
									acceptObservedKill: acceptObservedKill, returnedForRoute: returnedForRoute);
							}
							// M2: still no way from where it stands (the north end of Morheim's ice lake, below the fortress, where
							// the Gladiator waited for good): do what a player does, fly back to the hub where it may fly, else Return
							// to the bind, and plan the walk from there. Once for an approach, and only on a leg of Morheim, whose obelisk
							// is on the hub's map.
							if (!returnedForRoute && altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) &&
								!session.Api.World.IsDead && !combat.InCombat && session.Api.World.ObeliskBindPoint is { } boundAt &&
								boundAt.MapId == session.Api.World.MapId && Distance(session.CurrentPosition, boundAt.Position) > 50)
							{
								BotPosition stranded = session.CurrentPosition;
								bool flew = flyOutOfStrand != null && await flyOutOfStrand();
								if (!flew) await UseLearnedReturnToBindAsync();
								session.TraceDiagnostic("no-route-way-out", new Dictionary<string, object?>
								{
									["templateId"] = templateId, ["position"] = stranded, ["way"] = flew ? "flight-to-the-hub" : "return-to-bind",
									["arrived"] = session.CurrentPosition,
								});
								await session.SynchronizeAsync(token);
								await RestSafelyAsync(token);
								return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill,
									returnedForRoute: true);
							}
						}
						if (skipBlockedTarget && result.TargetObjectId is int unavailableObjectId &&
							result.Reason == "No collision-checked route to the current destination.")
						{
							// A guarded object remains unreachable after bounded ordinary
							// fights. Try another shipped spawn; do not fabricate access.
							navigator.UnavailableObjects.Add(unavailableObjectId);
							session.TraceDiagnostic("skip-blocked-quest-object", new Dictionary<string, object?>
							{
								["templateId"] = templateId,
								["objectId"] = unavailableObjectId,
								["anchor"] = anchor.Position,
								["position"] = session.CurrentPosition,
								["route"] = navigator.LastRouteDiagnostic,
							});
						}
						reasons.Add(result.Reason);
						break;
					}
				}
				// Every hint empty: wait through the shipped respawn, including the Heart debris's 295 s.
				// BC-06: navigation defense can kill a newly respawned source before this approach selects it.
				// Give that kill its own respawn wait; the prior hunter's exhausted budget is not a stall here.
				if (altgardLegId == "l10" && SourceKilledOnTheWay() != null)
				{
					emptySpawnWaits = 0;
					session.TraceDiagnostic("spawn-wait-reset-after-source-kill", new Dictionary<string, object?> { ["templateId"] = templateId });
				}
				int respawnSeconds = runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId)
					.Where(group => group.GetNpcId() == templateId).Select(group => group.GetRespawnTime()).DefaultIfEmpty(180).Max();
				int waitRounds = Math.Max(4, (int)Math.Ceiling(respawnSeconds / 60d) + 1);
				if (reasons.Count > 0 && reasons.All(reason => reason.Contains("no NPC was observed", StringComparison.Ordinal)) &&
					++emptySpawnWaits <= waitRounds)
				{
					session.TraceDiagnostic("spawn-wait", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["wait"] = emptySpawnWaits, ["position"] = session.CurrentPosition,
					});
					await RestSafelyAsync(token);
					if (CompletedApproachSource() is int sweptSource) return sweptSource;
					if (altgardLegId == "l10" && SourceKilledOnTheWay() != null) emptySpawnWaits = 0;
					if (altgardLegId == "l10")
					{
						// A patrol can respawn next to the waiting player. Observe and defend throughout this
						// minute instead of advancing its attacks for sixty seconds without a player response.
						long until = runtime.NowMillis + 60000;
						while (runtime.NowMillis < until)
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
							await session.SynchronizeAsync(token);
							if (session.Api.World.IsDead) await RestSafelyAsync(token);
							await DefendAgainstEngagedAsync("source-respawn-wait");
							if (CompletedApproachSource() is int respawnSource) return respawnSource;
						}
					}
					else await session.AdvanceAsync(TimeSpan.FromSeconds(60), token);
					await session.SynchronizeAsync(token);
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				emptySpawnWaits = 0;
				// AK-08: no hint has a route from here at all: a fight left the Cleric on ground the navmesh does not connect (smoke
				// run 7: 20 m above MuMu Village). Cast Return, as the road walk does, and try once more from the bind.
				// NL-3: no route from here to any spawn of it, on a map where the bot may fly: a flight first (the training ground
				// below Primum is an island of its own), as a player flies there; the stranded Return waited out its cooldown.
				if (!returnedFromStrand && reasons.Count > 0 && flyAcross != null && !combat.InCombat && anchors.Length > 0 &&
					reasons.All(reason => reason == "No collision-checked route to the current destination.") &&
					await flyAcross(anchors[0].Position))
				{
					await session.SynchronizeAsync(token);
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, returnedFromStrand: true, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				// NL-3: only when the bind is on this map: a Return to another map does not reach a spawn here (the Abyss Gate's
				// platform in Morheim, for a leg bound in Brusthonin).
				if (!returnedFromStrand && reasons.Count > 0 && session.Api.World.Skills.ContainsKey(243) &&
					session.Api.World.ObeliskBindPoint?.MapId == session.Api.World.MapId &&
					reasons.All(reason => reason == "No collision-checked route to the current destination."))
				{
					session.TraceDiagnostic("stranded-return", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["position"] = session.CurrentPosition,
					});
					await UseLearnedReturnToBindAsync();
					await RestSafelyAsync(token);
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, returnedFromStrand: true, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				// NR-R14b: the kill step leaves a monster that failed twice for another of its kind (AK-08). When it is the only
				// one, Q2277's one Black Claw hunter or Q2292's one Mumu Ang, the hunt then finds none while that monster walks
				// past in view. Before the hunt ends for want of one, a monster so left that still lives is taken back.
				int[] leftAlive = session.Api.World.Objects.Values
					.Where(known => known.TemplateId == templateId && !known.IsCorpse && navigator.LeftForAnother.Contains(known.ObjectId))
					.Select(known => known.ObjectId).Order().ToArray();
				if (leftAlive.Length > 0)
				{
					foreach (int left in leftAlive)
					{
						navigator.LeftForAnother.Remove(left);
						navigator.UnavailableObjects.Remove(left);
					}
					session.TraceDiagnostic("left-target-taken-back", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["objects"] = leftAlive, ["position"] = session.CurrentPosition,
					});
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange, completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
				throw new InvalidDataException($"No client-observed NPC {templateId} at {Math.Min(12, anchors.Length)} shipped spawn hints " +
					$"from {session.CurrentPosition}: {string.Join(" | ", reasons)}");

				async Task<int> RetryAfterReviveAsync(BotPosition from, string phase)
				{
					await RestSafelyAsync(token);
					// NL-4: revived outside the instance whose room holds this spawn: the leg's next decision is the way back in.
					if (altgardLeg?.InstanceRooms is { } rooms && session.Api.World.MapId != rooms.MapId &&
						runtime.Data.SpawnsDh.GetSpawnsByWorldId(rooms.MapId).Any(group => group.GetNpcId() == templateId))
						throw new NaturalLeftInstanceRoomException();
					session.TraceDiagnostic("spawn-approach-retry-after-revive", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["phase"] = phase, ["from"] = from,
						["position"] = session.CurrentPosition, ["revives"] = combat.ReviveCount,
					});
					return await ApproachShippedSpawnAsync(templateId, skipBlockedTarget, withinRange,
						completedSource: completedSource, acceptObservedKill: acceptObservedKill);
				}
			}

			async Task<int> ApproachShippedCombatSpawnAsync(int templateId)
			{
				BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
					.Where(waypoint => waypoint.TemplateId == templateId)
					.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).Take(12).ToArray();
				if (anchors.Length == 0) throw new InvalidDataException($"Shipped spawn graph has no combat NPC {templateId}.");
				var reasons = new List<string>();
				foreach (BotWaypoint anchor in anchors)
				{
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
						contract.MapId, templateId, anchor.Position, combat.ClassProfile.Ranges.SpawnApproachRange, navigator,
						$"{ClassLine.StarterLabel}-spell-range-target", token);
					if (!approach.Arrived)
					{
						reasons.Add(approach.Reason);
						continue;
					}
					// Prefer the target and spot that pull it alone (fewest helpers), not merely the nearest.
					NaturalNavigationObject[] visible = navigator.Observe().Npcs
						.Where(npc => npc.TemplateId == templateId &&
							Distance(session.CurrentPosition, npc.Position) <= combat.ClassProfile.Ranges.SpawnPullScanRange)
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
					if (visible.Length > 0 && await MoveToPullSpotAsync(visible, [], $"quest-kill-{templateId}") is { Helpers.Count: 0 } pull)
					{
						session.TraceDiagnostic("combat-standoff-selected", new Dictionary<string, object?>
						{
							["templateId"] = templateId,
							["objectId"] = pull.Target.Npc.ObjectId,
							["distance"] = Distance(session.CurrentPosition, pull.Target.Npc.Position),
							["position"] = session.CurrentPosition,
							["cleanPull"] = true,
						});
						return pull.Target.Npc.ObjectId;
					}
					for (int scan = 0; scan < 2; scan++)
					{
						NaturalNavigationObject? observed = navigator.Observe().Npcs
							.Where(npc => npc.TemplateId == templateId &&
								Distance(session.CurrentPosition, npc.Position) <= combat.ClassProfile.Ranges.SpawnApproachRange)
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
							.FirstOrDefault();
						if (observed != null)
						{
							session.TraceDiagnostic("combat-standoff-selected", new Dictionary<string, object?>
							{
								["templateId"] = templateId,
								["objectId"] = observed.ObjectId,
								["distance"] = Distance(session.CurrentPosition, observed.Position),
								["position"] = session.CurrentPosition,
							});
							return observed.ObjectId;
						}
						await session.SynchronizeAsync(token);
					}
					reasons.Add($"No client-observed {templateId} inside {combat.ClassProfile.Ranges.SpawnApproachRange} m of {anchor.Position}.");
				}
				throw new InvalidDataException($"No spell-range client-observed NPC {templateId}: " +
					string.Join(" | ", reasons));
			}

			async Task<int> ApproachShippedNpcThroughObservedGuardsAsync(int templateId,
				int maximumGuardClears)
			{
				BotWaypoint[] anchors = graph.GetMap(contract.MapId)!.Waypoints
					.Where(waypoint => waypoint.TemplateId == templateId)
					.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position)).Take(12).ToArray();
				if (anchors.Length == 0)
					throw new InvalidDataException($"Shipped spawn graph has no NPC {templateId}.");
				var reasons = new List<string>();
				foreach (BotWaypoint anchor in anchors)
				{
					for (int cleared = 0; cleared <= maximumGuardClears; cleared++)
					{
						int revivesBefore = combat.ReviveCount;
						NaturalNavigationResult result = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, templateId, anchor.Position, navigator, token);
						if (combat.ReviveCount > revivesBefore)
							throw new NaturalGuardedObjectiveRevivedException(
								$"{ClassLine.StarterName} revived while approaching guarded NPC {templateId} from {anchor.Position}.");
						if (result.Arrived && result.TargetObjectId is int objectId) return objectId;
						reasons.Add(result.Reason);
						if (result.Reason is not ("No collision-checked route to the current destination." or
							"New client-observed hazards exceeded the bounded replan budget.") ||
							cleared == maximumGuardClears) break;
						bool guardKilled = await TryClearObservedBlockerAsync(anchor.Position);
						if (combat.ReviveCount > revivesBefore)
							throw new NaturalGuardedObjectiveRevivedException(
								$"{ClassLine.StarterName} revived while clearing guarded NPC {templateId} from {anchor.Position}.");
						if (!guardKilled) break;
						session.TraceDiagnostic("return-corridor-guard-cleared", new Dictionary<string, object?>
						{
							["npcTemplateId"] = templateId,
							["clearedCount"] = cleared + 1,
							["position"] = session.CurrentPosition,
						});
					}
				}
				throw new InvalidDataException($"No checked guarded approach to NPC {templateId} " +
					$"from {session.CurrentPosition}: {string.Join(" | ", reasons)}; " +
					$"last route: {navigator.LastRouteDiagnostic}");
			}

			// Observed monsters as the pull planner sees them (Java aggro range and tribe).
			NaturalPullMonster[] ObservedPullMonsters(int? except = null) => navigator.Observe().Npcs
				.Where(npc => npc.ObjectId != except)
				.Select(npc => (npc, template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
				.Where(entry => runtime.IsAggressive(entry.template))
				.Select(entry => new NaturalPullMonster(entry.npc, entry.template!.GetAggroRange(),
					entry.template.GetTribe().ToString(),
					entry.template.GetBoundRadius().GetMaxOfFrontAndSide()))
				.ToArray();
			NaturalPullMonster PullMonsterOf(NaturalNavigationObject npc)
			{
				var template = runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId);
				return new NaturalPullMonster(npc, runtime.AggroRadius(template),
					template?.GetTribe().ToString() ?? "NONE",
					template?.GetBoundRadius().GetMaxOfFrontAndSide() ?? 0);
			}
			bool CanSupport(string helper, string asking) =>
				Enum.TryParse(helper, out Aion.GameServer.Model.TribeClass h) && Enum.TryParse(asking, out Aion.GameServer.Model.TribeClass a) &&
				runtime.Data.TribeRelations.CanSupport(h, a);

			// Monsters already on the bot (attacked it in the recent packet window and still within 30 m), pursuers
			// still closing in (over 10 m from where they were first seen, and nearer the bot than that spot), and
			// monsters whose aggro circle the bot stands in (one that just respawned beside it, as a Q2007 stalker
			// did). A player deals with these before pulling anything new. One that stays quiet (out of sight) is
			// waited for only once (quietNeighbours).
			(int[] Attackers, int[] Pursuers) Engaged()
			{
				var observed = navigator.Observe().Npcs.ToDictionary(npc => npc.ObjectId);
				var active = new HashSet<int>();
				NaturalCombatRetreatPolicy.ObserveEngagement(active, session.PacketHistory.TakeLast(400), session.CharacterId,
					id => observed.TryGetValue(id, out var npc) ? runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)?.GetL10n() : null,
					runtime.IsHostileSkill, session.Api.World.Summon?.ObjectId);
				int[] attackers = active
					.Where(id => observed.TryGetValue(id, out var npc) && Distance(session.CurrentPosition, npc.Position) < 30)
					.ToArray();
				int[] pursuers = ObservedPullMonsters()
					.Where(m => !attackers.Contains(m.Npc.ObjectId) && Distance(session.CurrentPosition, m.Npc.Position) < 45)
					// A known walker can travel >10 m from its first SM_NPC_INFO on its normal Java
					// route (NpcMoveController.isNextRouteStepChosen). Displacement alone does not
					// show pursuit; an actual attack or an overlapping aggro circle is checked below.
					.Where(m => m.Npc.PatrolPath is not { Count: > 0 })
					.Where(m => session.PacketHistory.FirstOrDefault(packet => packet.PacketType == typeof(SM_NPC_INFO) &&
						packet.Get<int>("objectId") == m.Npc.ObjectId) is { } info &&
						new BotPosition(info.Get<float>("x"), info.Get<float>("y"), info.Get<float>("z"), 0) is var home &&
						Distance(home, m.Npc.Position) > 10 &&
						Distance(session.CurrentPosition, m.Npc.Position) < Distance(session.CurrentPosition, home))
					.Select(m => m.Npc.ObjectId).ToArray();
				int[] neighbours = ObservedPullMonsters()
					.Where(m => !attackers.Contains(m.Npc.ObjectId) && !pursuers.Contains(m.Npc.ObjectId) &&
						!quietNeighbours.Contains(m.Npc.ObjectId) && m.AggroRadius > 0 &&
						new BotNavigationHazard(m.Npc.Position, m.AggroRadius).Contains(session.CurrentPosition))
					.Select(m => m.Npc.ObjectId).ToArray();
				return (attackers, [.. pursuers, .. neighbours]);
			}

			// Use a quest object and loot its item like a player: wait out the use bar the server starts
			// (SM_USE_OBJECT), and if a monster interrupts it (the closing SM_USE_OBJECT has duration 0), fight
			// what is on the Priest and walk back to use it again. Returns the object that was looted.
			async Task<int> UseAndLootQuestObjectAsync(int templateId, int itemId, bool skipBlockedTarget = false)
			{
				for (int attempt = 1; attempt <= 10; attempt++)
				{
					int objectId = await ApproachShippedSpawnAsync(templateId, skipBlockedTarget);
					long before = ItemCount(session.Api.World, itemId);
					int start = session.PacketHistory.Count;
					await NaturalDialogProtocol.OpenAsync(session, objectId, token);
					await session.SynchronizeAsync(token);
					DecodedBotServerPacket? started = session.PacketHistory.Skip(start)
						.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
							packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") != 2);
					int durationMs = started?.Get<int>("durationMs") ?? 3000;
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(durationMs, 1) + 1), token);
					await session.SynchronizeAsync(token);
					DecodedBotServerPacket? finish = session.PacketHistory.Skip(start)
						.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
							packet.Get<int>("targetObjectId") == objectId && packet.Get<byte>("actionType") == 2);
					bool completed = finish == null || finish.Get<int>("durationMs") > 0;
					if (completed && (await TryLootCorpseItemAsync(session, objectId, itemId, token, start) ||
						ItemCount(session.Api.World, itemId) > before))
						return objectId;
					session.TraceDiagnostic(completed ? "quest-object-without-item" : "quest-object-use-interrupted",
						new Dictionary<string, object?>
					{
						["templateId"] = templateId,
						["objectId"] = objectId,
						["itemId"] = itemId,
						["attempt"] = attempt,
						["durationMs"] = finish?.Get<int>("durationMs"),
						["position"] = session.CurrentPosition,
					});
					if (completed) navigator.UnavailableObjects.Add(objectId); // used up without the item: try another
					else if (!await DefendAgainstEngagedAsync($"quest-object-{templateId}"))
						await RestSafelyAsync(token);
				}
				throw new InvalidDataException($"Quest object {templateId} did not yield item {itemId} after ten uses.");
			}

			// Pull, one at a time, every observed monster whose aggro circle (plus the 2 m assist offset) comes
			// within 20 m of the objective, then walk back to it. Bounded only as a stall guard.
			async Task ClearAroundObjectiveAsync(int objectiveObjectId, string purpose) =>
				await ClearAroundSpotAsync(navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == objectiveObjectId)?.Position,
					objectiveObjectId, purpose);

			// The same, around a position (a shipped spawn hint) before walking onto it: the recorded human pulled the
			// Stalker beside the blue generator from 20 m out first, instead of stepping onto the generator and being
			// jumped there. With an object id, walks back to it afterwards.
			async Task ClearAroundSpotAsync(BotPosition? objective, int? objectiveObjectId, string purpose, IReadOnlyCollection<int>? preservedKinds = null)
			{
				if (objective is not BotPosition spot) return;
				// Clear the camp only once the bot has rejoined it. After a bind revive, old camp NPCs
				// may still be in the client model despite being over a kilometre away.
				if (Distance(session.CurrentPosition, spot) > 80) return;
				bool cleared = false;
				for (int pull = 0; pull < 20 && !session.Api.World.IsDead; pull++)
				{
					// A patrol counts wherever on its observed path it is now: it walks past the objective sooner or later.
					NaturalNavigationObject[] near = ObservedPullMonsters(objectiveObjectId)
						.Where(m => preservedKinds == null || !preservedKinds.Contains(m.Npc.TemplateId))
						.Where(m => m.Npc.PossiblePositions().Any(at =>
							Distance(at, spot) <= m.AggroRadius + NaturalPullPlanner.SupportRangeOffset + 20))
						.OrderBy(m => Distance(m.Npc.Position, session.CurrentPosition))
						.Select(m => m.Npc).ToArray();
					if (near.Length == 0) break;
					session.TraceDiagnostic("objective-clear", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["objective"] = spot,
						["monsters"] = near.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}").ToArray(),
					});
					NaturalPullPlan? plan = await MoveToPullSpotAsync(near, [], purpose);
					if (plan == null || session.Api.World.IsDead) break;
					int revivesBefore = combat.ReviveCount;
					bool killed;
					try { killed = await combat.TryKillAsync(plan.Target.Npc.ObjectId, token, session.CurrentPosition); }
					catch (NaturalCombatApproachBlockedException) { killed = false; }
					if (combat.ReviveCount > revivesBefore) return;
					if (killed) navigator.UnavailableObjects.Add(plan.Target.Npc.ObjectId);
					cleared = true;
					// Respawns take 180 s: rest only when it is needed, so the object gets used inside that window.
					if (combat.ClassProfile.Readiness.BeforeUseBar.RestFirst(session.Api.World))
						await RestSafelyAsync(token);
					else
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				}
				if (cleared && objectiveObjectId != null && !session.Api.World.IsDead)
					await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId,
						navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == objectiveObjectId)?.TemplateId ?? -1,
						spot, navigator, token);
			}

			// Fight what is already on the bot, one at a time, before any new pull. False if the bot died.
			async Task<bool> DefendAgainstEngagedAsync(string purpose)
			{
				// No cap on how many attackers are fought: chain aggro and respawns can keep them coming. Only the
				// wait for pursuers that never arrive is bounded (they gave up and walked home).
				int pursuerWaits = 0, blockedWaits = 0;
				for (int round = 0; round < NaturalJourneyCombat.MaximumCombatActions; round++)
				{
					var (attackers, pursuers) = Engaged();
					if (attackers.Length == 0 && pursuers.Length == 0) return true;
					if (attackers.Length == 0)
					{
						// Let a chaser arrive and commit rather than walking into a fresh pull with it behind us.
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						if (++pursuerWaits >= 3)
						{
							quietNeighbours.UnionWith(pursuers);
							return true; // it turned back (leash/give-up) or never saw the Priest; carry on
						}
						continue;
					}
					pursuerWaits = 0;
					int attacker = ChooseEngagedTarget(attackers, session, mauPolicy.PreferWoundedWhenTwoAttackers);
					session.TraceDiagnostic("defend-before-pull", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["attacker"] = attacker,
						["attackers"] = attackers,
						["pursuers"] = pursuers,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					int revivesBefore = combat.ReviveCount;
					bool killed;
					// BC-06: Engaged found these hits in the recent packet window. Carry that same evidence
					// into combat; starting after the triggering hits falsely describes a pack as zero attackers.
					int? engagementStart = altgardLegId == "l10" ? Math.Max(0, session.PacketHistory.Count - 400) : null;
					try { killed = await combat.TryKillAsync(attacker, token, session.CurrentPosition, engagementStart); }
					catch (NaturalCombatApproachBlockedException) when (
						// CP-40: a walk-in class has no spell to wait for; it takes the answer below. NR-55b: so does every class
						// that walks up to the monster it fights, whatever it pulls with.
						!combat.ClassProfile.Movement.GoesToItsTarget &&
						navigator.Observe().Npcs.Any(npc => npc.ObjectId == attacker &&
							Distance(session.CurrentPosition, npc.Position) <= combat.ClassProfile.Ranges.SpellRange))
					{
						// A ranged aggressor can be in Smite range across a blocked seam. Wait
						// for the spell cooldown instead of ending the whole quest or entering adds.
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						continue;
					}
					catch (NaturalCombatApproachBlockedException exception)
					{
						// AB-08: a retreat left the attacker out of range behind its pack (Sumarhon's camp): no checked way back.
						// Let it come to us, as a pursuer, and when it does not, leave it to the caller's next plan.
						session.TraceDiagnostic("defend-approach-blocked", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["attacker"] = attacker, ["waits"] = blockedWaits, ["reason"] = exception.Message,
							["position"] = session.CurrentPosition,
						});
						if (combat.ReviveCount > revivesBefore || session.Api.World.IsDead) return false;
						if (++blockedWaits >= 3)
						{
							quietNeighbours.Add(attacker);
							return true;
						}
						await session.AdvanceAsync(TimeSpan.FromSeconds(2), token);
						await session.SynchronizeAsync(token);
						continue;
					}
					if (combat.ReviveCount > revivesBefore || session.Api.World.IsDead) return false;
					if (killed) navigator.UnavailableObjects.Add(attacker);
					else return true; // combat decided otherwise (retreat); let the caller re-plan
				}
				throw new InvalidDataException($"{ClassLine.StarterName} could not clear engaged attackers before resting or pulling.");
			}

			async Task LootQuestItemsAroundAsync(CancellationToken lootToken)
			{
				BotWorldModel world = session.Api.World;
				int[] lootable = world.LootStatuses.Where(entry => entry.Value == 0 && !lootSwept.Contains(entry.Key))
					.Select(entry => entry.Key).ToArray();
				foreach (int corpse in lootable)
				{
					if (world.IsDead || Engaged().Attackers.Length > 0) return;
					string? skipped = null;
					if (!world.Objects.TryGetValue(corpse, out BotKnownObject? seen) || seen.Kind != BotKnownObjectKind.Npc)
					{
						lootSwept.Add(corpse); // gone (despawned or out of sight for good)
						skipped = "gone";
					}
					else if (Distance(seen.Position, session.CurrentPosition) > LootSweepReach)
					{
						// A ranged fight can end far from the first kill; leave it for a later sweep unless it is far behind.
						if (Distance(seen.Position, session.CurrentPosition) > 60) lootSwept.Add(corpse);
						skipped = "far";
					}
					else if (Distance(seen.Position, session.CurrentPosition) > 4 && world.MapId is int map &&
						geometry.SnapToGround(map, seen.Position with { Z = seen.Position.Z + 2 }) is { } ground)
					{
						IReadOnlyList<BotPosition> path = geometry.FindLocalPath(map, session.CurrentPosition, ground);
						// NL-4: a room keeper's corpse holds the room's only key and the keeper never respawns (the trial nl4-vs-d5
						// killed Flame Judge Bolcan and left his corpse to despawn for want of a path clear of monsters).
						bool onlyKey = altgardLeg?.InstanceRooms is { } keyRooms && keyRooms.MapId == map &&
							keyRooms.Rooms.Any(room => room.SourceNpcId == seen.TemplateId && ItemCount(world, room.ItemId) < 1);
						if (path.Count == 0 || !onlyKey && !navigator.IsSegmentSafe(path, corpse)) skipped = "no-safe-path"; // try again later
						else
						{
							await navigator.MoveAsync(path, lootToken);
							await session.SynchronizeAsync(lootToken);
						}
					}
					if (skipped != null)
					{
						session.TraceDiagnostic("quest-loot-skip", new Dictionary<string, object?>
						{
							["corpse"] = corpse, ["npcId"] = seen?.TemplateId, ["reason"] = skipped,
							["distance"] = seen == null ? null : Distance(seen.Position, session.CurrentPosition),
						});
						continue;
					}
					lootSwept.Add(corpse);
					// BC-06: several corpses can receive a drop while the bag is below its required count.
					// Check the current stack before each take rather than collecting an extra from an older corpse.
					IReadOnlyList<int> taken = await LootQuestItemsAsync(session, corpse, runtime.Data, lootToken,
						altgardLegId == "l10" ? QuestDropStillNeeded : null,
						altgardLeg?.Haramel?.TowerChestKeys?.Select(key => key.ItemId).ToHashSet());
					session.TraceDiagnostic("quest-loot-sweep", new Dictionary<string, object?>
					{
						["corpse"] = corpse, ["npcId"] = seen!.TemplateId, ["taken"] = taken, ["position"] = session.CurrentPosition,
					});
				}
			}

			async Task RestSafelyAsync(CancellationToken restToken)
			{
				restToken.ThrowIfCancellationRequested();
				if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0)
					await DefendAgainstEngagedAsync("before-rest");
				await combat.RestAsync(restToken);
				// AO-04: a camp fight can defer every kill's loot while another attacker is engaged. Revisit those
				// corpses once recovery settles, before leaving or searching for a drop source already killed as an add.
				if (!session.Api.World.IsDead) await SweepQuestItemsWhenSafeAsync(restToken);
			}

			// Observe and defend while waiting for a patrol to pass instead of leaving its attacks unanswered.
			async Task<bool> WaitBeforePullDefendingAsync(int milliseconds, string purpose)
			{
				long until = runtime.NowMillis + milliseconds;
				while (runtime.NowMillis < until)
				{
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(1000, until - runtime.NowMillis)), token);
					await session.SynchronizeAsync(token);
					if (session.Api.World.IsDead || !await DefendAgainstEngagedAsync(purpose + "-patrol-wait")) return false;
				}
				return true;
			}

			// Pull like a player: choose, among the given targets (earlier ones preferred on ties), the one and the
			// spot that bring the fewest helpers (Java assist rule), at spell range off to the side of the pack,
			// outside every circle; if helpers would still come, wait briefly for patrols to move. Then walk there
			// on a checked, hostile-free route. Returns the target to fight, or null when no spot is reachable.
			// A preference, not a wall: the spot where the Priest died is avoided when any other firing spot
			// works, but when it is the only way in (Rae's camp) the Priest still goes.
			async Task<NaturalPullPlan?> MoveToPullSpotAsync(IReadOnlyList<NaturalNavigationObject> targets,
				IReadOnlyList<BotPosition> stagingPoints, string purpose)
			{
				if (!await DefendAgainstEngagedAsync(purpose)) return null;
				if (combat.ClassProfile.Readiness.BeforePull.RestFirst(session.Api.World)) await RestSafelyAsync(token);
				await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				NaturalPullPlan? plan = null;
				combat.DecidedPullTarget = null;
				// NA-22: the Cleric waits 15 s at a time, up to four times, then decides (NaturalPatrolPolicy); the Priest
				// keeps its baseline of short waits.
				bool holdsForPatrols = combat.ClassProfile.PatrolRule == NaturalPatrolRule.HoldAndAssess;
				int waitCycles = holdsForPatrols ? NaturalPatrolPolicy.MaximumWaits : mauPolicy.PatrolWaitCycles;
				for (int wait = 0; wait <= waitCycles; wait++)
				{
					NaturalPullMonster[] monsters = ObservedPullMonsters();
					var observed = navigator.Observe().Npcs.ToDictionary(npc => npc.ObjectId);
					NaturalPullMonster[] pullTargets = targets
						.Select(t => monsters.FirstOrDefault(m => m.Npc.ObjectId == t.ObjectId) ??
							(observed.TryGetValue(t.ObjectId, out NaturalNavigationObject? seen) ? PullMonsterOf(seen) : null))
						.OfType<NaturalPullMonster>().ToArray();
					if (pullTargets.Length == 0) return null;
					// Walking there only passes the monsters; the planner keeps the spot itself off every patrol's whole path.
					BotNavigationHazard[] hazards = monsters.SelectMany(m => m.Npc.Hazards(m.AggroRadius, BotPatrolPath.PassingReach)).ToArray();
					var reachable = new Dictionary<(int, int), bool>();
					bool avoidDeaths = combat.DeathSpots.Count > 0;
					// Not on a patrol's path or a respawn point (the shipped spawn circles): a fight of a minute or
					// more there meets the patrol or the respawn, as the Hatata fights that went wrong did.
					bool avoidSpawns = combat.HostileSpawns.Count > 0;
					var evaluatedPullCandidates = new List<NaturalPullCandidate>();
					// CP-40: a walk-in class stages outside every circle and fights at the target; its plan's helpers are
					// the adds at the target's position. Every other style keeps the firing-spot plan.
					NaturalPullPlan? PlanOnce() => combat.ClassProfile.PullStyle == NaturalPullStyle.WalkIn
						? NaturalPullPlanner.WalkIn(session.CurrentPosition, pullTargets, monsters, CanSupport,
							(a, b) => geometry.HasLineOfSight(contract.MapId, a, b),
							point => geometry.SnapToGround(contract.MapId, point), PullSpotReachable,
							combat.ClassProfile.Ranges.MeleeReach, audit: evaluatedPullCandidates.Add)
						: PlanFiringSpotOnce();
					// NR-R21a: a spot is reached when the checked route to it ends on it. The ground rule looks two metres up, and under
					// a plank of Haramel's office it took the plank's underside for the ground of the route's last step, 1.85 m over
					// the spot: the walk ended up there, and no route left it.
					static bool EndsOn(IReadOnlyList<BotPosition> route, BotPosition spot) => route.Count > 0 && MathF.Abs(route[^1].Z - spot.Z) <= 1f;
					bool PullSpotReachable(BotPosition spot)
					{
						if (avoidDeaths && combat.DeathSpots.Any(death => Distance(death, spot) < DeathSpotAvoidance)) return false;
						if (avoidSpawns && combat.HostileSpawns.Any(spawn => spawn.DistanceTo(spot) <= spawn.Radius + NaturalPullPlanner.SupportRangeOffset)) return false;
						var key = ((int)MathF.Round(spot.X), (int)MathF.Round(spot.Y));
						if (!reachable.TryGetValue(key, out bool ok))
							reachable[key] = ok = Distance(session.CurrentPosition, spot) < 1 ||
								(geometry.NavMesh is { } router && router.NavMeshes.Get(contract.MapId) is { } mesh
									? mesh.IslandOf(spot) >= 0 && mesh.IslandOf(spot) == mesh.IslandOf(session.CurrentPosition) &&
										EndsOn(router.FindPath(contract.MapId, session.CurrentPosition, spot,
											BotNavQuery.Default with { Hazards = hazards }), spot)
									: EndsOn(geometry.FindJourneyPathAvoiding(contract.MapId, session.CurrentPosition, spot, hazards), spot));
						return ok;
					}
					NaturalPullPlan? PlanFiringSpotOnce() => NaturalPullPlanner.Plan(session.CurrentPosition, pullTargets, monsters, stagingPoints, CanSupport,
						(a, b) => geometry.HasLineOfSight(contract.MapId, a, b),
						point => geometry.SnapToGround(contract.MapId, point),
						spot =>
						{
							var key = ((int)MathF.Round(spot.X), (int)MathF.Round(spot.Y));
							// Navmesh-connected only: a grid shortcut can climb onto a rock top the navmesh keeps
							// as a separate island, stranding the bot for every later route.
							if (avoidDeaths && combat.DeathSpots.Any(death => Distance(death, spot) < DeathSpotAvoidance)) return false;
							if (avoidSpawns && combat.HostileSpawns.Any(spawn => spawn.DistanceTo(spot) <= spawn.Radius + NaturalPullPlanner.SupportRangeOffset)) return false;
							if (!reachable.TryGetValue(key, out bool ok))
								reachable[key] = ok = Distance(session.CurrentPosition, spot) < 1 ||
									(geometry.NavMesh is { } router && router.NavMeshes.Get(contract.MapId) is { } mesh
										? mesh.IslandOf(spot) >= 0 && mesh.IslandOf(spot) == mesh.IslandOf(session.CurrentPosition) &&
											EndsOn(router.FindPath(contract.MapId, session.CurrentPosition, spot,
												BotNavQuery.Default with { Hazards = hazards }), spot)
										: EndsOn(geometry.FindJourneyPathAvoiding(contract.MapId, session.CurrentPosition, spot, hazards), spot));
							return ok;
						}, audit: evaluatedPullCandidates.Add,
						pullDistanceMeters: combat.ClassProfile.PullDistance(mauPolicy));
					plan = PlanOnce();
					if (plan == null && avoidSpawns)
					{
						avoidSpawns = false; // every clean spot is on a patrol path or respawn point: accept one
						plan = PlanOnce();
					}
					if (plan == null && avoidDeaths)
					{
						avoidDeaths = false; // the only way in passes where the Priest died: go anyway
						plan = PlanOnce();
					}
					session.TraceDiagnostic("pull-plan", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["policyVersion"] = combat.ClassProfile.Combat.PolicyVersion(mauPolicy),
						["seed"] = runtime.Seed,
						["candidateScope"] = "spots-evaluated-before-baseline-rank-cutoff",
						["chosenAction"] = plan == null ? "no-plan" :
							plan.Helpers.Count > 0 && wait < waitCycles ? "wait" : "pull",
						["observedState"] = new
						{
							position = session.CurrentPosition,
							targets = pullTargets.Select(target => new
							{
								target.Npc.ObjectId, target.Npc.TemplateId, target.Npc.Position,
								target.AggroRadius, target.Tribe, target.BoundRadius,
							}).ToArray(),
							monsters = monsters.Select(monster => new
							{
								monster.Npc.ObjectId, monster.Npc.TemplateId, monster.Npc.Position,
								monster.AggroRadius, monster.Tribe, monster.BoundRadius,
								possiblePositions = monster.Npc.PossiblePositions().ToArray(),
							}).ToArray(),
							avoidDeaths, avoidSpawns,
						},
						["candidateActions"] = evaluatedPullCandidates.Select(candidate => new
						{
							candidate.TargetObjectId, candidate.FiringPosition,
							candidate.ExpectedHelperObjectIds, candidate.ClearanceFromOthers,
							candidate.Legal, candidate.IllegalReason,
							BaselineRejection = !candidate.Legal ? candidate.IllegalReason :
								plan != null && plan.Helpers.Count > 0 && wait < waitCycles ?
								"Baseline waits for helpers to move." :
								plan?.Target.Npc.ObjectId == candidate.TargetObjectId &&
								plan.FiringPosition == candidate.FiringPosition ? null :
								"Baseline chose a better-ranked evaluated pull.",
						}).ToArray(),
						["wait"] = wait,
						["position"] = session.CurrentPosition,
						["candidates"] = pullTargets.Select(t => $"{t.Npc.TemplateId}/{t.Npc.ObjectId}").ToArray(),
						["target"] = plan == null ? null : $"{plan.Target.Npc.TemplateId}/{plan.Target.Npc.ObjectId}",
						["firingPosition"] = plan?.FiringPosition,
						["expectedHelpers"] = plan?.Helpers.Select(h => $"{h.Npc.TemplateId}/{h.Npc.ObjectId}").ToArray(),
						["clearance"] = plan?.ClearanceFromOthers,
					});
					if (plan == null || plan.Helpers.Count == 0) break;
					if (holdsForPatrols)
					{
						NaturalPatrolDecision patrol = NaturalPatrolPolicy.Decide(ObservePatrol(plan, wait));
						session.TraceDiagnostic("patrol-decision", new Dictionary<string, object?>
						{
							["purpose"] = purpose, ["action"] = patrol.Action, ["wait"] = wait, ["winnable"] = patrol.Winnable,
							["reason"] = patrol.Reason, ["assessment"] = patrol.Assessment,
							["helpers"] = plan.Helpers.Select(h => $"{h.Npc.TemplateId}/{h.Npc.ObjectId}").ToArray(),
						});
						if (patrol.Action == "reroute") return null;
						if (patrol.Action != "wait")
						{
							// NR-55b: fight, or pull anyway. The decision goes with the pull into its fight, where a class
							// that walks up would otherwise refuse the walk into the pack it has just decided to take.
							combat.DecidedPullTarget = plan.Target.Npc.ObjectId;
							break;
						}
						// NL-4: a mustered leg defends itself while it holds, too (the trial nl4-vs-d2 held for a minute beside two elites
						// that were hitting it, and died).
						if (altgardLegId == "l10" || altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId))
						{
							if (!await WaitBeforePullDefendingAsync(patrol.WaitMillis, purpose)) return null;
						}
						else
						{
							await session.AdvanceAsync(TimeSpan.FromMilliseconds(patrol.WaitMillis), token);
							await session.SynchronizeAsync(token);
						}
						continue;
					}
					if (wait == waitCycles) break;
					// A helper stands in range: patrols move, so give it a few seconds before accepting a chain pull.
					await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
					await session.SynchronizeAsync(token);
				}
				if (plan == null) return null;
				if (Distance(session.CurrentPosition, plan.FiringPosition) > 1.5f)
				{
					BotNavigationHazard[] hazards = ObservedPullMonsters()
						.SelectMany(m => m.Npc.Hazards(m.AggroRadius, BotPatrolPath.PassingReach)).ToArray();
					IReadOnlyList<BotPosition> approach = geometry.FindJourneyPathAvoiding(contract.MapId,
						session.CurrentPosition, plan.FiringPosition, hazards);
					if (approach.Count == 0) return null;
					foreach (BotPosition[] chunk in approach.Chunk(8))
					{
						if (!navigator.IsSegmentSafe(chunk, plan.Target.Npc.ObjectId)) return null; // new hostile: re-plan
						await navigator.MoveAsync(chunk, token);
						await navigator.SynchronizeAsync(token);
						if (session.Api.World.IsDead) return null;
					}
				}
				return plan;
			}

			// NA-22: the client's view of a blocked pull for the Cleric's patrol decision.
			NaturalPatrolObservation ObservePatrol(NaturalPullPlan plan, int completedWaits)
			{
				BotWorldModel world = session.Api.World;
				NaturalClassProfile profile = combat.ClassProfile;
				bool Learned(string role, out NaturalPriestSkill? skill)
				{
					skill = NaturalPriestSkills.Best(role, world.Level, world.Skills, profile.Skills);
					return skill != null;
				}
				// NR-37: the recovery the class's own fight table names; a role it does not have is not ready.
				NaturalPatrolView view = profile.Patrol;
				NaturalPriestSkill? healSkill = null, hotSkill = null, salvationSkill = null;
				bool heal = view.HealRole != null && Learned(view.HealRole, out healSkill) && world.CurrentMp >= healSkill!.ManaCost;
				bool hot = view.HealOverTimeRole != null && Learned(view.HealOverTimeRole, out hotSkill) && world.CurrentMp >= hotSkill!.ManaCost;
				bool salvation = view.RescueRole != null && Learned(view.RescueRole, out salvationSkill) && world.CurrentDp >= salvationSkill!.DpCost;
				var effects = world.VisibleEffects ?? [];
				bool buffs = profile.Upkeep.All(buff => effects.Any(effect => profile.EffectIds(buff.Role).Contains(effect.SkillId))) &&
					effects.Any(effect => NaturalHelpItemPolicy.All.Any(item => item.SkillId == effect.SkillId &&
						item.EffectSlot == NaturalHelpItemPolicy.AwakeningSlot));
				int[] levels = plan.Helpers.Prepend(plan.Target)
					.Select(member => (int)(runtime.Data.NpcDataDh.GetNpcTemplate(member.Npc.TemplateId)?.GetLevel() ?? 0)).ToArray();
				// Rerouting through another corridor belongs to the route planner; the pull planner already chose the
				// spot with the fewest helpers, so here no other way is known.
				return new(profile.PatrolRule == NaturalPatrolRule.HoldAndAssess, completedWaits, world.Level, world.CurrentHp, world.MaxHp, world.CurrentMp, world.MaxMp,
					levels, heal, hot, salvation, buffs, NaturalIshalgenPotionPolicy.TotalHealingCount(world.Inventory.Values),
					RerouteAvailable: false, View: view);
			}

			// Fight your way in: when observed monsters close every hostile-free route, take the route that fights
			// the least, walk its hostile-free prefix to a firing point just outside the first circle it enters,
			// and pull that one monster with ordinary combat. True after a kill (the caller re-observes and
			// re-plans); false when there is no terrain route or no safe pull.
			async Task<bool> TryFightThroughAsync(BotPosition objective, int? objectiveObjectId, HashSet<int> rejected)
			{
				int travelRevives = combat.ReviveCount;
				bool LostTravel() => session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount != travelRevives;
				// Never walk on with monsters on the Priest (a fight given up still has its attacker): fight them first.
				if (!await DefendAgainstEngagedAsync("fight-through") || LostTravel()) return false;
				float AggroRadius(int templateId)
				{
					var template = runtime.Data.NpcDataDh.GetNpcTemplate(templateId);
					return runtime.IsAggressive(template) ? template.GetAggroRange() + 1f : 0f;
				}
				NaturalObservedMonster[] monsters = navigator.Observe().Npcs
					.Where(npc => npc.ObjectId != objectiveObjectId)
					.Select(npc => new NaturalObservedMonster(npc, AggroRadius(npc.TemplateId)))
					.Where(monster => monster.Radius > 0).ToArray();
				BotPosition origin = session.CurrentPosition;
				BotNavigationHazard[] fightHazards = monsters
					.SelectMany(m => m.Npc.Hazards(m.Radius, BotPatrolPath.PassingReach)).ToArray();
				IReadOnlyList<BotPosition> fightRoute = geometry.FindFightThroughPath(contract.MapId, origin,
					objective, fightHazards);
				if (fightRoute.Count == 0 && Distance(origin, objective) > 30)
				{
					// A long destination can exceed the local fight-through search while the next monster is
					// quite reachable. Plan to an observed forward guard first, then re-plan toward the objective.
					foreach (NaturalObservedMonster guard in monsters
						.Where(m => Distance(origin, m.Npc.Position) <= 65 &&
								Distance(m.Npc.Position, objective) < Distance(origin, objective) - 3)
						.OrderBy(m => Distance(origin, m.Npc.Position)).Take(12))
					{
						IReadOnlyList<BotPosition> leg = geometry.FindFightThroughPath(contract.MapId,
							origin, guard.Npc.Position, fightHazards);
						if (leg.Count == 0) continue;
						fightRoute = leg;
						session.TraceDiagnostic("fight-through-local-guard", new Dictionary<string, object?>
						{
							["objective"] = objective, ["guard"] = $"{guard.Npc.TemplateId}/{guard.Npc.ObjectId}",
							["routePoints"] = leg.Count, ["position"] = origin,
						});
						break;
					}
				}
				if (fightRoute.Count == 0)
				{
					// If even the nearby guard routes fail, take a short terrain-checked probe toward the
					// objective. Treat aggro as a cost (at most two new circles), defend after every two
					// points, and re-plan from the resulting client position. This is not a teleport or
					// permission to cross an unwalkable navmesh edge.
					var probes = geometry.GroundAround(contract.MapId, origin, [12f, 24f, 36f], 16)
						.Where(point => Distance(point, objective) < Distance(origin, objective) - 3 &&
							geometry.OnSameIsland(contract.MapId, origin, point))
						.OrderBy(point => Distance(point, objective)).Take(24)
						.Select(point => geometry.FindLocalPath(contract.MapId, origin, point).Take(6).ToArray())
						.Where(path => path.Length > 0 && Distance(origin, path[^1]) >= 2)
						.Select(path => new
						{
							Path = path,
							NewHazards = fightHazards.Count(hazard => !hazard.Contains(origin) &&
								path.Any(hazard.Contains)),
							Progress = Distance(origin, objective) - Distance(path[^1], objective),
						})
						.Where(probe => probe.NewHazards <= 2 && probe.Progress > 1)
						.OrderBy(probe => probe.NewHazards).ThenByDescending(probe => probe.Progress)
						.FirstOrDefault();
					if (probes == null) return false;
					session.TraceDiagnostic("fight-through-terrain-probe", new Dictionary<string, object?>
					{
						["objective"] = objective, ["from"] = origin, ["toward"] = probes.Path[^1],
						["newHazards"] = probes.NewHazards, ["routePoints"] = probes.Path.Length,
					});
					foreach (BotPosition[] segment in probes.Path.Chunk(2))
					{
						await navigator.MoveAsync(segment, token);
						await navigator.SynchronizeAsync(token);
						if (LostTravel() || !await DefendAgainstEngagedAsync("fight-through-terrain-probe") || LostTravel())
							return false;
					}
					return Distance(origin, session.CurrentPosition) >= 2;
				}
				IReadOnlyList<NaturalObservedMonster> orderedBlockers =
					NaturalFightThrough.BlockersInOrder(session.CurrentPosition, fightRoute, monsters);
				// The first circle on this route must be cleared first. Skipping a refused
				// Stalker to pull a patrol behind it puts both onto the Priest.
				if (orderedBlockers.Count > 0 && rejected.Contains(orderedBlockers[0].Npc.ObjectId))
					return false;
				NaturalFightThroughBlocker? next = fightRoute.Count == 0 ? null
					: NaturalFightThrough.SelectNext(session.CurrentPosition, fightRoute, monsters, rejected,
						combat.ClassProfile.Ranges.FiringRange);
				session.TraceDiagnostic("fight-through-plan", new Dictionary<string, object?>
				{
					["objective"] = objective,
					["position"] = session.CurrentPosition,
					["routePoints"] = fightRoute.Count,
					["blockers"] = orderedBlockers
						.Select(m => $"{m.Npc.TemplateId}/{m.Npc.ObjectId}").ToArray(),
					["next"] = next == null ? null : $"{next.Monster.Npc.TemplateId}/{next.Monster.Npc.ObjectId}",
					["firingPosition"] = next?.FiringPosition,
				});
				fightRouteOpen = fightRoute.Count > 0 && orderedBlockers.Count == 0;
				NaturalNavigationObject? walkBlocker = null;
				if (next == null)
				{
					// The walk below can end within arm's reach of the objective (2.8 m from Munin on his platform),
					// where no route of any kind is left to plan: report success so the navigator confirms arrival.
					if (Distance(session.CurrentPosition, objective) <= 3f) return true;
					// The strict router refused (a circle's clearance margin closes the last metres) but the
					// checked fight-through route enters no circle: walk it, segment by segment, as a player would.
					if (fightRouteOpen && Distance(session.CurrentPosition, objective) > 3 &&
						(fightRouteOpenAt is not BotPosition walked || Distance(walked, session.CurrentPosition) > 2))
					{
						fightRouteOpenAt = session.CurrentPosition;
						session.TraceDiagnostic("fight-through-walk", new Dictionary<string, object?>
						{
							["objective"] = objective, ["position"] = session.CurrentPosition, ["routePoints"] = fightRoute.Count,
						});
						foreach (BotPosition[] segment in fightRoute.Chunk(8))
						{
							if (!navigator.IsSegmentSafe(segment, objectiveObjectId))
							{
								// The segment brushes a circle the plan did not count (the margin, not the circle). A
								// player kills that monster and walks on: pull whichever one the segment touches.
								walkBlocker = monsters
									.Where(m => !rejected.Contains(m.Npc.ObjectId) && segment.Any(point =>
										// Horizontal: the server's aggro range ignores height (a peon under a deck still aggroes).
										m.Npc.PossiblePositions(BotPatrolPath.PassingReach).Any(at => MathF.Sqrt((point.X - at.X) * (point.X - at.X) +
											(point.Y - at.Y) * (point.Y - at.Y)) < m.Radius + 2)))
									.OrderBy(m => Distance(session.CurrentPosition, m.Npc.Position))
									.Select(m => m.Npc).FirstOrDefault();
								break;
							}
							await navigator.MoveAsync(segment, token);
							await navigator.SynchronizeAsync(token);
							if (LostTravel())
							{
								session.TraceDiagnostic("fight-through-walk-ended-after-death", new Dictionary<string, object?>
								{
									["objective"] = objective, ["position"] = session.CurrentPosition,
									["startingRevives"] = travelRevives, ["revives"] = combat.ReviveCount,
								});
								return false;
							}
						}
						if (walkBlocker == null) return true;
					}
					else return false;
				}
				// Pull the first monster the route meets before advancing into later circles.
				NaturalNavigationObject[] blockers = walkBlocker != null ? [walkBlocker]
					: next == null ? [] : [next.Monster.Npc];
				NaturalPullPlan? pull = await MoveToPullSpotAsync(blockers, next?.Staging ?? [], "fight-through");
				if (LostTravel()) return false;
				// CP-40: a walk-in class reaches what it can walk to from its staging spot, the fight-through's pull range;
				// every other style reaches what its spell does.
				float reach = combat.ClassProfile.PullStyle == NaturalPullStyle.WalkIn
					? combat.ClassProfile.Ranges.FightThroughPullRange : combat.ClassProfile.Ranges.SpellRange + 3;
				bool OutOfReach(NaturalNavigationObject npc) =>
					Distance(session.CurrentPosition, npc.Position) > reach ||
					!geometry.HasLineOfSight(contract.MapId, session.CurrentPosition, npc.Position);
				NaturalNavigationObject? target;
				if (pull == null)
				{
					// No clean firing spot exists (a dense camp): a player fights the blocker in front of them from
					// where they stand rather than giving up. Take the nearest blocker already in reach.
					target = blockers.Select(b => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == b.ObjectId))
						.OfType<NaturalNavigationObject>().Where(npc => !OutOfReach(npc))
						.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).FirstOrDefault();
					if (target == null)
					{
						// Nothing in reach or in sight from here (the Methu nest hides the Mau 8.6 m away): walk the
						// checked fight-through route toward the nearest blocker until it is in spell range and sight,
						// as a melee player closes on the monster in front of them. An attack on the way is fought
						// through the navigator's own defence.
						NaturalNavigationObject? advance = blockers
							.Select(b => navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == b.ObjectId))
							.OfType<NaturalNavigationObject>()
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position)).FirstOrDefault();
						float PullRange = combat.ClassProfile.Ranges.FightThroughPullRange;
						if (advance != null && fightRoute.Count > 1)
						{
							int closest = Enumerable.Range(0, fightRoute.Count)
								.OrderBy(i => Distance(fightRoute[i], advance.Position)).ThenBy(i => i).First();
							session.TraceDiagnostic("fight-through-advance", new Dictionary<string, object?>
							{
								["target"] = $"{advance.TemplateId}/{advance.ObjectId}",
								["distance"] = Distance(session.CurrentPosition, advance.Position),
								["routePoints"] = closest + 1,
								["position"] = session.CurrentPosition,
							});
							bool far = Distance(session.CurrentPosition, advance.Position) > PullRange;
							foreach (BotPosition[] pair in fightRoute.Take(closest + 1).Skip(1).Chunk(2))
							{
								await navigator.MoveAsync(pair, token);
								await navigator.SynchronizeAsync(token);
								if (LostTravel()) return false;
								target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == advance.ObjectId);
								if (target == null || !OutOfReach(target)) break;
								// Started far away: stop once within pull range and plan a proper pull from here.
								if (far && Distance(session.CurrentPosition, target.Position) <= PullRange) return true;
							}
						}
						if (target == null || OutOfReach(target))
						{
							rejected.Add(walkBlocker?.ObjectId ?? next?.Monster.Npc.ObjectId ?? advance?.ObjectId ?? -1);
							return true; // re-plan: nothing reachable to pull from here now
						}
					}
					session.TraceDiagnostic("fight-through-stand", new Dictionary<string, object?>
					{
						["target"] = $"{target.TemplateId}/{target.ObjectId}",
						["distance"] = Distance(session.CurrentPosition, target.Position),
						["position"] = session.CurrentPosition,
					});
				}
				else
					target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == pull.Target.Npc.ObjectId);
				// A patrolling monster walks on while the bot walks to its firing spot: follow it and plan the
				// pull again, as a player does, rather than giving up on it.
				for (int chase = 1; target != null && chase <= 3 && OutOfReach(target) && !LostTravel(); chase++)
				{
					session.TraceDiagnostic("pull-target-moved", new Dictionary<string, object?>
					{
						["target"] = $"{target.TemplateId}/{target.ObjectId}",
						["chase"] = chase,
						["distance"] = Distance(session.CurrentPosition, target.Position),
						["position"] = session.CurrentPosition,
					});
					NaturalPullPlan? again = await MoveToPullSpotAsync([target], [], "fight-through-chase");
					if (LostTravel()) return false;
					if (again == null) break;
					target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == again.Target.Npc.ObjectId);
				}
				if (target == null) return true; // it moved away or despawned: re-plan from here
				// NR-R8c: the plan left the bot where it stands, and from there the blocker is farther than the class reaches.
				// The planner measures on the map and the reach is measured through the air: on the ledge above Q2246's
				// insignia box a Blackened Angolem is 14.6 m off on the map and 17.6 m away, and a class with the Templar's
				// ranges reaches 17. Planning again answers the same spot. Walk the checked fight-through route toward the
				// blocker until it is in reach, as when no spot was found. A blocker in reach but out of sight is left as before.
				if (Distance(session.CurrentPosition, target.Position) > reach && fightRoute.Count > 1 &&
					Distance(session.CurrentPosition, fightRoute[0]) < 3)
				{
					int blocker = target.ObjectId;
					int nearest = Enumerable.Range(0, fightRoute.Count)
						.OrderBy(i => Distance(fightRoute[i], target.Position)).ThenBy(i => i).First();
					session.TraceDiagnostic("fight-through-close-in", new Dictionary<string, object?>
					{
						["target"] = $"{target.TemplateId}/{target.ObjectId}",
						["distance"] = Distance(session.CurrentPosition, target.Position),
						["reach"] = reach,
						["routePoints"] = nearest + 1,
						["position"] = session.CurrentPosition,
					});
					foreach (BotPosition[] pair in fightRoute.Take(nearest + 1).Skip(1).Chunk(2))
					{
						await navigator.MoveAsync(pair, token);
						await navigator.SynchronizeAsync(token);
						if (LostTravel()) return false;
						target = navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == blocker);
						if (target == null || !OutOfReach(target)) break;
					}
					if (target == null) return true;
				}
				if (OutOfReach(target))
				{
					rejected.Add(target.ObjectId);
					return true;
				}
				int revivesBefore = combat.ReviveCount;
				try
				{
					bool killed = await combat.TryKillAsync(target.ObjectId, token, session.CurrentPosition);
					if (combat.ReviveCount > revivesBefore) return false;
					if (!killed) { rejected.Add(target.ObjectId); return true; }
					navigator.UnavailableObjects.Add(target.ObjectId);
					session.TraceDiagnostic("fight-through-cleared", new Dictionary<string, object?>
					{
						["objective"] = objective,
						["cleared"] = $"{target.TemplateId}/{target.ObjectId}",
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					if (!await DefendAgainstEngagedAsync("fight-through-cleared")) return false;
					await combat.RestAsync(token);
					return combat.ReviveCount == revivesBefore;
				}
				catch (NaturalCombatApproachBlockedException exception)
				{
					session.TraceDiagnostic("fight-through-pull-blocked", new Dictionary<string, object?>
					{
						["blocker"] = target.ObjectId,
						["reason"] = exception.Message,
					});
					rejected.Add(target.ObjectId);
					return combat.ReviveCount == revivesBefore;
				}
			}

			async Task<bool> TryClearObservedBlockerAsync(BotPosition objective, int? objectiveObjectId = null)
			{
				int clearRevives = combat.ReviveCount;
				bool LostClear() => session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount != clearRevives;
				var rejected = new HashSet<int>();
				// First the route that fights the least, pulled in order; then the older corridor heuristics.
				for (int pull = 0; pull < 3; pull++)
				{
					// A fight-through walk can end at the objective itself: hand back so the navigator confirms arrival.
					if (Distance(session.CurrentPosition, objective) <= 3f) return true;
					int killsBefore = navigator.UnavailableObjects.Count;
					bool advanced = await TryFightThroughAsync(objective, objectiveObjectId, rejected);
					// A nested move can defend and revive while synchronizing. Do not let
					// the corridor fallback resume that camp route from the bind point.
					if (LostClear()) return false;
					if (!advanced)
					{
						// Nothing blocks now: walk again. Only once per spot, though; if the walk fails again from
						// the same place, the navigator sees something the fight-through plan does not.
						if (fightRouteOpen && !session.Api.World.IsDead &&
							(fightRouteOpenAt is not BotPosition last || Distance(last, session.CurrentPosition) > 2))
						{
							fightRouteOpenAt = session.CurrentPosition;
							return true;
						}
						break;
					}
					if (navigator.UnavailableObjects.Count > killsBefore ||
						Distance(session.CurrentPosition, objective) <= 3f) return true;
				}
				// A pack can block every checked detour even when a side guard's own
				// circle misses the straight objective line. Try the direct corridor
				// first, then a bounded twenty-metre shoulder of observed monsters.
				for (int corridorPass = 0; corridorPass < 3; corridorPass++)
				for (int attempt = 0; attempt < 4; attempt++)
				{
					float AggroRadius(int templateId)
					{
						var template = runtime.Data.NpcDataDh.GetNpcTemplate(templateId);
						return runtime.IsAggressive(template)
							? template.GetAggroRange() + 1f : 0f;
					}
					// A bind revive can leave distant camp NPCs in the client model until its known list
					// catches up. Only a nearby guard can be pulled from this position.
					IReadOnlyList<NaturalNavigationObject> observed = navigator.Observe().Npcs
						.Where(npc => Distance(session.CurrentPosition, npc.Position) <= 80).ToArray();
					float corridorMargin = corridorPass == 0 ? 0 : 20;
					NaturalNavigationObject? blocker = corridorPass == 2
						? NaturalGuardedObjectivePolicy.SelectBlockerOnRoute(session.CurrentPosition,
							geometry.FindInteractionPath(contract.MapId, session.CurrentPosition, objective),
							observed, AggroRadius, objectiveObjectId, rejected)
						: NaturalGuardedObjectivePolicy.SelectBlocker(session.CurrentPosition,
							objective, observed, AggroRadius, objectiveObjectId, rejected, corridorMargin);
					if (blocker == null) break;
					session.TraceDiagnostic("guarded-objective-clear", new Dictionary<string, object?>
					{
						["objective"] = objective,
						["blockerTemplateId"] = blocker.TemplateId,
						["blockerObjectId"] = blocker.ObjectId,
						["corridorSelection"] = corridorPass == 2 ? "checked-route" : "straight",
						["corridorMargin"] = corridorPass == 2 ? null : corridorMargin,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					BotPosition refuge = session.CurrentPosition;
					int revivesBefore = combat.ReviveCount;
					try
					{
						bool killed = await combat.TryKillAsync(blocker.ObjectId, token, refuge);
						if (combat.ReviveCount > revivesBefore) return false;
						if (killed)
						{
							navigator.UnavailableObjects.Add(blocker.ObjectId);
							return true;
						}
					}
					catch (NaturalCombatApproachBlockedException exception)
					{
						// The guard is real, but a newly observed pack may make its
						// checked spell-range approach unsafe. Try another observed guard
						// or a different shipped objective; never cross the hazard circle.
						session.TraceDiagnostic("guard-approach-blocked", new Dictionary<string, object?>
						{
							["blockerObjectId"] = blocker.ObjectId,
							["position"] = session.CurrentPosition,
							["reason"] = exception.Message,
						});
					}
					if (combat.ReviveCount > revivesBefore) return false;
					rejected.Add(blocker.ObjectId);
				}
				return false;
			}

			async Task WalkEasternRoadToDerotAsync()
			{
				// CP-07: bound at the outpost, a revive or a Return lands beside Derot. The road is for a bot that landed at the
				// map's first spawn point or at the village; one already at the outpost only walks the last metres.
				if (session.Api.World.MapId == contract.MapId &&
					Distance(session.CurrentPosition, IshalgenObeliskPosition(IshalgenOutpostObelisk)) <= IshalgenOutpostRadius)
				{
					session.TraceDiagnostic("eastern-road-skipped-at-outpost", new Dictionary<string, object?> { ["position"] = session.CurrentPosition });
					await ApproachShippedSpawnAsync(203539);
					return;
				}
				BotPosition roadStart = session.CurrentPosition;
				int historyStart = navigator.Events.Count;
				if (Distance(session.CurrentPosition, new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 80)
				{
					// Ordinary Return/revive lands at the bind point, not at the road's
					// Nobekk entrance. Rejoin through NPCs the Priest already visited;
					// each interaction leg is collision-checked against current hazards.
					await ApproachShippedSpawnAsync(203504); // Vandar
					await ApproachShippedSpawnAsync(203501); // Guheitun
					await ApproachShippedSpawnAsync(203502); // Vanar
					await ApproachShippedSpawnAsync(203516); // Ulgorn
					await ApproachShippedSpawnAsync(203518); // Boromer
				}
				await ApproachShippedSpawnAsync(203519);
				await ApproachShippedSpawnAsync(203534);
				await ApproachShippedSpawnAsync(790002);
				await ApproachShippedSpawnAsync(203535);
				await ApproachShippedSpawnAsync(203539);
				if (easternRoadIngressStart == null)
				{
					// This route was actually walked from Ulgorn's side during Q2003.
					// Save its client-estimated progress, not the merely planned A* path,
					// so later returns can check each reverse leg against current terrain
					// and observed hostiles instead of taking the dead-end valley shortcut.
					easternRoadIngressStart = roadStart;
					easternRoadIngress = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
						navigator.Events.Skip(historyStart));
					session.TraceDiagnostic("eastern-road-learned", new Dictionary<string, object?>
					{
						["origin"] = roadStart,
						["checkpoints"] = easternRoadIngress.Length,
						["position"] = session.CurrentPosition,
					});
				}
			}

			async Task<bool> RevivedInsteadOfReturnAsync()
			{
				if (!session.Api.World.IsDead && session.Api.World.CurrentHp > 0) return false;
				session.TraceDiagnostic("natural-return-dead-revive-at-bind", new Dictionary<string, object?>
				{
					["position"] = session.CurrentPosition, ["hp"] = session.Api.World.CurrentHp,
				});
				await RestSafelyAsync(token); // revives at the bound obelisk, then rests
				return true;
			}

			async Task UseLearnedReturnToBindAsync()
			{
				// Java ReturnEffect uses the obelisk's map, including a city delivery's trip back to Altgard.
				int returnMap = session.Api.World.ObeliskBindPoint?.MapId ?? contract.MapId;
				// Java ce54b7931 ReturnEffect moves the player to the character's
				// bind location. Skill 243 is auto-learned at level 1 for all classes.
				// This is an ordinary player cast, not a setup or GM teleport.
				const int returnSkillId = 243;
				Require.True(session.Api.World.Skills.TryGetValue(returnSkillId, out BotSkill? learned),
					$"The {ClassLine.StarterName} did not observe the auto-learned Return skill.");
				// CP-07: with a bind at an Ishalgen hub a fallback can fire beside the bound obelisk. Return would land where
				// the bot stands, which is no way out of anything, so it is not cast. Ishalgen only: on every other map the
				// helper is what it was.
				if (session.Api.World.MapId == contract.MapId && session.Api.World.ObeliskBindPoint is { MapId: 220010000 } beside &&
					Distance(session.CurrentPosition, beside.Position) <= 30)
				{
					session.TraceDiagnostic("natural-return-skipped-at-bind", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition, ["bindPosition"] = beside.Position,
					});
					return;
				}
				// NR-R11c: on any map, a bot that stands on its bind point itself has nowhere to Return to. A leg played again from
				// its end starts there (the Spirit Master's leg 9 ends with a Return), and Return still cools down from that first
				// time: the bot sat the 19 minutes out and cast into the cooldown's last second, which the login list gives in whole
				// seconds only.
				if (session.Api.World.ObeliskBindPoint is { } here && here.MapId == session.Api.World.MapId &&
					Distance(session.CurrentPosition, here.Position) <= 1)
				{
					session.TraceDiagnostic("natural-return-skipped-on-bind-point", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition, ["bindPosition"] = here.Position,
					});
					return;
				}
				session.BeginStep("ni07-natural-return", "cast-learned-return-after-checked-route-blocked");
				// NA-27 (LIVE): a monster can kill the bot on the very walk whose failure asked for Return, and Java refuses
				// a dead player's cast (CM_CASTSPELL: STR_SKILL_CANT_CAST, DEAD). A bind revive lands at the same bind
				// point Return goes to, so it replaces the cast; the death is recorded, not failed (OD-12).
				if (await RevivedInsteadOfReturnAsync()) return;
				// NR-54b: a fight lost while Return cools down ends in a bind revive too, inside the defence. That revive
				// stands at the bind point Return goes to, so it replaces the cast in the same way.
				int bindRevivesBefore = combat.BindReviveCount;
				bool RevivedAtBindMeanwhile()
				{
					if (combat.BindReviveCount == bindRevivesBefore) return false;
					session.TraceDiagnostic("natural-return-replaced-by-bind-revive", new Dictionary<string, object?>
					{
						["position"] = session.CurrentPosition, ["bindRevives"] = combat.BindReviveCount - bindRevivesBefore,
					});
					return true;
				}
				// A checked route can fail again soon after a prior Return. Honor the
				// SM_CASTSPELL_RESULT cooldown while staying ready to fight nearby monsters.
				TimeSpan remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId);
				if (remainingCooldown > TimeSpan.FromSeconds(5))
				{
					session.TraceDiagnostic("natural-return-cooldown-wait", new Dictionary<string, object?>
					{
						["remainingMillis"] = remainingCooldown.TotalMilliseconds,
						["position"] = session.CurrentPosition,
					});
					while ((remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId)) > TimeSpan.FromSeconds(5))
					{
						await DefendAgainstEngagedAsync("natural-return-cooldown");
						if (session.Api.World.IsDead ||
							session.Api.World.CurrentHp < session.Api.World.MaxHp * combat.ClassProfile.Campaign.ReturnCooldownHpFraction)
							await RestSafelyAsync(token);
						remainingCooldown = session.Api.Timing.TimeUntilCast(returnSkillId);
						if (remainingCooldown <= TimeSpan.FromSeconds(5)) break;
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Min(5000, remainingCooldown.TotalMilliseconds)), token);
						await session.SynchronizeAsync(token);
					}
				}
				if (RevivedAtBindMeanwhile()) return;
				BotPosition origin = session.CurrentPosition;
				int packetStart;
				DecodedBotServerPacket result;
				// A monster that reaches the bot during the cast interrupts it (Java Skill.cancelCast on damage); a
				// player fights it off, recovers and casts again.
				for (int attempt = 1; ; attempt++)
				{
					if (await RevivedInsteadOfReturnAsync() || RevivedAtBindMeanwhile()) return;
					packetStart = session.PacketHistory.Count;
					TimeSpan castGate = session.Api.Timing.TimeUntilCast(returnSkillId);
					if (castGate > TimeSpan.Zero) await session.AdvanceAsync(castGate + TimeSpan.FromMilliseconds(1), token);
					await session.SendPacketAsync(session.Api.Target(session.CharacterId), token);
					await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
						session.CurrentPosition, returnSkillId, checked((byte)learned!.Level), session.CharacterId)), token);
					DecodedBotServerPacket started = await BotCastProtocol.WaitForStartAsync(
						(predicate, waitToken) => session.WaitForPacketAsync(packet => predicate(packet) ||
							BotCastProtocol.IsStartRejection(packet), waitToken),
						session.CharacterId, returnSkillId, token);
					Require.Equal(typeof(SM_CASTSPELL), started.PacketType);
					IReadOnlyList<BotKnownObject> visibleBefore = session.Api.World.SnapshotObjects();
					session.Api.World.BeginWorldReload();
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(started.Get<ushort>("castDuration") + 1), token);
					result = await BotCastProtocol.WaitForCompletionAsync(
						session.WaitForPacketAsync, session.CharacterId, returnSkillId, token);
					if (result.PacketType == typeof(SM_CASTSPELL_RESULT)) break;
					Require.Equal(typeof(SM_SKILL_CANCEL), result.PacketType);
					Require.True(attempt < 4, "Return was interrupted four times in a row.");
					session.Api.World.RestoreObjects(visibleBefore);
					session.TraceDiagnostic("natural-return-interrupted", new Dictionary<string, object?>
					{
						["attempt"] = attempt,
						["hp"] = session.Api.World.CurrentHp,
						["position"] = session.CurrentPosition,
					});
					await session.SynchronizeAsync(token);
					await DefendAgainstEngagedAsync("natural-return");
					await RestSafelyAsync(token); // revives when the defence was lost, rests otherwise
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(BotCastProtocol.ReactionMillis), token);
				}
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(result.Get<ushort>("hitTime") + 1), token);
				await session.SynchronizeAsync(token);
				lastReturnMillis = runtime.NowMillis;
				// AK-Q4: a Return to a bind on another map (Pandaemonium to Basfelt) spawns the player on the new map first;
				// SM_PLAYER_INFO follows once the client has entered it, as after a teleporter's map change.
				if (session.PacketHistory.Skip(packetStart).Any(packet => packet.PacketType == typeof(SM_PLAYER_SPAWN)) &&
					!session.PacketHistory.Skip(packetStart).Any(packet => packet.PacketType == typeof(SM_PLAYER_INFO) &&
						packet.Get<int>("objectId") == session.CharacterId))
				{
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token, packet => packet.Get<int>("objectId") == session.CharacterId);
					await session.SynchronizeAsync(token);
				}
				Require.Contains(session.PacketHistory.Skip(packetStart), packet =>
					packet.PacketType == typeof(SM_CHANNEL_INFO));
				Require.Contains(session.PacketHistory.Skip(packetStart), packet =>
					packet.PacketType == typeof(SM_PLAYER_INFO) &&
					packet.Get<int>("objectId") == session.CharacterId);
				session.AcceptTeleportPosition();
				Require.Equal(returnMap, session.Api.World.MapId);
				Require.True(Distance(origin, session.CurrentPosition) > 30,
					$"Return completed but did not move the {ClassLine.StarterName} out of the checked-route pocket.");
				session.TraceDiagnostic("natural-return-completed", new Dictionary<string, object?>
				{
					["skillId"] = returnSkillId,
					["origin"] = origin,
					["bindPosition"] = session.CurrentPosition,
				});
			}

			async Task FinishCaptainOrderAsync()
			{
				if (QuestStatus(2100) < 3) await WaitForQuestStatusAsync(session, 2100, 3, token);
				session.BeginStep("ni07-q2100-finish", "walk-to-ulgorn-and-claim-natural-reward");
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2100, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0, $"Q2100 should present a selectable reward to the natural {ClassLine.StarterName}.");
				await FinishStandardQuestAsync(session, ulgorn, 2100, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Equal(5, session.Api.World.Quests[2100].Status);
			}

			async Task CompleteThinkingAheadAsync()
			{
				if (QuestStatus(2001) < 3) await WaitForQuestStatusAsync(session, 2001, 3, token);
				int boromer;
				if (AtQuestStep(2001, 0))
				{
					boromer = await ApproachShippedSpawnAsync(203518);
					session.BeginStep("ni07-q2001-boromer-start", "speak-to-boromer-and-watch-campaign-movie");
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SELECT1_1, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 51);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SETPRO1, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2001].StepAndFlags);
				}

				if (AtQuestStep(2001, 1))
				{
					for (int sack = 0; ItemCount(session.Api.World, 182203002) < 3; sack++)
					{
						session.BeginStep($"ni07-q2001-sack-{sack + 1}", "walk-and-loot-sprigg-grain-sack");
						int objectId = await UseAndLootQuestObjectAsync(700093, 182203002);
						navigator.UnavailableObjects.Add(objectId);
					}
					Require.Equal(3, ItemCount(session.Api.World, 182203002));
					boromer = await ApproachShippedSpawnAsync(203518);
					session.BeginStep("ni07-q2001-boromer-check", "present-grain-and-advance-mission");
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2001].StepAndFlags);
				}
				if (AtQuestStep(2001, 2))
				{
					boromer = await ApproachShippedSpawnAsync(203518);
					await OpenQuestDialogAsync(boromer, 2001);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(boromer, DialogAction.SETPRO3, questId: 2001), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2001].StepAndFlags);
				}

				for (int kill = 0; QuestStatus(2001) == 3 && QuestVar(2001) is >= 3 and <= 8; kill++)
				{
					session.BeginStep($"ni07-q2001-kill-{kill + 1}", "fight-sprigg-gatherer-for-campaign");
					int target = await ApproachShippedSpawnAsync(210369);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2001].Status);
				boromer = await ApproachShippedSpawnAsync(203518);
				session.BeginStep("ni07-q2001-finish", $"claim-{ClassLine.StarterLabel}-appropriate-campaign-reward");
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2001, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, boromer, 2001, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Equal(5, session.Api.World.Quests[2001].Status);
			}

			async Task AdvanceWheresRaeAsync()
			{
				int verdandi;
				if (QuestStatus(2002) < 3) await WaitForQuestStatusAsync(session, 2002, 3, token);
				if (AtQuestStep(2002, 0))
				{
					session.BeginStep("ni07-q2002-nobekk", "walk-to-nobekk-and-ask-about-rae");
					int nobekk = await ApproachShippedSpawnAsync(203519);
					await OpenQuestDialogAsync(nobekk, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(nobekk, DialogAction.SETPRO1, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2002].StepAndFlags);
				}

				if (AtQuestStep(2002, 1))
				{
					session.BeginStep("ni07-q2002-dabi", "walk-to-dabi-and-ask-about-verdandi");
					int dabi = await ApproachShippedSpawnAsync(203534);
					await OpenQuestDialogAsync(dabi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(dabi, DialogAction.SELECT2_1, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 52);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(dabi, DialogAction.SETPRO2, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2002].StepAndFlags);
				}

				if (AtQuestStep(2002, 2))
				{
					session.BeginStep("ni07-q2002-verdandi", "walk-to-verdandi-and-accept-sprigg-task");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO3, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (QuestStatus(2002) == 3 && QuestVar(2002) is >= 3 and < 10)
				{
					await ApproachShippedSpawnAsync(790002);
					BotPosition spriggRefuge = session.CurrentPosition; // Client-observed, already visited quest-giver ground.
					var rejectedSpriggs = new HashSet<int>();
					BotPosition[] pairedGuardHints = graph.GetMap(contract.MapId)!.Waypoints
						.Where(waypoint => waypoint.TemplateId == 210378)
						.Select(waypoint => waypoint.Position).ToArray();
					navigator.AvoidHostileAggro = true;
					for (int kill = 0, attempt = 0; QuestVar(2002) < 10 && attempt < 21; attempt++)
					{
						session.BeginStep($"ni07-q2002-kill-{kill + 1}-try-{attempt + 1}",
							$"pull-client-observed-sprigg-from-{ClassLine.StarterLabel}-spell-range");
						await RestSafelyAsync(token);
						await session.SynchronizeAsync(token);
						NaturalNavigationObject[] observed = navigator.Observe().Npcs.ToArray();
						NaturalNavigationObject[] candidates = observed
							.Where(npc => npc.TemplateId == 210377 && !rejectedSpriggs.Contains(npc.ObjectId) &&
								pairedGuardHints.All(guard => Distance(guard, npc.Position) >= 10))
							.OrderBy(npc => observed.Count(other => other.ObjectId != npc.ObjectId &&
								runtime.IsAggressive(
									runtime.Data.NpcDataDh.GetNpcTemplate(other.TemplateId)) &&
								Distance(other.Position, npc.Position) < 10))
							.ThenBy(npc => Distance(session.CurrentPosition, npc.Position)).ToArray();
						int? selected = null;
						foreach (NaturalNavigationObject candidate in candidates)
						{
							NaturalCampaignRules campaign = combat.ClassProfile.Campaign;
							BotPosition? stoodAt = null;
							if (Distance(session.CurrentPosition, candidate.Position) > campaign.SpriggRouteBeyond)
							{
								IReadOnlyList<BotPosition> route = await navigator.FindRouteAsync(
									session.CurrentPosition, candidate.Position, token);
								if (route.Count == 0) continue;
								BotPosition standoff = route.LastOrDefault(point =>
									Distance(point, candidate.Position) >= campaign.SpriggStandoff);
								if (standoff == default && Distance(session.CurrentPosition, candidate.Position) >= campaign.SpriggStandoff)
									standoff = session.CurrentPosition;
								if (standoff == default) continue;
								NaturalNavigationResult staging = await NaturalIshalgenNavigator.ExploreAnchorAsync(
									contract.MapId, -1, standoff, navigator, "sprigg-spell-range-standoff", token);
								if (!staging.Arrived) continue;
								stoodAt = standoff;
							}
							bool seen = navigator.Observe().Npcs.Any(npc => npc.ObjectId == candidate.ObjectId);
							if (seen && Distance(session.CurrentPosition, candidate.Position) <= campaign.SpriggSelectWithin)
							{
								selected = candidate.ObjectId;
								break;
							}
							// NR-R1b: the walk to a standoff ends within its arrival radius of it. A standoff that is within the
							// select distance was reached, though the bot stands a step short of it and the Sprigg a step beyond the
							// distance. The Sprigg is taken, and the fight walks what is left. Without this the walk does not move,
							// the Sprigg is not taken and every try is the same: the Rider's line in round 1, with two Spriggs at
							// 21.3 and 21.6 m and a select distance of 20 m.
							if (seen && stoodAt is { } stood && Distance(stood, candidate.Position) <= campaign.SpriggSelectWithin)
							{
								session.TraceDiagnostic("sprigg-taken-from-standoff", new Dictionary<string, object?>
								{
									["targetObjectId"] = candidate.ObjectId,
									["distance"] = Distance(session.CurrentPosition, candidate.Position),
									["standoffDistance"] = Distance(stood, candidate.Position),
									["selectWithin"] = campaign.SpriggSelectWithin,
									["position"] = session.CurrentPosition,
								});
								selected = candidate.ObjectId;
								break;
							}
						}
						if (selected == null && candidates.Length == 0)
						{
							// The 4.8 Sprigg spawns are beyond sight of Verdandi. Walk to a shipped
							// hint before treating an empty observed list as a respawn wait.
							try { selected = await ApproachShippedCombatSpawnAsync(210377); }
							catch (InvalidDataException exception) when (exception.Message.StartsWith(
								"No spell-range client-observed NPC 210377", StringComparison.Ordinal))
							{
								session.TraceDiagnostic("sprigg-hint-unreachable", new Dictionary<string, object?>
								{
									["reason"] = exception.Message,
									["position"] = session.CurrentPosition,
								});
							}
						}
						if (selected is not int target)
						{
							await session.AdvanceAsync(TimeSpan.FromSeconds(25), token); // Ordinary respawn/patrol wait.
							continue;
						}
						if (await combat.TryKillAsync(target, token, spriggRefuge))
						{
							navigator.UnavailableObjects.Add(target);
							kill++;
						}
						else rejectedSpriggs.Add(target);
					}
					navigator.AvoidHostileAggro = false;
					Require.Equal(10, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 10))
				{
					session.BeginStep("ni07-q2002-verdandi-report", "report-sprigg-kills-to-verdandi");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO4, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(11, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 11))
				{
					session.BeginStep("ni07-q2002-mushroom", "collect-sticky-mushroom-for-verdandi");
					if (ItemCount(session.Api.World, 182203003) == 0) await UseAndLootQuestObjectAsync(700045, 182203003);
					Require.Equal(1, ItemCount(session.Api.World, 182203003));
					session.BeginStep("ni07-q2002-mushroom-report", "present-collected-mushroom-to-verdandi");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(12, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (QuestStatus(2002) == 3 && QuestVar(2002) == 12 && session.Api.World.MapId == contract.MapId)
				{
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					session.BeginStep("ni07-q2002-ataxiar-enter", "take-verdandis-quest-teleport-to-ataxiar");
					session.Api.World.BeginWorldReload();
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO5, questId: 2002), token);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
						packet => packet.Get<int>("worldId") == 320010000);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
						packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					Require.Equal(320010000, session.Api.World.MapId);
					// The quest now marks entry after CM_LEVEL_READY sends player info.
					if (!AtQuestStep(2002, 99))
						await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
							packet => packet.Get<int>("questId") == 2002 &&
								packet.Fields.TryGetValue("stepAndFlags", out object? value) && value is int step && step == 99);
					Require.Equal(99, session.Api.World.Quests[2002].StepAndFlags);
					NaturalDecision inInstance = NaturalIshalgenDecisionEngine.Decide(contract,
						ObserveNaturalJourney(session), 7);
					if (!options.OptimizeHubs)
					{
						Require.Equal(2002, inInstance.SelectedQuestId);
						Require.Equal("continue-quest", inInstance.SelectedAction);
					}
					Require.Contains(inInstance.GlobalChecks, check => check.Rule == "quest-transport" && check.Verdict == "pass");
				}
				if (session.Api.World.MapId == 320010000 && AtQuestStep(2002, 99))
				{
					session.BeginStep("ni07-q2002-hagen", "walk-to-hagen-and-take-quest-return-flight");
					NaturalJourneyNavigator instanceNavigator = mapNavigators.Enter(NaturalMapKey.Observe(session.Api.World));
					NaturalNavigationResult hagenApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						320010000, 205020, new BotPosition(434.75f, 399.5f, 235f, 25), instanceNavigator, token);
					Require.True(hagenApproach.Arrived, hagenApproach.Reason);
					int hagen = Require.IsType<int>(hagenApproach.TargetObjectId);
					await NaturalDialogProtocol.OpenAsync(session, hagen, token);
					await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
						packet => packet.Get<int>("targetObjectId") == hagen);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(hagen, DialogAction.QUEST_SELECT, questId: 2002), token);
					await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
						packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
						packet.Get<byte>("emotionType") == (byte)EmotionType.START_FLYTELEPORT);
					session.Api.World.BeginWorldReload();
					await session.AdvanceAsync(TimeSpan.FromSeconds(40), token);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token,
						packet => packet.Get<int>("worldId") == contract.MapId);
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
						packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					Require.Equal(contract.MapId, session.Api.World.MapId);
					Require.Equal(13, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 13))
				{
					session.BeginStep("ni07-q2002-verdandi-return", "report-return-from-ataxiar");
					verdandi = await ApproachShippedSpawnAsync(790002);
					await OpenQuestDialogAsync(verdandi, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(verdandi, DialogAction.SETPRO6, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(14, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 14))
				{
					session.BeginStep("ni07-q2002-ribbit", "find-and-interact-with-cute-ribbit");
					int ribbit = await ApproachShippedSpawnAsync(203538);
					await NaturalDialogProtocol.OpenAsync(session, ribbit, token);
					await session.SynchronizeAsync(token);
					Require.Equal(15, session.Api.World.Quests[2002].StepAndFlags);
				}
				if (AtQuestStep(2002, 15))
				{
					int rae = await session.WaitForNpcAsync(203553, token);
					BotPosition raePosition = session.Api.World.Objects[rae].Position;
					NaturalNavigationResult raeApproach = await NaturalIshalgenNavigator.ApproachNpcAsync(
						contract.MapId, 203553, raePosition, navigator, token);
					Require.True(raeApproach.Arrived, raeApproach.Reason);
					session.BeginStep("ni07-q2002-rae", "speak-to-quest-spawned-rae");
					await OpenQuestDialogAsync(rae, 2002);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(rae, DialogAction.SETPRO7, questId: 2002), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2002].Status);
				}
				session.BeginStep("ni07-q2002-finish", $"return-to-ulgorn-and-claim-{ClassLine.StarterLabel}-reward");
				await ApproachShippedSpawnAsync(203534); // Retrace the walked route through Dabi and Nobekk.
				await ApproachShippedSpawnAsync(203519);
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2002, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0);
				await NaturalDialogProtocol.OpenAsync(session, ulgorn, token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn, DialogAction.QUEST_SELECT, questId: 2002), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn, DialogAction.SETPRO8, questId: 2002), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == ulgorn && packet.Get<int>("questId") == 2002);
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(ulgorn,
					checked((ushort)(DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex)), questId: 2002), token);
				await WaitForQuestStatusAsync(session, 2002, 5, token);
				Require.Contains(2002, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteTreasureOfTheDeceasedAsync()
			{
				int keeper;
				if (QuestStatus(2003) < 3) await WaitForQuestStatusAsync(session, 2003, 3, token);
				if (AtQuestStep(2003, 0))
				{
					session.BeginStep("ni07-q2003-start", "walk-to-treasure-keeper-and-watch-campaign-movie");
					await WalkEasternRoadToDerotAsync(); // Eastern road avoids the collision-blocked direct valley line.
					keeper = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(keeper, 2003);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(keeper, DialogAction.SELECT1_1, questId: 2003), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 53);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(keeper, DialogAction.SETPRO1, questId: 2003), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2003].StepAndFlags);
				}
				if (AtQuestStep(2003, 1))
				{
					int routedRevives = combat.ReviveCount;
					for (int attempt = 0; ItemCount(session.Api.World, 182203004) < 3; attempt++)
					{
						session.BeginStep($"ni07-q2003-kill-{attempt + 1}", "fight-and-loot-treasure-guardian");
						if (combat.ReviveCount > routedRevives)
						{
							await WalkEasternRoadToDerotAsync();
							routedRevives = combat.ReviveCount;
						}
						NaturalNavigationResult camp = await NaturalIshalgenNavigator.ExploreAnchorAsync(
							contract.MapId, -1, new BotPosition(667.961f, 1767.09f, 271.583f, 0),
							navigator, "shipped-west-side-camp", token);
						Require.True(camp.Arrived, $"{camp.Reason} from {session.CurrentPosition}");
						await RestSafelyAsync(token);
						if (combat.ReviveCount > routedRevives)
						{
							await WalkEasternRoadToDerotAsync();
							routedRevives = combat.ReviveCount;
						}
						int target = await ApproachShippedSpawnAsync(210592);
						bool killed = await combat.TryKillAsync(target, token,
							new BotPosition(948.97f, 1700.72f, 259.625f, 0));
						navigator.UnavailableObjects.Add(target);
						if (killed) await TryLootCorpseItemAsync(session, target, 182203004, token);
					}
					Require.Equal(3, ItemCount(session.Api.World, 182203004));
				}
				session.BeginStep("ni07-q2003-finish", "return-treasure-and-claim-reward");
				keeper = await ApproachShippedSpawnAsync(203539);
				await FinishNaturalItemQuestAsync(keeper, 2003);
				Require.Contains(2003, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteNewSkillAsync()
			{
				// Java _2132ANewSkill sets the var by starter class at the level change and answers only that class's trainer.
				await WaitForQuestStatusAsync(session, 2132, 4, token);
				Require.Equal(starterFacts.NewSkill.Var, session.Api.World.Quests[2132].StepAndFlags);
				session.BeginStep("ni07-q2132-finish", $"walk-to-{ClassLine.StarterLabel}-trainer-for-auto-learned-skill-quest");
				int trainer = await ApproachShippedSpawnAsync(starterFacts.NewSkill.TrainerNpcId);
				await FinishStandardQuestAsync(session, trainer, 2132, token);
				Require.Contains(2132, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteCharmedCubeAsync()
			{
				int derot, munin;
				if (QuestStatus(2004) < 3) await WaitForQuestStatusAsync(session, 2004, 3, token);
				if (AtQuestStep(2004, 0))
				{
					session.BeginStep("ni07-q2004-derot-start", "ask-derot-about-charmed-cube");
					derot = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(derot, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(derot, DialogAction.SETPRO1, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 1))
				{
					bool foundCube = ItemCount(session.Api.World, 182203005) > 0;
					for (int attempt = 1; !foundCube; attempt++)
					{
						session.BeginStep($"ni07-q2004-tombstone-{attempt}", "wake-and-fight-tombstone-guardian");
						await RestSafelyAsync(token);
						BotPosition tombstoneIngress = session.CurrentPosition;
						int tombstone = await ApproachShippedSpawnAsync(700047);
						await NaturalDialogProtocol.OpenAsync(session, tombstone, token);
						await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
							packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
							packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
						await session.SynchronizeAsync(token);
						BotKnownObject? guardian = session.Api.World.Objects.Values.FirstOrDefault(item =>
							item.Kind == BotKnownObjectKind.Npc && item.TemplateId == 211755 &&
							!navigator.UnavailableObjects.Contains(item.ObjectId));
						Require.NotNull(guardian);
						NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, 211755, guardian.Position, navigator, token);
						Require.True(approach.Arrived, approach.Reason);
						int target = Require.IsType<int>(approach.TargetObjectId);
						bool killed = false;
						for (int pull = 0; pull < 3 && !killed; pull++)
						{
							if (pull > 0)
							{
								await RestSafelyAsync(token);
								if (session.Api.World.LootStatuses.ContainsKey(target))
								{
									killed = true; // Ordinary defense during rest finished this guardian.
									break;
								}
								if (!navigator.Observe().Npcs.Any(npc => npc.ObjectId == target)) break;
							}
							killed = await combat.TryKillAsync(target, token, tombstoneIngress);
						}
						if (!killed) continue; // An escaped or despawned guardian did not earn a loot attempt.
						navigator.UnavailableObjects.Add(target);
						foundCube = await TryLootCorpseItemAsync(session, target, 182203005, token);
						await RestSafelyAsync(token);
					}
					Require.True(foundCube, "Ordinary tombstone guardians did not yield the quest cube.");
					Require.Equal(1, ItemCount(session.Api.World, 182203005));
					session.BeginStep("ni07-q2004-derot-check", "show-quest-cube-to-derot");
					derot = await ApproachShippedSpawnAsync(203539);
					await OpenQuestDialogAsync(derot, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(derot,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(2, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 2))
				{
					session.BeginStep("ni07-q2004-munin-start", "take-charmed-cube-to-munin");
					NaturalNavigationResult camp = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, new BotPosition(667.961f, 1767.09f, 271.583f, 0),
						navigator, "shipped-west-side-camp", token);
					Require.True(camp.Arrived, camp.Reason); // Static route hint; no roaming NPC interaction.
					NaturalNavigationResult hillside = await NaturalIshalgenNavigator.ExploreAnchorAsync(
						contract.MapId, -1, new BotPosition(413.25f, 1901.5f, 319.136f, 0),
						navigator, "shipped-munin-hillside", token);
					Require.True(hillside.Arrived, hillside.Reason);
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO3, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(3, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (QuestStatus(2004) == 3 && QuestVar(2004) is >= 3 and < 6)
				{
					await ApproachShippedSpawnAsync(203550);
					BotPosition muninRefuge = session.CurrentPosition;
					for (int kill = 0; QuestVar(2004) < 6; kill++)
					{
						bool killed = false;
						for (int attempt = 1; attempt <= 4 && !killed; attempt++)
						{
							session.BeginStep($"ni07-q2004-kill-{kill + 1}-try-{attempt}",
								$"fight-munins-cube-target-from-{ClassLine.StarterLabel}-spell-range");
							await RestSafelyAsync(token);
							int target = await ApproachShippedCombatSpawnAsync(210402);
							killed = await combat.TryKillAsync(target, token, muninRefuge);
							if (killed) navigator.UnavailableObjects.Add(target);
							else
							{
								NaturalNavigationResult regroup = await NaturalIshalgenNavigator.ExploreAnchorAsync(
									contract.MapId, -1, muninRefuge, navigator, "munin-retreat-refuge", token);
								Require.True(regroup.Arrived, regroup.Reason);
							}
						}
						Require.True(killed, $"Q2004 cube target {kill + 1} survived four ordinary {ClassLine.StarterName} pulls.");
						await RestSafelyAsync(token);
					}
					Require.Equal(6, session.Api.World.Quests[2004].StepAndFlags);
				}
				if (AtQuestStep(2004, 6))
				{
					session.BeginStep("ni07-q2004-munin-report", "report-cube-combat-to-munin");
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2004);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO4, questId: 2004), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2004].Status);
				}
				session.BeginStep("ni07-q2004-finish", "return-to-derot-for-quest-reward");
				derot = await ApproachShippedSpawnAsync(203539);
				await FinishStandardQuestAsync(session, derot, 2004, token);
				Require.Contains(2004, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteTeachingALessonAsync()
			{
				int mijou;
				if (QuestStatus(2005) < 3) await WaitForQuestStatusAsync(session, 2005, 3, token);
				navigator.AvoidHostileAggro = true;
				if (AtQuestStep(2005, 0))
				{
					session.BeginStep("ni07-q2005-mijou-start", "walk-to-mijou-and-watch-campaign-movie");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2005);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SELECT1_1, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
						packet.Get<int>("cutsceneId") == 54);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2005].StepAndFlags);
				}
				if (AtQuestStep(2005, 1))
				{
					// Java Q2005 awards Odella only from these three Stalker templates. Shipped
					// spots guide exploration; a target still has to arrive in client packets.
					// Build the hints from this world's loaded spawn data so retail placement
					// edits cannot leave yesterday's coordinates in the search loop.
					BotPosition[] stalkerSearchAreas = NaturalStalkerSearchPolicy.SelectAreas(
						runtime.Data.SpawnsDh.GetSpawnsByWorldId(contract.MapId)
							.Where(group => group.GetNpcId() is 210395 or 210396 or 210750)
							.SelectMany(group => group.GetSpawnTemplates())
							.Select(spot => new BotPosition(spot.GetX(), spot.GetY(), spot.GetZ(), 0)),
						session.CurrentPosition);
					if (stalkerSearchAreas.Length == 0)
						throw new InvalidDataException("Q2005 has no shipped Odella-dropping Stalker search areas.");
					session.TraceDiagnostic("q2005-search-areas", new Dictionary<string, object?>
					{
						["origin"] = session.CurrentPosition,
						["areas"] = stalkerSearchAreas,
					});
					int routedRevives = combat.ReviveCount;
					bool lastStalkerKilled = false;
					BotPosition? successfulStalkerArea = null;
					int preferredAreaMisses = 0;
					int corridorClearAttempts = 0;
					var rejectedPullTargets = new HashSet<int>();
					BotPosition rejectionPosition = session.CurrentPosition;
					long rejectionMillis = runtime.NowMillis;
					void RejectPullTarget(int objectId)
					{
						rejectedPullTargets.Add(objectId);
						rejectionPosition = session.CurrentPosition;
						rejectionMillis = runtime.NowMillis;
					}
					var searchNotes = new List<string>();
					BotNavigationHazard[] ObservedFieldHazards() => navigator.Observe().Npcs
						.Select(npc => (Npc: npc,
							Template: runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)))
						.Where(entry => runtime.IsAggressive(entry.Template))
						.SelectMany(entry => entry.Npc.Hazards(entry.Template!.GetAggroRange())).ToArray();
					async Task<bool> RestAtObservedFieldCampAsync()
					{
						BotNavigationHazard[] hostiles = ObservedFieldHazards();
						if (ItemCount(session.Api.World, 182203006) >= 3 ||
							!NaturalFieldCampPolicy.CanRest(session.CurrentPosition, hostiles))
							return false;
						session.TraceDiagnostic("q2005-field-camp", new Dictionary<string, object?>
						{
							["position"] = session.CurrentPosition,
							["inventoryCount"] = ItemCount(session.Api.World, 182203006),
							["observedHostiles"] = hostiles.Length,
						});
						await RestSafelyAsync(token); // Ordinary sit/stand; attack interrupts it.
						return true;
					}
					BotPosition[]? FindCheckedFiringEdge(NaturalNavigationObject target)
					{
						BotPosition start = session.CurrentPosition;
						foreach (float radius in new[] { 0f, 6f, 12f, 18f })
						for (int sector = 0; sector < 24; sector++)
						{
							float angle = sector * MathF.PI / 12;
							var candidate = new BotPosition(start.X + radius * MathF.Cos(angle),
								start.Y + radius * MathF.Sin(angle), start.Z, 0);
							IReadOnlyList<BotPosition>? edge = geometry.TraceEdge(contract.MapId, start, candidate);
							if (edge == null || !navigator.IsSegmentSafe(edge, target.ObjectId)) continue;
							BotPosition ground = edge[^1];
							if (Distance(ground, target.Position) <= combat.ClassProfile.Campaign.FiringEdgeWithin &&
								geometry.HasLineOfSight(contract.MapId, ground, target.Position))
								return edge.ToArray();
						}
						return null;
					}
					async Task<int> ReachMijouFromFieldAsync()
					{
						try
						{
							return await ApproachShippedNpcThroughObservedGuardsAsync(203540, maximumGuardClears: 12);
						}
						catch (NaturalGuardedObjectiveRevivedException exception)
						{
							// The failed field route belongs to the position before death. Rejoin
							// from the observed bind point instead of continuing that stale approach.
							session.TraceDiagnostic("q2005-return-after-revive", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition, ["reason"] = exception.Message,
							});
							return await RecoverMijouAfterReviveAsync();
						}
						catch (InvalidDataException exception) when (exception.Message.StartsWith(
							"No checked guarded approach to NPC 203540 ", StringComparison.Ordinal))
						{
							// A respawned pack or collision pocket can make the entire checked
							// walk back impossible. Return is the Priest's learned player skill;
							// rejoin the previously visited road from the natural bind point.
							session.TraceDiagnostic("q2005-return-skill-fallback", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition,
								["reason"] = exception.Message,
							});
							await UseLearnedReturnToBindAsync();
							await WalkEasternRoadToDerotAsync();
							return await ApproachShippedSpawnAsync(203540);
						}
					}
					async Task<int> RecoverMijouAfterReviveAsync()
					{
						await RestSafelyAsync(token);
						await WalkEasternRoadToDerotAsync();
						routedRevives = combat.ReviveCount;
						return await ApproachShippedSpawnAsync(203540);
					}
					async Task<int> ReturnToMijouAlongIngressAsync(int eventStart, BotPosition ingressStart)
					{
						if (combat.ReviveCount > routedRevives)
							return await RecoverMijouAfterReviveAsync();
						// A distant reverse A* can fail on the geodata even though the bot just
						// walked in. Revisit its client-estimated movement checkpoints in reverse.
						BotPosition[] breadcrumbs = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
							navigator.Events.Skip(eventStart), stride: 3);
						for (int guardClears = 0; guardClears <= 4; guardClears++)
						{
							int eventBefore = navigator.Events.Count;
							NaturalNavigationResult result = await NaturalIshalgenNavigator.RetraceIngressAsync(
								contract.MapId, ingressStart, breadcrumbs, navigator, token);
							if (combat.ReviveCount > routedRevives)
								return await RecoverMijouAfterReviveAsync();
							if (result.Arrived) return await ReachMijouFromFieldAsync();
							BotPosition blockedCheckpoint = navigator.Events.Skip(eventBefore)
								.LastOrDefault(entry => entry.Action == "navigation-failed")?.Destination ?? ingressStart;
							if (guardClears < 4 && await TryClearObservedBlockerAsync(blockedCheckpoint))
							{
								if (combat.ReviveCount > routedRevives)
									return await RecoverMijouAfterReviveAsync();
								continue; // Ordinary kill may open a checked route; re-observe every hazard.
							}
							if (combat.ReviveCount > routedRevives)
								return await RecoverMijouAfterReviveAsync();
							session.TraceDiagnostic("q2005-ingress-blocked", new Dictionary<string, object?>
							{
								["position"] = session.CurrentPosition,
								["ingressStart"] = ingressStart,
								["blockedCheckpoint"] = blockedCheckpoint,
								["reason"] = result.Reason,
								["route"] = navigator.LastRouteDiagnostic,
							});
							return await ReachMijouFromFieldAsync();
						}
						throw new InvalidDataException("Q2005 exhausted checked ingress retries without a return outcome.");
					}
					async Task WaitForStalkerRespawnAsync()
					{
						// A pursuer can follow the Priest back to Mijou. Advancing the whole
						// respawn delay at once queues its attacks without letting the bot react.
						int defendedPulls = 0;
						int attackHistoryStart = session.PacketHistory.Count;
						await NaturalObservedWait.WaitAsync(TimeSpan.FromSeconds(181), TimeSpan.FromSeconds(2),
							() => runtime.NowMillis,
							async (interval, waitToken) =>
							{
								int packetStart = session.PacketHistory.Count;
								await session.AdvanceAsync(interval, waitToken);
								await session.SynchronizeAsync(waitToken);
								if (session.Api.World.IsDead) await navigator.SynchronizeAsync(waitToken);
								return session.PacketHistory.Skip(packetStart)
									.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
										packet.Get<int>("targetObjId") == session.CharacterId)
									.Select(packet => packet.Get<int>("attackerObjId")).Distinct().ToArray();
							},
							async (attacker, waitToken) =>
							{
								if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? observed) ||
									observed.Kind != BotKnownObjectKind.Npc)
									throw new InvalidDataException($"Q2005 respawn wait received an attack from " +
										$"unobserved object {attacker} at {session.CurrentPosition}.");
								if (++defendedPulls > 4)
									throw new InvalidDataException($"Q2005 respawn wait exceeded four ordinary " +
										$"defensive fights near Mijou at {session.CurrentPosition}.");
								session.TraceDiagnostic("defend-respawn-wait", new Dictionary<string, object?>
								{
									["attacker"] = attacker,
									["npcId"] = observed.TemplateId,
									["hp"] = session.Api.World.CurrentHp,
									["position"] = session.CurrentPosition,
								});
								bool killed = await combat.TryKillAsync(attacker, waitToken,
									new BotPosition(946.29f, 1702.67f, 259.625f, 0), attackHistoryStart);
								if (killed)
								{
									navigator.UnavailableObjects.Add(attacker);
									await RestSafelyAsync(waitToken);
								}
							}, token);
					}
					int unreachableAreas = 0;
					for (int attempt = 0; ItemCount(session.Api.World, 182203006) < 3; attempt++)
					{
						// Route failures take no game time, so a pocket closed by observed packs could otherwise
						// rotate through the search areas forever. Bound the whole search loudly.
						if (attempt >= 120)
							throw new InvalidDataException($"Q2005 Stalker search exceeded 120 attempts at {session.CurrentPosition}: " +
								string.Join(" | ", searchNotes.TakeLast(8)));
						BotPosition isolatedStalker = successfulStalkerArea ??
							stalkerSearchAreas[attempt % stalkerSearchAreas.Length];
						session.BeginStep($"ni07-q2005-stalker-{attempt + 1}",
							$"fight-and-loot-gray-mane-stalker-at-level-{session.Api.World.Level}");
						await RestSafelyAsync(token);
						// A failed pull describes this position and patrol moment, not the NPC forever.
						if (rejectedPullTargets.Count > 0 && NaturalStalkerSearchPolicy.ShouldRetryRejected(
							session.CurrentPosition, rejectionPosition, runtime.NowMillis - rejectionMillis))
						{
							session.TraceDiagnostic("q2005-retry-rejected-targets", new Dictionary<string, object?>
							{
								["count"] = rejectedPullTargets.Count,
								["from"] = rejectionPosition,
								["position"] = session.CurrentPosition,
								["elapsedMs"] = runtime.NowMillis - rejectionMillis,
							});
							rejectedPullTargets.Clear();
						}
						int attackHistoryStart = session.PacketHistory.Count;
						if (combat.ReviveCount > routedRevives)
						{
							await WalkEasternRoadToDerotAsync();
							mijou = await ApproachShippedSpawnAsync(203540);
							routedRevives = combat.ReviveCount;
						}
						if (lastStalkerKilled)
						{
							await WaitForStalkerRespawnAsync(); // Normal shipped respawn, with client-visible defense.
							lastStalkerKilled = false;
						}
						NaturalNavigationObject? nearbyStalker = navigator.Observe().Npcs
							.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
								!rejectedPullTargets.Contains(npc.ObjectId) &&
								Distance(session.CurrentPosition, npc.Position) <= 80)
							.OrderBy(npc => Distance(session.CurrentPosition, npc.Position))
							.FirstOrDefault();
						if (nearbyStalker != null)
							isolatedStalker = nearbyStalker.Position;
						session.TraceDiagnostic("q2005-search-choice", new Dictionary<string, object?>
						{
							["attempt"] = attempt + 1,
							["source"] = nearbyStalker == null ? "shipped-spot" : "client-observed-stalker",
							["position"] = session.CurrentPosition,
							["destination"] = isolatedStalker,
							["observedObjectId"] = nearbyStalker?.ObjectId,
							["rejectedObjectIds"] = rejectedPullTargets.Count,
						});
						int ingressEventStart = navigator.Events.Count;
						BotPosition ingressStart = session.CurrentPosition;
						NaturalNavigationResult safeStalkerArea = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(
							contract.MapId, -1, isolatedStalker, combat.ClassProfile.Campaign.StalkerSearchRange, navigator,
							"shipped-stalker-search-area", token);
						if (!safeStalkerArea.Arrived &&
							safeStalkerArea.Reason == "No collision-checked route to the current destination." &&
							corridorClearAttempts++ < 12)
						{
							NaturalNavigationObject[] observed = navigator.Observe().Npcs.ToArray();
							var blocker = observed
								.Where(npc => runtime.IsAggressive(
									runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)) &&
									Distance(session.CurrentPosition, npc.Position) <= 30 &&
									!rejectedPullTargets.Contains(npc.ObjectId))
								.Select(npc => new { Npc = npc, Edge = FindCheckedFiringEdge(npc) })
								.Where(entry => entry.Edge != null)
								.OrderBy(entry => observed.Count(other => other.ObjectId != entry.Npc.ObjectId &&
									runtime.IsAggressive(
										runtime.Data.NpcDataDh.GetNpcTemplate(other.TemplateId)) &&
									Distance(other.Position, entry.Npc.Position) < 9))
								.ThenBy(entry => Distance(session.CurrentPosition, entry.Npc.Position))
								.FirstOrDefault();
							if (blocker != null)
							{
								BotPosition[] firingEdge = blocker.Edge!;
								if (Distance(session.CurrentPosition, firingEdge[^1]) > 1)
								{
									navigator.Record(new NaturalNavigationEvent(navigator.Events.Count + 1,
										"line-of-sight-flank", "planned", $"Checked short ground move to {ClassLine.StarterName} spell range " +
										"outside other client-observed aggro circles.", contract.MapId, blocker.Npc.TemplateId,
										session.CurrentPosition, firingEdge[^1], blocker.Npc.ObjectId, 0, 0, firingEdge));
									await navigator.MoveAsync(firingEdge, token);
									await navigator.SynchronizeAsync(token);
								}
								NaturalNavigationObject? currentBlocker = navigator.Observe().Npcs
									.FirstOrDefault(npc => npc.ObjectId == blocker.Npc.ObjectId);
								if (currentBlocker == null ||
									Distance(session.CurrentPosition, currentBlocker.Position) > combat.ClassProfile.Campaign.BlockerReplanBeyond ||
									!geometry.HasLineOfSight(contract.MapId, session.CurrentPosition, currentBlocker.Position))
								{
									RejectPullTarget(blocker.Npc.ObjectId);
									searchNotes.Add($"corridor {corridorClearAttempts}: firing target moved or lost after checked flank");
									continue;
								}
								bool cleared;
								try
								{
									cleared = await combat.TryKillAsync(blocker.Npc.ObjectId, token,
										new BotPosition(946.253f, 1702.775f, 259.625f, 0));
								}
								catch (NaturalCombatApproachBlockedException)
								{
									RejectPullTarget(blocker.Npc.ObjectId);
									searchNotes.Add($"corridor {corridorClearAttempts}: moving blocker had no checked combat approach");
									continue;
								}
								searchNotes.Add($"corridor {corridorClearAttempts}: " +
									$"{blocker.Npc.TemplateId}/{blocker.Npc.ObjectId} at {session.CurrentPosition}, " +
									$"killed={cleared}, HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}");
								if (cleared)
								{
									navigator.UnavailableObjects.Add(blocker.Npc.ObjectId);
									if (blocker.Npc.TemplateId is 210395 or 210396 or 210750)
									{
										// The corridor guard is itself a shipped Odella source. A
										// non-drop still needs another kill; do not discard its corpse
										// merely because this fight started as route clearing.
										await TryLootCorpseItemAsync(session, blocker.Npc.ObjectId, 182203006, token);
										lastStalkerKilled = true;
										successfulStalkerArea = blocker.Npc.Position;
										preferredAreaMisses = 0;
										if (!await RestAtObservedFieldCampAsync())
										{
											mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
										}
									}
								}
								else RejectPullTarget(blocker.Npc.ObjectId);
								if (combat.ReviveCount > routedRevives)
								{
									await WalkEasternRoadToDerotAsync();
									routedRevives = combat.ReviveCount;
								}
								attempt--; // The same Stalker search remains pending after a corridor fight.
								continue;
							}
							// A dense patrol can leave no safe short flank from the current spot.
							// Use the journey's checked fight-through route before declaring every
							// Stalker area unreachable from this same corridor.
							BotPosition beforeFightThrough = session.CurrentPosition;
							var unavailableBefore = navigator.UnavailableObjects.ToHashSet();
							bool fightProgress = await TryFightThroughAsync(
								isolatedStalker, nearbyStalker?.ObjectId, rejectedPullTargets);
							NaturalNavigationObject[] defeated = observed
								.Where(npc => !unavailableBefore.Contains(npc.ObjectId) &&
									navigator.UnavailableObjects.Contains(npc.ObjectId)).ToArray();
							session.TraceDiagnostic("q2005-corridor-fight-through", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["destination"] = isolatedStalker,
								["progressed"] = fightProgress,
								["movedMetres"] = Distance(beforeFightThrough, session.CurrentPosition),
								["defeated"] = defeated.Select(npc => $"{npc.TemplateId}/{npc.ObjectId}").ToArray(),
							});
							foreach (NaturalNavigationObject defeatedStalker in defeated.Where(npc =>
								npc.TemplateId is 210395 or 210396 or 210750))
							{
								await TryLootCorpseItemAsync(session, defeatedStalker.ObjectId, 182203006, token);
								lastStalkerKilled = true;
								successfulStalkerArea = defeatedStalker.Position;
								preferredAreaMisses = 0;
								if (!await RestAtObservedFieldCampAsync())
									mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
							}
							if (combat.ReviveCount > routedRevives)
							{
								await WalkEasternRoadToDerotAsync();
								routedRevives = combat.ReviveCount;
							}
							if (fightProgress && (defeated.Length > 0 ||
								Distance(beforeFightThrough, session.CurrentPosition) > 1))
							{
								attempt--; // Reobserve the same area after a real fight or checked move.
								continue;
							}
							searchNotes.Add($"corridor {corridorClearAttempts}: no checked flank or fight-through progress");
						}
						if (!safeStalkerArea.Arrived)
						{
							session.TraceDiagnostic("q2005-search-route-blocked", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["destination"] = isolatedStalker,
								["reason"] = safeStalkerArea.Reason,
								["route"] = navigator.LastRouteDiagnostic,
							});
							searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: {safeStalkerArea.Reason} " +
								$"from {session.CurrentPosition}; route={navigator.LastRouteDiagnostic}; " +
								$"nearby={string.Join(',', navigator.Observe().Npcs.Where(npc =>
									Distance(npc.Position, session.CurrentPosition) < 30)
									.Select(npc => $"{npc.TemplateId}@{Distance(npc.Position, session.CurrentPosition):F1}"))}");
							if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
								successfulStalkerArea = null;
							if (++unreachableAreas >= stalkerSearchAreas.Length)
							{
								// Every shipped area is closed from here: leave the pocket the ordinary way
								// (checked walk through observed guards, else the learned Return skill).
								unreachableAreas = 0;
								corridorClearAttempts = 0;
								mijou = await ReachMijouFromFieldAsync();
							}
							continue; // Try another shipped area; one blocked corridor is not proof all are blocked.
						}
						unreachableAreas = 0;
						await session.SynchronizeAsync(token);
						NaturalNavigationObject? observedStalker = navigator.Observe().Npcs
							.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
								!rejectedPullTargets.Contains(npc.ObjectId) &&
								Distance(session.CurrentPosition, npc.Position) <= 30)
							.OrderBy(npc => Distance(npc.Position, isolatedStalker))
							.FirstOrDefault();
						if (observedStalker == null)
						{
							session.TraceDiagnostic("q2005-search-empty", new Dictionary<string, object?>
							{
								["attempt"] = attempt + 1,
								["area"] = isolatedStalker,
								["position"] = session.CurrentPosition,
							});
							searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: no Stalker in client view");
							if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
								successfulStalkerArea = null;
							mijou = await ApproachShippedSpawnAsync(203540);
							continue; // A patrolling target may be outside this bounded scan; try another area.
						}
						// Choose the Stalker and the spell-range spot that bring no helpers (Java assist rule), off to the
						// side of any pack, and walk there before pulling; a planned clean pull replaces the blanket veto.
						NaturalPullPlan? stalkerPull = null;
						if (!combat.ClassProfile.Campaign.StalkerPull.RestFirst(session.Api.World))
						{
							NaturalNavigationObject[] stalkers = navigator.Observe().Npcs
								.Where(npc => npc.TemplateId is 210395 or 210396 or 210750 &&
									!rejectedPullTargets.Contains(npc.ObjectId) &&
									Distance(session.CurrentPosition, npc.Position) <= 35)
								.OrderBy(npc => Distance(npc.Position, isolatedStalker)).ToArray();
							stalkerPull = await MoveToPullSpotAsync(stalkers, [], "q2005-stalker");
							if (stalkerPull is { Helpers.Count: 0 } &&
								navigator.Observe().Npcs.FirstOrDefault(npc => npc.ObjectId == stalkerPull.Target.Npc.ObjectId) is { } planned)
								observedStalker = planned;
						}
						bool cleanPull = stalkerPull is { Helpers.Count: 0 } && observedStalker.ObjectId == stalkerPull.Target.Npc.ObjectId;
						NaturalNavigationObject[] nearbyHostiles = cleanPull ? [] : navigator.Observe().Npcs
							.Where(npc => npc.ObjectId != observedStalker.ObjectId &&
								runtime.IsAggressive(runtime.Data.NpcDataDh.GetNpcTemplate(npc.TemplateId)) &&
								Distance(session.CurrentPosition, npc.Position) < 12)
							.ToArray();
						if (nearbyHostiles.Length > 0 ||
							combat.ClassProfile.Campaign.StalkerPull.RestFirst(session.Api.World))
						{
							searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: " +
								$"unsafe HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}, " +
								$"nearby={string.Join(',', nearbyHostiles.Select(npc => npc.TemplateId))}");
							if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
								successfulStalkerArea = null;
							int[] attackingIds = session.PacketHistory.Skip(attackHistoryStart)
								.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
									packet.Get<int>("targetObjId") == session.CharacterId)
								.Select(packet => packet.Get<int>("attackerObjId")).Distinct()
								.Where(attacker => navigator.Observe().Npcs.Any(npc => npc.ObjectId == attacker &&
									Distance(session.CurrentPosition, npc.Position) < 30)).ToArray();
							bool attacked = attackingIds.Length != 0;
							searchNotes.Add($"attempt {attempt + 1}: client-observed attack={attacked}; " +
								$"returning on checked ingress");
							if (attacked)
								await combat.EscapeAsync(new BotPosition(946.29f, 1702.67f, 259.625f, 0),
									attackingIds.ToHashSet(), token);
							mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
							if (combat.ReviveCount > routedRevives)
							{
								await WalkEasternRoadToDerotAsync();
								routedRevives = combat.ReviveCount;
							}
							continue;
						}
						int target = observedStalker.ObjectId;
						bool killed;
						try
						{
							killed = await combat.TryKillAsync(target, token,
								new BotPosition(946.29f, 1702.67f, 259.625f, 0), attackHistoryStart);
						}
						catch (NaturalCombatApproachBlockedException exception)
						{
							RejectPullTarget(target);
							searchNotes.Add($"attempt {attempt + 1}: moving Stalker {target} had no checked pull: {exception.Message}");
							continue; // Reobserve at a different shipped area instead of walking into its pack.
						}
						searchNotes.Add($"attempt {attempt + 1} at {isolatedStalker}: " +
							$"target={observedStalker.TemplateId}/{target}, killed={killed}, " +
							$"HP={session.Api.World.CurrentHp}/{session.Api.World.MaxHp}");
						if (killed)
						{
							navigator.UnavailableObjects.Add(target);
							lastStalkerKilled = true;
							successfulStalkerArea = isolatedStalker;
							preferredAreaMisses = 0;
							await TryLootCorpseItemAsync(session, target, 182203006, token);
						}
						else if (successfulStalkerArea != null && ++preferredAreaMisses >= 2)
							successfulStalkerArea = null;
						if (killed && await RestAtObservedFieldCampAsync()) continue;
						mijou = await ReturnToMijouAlongIngressAsync(ingressEventStart, ingressStart);
					}
					Require.True(ItemCount(session.Api.World, 182203006) == 3,
						$"Q2005 needs three earned Odella; observed {ItemCount(session.Api.World, 182203006)}. " +
						string.Join(" | ", searchNotes));
					session.BeginStep("ni07-q2005-mijou-finish", $"show-odella-and-claim-{ClassLine.StarterLabel}-reward");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2005);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2005), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2005].Status);
				}
				mijou = await ApproachShippedSpawnAsync(203540);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2005, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, mijou, 2005, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(2005, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteHitThemWhereItHurtsAsync()
			{
				// Java ce54b7931 _2006HitThemWhereitHurts and QuestItemNpcAI:
				// an ordinary USE_OBJECT on a quest_use_item sack creates its corpse loot.
				int mijou;
				if (QuestStatus(2006) < 3) await WaitForQuestStatusAsync(session, 2006, 3, token);
				if (AtQuestStep(2006, 0))
				{
					session.BeginStep("ni07-q2006-mijou-start", "ask-mijou-about-mau-grain");
					mijou = await ApproachShippedSpawnAsync(203540);
					await OpenQuestDialogAsync(mijou, 2006);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou, DialogAction.SETPRO1, questId: 2006), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2006].StepAndFlags);
				}
				if (AtQuestStep(2006, 1))
				{
					BotPosition ingressStart = session.CurrentPosition;
					int ingressEventStart = navigator.Events.Count;
					BotPosition[] firstSackIngress = [];
					for (int attempt = 1; ItemCount(session.Api.World, 182203008) < 3; attempt++)
					{
						session.BeginStep($"ni07-q2006-sack-{attempt}", "walk-to-client-observed-mau-sack-and-loot-grain");
						if (attempt == 1) await RestSafelyAsync(token); // Mijou-side refuge; do not sit in the Mau field.
						// A sack's 3 s use bar breaks on any hit: fight what is on the Priest first, and never start a
						// use below 80% HP (a death here used to leave the loop navigating while dead).
						if (!await DefendAgainstEngagedAsync("q2006-sack")) { await RestSafelyAsync(token); continue; }
						if (combat.ClassProfile.Campaign.BeforeSack.RestFirst(session.Api.World)) await RestSafelyAsync(token);
						int sack = await ApproachShippedSpawnAsync(700095, skipBlockedTarget: true);
						int interactionPacketStart = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, sack, token);
						await session.WaitForPacketAsync(typeof(SM_EMOTION), token,
							packet => packet.Get<int>("senderObjectId") == session.CharacterId &&
								packet.Get<byte>("emotionType") == (byte)EmotionType.START_QUESTLOOT);
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(3001), token);
						await session.SynchronizeAsync(token);
						DecodedBotServerPacket? finish = session.PacketHistory.Skip(interactionPacketStart)
							.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
								packet.Get<int>("targetObjectId") == sack &&
								packet.Get<byte>("actionType") == 2);
						bool completedUse = finish?.Get<int>("durationMs") == 3000;
						bool looted = completedUse &&
							await TryLootCorpseItemAsync(session, sack, 182203008, token, interactionPacketStart);
						if (completedUse) navigator.UnavailableObjects.Add(sack);
						if (firstSackIngress.Length == 0)
							firstSackIngress = NaturalIshalgenNavigator.SelectRetraceCheckpoints(
								navigator.Events.Skip(ingressEventStart));
						if (!looted)
							session.TraceDiagnostic(completedUse ? "sack-without-grain" : "sack-use-interrupted",
								new Dictionary<string, object?>
							{
								["objectId"] = sack,
								["durationMs"] = finish?.Get<int>("durationMs"),
								["inventoryCount"] = ItemCount(session.Api.World, 182203008),
								["position"] = session.CurrentPosition,
							});
						int[] attackers = session.PacketHistory.Skip(interactionPacketStart)
							.Where(packet => packet.PacketType == typeof(SM_ATTACK) &&
								packet.Get<int>("targetObjId") == session.CharacterId)
							.Select(packet => packet.Get<int>("attackerObjId")).Distinct().Take(4).ToArray();
						foreach (int attacker in attackers)
						{
							if (!session.Api.World.Objects.TryGetValue(attacker, out BotKnownObject? hostile) ||
								hostile.Kind != BotKnownObjectKind.Npc ||
								Distance(session.CurrentPosition, hostile.Position) > 30) continue;
							if (await combat.TryKillAsync(attacker, token, ingressStart,
								interactionPacketStart))
								navigator.UnavailableObjects.Add(attacker);
						}
					}
				Require.Equal(3, ItemCount(session.Api.World, 182203008));
				session.BeginStep("ni07-q2006-mijou-check", "show-earned-mau-grain-to-mijou");
				if (firstSackIngress.Length > 0)
					{
						NaturalNavigationResult retrace = await NaturalIshalgenNavigator.RetraceIngressAsync(
							contract.MapId, ingressStart, firstSackIngress, navigator, token);
							session.TraceDiagnostic("q2006-checked-ingress-return", new Dictionary<string, object?>
						{
							["arrived"] = retrace.Arrived,
							["reason"] = retrace.Reason,
							["checkpoints"] = firstSackIngress.Length,
								["position"] = session.CurrentPosition,
							});
						if (!retrace.Arrived)
						{
							await UseLearnedReturnToBindAsync();
							await WalkEasternRoadToDerotAsync();
						}
					}
					mijou = await ApproachShippedNpcThroughObservedGuardsAsync(203540, maximumGuardClears: 12);
					await OpenQuestDialogAsync(mijou, 2006);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(mijou,
						DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: 2006), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2006].Status);
				}
				session.BeginStep("ni07-q2006-ulgorn-reward", $"return-to-ulgorn-and-claim-{ClassLine.StarterLabel}-reward");
				if (easternRoadIngressStart is BotPosition && easternRoadIngress.Length > 0)
				{
					NaturalNavigationResult easternReturn = await NaturalIshalgenNavigator.RetraceIngressAsync(
						contract.MapId, easternRoadIngressStart.Value, easternRoadIngress, navigator, token);
					session.TraceDiagnostic("eastern-road-return", new Dictionary<string, object?>
					{
						["arrived"] = easternReturn.Arrived,
						["reason"] = easternReturn.Reason,
						["checkpoints"] = easternRoadIngress.Length,
						["position"] = session.CurrentPosition,
					});
					Require.True(easternReturn.Arrived, easternReturn.Reason);
				}
				else await UseLearnedReturnToBindAsync(); // Ordinary learned spell when no prior walking history survives.
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2006, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, ulgorn, 2006, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(2006, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteWheresRaeThisTimeAsync()
			{
				// Java ce54b7931 _2007WheresRaeThisTime: all transitions are ordinary
				// NPC dialogue, quest_use_item interaction, or quest-granted teleport.
				if (QuestStatus(2007) < 3) await WaitForQuestStatusAsync(session, 2007, 3, token);
				(int npcId, int action, int step, string name)[] conversations =
				[
					(203516, DialogAction.SETPRO1, 1, "ulgorn"),
					(203519, DialogAction.SETPRO2, 2, "nobekk"),
					(203539, DialogAction.SETPRO3, 3, "derot"),
					(203552, DialogAction.SETPRO4, 4, "nalto"),
					(203554, DialogAction.SETPRO5, 5, "rae"),
				];
				// A player who dies in the Rae/Nalto camps walks back from bind and tries again. Ten deaths at one
				// objective is a finding worth stopping for; two was not.
				const int MaximumQ2007Recoveries = 10;
				async Task<int> ApproachGuardedCampaignNpcAsync(int templateId)
				{
					for (int recovery = 0; recovery <= MaximumQ2007Recoveries; recovery++)
					{
						try
						{
							bool atBind = Distance(session.CurrentPosition,
								new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 400;
							if (recovery > 0 || atBind && templateId is (203552 or 203554 or 700085 or 700086 or 700087))
							{
								// A death returns to bind. Never treat bind-area monsters as
								// guards of the distant Rae/Nalto corridor.
								session.BeginStep($"ni07-q2007-rejoin-{templateId}-{recovery}",
									"rest-at-bind-and-walk-previously-checked-eastern-road");
								await RestSafelyAsync(token);
								// Only a Priest still near bind needs the eastern road. The navigator may already have
								// walked back south after the revive; turning round to Nobekk from Rae's camp would be
								// a 2 km detour through every camp on the way.
								if (Distance(session.CurrentPosition, new BotPosition(571.0388f, 2787.342f, 299.875f, 0)) < 400)
									await WalkEasternRoadToDerotAsync();
								// Rae and her generators lie past Nalto's camp: rejoin through Nalto, and for a generator
								// through Rae as well, one guarded leg at a time, as the first visit did.
								if (templateId is 203554 or 700085 or 700086 or 700087)
									await ApproachShippedNpcThroughObservedGuardsAsync(203552, maximumGuardClears: 8);
								if (templateId is 700085 or 700086 or 700087)
									await ApproachShippedNpcThroughObservedGuardsAsync(203554, maximumGuardClears: 8);
							}
							return await ApproachShippedNpcThroughObservedGuardsAsync(templateId,
								maximumGuardClears: 8);
						}
						catch (NaturalGuardedObjectiveRevivedException exception) when (recovery < MaximumQ2007Recoveries)
						{
						session.TraceDiagnostic("q2007-guarded-objective-revive", new Dictionary<string, object?>
							{
								["npcTemplateId"] = templateId,
								["recovery"] = recovery + 1,
								["position"] = session.CurrentPosition,
								["reason"] = exception.Message,
							});
						}
						catch (InvalidDataException exception) when (recovery < MaximumQ2007Recoveries &&
							exception.Message.StartsWith("No checked guarded approach", StringComparison.Ordinal))
						{
							// Every checked way in is closed right now (a patrol in the corridor, a camp that has not
							// respawned into a pullable shape). Back off, rest away from respawns, let patrols move,
							// and try again, as a player would, within the same recovery bound.
							session.TraceDiagnostic("q2007-guarded-objective-wait", new Dictionary<string, object?>
							{
								["npcTemplateId"] = templateId,
								["recovery"] = recovery + 1,
								["position"] = session.CurrentPosition,
								["reason"] = exception.Message.Length > 300 ? exception.Message[..300] : exception.Message,
							});
							await RestSafelyAsync(token);
							if (recovery == 2)
							{
								session.TraceDiagnostic("q2007-guarded-route-return", new Dictionary<string, object?>
								{
									["npcTemplateId"] = templateId,
									["position"] = session.CurrentPosition,
								});
								await UseLearnedReturnToBindAsync();
							}
							await session.AdvanceAsync(TimeSpan.FromSeconds(30), token);
							await session.SynchronizeAsync(token);
						}
					}
					throw new InvalidDataException($"Q2007 guarded NPC {templateId} exhausted {MaximumQ2007Recoveries} recoveries.");
				}
				foreach (var (npcId, action, step, name) in conversations)
				{
					if (!AtQuestStep(2007, step - 1)) continue;
					session.BeginStep($"ni07-q2007-{name}", "walk-to-quest-npc-and-advance-dialogue");
					if (npcId == 203539)
						await WalkEasternRoadToDerotAsync(); // Reuse the earlier checked road, not the blocked direct valley.
					int npc = npcId is 203552 or 203554
						? await ApproachGuardedCampaignNpcAsync(npcId)
						: await ApproachShippedSpawnAsync(npcId);
					await OpenQuestDialogAsync(npc, 2007);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, checked((ushort)action), questId: 2007), token);
					await session.SynchronizeAsync(token);
					Require.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
				}
				foreach (var (npcId, step, color) in new[]
				{
					(700085, 6, "green"), (700086, 7, "blue"), (700087, 8, "violet"),
				})
				{
					if (!AtQuestStep(2007, step - 1)) continue;
					session.BeginStep($"ni07-q2007-{color}-generator", "walk-to-and-use-quest-generator");
					// Generators are quest_use_item objects (like the Mau sacks): the quest advances only when the
					// ordinary use bar completes (SM_USE_OBJECT action 2 with its full duration). An attack aborts
					// the use (duration 0); defend and use again.
					for (int use = 1; session.Api.World.Quests[2007].StepAndFlags < step; use++)
					{
						if (use > 10) Require.Fail($"Q2007 {color} generator use did not complete after ten attempts.");
						// Clear around the generator's spot from range before stepping onto it (the recorded human's
						// order), then approach with the same walk-back-after-death recovery as Rae and Nalto.
						BotPosition? generatorHint = graph.GetMap(contract.MapId)!.Waypoints
							.Where(waypoint => waypoint.TemplateId == npcId)
							.OrderBy(waypoint => Distance(session.CurrentPosition, waypoint.Position))
							.Select(waypoint => (BotPosition?)waypoint.Position).FirstOrDefault();
						// Never enter the camp low or without a potion in the bag (the recorded human topped up first).
						if (combat.ClassProfile.Campaign.BeforeCamp.RestFirst(session.Api.World) ||
							NaturalIshalgenPotionPolicy.SelectOwnedPotion(session.Api.World.Inventory.Values) == null)
							await RestSafelyAsync(token);
						await ClearAroundSpotAsync(generatorHint, null, $"q2007-{color}-generator-approach");
						int generator = await ApproachGuardedCampaignNpcAsync(npcId);
						// A player clears what stands near an object before a 3 s use bar a single hit interrupts.
						await ClearAroundObjectiveAsync(generator, $"q2007-{color}-generator");
						if (session.Api.World.IsDead || !navigator.Observe().Npcs.Any(npc => npc.ObjectId == generator &&
							Distance(session.CurrentPosition, npc.Position) <= 3)) continue; // walked off while clearing
						int usePacketStart = session.PacketHistory.Count;
						await NaturalDialogProtocol.OpenAsync(session, generator, token);
						await session.SynchronizeAsync(token);
						DecodedBotServerPacket? started = session.PacketHistory.Skip(usePacketStart)
							.LastOrDefault(packet => packet.PacketType == typeof(SM_USE_OBJECT) &&
								packet.Get<int>("targetObjectId") == generator && packet.Get<byte>("actionType") != 2);
						int durationMs = started?.Get<int>("durationMs") ?? 3000;
						await session.AdvanceAsync(TimeSpan.FromMilliseconds(Math.Max(durationMs, 1) + 1), token);
						await session.SynchronizeAsync(token);
						if (session.Api.World.Quests[2007].StepAndFlags >= step) break;
						session.TraceDiagnostic("generator-use-incomplete", new Dictionary<string, object?>
						{
							["generator"] = npcId,
							["use"] = use,
							["durationMs"] = durationMs,
							["questStep"] = session.Api.World.Quests[2007].StepAndFlags,
							["position"] = session.CurrentPosition,
						});
						// A death here is an ordinary death: revive at bind, walk back and use the generator again.
						await DefendAgainstEngagedAsync($"q2007-{color}-generator");
					}
					Require.Equal(step, session.Api.World.Quests[2007].StepAndFlags);
				}
			if (AtQuestStep(2007, 8))
			{
				session.BeginStep("ni07-q2007-rae-return", "ask-rae-for-quest-transport-back-to-ulgorn");
				int rae = await ApproachShippedSpawnAsync(203554);
				if (options.Course == NaturalMauCourse.GeneratorToRae) return;
					await OpenQuestDialogAsync(rae, 2007);
					session.Api.World.BeginWorldReload();
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(rae, DialogAction.SETPRO6, questId: 2007), token);
					// Java TeleportService.teleportToNpc(player, 203516) stays on Ishalgen, and a same-map teleport
					// (SpawnTask.run -> spawnOnSameMap) sends SM_CHANNEL_INFO and SM_PLAYER_INFO but no SM_PLAYER_SPAWN.
					await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token,
						packet => packet.Get<int>("objectId") == session.CharacterId);
					session.AcceptTeleportPosition();
					Require.Equal(4, session.Api.World.Quests[2007].Status);
				}
				session.BeginStep("ni07-q2007-ulgorn-reward", $"claim-{ClassLine.StarterLabel}-reward-after-quest-transport");
				int ulgorn = await ApproachShippedSpawnAsync(203516);
				string root = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ScenarioManifest.FindDefaultPath())!, "../.."));
				var inventory = NaturalIshalgenInventoryPolicy.Load(root,
					session.Api.World.Inventory.Values.Select(item => item.ItemId));
				int rewardIndex = inventory.ChooseReward(2007, session.Api.World.Level,
					session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
				Require.True(rewardIndex >= 0);
				await FinishStandardQuestAsync(session, ulgorn, 2007, token,
					DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex);
				Require.Contains(session.PacketHistory, packet => packet.PacketType == typeof(SM_PLAY_MOVIE) &&
					packet.Get<int>("cutsceneId") == 58);
				Require.Contains(2007, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteSparkleAndShineAsync()
			{
				// Java ce54b7931 ItemCollecting and ishalgen.xml Q2105; drops
				// come from ordinary client-observed 210367 kills and corpse loot.
				session.BeginStep("ni07-q2105-start", "accept-sparkie-collection-at-vanar");
				int vanarNpc = await ApproachShippedSpawnAsync(203502);
				if (QuestStatus(2105) < 3) await session.StartQuestAsync(vanarNpc, 2105, token);
				if (QuestStatus(2105) == 3)
				{
					for (int attempt = 0; ItemCount(session.Api.World, 182203105) < 3; attempt++)
					{
						session.BeginStep($"ni07-q2105-kill-{attempt + 1}", "fight-and-check-sparkie-drop");
						int target = await ApproachShippedSpawnAsync(210367);
						await combat.KillAsync(target, token);
						navigator.UnavailableObjects.Add(target);
						await TryLootCorpseItemAsync(session, target, 182203105, token);
						await RestSafelyAsync(token);
					}
					Require.Equal(3, ItemCount(session.Api.World, 182203105));
				}
				session.BeginStep("ni07-q2105-finish", "return-to-vanar-and-turn-in");
				vanarNpc = await ApproachShippedSpawnAsync(203502);
				await FinishNaturalItemQuestAsync(vanarNpc, 2105);
				Require.Equal(5, session.Api.World.Quests[2105].Status);
				Require.Equal(0, ItemCount(session.Api.World, 182203105));
			}

			async Task CompleteVanarsFlatteryAsync()
			{
				// Java ce54b7931 _2106VanarsFlattery: Vanar gives the letter,
				// SETPRO1 advances it, and 203517 accepts the ordinary turn-in.
				session.BeginStep("ni07-q2106-start", "accept-vanars-letter-without-creating-an-item");
				int vanarNpc = await ApproachShippedSpawnAsync(203502);
				if (QuestStatus(2106) < 3) await session.StartQuestAsync(vanarNpc, 2106, token);
				if (QuestStatus(2106) == 3)
				{
					Require.Equal(1, ItemCount(session.Api.World, 182203106));
					session.BeginStep("ni07-q2106-vanar-report", "confirm-letter-with-vanar");
					await OpenQuestDialogAsync(vanarNpc, 2106);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(vanarNpc, DialogAction.SETPRO1, questId: 2106), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2106].Status);
				}
				session.BeginStep("ni07-q2106-delivery", "walk-to-recipient-and-deliver-vanars-letter");
				int recipient = await ApproachShippedSpawnAsync(203517);
				await FinishStandardQuestAsync(session, recipient, 2106, token);
				Require.Contains(2106, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteInsectProblemAsync()
			{
				// Java ce54b7931 _2114TheInsectProblem: the SETPRO1 branch
				// requires ten ordinary Grove Sparkie kills before Motgar's reward.
				session.BeginStep("ni07-q2114-motgar-start", "choose-grove-sparkie-insect-problem");
				int motgar = await ApproachShippedSpawnAsync(203533);
				if (QuestStatus(2114) < 3)
				{
					await OpenQuestDialogAsync(motgar, 2114);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(motgar, DialogAction.SETPRO1, questId: 2114), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2114].StepAndFlags);
				}
				for (int kill = 0; QuestStatus(2114) == 3 && kill < 10; kill++)
				{
					session.BeginStep($"ni07-q2114-sparkie-{kill + 1}", "fight-grove-sparkie-for-motgar");
					int target = await ApproachShippedSpawnAsync(QuestVar(2114) < 11 ? 210734 : 210380);
					await combat.KillAsync(target, token);
					navigator.UnavailableObjects.Add(target);
					await RestSafelyAsync(token);
				}
				Require.Equal(4, session.Api.World.Quests[2114].Status);
				session.BeginStep("ni07-q2114-motgar-reward", "return-to-motgar-for-insect-reward");
				motgar = await ApproachShippedSpawnAsync(203533);
				await FinishStandardQuestAsync(session, motgar, 2114, token);
				Require.Contains(2114, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteImprisonedGourmetAsync()
			{
				// Java ce54b7931 _2123TheImprisonedGourmet: a Methu Egg
				// supplies item 182203122; SETPRO2 selects reward group one.
				session.BeginStep("ni07-q2123-munin-start", "accept-munins-gourmet-request");
				int munin = await ApproachShippedSpawnAsync(203550);
				if (QuestStatus(2123) < 3) await session.StartQuestAsync(munin, 2123, token);
				if (QuestStatus(2123) == 3)
				{
					session.BeginStep("ni07-q2123-methu-egg", "walk-to-and-loot-client-observed-methu-egg");
					if (ItemCount(session.Api.World, 182203122) == 0)
						navigator.UnavailableObjects.Add(await UseAndLootQuestObjectAsync(700128, 182203122, skipBlockedTarget: true));
					Require.Equal(1, ItemCount(session.Api.World, 182203122));
					session.BeginStep("ni07-q2123-munin-reward", "give-methu-egg-to-munin-without-ascension-dialogue");
					munin = await ApproachShippedSpawnAsync(203550);
					await OpenQuestDialogAsync(munin, 2123);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(munin, DialogAction.SETPRO2, questId: 2123), token);
					await session.SynchronizeAsync(token);
					Require.Equal(4, session.Api.World.Quests[2123].Status);
					Require.Equal(0, ItemCount(session.Api.World, 182203122));
				}
				await FinishStandardQuestAsync(session, munin, 2123, token);
				Require.Contains(2123, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteRobberyPlotAsync()
			{
				// Java ce54b7931 _2125TheRobberyPlot: Mijou -> Hephe -> Alfrigh.
				session.BeginStep("ni07-q2125-mijou-start", "accept-robbery-investigation");
				int mijou = await ApproachShippedSpawnAsync(203540);
				if (QuestStatus(2125) < 3) await session.StartQuestAsync(mijou, 2125, token);
				if (AtQuestStep(2125, 0))
				{
					session.BeginStep("ni07-q2125-hephe", "ask-hephe-about-robbery");
					int hephe = await ApproachShippedSpawnAsync(203514);
					await OpenQuestDialogAsync(hephe, 2125);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(hephe, DialogAction.SETPRO1, questId: 2125), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2125].StepAndFlags);
				}
				session.BeginStep("ni07-q2125-alfrigh", "report-investigation-to-alfrigh");
				int alfrigh = await ApproachShippedSpawnAsync(203543);
				await FinishStandardQuestAsync(session, alfrigh, 2125, token);
				Require.Contains(2125, session.Api.World.CompletedQuestIds);
			}

			async Task CompleteForLoveOfNegiAsync()
			{
				// Java ce54b7931 _2135ForLoveofNegi: Bolir gives the
				// letter, Negi takes it, and Bolir grants the ordinary reward.
				session.BeginStep("ni07-q2135-bolir-start", "accept-bolirs-letter");
				int bolir = await ApproachShippedSpawnAsync(203532);
				if (QuestStatus(2135) < 3) await session.StartQuestAsync(bolir, 2135, token);
				if (AtQuestStep(2135, 0))
				{
					Require.Equal(1, ItemCount(session.Api.World, 182203131));
					session.BeginStep("ni07-q2135-negi", "deliver-bolirs-letter-to-negi");
					int negi = await ApproachShippedSpawnAsync(203531);
					await OpenQuestDialogAsync(negi, 2135);
					await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(negi, DialogAction.SETPRO1, questId: 2135), token);
					await session.SynchronizeAsync(token);
					Require.Equal(1, session.Api.World.Quests[2135].StepAndFlags);
					Require.Equal(0, ItemCount(session.Api.World, 182203131));
				}
				session.BeginStep("ni07-q2135-bolir-reward", "return-to-bolir-for-delivery-reward");
				bolir = await ApproachShippedSpawnAsync(203532);
				await FinishStandardQuestAsync(session, bolir, 2135, token);
				Require.Contains(2135, session.Api.World.CompletedQuestIds);
			}

			async Task FinishNaturalItemQuestAsync(int npc, int questId, int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
			{
				if (QuestStatus(questId) == 4)
					await FinishStandardQuestAsync(session, npc, questId, token, rewardAction);
				else await FinishItemQuestAsync(session, npc, questId, token, rewardAction);
			}

			async Task AcceptAllAtCurrentHubAsync()
			{
				NaturalIshalgenHubPolicy.Hub? hub = NaturalIshalgenHubPolicy.At(session.CurrentPosition);
				if (hub == null) return;
				if (hub.Name == "aldelle") await BindAtAldelleIfNeededAsync();
				int[] eligible = NaturalIshalgenHubPolicy.Order(
					contract.Quests.Where(quest => hub.QuestIds.Contains(quest.Id) &&
						NaturalIshalgenHubPolicy.CanAccept(quest, session.Api.World))
						.Select(quest => quest.Id), contract,
					id => runtime.Data.Quests.GetQuestById(id)?.GetCategory() ?? QuestCategory.QUEST, hub);
				foreach (int id in eligible)
				{
					int? starterId = templatePlans.TryGetValue(id, out QuestRunPlan? plan) &&
						plan.StartTrigger.Kind == "npc" ? plan.StartTrigger.Npcs.FirstOrDefault()?.Id :
						NaturalIshalgenHubPolicy.CustomNpcStarters.GetValueOrDefault(id);
					if (starterId is not > 0) continue; // auto-start and scripted campaign dialogue
					// "At this hub" means an ordinary nearby NPC the client sees. A broad
					// named hub may cover several hundred metres; walking to every starter
					// just to accept its quest defeats grouping and can enter unsafe packs.
					if (!navigator.Observe().Npcs.Any(npc => npc.TemplateId == starterId.Value &&
						Distance(session.CurrentPosition, npc.Position) <= 45)) continue;
					if (!NaturalIshalgenHubPolicy.CanAccept(contract.Quests.Single(quest => quest.Id == id),
						session.Api.World)) continue;
					if (plan != null && plan.FinishedQuestGroups.Count > 0 &&
						!plan.FinishedQuestGroups.Any(group => group.All(session.Api.World.CompletedQuestIds.Contains)))
						continue;
					session.BeginStep($"ni07-hub-{hub.Name}-accept-{id}", "accept-all-available-hub-quests");
					int starter = await ApproachShippedSpawnAsync(starterId.Value);
					for (int attempt = 1; ; attempt++)
					{
						try { await session.StartQuestAsync(starter, id, token); break; }
						catch (NaturalDialogTooFarException) when (attempt < 3)
						{
							await ReapproachForDialogAsync(starter);
						}
					}
					session.TraceDiagnostic("hub-quest-accepted", new Dictionary<string, object?>
					{
						["hub"] = hub.Name, ["questId"] = id, ["starterId"] = starterId,
						["level"] = session.Api.World.Level,
					});
				}
			}

			async Task CompleteTemplateQuestAsync(QuestRunPlan plan, TemplatePhase phase = TemplatePhase.Full)
			{
				// Java ce54b7931 ItemCollecting, MonsterHunt, ReportTo. The plan
				// supplies shipped objectives only; this executor never uses Q4I's
				// Prepare grants, forced NPC HP, skill grants, neutrality or teleports.
				QuestRunBook book = QuestRunBook.Build(plan);
				for (int index = 0; index < book.Operations.Count; index++)
				{
					QuestRunOperation operation = book.Operations[index];
					if (phase == TemplatePhase.Work && operation.Kind == QuestRunOperationKind.ClaimReward ||
						phase == TemplatePhase.Claim && operation.Kind != QuestRunOperationKind.ClaimReward)
						continue;
					if (QuestStatus(plan.Id) == 5) return;
					if (QuestStatus(plan.Id) >= 3 && operation.Kind == QuestRunOperationKind.StartAtNpc) continue;
					if (QuestStatus(plan.Id) == 4 && operation.Kind != QuestRunOperationKind.ClaimReward) continue;
					session.BeginStep($"ni07-q{plan.Id}-{index}-{operation.Kind}",
						$"natural-template-{plan.Template}-{operation.Kind}");
					switch (operation.Kind)
					{
						case QuestRunOperationKind.Prepare:
							Require.True(plan.FinishedQuestGroups.Count == 0 ||
								plan.FinishedQuestGroups.Any(group => group.All(session.Api.World.CompletedQuestIds.Contains)),
								$"Q{plan.Id} prerequisites must be earned before starting.");
							break;
						case QuestRunOperationKind.StartAtNpc:
						{
							int starterId = operation.Npcs?.FirstOrDefault()?.Id ??
								throw new InvalidDataException($"Q{plan.Id} has no shipped NPC starter.");
							int starter = await ApproachShippedSpawnAsync(starterId);
							for (int attempt = 1; ; attempt++)
							{
								try { await session.StartQuestAsync(starter, plan.Id, token); break; }
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(starter); }
							}
							Require.Equal(3, session.Api.World.Quests[plan.Id].Status);
							break;
						}
						case QuestRunOperationKind.Kill:
						{
							// Quest data can list alternatives that never spawn on this map (Q2117 names 210389 and
							// 210655; only 210389 has spawns): hunt what is actually there, as a player would.
							int[] targetIds = (operation.Npcs?.Select(npc => npc.Id).Distinct() ?? [])
								.Where(SpawnsOnMap).ToArray();
							// BC-06: the L17 source fulfills all four kills; the higher variant's camp stalled
							// the natural approach after the first counter. Farm the proved lower source instead.
							if (altgardLegId == "l10" && plan.Id == 2280) targetIds = targetIds.Where(id => id == 210508).ToArray();
							// BC-02/03: the lower L19 warlocks fulfill all three kills. The L20 variant is on the
							// gate ground reached only by Q24016's later teleport; it is not a pre-campaign hunt route.
							if (altgardLegId == "l10" && plan.Id == 2282) targetIds = targetIds.Where(id => id != 210539).ToArray();
							if (targetIds.Length == 0) throw new InvalidDataException($"Q{plan.Id} kill has no shipped target.");
							var exhaustedKinds = new HashSet<int>();
							int killAbsences = 0;
							for (int kill = 0; NaturalQuestProgress.RemainingKills(plan, operation, session.Api.World) > 0; kill++)
							{
								// BC-06: Q2281's alternatives occupy different camps. Stay with an observed nearby
								// valid source rather than crossing the camps after every counter increment.
								int[] available = targetIds.Where(id => !exhaustedKinds.Contains(id)).ToArray();
								if (available.Length == 0) throw new NaturalHaramelSourcesExhaustedException($"Q{plan.Id} has no remaining reachable sources in this copy.");
								int kind = altgardLegId is "l10" or "l12" ? NearestKind(available) : available[kill % available.Length];
								int target;
								try { target = await KillShippedSpawnAsync(kind,
									objectiveDone: () => NaturalQuestProgress.RemainingKills(plan, operation, session.Api.World) == 0); }
								// M4: a target the other bots of the world keep dead past its respawn wait (Q2331's Chieftain Promakis, one spawn) is
								// waited for again, up to eight times, as a collection's absent source is.
								catch (InvalidDataException absent) when (altgardLeg?.PillarFlight == null && altgardLegId is not ("l10" or "l12") &&
									!session.Api.World.IsDead && absent.Message.StartsWith("No client-observed NPC", StringComparison.Ordinal) &&
									++killAbsences <= Math.Max(2 * available.Length, 8))
								{
									session.TraceDiagnostic("template-target-absent", new Dictionary<string, object?>
									{
										["quest"] = plan.Id, ["target"] = kind, ["absences"] = killAbsences,
									});
									ExcuseBoundedWait();
									continue;
								}
								catch (NaturalHaramelSourcesExhaustedException) when (altgardLegId == "l12" && available.Length > 1)
								{
									exhaustedKinds.Add(kind);
									continue; // Another qualifying kind may still meet this counter without a new entry.
								}
								if (target != 0) navigator.UnavailableObjects.Add(target);
								await RestSafelyAsync(token);
							}
							if (altgardLegId == "l10" && plan.Id == 2282)
							{
								NaturalAltgardHub hub = altgardLeg!.Hub;
								BotPosition hubAt = new(hub.Anchor[0], hub.Anchor[1], hub.Anchor[2], 0);
								if (session.Api.World.MapId != hub.MapId || Distance(session.CurrentPosition, hubAt) > 60)
									await UseLearnedReturnToBindAsync();
								await RestSafelyAsync(token);
							}
							break;
						}
						case QuestRunOperationKind.CollectQuestDrop:
						case QuestRunOperationKind.UseQuestObject:
						{
							int[] sourceIds = operation.Sources?.Select(source => source.Npc?.Id ?? 0)
								.Where(id => id > 0 && SpawnsOnMap(id)).Distinct().ToArray() ?? [];
							// BC-03/06: all ten trinkets can come from the L16 hunter. Wait for its proved ordinary
							// respawn instead of alternating into the upper L17 camp while the nearby source is dead.
							if (altgardLegId == "l10" && plan.Id == 2277) sourceIds = sourceIds.Where(id => id == 210551).ToArray();
							if (sourceIds.Length == 0)
								throw new InvalidDataException($"Q{plan.Id} item {operation.ItemId} has no shipped source.");
							// M3: a source of the turn that is not there after its respawn wait (Q2429's lone 212374 brax, which the other bots of
							// the world kill as well, beside ten 212373s) gives the turn to the next source; twice round them all without one ends it.
							// M4: a lone source is waited for again, up to eight times, as the other bots take their turns; the wait is progress.
							int absences = 0, barren = 0;
							for (int attempt = 0; ItemCount(session.Api.World, operation.ItemId) < operation.Count;
								attempt++)
							{
								int sourceId = altgardLeg?.PillarFlight != null ? NearestKind(sourceIds) : sourceIds[attempt % sourceIds.Length];
								if (sourceId >= 700000)
								{
									navigator.UnavailableObjects.Add(
										await UseAndLootQuestObjectAsync(sourceId, operation.ItemId, skipBlockedTarget: true));
									continue;
								}
								int source;
								try { source = await KillShippedSpawnAsync(sourceId, operation); }
								catch (InvalidDataException absent) when (altgardLeg?.PillarFlight == null && !session.Api.World.IsDead &&
									absent.Message.StartsWith("No client-observed NPC", StringComparison.Ordinal) && ++absences <= Math.Max(2 * sourceIds.Length, 8))
								{
									session.TraceDiagnostic("template-source-absent", new Dictionary<string, object?>
									{
										["quest"] = plan.Id, ["item"] = operation.ItemId, ["source"] = sourceId, ["absences"] = absences,
									});
									ExcuseBoundedWait();
									continue;
								}
								absences = 0;
								long held = ItemCount(session.Api.World, operation.ItemId);
								// Defense and the general quest sweep may finish the collection during the kill's approach.
								if (ItemCount(session.Api.World, operation.ItemId) < operation.Count)
									await TryLootCorpseItemAsync(session, source, operation.ItemId, token);
								// M4: a kill of the bot's own is the collection at work though the drop did not come (Q2320's spectres drop at 35%, with
								// seven other bots on them): it counts as progress, and forty of them in a row without the item end it as a drop that
								// never comes.
								if (ItemCount(session.Api.World, operation.ItemId) > held) barren = 0;
								else if (session.Api.World.LootStatuses.ContainsKey(source))
								{
									Require.True(++barren < 40, $"Q{plan.Id}: item {operation.ItemId} came from none of 40 kills of the bot's own.");
									ExcuseBoundedWait();
								}
								await RestSafelyAsync(token);
								navigator.UnavailableObjects.Add(source);
							}
							// Java QuestService.collectItemCheck accepts surplus and consumes only the required count.
							// Defensive kills and the quest-loot sweep can yield an extra item before this loop finishes.
							Require.True(ItemCount(session.Api.World, operation.ItemId) >= operation.Count,
								$"Q{plan.Id} needs at least {operation.Count} of item {operation.ItemId}.");
							break;
						}
						case QuestRunOperationKind.Report:
						{
							// The report_to plans have one report, the end NPC's, which ClaimReward plays. NL-3: Java ReportToMany (Q2725:
							// Hisui gives it, Mokkurkalfi hears it, Sinjah ends it) has a report before the end NPC's for each NPC on the
							// way, a talk whose SETPRO moves var 0 on to the next.
							int ordinal = book.Operations.Take(index).Count(entry => entry.Kind == QuestRunOperationKind.Report);
							if (ordinal == book.Operations.Count(entry => entry.Kind == QuestRunOperationKind.Report) - 1 || QuestStatus(plan.Id) != 3 ||
								(session.Api.World.Quests[plan.Id].StepAndFlags & 0x3F) != ordinal)
								break;
							int listener = await ApproachShippedSpawnAsync(operation.Npcs?.FirstOrDefault()?.Id ??
								throw new InvalidDataException($"Q{plan.Id} report {ordinal + 1} names no NPC."));
							await NaturalDialogProtocol.OpenAsync(session, listener, token);
							await NpcPageAsync(session, listener, token);
							await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(listener, DialogAction.QUEST_SELECT, questId: plan.Id), token);
							await NpcPageAsync(session, listener, token);
							await NaturalDialogProtocol.SelectAsync(session,
								session.Api.SelectDialog(listener, checked((ushort)(DialogAction.SETPRO1 + ordinal)), questId: plan.Id), token);
							for (int wait = 0; wait < 20 && (session.Api.World.Quests[plan.Id].StepAndFlags & 0x3F) == ordinal; wait++)
							{
								await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
								await session.SynchronizeAsync(token);
							}
							Require.True((session.Api.World.Quests[plan.Id].StepAndFlags & 0x3F) > ordinal, $"Q{plan.Id}'s report {ordinal + 1} did not move it on.");
							break;
						}
						case QuestRunOperationKind.Gather:
							await GatherAsync(operation);
							break;
						case QuestRunOperationKind.ClaimReward:
						{
							int recipientId = operation.Npcs?.FirstOrDefault()?.Id ??
								throw new InvalidDataException($"Q{plan.Id} has no shipped reward NPC.");
							if (plan.Id == 2129 &&
								session.Api.World.ObeliskBindPoint is { MapId: 220010000 } bind &&
								session.Api.World.Skills.ContainsKey(243) &&
								session.Api.Timing.TimeUntilCast(243) <= TimeSpan.FromSeconds(1) &&
								runtime.NowMillis - lastReturnMillis >= TimeSpan.FromMinutes(20).TotalMilliseconds + 1000)
							{
								BotPosition? rewardHint = graph.GetMap(contract.MapId)!.Waypoints
									.Where(waypoint => waypoint.TemplateId == recipientId)
									.Select(waypoint => (BotPosition?)waypoint.Position)
									.FirstOrDefault();
								if (rewardHint is BotPosition destination &&
									NaturalJourneyTravelPolicy.Choose(session.CurrentPosition, destination, bind,
										returnReady: true, flightFareAffordable: false) == NaturalTravelChoice.Return)
									await UseLearnedReturnToBindAsync();
							}
							QuestRunPosition? recipientAt = operation.Npcs?.FirstOrDefault()?.Positions.FirstOrDefault(position => !position.ConditionalEvent);
							int recipient;
							if (recipientAt is { } at && at.MapId != (altgardLeg?.Hub.MapId ?? contract.MapId) && altgardLeg?.Haramel == null)
							{
								Require.Equal(at.MapId, session.Api.World.MapId);
								float talkRange = runtime.Data.NpcDataDh.GetNpcTemplate(recipientId)?.GetTalkDistance() ?? 3;
								NaturalNavigationResult reached = await NaturalIshalgenNavigator.ExploreWithinRangeAsync(at.MapId, recipientId,
									new BotPosition(at.X, at.Y, at.Z, 0), talkRange, navigator, "NPC", token);
								Require.True(reached.Arrived, reached.Reason);
								recipient = Require.IsType<int>(reached.TargetObjectId);
							}
							else recipient = await ApproachShippedSpawnAsync(recipientId);
							int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD;
							// AK-08: a leg's contract may fix the choice (Q2292's Turquoise Earrings); it outranks the inventory policy.
							if (altgardLeg?.RewardChoiceList.FirstOrDefault(choice => choice.QuestId == plan.Id) is { } chosen)
								rewardAction = NaturalAscensionContract.DialogActionId(chosen.Action);
							else if (plan.HasSelectableReward)
							{
								string root = runtime.RepoRoot;
								var inventory = NaturalIshalgenInventoryPolicy.Load(root,
									session.Api.World.Inventory.Values.Select(item => item.ItemId), ClassLine);
								int rewardIndex = inventory.ChooseReward(plan.Id, session.Api.World.Level,
									session.Api.World.Inventory.Values, combat.ClassProfile.Gear);
								Require.True(rewardIndex >= 0);
								rewardAction = DialogAction.SELECTED_QUEST_REWARD1 + rewardIndex;
							}
							for (int attempt = 1; ; attempt++)
							{
								try
								{
									if (plan.Template == "item_collecting")
										await FinishNaturalItemQuestAsync(recipient, plan.Id, rewardAction);
									else
										await FinishStandardQuestAsync(session, recipient, plan.Id, token, rewardAction);
									break;
								}
								catch (NaturalDialogTooFarException) when (attempt < 3) { await ReapproachForDialogAsync(recipient); }
								catch (QuestDialogEchoException followUp) when (altgardLeg?.Haramel != null && plan.Id is 28504 or 28505 &&
									followUp.Action == new PendingQuestDialogAction(recipient, rewardAction, plan.Id) &&
									session.Api.World.CompletedQuestIds.Contains(plan.Id) && session.Api.World.CompletedQuestCounts.GetValueOrDefault(plan.Id) == 1)
								{
									session.TraceDiagnostic("haramel-completed-reward-followup-refused", new Dictionary<string, object?> { ["quest"] = plan.Id, ["action"] = rewardAction });
									await session.SynchronizeAsync(token);
									break;
								}
							}
							Require.Contains(plan.Id, session.Api.World.CompletedQuestIds);
							break;
						}
						default:
							throw new InvalidDataException($"Q{plan.Id} needs natural {operation.Kind} capability.");
					}
				}
			}

			// Kill one shipped spawn of this template. A death mid-fight ends the engagement without kill
			// evidence (the monster resets while the bot revives at the obelisk): walk back and fight again,
			// as a player does, rather than treating the vanished target as an error. A target that despawns
			// or walks out of view for another reason is simply looked for again.
			async Task<int> KillShippedSpawnAsync(int templateId, QuestRunOperation? collection = null, Func<bool>? objectiveDone = null)
			{
				int? CompletedObjectiveSource()
				{
					if (objectiveDone?.Invoke() != true) return CompletedCollectionSource();
					// RC-11: defense can finish the counter while approach/recovery is still running.
					// Zero means no extra target was killed; the ordinary quest journal is the completion evidence.
					session.TraceDiagnostic("hunt-objective-completed-during-approach", new Dictionary<string, object?>
					{
						["templateId"] = templateId, ["position"] = session.CurrentPosition,
					});
					return 0;
				}
				int CollectedSource(int objectId)
				{
					// RC-11: a partially collected stack still needs a new live source. The first
					// full run selected the same one-tail Ampha corpse for all thirty attempts.
					if (collection != null) navigator.UnavailableObjects.Add(objectId);
					return objectId;
				}
				// AO-04: defense can kill and loot the requested source while navigation is still approaching it.
				// Client inventory plus the ordinary loot record completes that objective; do not wait for another live spawn.
				int? CompletedCollectionSource() => collection != null && ItemCount(session.Api.World, collection.ItemId) >= collection.Count &&
					SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? swept)
					? swept.Where(entry => entry.Value.Contains(collection.ItemId)).Select(entry => (int?)entry.Key).LastOrDefault()
					: null;
				int unsuccessfulKills = 0, tacticalRetreats = 0, walkedHome = 0, takenByOthers = 0;
				var failedTargets = new Dictionary<int, int>();
				// NL-2: where the last failed pull ended, and whether this hunt has taken its way out of a stranded spot.
				BotPosition? lastFailedAt = null;
				bool tookWayOut = false;
				for (int attempt = 1; ; attempt++)
				{
					// RC-03: a bind death can leave Ishalgen. Let the normal transport loop return before another pull.
					if (laterCapital != null && collection?.ItemId == 182207011 && session.Api.World.MapId != collection.MapId) return 0;
					// Stop at pull range, outside the target's circle: the fight is planned from there, not started
					// by walking into it (which is how every add reached the bot at Hatata's cave).
					int approachEvidenceStart = session.PacketHistory.Count;
					int target = await ApproachShippedSpawnAsync(templateId, withinRange: combat.ClassProfile.Ranges.SpellRange + 3,
						completedSource: collection == null && objectiveDone == null ? null : CompletedObjectiveSource,
						acceptObservedKill: altgardLegId is "l10" or "l12");
					if (objectiveDone?.Invoke() == true) return 0;
					if (CompletedCollectionSource() is int collectedFrom) return CollectedSource(collectedFrom);
					if (altgardLegId is "l10" or "l12" && session.PacketHistory.Skip(approachEvidenceStart).Any(packet =>
						BotAttackStatus.CarriesHp(packet) && packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0))
						return CollectedSource(target);
					int revives = combat.ReviveCount;
					int retreats = combat.CompletedRetreats;
					int returns = combat.TargetReturns;
					int taken = combat.TargetsTaken;
					int evidenceStart = session.PacketHistory.Count;
					bool killed;
					try
					{
						if (templateId == 212876 && prepareRecordedFireFight != null && session.Api.World.MapId == 320050000)
						{
							// The recorded eastern pull draws Bolcan away from the western spirits. The generic
							// static helper prediction instead walked north to pull a spirit and drew Bolcan into it.
							try { killed = await combat.TryKillAsync(target, token, session.CurrentPosition, preferPull: true,
								rangeRecovery: recordedFireBossRecovery); }
							catch (NaturalCombatApproachBlockedException) { killed = false; }
						}
						else killed = await PullAndKillAsync(target, $"kill-{templateId}");
					}
					catch (NaturalCombatApproachBlockedException gone) when (!session.Api.World.Objects.ContainsKey(target))
					{
						// NR-48: the monster the bot was walking to is no longer there. Alone that does not happen on the way
						// to a pull; in a shared world another player killed it and its corpse is gone. It is not a blocked
						// approach: look for another, and wait for the place to fill again as an empty one is waited for.
						combat.NoteTargetTaken();
						session.TraceDiagnostic("kill-target-taken", new Dictionary<string, object?>
						{
							["template"] = templateId, ["target"] = target, ["reason"] = gone.Message, ["position"] = session.CurrentPosition,
						});
						killed = false;
					}
					// Pull planning may defend against this very target as an add, then report
					// it vanished when its corpse leaves the visible NPC list. At the level cap
					// the EXP total cannot rise; its 0% HP packet is still kill evidence.
					if (killed || session.PacketHistory.Skip(evidenceStart).Any(packet =>
						BotAttackStatus.CarriesHp(packet) &&
						packet.Get<int>("objectId") == target && packet.Get<byte>("hpOrMp") == 0))
						return CollectedSource(target);
					// A pull can stop on SM_DIE before RestSafely performs the bind revive.
					// That is a death outcome, not evidence that a live named source is unreachable.
					bool died = session.Api.World.IsDead || session.Api.World.CurrentHp <= 0 || combat.ReviveCount > revives;
					bool retreated = !died && combat.CompletedRetreats > retreats;
					// AK-08: the same monster failing twice with no death or retreat cannot be fought from anywhere the bot reaches
					// (the Leg 5 smoke run: a Sumarhon sentry 2.8 m below the Cleric's ledge, every cast STR_SKILL_OBSTACLE). Leave it,
					// as a player does, and pull another of its kind.
					// This hunt deliberately farms one proven spawn. Retiring its live
					// object would wait for a respawn that cannot occur; retain the normal
					// six failed-pull/twelve retreat bounds instead.
					if (!died && !retreated && !UsesProvenWarlockSpawn(templateId) &&
						failedTargets.TryGetValue(target, out int failures) && failures >= 1)
					{
						navigator.UnavailableObjects.Add(target);
						navigator.LeftForAnother.Add(target);
						session.TraceDiagnostic("kill-target-abandoned", new Dictionary<string, object?>
						{
							["template"] = templateId, ["target"] = target, ["position"] = session.CurrentPosition,
						});
					}
					if (!died && !retreated) failedTargets[target] = failedTargets.GetValueOrDefault(target) + 1;
					session.TraceDiagnostic(died ? "kill-retry-after-death" : retreated ? "kill-retry-after-retreat" : "kill-target-vanished", new Dictionary<string, object?>
					{
						["template"] = templateId,
						["target"] = target,
						["attempt"] = attempt,
						["tacticalRetreats"] = tacticalRetreats,
						["position"] = session.CurrentPosition,
					});
					if (retreated)
					{
						if (++tacticalRetreats >= 12)
							throw new InvalidDataException($"NPC {templateId} remains guarded after {tacticalRetreats} disengagement retreats.");
					}
					// NR-15: the fight ended because the target gave up and walks home. An attack on a returning monster only
					// starts its return again (Java AttackEventHandler.onAttack, state RETURNING), so it could not be fought:
					// that is not one of the six failed pulls. After a retreat a whole pack walks home, and three of its
					// members used up the six in 28 s. The same monster twice is still left for another, as above.
					// NR-48: a target another player took is not one of the six failed pulls either. Twenty-four of them in
					// one hunt is a ground that others keep empty; the stall budget ends such a hunt before this does.
					else if (!died && combat.TargetsTaken > taken)
					{
						if (++takenByOthers >= 24)
							throw new InvalidDataException($"NPC {templateId}: {takenByOthers} targets were taken by others (last target {target}).");
					}
					else if (!died && combat.TargetReturns > returns)
					{
						if (++walkedHome >= 12)
							throw new InvalidDataException($"NPC {templateId}: {walkedHome} targets gave up and walked home (last target {target}).");
					}
					else
					{
						// NL-2: two failed pulls that end where the bot stood is a stranded spot, not a monster that cannot be
						// fought. The Assassin stood on one rock in Brusthonin for four game hours, 20 m from Q4016's
						// outriders, with no checked way to any of them, until its six pulls were spent. On a mustered leg the
						// hunt takes the way out once, as the approach does: a flight to the hub where it may fly, else
						// Return to the bind, and hunts on from there.
						bool stranded = !died && lastFailedAt is { } before && Distance(before, session.CurrentPosition) < 3;
						lastFailedAt = died ? null : session.CurrentPosition;
						if (stranded && !tookWayOut && altgardLegId != null && NaturalAltgardContract.MusteredLegs.Contains(altgardLegId) &&
							!combat.InCombat && session.Api.World.ObeliskBindPoint is { } boundAt && boundAt.MapId == session.Api.World.MapId &&
							Distance(session.CurrentPosition, boundAt.Position) > 50)
						{
							tookWayOut = true;
							BotPosition strandedAt = session.CurrentPosition;
							bool flew = flyOutOfStrand != null && await flyOutOfStrand();
							if (!flew) await UseLearnedReturnToBindAsync();
							session.TraceDiagnostic("hunt-way-out", new Dictionary<string, object?>
							{
								["templateId"] = templateId, ["position"] = strandedAt, ["way"] = flew ? "flight-to-the-hub" : "return-to-bind",
								["arrived"] = session.CurrentPosition, ["failedPulls"] = unsuccessfulKills,
							});
							await session.SynchronizeAsync(token);
							unsuccessfulKills = 0;
							lastFailedAt = null;
						}
						else if (++unsuccessfulKills >= 6)
							throw new InvalidDataException($"NPC {templateId} was not killed in {unsuccessfulKills} non-retreat attempts (last target {target}).");
					}
					await RestSafelyAsync(token);
				}
			}

			// Pull one quest target the way a player pulls a named: from the firing spot with the fewest adds, and
			// only after the monsters that would actually join (the server's assist rule at that spot, plus any circle
			// that reaches it: NaturalPullPlanner.AddsAt) have been pulled and killed one at a time, each planned the
			// same way. Nothing else nearby is touched. Adds respawn in 180 s, so rest between them only when needed
			// and engage the target inside that window. With no clean spot at all (a dense cave), fight from here and
			// take the adds as they come.
			async Task<bool> PullAndKillAsync(int target, string purpose)
			{
				int startingRevives = combat.ReviveCount;
				bool LostEngagement() => session.Api.World.IsDead || combat.ReviveCount != startingRevives;
				for (int add = 0; add < 8; add++)
				{
					if (LostEngagement()) return false;
					NaturalNavigationObject? npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
					if (npc == null) return false;
					NaturalPullPlan? plan = await MoveToPullSpotAsync([npc], [], purpose);
					if (LostEngagement()) return false;
					if (plan == null)
					{
						// No spot in range, sight and outside every circle. Patrols move: watch them walk on and plan
						// again, up to a minute, as a player waits at the cave mouth for the patrol to pass.
						for (int patience = 0; plan == null && patience < 6 && !LostEngagement(); patience++)
						{
							session.TraceDiagnostic("pull-wait-for-patrol", new Dictionary<string, object?>
							{
								["purpose"] = purpose,
								["target"] = $"{npc.TemplateId}/{npc.ObjectId}",
								["patience"] = patience,
								["position"] = session.CurrentPosition,
							});
							if (altgardLegId == "l10")
							{
								if (!await WaitBeforePullDefendingAsync(10000, purpose)) return false;
							}
							else await session.AdvanceAsync(TimeSpan.FromSeconds(10), token);
							await navigator.SynchronizeAsync(token);
							npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
							if (npc == null) return false;
							plan = await MoveToPullSpotAsync([npc], [], purpose);
						}
						if (LostEngagement()) return false;
						if (plan != null) continue; // plan the adds from the spot found
						// Still none: the fight will be at the target itself. Take the adds that would join there
						// first, nearest first, each from a spot of its own if it has one, else where it stands. Two
						// Gray Mane patrols at Hatata's side took four lives in one run before this.
						NaturalPullMonster[] monstersNow = ObservedPullMonsters();
						NaturalPullMonster standTarget = monstersNow.FirstOrDefault(m => m.Npc.ObjectId == target) ?? PullMonsterOf(npc);
						IReadOnlyList<NaturalPullMonster> addsThere = NaturalPullPlanner.AddsAt(standTarget, npc.Position, monstersNow,
							CanSupport, (a, b) => geometry.HasLineOfSight(contract.MapId, a, b), combat.ClassProfile.Ranges.MeleeReach);
						session.TraceDiagnostic("adds-that-would-join", new Dictionary<string, object?>
						{
							["purpose"] = purpose + "-at-target",
							["target"] = $"{standTarget.Npc.TemplateId}/{standTarget.Npc.ObjectId}",
							["firingPosition"] = npc.Position,
							["adds"] = addsThere.Select(a => $"{a.Npc.TemplateId}/{a.Npc.ObjectId}").ToArray(),
						});
						foreach (NaturalPullMonster addMonster in addsThere.OrderBy(a => Distance(session.CurrentPosition, a.Npc.Position)))
						{
							if (navigator.UnavailableObjects.Contains(addMonster.Npc.ObjectId)) continue;
							NaturalPullPlan? addPlanThere = await MoveToPullSpotAsync([addMonster.Npc], [], purpose + "-add");
							if (LostEngagement()) return false;
							if (addPlanThere == null)
							{
								NaturalNavigationResult closeAdd = await NaturalIshalgenNavigator.ApproachNpcAsync(
									contract.MapId, addMonster.Npc.TemplateId, addMonster.Npc.Position, navigator, token);
								if (LostEngagement()) return false;
								if (!closeAdd.Arrived) continue;
							}
							int revivesAdd = combat.ReviveCount;
							bool killedThere;
							try { killedThere = await combat.TryKillAsync(addMonster.Npc.ObjectId, token, session.CurrentPosition); }
							catch (NaturalCombatApproachBlockedException) { killedThere = false; }
							if (combat.ReviveCount > revivesAdd) return false;
							if (killedThere) navigator.UnavailableObjects.Add(addMonster.Npc.ObjectId);
							if (combat.ClassProfile.Readiness.BetweenAdds.RestFirst(session.Api.World))
								await RestSafelyAsync(token);
							else
								await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
						}
						npc = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target);
						if (npc == null) return false;
						NaturalNavigationResult close = await NaturalIshalgenNavigator.ApproachNpcAsync(
							contract.MapId, npc.TemplateId, npc.Position, navigator, token);
						if (LostEngagement()) return false;
						if (!close.Arrived && !await TryClearObservedBlockerAsync(npc.Position, npc.ObjectId)) return false;
						break;
					}
					NaturalPullMonster[] monsters = ObservedPullMonsters();
					NaturalPullMonster pullTarget = monsters.FirstOrDefault(m => m.Npc.ObjectId == target) ?? PullMonsterOf(
						navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == target) ?? npc);
					// CP-40: a walk-in class fights where the target stands, so the adds are counted there; every other
					// style brings the target to its firing spot.
					BotPosition fightSpot = combat.ClassProfile.PullStyle == NaturalPullStyle.WalkIn ? pullTarget.Npc.Position : session.CurrentPosition;
					IReadOnlyList<NaturalPullMonster> adds = NaturalPullPlanner.AddsAt(pullTarget, fightSpot, monsters,
						CanSupport, (a, b) => geometry.HasLineOfSight(contract.MapId, a, b), combat.ClassProfile.Ranges.MeleeReach);
					session.TraceDiagnostic("adds-that-would-join", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["target"] = $"{pullTarget.Npc.TemplateId}/{pullTarget.Npc.ObjectId}",
						["firingPosition"] = fightSpot,
						["adds"] = adds.Select(a => $"{a.Npc.TemplateId}/{a.Npc.ObjectId}").ToArray(),
					});
					if (adds.Count == 0) break;
					// The add with the fewest adds of its own goes first, so each fight stays one-on-one.
					NaturalPullPlan? addPlan = await MoveToPullSpotAsync(adds.Select(a => a.Npc).ToArray(), [], purpose + "-add");
					if (LostEngagement()) return false;
					if (addPlan == null) break;
					int revivesBefore = combat.ReviveCount;
					bool killedAdd;
					try { killedAdd = await combat.TryKillAsync(addPlan.Target.Npc.ObjectId, token, session.CurrentPosition); }
					catch (NaturalCombatApproachBlockedException) { killedAdd = false; }
					if (combat.ReviveCount > revivesBefore) return false;
					if (killedAdd) navigator.UnavailableObjects.Add(addPlan.Target.Npc.ObjectId);
					if (combat.ClassProfile.Readiness.BetweenAdds.RestFirst(session.Api.World))
						await RestSafelyAsync(token);
					else
						await combat.BuffOurselfAsync(NaturalHelpTrigger.PrePull, token);
				}
				// A named is engaged rested: full heals in reserve matter more than the respawn window's last seconds.
				if (combat.ClassProfile.Readiness.BeforeNamedTarget.RestFirst(session.Api.World))
					await RestSafelyAsync(token);
				if (LostEngagement()) return false;
				try { return await combat.TryKillAsync(target, token, session.CurrentPosition); }
				catch (NaturalCombatApproachBlockedException) { return false; }
			}

			int GatheringSkillLevel() =>
				session.Api.World.Skills.TryGetValue(30001, out BotSkill? gatheringSkill) ? gatheringSkill.Level : 0;

			// A collect step served by gatherable nodes: Q2133's Young Azpha (skill 1) and Q2134's Impure Iron Ore
			// (skill 15). The plan lists every node template that yields the item on every map; only the spots on
			// this map count. Both require human Collecting 30001, not the Daeva's Essencetapping 30002:
			// OD-16 schedules them before Ascension. When Collecting is below the node's level, skill up on Young Azpha,
			// as a player does (Java GatherableController.OnStartUse refuses a node above the skill level, and
			// PlayerSkillList.AddSkillXp raises the skill one point per ~76-224 xp at 91 xp a harvest).
			async Task GatherAsync(QuestRunOperation operation)
			{
				QuestRunSource source = (operation.Sources ?? []).Prepend(operation.Source).OfType<QuestRunSource>()
					.FirstOrDefault(candidate => candidate.Kind == "gatherable" && candidate.GatherableId is > 0 &&
						candidate.Positions.Any(position => position.MapId == contract.MapId))
					?? throw new InvalidDataException($"Item {operation.ItemId} has no gatherable node on map {contract.MapId}.");
				if (GatheringSkillLevel() < 1)
					throw new InvalidDataException("Client did not observe the required level-1 gathering skill.");
				if (GatheringSkillLevel() < source.SkillLevel)
				{
					session.TraceDiagnostic("gather-train", new Dictionary<string, object?>
					{
						["template"] = source.GatherableId,
						["requiredSkillLevel"] = source.SkillLevel,
						["gatheringSkillLevel"] = GatheringSkillLevel(),
					});
					var trainer = new NaturalIshalgenGatheringPolicy(SoakGatheringPool.StarterSpots(),
						GatheringTarget.YoungAzpha.MapId, GatheringTarget.YoungAzpha.TemplateId, long.MaxValue);
					for (int round = 0; round < 8 && GatheringSkillLevel() < source.SkillLevel; round++)
						await HarvestAsync(trainer, GatheringTarget.YoungAzpha.ItemId, "Essencetapping practice",
							() => GatheringSkillLevel() >= source.SkillLevel);
					if (GatheringSkillLevel() < source.SkillLevel)
						throw new InvalidDataException($"Essencetapping stayed at {GatheringSkillLevel()} below the node's level {source.SkillLevel}.");
				}
				IEnumerable<SoakGatheringSpot> spots = source.GatherableId == GatheringTarget.YoungAzpha.TemplateId
					? SoakGatheringPool.StarterSpots()
					: source.Positions.Where(position => position.MapId == contract.MapId)
						.Select(position => new SoakGatheringSpot(position.MapId, 1, source.GatherableId!.Value, position.X, position.Y, position.Z));
				var policy = new NaturalIshalgenGatheringPolicy(spots, contract.MapId, source.GatherableId!.Value, operation.Count);
				await HarvestAsync(policy, operation.ItemId, $"gatherable {source.GatherableId}",
					() => ItemCount(session.Api.World, operation.ItemId) >= operation.Count);
				Require.Equal(operation.Count, ItemCount(session.Api.World, operation.ItemId));
			}

			async Task HarvestAsync(NaturalIshalgenGatheringPolicy policy, int itemId, string purpose, Func<bool> done)
			{
				for (int decisionIndex = 0; decisionIndex < 96 && !done(); decisionIndex++)
				{
					NaturalGatherNode[] visible = session.Api.World.Objects.Values
						.Where(obj => obj.Kind == BotKnownObjectKind.Gatherable &&
							obj.TemplateId == policy.TemplateId)
						.Select(obj => new NaturalGatherNode(obj.ObjectId, obj.Position)).ToArray();
					TimeSpan elapsed = TimeSpan.FromMilliseconds(runtime.NowMillis);
					NaturalGatherChoice choice = policy.Decide(session.CurrentPosition, visible,
						ItemCount(session.Api.World, itemId), elapsed);
					session.TraceDiagnostic("gather-decision", new Dictionary<string, object?>
					{
						["purpose"] = purpose,
						["template"] = policy.TemplateId,
						["gatheringSkillLevel"] = GatheringSkillLevel(),
						["action"] = choice.Action,
						["reason"] = choice.Reason,
						["objectId"] = choice.ObjectId,
						["spot"] = choice.Spot?.Position,
						["inventoryCount"] = ItemCount(session.Api.World, itemId),
					});
					if (choice.Action == "explore" && choice.Spot is { } hint)
					{
						NaturalNavigationResult search = await NaturalIshalgenNavigator.ExploreAnchorAsync(
							contract.MapId, -1, hint.Position, navigator, $"{purpose} spawn hint", token);
						if (!search.Arrived)
						{
							if (search.Reason != "No collision-checked route to the current destination." ||
								!await TryClearObservedBlockerAsync(hint.Position))
								policy.RecordUnreachable(hint, TimeSpan.FromMilliseconds(runtime.NowMillis));
						}
						else if (!session.Api.World.Objects.Values.Any(obj =>
							obj.Kind == BotKnownObjectKind.Gatherable &&
							obj.TemplateId == policy.TemplateId &&
							Distance(obj.Position, hint.Position) < 1))
							policy.RecordUnobserved(hint, TimeSpan.FromMilliseconds(runtime.NowMillis));
						continue;
					}
					if (choice.Action == "wait" && choice.Wait is { } wait)
					{
						for (TimeSpan remaining = wait; remaining > TimeSpan.Zero;)
						{
							TimeSpan interval = remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1);
							await session.AdvanceAsync(interval, token);
							await navigator.SynchronizeAsync(token);
							remaining -= interval;
						}
						continue;
					}
					if (choice.Action != "gather" || choice.Spot is not { } spot || choice.ObjectId is not int objectId)
						throw new InvalidDataException($"Unexpected {purpose} decision {choice.Action}.");
					NaturalNavigationResult approach = await NaturalIshalgenNavigator.ApproachObservedObjectAsync(
						contract.MapId, policy.TemplateId, spot.Position,
						navigator, purpose, token);
					if (!approach.Arrived || approach.TargetObjectId != objectId)
					{
						if (approach.Reason != "No collision-checked route to the current destination." ||
							!await TryClearObservedBlockerAsync(spot.Position, objectId))
							policy.RecordUnreachable(spot, TimeSpan.FromMilliseconds(runtime.NowMillis));
						continue;
					}
					long before = ItemCount(session.Api.World, itemId);
					int packetStart = session.PacketHistory.Count;
					foreach (BotClientPacket packet in session.Api.Gather(objectId))
						await session.SendPacketAsync(packet, token);
					DecodedBotServerPacket update = await session.WaitForPacketAsync(typeof(SM_GATHER_UPDATE), token);
					if (update.Get<byte>("action") == 0)
					{
						DecodedBotServerPacket? finished = null;
						for (int second = 0; second < 60 && finished == null; second++)
						{
							await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							await navigator.SynchronizeAsync(token);
							finished = session.PacketHistory.Skip(packetStart)
								.LastOrDefault(packet => packet.PacketType == typeof(SM_GATHER_UPDATE) &&
									packet.Get<byte>("action") >= 5);
						}
						update = finished ?? throw new TimeoutException($"{purpose} harvest did not finish within 60 virtual seconds.");
					}
					byte outcome = update.Get<byte>("action");
					policy.RecordAttempt(spot, objectId, outcome, TimeSpan.FromMilliseconds(runtime.NowMillis));
					if (outcome == 6)
					{
						for (int followup = 0; followup < 5 && ItemCount(session.Api.World, itemId) == before; followup++)
						{
							await session.AdvanceAsync(TimeSpan.FromSeconds(1), token);
							await navigator.SynchronizeAsync(token);
						}
						Require.Equal(before + 1, ItemCount(session.Api.World, itemId));
					}
				}
			}

			// A walking NPC moved on between the client-estimated arrival and the talk (Q2110's starter is a
			// walker): walk up to it again, as a player does when the "too far" message appears.
			async Task ReapproachForDialogAsync(int npc)
			{
				// M2: a walking NPC is seen where it is going (Bragi, on his round of Morheim Ice Fortress, took up his walk
				// after the first quest and the Templar stood at his next stop before he did). The talk was refused because he
				// was not there yet: wait for him to stop, as a player waits for him to come.
				for (int wait = 0; wait < 80 && session.Api.World.Objects.TryGetValue(npc, out BotKnownObject? walking) && walking.MoveTarget != null; wait++)
				{
					await session.AdvanceAsync(TimeSpan.FromMilliseconds(250), token);
					await session.SynchronizeAsync(token);
				}
				// M3: Spedor walks a long round (a walker_id spawn) and does not stop for long: follow him to where he is now, a few
				// times, until he is in talk range.
				for (int chase = 0; chase < 8 && session.Api.World.Objects.TryGetValue(npc, out BotKnownObject? moving) && moving.MoveTarget != null; chase++)
				{
					NaturalNavigationObject? now = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == npc);
					if (now == null || Distance(session.CurrentPosition, now.Position) <= 3) break;
					session.TraceDiagnostic("dialog-follow-walker", new Dictionary<string, object?>
					{
						["npc"] = $"{now.TemplateId}/{npc}", ["distance"] = Distance(session.CurrentPosition, now.Position), ["chase"] = chase,
					});
					await NaturalIshalgenNavigator.ApproachNpcAsync(contract.MapId, now.TemplateId, now.Position, navigator, token);
				}
				NaturalNavigationObject? seen = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == npc);
				if (seen == null) throw new InvalidDataException($"NPC {npc} is too far to talk to and no longer observed.");
				session.TraceDiagnostic("dialog-too-far", new Dictionary<string, object?>
				{
					["npc"] = $"{seen.TemplateId}/{npc}",
					["distance"] = Distance(session.CurrentPosition, seen.Position),
					["position"] = session.CurrentPosition,
				});
				NaturalNavigationResult again = await NaturalIshalgenNavigator.ApproachNpcAsync(
					contract.MapId, seen.TemplateId, seen.Position, navigator, token);
				if (!again.Arrived) throw new InvalidDataException($"Could not walk back to NPC {npc} to talk: {again.Reason}");
				// Arrival permits 3 m of client-estimated distance. A talk refusal is
				// stronger server evidence: send the final short movement and allow its
				// ground position to settle before retrying the same ordinary dialogue.
				seen = navigator.Observe().Npcs.FirstOrDefault(n => n.ObjectId == npc);
				if (seen != null && Distance(session.CurrentPosition, seen.Position) > 0.4f)
					await navigator.MoveAsync([seen.Position], token);
				await navigator.SynchronizeAsync(token);
				await session.AdvanceAsync(TimeSpan.FromMilliseconds(500), token);
			}

			async Task OpenQuestDialogAsync(int npc, int questId)
			{
				for (int attempt = 1; ; attempt++)
				{
					try
					{
						await NaturalDialogProtocol.OpenAsync(session, npc, token);
						await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
							packet => packet.Get<int>("targetObjectId") == npc);
						break;
					}
					catch (NaturalDialogTooFarException) when (attempt < 3)
					{
						await ReapproachForDialogAsync(npc);
					}
				}
				await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npc, DialogAction.QUEST_SELECT, questId: questId), token);
				await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
					packet => packet.Get<int>("targetObjectId") == npc && packet.Get<int>("questId") == questId);
			}
		}
		catch (Exception failure)
		{
			WriteFailure(failure is TimeoutException ? "stall" : failure is OperationCanceledException ? "cancelled" :
				"execution", failure);
			session.PublishDashboard("failed", force: true);
			throw;
		}
	}

	/// <summary>Bridge talk steps the runner can play so far (NA-12: the Q2008 Norn circuit into Ataxiar): the eleven that
	/// are the same for every class pair, by key.</summary>
	private static readonly HashSet<string> ImplementedBridgeSteps =
		["q2008-v0-munin", "q2008-v1-urd", "q2008-v2-verdandi", "q2008-v3-skuld", "q2008-v4-munin",
		"q2008-v99-hagen", "q2008-reward-munin",
		"q2009-v0-munin", "q2009-v1-heimdall", "q2009-v2-balder", "q24010-reward-suthran"];

	/// <summary>CP-26: the runner plays those eleven and the four class-dependent steps of the line's bridge, which are
	/// found by role: the class choice, the ceremony and the dispatch quest's two.</summary>
	private static bool PlaysBridgeStep(NaturalAscensionContract bridge, NaturalAscensionStep step) =>
		ImplementedBridgeSteps.Contains(step.Key) || Enum.GetValues<NaturalAscensionStepRole>().Any(role => bridge.Step(role) == step);

	private static NaturalIshalgenObservation ObserveNaturalJourney(INaturalJourneySession session)
	{
		BotWorldModel world = session.Api.World;
		return new NaturalIshalgenObservation(true,
			world.QuestJournalObserved, world.CompletedJournalObserved,
			world.MapId, world.Level, world.IsDead,
			new Dictionary<int, BotQuestState>(world.Quests), world.CompletedQuestIds.ToHashSet(),
			session.CurrentPosition, world.Objects.Values.ToArray())
		{
			PlayerClass = world.Objects.GetValueOrDefault(session.CharacterId)?.PlayerClass,
		};
	}

	// What the general quest-loot sweep took from each corpse, so a later TryLootCorpseItemAsync on that corpse counts it.
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BotWorldModel, Dictionary<int, List<int>>> SweptQuestItems = new();

	/// <summary>Take QUEST-group drops and explicitly required non-quest keys. Returns the item ids taken.</summary>
	internal static async Task<IReadOnlyList<int>> LootQuestItemsAsync(INaturalJourneySession session, int objectId,
		Aion.GameServer.Dataholders.StaticData data, CancellationToken token, Func<int, bool>? itemNeeded = null,
		IReadOnlySet<int>? additionalItemIds = null)
	{
		await session.SendPacketAsync(session.Api.Loot(objectId), token);
		// A corpse with nothing for this character sends no list. Five seconds of real time are the limit for it.
		if (await session.WaitForPacketWithinAsync(typeof(SM_LOOT_ITEMLIST), TimeSpan.FromSeconds(5), token,
				packet => packet.Get<int>("targetObjectId") == objectId) is not { } list)
			return [];
		var taken = new List<int>();
		foreach (IReadOnlyDictionary<string, object?> entry in list.Get<List<IReadOnlyDictionary<string, object?>>>("items"))
		{
			int itemId = Get<int>(entry, "itemId");
			var template = data.ItemDataDh.GetItemTemplate(itemId);
			if (template == null || template.itemGroup != Aion.GameServer.Model.Templates.Items.Enums.ItemGroup.QUEST && additionalItemIds?.Contains(itemId) != true) continue;
			if (itemNeeded?.Invoke(itemId) == false) continue;
			BotInventoryItem? existing = session.Api.World.Inventory.Values.FirstOrDefault(owned => owned.ItemId == itemId);
			// Java DropService rejects a second limit-one item; there will be no inventory update to wait for.
			if (existing != null && template.HasLimitOne()) continue;
			// NR-R16c: Java ItemService.addItem fills a stack the player has room in (addStackableItem: an update of that
			// stack) and makes a new item for one that does not stack or finds every stack full (addNonStackableItem).
			BotInventoryItem? stack = !template.IsStackable() ? null : session.Api.World.Inventory.Values
				.FirstOrDefault(owned => owned.ItemId == itemId && owned.Count < template.GetMaxStackCount());
			await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(entry, "index")), token);
			if (stack == null)
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
					packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items").Any(item => Get<int>(item, "itemId") == itemId));
			else
				await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token, packet => packet.Get<int>("objectId") == stack.ObjectId);
			taken.Add(itemId);
		}
		await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
		if (taken.Count > 0)
		{
			Dictionary<int, List<int>> swept = SweptQuestItems.GetOrCreateValue(session.Api.World);
			if (!swept.TryGetValue(objectId, out List<int>? items)) swept[objectId] = items = [];
			items.AddRange(taken);
		}
		return taken;
	}

	internal static async Task<bool> TryLootCorpseItemAsync(INaturalJourneySession session,
		int objectId, int itemId, CancellationToken token, int preopenedPacketStart = -1)
	{
		// The general sweep may have taken it already, right after the kill.
		if (SweptQuestItems.TryGetValue(session.Api.World, out Dictionary<int, List<int>>? sweptItems) &&
			sweptItems.TryGetValue(objectId, out List<int>? sweptFromCorpse) && sweptFromCorpse.Contains(itemId))
		{
			session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
			{
				["objectId"] = objectId, ["itemId"] = itemId, ["dropped"] = true, ["sweptAfterKill"] = true,
				["inventoryCount"] = ItemCount(session.Api.World, itemId),
			});
			return true;
		}
		long beforeCount = ItemCount(session.Api.World, itemId);
		// Quest-use objects open their drop list on completion. Reopening that list sends a second
		// START_LOOT/END_LOOT cycle within the same client frame and can leave observers in a loot pose.
		DecodedBotServerPacket? list = preopenedPacketStart >= 0
			? session.PacketHistory.Skip(preopenedPacketStart).LastOrDefault(packet =>
				packet.PacketType == typeof(SM_LOOT_ITEMLIST) && packet.Get<int>("targetObjectId") == objectId)
			: null;
		if (list == null)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId), token);
			list = await session.WaitForPacketWithinAsync(typeof(SM_LOOT_ITEMLIST), TimeSpan.FromSeconds(5), token,
				packet => packet.Get<int>("targetObjectId") == objectId);
			if (list == null)
			{
				session.TraceDiagnostic("quest-loot-list-missing", new Dictionary<string, object?>
				{
					["objectId"] = objectId,
					["itemId"] = itemId,
					["inventoryCount"] = beforeCount,
					["position"] = session.CurrentPosition,
				});
				return false;
			}
		}
		IReadOnlyDictionary<string, object?>? item = list
			.Get<List<IReadOnlyDictionary<string, object?>>>("items")
			.FirstOrDefault(entry => Get<int>(entry, "itemId") == itemId);
		if (item == null)
		{
			await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
			session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
			{
				["objectId"] = objectId,
				["itemId"] = itemId,
				["dropped"] = false,
				["inventoryCount"] = ItemCount(session.Api.World, itemId),
			});
			return false;
		}
		BotInventoryItem? existing = session.Api.World.Inventory.Values.SingleOrDefault(entry => entry.ItemId == itemId);
		if (preopenedPacketStart >= 0) await session.AdvanceAsync(TimeSpan.FromMilliseconds(450), token);
		await session.SendPacketAsync(session.Api.Loot(objectId, Get<byte>(item, "index")), token);
		if (existing == null)
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_ADD_ITEM), token,
				packet => packet.Get<List<IReadOnlyDictionary<string, object?>>>("items")
					.Any(entry => Get<int>(entry, "itemId") == itemId));
		else
			await session.WaitForPacketAsync(typeof(SM_INVENTORY_UPDATE_ITEM), token,
				packet => packet.Get<int>("objectId") == existing.ObjectId);
		await session.SendPacketAsync(session.Api.Loot(objectId, close: true), token);
		long afterCount = ItemCount(session.Api.World, itemId);
		session.TraceDiagnostic("quest-drop-attempt", new Dictionary<string, object?>
		{
			["objectId"] = objectId,
			["itemId"] = itemId,
			["dropped"] = true,
			["inventoryCount"] = afterCount,
		});
		Require.True(afterCount > beforeCount,
			$"Corpse {objectId} listed quest item {itemId}, but inventory stayed at {beforeCount}.");
		return true;
	}

	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));
	// M3: a dialog page of the NPC talked to. Java quest handlers can send other dialog windows on the same talk: Q2718's
	// (Reshanta) sends SM_DIALOG_WINDOW(0, 0) to everyone who talks to Vivi, one of its four NPCs, without having taken it.
	private static Task<DecodedBotServerPacket> NpcPageAsync(INaturalJourneySession session, int npcObjectId, CancellationToken token) =>
		session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token, packet => packet.Get<int>("targetObjectId") == npcObjectId);

	private static async Task FinishStandardQuestAsync(
		INaturalJourneySession session,
		int npcObjectId,
		int questId,
		CancellationToken token,
		int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
	{
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await NpcPageAsync(session, npcObjectId, token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await NpcPageAsync(session, npcObjectId, token);
		if (session.Api.World.Quests[questId].Status == 3)
		{
			await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.SELECT_QUEST_REWARD, questId: questId), token);
			await NpcPageAsync(session, npcObjectId, token);
		}
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, checked((ushort)rewardAction), questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await session.SendPacketAsync(session.Api.CloseDialog(npcObjectId), token);
	}

	private static async Task WaitForQuestStatusAsync(
		INaturalJourneySession session, int questId, byte status, CancellationToken token)
	{
		if (session.Api.World.Quests.TryGetValue(questId, out BotQuestState? quest) && quest.Status == status)
			return;
		await session.WaitForPacketAsync(typeof(SM_QUEST_ACTION), token,
			packet => packet.Get<int>("questId") == questId &&
				packet.Fields.TryGetValue("status", out object? value) && value is byte actual && actual == status);
	}

	private static long ItemCount(BotWorldModel world, int itemId) =>
		world.Inventory.Values.Where(item => item.ItemId == itemId).Sum(item => item.Count);

	private static int ChooseEngagedTarget(IReadOnlyList<int> attackers, INaturalJourneySession session,
		bool preferWounded) => attackers
		.OrderBy(id => PriorityForEngagedTarget(id, attackers.Count, session, preferWounded))
		.ThenBy(id => session.Api.World.Objects.TryGetValue(id, out BotKnownObject? npc)
			? Distance(session.CurrentPosition, npc.Position) : float.MaxValue)
		.First();

	private static int PriorityForEngagedTarget(int objectId, int attackerCount,
		INaturalJourneySession session, bool preferWounded)
	{
		if (!preferWounded || attackerCount != 2) return 0;
		for (int index = session.PacketHistory.Count - 1; index >= 0; index--)
		{
			DecodedBotServerPacket packet = session.PacketHistory[index];
			if (BotAttackStatus.CarriesHp(packet) && packet.Get<int>("objectId") == objectId)
				return packet.Get<byte>("hpOrMp");
		}
		return 100; // Unknown target HP is not treated as wounded.
	}

	private static T Get<T>(IReadOnlyDictionary<string, object?> fields, string name) =>
		fields.TryGetValue(name, out object? value) && value is T typed
			? typed
			: throw new InvalidDataException($"Decoded field '{name}' was missing or was not {typeof(T).Name}.");
	private static async Task FinishItemQuestAsync(
		INaturalJourneySession session, int npcObjectId, int questId, CancellationToken token,
		int rewardAction = DialogAction.SELECTED_QUEST_NOREWARD)
	{
		await NaturalDialogProtocol.OpenAsync(session, npcObjectId, token);
		await NpcPageAsync(session, npcObjectId, token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(npcObjectId, DialogAction.QUEST_SELECT, questId: questId), token);
		await NpcPageAsync(session, npcObjectId, token);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(
			npcObjectId, DialogAction.CHECK_USER_HAS_QUEST_ITEM, questId: questId), token);
		await NpcPageAsync(session, npcObjectId, token);
		Require.Equal((byte)4, session.Api.World.Quests[questId].Status);
		await NaturalDialogProtocol.SelectAsync(session, session.Api.SelectDialog(
			npcObjectId, checked((ushort)rewardAction), questId: questId), token);
		await WaitForQuestStatusAsync(session, questId, 5, token);
		await session.WaitForPacketAsync(typeof(SM_DIALOG_WINDOW), token,
			packet => packet.Get<int>("targetObjectId") == npcObjectId);
		await session.SendPacketAsync(session.Api.CloseDialog(npcObjectId), token);
	}


}
