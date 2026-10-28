using System.Text.Json;
using Aion.Commons.Logging;
using Aion.Bots.Dashboard;
using Aion.Bots.Navigation;
using Aion.Bots.Movement;
using Aion.Bots.Protocol;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.Tracing;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Movement;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Retail;
using Aion.GameServer.TestKit;
using Microsoft.Extensions.Logging;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>
    /// Controlled full-bootstrap SIM encounters, not calculator fixtures or a claim of client rendering.
    /// Uses an isolated copy of the existing normal-access character, unchanged gear/skills/quests,
    /// complete shipped instance spawns, production AI/movement/HP and ordinary client packets.
    /// Only player starting positions are director setup; room traversal is a separate acceptance gate.
    /// </summary>
    [SkippableFact]
    public async Task ThreeKeysRetailCohortRunsActualSpawnCombatAndMovementThroughClientPackets()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_THREE_KEYS_SIM") != "1",
            "Requires the retained private Three Keys character clone and complete retail package.");
        Assert.True(SkyTempleNpcProfileCatalog.Enabled);
        string output = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")
            ?? throw new InvalidOperationException("Three Keys SIM requires an external evidence directory.");
        Directory.CreateDirectory(output);
        string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "tk25-sim";
        // Keep the rejected retail-authored B123 route visible. Its template fingerprint
        // covers other failures too, so every check also validates the full diagnostic.
        var allowances = JsonSerializer.Deserialize<List<LogProblemAllowlistEntry>>(
            File.ReadAllText(evidence?.AllowlistPath ?? allowlistPath))!;
        allowances.Add(new LogProblemAllowlistEntry
        {
            Fingerprint = "208f55be", Owner = "retail-mob-discovery", Tracking = "docs/retail-mob-system.md#current-three-keys-checkpoint",
            Reason = "Supplied B123 waypoint 13 selects native NoGround; TK25 separately checks the exact NPC, route, point and failure. Whole route remains rejected.",
            Modes = ["SIM"], Servers = ["gs"], Scenarios = ["TK25"], MaxCount = 1, Expires = new DateOnly(2027, 10, 24)
        });
        string localAllowlist = Path.Combine(output, "tk25-log-allowlist.json");
        await File.WriteAllTextAsync(localAllowlist, JsonSerializer.Serialize(allowances));
        using var policy = new SimulationLogPolicy(run, "TK25", fixture.Clock, localAllowlist,
            captureProvider: fixture.LogCapture, loggerFactory: fixture.LoggerFactory, evidence: evidence);
        void AssertThreeKeysLogClean()
        {
            foreach (var problem in policy.SnapshotProblems().Where(p => p.Fingerprint == "208f55be"))
                Assert.True(ThreeKeysRetailDiagnostics.IsVerifiedSourceRouteRejection(problem), problem.Message);
            policy.AssertClean();
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        CancellationToken token = timeout.Token;
        string tracePath = Path.Combine(output, "combat.trace.jsonl");
        using var trace = BotActionTraceWriter.Open(tracePath, run, "b01", "sim-player-1",
            virtualTime: () => TimeSpan.FromMilliseconds(fixture.Clock.NowMillis));
        await using var session = new SimulationL0Session(fixture, policy, "b01", 1,
            "Asimgladiator", Race.ASMODIANS, trace, tracePath)
        { IdentityClassLine = NaturalClassLine.WarriorGladiator };
        var dashboard = new LiveBotDashboardState();
        await using var dashboardHost = new LiveBotDashboardHost(run, ["TK25"], dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
        session.Dashboard = dashboard;
        using var runtimeLog=new ThreeKeysRuntimeLogProvider(Path.Combine(output,"cohort-events.jsonl"),()=>fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(runtimeLog);
        Console.WriteLine($"TK25 monitor: {dashboardHost.Url}");
        await session.LoginCharacterListAsync(token);
        session.SelectCharacter(320000, "Asimgladiator");
        await session.EnterWorldAsync(token);
        await session.SynchronizeAsync(token);
        Aion.GameServer.Model.GameObjects.Players.Player Player() => fixture.World.GetPlayer(session.CharacterId);
        Assert.Equal(PlayerClass.GLADIATOR, Player().GetPlayerClass());
        Assert.Equal(38, (int)Player().GetLevel());
        Assert.False(Player().IsStaff());
        Assert.False(Player().IsInvulnerable());
        var startingEffects=Player().GetEffectController().GetAllEffects().Select(e=>new
            {id=e.GetSkillId(),name=e.GetSkillTemplate().GetName(),activation=e.GetSkillTemplate().GetActivationAttribute().ToString()}).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output,"character-effects-at-start.json"),JsonSerializer.Serialize(startingEffects),token);
        Assert.DoesNotContain(startingEffects,e=>e.id==240||e.name?.StartsWith("GM",StringComparison.OrdinalIgnoreCase)==true);
        Assert.InRange(Player().GetLifeStats().GetCurrentHp(),1,Player().GetLifeStats().GetMaxHp());
        var equipment = session.Api.World.Inventory.Values.Where(i => i.Details.EquippedSlot is { } slot && slot != 0)
            .Select(i => new { i.ObjectId, i.ItemId, i.Details.EquippedSlot }).OrderBy(i => i.ObjectId).ToArray();
        Assert.NotEmpty(equipment);
        await File.WriteAllTextAsync(Path.Combine(output, "character-at-start.json"), JsonSerializer.Serialize(new
        {
            session.CharacterId, name = Player().GetName(), level = Player().GetLevel(),
            hp = Player().GetLifeStats().GetCurrentHp(), maxHp = Player().GetLifeStats().GetMaxHp(),
            maxFp=session.Api.World.MaxFlightTime,movementSpeed=session.Api.World.MovementSpeed,
            attackSpeed=Player().GetGameStats().GetAttackSpeed().GetCurrent(),staff=Player().IsStaff(),
            equipment, skills = session.Api.World.Skills,
            setup = "Cloned pre-play character; retained GM Armor240 removed from scratch copy; equipment, learned skills and quests unchanged. No NPC HP/damage edits. Player placement only.",
        }), token);

        // Labelled setup bypasses the outdoor approach, not the actual portal or instance creation.
        int firstPortal=Environment.GetEnvironmentVariable("AION_THREE_KEYS_MODE")=="rooms"?730041:730043;
        Npc portal = fixture.World.GetWorldMap(220020000).GetMainWorldMapInstance().GetNpcs(firstPortal).Single();
        session.BeginStep("entry-setup", "place-copied-player-beside-shipped-first-portal");
        await TeleportForSetupAsync(session, Player(), 220020000,
            portal.GetX() + 1, portal.GetY(), portal.GetZ(), token);
        await session.SynchronizeAsync(token);
        if(Environment.GetEnvironmentVariable("AION_THREE_KEYS_MODE")=="rooms")
        {
            BotNavigationGeometry RoomGeometry()=>BotNavigationGeometry.ForServerWorld(Player().GetInstanceId(),Race.ASMODIANS);
            var roomRuntime=new NaturalJourneyRuntime(RealStaticData.RepoRoot(),"SIM-TK25-ROOMS",fixture.Seed,
                fixture.DataManager.StaticData,()=>fixture.Clock.NowMillis,fixture.Epoch,RoomGeometry,
                _=>Task.FromResult(false),AssertThreeKeysLogClean,()=>policy.SnapshotProblems(),trace,dashboard);
            await RunThreeKeysRoomsAsync(session,roomRuntime,output,token);
            AssertThreeKeysLogClean();return;
        }
        int entryStart = session.PacketHistory.Count;
        Assert.True(await NaturalAltgardQuestSteps.UseObjectAsync(session, portal.GetObjectId(), null, token, reloadWorld: true));
        if (!session.PacketHistory.Skip(entryStart).Any(p => p.PacketType == typeof(SM_PLAYER_SPAWN)))
            await session.WaitForPacketAsync(typeof(SM_PLAYER_SPAWN), token);
        if (!session.PacketHistory.Skip(entryStart).Any(p => p.PacketType == typeof(SM_PLAYER_INFO)))
            await session.WaitForPacketAsync(typeof(SM_PLAYER_INFO), token);
        session.AcceptTeleportPosition();
        await session.SynchronizeAsync(token);
        Assert.Equal(320050000, Player().GetWorldId());
        int instanceId = Player().GetInstanceId();
        var instance = Player().GetWorldMapInstance();
        BotPosition At(VisibleObject obj) => new(obj.GetX(), obj.GetY(), obj.GetZ(), obj.GetHeading());
        var entry = session.CurrentPosition;
        BotNavigationGeometry Geometry() => BotNavigationGeometry.ForServerWorld(instanceId, Race.ASMODIANS);
        var runtime = new NaturalJourneyRuntime(RealStaticData.RepoRoot(), "SIM-TK25", fixture.Seed,
            fixture.DataManager.StaticData, () => fixture.Clock.NowMillis, fixture.Epoch, Geometry,
            _ => Task.FromResult(false), AssertThreeKeysLogClean, () => policy.SnapshotProblems(), trace, dashboard);
        var journey = new NaturalIshalgenJourney(session, runtime,
            new NaturalJourneyOptions(ClassLine: NaturalClassLine.WarriorGladiator));
        int[] cohort = Enumerable.Range(212761, 22).Concat(new[] { 212833, 212835, 212876 }).ToArray();
        var initial = instance.GetNpcs().Where(n => cohort.Contains(n.GetNpcId())).Select(n =>
        {
            Assert.True(SkyTempleNpcProfileCatalog.TryGet(n, out var profile));
            Assert.Equal(profile.MaxHp, n.GetLifeStats().GetMaxHp());
            Assert.Equal(profile.MaxHp, n.GetLifeStats().GetCurrentHp());
            return new { npc = n.GetNpcId(), obj = n.GetObjectId(), hp = n.GetLifeStats().GetCurrentHp(),
                maxHp = n.GetLifeStats().GetMaxHp(), position = At(n), profile.SourceRecordSha256 };
        }).ToArray();
        Assert.Equal(cohort, initial.Select(n => n.npc).Distinct().Order().ToArray());
        await File.WriteAllTextAsync(Path.Combine(output, "all-instance-spawns.json"), JsonSerializer.Serialize(initial), token);

        int[] selected = (Environment.GetEnvironmentVariable("AION_THREE_KEYS_NPCS") ?? string.Join(',', cohort))
            .Split(',').Select(int.Parse).Distinct().ToArray();
        Assert.All(selected, id => Assert.Contains(id, cohort));
        if(Environment.GetEnvironmentVariable("AION_THREE_KEYS_MODE")=="movement")
        {
            await RunThreeKeysMovementChecksAsync(session,runtime,selected,output,token);
            AssertThreeKeysLogClean();return;
        }
        var outcomes = new List<object>();
        var failed = new List<int>();
        using var observations = new StreamWriter(new FileStream(Path.Combine(output, "actor-observations.jsonl"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
        int firstLog = fixture.LogCapture.Entries.Count;
        try
        {
            foreach (int id in selected)
            {
                Npc[] choices = instance.GetNpcs(id).Where(n => n.IsSpawned() && !n.IsDead() &&
                    n.GetLifeStats().GetCurrentHp() == n.GetLifeStats().GetMaxHp()).ToArray();
                var candidates = choices.SelectMany(n => Geometry().GroundAround(320050000, At(n), [8f, 7f, 6f, 10f])
                    .Where(p => Geometry().HasLineOfSight(320050000, p, At(n)))
                    .Select(p => new { Npc = n, Stand = p, Neighbours = instance.GetNpcs().Count(other => other != n &&
                        !other.IsDead() && cohort.Contains(other.GetNpcId()) && NaturalFlightPolicy.Distance(p, At(other)) < 15) }))
                    .OrderBy(c => c.Neighbours).ToArray();
                if (candidates.Length == 0)
                {
                    outcomes.Add(new { npc = id, failure = "No full-HP shipped spawn with a checked same-floor starting point." });
                    failed.Add(id); continue;
                }
                var candidate = candidates[0];
                Npc victim = candidate.Npc;
                long lastSample = -1;
                BotPosition previous = At(victim);
                float moved = 0;
                int packetStart = session.PacketHistory.Count, logStart = fixture.LogCapture.Entries.Count;
                session.AfterSynchronize = () =>
                {
                    if (fixture.Clock.NowMillis == lastSample) return;
                    lastSample = fixture.Clock.NowMillis;
                    BotPosition position = At(victim);
                    moved += NaturalFlightPolicy.Distance(previous, position); previous = position;
                    var nav = ((NpcMoveController)victim.GetMoveController()).RetailNavigation;
                    observations.WriteLine(JsonSerializer.Serialize(new
                    {
                        t = fixture.Clock.NowMillis, npc = id, obj = victim.GetObjectId(), position,
                        hp = victim.GetLifeStats().GetCurrentHp(), maxHp = victim.GetLifeStats().GetMaxHp(),
                        ai = victim.GetAi().GetState().ToString(), nav.RemainingWaypoints, nav.LastFailure, nav.IsReturning,
                        canAttack = victim.CanAttack(), casting = victim.GetCastingSkill()?.GetSkillId(), state = victim.GetState(),
                        player = session.CurrentPosition, playerHp = Player().GetLifeStats().GetCurrentHp(),
                        serverPlayer = At(Player()),
                        map = Player().GetWorldId(), instance = Player().GetInstanceId(),
                    }));
                };
                NaturalCombatDiagnosticResult? result = null;
                string? failure = null;
                try
                {
                    // Rest at the real landing with ordinary abilities before each labelled encounter placement.
                    await TeleportForSetupAsync(session, Player(), 320050000, entry.X, entry.Y, entry.Z, token, instanceId);
                    await session.SynchronizeAsync(token);
                    session.BeginStep($"fight-{id}", "normal-copied-character-against-full-HP-shipped-NPC");
                    result = await journey.RunObservedCombatAsync(async _ =>
                    {
                        ushort taunt=checked((ushort)session.Api.World.Skills.Keys.Where(k=>k is >=2981 and <=2986).Max());
                        var tauntWait=session.Api.Timing.TimeUntilCast(taunt);
                        if(tauntWait>TimeSpan.Zero)
                        {await session.AdvanceAsync(tauntWait+TimeSpan.FromMilliseconds(1),token);await session.SynchronizeAsync(token);}
                        await TeleportForSetupAsync(session, Player(), 320050000,
                            candidate.Stand.X, candidate.Stand.Y, candidate.Stand.Z, token, instanceId);
                        await session.SynchronizeAsync(token);
                        Assert.Contains(victim.GetObjectId(), session.Api.World.Objects.Keys);
                        int observeMs=int.Parse(Environment.GetEnvironmentVariable("AION_THREE_KEYS_OBSERVE_MS")??"0");
                        if(observeMs>0)
                        {
                            // Learn nothing and grant nothing: use this character's already
                            // learned Taunt to expose real incoming attacks before the kill.
                            await session.SendPacketAsync(session.Api.Target(victim.GetObjectId()),token);
                            await session.SendPacketAsync(session.Api.Cast(runtime.CreateSpellCast(session.Api.World,
                                session.CurrentPosition,taunt,checked((byte)session.Api.World.Skills[taunt].Level),victim.GetObjectId())),token);
                            for(int elapsed=0;elapsed<observeMs&&!session.Api.World.IsDead;elapsed+=200)
                            {await session.AdvanceAsync(TimeSpan.FromMilliseconds(200),token);await session.SynchronizeAsync(token);}
                            Assert.False(session.Api.World.IsDead,"Copied character died during passive incoming-attack observation.");
                            // A short checked lateral walk makes the chase track a genuinely
                            // moving target. Movement frames remain speed-paced client packets.
                            var origin=session.CurrentPosition;
                            var lateral=Geometry().GroundAround(320050000,origin,[3f,4f])
                                .Select(p=>Geometry().TraceEdge(320050000,origin,p))
                                .FirstOrDefault(path=>path is {Count:>1});
                            if(lateral is not null)
                            {
                                var plan=new BotMover(session.Api.World,session.Api.Timing).CreateGroundPlan(lateral,
                                    origin,session.Api.World.MovementSpeed??throw new InvalidDataException("Player movement speed unobserved."));
                                await session.ExecuteMovementAsync(plan,token);
                                await session.SynchronizeAsync(token);
                            }
                        }
                        return victim.GetObjectId();
                    }, token);
                    if (!result.Killed || !victim.IsDead()) failed.Add(id);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                { failure = error.ToString(); failed.Add(id); }
                finally { session.AfterSynchronize = null; }
                var packets = session.PacketHistory.Skip(packetStart).Where(p => p.PacketType == typeof(SM_ATTACK) ||
                    p.PacketType == typeof(SM_CASTSPELL) || p.PacketType == typeof(SM_CASTSPELL_RESULT))
                    .Select(p => new { packet = p.PacketType.Name, p.Fields }).ToArray();
                var logs = fixture.LogCapture.Entries.Skip(logStart).Where(l => l.Message.Contains("RETAIL_SKYTEMPLE_"))
                    .Select(l => new { l.Category, l.Level, l.Message }).ToArray();
                var outcome = new { npc = id, obj = victim.GetObjectId(), candidate.Stand, candidate.Neighbours,
                    maxHp = victim.GetLifeStats().GetMaxHp(), result, failure, moved, packets, logs,
                    boundary = "Controlled SIM starting positions; natural client packets and unmodified mobs; no rendering/complete traversal claim." };
                outcomes.Add(outcome);
                await File.WriteAllTextAsync(Path.Combine(output, $"npc-{id}.json"), JsonSerializer.Serialize(outcome), token);
                Console.WriteLine($"TK25 {id}: killed={result?.Killed}; deaths={result?.Deaths}; moved={moved:F2}; failure={failure}");
            }
        }
        finally
        {
            session.AfterSynchronize = null;
            await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(new
            {
                mode = "SIM", selected, fullCohort = cohort, instanceId, spawnCount = initial.Length,
                outcomes, failed, setup = "Player encounter placement only; no NPC/stat/gear/quest edits.",
                all25ClientAcceptance = false, next = "Inspect actual attack/movement evidence and exercise explicit obstacle/return/regen coverage.",
            }));
            var logs = fixture.LogCapture.Entries.Skip(firstLog).Where(l => l.Message.Contains("RETAIL_SKYTEMPLE_") || l.Exception != null);
            await File.WriteAllLinesAsync(Path.Combine(output, "retail-runtime.jsonl"), logs.Select(l => JsonSerializer.Serialize(new
                { l.Category, l.Level, l.Message, exception = l.Exception?.ToString() })));
        }
        Assert.Empty(failed);
        AssertThreeKeysLogClean();
    }

    // Packet-drain timestamps are receipt times. Capture actual server log calls
    // on the SIM clock so cast-end/emission/HP timing can be audited independently.
    private sealed class ThreeKeysRuntimeLogProvider(string path,Func<long> clock):ILoggerProvider
    {
        private readonly object gate=new();
        private readonly Func<long> timestamp=clock;
        private readonly StreamWriter writer=new(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){AutoFlush=true};
        private bool disposed;
        public ILogger CreateLogger(string categoryName)=>new Logger(this,categoryName);
        public void Dispose(){lock(gate){if(disposed)return;disposed=true;writer.Dispose();}}
        private sealed class Logger(ThreeKeysRuntimeLogProvider owner,string category):ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;
            public bool IsEnabled(LogLevel level)=>level!=LogLevel.None;
            public void Log<TState>(LogLevel level,EventId eventId,TState state,Exception? exception,Func<TState,Exception?,string> formatter)
            {
                string message=formatter(state,exception);
                if(!message.Contains("RETAIL_SKYTEMPLE_")&&!message.StartsWith("RETAIL_STAT_")&&!message.StartsWith("SkyTemple navigation ")&&!message.StartsWith("SkyTemple chase ")&&!message.StartsWith("SkyTemple return "))return;
                lock(owner.gate)
                {
                    if(owner.disposed)return;
                    owner.writer.WriteLine(JsonSerializer.Serialize(new{t=owner.timestamp(),category,level=level.ToString(),message,exception=exception?.ToString()}));
                }
            }
        }
    }
}
