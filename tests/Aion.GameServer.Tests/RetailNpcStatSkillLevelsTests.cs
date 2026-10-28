using System.Reflection;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatSkillLevelsTests
{
    [Fact]
    public void ConstructionWindowReturnsTheOriginalNullBeforeInspectingNpcOrStatistics()
    {
        Assert.Null(RetailNpcStatSkillLevels.Get(null!, null!));
        Assert.Empty(RetailNpcStatSkillLevels.GetDecisions(null!, null!));
    }

    [Fact]
    public void ExplicitUniqueLevelRequiresIndependentSourceIdentityAndKeepsZero()
    {
        var group = Skills(Row("SourceName", "0"));
        var bindings = Bindings(Binding(100, "SourceName", 0));
        var result = RetailNpcStatSkillLevels.Resolve([group], bindings, 100,
            (id, level) => id == 100 && level == 0 ? "SourceName" : null);
        Assert.Equal(0, result.Level);
        Assert.Equal("verified-source-level", result.Status);
        var mismatch = RetailNpcStatSkillLevels.Resolve([group], bindings, 100, (_, _) => "DifferentName");
        Assert.Null(mismatch.Level);
        Assert.Equal("unpublished-or-mismatched-skill-source", mismatch.Status);
    }

    [Fact]
    public void NativeCaseEquivalentRowsStillRequireEveryOrdinalAndOneLevel()
    {
        var group=Skills(Row("SourceName","11"),Row("sourcename","11"));
        var bindings=Bindings(Binding(100,"SourceName",0),Binding(100,"sourcename",1));
        var result=RetailNpcStatSkillLevels.Resolve([group],bindings,100,(_,_)=>"SOURCENAME");
        Assert.Equal(11,result.Level);
        result=RetailNpcStatSkillLevels.Resolve([group],Bindings(Binding(100,"SourceName",0)),100,(_,_)=>"SourceName");
        Assert.Null(result.Level);Assert.Equal("incomplete-source-binding",result.Status);
        group=Skills(Row("SourceName","11"),Row("sourcename","12"));
        result=RetailNpcStatSkillLevels.Resolve([group],bindings,100,(_,_)=>"SourceName");
        Assert.Null(result.Level);Assert.Equal("ambiguous-source-level",result.Status);
    }

    [Theory]
    [InlineData(null, "missing-or-invalid-source-level")]
    [InlineData("oops", "missing-or-invalid-source-level")]
    [InlineData("-1", "unsupported-source-level-range")]
    [InlineData("32768", "unsupported-source-level-range")]
    public void MissingAndUnsupportedLevelsNeverBecomeGuessedDefaults(string? level, string expected)
    {
        var result = RetailNpcStatSkillLevels.Resolve([Skills(Row("SourceName", level))],
            Bindings(Binding(100, "SourceName", 0)), 100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void RepeatedSourceEntryRequiresOneLevelAcrossEveryIndependentlyBoundOrdinal()
    {
        var group = Skills(Row("SourceName", "20"), Row("SourceName", "65"));
        var bindings = Bindings(Binding(100, "SourceName", 0), Binding(100, "SourceName", 1));
        var result = RetailNpcStatSkillLevels.Resolve([group], bindings, 100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("ambiguous-source-level", result.Status);
        group = Skills(Row("SourceName", "65"), Row("SourceName", "65"));
        result = RetailNpcStatSkillLevels.Resolve([group], bindings, 100, (_, _) => "SourceName");
        Assert.Equal(65, result.Level);
    }

    [Fact]
    public void UnboundDuplicateNameOrWrongOrdinalIsVisibleInsteadOfChoosingFirst()
    {
        var group = Skills(Row("SourceName", "65"), Row("SourceName", "65"));
        var result = RetailNpcStatSkillLevels.Resolve([group], Bindings(Binding(100, "SourceName", 0)),
            100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("incomplete-source-binding", result.Status);
        result = RetailNpcStatSkillLevels.Resolve([group], Bindings(Binding(100, "SourceName", 2)),
            100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("unverified-source-binding", result.Status);
        result = RetailNpcStatSkillLevels.Resolve([group, group], Bindings(Binding(100, "SourceName", 0)),
            100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("missing-or-ambiguous-source-skills-group", result.Status);
    }

    [Fact]
    public void SourceIdNameAndBindingStatusMustAgreeWithoutBorrowingOtherSkillLevels()
    {
        var group = Skills(Row("SourceName", "65"));
        var result = RetailNpcStatSkillLevels.Resolve([group], Bindings(Binding(101, "SourceName", 0)),
            100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("missing-source-binding", result.Status);
        result = RetailNpcStatSkillLevels.Resolve([group], Bindings(Binding(100, "DifferentName", 0)),
            100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("source-name-mismatch", result.Status);
        var binding = Binding(100, "SourceName", 0); binding["status"] = "unresolved";
        result = RetailNpcStatSkillLevels.Resolve([group], Bindings(binding), 100, (_, _) => "SourceName");
        Assert.Null(result.Level); Assert.Equal("unverified-source-binding", result.Status);
    }

    [Fact]
    public void NumericClonePreservesEverySelectionTimingChainAndConditionField()
    {
        var original = new NpcSkillTemplate
        {
            id = 21692, lv = 20, prob = 35, minHp = 12, maxHp = 87, minTime = 100,
            maxTime = 500, cd = 30000, is_post_spawn = true, prio = 17, nextSkillTime = 777,
            nextChainId = 8, chainId = 4, maxChainTime = 900, conjunction = ConjunctionType.OR,
            conditionTemplate = new NpcSkillConditionTemplate { hpBelow = 23 },
            spawn = new NpcSkillSpawn { npcId = 210649, delay = 500 }
        };
        var selected = original.CloneWithSkillLevel(65);
        Assert.NotSame(original, selected); Assert.Equal(20, original.lv); Assert.Equal(65, selected.lv);
        AssertOnlyLevelDiffers(original, selected);
        Assert.Same(original.conditionTemplate, selected.conditionTemplate);
        Assert.Same(original.spawn, selected.spawn);
    }

    [SkippableFact]
    public void ExistingRaidSkillLevelChangesWithoutChangingHostListOrOtherEntryMetadataAndRevertsOnOwnership()
    {
        var f = Fixture(); using var scope = RetailNpcStatCatalog.InstallForTests(f.Bytes, f.Hash, f.Directory);
        Skip.If(!RetailNpcStatCatalog.TryGet(220030000, 234561, out _), "Requires the full statistics cohort containing234561");
        using var harness = BossAiHarness.For(220030000).Build();
        Npc Spawn() => new(new NpcController(), Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, 234561, 100, 100, 200, 0),
            DataManager.NPC_DATA.GetNpcTemplate(234561));
        var npc = Spawn();
        var original = DataManager.NPC_SKILL_DATA.GetOrCreateNpcSkillList(234561);
        var selected = npc.GetSkillList();
        Assert.NotSame(original, selected); Assert.Same(selected, npc.GetSkillList());
        Assert.NotSame(selected, Spawn().GetSkillList()); // No new mutable entries shared between actors.
        Assert.Equal(original.Size(), selected.Size());
        Assert.Equal(original.GetPriorities(), selected.GetPriorities());
        for (int i = 0; i < original.Size(); i++)
        {
            var before = original.GetSkillOnPosition(i); var after = selected.GetSkillOnPosition(i);
            Assert.Equal(before.GetSkillId(), after.GetSkillId());
            AssertOnlyLevelDiffers(before.GetTemplate(), after.GetTemplate());
            if (before.GetSkillId() == 21692)
            {
                Assert.Equal(20, before.GetSkillLevel()); Assert.Equal(65, after.GetSkillLevel());
                Assert.Equal(30000, after.GetTemplate().GetCooldown());
                Assert.Equal(35, after.GetTemplate().GetProbability());
            }
        }
        Assert.Contains(RetailNpcStatSkillLevels.GetDecisions(npc, original),
            d => d.SkillId == 21692 && d.HostLevel == 20 && d.SourceLevel == 65 && d.Status == "source-level-applied");
        Assert.Equal(65,Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc,21692));
        Aion.GameServer.Ai.NpcSkillCasting.QueueAtDataLevel(npc,21692,NpcSkillTargetAttribute.MOST_HATED);
        Assert.Equal(21692,npc.GetNextQueuedSkill().GetSkillId());
        Assert.Equal(65,npc.GetNextQueuedSkill().GetSkillLevel());
        npc.ClearQueuedSkills();
        Assert.Equal(17,Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc,999999,17));
        npc.SetCreatorId(42);
        Assert.Same(original, npc.GetSkillList()); Assert.Empty(RetailNpcStatSkillLevels.GetDecisions(npc, original));
        Assert.Equal(20,Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc,21692));
        npc.SetCreatorId(0);
        Assert.Same(original, npc.GetSkillList()); // Retired actors never silently re-admit.
        Assert.Equal(20,Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc,21692));
    }

    private static void AssertOnlyLevelDiffers(NpcSkillTemplate original, NpcSkillTemplate selected)
    {
        foreach (var field in typeof(NpcSkillTemplate).GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (field.Name != nameof(NpcSkillTemplate.lv)) Assert.Equal(field.GetValue(original), field.GetValue(selected));
    }
    private static object[] Node(string tag, params object[] children) => [tag, Array.Empty<object>(), children];
    private static object Row(string name, string? level) => Node("data", level is null
        ? [Node("skill_name", name)] : [Node("skill_name", name), Node("skill_level", level)]);
    private static JsonElement Skills(params object[] rows) => JsonSerializer.SerializeToElement(Node("skills", rows));
    private static JsonElement Bindings(params object[] rows) => JsonSerializer.SerializeToElement(rows);
    private static Dictionary<string, object> Binding(int id, string name, int ordinal) => new()
    {
        ["supported48_id"] = id, ["name"] = name, ["status"] = "dual_join_agrees_static_only",
        ["source_group_index"] = 0, ["source_entry_index"] = ordinal
    };
    private static (byte[] Bytes, string Hash, string Directory) Fixture()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_MANIFEST")
            ?? Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_SHA256")
            ?? Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash), "Requires the external statistics-only package");
        return (File.ReadAllBytes(path!), hash!, Path.GetDirectoryName(Path.GetFullPath(path!))!);
    }
}
