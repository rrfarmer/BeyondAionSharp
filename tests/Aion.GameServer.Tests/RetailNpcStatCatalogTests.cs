using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatCatalogTests
{
    [Theory]
    [InlineData("BN_Charmed_idnovice","BN_Charmed_IDNovice",true)]
    [InlineData("Spell","SPELL",true)]
    [InlineData("Spell ","Spell",false)]
    [InlineData("Spell-A","Spell_A",false)]
    [InlineData("Straße","STRASSE",false)]
    [InlineData("ÄSpell","äspell",false)]
    [InlineData("ÄSpell","ÄSpell",true)]
    [InlineData(null,"Spell",false)]
    public void NativeSkillNamesUseTheVerifiedAsciiContractOnly(string? left,string? right,bool expected)
        =>Assert.Equal(expected,RetailNpcStatCatalog.SameNativeSkillName(left,right));

    [SkippableFact]
    public void EveryReviewedCaseOnlyBindingResolvesItsExactNativeRecordAndAuthoredLevel()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_NAME_REVIEW");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_NAME_REVIEW_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires the pinned external case-only source census");
        var bytes=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var doc=System.Text.Json.JsonDocument.Parse(bytes);
        var f=Fixture();using var installed=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        int count=0;var skills=new HashSet<int>();var npcs=new HashSet<int>();
        foreach(var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            Assert.True(row.GetProperty("candidate_independent_stack_agrees").GetBoolean());
            int npc=row.GetProperty("npc").GetInt32(),id=Assert.Single(row.GetProperty("native_candidates").EnumerateArray()).GetInt32();
            var profile=RetailNpcStatCatalog.Members.First(m=>m.Profile.NpcId==npc).Profile;
            var groups=profile.Source.Source.GetRawRootChildren("skills");Assert.Single(groups);
            int ordinal=row.GetProperty("source_entry_index").GetInt32();
            var entries=Aion.GameServer.Dataholders.RetailNpc.RetailNpcSourceProfile.RawChildren(groups[0]).Where(x=>x[0].GetString()=="data").ToArray();
            int level=SkyTempleNpcProfile.LeafInt(entries[ordinal],"skill_level");
            Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,id,level,out var source),$"{npc}/{id}/{level}");
            Assert.Equal(Assert.Single(row.GetProperty("native_names").EnumerateArray()).GetString(),source.Name);
            Assert.NotEqual(row.GetProperty("reference_name").GetString(),source.Name);
            Assert.True(RetailNpcStatCatalog.SameNativeSkillName(row.GetProperty("reference_name").GetString(),source.Name));
            count++;skills.Add(id);npcs.Add(npc);
        }
        Assert.Equal(130,count);Assert.Equal(17,skills.Count);Assert.Equal(126,npcs.Count);
    }

    private sealed class CriticalRateOwner : Aion.GameServer.Model.Stats.Calc.IStatOwner { }
    [Aion.GameServer.Ai.AIName("retail_stat_base_fixture")]
    public sealed class BaseFixtureAI(Aion.GameServer.Model.GameObjects.Npc owner):Aion.GameServer.Ai.NpcAI(owner)
    {
        public override bool CanThink()=>false;
        protected override void HandleSpawned()=>SetStateIfNot(Aion.GameServer.Ai.AIState.IDLE);
    }

    [SkippableFact]
    public void SelectedIncomingSpellUsesSourceCapBeforeDefenseWithoutChangingPlayerStatPrograms()
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220010000).Build();
        var npc=h.SpawnWithAi(210649,"aggressive",100,100,200);var player=h.SpawnPlayer(101,100,200);
        player.GetCommonData().SetLevel(16);
        Assert.Equal(3400,RetailNpcStatCatalog.MagicBoostCap);
        Assert.Equal(1000,npc.GetGameStats().StatisticsProfile!.StatRatio);
        Assert.Equal(0,npc.GetGameStats().GetMBResist().GetCurrent());
        Assert.Equal(0,npc.GetGameStats().GetMDef().GetCurrent());
        var owner=new CriticalRateOwner();
        player.GetGameStats().AddEffectOnly(owner,
            [new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(Aion.GameServer.Model.Stats.Container.StatEnum.BOOST_MAGICAL_SKILL,10000,false)]);
        Assert.Equal(10000,player.GetGameStats().GetMBoost().GetCurrent());
        var template=new Aion.GameServer.SkillEngine.Effects.SpellAttackInstantEffect {Element=Aion.GameServer.Model.SkillElement.FIRE};
        Assert.True(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryMagicalSkillBase(player,npc,500,0,template,true,false,false,out var amount));
        Assert.Equal(2200f,amount);
        Assert.Equal(10000,player.GetGameStats().GetMBoost().GetCurrent());
        Assert.True(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryMagicalSkillBase(player,npc,500,0,template,false,false,false,out amount));
        Assert.Equal(500f,amount);
    }

    [SkippableTheory]
    [InlineData(true,-25f,1f)]
    [InlineData(false,0f,1f)]
    [InlineData(true,.75f,1f)]
    [InlineData(false,123.5f,123.5f)]
    public void SelectedCommonSkillMinimumPrecedesTargetTrimWithoutChangingOtherConsumers(bool physical,float amount,float expected)
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220030000).Build();
        var npc=h.SpawnWithAi(210582,"aggressive",100,100,200);var player=h.SpawnPlayer(101,100,200);
        Aion.GameServer.SkillEngine.Effects.DamageEffect primary=physical
            ?new Aion.GameServer.SkillEngine.Effects.SkillAttackInstantEffect():new Aion.GameServer.SkillEngine.Effects.SpellAttackInstantEffect();
        var common=Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryCommonSkillMinimum(player,npc,primary,amount,out var result);
        Assert.True(common);Assert.Equal(expected,result);
        Assert.True(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryTargetTrim(player,npc,physical,result,out var trimmed));
        Assert.Equal(1f,trimmed);
        foreach(var other in new Aion.GameServer.SkillEngine.Effects.DamageEffect[]
            {new Aion.GameServer.SkillEngine.Effects.NoReduceSpellATKInstantEffect(),new Aion.GameServer.SkillEngine.Effects.ProcAtkInstantEffect(),new Aion.GameServer.SkillEngine.Effects.DelayedSpellAttackInstantEffect()})
        {
            Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryCommonSkillMinimum(player,npc,other,amount,out result));
            Assert.Equal(amount,result);
        }
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryCommonSkillMinimum(npc,player,primary,amount,out result));
        Assert.Equal(amount,result);
        npc.SetCreatorId(42);
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryCommonSkillMinimum(player,npc,primary,amount,out result));
        Assert.Equal(amount,result);
    }

    [SkippableTheory]
    [InlineData(true,100f,1f)]
    [InlineData(false,100f,1f)]
    [InlineData(true,0.25f,1f)]
    [InlineData(false,0f,0f)]
    public void SourceFixedDamageBelongsToTheNpcTargetAndPreservesZero(bool physical,float damage,float expected)
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220030000).Build();
        var box=h.SpawnWithAi(210582,"aggressive",100,100,200);var player=h.SpawnPlayer(101,100,200);
        Assert.Equal(1,box.GetGameStats().StatisticsProfile!.PhysicalDamageTrim);
        Assert.Equal(1,box.GetGameStats().StatisticsProfile!.MagicalDamageTrim);
        Assert.True(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryTargetTrim(player,box,physical,damage,out float received));
        Assert.Equal(expected,received);
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TryTargetTrim(box,player,physical,damage,out float outgoing));
        Assert.Equal(damage,outgoing);
    }

    [SkippableFact]
    public void TemplateDamageKeepsItsExistingPveBypass()
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220010000).Build();var npc=h.SpawnWithAi(210649,"aggressive",100,100,200);
        var player=h.SpawnPlayer(101,100,200);
        Assert.False(Aion.GameServer.Controllers.Attack.RetailNpc.SkyTemplePlayerCombat.TrySkillPve(player,npc,true,100,out float result,useTemplateDamage:true));
        Assert.Equal(100f,result);
    }

    [SkippableFact]
    public void StatisticsNeverFillMissingHostMovementWithSourceSpeeds()
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        Assert.True(RetailNpcStatCatalog.TryGet(220010000,210649,out var profile));
        Assert.True(profile.NormalWalkSpeed>0);
        var host=new Aion.GameServer.Model.Templates.Stats.StatsTemplate();
        var selected=profile.CreateStatsTemplate(host);
        Assert.Null(selected.Speeds);
        Assert.Equal(0f,selected.GetWalkSpeed());Assert.Equal(0f,selected.GetRunSpeed());
        Assert.Equal(0f,selected.GetRunSpeedFight());Assert.Equal(0f,selected.GetFlySpeed());
        host.Speeds=new Aion.GameServer.Model.Templates.Stats.CreatureSpeeds {WalkSpeed=0,RunSpeed=7};
        selected=profile.CreateStatsTemplate(host);
        Assert.Same(host.Speeds,selected.Speeds);Assert.Equal(0f,selected.GetWalkSpeed());Assert.Equal(7f,selected.GetRunSpeed());
    }

    [SkippableTheory]
    [InlineData(-1,0)]
    [InlineData(724,724)]
    [InlineData(772,772)]
    [InlineData(829,829)]
    [InlineData(10000,9999)]
    public void NativeNpcCriticalRateUsesItsOwnCapWithoutChangingDamageOrPlayerCaps(int bonus,int expected)
    {
        var f=Fixture();using var scope=RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h=BossAiHarness.For(220010000).Build();
        var npc=h.SpawnWithAi(210649,"aggressive",100,100,200);
        Assert.Equal(0,npc.GetGameStats().StatisticsProfile!.Values["physical_critical_reduce_rate"]);
        npc.GetGameStats().AddEffectOnly(new CriticalRateOwner(),
            [new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_CRITICAL_RESIST,bonus,false),
             new Aion.GameServer.Model.Stats.Calc.Functions.StatAddFunction(Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,10000,false)]);
        Assert.Equal(expected,npc.GetGameStats().GetPCR().GetCurrent());
        Assert.Equal(700,npc.GetGameStats().GetStat(Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,10000).GetCurrent());
        var player=h.SpawnPlayer(101,100,200);
        Assert.Equal(700,player.GetGameStats().GetStat(Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_CRITICAL_RESIST,10000).GetCurrent());
        npc.SetCreatorId(42);
        Assert.Null(npc.GetGameStats().StatisticsProfile);
        Assert.Equal(700,npc.GetGameStats().GetStat(Aion.GameServer.Model.Stats.Container.StatEnum.PHYSICAL_CRITICAL_RESIST,10000).GetCurrent());
    }
    [Fact]
    public async Task StatisticsRequireBothExplicitManifestAndDigest()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => RetailNpcStatCatalog.InitializeAsync("missing", null));
        Assert.False(RetailNpcStatCatalog.Enabled);
    }

    [SkippableFact]
    public void CompletePriorityMembershipDoesNotEnableMovementOrReplaceSkills()
    {
        var f = Fixture();
        using var scope = RetailNpcStatCatalog.InstallForTests(f.Bytes, f.Hash, f.Directory);
        Assert.NotEmpty(RetailNpcStatCatalog.Members);
        foreach (var group in RetailNpcStatCatalog.Members.GroupBy(x => x.MapId))
        {
            // Map0 is catalog membership for conditional/global templates,
            // not a playable map. Exercise its fallback in a supported world.
            int fixtureMap=group.Key==0?220010000:group.Key;
            using var h = BossAiHarness.For(fixtureMap).WithAi(typeof(BaseFixtureAI)).Build();
            foreach (var member in group)
            {
                var p = member.Profile;
                var template = DataManager.NPC_DATA.GetNpcTemplate(p.NpcId);
                // This gate checks base materialization. Actual post-spawn
                // buffs have separate current-defense/Effect/SIM coverage.
                var npc = h.SpawnWithAi(p.NpcId, "retail_stat_base_fixture", 100, 100, 200);
                Assert.True(RetailNpcStatCatalog.TryGet(npc, out var selected),$"Missing stats for {p.NpcId} in {fixtureMap} (membership {group.Key})"); Assert.Same(p, selected);
                Assert.False(SkyTempleNpcProfileCatalog.TryGet(npc, out _));
                Assert.Null(npc.GetGameStats().RetailProfile); Assert.Same(p, npc.GetGameStats().StatisticsProfile);
                Assert.True(p.StatisticsOnly); Assert.Empty(p.Skills);
                // The profile retains the exact source integer; host Stat2
                // materializes binary32. Check that explicit boundary rather
                // than requiring unrepresentable large integers to survive it.
                int materializedHp=(int)(float)p.MaxHp;
                Assert.InRange(Math.Abs((double)materializedHp-p.MaxHp)/Math.Max(1,p.MaxHp),0,0.0000001);
                Assert.Equal(materializedHp, npc.GetLifeStats().GetMaxHp()); Assert.Equal(materializedHp, npc.GetLifeStats().GetCurrentHp());
                Assert.Equal(p.Level, (int)npc.GetLevel());
                Assert.Equal(p.PhysicalDefense, npc.GetGameStats().GetPDef().GetCurrent());
                Assert.Equal(p.MagicalDefense, npc.GetGameStats().GetMDef().GetCurrent());
                Assert.Equal(p.MagicalResist, npc.GetGameStats().GetMResist().GetCurrent());
                Assert.Equal(template.GetStatsTemplate().GetRunSpeed(), npc.GetGameStats().GetStatsTemplate().GetRunSpeed());
                Assert.Equal(template.GetStatsTemplate().GetWalkSpeed(), npc.GetGameStats().GetStatsTemplate().GetWalkSpeed());
                Assert.Equal(template.GetAttackRange() * 1000f, npc.GetGameStats().GetAttackRange().GetExactCurrent());
                Assert.Equal(template.GetStatsTemplate().GetAttack(), npc.GetGameStats().GetStatsTemplate().GetAttack());
            }
        }
        Assert.True(RetailNpcStatCatalog.TryGet(220010000, 210649, out var sparkie));
        Assert.Equal((199, 22, 33, 80), (sparkie.MaxHp, sparkie.MinDamage, sparkie.MaxDamage, sparkie.PhysicalDefense));
        Assert.Equal(0, sparkie.MagicalDefense);
        Assert.True(RetailNpcStatCatalog.TryGet(220010000, 210596, out var chest));
        Assert.True(chest.HasNormalAttackCorrection);
        Assert.Equal(("physical",1f,1000,100),(chest.Attacks[0].Type,chest.Attacks[0].Range,chest.Attacks[0].Weight,chest.Attacks[0].Modifier));
        Assert.True(RetailNpcStatCatalog.TryGet(220030000, 204260, out var shaman));
        Assert.Equal(2, shaman.Attacks.Length);
    }

    [SkippableFact]
    public void NativeZeroKnowledgeDoesNotEraseACompatibilitySpell()
    {
        var f = Fixture(); using var scope = RetailNpcStatCatalog.InstallForTests(f.Bytes,f.Hash,f.Directory);
        using var h = BossAiHarness.For(220010000).Build();
        var source = h.SpawnWithAi(210649,"aggressive",100,100,200);
        var target = h.SpawnPlayer(101,100,200);
        var effect = new Aion.GameServer.SkillEngine.Effects.SpellAttackInstantEffect();
        Assert.Equal(0,source.GetGameStats().GetKnowledge().GetCurrent());
        float actual = Aion.GameServer.Utils.Stats.StatFunctions.CalculateMagicalSkillDamage(source,target,100,0,effect,false,true,false);
        // This explicit compatibility path retains its independent +/-8% roll.
        // Zero NPC KNO must not turn the whole spell into zero damage.
        Assert.InRange(actual,92f,108f);
    }

    [SkippableFact]
    public void LateOwnerAssignmentRestoresTheOriginalFreshNpcStatsAndHp()
    {
        var f = Fixture(); using var scope = RetailNpcStatCatalog.InstallForTests(f.Bytes, f.Hash, f.Directory);
        using var h = BossAiHarness.For(220030000).Build();
        var host = DataManager.NPC_DATA.GetNpcTemplate(204260);
        var spawn = Aion.GameServer.SpawnEngine.SpawnEngine.NewSingleTimeSpawn(220030000, 204260, 100, 100, 200, 0);
        var npc = new Aion.GameServer.Model.GameObjects.Npc(new Aion.GameServer.Controllers.NpcController(), spawn, host);
        Assert.NotNull(npc.GetGameStats().StatisticsProfile);
        Assert.NotEqual(host.GetStatsTemplate().GetMaxHp(), npc.GetLifeStats().GetCurrentHp());
        npc.SetCreatorId(42);
        Assert.Null(npc.GetGameStats().StatisticsProfile);
        Assert.False(RetailNpcStatCatalog.TryGet(npc, out _));
        Assert.Same(host.GetStatsTemplate(), npc.GetGameStats().GetStatsTemplate());
        Assert.Equal(host.GetStatsTemplate().GetMaxHp(), npc.GetLifeStats().GetCurrentHp());
        Assert.Equal(host.GetStatsTemplate().GetMaxHp(), npc.GetLifeStats().GetMaxHp());
        npc.SetCreatorId(0);
        Assert.False(RetailNpcStatCatalog.TryGet(npc, out _)); // Never silently re-admit a changed actor context.
    }

    [SkippableFact]
    public void AStatisticsPackageCannotSilentlyOverlapOrChangeSourceBytes()
    {
        var f = Fixture(); var root = JsonNode.Parse(f.Bytes)!;
        var groups = root["cohorts"]!.AsArray(); var copy = groups[0]!.DeepClone(); copy["id"] = "overlap"; groups.Add(copy);
        byte[] changed = System.Text.Encoding.UTF8.GetBytes(root.ToJsonString());
        Assert.Throws<InvalidDataException>(() => RetailNpcStatCatalog.InstallForTests(changed,
            Convert.ToHexStringLower(SHA256.HashData(changed)), f.Directory));
        Assert.Throws<InvalidDataException>(() => RetailNpcStatCatalog.InstallForTests(f.Bytes.Concat(new byte[] { 32 }).ToArray(), f.Hash, f.Directory));
        Assert.False(RetailNpcStatCatalog.Enabled);
    }

    private static (byte[] Bytes, string Hash, string Directory) Fixture()
    {
        string? path = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash = Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash), "Requires the external statistics-only package");
        return (File.ReadAllBytes(path!), hash!, Path.GetDirectoryName(Path.GetFullPath(path!))!);
    }
}
