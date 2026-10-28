using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public class SkyTempleNpcProfileTests
{
    [Fact]
    public void UnpinnedOrTruncatedArtifactIsRejectedBeforePublication()
    {
        Assert.Throws<InvalidDataException>(() => SkyTempleNpcProfileCatalog.Parse([]));
        Assert.Throws<InvalidDataException>(() => SkyTempleNpcProfileCatalog.Parse("{}"u8));
    }

    [Fact]
    public void MembershipAlwaysExcludesOtherMapsAndTemplates()
    {
        Assert.False(SkyTempleNpcProfileCatalog.TryGet(320040000, 212769, out _));
        Assert.False(SkyTempleNpcProfileCatalog.TryGet(320050000, 200000, out _));
    }

    // Private extracted data stays outside the checkout. Root runs this fixture with the same
    // artifact path supplied to the opt-in live bootstrap; Parse does not activate the catalog.
    [SkippableFact]
    public void EverySelectedProfileHasTheExactHpDamageAndPhysicalDefenseVector()
    {
        var profiles = Load();
        (int Id, int Hp, int Min, int Max, int Pdef)[] expected = [
            (212761,3856,226,339,199),(212762,3856,226,339,199),(212763,4122,236,355,207),
            (212764,4122,236,355,207),(212765,4808,152,228,524),(212766,4808,152,228,524),
            (212767,5141,159,239,538),(212768,5141,159,239,538),(212769,3637,181,272,351),
            (212770,3637,181,272,351),(212771,3889,189,284,362),(212772,4125,196,294,372),
            (212773,4599,154,231,524),(212774,4599,154,231,524),(212775,4917,160,240,538),
            (212776,3637,181,272,332),(212777,4917,160,240,538),(212778,5215,166,249,554),
            (212779,5141,183,275,652),(212780,5497,173,260,569),(212781,3889,189,284,343),
            (212782,5452,189,284,670),(212833,5359,355,516,207),(212835,5905,206,300,461),
            (212876,5954,216,315,474)];
        Assert.Equal(expected.Length, profiles.Count);
        foreach (var e in expected)
        {
            var p = profiles[e.Id]; var stats = p.CreateStatsTemplate();
            Assert.Equal((e.Hp,e.Min,e.Max,e.Pdef), (p.MaxHp,p.MinDamage,p.MaxDamage,p.PhysicalDefense));
            Assert.Equal(e.Hp, stats.GetMaxHp()); Assert.Equal(e.Pdef, stats.GetPdef());
            Assert.Equal(0, p.MagicalDefense); Assert.Equal(0, stats.GetMdef());
            Assert.Equal(0, stats.GetAttack()); Assert.Equal(0, stats.GetMagicalAttack());
            Assert.Equal(0, stats.GetMaxMp()); Assert.Equal(1000, p.StatRatio);
            Assert.Equal(SkyTempleNpcProfileCatalog.DatasetId, p.DatasetId);
            Assert.Equal(SkyTempleNpcProfileCatalog.ArtifactSha256, p.Generation);
            Assert.Equal(3, p.Source.Source.Blockers.Length);
            Assert.False(p.Source.Source.ActivationReady); Assert.False(p.Source.Source.FixtureVerified);
        }
        Assert.Equal(31, profiles[212781].Level); // Raw level31; devname suffix32 is not an override.
        Assert.Equal(1928, profiles[212761].HpRegen); Assert.Equal(2233, profiles[212876].HpRegen);
    }

    [SkippableFact]
    public void OrderedAttacksAndAll59SkillsRetainSourceMetadataAndAbsentDefaults()
    {
        var profiles = Load(); var skills = profiles.Values.SelectMany(p => p.Skills).ToArray();
        Assert.Equal(59, skills.Length); Assert.Equal(20, skills.Select(s => s.Id48).Distinct().Count());
        Assert.Equal(45, skills.Count(s => s.Rate == 0));
        Assert.Equal(5, skills.Count(s => !s.CountPresent)); Assert.Equal(45, skills.Count(s => !s.DelayPresent));
        Assert.All(skills.Where(s => !s.CountPresent), s => Assert.Equal(0, s.Count));
        Assert.All(skills.Where(s => !s.DelayPresent), s => Assert.Equal(0, s.Delay));
        foreach (var p in profiles.Values)
        {
            var attack = Assert.Single(p.Attacks);
            Assert.Equal(0, attack.Ordinal); Assert.Equal(1000, attack.Weight); Assert.Equal(100, attack.Modifier);
            Assert.Equal(p.Skills.Length, p.Skills.Select(s => s.Ordinal).Distinct().Count());
            Assert.Equal(Enumerable.Range(0,p.Skills.Length), p.Skills.Select(s => s.Ordinal));
        }
        Assert.Equal(new[] {17039,16725}, profiles[212770].Skills.Select(s => s.Id48));
        Assert.Equal(new[] {21,21}, profiles[212770].Skills.Select(s => s.Level));
        Assert.Equal(new[] {16723,16592,16522,16710}, profiles[212780].Skills.Select(s => s.Id48));
        Assert.Equal(16f, profiles[212761].Attacks[0].Range);
        Assert.Equal("magical_water", profiles[212761].Attacks[0].Type);
        Assert.Equal("physical", profiles[212776].Attacks[0].Type);
        Assert.Equal(2486, profiles[212769].AttackDelay);
    }

    [SkippableFact]
    public void SourceBoundsSpeedsAndZeroPrimariesSurviveTheRuntimeStatsTemplate()
    {
        var profiles = Load(); var air = profiles[212769]; var fire = profiles[212774];
        Assert.Equal(new SkyTempleNpcBounds(.7f,1.2f,1.17f,.6f), air.Bounds);
        Assert.Equal(.829f, air.NormalWalkSpeed); Assert.Equal(8f, air.NormalRunSpeed); Assert.Equal(6f, air.CombatRunSpeed);
        // Scale140 is applied once to raw bounds, independently of the unscaled movement-speed source.
        Assert.Equal(.8f*1.4f, fire.Bounds.Front); Assert.Equal(1.36f*1.4f, fire.Bounds.Upper);
        Assert.Equal(7.5f, fire.NormalRunSpeed); Assert.Equal(5.5f, fire.CombatRunSpeed);
        foreach (var p in profiles.Values)
        {
            var stats = p.CreateStatsTemplate();
            Assert.Equal(0, stats.GetPower()); Assert.Equal(0, stats.GetHealth());
            Assert.Equal(0, stats.GetAgility()); Assert.Equal(0, stats.GetBaseAccuracy());
            Assert.Equal(0, stats.GetKnowledge()); Assert.Equal(0, stats.GetWill());
            Assert.Equal(p.NormalWalkSpeed, stats.GetWalkSpeed()); Assert.Equal(p.NormalRunSpeed, stats.GetRunSpeed());
            Assert.Equal(p.CombatRunSpeed, stats.GetRunSpeedFight());
            Assert.Equal(15000, p.MaxChaseMilliseconds); Assert.Equal(2f, p.ReturnSpeedMultiplier);
            Assert.True(p.TrySpecialBase(StatEnum.FIRE_RESISTANCE, out int value));
            Assert.Equal(p.Values["elemental_defend_fire"], value);
            Assert.True(p.TrySpecialBase(StatEnum.PERIFICATION_RESISTANCE, out int petrification));
            Assert.Equal(p.Values["abnormal_resist[13]"], petrification);
            Assert.True(p.TrySpecialBase(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, out int physicalReduction));
            Assert.Equal(p.Values["physical_critical_damage_reduce"], physicalReduction);
            Assert.True(p.TrySpecialBase(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, out int magicalReduction));
            Assert.Equal(p.Values["magical_critical_damage_reduce"], magicalReduction);
        }
    }

    [SkippableFact]
    public void ActualSpawnedNpcLifeStatsLevelDefenseAndOrderedSkillsConsumeEveryProfile()
    {
        byte[] bytes = Source();
        var profiles = SkyTempleNpcProfileCatalog.Parse(bytes);
        using var scope = SkyTempleNpcProfileCatalog.InstallForTests(bytes);
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        int bindings = 0;
        foreach (var p in profiles.Values.OrderBy(p => p.NpcId))
        {
            var npc = h.SpawnWithAi(p.NpcId, "general", 100, 100, 200);
            Assert.Equal(p.Level, (int)npc.GetLevel());
            Assert.Equal(p.MaxHp, npc.GetLifeStats().GetMaxHp());
            Assert.Equal(p.MaxHp, npc.GetLifeStats().GetCurrentHp());
            Assert.Equal(p.MaxHp, npc.GetGameStats().GetMaxHp().GetCurrent());
            Assert.Equal(0, npc.GetGameStats().GetMDef().GetCurrent());
            Assert.Equal(p.PhysicalDefense, npc.GetGameStats().GetPDef().GetCurrent());
            Assert.Equal(p.HpRegen, npc.GetGameStats().GetHpRegenRate().GetCurrent());
            Assert.Equal(p.Values["physical_critical_damage_reduce"], npc.GetGameStats().GetStat(StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent());
            Assert.Equal(p.Values["magical_critical_damage_reduce"], npc.GetGameStats().GetStat(StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE, 0).GetCurrent());
            Assert.Equal(p.SourceRecordSha256, npc.GetGameStats().RetailProfile!.SourceRecordSha256);
            Assert.Equal(p.Skills.Length, npc.GetSkillList().Size());
            foreach (var skill in p.Skills)
            {
                var actual = npc.GetSkillList().GetSkillOnPosition(skill.Ordinal);
                Assert.Equal(skill.Id48, actual.GetSkillId()); Assert.Equal(skill.Level, actual.GetSkillLevel());
                Assert.Equal(skill.Level, Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc,skill.Id48));
                bindings++;
            }
            Assert.Equal(7, Aion.GameServer.Ai.NpcSkillCasting.LevelOf(npc, -1, 7));
        }
        Assert.Equal(59, bindings); Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SelectedNpcFirstAttackIsReadyThenTheEmittedAttackOwnsItsCadence()
    {
        using var scope = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var npc = h.SpawnWithAi(212761, "general", 100, 100, 200);
        npc.SetTarget(h.SpawnPlayer(101, 100, 200));
        Assert.Equal(0, npc.GetGameStats().GetNextAttackInterval());
        int cadence = npc.GetGameStats().GetAttackSpeed().GetCurrent();
        npc.GetGameStats().RenewLastAttackTime(); // Legacy diagnostic timestamp does not own retail728.
        Assert.Equal(0, npc.GetGameStats().GetNextAttackInterval());
        Aion.GameServer.Controllers.Attack.RetailNpc.SkyTempleNpcSkillState.NormalEmitted(npc,
            Aion.GameServer.Utils.SystemClock.RetailTick32());
        Assert.Equal(cadence, npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(cadence - 1));
        Assert.Equal(1, npc.GetGameStats().GetNextAttackInterval());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, npc.GetGameStats().GetNextAttackInterval());
        npc.GetGameStats().ResetFightStats();
        Assert.Equal(0, npc.GetGameStats().GetNextAttackInterval());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SameNpcTemplateOutsideSkyTempleRetainsLegacyFirstAttack750ms()
    {
        using var scope = SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h = BossAiHarness.For(220020000).Build();
        var npc = h.SpawnWithAi(212761, "general", 100, 100, 200);
        npc.SetTarget(h.SpawnPlayer(101, 100, 200));
        Assert.Null(npc.GetGameStats().RetailProfile);
        Assert.Equal(750, npc.GetGameStats().GetNextAttackInterval());
        Assert.Empty(h.Clock.Faults);
    }

    private sealed class OwnerVirtualProbe(
        Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawn,
        Aion.GameServer.Model.Templates.Npc.NpcTemplate template)
        : Aion.GameServer.Model.GameObjects.Npc(new Aion.GameServer.Controllers.NpcController(),spawn,template)
    {
        public override int GetCreatorId() => throw new InvalidOperationException("Owner is not initialized during base construction");
    }

    [SkippableTheory]
    [InlineData(220020000,212876)]
    [InlineData(320050000,730042)]
    public void UnmatchedNpcConstructionNeverReadsVirtualOwnerState(int map,int id)
    {
        using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h=BossAiHarness.For(map).Build();
        var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(map,id,100,100,200,0,null,"general");
        var npc=new OwnerVirtualProbe(spawn,Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(id));
        Assert.False(SkyTempleNpcProfileCatalog.TryGet(npc,out _));
        Assert.Null(npc.GetGameStats().RetailProfile);
    }

    [SkippableFact]
    public void MatchedNpcWithSpawnOwnerKeepsLegacyStatsBeforeFactoryAssignsCreatorId()
    {
        using var profiles=SkyTempleNpcProfileCatalog.InstallForTests(Source());
        using var h=BossAiHarness.For(SkyTempleNpcProfileCatalog.MapId).Build();
        var spawn=Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(SkyTempleNpcProfileCatalog.MapId,
            212876,100,100,200,0,123);
        var template=Aion.GameServer.Dataholders.DataManager.NPC_DATA.GetNpcTemplate(212876);
        var npc=new Aion.GameServer.Model.GameObjects.Npc(new Aion.GameServer.Controllers.NpcController(),spawn,template);
        Assert.Equal(0,npc.GetCreatorId());
        Assert.False(SkyTempleNpcProfileCatalog.TryGet(npc,out _));
        Assert.Null(npc.GetGameStats().RetailProfile);
        Assert.Equal(57376,npc.GetLifeStats().GetMaxHp());
    }

    private static System.Collections.Frozen.FrozenDictionary<int,SkyTempleNpcProfile> Load()
        => SkyTempleNpcProfileCatalog.Parse(Source());
    private static byte[] Source()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_TEST_DATA")
            ?? Environment.GetEnvironmentVariable("AION_RETAIL_SKYTEMPLE_DATA");
        Skip.If(string.IsNullOrWhiteSpace(path), "Set AION_RETAIL_SKYTEMPLE_TEST_DATA to the private pinned25 artifact.");
        return File.ReadAllBytes(path!);
    }
}
