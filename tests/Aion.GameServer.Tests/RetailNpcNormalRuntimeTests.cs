using System.Text;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcNormalRuntimeTests
{
    private sealed class Owner : IStatOwner;
    private sealed class Capture : AttackCalcObserver
    {
        internal int Calls, Damage;
        public override void CheckShield(List<AttackResult> attacks, Effect effect, Creature attacker)
        { Calls++; Damage += attacks.Sum(a => a.GetDamage()); }
    }

    [Fact]
    public void NormalRandomUsesKnownMt64WordsAndEqualEndpointsConsumeNone()
    {
        // MT19937-64 seed 5489 words: 14514284786278117030,
        // 4620546740167642908, 13109570281517897720.
        var random = new SkyTempleNpcCombat.LiveRandom(5489);
        Assert.Equal(31, random.Range(1, 1000));
        Assert.Equal(250, random.Range(250, 250));
        Assert.Equal(909, random.Range(1000, 1));
        Assert.Equal(721, random.Range(1, 1000));
    }

    [SkippableFact]
    public async Task CompleteNormalPackageValidatesHostIdentitiesAndPublishesWithoutSkillOrNavigationContexts()
    {
        var (bytes, pin, _) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true);
        using var restore = RetailNpcStatCatalog.PreservePublicationForTests();
        using var h = BossAiHarness.For(220010000).Build();
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllBytesAsync(path, bytes);
            await RetailNpcStatCatalog.InitializeAsync(path, pin);
            Assert.True(RetailNpcStatCatalog.Enabled);
            Assert.Equal(11517, RetailNpcStatCatalog.Members.Count);
            Assert.Equal(118, RetailNpcStatCatalog.SupportedMaps.Count);
            Assert.NotNull(RetailNpcStatCatalog.CombatPve);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RetailNpcStatCatalog.InitializeAsync(path, pin));
        }
        finally { File.Delete(path); }
    }

    [SkippableFact]
    public void OrdinaryNormalsConsumeSelectedRangesElementsAndPveInputsAcrossTheMapCoverage()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        int checkedCount = 0, boundedCount = 0, emptyCount = 0, dualCount = 0, elementalCount = 0;
        var coverage = new List<object>();
        foreach (var map in RetailNpcStatCatalog.Members.Where(m => m.MapId != 0).GroupBy(m => m.MapId))
        {
            using var h = BossAiHarness.For(map.Key).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
            var target = MortalPlayer(h, map.Key);
            foreach (var member in map)
            {
                var p = member.Profile;
                if (!p.Attacks.IsEmpty && (!p.HasNormalAttackCorrection || SkyTempleNpcCombat.RequiresEquipmentResponse(p)))
                {
                    coverage.Add(new { map_id = map.Key, npc_id = p.NpcId, source_sha256 = p.SourceRecordSha256,
                        status = "bounded", reason = !p.HasNormalAttackCorrection ? "unsupported-normal-shape" : "exclusive-equipment-response" });
                    boundedCount++; continue;
                }
                var npc = FreshNpc(h, map.Key, p.NpcId);
                var capture = new Capture();
                target.GetObserveController().AddAttackCalcObserver(capture);
                try
                {
                    Assert.True(SkyTempleNpcCombat.TryCalculateNormal(npc, target, out var result, out var element), $"map={map.Key} npc={p.NpcId}");
                    if (p.Attacks.IsEmpty)
                    { Assert.Equal(0, Assert.Single(result).GetDamage()); emptyCount++; }
                    else
                    {
                        bool dual = p.Source.Integers["dual"].ParsedValue is not (null or 0);
                        if (dual) dualCount++;
                        bool physical = element == SkillElement.NONE;
                        if (!physical) elementalCount++;
                        var attacks = p.Attacks.Where(a => SkyTempleCombatInputs.Element(a.Type) == element).ToArray();
                        Assert.NotEmpty(attacks);
                        int min = Math.Min(p.MinDamage, p.MaxDamage), max = Math.Max(p.MinDamage, p.MaxDamage);
                        if (dual) { min /= 2; max /= 2; }
                        int current = unchecked(RetailNpcStatCatalog.CombatPve!.PlayerBase(map.Key, Race.ASMODIANS, physical, false)
                            + RetailNpcStatCatalog.CombatPve.NpcCounterpartyDelta(p, Race.ASMODIANS, physical, true));
                        float pve = Math.Clamp(1f - Math.Clamp(current, -10000, 10000) / 1000f, .1f, 12f);
                        int lower = attacks.Min(a => (int)Math.Max(1f, Math.Max(1f, min * (a.Modifier / 100f)) * pve));
                        int upper = attacks.Max(a => (int)Math.Max(1f, Math.Max(1f, max * (a.Modifier / 100f)) * pve));
                        Assert.Equal(dual ? 2 : 1, result.Count);
                        foreach (var hand in result)
                            Assert.True(hand.GetDamage() >= lower && hand.GetDamage() <= upper,
                                $"map={map.Key} npc={p.NpcId} element={element} damage={hand.GetDamage()} expected={lower}..{upper}");
                        Assert.Equal(result.Count, capture.Calls); // Existing protection owner runs once per hand.
                    }
                    checkedCount++;
                    coverage.Add(new { map_id = map.Key, npc_id = p.NpcId, source_sha256 = p.SourceRecordSha256,
                        status = p.Attacks.IsEmpty ? "verified-no-normal" : "verified-controlled-normal",
                        element = element.ToString(), hands = result.Count });
                }
                finally { target.GetObserveController().RemoveAttackCalcObserver(capture); }
            }
            Assert.Empty(h.Clock.Faults);
        }
        Assert.Equal(11208, checkedCount + boundedCount);
        Assert.True(checkedCount > 10000);
        Assert.True(elementalCount > 0); Assert.True(dualCount > 0); Assert.True(emptyCount > 0);
        // Keep detailed counts in the test output without creating a production reporting hook.
        Console.WriteLine($"NORMAL_COVERAGE checked={checkedCount} bounded={boundedCount} no-normal={emptyCount} dual={dualCount} elemental={elementalCount}");
        string? report = Environment.GetEnvironmentVariable("AION_RETAIL_NORMAL_TEST_REPORT");
        if (!string.IsNullOrWhiteSpace(report))
            File.WriteAllText(report, System.Text.Json.JsonSerializer.Serialize(new {
                offline_only = true, fixture = "fresh NPC; neutral AI; noncritical; zero target defenses; source PvE",
                checked_memberships = checkedCount, bounded_memberships = boundedCount, no_normal = emptyCount,
                dual = dualCount, observed_elemental = elementalCount, rows = coverage }));
    }

    [SkippableFact]
    public void AllTwentyFiveThreeKeysNormalsReachActualHpOnceThroughTheController()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        const int map = 320050000;
        using var h = BossAiHarness.For(map).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var target = MortalPlayer(h, map);
        var capture = new Capture(); target.GetObserveController().AddAttackCalcObserver(capture);
        int count = 0;
        foreach (var member in RetailNpcStatCatalog.Members.Where(m => m.MapId == map))
        {
            var npc = FreshNpc(h, map, member.Profile.NpcId);
            int before = target.GetLifeStats().GetCurrentHp(), calls = capture.Calls, damage = capture.Damage;
            npc.GetController().AttackTarget(target, 0, true);
            Assert.Equal(calls + 1, capture.Calls);
            int delivered = capture.Damage - damage;
            Assert.True(delivered > 0, $"npc={npc.GetNpcId()}");
            Assert.Equal(before - delivered, target.GetLifeStats().GetCurrentHp());
            count++;
        }
        Assert.Equal(25, count); Assert.Empty(h.Clock.Faults);
    }

    internal static Npc FreshNpc(BossAiHarness h, int map, int id, string aiName = "retail_stat_runtime_fixture")
    {
        var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map, id, 100, 100, 200, 0, null, aiName);
        var npc = new Npc(new NpcController(), spawn, DataManager.NPC_DATA.GetNpcTemplate(id));
        npc.SetEffectController(new EffectController(npc));
        npc.SetKnownlist(new KnownList(npc));
        npc.SetPosition(h.World.CreatePosition(map, 100, 100, 200, 0, 1));
        // Controlled noncritical ordinary state; status arithmetic has its own native-vector tests.
        npc.GetGameStats().AddEffectOnly(new Owner(), [new StatRateFunction(StatEnum.PHYSICAL_CRITICAL, -100, true),
            new StatRateFunction(StatEnum.MAGICAL_CRITICAL, -100, true)]);
        return npc;
    }

    internal static Player MortalPlayer(BossAiHarness h, int map)
    {
        var common = new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR); common.SetRace(Race.ASMODIANS);
        common.SetGender(Gender.MALE); common.SetName("NormalFixture" + common.GetPlayerObjId()); common.SetNote("");
        common.SetLevel(50);
        var player = new Player(new PlayerAccountData(common, new PlayerAppearance()), new Account(1));
        player.SetKnownlist(new KnownList(player)); player.SetEffectController(new PlayerEffectController(player));
        player.SetFlyController(new Aion.GameServer.Controllers.FlyController(player));
        player.SetPosition(h.World.CreatePosition(map, 101, 100, 200, 0, 1));
        h.World.StoreObject(player); h.World.Spawn(player);
        player.GetGameStats().AddEffectOnly(new Owner(), new[] { StatEnum.MAXHP, StatEnum.PHYSICAL_DEFENSE, StatEnum.MAGICAL_DEFEND,
            StatEnum.EVASION, StatEnum.PARRY, StatEnum.BLOCK, StatEnum.WATER_RESISTANCE, StatEnum.WIND_RESISTANCE,
            StatEnum.EARTH_RESISTANCE, StatEnum.FIRE_RESISTANCE, StatEnum.LIGHT_RESISTANCE, StatEnum.DARK_RESISTANCE }
            .Select(s => (IStatFunction)new StatSetFunction(s, s == StatEnum.MAXHP ? 2000000000 : 0)).ToArray());
        player.GetLifeStats().SetCurrentHp(2000000000);
        Assert.False(player.IsInvulnerable());
        return player;
    }
}
