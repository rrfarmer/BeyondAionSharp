using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Handlers.AI;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerNormalTests
{
    private sealed class Owner : IStatOwner;
    private sealed class Outcome(AttackStatus forced,bool critical=false,bool subtractOne=false) : AttackCalcObserver
    {
        internal int Protections,CriticalQueries,Received;
        public override bool CheckStatus(AttackStatus status)=>status==forced;
        public override AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus status,bool skill)
        {
            CriticalQueries++;
            var result=new AttackerCriticalStatus(1,1000,false);result.SetResult(critical);return result;
        }
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature source)
        {
            Protections++;Received+=attacks.Sum(a=>a.GetDamage());
            if(subtractOne)foreach(var attack in attacks)attack.SetDamage(Math.Max(0,attack.GetDamage()-1));
        }
    }

    [AIName("retail_player_normal_one_damage")]
    public sealed class OneDamage(Npc owner) : OneDmgAI(owner)
    {
        internal int Incoming;
        public override float ModifyDamage(Creature source,float damage,Effect effect)
        {Incoming++;return base.ModifyDamage(source,damage,effect);}
    }

    [SkippableFact]
    public void ProductionEntryRetainsNakedPlayerSourceAndAppliesNpcArmorDefenseCriticalAndProtection()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        var member=RetailNpcStatCatalog.Members.First(m=>m.MapId!=0 && m.Profile.Level==65 && m.Profile.StatRatio>1000
            && m.Profile.PhysicalDamageTrim==0);
        using var h=BossAiHarness.For(member.MapId).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
        var p=member.Profile;
        foreach(var (status,critical) in new[]{(AttackStatus.NORMALHIT,false),(AttackStatus.BLOCK,false),
            (AttackStatus.PARRY,false),(AttackStatus.PARRY,true),(AttackStatus.DODGE,false)})
        {
            var source=Source(h,member.MapId);
            var target=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,p.NpcId);
            Set(target,StatEnum.PHYSICAL_DEFENSE,40);Set(target,StatEnum.EVASION,0);Set(target,StatEnum.BLOCK,0);Set(target,StatEnum.PARRY,0);
            Set(target,StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,200);
            var sourceObserver=new Outcome(AttackStatus.NORMALHIT,critical);
            var targetObserver=new Outcome(status,subtractOne:true);
            source.GetObserveController().AddAttackCalcObserver(sourceObserver);
            target.GetObserveController().AddAttackCalcObserver(targetObserver);
            Assert.True(RetailNpcPlayerNormal.TryMatch(source,target,out _));
            float bonus=source.GetGameStats().GetMainHandPAttack(new HashSet<CalculationType>()).GetBonus();
            var result=Assert.Single(AttackUtil.CalculatePhysAttackResult(source,target,new HashSet<CalculationType>()));
            Assert.Equal(critical?AttackStatusExtensions.GetCriticalStatusFor(status):status,result.GetAttackStatus());
            Assert.Equal(status==AttackStatus.DODGE?0:1,sourceObserver.CriticalQueries);
            Assert.Equal(1,targetObserver.Protections);
            float Expected(int sample)
            {
                if(status==AttackStatus.DODGE)return 0;
                float damage=(sample+bonus-4f)/(p.StatRatio/1000f);
                if(status is AttackStatus.BLOCK or AttackStatus.PARRY)damage*=.9f;
                if(critical)damage*=1.3f;
                damage*=Pve(p,member.MapId,true);
                return Math.Max(0,(int)Math.Max(1f,damage)-1);
            }
            Assert.InRange(result.GetDamage(),(int)Expected(16),(int)Expected(20));
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void CompleteSelectedCohortConsumesActualNpcDefenseAndElementGettersFromExistingPlayerInputs()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        int checkedCount=0,calculations=0,fixedTrimMemberships=0;
        var coverage=new List<object>();
        foreach(var map in RetailNpcStatCatalog.Members.Where(m=>m.MapId!=0).GroupBy(m=>m.MapId))
        {
            using var h=BossAiHarness.For(map.Key).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI)).Build();
            var source=Source(h,map.Key);
            foreach(var member in map)
            {
                var p=member.Profile;
                var target=RetailNpcNormalRuntimeTests.FreshNpc(h,map.Key,p.NpcId);
                Assert.True(RetailNpcPlayerNormal.TryMatch(source,target,out var selected));Assert.Same(p,selected);
                foreach(var element in new[]{SkillElement.NONE,SkillElement.FIRE,SkillElement.WATER,SkillElement.WIND,
                    SkillElement.EARTH,SkillElement.LIGHT,SkillElement.DARK})
                {
                    bool physical=element==SkillElement.NONE;
                    var results=new List<AttackResult>{new(10000.5f,AttackStatus.NORMALHIT)};
                    RetailNpcPlayerNormal.ApplyDefenseStages(source,target,p,AttackStatus.NORMALHIT,element,results);
                    // Independent native-stage oracle, retaining the fixed fractional
                    // source input. Loading, current getters and production consumers
                    // are exercised; no weapon tables or NPC attacks are substituted.
                    float expected=10000.5f;
                    int excess=Math.Max(0,p.Level-65-2);
                    expected*=Math.Max(0f,1f-excess*.1f);
                    int defense=physical?target.GetGameStats().GetPDef().GetCurrent():target.GetGameStats().GetMDef().GetCurrent();
                    expected=Math.Max(0f,expected-defense/10f);
                    if(physical)expected/=p.StatRatio/1000f;
                    else expected*=1f-Math.Max(-1300,target.GetGameStats().GetResistance(element.GetStatForElement()!.Value).GetCurrent())/1300f;
                    expected*=Pve(p,map.Key,physical);expected=Math.Max(1f,expected);
                    Assert.True(BitConverter.SingleToInt32Bits(expected)==BitConverter.SingleToInt32Bits(results[0].GetExactDamage()),
                        $"map={map.Key} npc={p.NpcId} element={element} actual={results[0].GetExactDamage()} expected={expected}");
                    int recordTrim=physical?p.PhysicalDamageTrim:p.MagicalDamageTrim;
                    Assert.Equal(recordTrim!=0?(float)recordTrim:expected,SkyTempleNpcCombat.TargetTrim(p,physical,results[0].GetExactDamage()));
                    Assert.Equal(0f,SkyTempleNpcCombat.TargetTrim(p,physical,0));
                    Assert.Equal(recordTrim!=0?(float)recordTrim:.5f,SkyTempleNpcCombat.TargetTrim(p,physical,.5f));
                    calculations++;
                }
                bool trim=p.PhysicalDamageTrim!=0 || p.MagicalDamageTrim!=0;
                if(trim)fixedTrimMemberships++;
                coverage.Add(new{map_id=map.Key,npc_id=p.NpcId,source=p.SourceRecordSha256,
                    numeric_gate="verified-player-npc-defense-consumer",late_trim=trim?"verified-fixed-target-trim":"none"});
                checkedCount++;
            }
            Assert.Empty(h.Clock.Faults);
        }
        Assert.Equal(11208,checkedCount);Assert.Equal(78456,calculations);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_PLAYER_RECEIVER_COVERAGE") is {Length:>0} output)
            File.WriteAllText(output,System.Text.Json.JsonSerializer.Serialize(new{offline_only=true,checkedCount,calculations,fixedTrimMemberships,
                scope="neutral-AI numeric receiver stages; existing player source inputs; active scripts/late trims separately bounded",rows=coverage}));
    }

    [SkippableFact]
    public void PlayerCriticalHandsUseReceiverReductionAndOneDamageDoesNotReviveMisses()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        const int map=220010000;
        using var h=BossAiHarness.For(map).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI),typeof(OneDamage),typeof(RetailNpcNormalReceiverTests.UnknownScript)).Build();
        var p=RetailNpcStatCatalog.Members.First(m=>m.MapId==map && m.Profile.PhysicalDamageTrim==0).Profile;
        var source=Source(h,map);
        var target=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId);
        Set(target,StatEnum.PHYSICAL_DEFENSE,40);Set(target,StatEnum.PHYSICAL_CRITICAL_DAMAGE_REDUCE,200);
        var hands=new List<AttackResult>{new(1000.5f,AttackStatus.CRITICAL_PARRY),new(700.5f,AttackStatus.OFFHAND_CRITICAL_PARRY)};
        RetailNpcPlayerNormal.ApplyDefenseStages(source,target,p,AttackStatus.CRITICAL_PARRY,SkillElement.NONE,hands);
        float expectedMain=(1000.5f-4f)/(p.StatRatio/1000f);expectedMain*=.9f;expectedMain*=1.3f;expectedMain*=Pve(p,map,true);
        float expectedOff=(700.5f-4f)/(p.StatRatio/1000f);expectedOff*=.9f;expectedOff*=1.7f;expectedOff*=Pve(p,map,true);
        Assert.Equal(expectedMain,hands[0].GetExactDamage());Assert.Equal(expectedOff,hands[1].GetExactDamage());

        var one=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId,"retail_player_normal_one_damage");
        Set(one,StatEnum.BLOCK,0);Set(one,StatEnum.PARRY,0);
        var forcedDodge=new Outcome(AttackStatus.DODGE);
        one.GetObserveController().AddAttackCalcObserver(forcedDodge);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,one,out var missed,out _));
        Assert.Equal(0,Assert.Single(missed).GetDamage());Assert.Equal(0,((OneDamage)one.GetAi()).Incoming);
        one.GetObserveController().RemoveAttackCalcObserver(forcedDodge);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,one,out var hit,out _));
        Assert.Equal(1,Assert.Single(hit).GetDamage());Assert.Equal(1,((OneDamage)one.GetAi()).Incoming);
        var unknown=RetailNpcNormalRuntimeTests.FreshNpc(h,map,p.NpcId,"retail_receiver_unknown_script");
        Assert.False(SkyTempleNpcCombat.TryCalculateNormal(source,unknown,out _,out _));
        Assert.False(RetailNpcPlayerNormal.TryMatch(source,Source(h,map),out _));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void NativeFixedTargetTrimRunsAfterNumericStagesBeforeProtectionAndNeverRevivesNpcMisses()
    {
        var (bytes,pin,directory)=RetailNpcRuntimeGetterTests.ProfileFixture(includeCombat:true);
        using var installed=RetailNpcStatCatalog.InstallForTests(bytes,pin,directory);
        var member=RetailNpcStatCatalog.Members.First(m=>m.MapId!=0 && m.Profile.PhysicalDamageTrim==1 && m.Profile.Level<=65);
        using var h=BossAiHarness.For(member.MapId).WithAi(typeof(RetailNpcRuntimeGetterTests.BaseAI),typeof(OneDmgAI)).Build();
        var p=member.Profile;
        var target=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,p.NpcId);
        foreach(var stat in new[]{StatEnum.PHYSICAL_DEFENSE,StatEnum.EVASION,StatEnum.BLOCK,StatEnum.PARRY})Set(target,stat,0);
        var protection=new Outcome(AttackStatus.NORMALHIT,subtractOne:true);
        target.GetObserveController().AddAttackCalcObserver(protection);
        var source=Source(h,member.MapId);Set(source,StatEnum.PVE_ATTACK_RATIO,10000);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(source,target,out var result,out _));
        Assert.Equal(0,Assert.Single(result).GetDamage());Assert.Equal(1,protection.Received);Assert.Equal(1,protection.Protections);
        target.GetObserveController().RemoveAttackCalcObserver(protection);

        var sourceProfile=RetailNpcStatCatalog.Members.First(m=>m.MapId==member.MapId && m.Profile.Level>=p.Level-2
            && m.Profile.HasNormalAttackCorrection && !m.Profile.Attacks.IsEmpty
            && !SkyTempleNpcCombat.RequiresEquipmentResponse(m.Profile) && m.Profile.Attacks.All(a=>a.Type=="physical")).Profile;
        var npc=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,sourceProfile.NpcId);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(npc,target,out var npcResults,out _));
        Assert.All(npcResults,r=>Assert.Equal(1,r.GetDamage()));
        var one=RetailNpcNormalRuntimeTests.FreshNpc(h,member.MapId,sourceProfile.NpcId,"onedmg_aggressive");
        var dodge=new Outcome(AttackStatus.DODGE);
        target.GetObserveController().AddAttackCalcObserver(dodge);
        Assert.True(SkyTempleNpcCombat.TryCalculateNormal(one,target,out var missed,out _));
        Assert.Equal(0,Assert.Single(missed).GetDamage());
        Assert.Empty(h.Clock.Faults);
    }

    private static Player Source(BossAiHarness h,int map)
    {
        var player=RetailNpcNormalRuntimeTests.MortalPlayer(h,map);player.GetCommonData().SetLevel(65);
        Set(player,StatEnum.POWER,100);Set(player,StatEnum.PHYSICAL_CRITICAL,0);Set(player,StatEnum.MAGICAL_CRITICAL,0);
        Set(player,StatEnum.PHYSICAL_ACCURACY,10000);Set(player,StatEnum.MAGICAL_ACCURACY,10000);
        return player;
    }
    private static float Pve(SkyTempleNpcProfile p,int map,bool physical)
    {
        int value=unchecked(RetailNpcStatCatalog.CombatPve!.PlayerBase(map,Race.ASMODIANS,physical,true)
            +RetailNpcStatCatalog.CombatPve.NpcCounterpartyDelta(p,Race.ASMODIANS,physical,false));
        return Math.Clamp(1f+Math.Clamp(value,-10000,10000)/1000f,.1f,12f);
    }
    private static void Set(Creature actor,StatEnum stat,int value)=>actor.GetGameStats().AddEffectOnly(new Owner(),[new StatSetFunction(stat,value)]);
}
