using Aion.GameServer.Ai;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Model.Stats.Container;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcRuntimeGetterTests
{
    [AIName("retail_stat_runtime_fixture")]
    public sealed class BaseAI(Npc owner) : NpcAI(owner);

    [SkippableFact]
    public void SelectedMapMembershipsUseRetailBaseGettersWithoutAnAiOverride()
    {
        var (bytes, pin, directory) = ProfileFixture();
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        int checkedCount = 0;
        foreach (var group in RetailNpcStatCatalog.Members.Where(m => m.MapId != 0).GroupBy(m => m.MapId))
        {
            int map = group.Key;
            using var h = BossAiHarness.For(map).WithAi(typeof(BaseAI)).Build();
            foreach (var member in group)
            {
                var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map, member.Profile.NpcId,
                    100, 100, 200, 0, null, "retail_stat_runtime_fixture");
                var npc = new Npc(new NpcController(), spawn, DataManager.NPC_DATA.GetNpcTemplate(member.Profile.NpcId));
                var p = member.Profile; var stats = npc.GetGameStats();
                Assert.Same(p, stats.StatisticsProfile);
                Assert.Equal(p.MaxHp, npc.GetLifeStats().GetMaxHp());
                Assert.Equal(p.MaxHp, npc.GetLifeStats().GetCurrentHp());
                Assert.Equal(p.Level, (int)npc.GetLevel());
                Assert.Equal(p.PhysicalDefense, stats.GetPDef().GetCurrent());
                Assert.Equal(p.MagicalDefense, stats.GetMDef().GetCurrent());
                Assert.Equal(p.MagicalResist, stats.GetMResist().GetCurrent());
                Assert.True(p.PhysicalAccuracy == stats.GetMainHandPAccuracy().GetCurrent(),
                    $"map={map} npc={npc.GetNpcId()} accuracy expected={p.PhysicalAccuracy} actual={stats.GetMainHandPAccuracy().GetCurrent()}; " +
                    string.Join(";", stats.GetStatsSorted(StatEnum.PHYSICAL_ACCURACY).Select(f => f.GetType().Name + ":" + f.GetValue())));
                Assert.Equal(p.MagicalAccuracy, stats.GetMAccuracy().GetCurrent());
                Assert.Equal(p.PhysicalCritical, stats.GetMainHandPCritical().GetCurrent());
                Assert.Equal(p.MagicalCritical, stats.GetMCritical().GetCurrent());
                Assert.Equal(p.Values["physical_critical_reduce_rate"], stats.GetPCR().GetCurrent());
                Assert.Equal(p.Values["magical_critical_reduce_rate"], stats.GetMCR().GetCurrent());
                Assert.Equal(p.Values["dodge"], stats.GetEvasion().GetCurrent());
                Assert.Equal(p.Values["parry"], stats.GetParry().GetCurrent());
                Assert.Equal(p.Values["block"], stats.GetBlock().GetCurrent());
                foreach (var (channel, field) in new[] {
                    (StatEnum.WATER_RESISTANCE, "elemental_defend_water"),
                    (StatEnum.WIND_RESISTANCE, "elemental_defend_air"),
                    (StatEnum.EARTH_RESISTANCE, "elemental_defend_earth"),
                    (StatEnum.FIRE_RESISTANCE, "elemental_defend_fire"),
                    (StatEnum.LIGHT_RESISTANCE, "elemental_defend_light"),
                    (StatEnum.DARK_RESISTANCE, "elemental_defend_dark") })
                    Assert.Equal(p.Values[field], stats.GetResistance(channel).GetCurrent());
                Assert.Equal(p.Values["str"], stats.GetPower().GetCurrent());
                Assert.Equal(p.Values["vit"], stats.GetHealth().GetCurrent());
                Assert.Equal(p.Values["agi"], stats.GetAgility().GetCurrent());
                Assert.Equal(p.Values["dex"], stats.GetAccuracy().GetCurrent());
                Assert.Equal(p.Values["kno"], stats.GetKnowledge().GetCurrent());
                Assert.Equal(p.Values["will"], stats.GetWill().GetCurrent());
                var host = npc.GetObjectTemplate().GetStatsTemplate();
                Assert.Equal(host.GetMaxMp(), stats.GetStatsTemplate().GetMaxMp());
                Assert.Equal(host.GetAttack(), stats.GetStatsTemplate().GetAttack());
                Assert.Equal(host.GetMagicalAttack(), stats.GetStatsTemplate().GetMagicalAttack());
                Assert.Same(host.Speeds, stats.GetStatsTemplate().Speeds);
                checkedCount++;
            }
            Assert.Empty(h.Clock.Faults);
        }
        Assert.Equal(11208, checkedCount); // Includes reviewed historical/conditional memberships, not reachability proof.
    }

    [SkippableFact]
    public void LargeHpAndLateOwnershipUseTheSelectedIntegerThenRestoreHostState()
    {
        var (bytes, pin, directory) = ProfileFixture();
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        using (var h = BossAiHarness.For(120010000).WithAi(typeof(BaseAI)).Build())
        {
            var npc = h.SpawnWithAi(216520, "retail_stat_runtime_fixture");
            Assert.Equal(86954218, npc.GetGameStats().StatisticsProfile!.MaxHp);
            Assert.Equal(86954218, npc.GetLifeStats().GetMaxHp());
            Assert.Empty(h.Clock.Faults);
        }
        using (var h = BossAiHarness.For(220030000).Build())
        {
            var host = DataManager.NPC_DATA.GetNpcTemplate(204260);
            var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, 204260, 100, 100, 200, 0);
            var npc = new Npc(new NpcController(), spawn, host);
            Assert.Equal(3318, npc.GetLifeStats().GetCurrentHp());
            npc.SetCreatorId(42);
            Assert.Null(npc.GetGameStats().StatisticsProfile);
            Assert.Same(host.GetStatsTemplate(), npc.GetGameStats().GetStatsTemplate());
            Assert.Equal(host.GetStatsTemplate().GetMaxHp(), npc.GetLifeStats().GetCurrentHp());
            npc.SetCreatorId(0);
            Assert.False(RetailNpcStatCatalog.TryGet(npc, out _));
            Assert.Empty(h.Clock.Faults);
        }
    }

    internal static (byte[] Bytes, string Pin, string Directory) ProfileFixture(bool includeCombat = false, bool includeBuffs = false)
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? pin = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(pin), "Requires the pinned external full-12 stat package");
        byte[] bytes = File.ReadAllBytes(path!);
        Assert.Equal(pin, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var root = JsonNode.Parse(bytes)!.AsObject();
        // Exercise ordinary profiles independently of optional skill/player contexts.
        var keys = new HashSet<string> { "version", "mode", "region", "dataset_id", "output_manifest_sha256",
            "cohorts", "identity_review", "supported_maps" };
        if (includeCombat) keys.Add("combat_stats");
        if (includeBuffs) { keys.Add("skill_sources"); keys.Add("buff_identity_review"); }
        foreach (string key in root.Select(k => k.Key).Where(k => !keys.Contains(k)).ToArray()) root.Remove(key);
        bytes = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
        return (bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), Path.GetDirectoryName(path!)!);
    }
}
