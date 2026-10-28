using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    /// <summary>Real character stats, source-selected targets and existing damage/
    /// HP owners. Direct effect initialization deliberately tests common-skill
    /// arithmetic independently of cast admission, faction and encounter AI.</summary>
    [SkippableFact]
    public async Task RetailStatisticsIncomingMagicCommitsSourceTrimInSim()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_INCOMING_SIM") != "1",
            "Requires a fresh private scratch database and statistics manifest");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR") ?? throw new InvalidOperationException("External SIM evidence required");
        string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID") ?? "retail-stat-incoming";
        using var policy = new SimulationLogPolicy(run, "RETAIL-STAT-INCOMING", fixture.Clock, allowlistPath,
            new SimulationLogPolicyOptions { FailOnWarnings = false }, fixture.LogCapture, fixture.LoggerFactory, evidence: evidence);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30)); var token = timeout.Token;
        await using var session = new SimulationL0Session(fixture, policy, "b01", 1, "Farmer", Race.ASMODIANS)
            { IdentityClassLine = NaturalClassLine.MageSorcerer };
        var dashboard = new LiveBotDashboardState();
        await using var monitor = new LiveBotDashboardHost(run, ["RETAIL-STAT-INCOMING"], dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
        session.Dashboard = dashboard; Console.WriteLine("Retail incoming magic SIM monitor: " + monitor.Url);
        using var logs = new ThreeKeysRuntimeLogProvider(Path.Combine(output, "incoming-events.jsonl"), () => fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(logs);
        await session.LoginCharacterListAsync(token); session.SelectCharacter(104728, "Farmer");
        await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
        var player = fixture.World.GetPlayer(session.CharacterId);
        Assert.False(player.IsStaff()); Assert.False(player.IsInvulnerable());
        Assert.NotEqual(Aion.GameServer.Model.Templates.Items.ItemAttackType.PHYSICAL, player.GetAttackType());
        Assert.DoesNotContain(player.GetEffectController().GetAllEffects(), e => e.GetSkillId() is 240 or 396);
        var learned = player.GetSkillList().GetSkillEntry(1285); // Existing single-effect Flame Bolt IV; no new skills or gear.
        Assert.NotNull(learned);
        var skill = learned.GetSkillTemplate();
        Assert.Single(skill.GetEffects()!.GetEffects());
        await File.WriteAllTextAsync(Path.Combine(output, "character.json"), JsonSerializer.Serialize(new
        {
            session.CharacterId, level = player.GetLevel(), attackType = player.GetAttackType().ToString(),
            skill = learned.GetSkillId(), skillLevel = learned.GetSkillLevel(),
            equipment = session.Api.World.Inventory.Values.Where(i => i.Details.EquippedSlot is { } slot && slot != 0)
                .Select(i => new { i.ItemId, i.Details.EquippedSlot }),
            scope = "Unchanged mage and gear; direct common effect and normal calculation/HP component checks; no cast admission, natural encounter or balance claim"
        }), token);
        var maps = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_MAPS")?.Split(',').Select(int.Parse).ToHashSet();
        var ids = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_SIM_NPCS")?.Split(',').Select(int.Parse).ToHashSet();
        var members = RetailNpcStatCatalog.Members.Where(m => m.MapId != 0 && m.Profile.MagicalDamageTrim == 1)
            .Where(m => maps is null || maps.Contains(m.MapId)).Where(m => ids is null || ids.Contains(m.Profile.NpcId))
            .OrderBy(m => m.MapId).GroupBy(m => m.Profile.NpcId).Select(g => g.First()).ToArray();
        Assert.NotEmpty(members);
        var outcomes = new List<object>(); var blocked = new List<int>();
        await using var progress = new StreamWriter(Path.Combine(output, "incoming-outcomes.jsonl"));
        foreach (var group in members.GroupBy(m => m.MapId))
        {
            var map = fixture.World.GetWorldMap(group.Key); var instance = map.GetMainWorldMapInstance();
            foreach (var npc in instance.GetNpcs().ToArray()) npc.GetController().Delete();
            var spot = group.SelectMany(m => DataManager.SPAWNS_DATA.GetSpawnsForNpc(group.Key, m.Profile.NpcId))
                .SelectMany(g => g.GetSpawnTemplates()).FirstOrDefault()
                ?? DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(g => g.GetSpawnTemplates()).FirstOrDefault();
            bool syntheticAnchor = spot is null;
            spot ??= Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key, group.First().Profile.NpcId,
                Math.Max(64, map.GetWorldSize() / 2f), Math.Max(64, map.GetWorldSize() / 2f),
                Math.Max(map.GetWaterLevel(), map.GetDeathLevel()) + 100, 0);
            await TeleportForSetupAsync(session, player, group.Key, spot.GetX() + .5f, spot.GetY(), spot.GetZ(), token);
            await session.SynchronizeAsync(token);
            if (player.IsProtectionActive()) player.GetController().StopProtectionActiveTask();
            foreach (var member in group)
            {
                var p = member.Profile; var trials = new List<object>(); bool normalPositive = false, skillPositive = false;
                foreach (bool commonSkill in new[] { false, true })
                {
                    for (int trial = 0; trial < 256; trial++)
                    {
                        token.ThrowIfCancellationRequested();
                        var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key, p.NpcId,
                            player.GetX() - .5f, player.GetY(), player.GetZ(), spot.GetHeading(), null, "retail_statistics_fixture");
                        var npc = Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, instance.GetInstanceId()));
                        try
                        {
                            Assert.Same(p, npc.GetGameStats().StatisticsProfile); npc.UnsetState(CreatureState.RESTING);
                            int before = npc.GetLifeStats().GetCurrentHp(), start = fixture.LogCapture.Entries.Count, requested;
                            string status;
                            if (commonSkill)
                            {
                                var effect = new Effect(player, npc, skill, learned.GetSkillLevel());
                                effect.Initialize();
                                Assert.Equal(before, npc.GetLifeStats().GetCurrentHp());
                                requested = effect.IsInSuccessEffects(1) ? effect.GetReserveds(1).GetValue() : 0;
                                status = effect.GetAttackStatus().ToString();
                                effect.ApplyEffect();
                            }
                            else
                            {
                                Assert.True(SkyTemplePlayerCombat.TryNormal(player, npc, new HashSet<CalculationType>(), out var hits, out var element));
                                Assert.NotEqual(SkillElement.NONE, element);
                                Assert.All(hits, hit => Assert.InRange(hit.GetDamage(), 0, 1));
                                requested = hits.Sum(hit => hit.GetDamage()); status = hits[0].GetAttackStatus().ToString();
                                npc.GetController().OnAttack(player, requested, hits[0].GetAttackStatus());
                            }
                            int after = npc.GetLifeStats().GetCurrentHp();
                            Assert.InRange(requested, 0, 1); Assert.Equal(Math.Min(before, requested), before - after);
                            var events = fixture.LogCapture.Entries.Skip(start).Select(e => e.Message)
                                .Where(m => m.StartsWith("RETAIL_SKYTEMPLE_HP ")).ToArray();
                            trials.Add(new { commonSkill, trial, requested, before, after, status, events });
                            if (requested > 0)
                            {
                                Assert.Equal(1, before - after);
                                Assert.Contains(events, e => e.Contains("target=" + npc.GetObjectId() + " ")
                                    && e.Contains("requested=1 ") && e.Contains("actualLoss=1"));
                                if (commonSkill) skillPositive = true; else normalPositive = true;
                                break;
                            }
                        }
                        finally { npc.GetController().Delete(); }
                    }
                }
                if (!normalPositive || !skillPositive) blocked.Add(p.NpcId);
                var outcome = new { map = group.Key, npc = p.NpcId, p.SourceRecordSha256, p.MagicalDamageTrim,
                    syntheticAnchor, normalPositive, skillPositive, trials };
                outcomes.Add(outcome); await progress.WriteLineAsync(JsonSerializer.Serialize(outcome)); await progress.FlushAsync(token);
                if (outcomes.Count % 25 == 0) Console.WriteLine($"Incoming magic fixtures: {outcomes.Count}/{members.Length}");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(output, "incoming-acceptance.json"), JsonSerializer.Serialize(new
        {
            expected = members.Length, tested = outcomes.Count, blocked, outcomes,
            scope = "Mapped target trims through real magical normal and common single-effect skill numeric/HP owners; direct effects do not prove legal casting or encounter behavior"
        }), token);
        Assert.Empty(blocked); Assert.Equal(members.Length, outcomes.Count); policy.AssertClean();
    }
}
