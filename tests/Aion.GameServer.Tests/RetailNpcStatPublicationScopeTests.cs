using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatPublicationScopeTests
{
    private sealed class Owner : IStatOwner;
    [SkippableFact]
    public async Task RuntimePublishesOrdinaryConsumersWithoutActivatingOptionalPlayerOrSkillExperiments()
    {
        var (bytes,_,_)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true,includeBuffs:true);
        using var restore=RetailNpcStatCatalog.PreservePublicationForTests();
        using var h=BossAiHarness.For(220010000).Build();
        var root=JsonNode.Parse(bytes)!.AsObject();
        // This optional, uncommitted reader must not participate in ordinary
        // runtime publication even when a combined discovery manifest carries it.
        root["player_shield_context"]="offline-only-candidate";
        string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        async Task Publish(JsonObject manifest)
        {
            byte[] content=Encoding.UTF8.GetBytes(manifest.ToJsonString());
            await File.WriteAllBytesAsync(path,content);
            await RetailNpcStatCatalog.InitializeAsync(path,Convert.ToHexStringLower(SHA256.HashData(content)));
        }
        try
        {
            var missingRequired=root.DeepClone().AsObject();missingRequired.Remove("combat_stats");
            await Assert.ThrowsAsync<InvalidDataException>(()=>Publish(missingRequired));
            Assert.False(RetailNpcStatCatalog.Enabled);
            await Publish(root);
            Assert.True(RetailNpcStatCatalog.Enabled);
            Assert.Equal(11517,RetailNpcStatCatalog.Members.Count);
            Assert.NotNull(RetailNpcStatCatalog.CombatPve);
            Assert.Equal(524,RetailNpcStatCatalog.BuffSources.Sum(s=>s.Programs.Length));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Publish(root));
        }
        finally {File.Delete(path);}
    }

    [SkippableFact]
    public void ConditionalGlobalEntriesUseSelectedUnownedStatsWithoutInventingPlacements()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        const int map=220010000;
        using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var target=RetailNpcNormalRuntimeTests.MortalPlayer(h,map);
        int count=0,normal=0,absent=0;
        var coverage=new List<object>();
        foreach(var member in RetailNpcStatCatalog.Members.Where(m=>m.MapId==0))
        {
            var p=member.Profile;
            Assert.DoesNotContain(RetailNpcStatCatalog.Members,m=>m.MapId==map && m.Profile.NpcId==p.NpcId);
            var npc=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId,suppressCritical:false);
            Assert.True(RetailNpcStatCatalog.TryGet(npc,out var selected));Assert.Same(p,selected);
            var stats=npc.GetGameStats();Assert.Same(p,stats.StatisticsProfile);
            Assert.Equal(p.MaxHp,npc.GetLifeStats().GetMaxHp());Assert.Equal(p.MaxHp,npc.GetLifeStats().GetCurrentHp());
            Assert.Equal(p.PhysicalDefense,stats.GetPDef().GetCurrent());Assert.Equal(p.MagicalDefense,stats.GetMDef().GetCurrent());
            Assert.Equal(p.PhysicalAccuracy,stats.GetMainHandPAccuracy().GetCurrent());
            Assert.Equal(p.MagicalAccuracy,stats.GetMAccuracy().GetCurrent());
            Assert.Equal(p.PhysicalCritical,stats.GetMainHandPCritical().GetCurrent());
            Assert.Equal(p.MagicalCritical,stats.GetMCritical().GetCurrent());
            Assert.Equal(p.MagicalResist,stats.GetMResist().GetCurrent());
            Assert.Equal(p.Dodge,stats.GetEvasion().GetCurrent());Assert.Equal(p.Parry,stats.GetParry().GetCurrent());
            Assert.Equal(p.Block,stats.GetBlock().GetCurrent());
            foreach(var stat in new[]{StatEnum.WATER_RESISTANCE,StatEnum.WIND_RESISTANCE,StatEnum.EARTH_RESISTANCE,
                StatEnum.FIRE_RESISTANCE,StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE})
                Assert.Equal(p.Values[RetailNpcStatDefenses.Field(stat)!],stats.GetResistance(stat).GetCurrent());
            Assert.Equal(p.Values["str"],stats.GetPower().GetCurrent());Assert.Equal(p.Values["agi"],stats.GetAgility().GetCurrent());
            Assert.Equal(p.Values["dex"],stats.GetAccuracy().GetCurrent());Assert.Equal(p.Values["kno"],stats.GetKnowledge().GetCurrent());
            Assert.Equal(p.Values["will"],stats.GetWill().GetCurrent());
            // Range checks then use a separately declared noncritical state.
            stats.AddEffectOnly(new Owner(),[new StatRateFunction(StatEnum.PHYSICAL_CRITICAL,-100,true),
                new StatRateFunction(StatEnum.MAGICAL_CRITICAL,-100,true)]);
            Assert.True(SkyTempleNpcCombat.TryCalculateNormal(npc,target,out var attacks,out var element));
            if(p.Attacks.IsEmpty)
            {Assert.Equal(0,Assert.Single(attacks).GetDamage());absent++;}
            else
            {
                Assert.True(p.HasNormalAttackCorrection);Assert.False(SkyTempleNpcCombat.RequiresEquipmentResponse(p));
                bool dual=p.Source.Integers["dual"].ParsedValue is not (null or 0);
                Assert.Equal(dual?2:1,attacks.Count);
                int minimum=Math.Min(p.MinDamage,p.MaxDamage)/(dual?2:1),maximum=Math.Max(p.MinDamage,p.MaxDamage)/(dual?2:1);
                bool physical=element==SkillElement.NONE;
                var entries=p.Attacks.Where(a=>SkyTempleCombatInputs.Element(a.Type)==element).ToArray();Assert.NotEmpty(entries);
                int value=unchecked(RetailNpcStatCatalog.CombatPve!.PlayerBase(map,Race.ASMODIANS,physical,false)
                    +RetailNpcStatCatalog.CombatPve.NpcCounterpartyDelta(p,Race.ASMODIANS,physical,true));
                float pve=Math.Clamp(1f-Math.Clamp(value,-10000,10000)/1000f,.1f,12f);
                int lower=entries.Min(a=>(int)Math.Max(1f,Math.Max(1f,minimum*(a.Modifier/100f))*pve));
                int upper=entries.Max(a=>(int)Math.Max(1f,Math.Max(1f,maximum*(a.Modifier/100f))*pve));
                Assert.All(attacks,a=>Assert.InRange(a.GetDamage(),lower,upper));normal++;
            }
            coverage.Add(new{membership_map_id=0,fixture_map_id=map,npc_id=p.NpcId,source=p.SourceRecordSha256,
                gate=p.Attacks.IsEmpty?"verified-global-unowned-no-normal":"verified-global-unowned-normal"});
            count++;
        }
        Assert.Equal(309,count);Assert.Equal(40,normal);Assert.Equal(269,absent);Assert.Empty(h.Clock.Faults);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_GLOBAL_COVERAGE") is {Length:>0} output)
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{offline_only=true,count,normal,absent,
                scope="unowned global lookup/projection in one supported fixture world; no spawn placement, ownership/scaling or active script claim",rows=coverage}));
    }
}
