using System.Text.Json.Nodes;
using Aion.GameServer.Dataholders.RetailNpc;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Stats;
using Aion.GameServer.Retail;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcProfileValueTests
{
    [Fact]
    public void StatisticsConversionReplacesReviewedStatsAndRetainsCompatibilityInputs()
    {
        var profile = Synthetic();
        var original = new StatsTemplate
        {
            MaxHp = 9, MaxMp = 777, Attack = 444, Matk = 555,
            Speeds = new() { WalkSpeed = 6, RunSpeed = 9, RunSpeedFight = 12 }
        };
        var stats = profile.CreateStatsTemplate(original);
        Assert.Equal(1234, stats.GetMaxHp());
        Assert.Equal(222, stats.GetPdef()); Assert.Equal(333, stats.GetMdef());
        Assert.Equal(444, stats.GetMresist()); Assert.Equal(555, stats.GetAccuracy());
        Assert.Equal(666, stats.GetMacc()); Assert.Equal(44, stats.GetPcrit());
        Assert.Equal(55, stats.GetMcrit()); Assert.Equal(777, stats.GetMagicBoost());
        Assert.Equal(888, stats.GetMsup()); Assert.Equal(111, stats.GetEvasion());
        Assert.Equal(11, stats.GetParry()); Assert.Equal(12, stats.GetBlock());
        Assert.Equal(0, stats.GetPower()); Assert.Equal(91, stats.GetHealth());
        Assert.Equal(92, stats.GetAgility()); Assert.Equal(93, stats.GetBaseAccuracy());
        Assert.Equal(94, stats.GetKnowledge()); Assert.Equal(95, stats.GetWill());
        Assert.Equal(777, stats.GetMaxMp());
        Assert.Equal(444, stats.GetAttack()); Assert.Equal(555, stats.GetMagicalAttack());
        Assert.Same(original.Speeds, stats.Speeds);
        Assert.Equal(6, stats.GetWalkSpeed()); Assert.Equal(9, stats.GetRunSpeed());
        Assert.Equal(12, stats.GetRunSpeedFight());
        Assert.Equal(10, profile.MinDamage); Assert.Equal(20, profile.MaxDamage);
        Assert.Equal(9, original.GetMaxHp());
        stats.SetMaxHp(1);
        Assert.Equal(1234, profile.MaxHp);
        Assert.Equal(1234, profile.CreateStatsTemplate(original).GetMaxHp());
    }

    [Fact]
    public void MissingCompatibilityTemplateCannotDefaultMpOrApplySourceMovement()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Synthetic().CreateStatsTemplate());
        Assert.Contains("original 4.8 template", error.Message);
    }

    [Fact]
    public void OrderedAttacksKeepExplicitZeroAndOriginalElementTokens()
    {
        var p = Synthetic(Node("attacks", null,
            Node("data", null, Node("attack_type", "physical"), Node("attack_range", "0"),
                Node("attack_rate", "0"), Node("attack_mod", "0")),
            Node("data", null, Node("attack_type", "magical_Fire"), Node("attack_range", "12"),
                Node("attack_rate", "1000"), Node("attack_mod", "120"))));
        Assert.Equal([0, 1], p.Attacks.Select(a => a.Ordinal));
        Assert.Equal("physical", p.Attacks[0].Type);
        Assert.Equal(0, p.Attacks[0].Range); Assert.Equal(0, p.Attacks[0].Weight);
        Assert.Equal(0, p.Attacks[0].Modifier);
        Assert.Equal("magical_Fire", p.Attacks[1].Type);
        Assert.Equal(12, p.Attacks[1].Range); Assert.Equal(1000, p.Attacks[1].Weight);
        Assert.Equal(120, p.Attacks[1].Modifier);
        Assert.Equal(RetailNpcSourcePresence.ExplicitZero, p.Source.Attacks.Entries[0].Weight.Presence);
        Assert.True(p.HasNormalAttackCorrection);
    }

    [Fact]
    public void AbsentListStaysEmptyAndPartialEntryRetainsItsConstructorDefaults()
    {
        Assert.Empty(Synthetic().Attacks);
        Assert.False(Synthetic().HasNormalAttackCorrection);
        var p = Synthetic(Node("attacks", null, Node("data", null)));
        var a = Assert.Single(p.Attacks);
        Assert.Equal("physical", a.Type); Assert.Equal(1, a.Range);
        Assert.Equal(1000, a.Weight); Assert.Equal(100, a.Modifier);
        Assert.Equal(RetailNpcSourcePresence.Absent, p.Source.Attacks.Entries[0].Weight.Presence);
    }

    [Fact]
    public void ElementalChannelsAreDistinctAndDoNotFallBackToHostDefaults()
    {
        var p = Synthetic();
        StatEnum[] stats = [StatEnum.WATER_RESISTANCE, StatEnum.WIND_RESISTANCE, StatEnum.EARTH_RESISTANCE,
            StatEnum.FIRE_RESISTANCE, StatEnum.LIGHT_RESISTANCE, StatEnum.DARK_RESISTANCE];
        for (int i = 0; i < stats.Length; i++)
        {
            Assert.True(p.TrySpecialBase(stats[i], out int value));
            Assert.Equal((i + 1) * 11, value);
        }
        Assert.False(p.TrySpecialBase(StatEnum.PHYSICAL_ATTACK, out _));
    }

    private static SkyTempleNpcProfile Synthetic(JsonNode? attacks = null)
    {
        var fixture = new RetailNpcOrdinaryCoreSeedProjectionTests.Fixture();
        foreach (var (name, value) in new (string, int)[]
        {
            ("max_hp", 1234), ("max_mp", 375), ("min_damage", 10), ("max_damage", 20), ("hit_count", 1),
            ("physical_defend", 222), ("magical_defend", 333), ("magical_resist", 444), ("hit_accuracy", 555),
            ("magical_hit_accuracy", 666), ("critical", 44), ("magical_critical", 55), ("magical_skill_boost", 777),
            ("magical_skill_boost_resist", 888), ("dodge", 111), ("parry", 11), ("block", 12),
            ("physical_attack", 900000), ("magical_attack", 900001),
            ("move_speed_normal_walk", 1), ("move_speed_normal_run", 2), ("move_speed_combat_run", 3)
        }) fixture.Set(name, value);
        fixture.Add(Node("first_level_attr", null, RetailNpcTemplateProjection.PrimaryFields
            .Select((name, i) => Node(name, i == 0 ? "0" : (90 + i).ToString()))
            .Concat(RetailNpcTemplateProjection.ElementFields.Select((name, i) => Node(name, ((i + 1) * 11).ToString()))).ToArray()));
        if (attacks is not null) fixture.Add(attacks);
        var source = fixture.Inputs();
        var admitted = RetailNpcLevelRatioTableInputs.Admit(SyntheticNpcRatioTable.Bytes(),
            new(source.Receipt.DatasetId, source.Receipt.OutputManifestSha256, source.Receipt.Region,
                SyntheticNpcRatioTable.Sha256, @"Z:\synthetic\pc_stat_ratio.xml", "pc_stat_ratio.xml", "regional_missing_base_fallback"));
        Assert.True(admitted.Success); var table = admitted.Table!;
        var domain = new RetailNpcOrdinarySeedDomain(source.Receipt, table.Receipt.SourceSha256,
            true, true, 1, 0, 0, false, false, false);
        var core = RetailNpcOrdinaryCoreSeedProjection.ProjectOrdinaryCoreSeeds(source, table, domain);
        Assert.True(core.Success);
        var scalars = RetailNpcOrdinaryScalarProjection.ProjectStatistics(source, core.Seeds!, new(domain));
        var bounds = RetailNpcBoundsConstruction.Construct(source, new(source.Receipt, true, true, "Synthetic profile values"));
        Assert.True(scalars.Success); Assert.True(bounds.Success);
        return new(source, core.Seeds!, scalars.Inputs!, bounds.Plan!, "synthetic-test-inputs", statisticsOnly: true);
    }

    private static JsonArray Node(string name, string? value, params JsonNode[] children)
    {
        var content = new JsonArray();
        if (value is not null) content.Add(value);
        foreach (var child in children) content.Add(child);
        return new(JsonValue.Create(name), new JsonArray(), content);
    }
}
