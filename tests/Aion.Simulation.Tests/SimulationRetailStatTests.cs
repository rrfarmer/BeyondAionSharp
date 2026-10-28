using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Movement;
using Aion.Bots.Scenarios;
using Aion.Bots.Scenarios.Classes;
using Aion.Bots.World;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Retail;
using Aion.GameServer.TestKit;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>Production bootstrap, shipped-population census and isolated
    /// controller-level stat/HP fixtures. No path, AI-choice, natural travel or
    /// level-appropriate balance claim. Actor bases, gear and damage are unedited.</summary>
    [SkippableFact]
    public async Task RetailStatisticsUseSourceBasesAndFinalHpInSim()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM") != "1", "Requires a fresh private scratch database and statistics-only manifest");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR") ?? throw new InvalidOperationException("External SIM evidence directory required");
        string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "retail-statistics";
        using var policy = new SimulationLogPolicy(run, "RETAIL-STATS", fixture.Clock, allowlistPath,
            new SimulationLogPolicyOptions { FailOnWarnings = false }, fixture.LogCapture, fixture.LoggerFactory, evidence: evidence);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(40)); var token = timeout.Token;
        await using var session = new SimulationL0Session(fixture, policy, "b01", 1, "Asimgladiator", Race.ASMODIANS)
            { IdentityClassLine = NaturalClassLine.WarriorGladiator };
        var dashboard = new LiveBotDashboardState();
        await using var monitor = new LiveBotDashboardHost(run, ["RETAIL-STATS"], dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
        session.Dashboard = dashboard;
        Console.WriteLine("Retail statistics SIM monitor: " + monitor.Url);
        using var logs = new ThreeKeysRuntimeLogProvider(Path.Combine(output, "stat-events.jsonl"), () => fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(logs);
        await session.LoginCharacterListAsync(token); session.SelectCharacter(320000, "Asimgladiator");
        await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
        var player = fixture.World.GetPlayer(session.CharacterId);
        Assert.Equal(38, (int)player.GetLevel()); Assert.False(player.IsStaff()); Assert.False(player.IsInvulnerable());
        Assert.DoesNotContain(player.GetEffectController().GetAllEffects(), e => e.GetSkillId() is 240 or 396);
        await File.WriteAllTextAsync(Path.Combine(output, "character.json"), JsonSerializer.Serialize(new
        {
            session.CharacterId, level = player.GetLevel(), hp = player.GetLifeStats().GetCurrentHp(), maxHp = player.GetLifeStats().GetMaxHp(),
            equipment = session.Api.World.Inventory.Values.Where(i => i.Details.EquippedSlot is { } slot && slot != 0)
                .Select(i => new { i.ItemId, i.Details.EquippedSlot }),
            scope = "Unchanged existing ordinary character in a scratch DB; controlled calculation target, not a level-appropriate balance test"
        }), token);
        string? equipmentFixture=InstallExclusiveEquipmentFixture(player,output);
        var wantedMaps = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_MAPS")?.Split(',').Select(int.Parse).ToHashSet();
        var wantedNpcs = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_NPCS")?.Split(',').Select(int.Parse).ToHashSet();
        var members = RetailNpcStatCatalog.Members.Where(m => wantedMaps is null || wantedMaps.Contains(m.MapId))
            .Where(m=>wantedNpcs is null||wantedNpcs.Contains(m.Profile.NpcId))
            .OrderBy(m => m.MapId == 220010000 ? 0 : m.MapId == 220030000 ? 1 : m.MapId).ThenBy(m => m.Profile.NpcId).ToArray();
        Assert.NotEmpty(members);
        var census = new List<object>(); var outcomes = new List<object>(); int deaths = 0;
        await using var progress = new StreamWriter(Path.Combine(output, "fixture-outcomes.jsonl"));
        foreach (var group in members.GroupBy(m => m.MapId))
        {
            // Global conditional IDs are numeric/profile fixtures in an explicit
            // supported world. This does not prove their natural owner/spawn path.
            int fixtureMap=group.Key==0?220010000:group.Key;
            var map = fixture.World.GetWorldMap(fixtureMap);
            var instance = map.GetMainWorldMapInstance();
            foreach (var npc in instance.GetNpcs())
                if (RetailNpcStatCatalog.TryGet(npc, out var selected))
                    census.Add(new { map = fixtureMap, npc = npc.GetNpcId(), obj = npc.GetObjectId(),
                        maxHp = npc.GetLifeStats().GetMaxHp(), expectedHp = selected.MaxHp,
                        pdef = npc.GetGameStats().GetPDef().GetCurrent(), mdef = npc.GetGameStats().GetMDef().GetCurrent(),
                        scoped = npc.GetGameStats().StatisticsProfile is not null });
            // Private component fixtures isolate stat arithmetic from unrelated
            // AI encounters. The shipped census above is retained separately.
            foreach (var npc in instance.GetNpcs().ToArray()) npc.GetController().Delete();
            var spot = group.SelectMany(m => DataManager.SPAWNS_DATA.GetSpawnsForNpc(fixtureMap, m.Profile.NpcId))
                .SelectMany(g => g.GetSpawnTemplates()).FirstOrDefault()
                ?? DataManager.SPAWNS_DATA.GetSpawnsByWorldId(fixtureMap).SelectMany(g=>g.GetSpawnTemplates()).FirstOrDefault();
            bool syntheticAnchor=spot is null;
            // Historical/dynamic memberships can have no authored static spawn.
            // A numeric fixture needs a shared coordinate and the correct map
            // combat context; it makes no placement, travel or collision claim.
            spot ??= Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(fixtureMap,group.First().Profile.NpcId,
                Math.Max(64,map.GetWorldSize()/2f),Math.Max(64,map.GetWorldSize()/2f),
                Math.Max(map.GetWaterLevel(),map.GetDeathLevel())+100,0);
            bool positioned = false;
            foreach (var member in group)
            {
                var p = member.Profile;
                if (player.IsDead())
                {
                    await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
                    await session.SendPacketAsync(session.Api.Revive(), token);
                    await session.SynchronizeAsync(token); Assert.False(player.IsDead());
                    positioned = false;
                }
                if (!positioned || player.GetWorldId()!=fixtureMap)
                {
                    // Reuse one controlled stand per map. Repeated identical
                    // teleports/moves per NPC are not part of a stat fixture.
                    await TeleportForSetupAsync(session, player, fixtureMap, spot.GetX() + .5f, spot.GetY(), spot.GetZ(), token);
                    await session.SynchronizeAsync(token);
                    if (player.IsProtectionActive())
                    {
                        // This controlled stand tests combat, not arrival
                        // protection. Use its existing owner to end it, exactly
                        // as the move/skill path does, without travel or a60s
                        // whole-world AI advance for every fixture revive.
                        player.GetController().StopProtectionActiveTask();
                        await session.SynchronizeAsync(token);
                        Assert.False(player.IsProtectionActive());
                    }
                    positioned = true;
                }
                // A nonthinking NPC AI isolates this component fixture from
                // custom encounter scripts while retaining normal controller APIs.
                // Production templates/AI/skill lists remain unchanged.
                var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(fixtureMap, p.NpcId,
                    spot.GetX(), spot.GetY(), spot.GetZ(), spot.GetHeading(), null, "retail_statistics_fixture");
                var npc = Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, instance.GetInstanceId()));
                try
                {
                    var s = npc.GetGameStats();
                    Assert.Same(p, s.StatisticsProfile); Assert.Null(s.RetailProfile);
                    Assert.False(SkyTempleNpcProfileCatalog.TryGet(npc, out _));
                    // Selected MAXHP getters retain the native integer cache.
                    // A binary32 round trip loses HP on large world bosses.
                    int materializedHp=p.MaxHp;
                    Assert.Equal(p.Level, (int)npc.GetLevel()); Assert.Equal(materializedHp, npc.GetLifeStats().GetCurrentHp());
                    Assert.Equal(materializedHp, npc.GetLifeStats().GetMaxHp()); Assert.Equal(p.PhysicalDefense, s.GetPDef().GetCurrent());
                    Assert.Equal(p.MagicalDefense, s.GetMDef().GetCurrent()); Assert.Equal(p.MagicalResist, s.GetMResist().GetCurrent());
                    Assert.Equal(p.Values["physical_critical_reduce_rate"],s.GetPCR().GetCurrent());
                    Assert.Equal(p.Values["magical_critical_reduce_rate"],s.GetMCR().GetCurrent());
                    var host = npc.GetObjectTemplate().GetStatsTemplate();
                    Assert.Equal(host.GetRunSpeed(), s.GetStatsTemplate().GetRunSpeed());
                    Assert.Equal(host.GetWalkSpeed(), s.GetStatsTemplate().GetWalkSpeed());
                    // Some friendly guards are authored sitting. A normal combat
                    // AI would stand them when attacked; this isolated fixture
                    // wakes its actor explicitly instead of changing that AI.
                    bool seated = npc.IsInState(CreatureState.RESTING);
                    if (seated) npc.UnsetState(CreatureState.RESTING);
                    int start = fixture.LogCapture.Entries.Count, npcBefore = npc.GetLifeStats().GetCurrentHp();
                    // Reverse-direction source defense through the same damage
                    // calculator and standard HP owner; no faction/quest edit.
                    // Exercise the production dispatcher, including its selected
                    // NPC receiver branch, rather than a calculator-only helper.
                    var outgoing = player.GetAttackType() == ItemAttackType.PHYSICAL
                        ? AttackUtil.CalculatePhysAttackResult(player, npc, new HashSet<CalculationType>())
                        : AttackUtil.CalculateMagAttackResult(player, npc, player.GetAttackType().GetMagicalElement(), new HashSet<CalculationType>());
                    bool incomingPhysical=player.GetAttackType()==Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL;
                    if((incomingPhysical?p.PhysicalDamageTrim:p.MagicalDamageTrim)==1)
                        Assert.All(outgoing,r=>Assert.InRange(r.GetDamage(),0,1));
                    npc.GetController().OnAttack(player, outgoing.Sum(x => x.GetDamage()), outgoing[0].GetAttackStatus());
                    int npcAfter = npc.GetLifeStats().GetCurrentHp();
                    Assert.Equal(Math.Min(npcBefore, outgoing.Sum(x => x.GetDamage())), npcBefore - npcAfter);
                    // Use a separate fresh attacker. Even a one-shot NPC must
                    // exercise the reverse defense calculation while the player
                    // is alive, and a killed defender cannot perform its auto.
                    npc.GetController().Delete();
                    npc = Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, instance.GetInstanceId()));
                    npc.UnsetState(CreatureState.RESTING);
                    int playerBefore = player.GetLifeStats().GetCurrentHp();
                    int attempts = 0;
                    do
                    {
                        npc.GetController().AttackTarget(player, 0, false);
                        attempts++;
                    } while (p.HasNormalAttackCorrection && player.GetLifeStats().GetCurrentHp() == playerBefore && attempts < 64);
                    int playerAfter = player.GetLifeStats().GetCurrentHp(); if (player.IsDead()) deaths++;
                    var events = fixture.LogCapture.Entries.Skip(start).Select(e => e.Message)
                        .Where(m => m.StartsWith("RETAIL_SKYTEMPLE_CALC ") || m.StartsWith("RETAIL_SKYTEMPLE_HP ") || m.StartsWith("RETAIL_STAT_DUAL_OFFHAND ") || m.StartsWith("RETAIL_STAT_NORMAL_INPUT ")).ToArray();
                    if (p.HasNormalAttackCorrection)
                    {
                        Assert.True(playerAfter < playerBefore, $"NPC {p.NpcId} produced no final HP loss in {attempts} normal attacks");
                        Assert.Contains(events, m => m.StartsWith("RETAIL_SKYTEMPLE_CALC ") && m.Contains("npc=" + p.NpcId + " "));
                        foreach (var message in events.Where(m => m.StartsWith("RETAIL_SKYTEMPLE_HP ")))
                        {
                            var fields = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                                .Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
                            Assert.Equal(Math.Min(int.Parse(fields["before"]), Math.Max(0, int.Parse(fields["requested"]))),
                                int.Parse(fields["before"]) - int.Parse(fields["after"]));
                        }
                    }
                    else
                    {
                        Assert.Equal(playerBefore,playerAfter);
                        Assert.DoesNotContain(events,m=>m.StartsWith("RETAIL_SKYTEMPLE_CALC "));
                    }
                    if(p.HasNormalAttackCorrection)
                    {
                        var mains=events.Where(m=>m.StartsWith("RETAIL_SKYTEMPLE_CALC ")).ToArray();
                        Assert.Equal(attempts, mains.Length);
                        bool dual=p.Source.Integers["dual"].ParsedValue is not (null or 0);
                        Assert.Equal(dual ? mains.Count(m=>!m.Contains("status=DODGE ")&&!m.Contains("status=RESIST ")) : 0,
                            events.Count(m=>m.StartsWith("RETAIL_STAT_DUAL_OFFHAND ")));
                    }
                    var outcome = new { map = group.Key, fixtureMap, syntheticAnchor, npc = p.NpcId, p.MaxHp, p.MinDamage, p.MaxDamage,
                        p.PhysicalDefense, p.MagicalDefense, p.MagicalResist, p.Attacks, p.SourceRecordSha256,
                        p.PhysicalDamageTrim,p.MagicalDamageTrim,
                        incomingRecords=outgoing.Select(x=>new {damage=x.GetDamage(),status=x.GetAttackStatus().ToString()}).ToArray(),
                        materializedHp,hpRoundingDelta=materializedHp-p.MaxHp,binary32HpProjection=(int)(float)p.MaxHp,
                        physicalCriticalResist=s.GetPCR().GetCurrent(),magicalCriticalResist=s.GetMCR().GetCurrent(),
                        omittedFixturePostSpawnSkills=npc.GetSkillList().GetPostSpawnSkills().Select(x=>new {id=x.GetSkillId(),level=x.GetSkillLevel()}).ToArray(),
                        normalCorrection = p.HasNormalAttackCorrection, attempts, playerBefore, playerAfter,
                        npcBefore, npcAfter, fixtureWokeSittingActor = seated, events };
                    outcomes.Add(outcome);
                    await progress.WriteLineAsync(JsonSerializer.Serialize(outcome)); await progress.FlushAsync(token);
                }
                finally { npc.GetController().Delete(); }
                if (outcomes.Count % 25 == 0) Console.WriteLine($"Retail stat fixtures: {outcomes.Count}/{members.Length}; real character deaths={deaths}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "shipped-census.json"), JsonSerializer.Serialize(census), token);
        await File.WriteAllTextAsync(Path.Combine(output, "acceptance.json"), JsonSerializer.Serialize(new
        {
            members = members.Length, tested = outcomes.Count, deaths, productionStatisticsBootstrap = true,equipmentFixture,
            scope = "Controlled unbuffed stat/normal/controller HP fixtures; arrival protection ended by its existing controller owner; no routing, source AI, natural combat or balance acceptance",
            census, outcomes, warnings = fixture.LogCapture.Entries.Where(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning).Select(e => e.Message)
        }), token);
        Assert.Equal(members.Length, outcomes.Count); policy.AssertClean();
    }
}

[Aion.GameServer.Ai.AIName("retail_statistics_fixture")]
public sealed class RetailStatisticsFixtureAI : Aion.GameServer.Ai.NpcAI
{
    public RetailStatisticsFixtureAI(Npc owner) : base(owner) { }
    public override bool CanThink() => false;
    public override void Think() { }
    // This is an explicitly unbuffed numeric fixture, not a production NPC AI.
    // NpcAI.HandleSpawned casts post-spawn skills even when CanThink is false.
    // Real population actors keep that behavior and are captured in the census.
    protected override void HandleSpawned() => SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
    protected override void HandleAttack(Creature creature) { }
}
