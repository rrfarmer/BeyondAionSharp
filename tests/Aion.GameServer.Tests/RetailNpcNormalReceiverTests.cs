using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcNormalReceiverTests
{
    private sealed class Owner : IStatOwner;
    private sealed class Outcome(AttackStatus status,bool critical) : AttackCalcObserver
    {
        public int ProtectionCalls, CriticalCalls;
        public override bool CheckStatus(AttackStatus candidate) => candidate==status;
        public override AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus candidate,bool skill)
        {
            CriticalCalls++;
            var result=new AttackerCriticalStatus(1,1000,false);
            result.SetResult(critical);
            return result;
        }
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker) => ProtectionCalls++;
    }

    [AIName("retail_receiver_unknown_script")]
    public sealed class UnknownScript(Npc owner) : NpcAI(owner)
    {
        public override float ModifyDamage(Creature attacker,float damage,Effect effect) => damage*7;
    }

    [SkippableTheory]
    [InlineData(false,false,false,AttackStatus.NORMALHIT,0,0)]
    [InlineData(false,false,false,AttackStatus.BLOCK,0,0)]
    [InlineData(false,false,false,AttackStatus.PARRY,0,0)]
    [InlineData(false,false,true,AttackStatus.BLOCK,0,200)]
    [InlineData(false,true,true,AttackStatus.PARRY,0,200)]
    [InlineData(true,false,false,AttackStatus.NORMALHIT,-2000,0)]
    [InlineData(true,false,false,AttackStatus.BLOCK,650,0)]
    [InlineData(true,false,true,AttackStatus.PARRY,-650,200)]
    public void MatchedNpcReceiverUsesNativeRatioElementsActiveDefenseAndSharedHands(
        bool elemental,bool dual,bool critical,AttackStatus forced,int resistance,int reduction)
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        var member=RetailNpcStatCatalog.Members.First(m=>m.MapId!=0 && m.Profile.MinDamage>100
            && m.Profile.HasNormalAttackCorrection && !SkyTempleNpcCombat.RequiresEquipmentResponse(m.Profile)
            && !m.Profile.Attacks.IsEmpty
            && m.Profile.Attacks.All(a=>(SkyTempleCombatInputs.Element(a.Type)!=SkillElement.NONE)==elemental)
            && (m.Profile.Source.Integers["dual"].ParsedValue is not (null or 0))==dual);
        using var h=BossAiHarness.For(member.MapId).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var p=member.Profile;
        var source=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,p.NpcId);
        var target=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,p.NpcId);
        Set(target,StatEnum.PHYSICAL_DEFENSE,40); Set(target,StatEnum.MAGICAL_DEFEND,40);
        Set(target,StatEnum.EVASION,0); Set(target,StatEnum.BLOCK,0); Set(target,StatEnum.PARRY,0);
        foreach(var elementStat in new[]{StatEnum.FIRE_RESISTANCE,StatEnum.WATER_RESISTANCE,StatEnum.WIND_RESISTANCE,
            StatEnum.EARTH_RESISTANCE,StatEnum.LIGHT_RESISTANCE,StatEnum.DARK_RESISTANCE}) Set(target,elementStat,resistance);
        Set(target,StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,reduction);
        Set(target,StatEnum.MAGICAL_CRITICAL_DAMAGE_REDUCE,reduction);
        Set(source,StatEnum.PVE_ATTACK_RATIO,9000); Set(target,StatEnum.PVE_DEFEND_RATIO,-9000);
        var sourceObserver=new Outcome(AttackStatus.NORMALHIT,critical);
        var targetObserver=new Outcome(forced,false);
        source.GetObserveController().AddAttackCalcObserver(sourceObserver);
        target.GetObserveController().AddAttackCalcObserver(targetObserver);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,target,out var results,out var element));
        Assert.Equal(dual?2:1,results.Count);
        Assert.Equal(results.Count,targetObserver.ProtectionCalls);
        Assert.Equal(1,sourceObserver.CriticalCalls);
        Assert.Equal(critical?AttackStatusExtensions.GetCriticalStatusFor(forced):forced,results[0].GetAttackStatus());
        if(dual) Assert.Equal(AttackStatusExtensions.GetOffHandStats(results[0].GetAttackStatus()),results[1].GetAttackStatus());
        // Independent endpoint oracle: no production damage/status/PvE helpers.
        var attacks=p.Attacks.Where(a=>SkyTempleCombatInputs.Element(a.Type)==element).ToArray();
        float factor=forced is AttackStatus.BLOCK or AttackStatus.PARRY ? .9f : 1f;
        float coefficient=critical?Math.Max(1f,(elemental?1f:1.5f)-(int)(reduction/(p.StatRatio/1000f))/1000f):1f;
        float Damage(int sample,int modifier)
        {
            float value=Math.Max(1f,sample*(modifier/100f));
            value=Math.Max(0f,value-4f);
            if(elemental) value*=1f-Math.Max(-1300,(int)(resistance/(p.StatRatio/1000f)))/1300f;
            else value/=p.StatRatio/1000f;
            value*=factor; value*=coefficient;
            return Math.Max(1f,value);
        }
        int min=Math.Min(p.MinDamage,p.MaxDamage)/(dual?2:1),max=Math.Max(p.MinDamage,p.MaxDamage)/(dual?2:1);
        foreach(var result in results)
            Assert.InRange(result.GetDamage(),attacks.Min(a=>(int)Damage(min,a.Modifier)),attacks.Max(a=>(int)Damage(max,a.Modifier)));
        Assert.Empty(h.Clock.Faults);
    }

    [Theory]
    [InlineData(true,AttackStatus.CRITICAL_BLOCK,189f)]
    [InlineData(true,AttackStatus.CRITICAL_PARRY,189f)]
    [InlineData(false,AttackStatus.CRITICAL_BLOCK,126f)]
    [InlineData(false,AttackStatus.CRITICAL_PARRY,126f)]
    public void NpcSharedOffhandAppliesTenPercentDefenseBeforeCritical(bool physical,AttackStatus status,float expected)
    {
        var shared=RetailNpcDualNormalPlan.Share(status);
        Assert.Equal(expected,RetailNpcDualNormalPlan.ApplySharedOffhand(140f,shared,physical,1000,0,10,1,npcDefender:true));
    }

    [SkippableFact]
    public void CompleteSelectedCohortUsesNpcReceiverCalculationWithVisibleSourceExceptions()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        int checkedNormals=0,emptyNormals=0,bounded=0;
        var coverage=new List<object>();
        foreach(var map in RetailNpcStatCatalog.Members.Where(m=>m.MapId!=0).GroupBy(m=>m.MapId))
        {
            using var h=BossAiHarness.For(map.Key).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
            foreach(var member in map)
            {
                var p=member.Profile;
                if(!p.Attacks.IsEmpty && (!p.HasNormalAttackCorrection || SkyTempleNpcCombat.RequiresEquipmentResponse(p)))
                {
                    bounded++;
                    coverage.Add(new{map_id=map.Key,npc_id=p.NpcId,status="bounded-source-normal",source=p.SourceRecordSha256});
                    continue;
                }
                var source=RetailNpcNormalRuntimeTests.FreshNpc(h,map.Key,p.NpcId);
                var target=RetailNpcNormalRuntimeTests.FreshNpc(h,map.Key,p.NpcId);
                var capture=new Outcome(AttackStatus.NORMALHIT,false);
                target.GetObserveController().AddAttackCalcObserver(capture);
                Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,target,out var results,out var element),$"map={map.Key} npc={p.NpcId}");
                if(p.Attacks.IsEmpty)
                {
                    emptyNormals++;
                    Assert.Equal(0,Assert.Single(results).GetDamage());
                }
                else
                {
                    checkedNormals++;
                    bool dual=p.Source.Integers["dual"].ParsedValue is not (null or 0);
                    Assert.Equal(results[0].GetAttackStatus()==AttackStatus.DODGE?1:dual?2:1,results.Count);
                    Assert.Equal(results.Count,capture.ProtectionCalls);
                    var attacks=p.Attacks.Where(a=>SkyTempleCombatInputs.Element(a.Type)==element).ToArray();
                    Assert.NotEmpty(attacks);
                    int min=Math.Min(p.MinDamage,p.MaxDamage)/(dual?2:1),max=Math.Max(p.MinDamage,p.MaxDamage)/(dual?2:1);
                    float Bound(int sample,int modifier,AttackStatus status)
                    {
                        if(status is AttackStatus.DODGE or AttackStatus.OFFHAND_DODGE) return 0;
                        bool physical=element==SkillElement.NONE;
                        float damage=Math.Max(1f,sample*(modifier/100f));
                        int defense=physical?target.GetGameStats().GetPDef().GetCurrent():target.GetGameStats().GetMDef().GetCurrent();
                        damage=Math.Max(0f,damage-defense/10f);
                        if(physical) damage/=p.StatRatio/1000f;
                        else
                        {
                            int resist=target.GetGameStats().GetResistance(element.GetStatForElement()!.Value).GetCurrent();
                            damage*=1f-Math.Max(-1300,(int)(resist/(p.StatRatio/1000f)))/1300f;
                        }
                        if(AttackStatusExtensions.GetBaseStatus(status) is AttackStatus.PARRY or AttackStatus.BLOCK) damage*=.9f;
                        return Math.Max(1f,damage);
                    }
                    foreach(var result in results)
                    {
                        var status=result.GetAttackStatus();
                        Assert.False(status.IsCritical());
                        int lower=attacks.Min(a=>(int)Bound(min,a.Modifier,status));
                        int upper=attacks.Max(a=>(int)Bound(max,a.Modifier,status));
                        Assert.True(result.GetDamage()>=lower && result.GetDamage()<=upper,
                            $"map={map.Key} npc={p.NpcId} element={element} status={status} damage={result.GetDamage()} expected={lower}..{upper}");
                    }
                }
                coverage.Add(new{map_id=map.Key,npc_id=p.NpcId,status=p.Attacks.IsEmpty?"verified-no-normal":"verified-controlled-npc-receiver",source=p.SourceRecordSha256});
            }
            Assert.Empty(h.Clock.Faults);
        }
        Assert.Equal(9420,checkedNormals); Assert.Equal(1723,emptyNormals); Assert.Equal(65,bounded);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_NPC_RECEIVER_COVERAGE") is {Length:>0} output)
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{offline_only=true,scope="ordinary ownerless NPC pairs with neutral AI; no active script/stance overrides",rows=coverage}));
    }

    [SkippableTheory]
    [InlineData("onedmg_aggressive",true)]
    [InlineData("retail_receiver_unknown_script",false)]
    public void ReceiverScriptsKeepOneDamageOrExplicitCompatibility(string ai,bool admitted)
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        const int map=220010000;
        using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI),typeof(OneDmgAI),typeof(UnknownScript)).Build();
        var p=RetailNpcStatCatalog.Members.First(m=>m.MapId==map && m.Profile.HasNormalAttackCorrection && !m.Profile.Attacks.IsEmpty).Profile;
        var source=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId);
        var target=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId,ai);
        Set(target,StatEnum.EVASION,0); Set(target,StatEnum.BLOCK,0); Set(target,StatEnum.PARRY,0);
        Assert.Equal(admitted,SkyTempleNpcCombat.TryCalculateNormal(source,target,out var results,out _));
        if(admitted) Assert.All(results,r=>Assert.Equal(1,r.GetDamage()));
    }

    [SkippableFact]
    public void SourceNpcOnlyScriptCompatibilityDoesNotChangePlayerTargetCalculation()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        const int map=220010000;
        using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI),typeof(FortressProtectorNpcAI)).Build();
        var p=RetailNpcStatCatalog.Members.First(m=>m.MapId==map && m.Profile.HasNormalAttackCorrection && !m.Profile.Attacks.IsEmpty).Profile;
        var source=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId,"fortress_protector");
        var target=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId);
        Assert.Equal(500f,source.GetAi().ModifyOwnerDamage(100f,target,null!));
        Assert.False(SkyTempleNpcCombat.TryCalculateNormal(source,target,out _,out _));
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,RetailNpcNormalRuntimeTests.MortalPlayer(h,map),out _,out _));
        var passiveProfile=RetailNpcStatCatalog.Members.First(m=>m.MapId==map && m.Profile.Attacks.IsEmpty).Profile;
        var passive=RetailNpcNormalRuntimeTests.FreshNpc(h,map,passiveProfile.NpcId,"fortress_protector");
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(passive,target,out var noAttack,out _));
        Assert.Equal(0,Assert.Single(noAttack).GetDamage()); // Explicit absent normal must not fall through to generic damage.
        Assert.Empty(h.Clock.Faults);
    }

    private static void Set(Npc npc,StatEnum stat,int value) => npc.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(stat,value)]);
}
