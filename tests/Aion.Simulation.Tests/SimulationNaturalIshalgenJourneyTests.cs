using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Navigation.NavMesh;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Reflexes;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
	private static float Distance(BotPosition a, BotPosition b) => MathF.Sqrt(
		MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2) + MathF.Pow(a.Z - b.Z, 2));

	/// <summary>
	/// A fresh or retained Priest follows the frozen NI-07 quest contract. The explicitly
	/// selected short scope is a diagnostic checkpoint, not acceptance evidence.
	/// </summary>
	[SkippableFact]
	public async Task NaturalIshalgenPriestCompletesFrozenJourneyWithoutSetup()
	{
		Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
		bool stopAfterQ2004 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2004") == "1";
		bool stopAfterQ2005 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2005") == "1";
		bool stopAfterQ2006 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2006") == "1";
		bool stopAfterQ2007 = Environment.GetEnvironmentVariable("NI07_STOP_AFTER_Q2007") == "1";
		bool fullJourney = Environment.GetEnvironmentVariable("NI07_FULL_JOURNEY") == "1";
		bool continuousAltgard = Environment.GetEnvironmentVariable("AF_ALTGARD") == "all";
		Skip.IfNot(stopAfterQ2004 || stopAfterQ2005 || stopAfterQ2006 || stopAfterQ2007 || fullJourney,
			"Set NI07_STOP_AFTER_Q2004=1, NI07_STOP_AFTER_Q2005=1, NI07_STOP_AFTER_Q2006=1 " +
			"or NI07_STOP_AFTER_Q2007=1 " +
			"for a focused checkpoint, or NI07_FULL_JOURNEY=1 for 41-quest acceptance.");
		// CP-15: CP_CLASS names the class line; unset, it is the accepted Priest and Cleric line.
		NaturalClassLine line = NaturalClassLine.Parse(Environment.GetEnvironmentVariable(NaturalClassLine.EnvironmentVariable));
		string starter = line.Starter.ToString().ToLowerInvariant();
		// NR-47: a character captured in a round lives on its seat's account, and may have its seat's name. A run that
		// resumes it alone, as Verify does, is told both; unset, they are the line's as always.
		int accountId = int.TryParse(Environment.GetEnvironmentVariable("NI08_RESUME_ACCOUNT"), out int roundAccount) && roundAccount > 0
			? roundAccount : line.SimAccountId;
		string characterName = Environment.GetEnvironmentVariable("NI08_RESUME_NAME") is { Length: > 0 } roundName ? roundName : line.CharacterName;
		string combatTracePath = Path.Combine(
			Environment.GetEnvironmentVariable("AION_NI07_COMBAT_DIR") ??
				Path.Combine(Aion.GameServer.TestKit.RealStaticData.RepoRoot(), "run", "ni07-combat"),
			$"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.trace.jsonl");
		using var combatTrace = BotActionTraceWriter.Open(combatTracePath,
			Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "ni07", "b01", $"sim-player-{accountId}",
			virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
		Console.WriteLine($"NI-07 combat trace: {combatTracePath}");
		// Optional read-only source-calculation evidence alongside ordinary play.
		// No encounter, progression, combat policy or stat input is changed.
		string? retailEvidence=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_NATURAL_EVIDENCE");
		using var retailLogs=string.IsNullOrEmpty(retailEvidence)?null:
			new ThreeKeysRuntimeLogProvider(retailEvidence,()=>fixture.Clock.NowMillis);
		if(retailLogs is not null)fixture.LoggerFactory.AddProvider(retailLogs);
		using var policy = NewPolicy("NI07", includeHistory: true);
		// The joined scope includes Ishalgen/Ascension and thirteen Altgard segments. Preserve
		// the ordinary 45-minute deadline and every in-game recovery/stall limit; allow the
		// aggregate SIM process enough wall time to run all fourteen stages.
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(
			stopAfterQ2004 ? 6 : stopAfterQ2005 ? 8 : stopAfterQ2006 ? 16 : stopAfterQ2007 ? 20 : continuousAltgard ? 90 : 45));
		CancellationToken token = timeout.Token;
		await using var session = new SimulationL0Session(
			fixture, policy, "b01", accountId: accountId, characterName, Race.ASMODIANS,
			combatTrace, combatTracePath) { IdentityClassLine = line };
		var dashboard = new LiveBotDashboardState();
		int dashboardPort = int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880");
		await using var dashboardHost = LiveBotDashboardHost.OpenFirstFree(
			Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "natural-ishalgen", ["NI-07", "NI-08"], dashboard, dashboardPort);
		session.Dashboard = dashboard;
		if (dashboardHost.Enabled)
		{
			Console.WriteLine($"Natural Ishalgen dashboard: {dashboardHost.Url}");
			// NR-45: a run beside others is not at the default port. Its evidence says where its monitor is.
			File.WriteAllText(Path.Combine(Path.GetDirectoryName(combatTracePath)!, "monitor.json"),
				$"{{\"url\":\"{dashboardHost.Url}\",\"port\":{dashboardHost.Port}}}");
		}
		// Leg-scoped help (the ax leg's scroll and Bronze Coins) is approved up to a total, counted here.
		var legSupplied = new Dictionary<int, long>();
		var runtime = new NaturalJourneyRuntime(Aion.GameServer.TestKit.RealStaticData.RepoRoot(),
			"SIM-natural-ishalgen", fixture.Seed, fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch,
			() => BotNavigationGeometry.ForServerWorld(fixture.World.GetPlayer(session.CharacterId).GetInstanceId(), Race.ASMODIANS),
			EnterAsync, policy.AssertClean, () => policy.SnapshotProblems(), combatTrace, dashboard)
		{
			// NA-21: the approved help items (OD-13), unless NA_HELP_ITEMS=0 asks for a clean natural run.
			SupplyHelpItemAsync = NaturalHelpItemSupply.Enabled(Environment.GetEnvironmentVariable(NaturalHelpItemSupply.Switch))
				? SupplyHelpItemAsync : null,
		};
		await new NaturalIshalgenJourney(session, runtime, new NaturalJourneyOptions(
			stopAfterQ2004 ? 2004 : stopAfterQ2005 ? 2005 : stopAfterQ2006 ? 2006 : stopAfterQ2007 ? 2007 : null,
			Environment.GetEnvironmentVariable("NI08_RELOG_AT"), Environment.GetEnvironmentVariable("NI08_STOP_AT"),
			Environment.GetEnvironmentVariable("NI07_STOP_ON_DEATH") == "1",
			Environment.GetEnvironmentVariable("NI07_OPTIMIZE_HUBS") == "1",
			AscensionBridge: continuousAltgard || Environment.GetEnvironmentVariable("NA_ASCENSION") == "1",
			// AF-08/09: Altgard Leg 1 from a restored `altgard` snapshot (docs/natural-altgard-leveling.md).
			AltgardLeg1: Environment.GetEnvironmentVariable("AF_ALTGARD") == "1",
			// AM-06/07: AF_ALTGARD=l2 plays Leg 2 from `altgard-l12`; AF_ONLY limits a diagnostic run to the listed quests.
			AltgardLegId: Environment.GetEnvironmentVariable("AF_ALTGARD") is { Length: > 1 } legId ? legId : null,
			AltgardOnlyQuests: Environment.GetEnvironmentVariable("AF_ONLY") is { Length: > 0 } onlyList
				? onlyList.Split(',').Select(int.Parse).ToArray() : null,
			CoinGearReceiptPath: Environment.GetEnvironmentVariable("AF_CG_RECEIPTS"),
			HaramelProgressPath: Environment.GetEnvironmentVariable("AF_HM_PROGRESS"),
			CapitalStage: Environment.GetEnvironmentVariable("PC_CAPITAL"),
			LaterCapital: Environment.GetEnvironmentVariable("RC_CAPITAL") == "1",
			// AX-08: AX_ARENA_FIRST_TRY=timeout or death loses Garm's first arena try by ordinary play, to prove the failure path.
			AbyssArenaFirstTry: Environment.GetEnvironmentVariable("AX_ARENA_FIRST_TRY") is { Length: > 0 } lose ? lose : null,
			// AX-10: AX_RING_FIRST_TRY=timeout stays on the ground for Yornduf's first 70 s, to prove the failure path.
			AbyssRingFirstTry: Environment.GetEnvironmentVariable("AX_RING_FIRST_TRY") is { Length: > 0 } grounded ? grounded : null,
			ClassLine: line)).RunAsync(token);

		async Task SupplyHelpItemAsync(int itemId, long count, CancellationToken supplyToken)
		{
			NaturalHelpItemSupply.RequireApproved(itemId, count, session.IdentityAltgardLegId, legSupplied.GetValueOrDefault(itemId));
			legSupplied[itemId] = legSupplied.GetValueOrDefault(itemId) + count;
			var player = fixture.World.GetPlayer(session.CharacterId);
			Assert.Equal(0, Aion.GameServer.Services.Items.ItemService.AddItem(player, itemId, count, allowInventoryOverflow: true));
			await session.SynchronizeAsync(supplyToken);
		}

		async Task<bool> EnterAsync(CancellationToken token)
		{
			string? resumeIdentity = Environment.GetEnvironmentVariable("NI08_RESUME_CHARACTER");
			bool resuming = !string.IsNullOrWhiteSpace(resumeIdentity);
			int retainedId = 0;
			if (resuming && (!int.TryParse(resumeIdentity, out retainedId) || retainedId <= 0))
				throw new ArgumentException("NI08_RESUME_CHARACTER must be a positive character ID; refusing to create a replacement.");
			session.BeginStep(resuming ? "ni08-login-existing" : "ni07-create",
				resuming ? $"reconstruct-retained-{starter}" : $"create-natural-asmodian-{starter}");
			if (resuming)
			{
				var list = await session.LoginCharacterListAsync(token);
				var retained = list.Get<List<IReadOnlyDictionary<string, object?>>>("characters")
					.SingleOrDefault(c => Get<int>(c, "objectId") == retainedId)
					?? throw new InvalidDataException($"NI-08 retained character {retainedId} is missing; refusing to create a replacement.");
				if (Get<string>(retained, "name") != characterName || Get<int>(retained, "race") != (int)Race.ASMODIANS ||
					Get<int>(retained, "deletionTimeSeconds") != 0)
					throw new InvalidDataException($"NI-08 retained character {retainedId} identity changed.");
				// The Priest, or the Cleric it became at Ascension (NA-07); for another line, its starter or its second class.
				NaturalJourneyIdentityRules.Classify(line, Get<int>(retained, "playerClass"), Get<ushort>(retained, "level"),
					Get<int>(retained, "mapId"), session.IdentityAltgardLegId);
				session.SelectCharacter(retainedId, characterName);
			}
			else
			{
				await session.LoginAndAuthenticateAsync(token);
				await session.CreateCharacterAsync(token, line.Starter);
			}
			await session.EnterWorldAsync(token);
			if (!resuming) await session.WaitForPrologueAsync(token);
			await session.SynchronizeAsync(token);
			var entered = fixture.World.GetPlayer(session.CharacterId);
			NaturalJourneyIdentityRules.Classify(line, entered.GetPlayerClass(), entered.GetLevel(), entered.GetWorldId(), session.IdentityAltgardLegId);
			if (!resuming) Assert.Equal(line.Starter, entered.GetPlayerClass());
			return resuming;
		}
	}
}
