using System.Security.Cryptography;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcBuffRuntimeTests
{
    private sealed class Owner : IStatOwner;

    [SkippableFact]
    public void FrozenNativeProgramDifferencesApplyAndRemoveThroughTheExistingEffectOwner()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_BUFF_CASES");
        Skip.If(string.IsNullOrWhiteSpace(path), "Requires the pinned external static StatUp comparison");
        byte[] casesBytes = File.ReadAllBytes(path!);
        Assert.Equal("37fc2c0e3f73598cda7319804ae22b3016673906d020e9a1500b295a9bef14fb",
            Convert.ToHexStringLower(SHA256.HashData(casesBytes)));
        using var casesDoc = JsonDocument.Parse(casesBytes);
        var cases = casesDoc.RootElement.GetProperty("mismatches").EnumerateArray()
            .Concat(casesDoc.RootElement.GetProperty("level_changes").EnumerateArray()).ToArray();
        Assert.Equal(293, cases.Length);
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var rows = cases.Select(c => (Case: c, Member: RetailNpcStatCatalog.Members
            .Where(m => m.MapId != 0 && m.Profile.NpcId == c.GetProperty("npc").GetInt32()).OrderBy(m => m.MapId).First())).ToArray();
        var tracked = new[] { StatEnum.PHYSICAL_DEFENSE, StatEnum.MAGICAL_DEFEND, StatEnum.MAGICAL_RESIST,
            StatEnum.EVASION, StatEnum.PARRY, StatEnum.BLOCK }.Concat(RetailNpcStatBuffs.Elements).ToArray();
        int count = 0, changed = 0;
        foreach (var group in rows.GroupBy(r => r.Member.MapId))
        {
            using var h = BossAiHarness.For(group.Key).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
            foreach (var (c, member) in group)
            {
                var npc = FreshNpc(h, group.Key, member.Profile.NpcId);
                var p = member.Profile;
                int id = c.GetProperty("skill").GetInt32(), position = c.GetProperty("effect").GetInt32();
                // The old static census assumed absent lv=1. The actual XML
                // loader defaults it to zero; use the loaded host definition.
                int hostLevel = DataManager.NPC_SKILL_DATA.GetNpcSkillTemplates(p.NpcId)
                    .First(s => s.GetSkillId() == id).GetSkillLevel();
                var skill = DataManager.SKILL_DATA.GetSkillTemplate(id);
                var template = Assert.IsType<StatupEffect>(skill.GetEffectTemplate(position));
                var before = tracked.ToDictionary(s => s, s => npc.GetGameStats().GetStat(s, p.Values[RetailNpcStatDefenses.Field(s)!]).GetCurrent());
                int hpBefore = npc.GetLifeStats().GetMaxHp();
                var effect = new Effect(npc, npc, skill, hostLevel);
                effect.AddSuccessEffect(template); effect.ApplyEffect();
                Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect), $"npc={p.NpcId} skill={id} position={position} hostLevel={hostLevel}");
                Assert.Equal(hostLevel, effect.GetSkillLevel());
                Assert.Contains(effect, npc.GetEffectController().GetAllEffects());
                foreach (var stat in tracked)
                {
                    var parts = c.GetProperty("source").EnumerateArray().Where(a => a[0].GetString() == stat.ToString()).ToArray();
                    int percent = parts.Where(a => a[1].GetString() == "PERCENT").Sum(a => a[2].GetInt32());
                    int flat = parts.Where(a => a[1].GetString() == "ADD").Sum(a => a[2].GetInt32());
                    var seed = p.ScalarInputs.Fields[RetailNpcStatDefenses.Field(stat)!]; var spec = seed.Specification;
                    long expected = Math.Clamp(seed.Value + seed.Value * percent / 100 + flat, spec.Minimum!.Value, spec.Maximum!.Value);
                    int actual = npc.GetGameStats().GetStat(stat, p.Values[spec.Field]).GetCurrent();
                    Assert.True(expected == actual, $"npc={p.NpcId} skill={id} stat={stat} expected={expected} actual={actual}");
                    if (actual != before[stat]) changed++;
                }
                foreach (bool physical in new[] { true, false })
                {
                    var stat = physical ? StatEnum.PHYSICAL_ATTACK : StatEnum.MAGICAL_ATTACK;
                    var parts = c.GetProperty("source").EnumerateArray().Where(a => a[0].GetString() == stat.ToString()).ToArray();
                    Assert.Equal(new(parts.Where(a => a[1].GetString() == "PERCENT").Sum(a => a[2].GetInt32()),
                        parts.Where(a => a[1].GetString() == "ADD").Sum(a => a[2].GetInt32())),
                        RetailNpcStatNormalInputs.CurrentModifiers(npc, physical));
                }
                effect.EndEffect();
                Assert.DoesNotContain(effect, npc.GetEffectController().GetAllEffects());
                Assert.All(tracked, s => Assert.Equal(before[s], npc.GetGameStats().GetStat(s, p.Values[RetailNpcStatDefenses.Field(s)!]).GetCurrent()));
                Assert.Equal(hpBefore, npc.GetLifeStats().GetMaxHp());
                Assert.Equal(new(0, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, true));
                Assert.Equal(new(0, 0), RetailNpcStatNormalInputs.CurrentModifiers(npc, false));
                count++;
            }
            Assert.Empty(h.Clock.Faults);
        }
        Assert.Equal(293, count); Assert.True(changed > 0);
        Console.WriteLine($"BUFF_COVERAGE recipes=27 npc-effects={count} defense-values={count * tracked.Length} changed={changed}");
    }

    [SkippableTheory]
    [InlineData(210407, 16394)]
    [InlineData(210409, 16856)]
    public void IshalgenTriggeredBuffUsesAnExactConstantChildWithoutChangingItsSkillList(int npcId, int parentId)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        using var h = BossAiHarness.For(220010000).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var npc = FreshNpc(h, 220010000, npcId); var p = npc.GetGameStats().StatisticsProfile!;
        var parent = DataManager.SKILL_DATA.GetSkillTemplate(parentId);
        var trigger = Assert.IsType<ProvokerEffect>(parent.GetEffectTemplate(1));
        var child = DataManager.SKILL_DATA.GetSkillTemplate(trigger.skillId);
        Assert.True(RetailNpcStatCatalog.TryGetBuff(p, trigger.skillId, 1, out var source, out var lineage,
            link => RetailNpcStatTriggeredBuffs.MatchesHost(link, DataManager.SKILL_DATA.GetSkillTemplate(link.ParentId)?.GetEffectTemplate(link.Position))));
        Assert.Contains(lineage, link => link.ParentId == parentId && link.ChildId == child.GetSkillId());
        var hostList = DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(npcId);
        Assert.DoesNotContain(Enumerable.Range(0, hostList.Size()).Select(hostList.GetSkillOnPosition), s => s.GetSkillId() == trigger.skillId);
        var program = Assert.Single(source.Programs);
        var statup = Assert.IsType<StatupEffect>(child.GetEffectTemplate(program.Position));
        var effect = new Effect(npc, npc, child, 1); effect.AddSuccessEffect(statup); effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        foreach (var part in program.Parts)
        {
            var registered = Assert.Single(npc.GetGameStats().GetStatsSorted(part.Stat));
            Assert.Equal(RetailNpcStatBuffs.Value(part, 1), registered.GetValue());
            Assert.Same(effect, registered.GetOwner());
        }
        effect.EndEffect();
        Assert.All(program.Parts, part => Assert.Empty(npc.GetGameStats().GetStatsSorted(part.Stat)));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(256681, 16405)]
    [InlineData(282996, 20536)]
    public void OwnershipRetirementRestoresHostChannelsWithoutEndingTheEffectOrOtherOwners(int npcId, int skillId)
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var member = RetailNpcStatCatalog.Members.First(m => m.MapId != 0 && m.Profile.NpcId == npcId);
        using var h = BossAiHarness.For(member.MapId).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var npc = FreshNpc(h, member.MapId, npcId);
        var list = DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(npcId);
        int level = Enumerable.Range(0, list.Size()).Select(list.GetSkillOnPosition).First(e => e.GetSkillId() == skillId).GetSkillLevel();
        var skill = DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        var effect = new Effect(npc, npc, skill, level); effect.AddSuccessEffect(Assert.IsType<StatupEffect>(skill.GetEffectTemplate(1))); effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        Assert.NotEmpty(npc.GetGameStats().GetStatsSorted(StatEnum.LIGHT_RESISTANCE));
        var other = new Owner(); npc.GetGameStats().AddEffectOnly(other, [new StatAddFunction(StatEnum.PARRY, 123, true)]);
        npc.SetCreatorId(42);
        Assert.Null(npc.GetGameStats().StatisticsProfile);
        Assert.Contains(effect, npc.GetEffectController().GetAllEffects());
        Assert.Empty(npc.GetGameStats().GetStatsSorted(StatEnum.LIGHT_RESISTANCE));
        if (skillId == 16405) Assert.Equal(50, Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.MAGICAL_RESIST)).GetValue());
        effect.EndEffect();
        Assert.Empty(npc.GetGameStats().GetStatsSorted(StatEnum.MAGICAL_RESIST));
        Assert.Same(other, Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.PARRY)).GetOwner());
        Assert.Empty(h.Clock.Faults);
    }

    private static Npc FreshNpc(BossAiHarness h, int map, int id)
    {
        var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map, id, 100, 100, 200, 0, null, "retail_stat_runtime_fixture");
        var npc = new Npc(new NpcController(), spawn, DataManager.NPC_DATA.GetNpcTemplate(id));
        npc.SetEffectController(new EffectController(npc)); npc.SetKnownlist(new KnownList(npc));
        npc.SetPosition(h.World.CreatePosition(map, 100, 100, 200, 0, 1));
        // Supply the existing effect engine's spawned-actor admission flag
        // without running spawn AI, passive selection or any virtual clock.
        npc.GetPosition().SetIsSpawned(true);
        return npc;
    }

    [SkippableFact]
    public void EveryLoadedBuffProgramHasAnExplicitSupportedOrCompatibilityResult()
    {
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        var rows = RetailNpcStatCatalog.BuffSources.SelectMany(s => s.Programs.Select(p => new {
            skill_id = s.Id, source_name = s.Name, source_sha256 = s.Sha256, position = p.Position,
            supported = p.Supported, reason = p.UnsupportedReason, parts = p.Parts.Select(a => new {
                stat = a.Stat.ToString(), delta = a.Delta, value = a.Value, percent = a.Percent }).ToArray() })).ToArray();
        Assert.True(rows.Length > 200);
        foreach (var row in rows)
        {
            if (row.supported) Assert.Null(row.reason);
            else { Assert.False(string.IsNullOrWhiteSpace(row.reason)); Assert.Empty(row.parts); }
        }
        Console.WriteLine($"BUFF_PROGRAMS total={rows.Length} supported={rows.Count(r => r.supported)} compatibility={rows.Count(r => !r.supported)}");
        string? report = Environment.GetEnvironmentVariable("AION_RETAIL_BUFF_PROGRAM_REPORT");
        if (!string.IsNullOrWhiteSpace(report)) File.WriteAllText(report, JsonSerializer.Serialize(new {
            offline_only = true, lifecycle_verified = false, rows }));
    }

}
