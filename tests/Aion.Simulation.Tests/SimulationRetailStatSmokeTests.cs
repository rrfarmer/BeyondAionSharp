using System.Text.Json;
using Aion.Bots.Dashboard;
using Aion.Bots.Scenarios.Classes;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.Simulation.Tests;

public sealed partial class SimulationFastScenarioTests
{
    // Supplied parent/StatUp admission, real Effect ownership and trigger
    // notification. This does not certify AI selection or proc probability.
    [SkippableFact]
    public async Task RetailStatisticsFocusedBuffSmokeUsesProductionEffectsAndTriggers()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);
        Skip.If(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_BUFF_SMOKE") != "1", "Requires the focused private statistics SIM");
        Assert.True(RetailNpcStatCatalog.Enabled);
        string output = Environment.GetEnvironmentVariable("AION_E2E_RUN_DIR")!;
        string run = Environment.GetEnvironmentVariable("AION_SIM_RUN_ID")!;
        using var policy = new SimulationLogPolicy(run, "STAT-BUFF-SMOKE", fixture.Clock, allowlistPath,
            new SimulationLogPolicyOptions { FailOnWarnings = false }, fixture.LogCapture, fixture.LoggerFactory, evidence: evidence);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        await using var session = new SimulationL0Session(fixture, policy, "b01", 1, "Asimgladiator", Race.ASMODIANS)
            { IdentityClassLine = NaturalClassLine.WarriorGladiator };
        var dashboard = new LiveBotDashboardState();
        await using var monitor = new LiveBotDashboardHost(run, ["STAT-BUFF-SMOKE"], dashboard,
            int.Parse(Environment.GetEnvironmentVariable("AION_BOT_DASHBOARD_PORT") ?? "17880"));
        session.Dashboard = dashboard;
        Console.WriteLine("Retail buff smoke monitor: " + monitor.Url);
        using var logs = new ThreeKeysRuntimeLogProvider(Path.Combine(output, "buff-events.jsonl"), () => fixture.Clock.NowMillis);
        fixture.LoggerFactory.AddProvider(logs);
        await session.LoginCharacterListAsync(token); session.SelectCharacter(320000, "Asimgladiator");
        await session.EnterWorldAsync(token); await session.SynchronizeAsync(token);
        var player = fixture.World.GetPlayer(session.CharacterId);
        Assert.False(player.IsStaff());
        var rows = new List<object>();
        var boundaries = new List<object>();
        int triggers = 0, scannedProfiles = 0;
        foreach (var group in RetailNpcStatCatalog.Members.Where(m => m.MapId is 220010000 or 220030000 or 320050000).GroupBy(m => m.MapId))
        {
            var map = fixture.World.GetWorldMap(group.Key);
            var instance = map.GetMainWorldMapInstance();
            foreach (var old in instance.GetNpcs().ToArray()) old.GetController().Delete();
            var spot = DataManager.SPAWNS_DATA.GetSpawnsByWorldId(group.Key).SelectMany(g => g.GetSpawnTemplates()).First();
            await TeleportForSetupAsync(session, player, group.Key, spot.GetX() + .5f, spot.GetY(), spot.GetZ(), token);
            await session.SynchronizeAsync(token);
            foreach (var member in group)
            {
                var p = member.Profile;
                var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(group.Key, p.NpcId,
                    spot.GetX(), spot.GetY(), spot.GetZ(), 0, null, "retail_statistics_fixture");
                var npc = Assert.IsAssignableFrom<Npc>(Aion.GameServer.SpawnEngine.SpawnEngine.SpawnObject(spawn, instance.GetInstanceId()));
                try
                {
                    Assert.Same(p, npc.GetGameStats().StatisticsProfile);
                    scannedProfiles++;
                    for (int i = 0; i < npc.GetSkillList().Size(); i++)
                    {
                        var entry = npc.GetSkillList().GetSkillOnPosition(i);
                        var template = DataManager.SKILL_DATA.GetSkillTemplate(entry.GetSkillId());
                        int level = entry.GetSkillLevel();
                        if (!RetailNpcStatCatalog.TryGetBuff(p, entry.GetSkillId(), level, out var source))
                        {
                            if (!RetailNpcStatCatalog.TryGetBuffNumericLevel(npc, p, entry.GetSkillId(), level, out level)
                                || !RetailNpcStatCatalog.TryGetBuff(p, entry.GetSkillId(), level, out source))
                            {
                                if (template.GetEffects()?.GetEffects().Any(e => e is StatupEffect) == true)
                                    boundaries.Add(new { npc = p.NpcId, map = group.Key, skill = entry.GetSkillId(), reason = "missing-exact-source-binding" });
                                continue;
                            }
                        }
                        foreach (var program in source.Programs)
                        {
                            if (!program.Supported)
                            {
                                boundaries.Add(new { npc = p.NpcId, skill = source.Id, program.Position, program.UnsupportedReason });
                                continue;
                            }
                            var et = Assert.IsType<StatupEffect>(template.GetEffectTemplate(program.Position));
                            var effect = new Effect(npc, npc, template, entry.GetSkillLevel());
                            effect.AddSuccessEffect(et); effect.ApplyEffect();
                            Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
                            var observed = new List<object>();
                            foreach (var stat in program.Parts.Select(part => part.Stat).Distinct())
                            {
                                var parts = program.Parts.Where(part => part.Stat == stat).ToArray();
                                int percent = parts.Where(part => part.Percent).Sum(part => RetailNpcStatBuffs.Value(part, level));
                                int flat = parts.Where(part => !part.Percent).Sum(part => RetailNpcStatBuffs.Value(part, level));
                                if (stat is StatEnum.PHYSICAL_ATTACK or StatEnum.MAGICAL_ATTACK)
                                {
                                    var actual = RetailNpcStatNormalInputs.CurrentModifiers(npc, stat == StatEnum.PHYSICAL_ATTACK);
                                    Assert.Equal(new(percent, flat), actual);
                                    observed.Add(new { stat = stat.ToString(), percent, flat });
                                }
                                else
                                {
                                    string field = Assert.IsType<string>(RetailNpcStatDefenses.Field(stat));
                                    var seed = p.ScalarInputs.Fields[field]; var spec = seed.Specification;
                                    long expected = Math.Clamp(seed.Value + seed.Value * percent / 100 + flat, spec.Minimum!.Value, spec.Maximum!.Value);
                                    int actual = npc.GetGameStats().GetStat(stat, p.Values[field]).GetCurrent();
                                    Assert.Equal((int)(float)expected, actual);
                                    observed.Add(new { stat = stat.ToString(), basis = seed.Value, percent, flat, expected, actual });
                                }
                            }
                            // The same registered buff functions must reach the
                            // production normal consumer and final HP owner.
                            if (player.IsDead())
                            {
                                await session.AdvanceAsync(TimeSpan.FromSeconds(3), token);
                                await session.SendPacketAsync(session.Api.Revive(), token);
                                await session.SynchronizeAsync(token);
                            }
                            Assert.False(player.IsDead());
                            if (player.IsProtectionActive()) player.GetController().StopProtectionActiveTask();
                            int playerBefore = player.GetLifeStats().GetCurrentHp(), logStart = fixture.LogCapture.Entries.Count;
                            npc.GetController().AttackTarget(player, 0, false);
                            int playerAfter = player.GetLifeStats().GetCurrentHp();
                            var normalEvents = fixture.LogCapture.Entries.Skip(logStart).Select(e => e.Message)
                                .Where(m => m.StartsWith("RETAIL_STAT_NORMAL_INPUT ") || m.StartsWith("RETAIL_SKYTEMPLE_HP ")).ToArray();
                            var inputs = normalEvents.Where(m => m.StartsWith("RETAIL_STAT_NORMAL_INPUT ")).ToArray();
                            Assert.Equal(p.HasNormalAttackCorrection, inputs.Length > 0);
                            foreach (string message in inputs)
                            {
                                using var input = JsonDocument.Parse(message["RETAIL_STAT_NORMAL_INPUT ".Length..]);
                                var root = input.RootElement;
                                var modifiers = RetailNpcStatNormalInputs.CurrentModifiers(npc, root.GetProperty("physical").GetBoolean());
                                Assert.Equal(modifiers.Percent, root.GetProperty("percent").GetInt32());
                                Assert.Equal(modifiers.Flat, root.GetProperty("flat").GetInt32());
                            }
                            int requested = inputs.Sum(message =>
                            {
                                using var input = JsonDocument.Parse(message["RETAIL_STAT_NORMAL_INPUT ".Length..]);
                                return input.RootElement.GetProperty("protectedDamage").GetInt32();
                            });
                            Assert.Equal(Math.Min(playerBefore, requested), playerBefore - playerAfter);
                            effect.EndEffect();
                            Assert.DoesNotContain(effect, npc.GetEffectController().GetAllEffects());
                            foreach (var stat in program.Parts.Select(part => part.Stat).Distinct())
                            {
                                Assert.Empty(npc.GetGameStats().GetStatsSorted(stat));
                                if (RetailNpcStatDefenses.Field(stat) is { } field)
                                    Assert.Equal(p.Values[field], npc.GetGameStats().GetStat(stat, p.Values[field]).GetCurrent());
                                else Assert.Equal(new(0, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, stat == StatEnum.PHYSICAL_ATTACK));
                            }
                            rows.Add(new { npc = p.NpcId, map = group.Key, skill = source.Id, level, program.Position, source.Sha256,
                                profile = p.SourceRecordSha256, observed, playerBefore, playerAfter, normalEvents, removed = true });
                        }
                    }
                    if (p.NpcId is 210407 or 210409)
                    {
                        int parentId = p.NpcId == 210407 ? 16394 : 16856;
                        var entry = Enumerable.Range(0, npc.GetSkillList().Size()).Select(npc.GetSkillList().GetSkillOnPosition)
                            .Single(s => s.GetSkillId() == parentId);
                        var template = DataManager.SKILL_DATA.GetSkillTemplate(parentId);
                        var parent = new Effect(npc, npc, template, entry.GetSkillLevel());
                        parent.AddSuccessEffect(Assert.Single(template.GetEffects()!.GetEffects().OfType<ProvokerEffect>()));
                        parent.ApplyEffect();
                        int before = npc.GetGameStats().GetPDef().GetCurrent(), attempts = 0;
                        do { npc.GetObserveController().NotifyAttackedObservers(player, 0); attempts++; }
                        while (!npc.GetEffectController().HasAbnormalEffect(16393) && attempts < 128);
                        var child = Assert.Single(npc.GetEffectController().GetAllEffects(), e => e.GetSkillId() == 16393);
                        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(child));
                        long basis = p.ScalarInputs.Fields["physical_defend"].Value;
                        int expected = (int)Math.Clamp(basis + basis * 70 / 100, 0, 500000);
                        Assert.Equal(expected, npc.GetGameStats().GetPDef().GetCurrent());
                        child.EndEffect(); parent.EndEffect();
                        Assert.Equal(before, npc.GetGameStats().GetPDef().GetCurrent());
                        rows.Add(new { npc = p.NpcId, map = group.Key, skill = 16393, parentId, attempts, before, expected, removed = true });
                        triggers++;
                    }
                }
                finally { npc.GetController().Delete(); }
            }
        }
        Assert.Equal(2, triggers);
        Assert.NotEmpty(rows);
        await File.WriteAllTextAsync(Path.Combine(output, "buff-smoke.json"), JsonSerializer.Serialize(new { scannedProfiles, rows, boundaries, triggers,
            scope = "Production numeric Effect application/removal and parent observer triggers; supplied effect admission, no natural AI/proc probability claim" }), token);
        policy.AssertClean();
    }
}
