using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatHealTests
{
    private sealed class Owner:IStatOwner { }

    [Fact]
    public void AbsentBoostHeaderUsesConstructorZeroAndExplicitZeroIsPreserved()
    {
        byte[] bytes=Encoding.Unicode.GetBytes("<skill_base><id>910010</id><name>HealNumericWitness</name>"
            +"<effect1_type>Heal_Instant</effect1_type><effect1_reserved1>0</effect1_reserved1>"
            +"<effect1_reserved2>0</effect1_reserved2><effect1_reserved6>0</effect1_reserved6></skill_base>");
        var part=Assert.Single(RetailNpcStatHealCombat.Parse(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Parts);
        Assert.True(part.Supported);Assert.Equal((0,0,false),(part.Delta,part.Value,part.Percent));
    }

    [SkippableFact]
    public void SourceCensusSeparatesNinetySevenNoBoostProgramsFromEightBoostContexts()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires selected native skill sources");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));int supported=0,boosted=0;
        foreach(var asset in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            var source=RetailNpcStatHealCombat.Parse(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            foreach(var part in source.Parts)
                if(part.Supported)supported++;
                else{Assert.Equal("unreviewed-heal-boost",part.UnsupportedReason);boosted++;}
        }
        Assert.Equal((97,8),(supported,boosted));
    }

    [SkippableFact]
    public void RealSourceHealsCacheNativeFloatNumbersAndClampOnlyAtTheExistingHpOperation()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HEAL_UNIT_CASES");
        Skip.If(string.IsNullOrEmpty(path),"Requires independent heal numeric fixtures");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(path!));var root=doc.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),root.GetProperty("sha256").GetString()!,Path.GetDirectoryName(manifest)!);
        foreach(var c in root.GetProperty("cases").EnumerateArray())
        {
            int map=c.GetProperty("map").GetInt32();
            using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var npc=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");
            int maximum=c.GetProperty("fixtureMaxHp").GetInt32(),reserved=c.GetProperty("expectedReserved").GetInt32();
            int requested=c.GetProperty("expectedRequested").GetInt32(),level=c.GetProperty("level").GetInt32();
            npc.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.MAXHP,maximum),
                new StatSetFunction(StatEnum.REGEN_HP,0),new StatSetFunction(StatEnum.HEAL_BOOST,5000),
                new StatSetFunction(StatEnum.HEAL_SKILL_BOOST,5000)]);
            Assert.Equal(maximum,npc.GetLifeStats().GetMaxHp());
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(c.GetProperty("skill").GetInt32());
            var part=Assert.IsType<HealInstantEffect>(skill.GetEffectTemplate(c.GetProperty("position").GetInt32()));
            npc.GetLifeStats().SetCurrentHp(maximum);
            var effect=new Effect(npc,npc,skill,level);part.Calculate(effect);
            Assert.Equal(reserved,RetailNpcStatHealCombat.GetSelectedValue(effect,part.Position));
            Assert.Equal(0,effect.GetReserveds(part.Position).GetValue()); // bounded 4.8 packet preview
            // Damage between Check and Begin does not erase a full-HP Check.
            npc.GetLifeStats().SetCurrentHp(1);part.ApplyEffect(effect);
            Assert.Equal(1+requested,npc.GetLifeStats().GetCurrentHp());
            npc.GetLifeStats().SetCurrentHp(maximum-3);
            var capped=new Effect(npc,npc,skill,level);part.Calculate(capped);
            Assert.Equal(reserved,RetailNpcStatHealCombat.GetSelectedValue(capped,part.Position));
            Assert.Equal(3,capped.GetReserveds(part.Position).GetValue());
            part.ApplyEffect(capped);Assert.Equal(maximum,npc.GetLifeStats().GetCurrentHp());
            // A disease acquired after Check is still enforced by the HP owner.
            var diseased=new Effect(npc,npc,skill,level);part.Calculate(diseased);
            npc.GetLifeStats().SetCurrentHp(1);npc.GetEffectController().SetAbnormal(AbnormalState.DISEASE);
            part.ApplyEffect(diseased);Assert.Equal(1,npc.GetLifeStats().GetCurrentHp());
            npc.GetEffectController().UnsetAbnormal(AbnormalState.DISEASE);
            // Unreviewed late multiplier context restores the exact old host
            // reservation, with a compatibility notice; it never applies a
            // selected cached amount under an unreviewed writer.
            npc.GetLifeStats().SetCurrentHp(maximum);
            var changed=new Effect(npc,npc,skill,level);part.Calculate(changed);
            npc.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(StatEnum.HEAL_SKILL_DEBOOST,100)]);
            Assert.NotEmpty(npc.GetGameStats().GetStatsSorted(StatEnum.HEAL_SKILL_DEBOOST));
            npc.GetLifeStats().SetCurrentHp(1);part.ApplyEffect(changed);
            Assert.Equal(1,npc.GetLifeStats().GetCurrentHp());
            Assert.Equal(0,changed.GetReserveds(part.Position).GetValue());
            Assert.Null(RetailNpcStatHealCombat.GetSelectedValue(changed,part.Position));
            Assert.Empty(h.Clock.Faults);
        }
    }
}
