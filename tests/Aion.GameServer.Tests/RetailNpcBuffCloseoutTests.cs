using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcBuffCloseoutTests
{
    [SkippableTheory]
    [InlineData(true, 41)]
    [InlineData(false, 64)]
    public void ReviewedCasesApplyAndRemoveTheirPinnedProgramsOffline(bool identitiesOnly, int expectedCases)
    {
        string? casesPath = Environment.GetEnvironmentVariable("AION_RETAIL_BUFF_CLOSEOUT_CASES");
        Skip.If(string.IsNullOrEmpty(casesPath), "Requires the retained buff-boundary inventory");
        using var cases = JsonDocument.Parse(File.ReadAllBytes(casesPath!));
        var rows = cases.RootElement.GetProperty("cases").EnumerateArray()
            .Where(x => !identitiesOnly || x.GetProperty("classification").GetString() is "host-stack-identity-mismatch" or "source-skill-not-in-selected-manifest")
            .Select(x => x.Clone()).ToArray();
        Assert.Equal(expectedCases, rows.Length);
        var (bytes, pin, directory) = RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat: true, includeBuffs: true);
        using var installed = RetailNpcStatCatalog.InstallForTests(bytes, pin, directory);
        foreach (var group in rows.GroupBy(x => x.GetProperty("map").GetInt32()))
        {
            using var h = BossAiHarness.For(group.Key).WithAi(typeof(RetailNpcStatRetainedBuffTests.FixtureAI)).Build();
            foreach (var row in group)
            {
                var npc = h.SpawnWithAi(row.GetProperty("npc").GetInt32(), "retail_retained_buff_fixture");
                Assert.True(RetailNpcStatCatalog.TryGet(npc, out var profile));
                int id = row.GetProperty("skill").GetInt32();
                var entry = Enumerable.Range(0, npc.GetSkillList().Size()).Select(npc.GetSkillList().GetSkillOnPosition)
                    .Single(x => x.GetSkillId() == id);
                int level = entry.GetSkillLevel();
                if (!RetailNpcStatCatalog.TryGetBuff(profile, id, level, out var source))
                {
                    if (!RetailNpcStatCatalog.TryGetBuffNumericLevel(npc, profile, id, level, out level)
                        || !RetailNpcStatCatalog.TryGetBuff(profile, id, level, out source))
                        Assert.True(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, id, level, out source),
                            $"Missing reviewed numeric source: map={group.Key} npc={npc.GetNpcId()} skill={id}");
                }
                var template = DataManager.SKILL_DATA.GetSkillTemplate(id);
                if (id == 19599)
                {
                    string? stack = template.stack; int nameId = template.nameId;
                    try
                    {
                        template.stack = stack + "_UNREVIEWED";
                        Assert.False(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, id, entry.GetSkillLevel(), out _));
                        template.stack = stack; template.nameId++;
                        Assert.False(RetailNpcStatCatalog.TryGetRetainedHostBuff(npc, profile, id, entry.GetSkillLevel(), out _));
                    }
                    finally { template.stack = stack; template.nameId = nameId; }
                }
                foreach (var program in source.Programs)
                {
                    float speedBasis = npc.GetGameStats().GetMovementSpeed().GetExactBaseWithoutBaseRate();
                    int hpBefore = npc.GetLifeStats().GetCurrentHp(), maxBefore = npc.GetLifeStats().GetMaxHp();
                    var effect = new Effect(npc, npc, template, entry.GetSkillLevel());
                    effect.AddSuccessEffect(Assert.IsType<StatupEffect>(template.GetEffectTemplate(program.Position)));
                    effect.ApplyEffect(); Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
                    int maxAfter = npc.GetLifeStats().GetMaxHp();
                    Assert.Equal(RetailNpcCurrentHpMath.Adjust(maxBefore, maxAfter, hpBefore), npc.GetLifeStats().GetCurrentHp());
                    foreach (var stat in program.Parts.Select(p => p.Stat).Distinct())
                    {
                        var parts = program.Parts.Where(p => p.Stat == stat).ToArray();
                        int percent = parts.Where(p => p.Percent).Sum(p => RetailNpcStatBuffs.Value(p, level));
                        int flat = parts.Where(p => !p.Percent).Sum(p => RetailNpcStatBuffs.Value(p, level));
                        if (stat is StatEnum.PHYSICAL_ATTACK or StatEnum.MAGICAL_ATTACK)
                            Assert.Equal(new(percent, flat), RetailNpcStatNormalInputs.CurrentModifiers(npc, stat == StatEnum.PHYSICAL_ATTACK));
                        else if (stat == StatEnum.ATTACK_SPEED)
                            Assert.Equal(RetailNpcBuffScalarMath.AttackDelay(profile.AttackDelay, percent, flat), npc.GetGameStats().GetAttackSpeed().GetCurrent());
                        else if (stat == StatEnum.SPEED)
                            Assert.Equal((int)RetailNpcBuffScalarMath.Speed(speedBasis, percent, checked(flat * 100)), npc.GetGameStats().GetMovementSpeed().GetCurrent());
                        else if (stat == StatEnum.BOOST_CASTING_TIME)
                            Assert.Equal(1000 - 1000 * percent / 100 - flat, npc.GetGameStats().GetPositiveReverseStat(stat, 1000));
                        else
                        {
                            string field = RetailNpcStatDefenses.Field(stat)!;
                            var seed = profile.ScalarInputs.Fields[field];
                            long expected = RetailNpcStatDefenses.Calculate(seed.Value, percent, flat, seed.Specification.Minimum!.Value, seed.Specification.Maximum!.Value);
                            Assert.Equal(expected, npc.GetGameStats().GetStat(stat, profile.Values[field]).GetCurrent());
                        }
                    }
                    int hpBuffed = npc.GetLifeStats().GetCurrentHp();
                    effect.EndEffect();
                    Assert.Equal(RetailNpcCurrentHpMath.Adjust(maxAfter, maxBefore, hpBuffed), npc.GetLifeStats().GetCurrentHp());
                    foreach (var stat in program.Parts.Select(p => p.Stat).Distinct()) Assert.Empty(npc.GetGameStats().GetStatsSorted(stat));
                }
                npc.GetController().Delete();
            }
            Assert.Empty(h.Clock.Faults);
        }
    }
}
