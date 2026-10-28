using System.Text.Json;
using System.Reflection;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpc17860CastFixtureTests
{
    [SkippableFact]
    public void SelectedKasikaProfileSuppliesScaledNpcDimensionsToFireballFixture()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned retail package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        // The selected 5.8 record has front 1.23, side .8, scale 130%.
        Assert.Equal(1.599f,profile.Bounds.Front,3);
        Assert.Equal(1.04f,profile.Bounds.Side,3);
        // .5/.5 is the traced initial player class, supplied as a fixture assumption.
        Assert.True(RetailNpc17860CastFixture.InSourceFireballForOfflineFixture(profile,0,0,0,4,0,7,.5f,.5f));
        Assert.False(RetailNpc17860CastFixture.InSourceFireballForOfflineFixture(profile,0,0,0,4.1f,0,0,.5f,.5f));
        Assert.False(RetailNpc17860CastFixture.InSourceFireballForOfflineFixture(profile,0,0,0,4,0,7.01f,.5f,.5f));
    }

    [Fact]
    public void FireballFixtureUsesHorizontalRangeAndSeparateInclusiveHeight()
    {
        static bool In(float x,float y,float z,float casterWidth=0,float casterDepth=0,float targetWidth=0,float targetDepth=0)
            =>RetailNpc17860CastFixture.InFireballForOfflineFixture(0,0,0,x,y,z,casterWidth,casterDepth,targetWidth,targetDepth);
        Assert.True(In(0,0,7)); // Above a spherical radius of three, but inside the retail height.
        Assert.True(In(0,0,-7));
        Assert.False(In(0,0,7.001f));
        Assert.False(In(0,0,-7.001f));
        Assert.True(In(2.999f,0,0));
        Assert.False(In(3,0,0)); // Horizontal radius uses a strict comparison.
        Assert.True(In(4.9f,0,7,2,4,1,2)); // Half of each actor's larger native dimension.
        Assert.False(In(6,0,0,2,4,1,2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>In(float.NaN,0,0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>In(0,0,0,-1));
    }

    [Fact]
    public void ExistingAreaCapKeepsClosestTargetsRatherThanInputOrder()
    {
        // This documents the host rule, not a claim that it matches the 5.8 collector.
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
        var first=RetailNpcStatPeriodicTests.Player(h,220020000);
        first.SetPosition(h.World.CreatePosition(220020000,985,135,242,0,1));
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,17860,1,first);
        Assert.NotNull(skill);
        var properties=skill.GetSkillTemplate().GetProperties();
        Assert.Equal(24,properties.GetTargetMaxCount());
        Assert.Equal(TargetRangeAttribute.AREA,properties.GetTargetType());
        var candidates=new List<Creature>();
        for(int i=25;i>=1;i--)
        {
            var player=RetailNpcStatPeriodicTests.Player(h,220020000);
            player.SetPosition(h.World.CreatePosition(220020000,985+i*0.1f,135,242,0,1));
            candidates.Add(player);
        }
        var targets=new List<Creature>{first};targets.AddRange(candidates);
        var result=new Properties.ValidationResult(targets,first);
        Assert.True(MaxCountProperty.Set(properties,result));
        Assert.Equal(24,targets.Count);
        Assert.Same(first,targets[0]);
        Assert.DoesNotContain(candidates[0],targets);
        Assert.DoesNotContain(candidates[1],targets);
        Assert.Contains(candidates[2],targets);
        Assert.Contains(candidates[^1],targets);
    }

    private sealed class FixturePlayerController:PlayerController
    {
        internal bool DeathReported;
        public override void OnDie(Creature lastAttacker)=>DeathReported=true;
    }

    [SkippableFact]
    public void SelectedMarkerDrivesPreparedAreaTargetsAndIndependentResistance()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(hash),"Requires pinned timing candidate package");
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] source=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var candidate=RetailNpc17860PrimaryProjection.ReadForOfflineFixture(source);
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220020000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(212874,"retail_stat_base_fixture");
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,0)]);
        Player MakeTarget(int resist)
        {
            var p=RetailNpcStatPeriodicTests.Player(h,220020000);p.GetCommonData().SetLevel(40);
            p.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
                new StatSetFunction(StatEnum.MAGICAL_RESIST,resist),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
                new StatSetFunction(StatEnum.EARTH_RESISTANCE,0),new StatSetFunction(StatEnum.REGEN_HP,0)]);
            p.GetLifeStats().OnStatsChange(null!);p.GetLifeStats().SetCurrentHp(80000);
            BossAiHarness.MakeMutuallyKnown(npc,p);return p;
        }
        var first=MakeTarget(0);var second=MakeTarget(0);var resistant=MakeTarget(9999);var lethal=MakeTarget(0);
        var lethalController=new FixturePlayerController();lethalController.SetOwner(lethal);
        typeof(VisibleObject).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(lethal,lethalController);
        lethal.GetLifeStats().SetCurrentHp(1);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,17860,1,first);Assert.NotNull(skill);
        skill.GetEffectedList().Add(first);skill.GetEffectedList().Add(second);skill.GetEffectedList().Add(resistant);skill.GetEffectedList().Add(lethal);
        Assert.True(RetailNpcStatCatalog.TryGetSkillTiming(npc,17860,1,out var timing));Assert.Equal(733,timing.Word);
        var e=new RetailNpc17860DefenseEffect.ElementInput(500,0,0,-10000,10000);
        var inputs=new RetailNpc17860DefenseEffect.Inputs(2000,1000,0,0,e,e,e,e,e,e);
        bool IsSelected(Player p)=>ReferenceEquals(p,first)||ReferenceEquals(p,second)||ReferenceEquals(p,resistant)||ReferenceEquals(p,lethal);
        var altered=(byte[])source.Clone();altered[^1]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpc17860CastFixture.AttachFromCatalogForOfflineFixture(skill,candidate,altered,_=>inputs,IsSelected,(_,_,high)=>high));
        RetailNpc17860CastFixture.AttachFromCatalogForOfflineFixture(skill,candidate,source,_=>inputs,IsSelected,
            (p,low,high)=>ReferenceEquals(p,resistant)?low:high);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(castMs>0);int hpFirst=first.GetLifeStats().GetCurrentHp(),hpSecond=second.GetLifeStats().GetCurrentHp();
        int hpResistant=resistant.GetLifeStats().GetCurrentHp();
        int defFirst=first.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent();
        int defSecond=second.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent();
        Assert.True(skill.UseWithoutPropSkill());Assert.Equal(733,skill.GetHitTime());
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs));
        var effects=RetailNpc17860CastFixture.ObservedEffectsForOfflineFixture(skill);Assert.Equal(4,effects.Length);
        var firstEffect=Assert.Single(effects,x=>ReferenceEquals(x.GetEffected(),first));
        var secondEffect=Assert.Single(effects,x=>ReferenceEquals(x.GetEffected(),second));
        var resistedEffect=Assert.Single(effects,x=>ReferenceEquals(x.GetEffected(),resistant));
        var lethalEffect=Assert.Single(effects,x=>ReferenceEquals(x.GetEffected(),lethal));
        Assert.True(firstEffect.IsInSuccessEffects(1));Assert.True(secondEffect.IsInSuccessEffects(1));
        Assert.False(resistedEffect.IsInSuccessEffects(1));Assert.False(resistedEffect.IsInSuccessEffects(2));
        int firstDamage=firstEffect.GetReserveds(1).GetValue(),secondDamage=secondEffect.GetReserveds(1).GetValue(),lethalDamage=lethalEffect.GetReserveds(1).GetValue();
        Assert.True(firstDamage>0);Assert.True(secondDamage>0);Assert.True(lethalDamage>0);
        Assert.Equal(hpFirst,first.GetLifeStats().GetCurrentHp());Assert.Equal(hpSecond,second.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(682));Assert.Equal(hpFirst,first.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(hpFirst-firstDamage,first.GetLifeStats().GetCurrentHp());Assert.Equal(hpSecond-secondDamage,second.GetLifeStats().GetCurrentHp());
        Assert.Equal(hpResistant,resistant.GetLifeStats().GetCurrentHp());Assert.Equal(0,lethal.GetLifeStats().GetCurrentHp());Assert.True(lethalController.DeathReported);
        Assert.Equal(defFirst,first.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        h.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Assert.Equal(defFirst-100,first.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        Assert.Equal(defSecond-100,second.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        Assert.Null(resistant.GetEffectController().GetAbnormalEffect(firstEffect.GetStack()));
        Assert.Null(lethal.GetEffectController().GetAbnormalEffect(firstEffect.GetStack()));
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));
        int expectedHeal=RetailNpcStatSpellDrain.HealAmount(firstDamage,200)+RetailNpcStatSpellDrain.HealAmount(secondDamage,200)
            +RetailNpcStatSpellDrain.HealAmount(lethalDamage,200);
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+expectedHeal),npc.GetLifeStats().GetCurrentHp());
        firstEffect.EndEffect(false);secondEffect.EndEffect(false);
        Assert.Equal(defFirst,first.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        Assert.Equal(defSecond,second.GetGameStats().GetStat(StatEnum.PHYSICAL_DEFENSE,1000).GetCurrent());
        Assert.Empty(h.Clock.Faults);
    }
}
