using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.World.Knownlist;
using IDFactory = Aion.GameServer.Utils.IdFactory.IDFactory;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatSpellDrainTests
{
    [Fact]
    public void CurseResistanceCombinesOrdinaryModifiersAndSelectsWholeAbsoluteRecord()
    {
        usingInput(new(100,new(20,10),new(30,10),default,default),170);
        // Combined percentages are divided once: 5 + trunc(5 * 20 / 100) = 6.
        usingInput(new(5,new(10,0),new(10,0),default,default),6);
        usingInput(new(100,new(-100,-10),new(-100,-10),default,default),0);
        usingInput(new(9000,new(50,10),new(50,10),default,default),9999);
        usingInput(new(100,new(20,10),new(30,10),new(1,true,0),default),0); // Present zero overrides.
        usingInput(new(100,new(20,10),new(30,10),new(1,true,500),new(2,true,900)),900);
        usingInput(new(100,new(20,10),new(30,10),new(1,true,500),new(2,false,0)),170);
        static void usingInput(RetailNpcStatSpellDrain.CurseResistanceInput input,int expected)
            => Assert.Equal(expected,RetailNpcStatSpellDrain.CalculateCurseResistance(input));
    }

    private sealed class UnreviewedProtectionObserver : AttackCalcObserver
    {
        public int Calls;
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker) => Calls++;
    }

    private sealed class InstallProtectionDuringStatus(ObserveController owner, AttackCalcObserver protection) : AttackCalcObserver
    {
        public int Calls;
        public override bool CheckStatus(AttackStatus status)
        {
            if (Interlocked.Increment(ref Calls) == 1) owner.AddAttackCalcObserver(protection);
            return false;
        }
    }

    private sealed class InstallProtectionWhenAttacked(ObserveController owner,AttackCalcObserver protection,Action? beforeInstall=null)
        : ActionObserver(ObserverType.ATTACKED)
    {
        public int Calls;
        public override void Attacked(Creature attacker,int skillId)
        {
            Calls++;
            beforeInstall?.Invoke();
            owner.AddAttackCalcObserver(protection);
        }
    }

    [SkippableFact]
    public void VampireReplacementBuildsFreshFixturePlanAfterTheOldCurseEnds()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        byte[] bytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(bytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,
            File.ReadAllBytes(asset.GetProperty("path").GetString()!));
        using var active=RetailNpcStatCatalog.InstallForTests(bytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
            new StatSetFunction(StatEnum.MAXMP,1000),new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(80000);
        player.GetLifeStats().SetCurrentMp(600);npc.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var skill=new TimedSkill(template,npc,player,38);skill.SetWord(0);
        var initial=new Effect(skill,player);
        var initialPlan=new RetailNpcCurseResourceOwner.Plan(100000,100000,0,1000,1000,0,_=>true);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(initial,candidate,initialPlan,(_,hi)=>hi);
        initial.Initialize();Assert.True(initial.IsInSuccessEffects(2));
        int firstDamage=initial.GetReserveds(first.Position).GetValue();Assert.InRange(firstDamage,1,10000);
        initial.ApplyEffect();Assert.Equal(65000,player.GetLifeStats().GetMaxHp());
        int firstCurseHp=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            100000,65000,80000-firstDamage);
        Assert.Equal(firstCurseHp,player.GetLifeStats().GetCurrentHp());
        int calls=0;var replacement=new Effect(skill,player);
        RetailNpcCurseResourceOwner.Plan? FreshPlan(Player p,Effect e)
        {
            calls++;Assert.Same(player,p);Assert.Same(replacement,e);Assert.True(initial.RetailHasEnded);
            Assert.False(p.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
            Assert.Equal(100000,p.GetLifeStats().GetMaxHp());Assert.Equal(1000,p.GetLifeStats().GetMaxMp());
            var checks=RetailNpcCurseStatWitness.Create(p,e,x=>ReferenceEquals(x,player),-35000,-350);
            Assert.True(checks.Before(p));Assert.False(checks.After(p));
            return new(100000,100000,0,1000,1000,0,checks.Before,checks.After,checks.CommitStamp);
        }
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(replacement,candidate,FreshPlan,(_,hi)=>hi);
        replacement.Initialize();Assert.Equal(0,calls);Assert.True(replacement.IsInSuccessEffects(2));
        int secondDamage=replacement.GetReserveds(first.Position).GetValue();Assert.InRange(secondDamage,1,10000);
        replacement.ApplyEffect();Assert.Equal(1,calls);Assert.True(initial.RetailHasEnded);
        int hpAfterOld=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            65000,100000,firstCurseHp-secondDamage);
        int hpAfterNew=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,100000,65000,hpAfterOld);
        Assert.Equal(65000,player.GetLifeStats().GetMaxHp());Assert.Equal(hpAfterNew,player.GetLifeStats().GetCurrentHp());
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+firstDamage+secondDamage),npc.GetLifeStats().GetCurrentHp());
        // If a replacement cannot produce its fresh plan, the primary has
        // already applied. Retire the failed curse entry without hiding that
        // failure or leaving an active modifier; never replay the first hit.
        var rejected=new Effect(skill,player);int failedFactoryCalls=0;
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(rejected,candidate,
            (Player _,Effect _) => {failedFactoryCalls++;return null;},(_,hi)=>hi);
        rejected.Initialize();Assert.True(rejected.IsInSuccessEffects(2));
        int thirdDamage=rejected.GetReserveds(first.Position).GetValue();Assert.InRange(thirdDamage,1,10000);
        int npcBeforeRejectedHeal=npc.GetLifeStats().GetCurrentHp();
        var failure=Assert.Throws<Exception>(()=>rejected.ApplyEffect());
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Equal(1,failedFactoryCalls);Assert.True(replacement.RetailHasEnded);Assert.True(rejected.RetailHasEnded);
        Assert.Null(player.GetEffectController().FindBySkillId(16596));
        Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
            65000,100000,hpAfterNew-thirdDamage),player.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),npcBeforeRejectedHeal+thirdDamage),npc.GetLifeStats().GetCurrentHp());
        // A stat writer after the final snapshot invalidates its reservation;
        // the landed hit stays landed, while the curse resource update aborts.
        var stale=new Effect(skill,player);var lateOwner=new OtherStatOwner();
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(stale,candidate,(Player p,Effect e)=>
        {
            var checks=RetailNpcCurseStatWitness.Create(p,e,x=>ReferenceEquals(x,player),-35000,-350);
            return new(100000,100000,0,1000,1000,0,checks.Before,checks.After,current=>
            {
                var stamp=checks.CommitStamp(current);
                current.GetGameStats().AddEffectOnly(lateOwner,[new StatAddFunction(StatEnum.MAGICAL_RESIST,1,true)]);
                return stamp;
            });
        },(_,hi)=>hi);
        int hpBeforeStale=player.GetLifeStats().GetCurrentHp(),mpBeforeStale=player.GetLifeStats().GetCurrentMp();
        int npcBeforeStaleHeal=npc.GetLifeStats().GetCurrentHp();
        stale.Initialize();Assert.True(stale.IsInSuccessEffects(2));
        int staleDamage=stale.GetReserveds(first.Position).GetValue();
        var staleFailure=Assert.Throws<Exception>(()=>stale.ApplyEffect());
        Assert.IsType<InvalidOperationException>(staleFailure.InnerException);
        Assert.True(stale.RetailHasEnded);Assert.Equal(hpBeforeStale-staleDamage,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(mpBeforeStale,player.GetLifeStats().GetCurrentMp());Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
        Assert.Null(player.GetEffectController().FindBySkillId(16596));
        player.GetGameStats().EndEffect(lateOwner);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),npcBeforeStaleHeal+staleDamage),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(16596,214435,38,100)]
    [InlineData(18459,214583,41,130)]
    [InlineData(18460,214583,41,100)]
    public void SelectedVampirePrimaryFeedsPreparedCurseAndCasterHealOffline(int skillId,int npcId,int skillLevel,int healPercent)
    {
        string? manifestPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(manifestPath)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        byte[] manifestBytes=File.ReadAllBytes(manifestPath!);
        using var manifest=JsonDocument.Parse(manifestBytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x=>x.GetProperty("id").GetInt32()==skillId);
        byte[] sourceBytes=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId,sourceBytes);
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(manifestPath!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(npcId,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
            new StatSetFunction(StatEnum.MAXMP,1000),new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(80000);player.GetLifeStats().SetCurrentMp(600);
        npc.GetLifeStats().SetCurrentHp(100);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(skillId);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(template.GetEffectTemplate(2));
        var skill=new TimedSkill(template,npc,player,skillLevel);skill.SetWord(0);
        var effect=new Effect(skill,player);
        var plan=new RetailNpcCurseResourceOwner.Plan(100000,100000,0,1000,1000,0,_=>true);
        var draws=new List<(int,int)>();
        int Draw(int lo,int hi){draws.Add((lo,hi));return Math.Max(lo,hi);}
        var declined=new Effect(skill,player);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(declined,candidate with {Id=-1},plan,Draw);
        declined.Initialize();
        Assert.True(RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(declined,out bool declinedHandled,out bool declinedHit));
        Assert.False(declinedHandled);Assert.False(declinedHit);Assert.Empty(draws);Assert.Empty(declined.GetSuccessEffects());
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,plan,Draw);
        effect.Initialize();
        Assert.True(RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(effect,out bool handled,out bool hit));
        Assert.True(handled);Assert.True(hit);Assert.True(effect.IsInSuccessEffects(1));
        Assert.NotEmpty(draws);int damage=effect.GetReserveds(first.Position).GetValue();
        Assert.InRange(damage,1,25000);
        Assert.True(effect.IsInSuccessEffects(2));
        // Supply a second recipient explicitly for the area source. This
        // checks per-recipient effects and healing, not area target discovery.
        Player? second=null;Effect? secondEffect=null;int secondDamage=0;
        if(skillId==18460)
        {
            second=RetailNpcStatPeriodicTests.Player(h,220050000);second.GetCommonData().SetLevel(38);
            second.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,100000),
                new StatSetFunction(StatEnum.MAXMP,1000),new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
                new StatSetFunction(StatEnum.CURSE_RESISTANCE,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
            second.GetLifeStats().OnStatsChange(null!);second.GetLifeStats().SetCurrentHp(80000);
            second.GetLifeStats().SetCurrentMp(600);secondEffect=new Effect(skill,second);
            RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(secondEffect,candidate,plan,Draw);
            secondEffect.Initialize();
            Assert.True(RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(secondEffect,out bool secondHandled,out bool secondHit));
            Assert.True(secondHandled);Assert.True(secondHit);secondDamage=secondEffect.GetReserveds(first.Position).GetValue();
            Assert.InRange(secondDamage,1,25000);
            Assert.True(secondEffect.IsInSuccessEffects(2));
        }
        effect.ApplyEffect();
        secondEffect?.ApplyEffect();
        int afterHit=80000-damage;
        int afterCurse=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,100000,65000,afterHit);
        Assert.Equal(65000,player.GetLifeStats().GetMaxHp());
        Assert.Equal(afterCurse,player.GetLifeStats().GetCurrentHp());
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        if(second is not null)
        {
            Assert.Equal(65000,second.GetLifeStats().GetMaxHp());
            Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,
                100000,65000,80000-secondDamage),second.GetLifeStats().GetCurrentHp());
            Assert.True(second.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        }
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,healPercent)
            +RetailNpcStatSpellDrain.HealAmount(secondDamage,healPercent)),
            npc.GetLifeStats().GetCurrentHp());
        curse.EndEffect(effect);
        Assert.Equal(100000,player.GetLifeStats().GetMaxHp());
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,65000,100000,afterCurse),
            player.GetLifeStats().GetCurrentHp());
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        if(secondEffect is not null && second is not null)
        {
            curse.EndEffect(secondEffect);Assert.Equal(100000,second.GetLifeStats().GetMaxHp());
            Assert.False(second.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));secondEffect.EndEffect();
        }
        effect.EndEffect();Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void VampirePrimaryOperandsComeFromPinnedFullSourceButRemainGameplayGated()
    {
        string? manifestPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hostPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(manifestPath)||string.IsNullOrEmpty(hostPath),"Requires pinned sources and host templates");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        var assets=manifest.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Where(x=>new[]{16596,18459,18460}.Contains(x.GetProperty("id").GetInt32()))
            .ToDictionary(x=>x.GetProperty("id").GetInt32());
        var hosts=XDocument.Load(hostPath!).Root!.Elements()
            .Where(x=>new[]{16596,18459,18460}.Contains((int)x.Attribute("skill_id")!))
            .ToDictionary(x=>(int)x.Attribute("skill_id")!);
        Assert.Equal(3,assets.Count);Assert.Equal(3,hosts.Count);
        foreach(var (id,percent,healing,area) in new[]{(16596,181,100,false),(18459,181,130,false),(18460,151,100,true)})
        {
            var asset=assets[id];byte[] raw=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
            string hash=asset.GetProperty("sha256").GetString()!;
            var full=RetailNpcStatSkillCombat.ParseSource(raw,hash);
            Assert.False(full.Supported);
            var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(id,raw);
            Assert.Equal(hash,candidate.FullSourceSha256);
            Assert.NotEqual(hash,candidate.Primary.Sha256); // Deliberate derived primary, not the full record.
            Assert.Equal(RetailNpcStatSkillCombat.Kind.SpellDrain,candidate.Primary.Family);
            Assert.Equal(percent,candidate.Primary.Percent);
            Assert.True(candidate.Primary.UsesWeaponRange);
            Assert.Equal(SkillElement.WATER,candidate.Primary.Element);
            Assert.Equal(healing,candidate.Primary.Drain!.HpPercent);
            Assert.Equal(area,candidate.Primary.Drain.Area);
            using var hostReader=hosts[id].CreateReader();
            var host=(SkillTemplate)new XmlSerializer(typeof(SkillTemplate),new XmlRootAttribute("skill_template")).Deserialize(hostReader)!;
            var primary=Assert.IsType<SpellAtkDrainInstantEffect>(host.GetEffectTemplate(1));
            Assert.Equal(SkillElement.WATER,primary.Element);Assert.Equal(healing,primary.hpPercent);
            Assert.True(RetailNpcStatSpellDrain.CurseHostMatches(host.GetEffectTemplate(2)!));
            raw[^2]^=1;
            Assert.Throws<InvalidDataException>(()=>RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(id,raw));
        }
    }

    [Theory]
    [InlineData(123,100,123)] [InlineData(123,255,313)] [InlineData(20,200,40)]
    [InlineData(0,200,0)] [InlineData(123,0,0)] [InlineData(21474837,100,-21474836)]
    public void NativeHealUsesWrappedIntProductThenFloatDivision(int damage,int percent,int expected)
        =>Assert.Equal(expected,RetailNpcStatSpellDrain.HealAmount(damage,percent));

    private sealed class TimedSkill(SkillTemplate template,Creature caster,Creature target,int level=1):Skill(template,caster,level,target,null!)
    { internal void SetWord(int word){SetClientHitTime(word);UpdateHitTime(false);} }

    [SkippableFact]
    public void OrdinaryBoundNpcCastDoesNotReceiveAnInferredRetailHitWord()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(300170000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(216210,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,300170000);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,19076,46,out var source));
        Assert.NotNull(source.Drain);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,19076,46,player);
        Assert.NotNull(skill);
        skill.GetEffectedList().Add(player);
        Assert.True(skill.UseWithoutPropSkill());
        Assert.Equal(0,skill.GetHitTime());
        Assert.True(skill.IsInstantSkill());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void BoundDrainRunsThroughHostCastAndHealsFromThePrimaryHit()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(300170000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(216210,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,300170000);
        BossAiHarness.MakeMutuallyKnown(npc,player);player.GetCommonData().SetLevel(46);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,0),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
            new StatSetFunction(StatEnum.WIND_RESISTANCE,0),new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,0)]);
        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,19076,46,out var source));
        Assert.True(source.Supported);Assert.NotNull(source.Drain);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,19076,46,player);
        Assert.NotNull(skill);skill.GetEffectedList().Add(player);
        Assert.True(skill.UseWithoutPropSkill());Assert.Equal(0,skill.GetHitTime());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1500));
        int primaryDamage=50000-player.GetLifeStats().GetCurrentHp();
        Assert.True(primaryDamage>0,$"No primary hit; clock faults: {string.Join(", ",h.Clock.Faults)}");
        var applied=player.GetEffectController().GetAbnormalEffect(skill.GetSkillTemplate().GetStack()!);
        Assert.NotNull(applied);
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(applied.EffectInPos(1));
        Assert.Equal(primaryDamage,applied.GetReserveds(1).GetValue());
        Assert.True(RetailNpcStatSpellDrain.TryApply(applied,primary,()=>throw new InvalidOperationException("Drain replayed")));
        Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(primaryDamage,source.Drain.HpPercent)),
            npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
    private sealed class OtherStatOwner:IStatOwner;
    private sealed class RecordingConnection():AionConnection("127.0.0.1")
    {
        internal readonly List<AionServerPacket> Packets=[];
        protected override void EnqueuePacket(AionServerPacket packet,bool closing)=>Packets.Add(packet);
    }
    private sealed class EndOnHp(Effect effect):ActionObserver(ObserverType.HP_CHANGED)
    {
        internal int Calls;
        public override void HpChanged(int hp)
        { if(Interlocked.Increment(ref Calls)==1)effect.EndEffect(); }
    }
    private sealed class DamageablePlayer(PlayerAccountData data,Account account):Player(data,account)
    { public override bool IsInvulnerable()=>false; }

    private static Player SpawnDamageablePlayer(BossAiHarness h)
    {
        var common=new PlayerCommonData(IDFactory.GetInstance().NextId());
        common.SetPlayerClass(PlayerClass.WARRIOR);common.SetRace(Race.ELYOS);common.SetGender(Gender.MALE);
        common.SetName("DrainFixture"+common.GetPlayerObjId());common.SetNote("");
        var player=new DamageablePlayer(new PlayerAccountData(common,new PlayerAppearance()),new Account(1));
        player.SetKnownlist(new KnownList(player));player.SetEffectController(new PlayerEffectController(player));
        player.SetPosition(h.World.CreatePosition(220030000,985f,135f,242f,0,1));
        h.World.StoreObject(player);h.World.Spawn(player);return player;
    }

    [Fact]
    public void ProtectedRecordIsCapturedOnceAndHealWaitsForHitWordPlusOneSecond()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var caster=h.SpawnWithAi(210548,"general");
        caster.GetLifeStats().SetCurrentHp(100);
        var drain=new SpellAtkDrainInstantEffect {Position=1,hpPercent=1};
        var template=new SkillTemplate {skillId=910038,stack="drain-fixture",effects=new Effects {effects=[drain]}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(200);
        var effect=new Effect(skill,player);effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(effect,drain,new(200,false));int attacks=0;
        Assert.True(RetailNpcStatSpellDrain.TryApply(effect,drain,()=>attacks++));
        Assert.True(RetailNpcStatSpellDrain.TryApply(effect,drain,()=>attacks++));Assert.Equal(1,attacks);
        Assert.Equal(100,caster.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1199));Assert.Equal(100,caster.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(140,caster.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void ZeroPostProtectionDamageNeverSchedulesAHeal()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var caster=h.SpawnWithAi(210548,"general");
        caster.GetLifeStats().SetCurrentHp(100);var drain=new SpellAtkDrainInstantEffect {Position=1};
        var template=new SkillTemplate {skillId=910039,effects=new Effects {effects=[drain]}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(0);
        var effect=new Effect(skill,player);effect.SetReserveds(new EffectReserved(1,0,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(effect,drain,new(100,false));Assert.True(RetailNpcStatSpellDrain.TryApply(effect,drain,()=>{}));
        h.Clock.Advance(TimeSpan.FromMilliseconds(2000));Assert.Equal(100,caster.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void DelayedRetailHealRefusesNpcWhoseHpIsNegativeAfterSignedOverflow()
    {
        using var h=BossAiHarness.For(220030000).Build();
        var player=h.SpawnPlayer();var caster=h.SpawnWithAi(210548,"general");
        caster.GetGameStats().AddEffectOnly(new OtherStatOwner(),[new StatSetFunction(StatEnum.MAXHP,2147483520)]);
        Assert.Equal(2147483520,caster.GetLifeStats().GetMaxHp());
        caster.GetLifeStats().SetCurrentHp(2147483520);
        var drain=new SpellAtkDrainInstantEffect {Position=1};
        var template=new SkillTemplate {skillId=910045,effects=new Effects {effects=[drain]}};
        void Schedule(int hitWord)
        {
            var skill=new TimedSkill(template,caster,player);skill.SetWord(hitWord);
            var effect=new Effect(skill,player);
            effect.SetReserveds(new EffectReserved(1,100,EffectReserved.ResourceType.HP,true),false);
            RetailNpcStatSpellDrain.Prepare(effect,drain,new(200,false));
            Assert.True(RetailNpcStatSpellDrain.TryApply(effect,drain,()=>{}));
        }
        Schedule(0);Schedule(200);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        int overflowed=unchecked(2147483520+200);
        Assert.Equal(overflowed,caster.GetLifeStats().GetCurrentHp());
        Assert.False(caster.IsDead()); // Legacy host equality does not detect negative HP.
        h.Clock.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(overflowed,caster.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void ActualDrainEffectUsesTheExistingDamageOwnerOnceAndRetailHealOwnerOnce()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=SpawnDamageablePlayer(h);var caster=h.SpawnWithAi(210548,"general");
        caster.GetLifeStats().SetCurrentHp(100);player.GetLifeStats().SetCurrentHp(100);
        var drain=new SpellAtkDrainInstantEffect {Position=1,hpPercent=1};
        var template=new SkillTemplate {skillId=910040,stack="drain-integration",effects=new Effects {effects=[drain]}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(200);
        var effect=new Effect(skill,player);effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(effect,drain,new(200,false));
        drain.ApplyEffect(effect);drain.ApplyEffect(effect);
        Assert.Equal(80,player.GetLifeStats().GetCurrentHp());Assert.Equal(100,caster.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1200));
        Assert.Equal(140,caster.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);
    }

    [Fact]
    public void SelectedDrainRejectsTargetReflectionAfterPreparation()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();var caster=h.SpawnWithAi(210548,"general");
        var drain=new SpellAtkDrainInstantEffect {Position=1};
        var template=new SkillTemplate {skillId=910044,subType=SkillSubType.DEBUFF,effects=new Effects {effects=[drain]}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(0);var effect=new Effect(skill,player);
        effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(effect,drain,new(100,false));
        effect.SetShieldDefense(ShieldType.SKILL_REFLECTOR.GetId());Assert.Same(caster,effect.GetEffected());
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellDrain.TryApply(effect,drain,()=>{}));
    }

    [Fact]
    public void ProtectionPreflightDetectsShieldWithoutConsumingIt()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        var e=new Effect(player,player,new SkillTemplate {skillId=910041},1);
        var observer=new AttackShieldObserver(10,100,false,e,HitType.MAHIT,ShieldType.NORMAL,100);
        var controller=player.GetObserveController();Assert.False(controller.HasDrainProtectionObserver);
        controller.AddAttackCalcObserver(observer);Assert.True(controller.HasDrainProtectionObserver);
        controller.RemoveAttackCalcObserver(observer);Assert.False(controller.HasDrainProtectionObserver);
        var noProtection=new AttackCalcObserver();controller.AddAttackCalcObserver(noProtection);
        Assert.False(controller.HasDrainProtectionObserver);
        var otherProtection=new UnreviewedProtectionObserver();controller.AddAttackCalcObserver(otherProtection);
        Assert.True(controller.HasDrainProtectionObserver);Assert.Equal(0,otherProtection.Calls);
        controller.RemoveAttackCalcObserver(otherProtection);controller.RemoveAttackCalcObserver(noProtection);
        Assert.False(controller.HasDrainProtectionObserver);
    }

    [Theory]
    [InlineData(0)] [InlineData(100)]
    public void MpPreEffectConsumesInclusiveNativeDrawAndAlwaysPassesForOneHundred(int roll)
    {
        int draws=0;
        Assert.True(RetailNpcStatSpellDrain.SecondaryPreEffectChance((first,last)=>
        {Assert.Equal(0,first);Assert.Equal(100,last);draws++;return roll;}));
        Assert.Equal(1,draws);
    }

    [Fact]
    public void MatchingMpSiblingUsesTheExistingFlatManaOwner()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=SpawnDamageablePlayer(h);var caster=h.SpawnWithAi(210548,"general");
        player.GetLifeStats().SetCurrentMp(100);
        var first=new SpellAtkDrainInstantEffect {Position=1};
        var mp=new MpAttackEffect {Position=2,NoResist=true,Value=30,checktime=1000,Duration2=1100,Effectid=10182462,
            Element=SkillElement.FIRE,PreEffects=[1],PreEffectProb=100,Hoptype=HopType.SKILLLV,HopA=60,HopB=60};
        var template=new SkillTemplate {skillId=910042,stack="drain-mp-fixture",effects=new Effects {effects=[first,mp]},
            properties=new Aion.GameServer.SkillEngine.Properties.Properties {FirstTargetRaw="TARGET"}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(0);var effect=new Effect(skill,player);
        effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        var program=new RetailNpcStatSpellDrain.MpProgram(30,1000,1100,10182462,SkillElement.FIRE,100);
        Assert.True(RetailNpcStatSpellDrain.MpHostMatches(program,mp));Assert.True(RetailNpcStatSpellDrain.SecondaryContextMatches(effect));
        effect.SetForceType(Effect.ForceType.DEFAULT);Assert.False(RetailNpcStatSpellDrain.SecondaryContextMatches(effect));effect.SetForceType(null!);
        RetailNpcStatSpellDrain.Prepare(effect,first,new(20,true,program));
        Assert.False(mp.Calculate(effect,null,null)); // the preceding hit has not succeeded
        effect.AddSuccessEffect(first);
        Assert.True(mp.Calculate(effect,null,null));Assert.True(effect.IsInSuccessEffects(2));
        mp.OnPeriodicAction(effect);Assert.Equal(70,player.GetLifeStats().GetCurrentMp());
        player.GetLifeStats().SetCurrentMp(10);mp.OnPeriodicAction(effect);
        Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
    }

    [Fact]
    public void StunChecksKeepNativeOrderAndNoResistSkipsBothResistanceCalls()
    {
        var calls=new List<string>();int Draw(int first,int last){Assert.Equal((0,100),(first,last));calls.Add("pre");return 100;}
        void Copy()=>calls.Add("copy");bool Magic(){calls.Add("magic");return true;}bool Status(){calls.Add("status");return true;}
        Assert.False(RetailNpcStatSpellDrain.StunChecks(false,false,Draw,Copy,Magic,Status));Assert.Empty(calls);
        Assert.True(RetailNpcStatSpellDrain.StunChecks(true,true,Draw,Copy,Magic,Status));Assert.Equal(new[]{"pre","copy"},calls);
        calls.Clear();Assert.True(RetailNpcStatSpellDrain.StunChecks(true,false,Draw,Copy,Magic,Status));
        Assert.Equal(new[]{"pre","copy","magic","status"},calls);
        calls.Clear();Assert.False(RetailNpcStatSpellDrain.StunChecks(true,false,Draw,Copy,()=>{calls.Add("magic");return false;},Status));
        Assert.Equal(new[]{"pre","copy","magic"},calls);
    }

    [Fact]
    public void SelectedNoResistStunUsesFirstHitAndCriticalCacheOnce()
    {
        using var h=BossAiHarness.For(220030000).Build();var player=SpawnDamageablePlayer(h);var caster=h.SpawnWithAi(210548,"general");
        var first=new SpellAtkDrainInstantEffect {Position=1};
        var stun=new StunEffect {Position=2,NoResist=true,Duration2=3000,Effectid=20000,Element=SkillElement.EARTH,
            PreEffects=[1],Hoptype=HopType.SKILLLV,HopA=60,HopB=60};
        var template=new SkillTemplate {skillId=910043,stack="drain-stun-fixture",effects=new Effects {effects=[first,stun]},
            properties=new Aion.GameServer.SkillEngine.Properties.Properties {FirstTargetRaw="TARGET"}};
        var skill=new TimedSkill(template,caster,player);skill.SetWord(0);var effect=new Effect(skill,player);
        effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        var program=new RetailNpcStatSpellDrain.StunProgram(true);
        Assert.True(RetailNpcStatSpellDrain.StunHostMatches(program,stun));
        RetailNpcStatSpellDrain.Prepare(effect,first,new(100,true,null,program));
        stun.Calculate(effect);Assert.False(effect.IsInSuccessEffects(2));
        effect.AddSuccessEffect(first);effect.SetCalculatedMagicalCritical(1,true);
        stun.Calculate(effect);Assert.True(effect.IsInSuccessEffects(2));Assert.True(effect.IsMagicalCritical(2));
        stun.Calculate(effect);Assert.True(effect.IsInSuccessEffects(2));
    }

    private sealed class ForcedMagicResist : AttackCalcObserver
    {
        internal int Calls;
        public override bool CheckStatus(AttackStatus status){Calls++;return status==AttackStatus.RESIST;}
    }
    private sealed class CountingStunResistance():StatSetFunction(StatEnum.STUN_RESISTANCE,1000)
    {
        internal int Reads;
        public override void Apply(Stat2 stat,ISet<CalculationType> types){Reads++;base.Apply(stat,types);}
    }

    [SkippableFact]
    public void ActualResistibleDrainStunPreservesFirstHitAndSkipsStatusOnMagicResistance()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213148,"retail_stat_base_fixture");var player=h.SpawnPlayer();
        player.GetCommonData().SetLevel(27);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16599,27,out var source));
        var program=Assert.IsType<RetailNpcStatSpellDrain.StunProgram>(source.Drain!.Stun);Assert.False(program.NoResist);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16599);var first=template.GetEffectTemplate(1)!;
        var stun=Assert.IsType<StunEffect>(template.GetEffectTemplate(2));
        var skill=new TimedSkill(template,npc,player,27);skill.SetWord(0);var effect=new Effect(skill,player);
        effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        effect.AddSuccessEffect(first);effect.SetCalculatedMagicalCritical(1,true);
        var observer=new ForcedMagicResist();var resistance=new CountingStunResistance();
        player.GetObserveController().AddAttackCalcObserver(observer);player.GetGameStats().AddEffectOnly(null!,[resistance]);
        RetailNpcStatSpellDrain.Prepare(effect,first,source.Drain);
        stun.Calculate(effect);
        Assert.Equal(1,observer.Calls);Assert.Equal(0,resistance.Reads);
        Assert.True(effect.IsInSuccessEffects(1));Assert.False(effect.IsInSuccessEffects(2));Assert.True(effect.IsMagicalCritical(2));

        player.GetObserveController().RemoveAttackCalcObserver(observer);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.STUN_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        resistance.Reads=0;
        var second=new Effect(skill,player);second.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        second.AddSuccessEffect(first);second.SetCalculatedMagicalCritical(1,false);
        RetailNpcStatSpellDrain.Prepare(second,first,source.Drain);stun.Calculate(second);
        Assert.Equal(1,resistance.Reads);Assert.True(second.IsInSuccessEffects(1));Assert.False(second.IsInSuccessEffects(2));
        Assert.False(second.IsMagicalCritical(2));
    }

    [SkippableFact]
    public void BoundDrainWithPeriodicSpellUsesBothRetailOwnersAndExistingTimer()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        foreach(var (npcId,skillId,basis) in new[]{(216210,19076,652),(216300,19082,125)})
        {
            using var h=BossAiHarness.For(300170000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var npc=h.SpawnWithAi(npcId,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,300170000);
            player.GetCommonData().SetLevel(46);
            npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
                new StatSetFunction(StatEnum.BOOST_MAGICAL_SKILL,0)]);
            player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0),
                new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.MAGICAL_DEFEND,0),
                new StatSetFunction(StatEnum.WIND_RESISTANCE,0),new StatSetFunction(StatEnum.MAGIC_SKILL_BOOST_RESIST,0)]);
            player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
            Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
            Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,skillId,46,out var source));
            var program=Assert.IsType<RetailNpcStatSpellDrain.PeriodicSpellProgram>(source.Drain!.PeriodicSpell);
            Assert.Equal(basis,program.Value);
            var template=DataManager.SKILL_DATA.GetSkillTemplate(skillId);
            var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
            var dot=Assert.IsType<SpellAttackEffect>(template.GetEffectTemplate(2));
            Assert.True(RetailNpcStatSpellDrain.PeriodicSpellHostMatches(program,dot));
            dot.Value++;Assert.False(RetailNpcStatSpellDrain.PeriodicSpellHostMatches(program,dot));dot.Value--;
            dot.PreEffectProb=99;Assert.False(RetailNpcStatSpellDrain.PeriodicSpellHostMatches(program,dot));dot.PreEffectProb=100;
            var skill=new TimedSkill(template,npc,player,46);skill.SetWord(0);var effect=new Effect(skill,player);
            Assert.True(RetailNpcStatSkillCombat.TryCalculate(effect,first,null,out bool success));Assert.True(success);
            Assert.True(effect.IsInSuccessEffects(1));int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
            dot.Calculate(effect);Assert.True(effect.IsInSuccessEffects(2));
            first.ApplyEffect(effect);int afterPrimary=player.GetLifeStats().GetCurrentHp();
            Assert.Equal(50000-damage,afterPrimary);dot.StartEffect(effect);
            Assert.Equal(basis,effect.GetReserveds(2).GetValue());
            Assert.Equal((0,50),SkyTempleCombatInputs.Pve(npc,player,false));
            int oldCasterHp=npc.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
            Assert.Empty(h.Clock.Faults);Assert.Equal(afterPrimary-(int)(basis*.95f),player.GetLifeStats().GetCurrentHp());
            Assert.Equal(oldCasterHp+RetailNpcStatSpellDrain.HealAmount(damage,source.Drain.HpPercent),npc.GetLifeStats().GetCurrentHp());
            effect.EndEffect();int afterEnd=player.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(3000));
            Assert.Equal(afterEnd,player.GetLifeStats().GetCurrentHp());Assert.Empty(h.Clock.Faults);

            var resisted=new Effect(skill,player);resisted.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            resisted.AddSuccessEffect(first);resisted.SetCalculatedMagicalCritical(1,true);
            RetailNpcStatSpellDrain.Prepare(resisted,first,source.Drain);
            var forceResist=new ForcedMagicResist();player.GetObserveController().AddAttackCalcObserver(forceResist);
            dot.Calculate(resisted);Assert.Equal(1,forceResist.Calls);Assert.True(resisted.IsInSuccessEffects(1));
            Assert.False(resisted.IsInSuccessEffects(2));Assert.True(resisted.IsMagicalCritical(2));
            player.GetObserveController().RemoveAttackCalcObserver(forceResist);

            var changed=new Effect(skill,player);changed.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            changed.AddSuccessEffect(first);RetailNpcStatSpellDrain.Prepare(changed,first,source.Drain);
            dot.PreEffectProb=1;Assert.Throws<InvalidOperationException>(()=>dot.Calculate(changed));dot.PreEffectProb=100;
        }
    }

    [SkippableFact]
    public void PeriodicDrainSiblingRejectsChangedSourceFields()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        foreach(var asset in manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Where(a=>a.GetProperty("id").GetInt32() is 19076 or 19082))
        {
            byte[] raw=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
            var source=RetailNpcStatSkillCombat.ParseSource(raw,asset.GetProperty("sha256").GetString()!);Assert.True(source.Supported,source.UnsupportedReason);
            foreach(var (field,value) in new[]{("effect2_reserved8",1),("effect2_reserved12",0),
                ("effect2_cond_preeffect_prob2",99),("effect2_remain2",3000)})
            {
                var changed=XElement.Parse(Encoding.Unicode.GetString(raw));changed.SetElementValue(field,value);
                byte[] bytes=Encoding.Unicode.GetBytes(changed.ToString(SaveOptions.DisableFormatting));
                Assert.False(RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Supported);
            }
        }
    }

    [Theory]
    [InlineData(1,true)] [InlineData(70,true)] [InlineData(71,false)] [InlineData(100,false)]
    public void SourceBoundBlindUsesInclusiveNativeRoll(int roll,bool expected)
    {
        int calls=0;
        Assert.Equal(expected,RetailNpcStatSpellDrain.BlindMiss(70,(first,last)=>{calls++;Assert.Equal((1,100),(first,last));return roll;}));
        Assert.Equal(1,calls);
    }

    [SkippableFact]
    public void BoundDrainBlindUsesRetailPrimaryAndNoResistSecondary()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213747,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        player.GetCommonData().SetLevel(37);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.REGEN_HP,0),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0)]);
        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,17900,37,out var source));
        var program=Assert.IsType<RetailNpcStatSpellDrain.BlindProgram>(source.Drain!.Blind);Assert.Equal(70,program.Chance);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(17900);var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var blind=Assert.IsType<BlindEffect>(template.GetEffectTemplate(2));Assert.True(RetailNpcStatSpellDrain.BlindHostMatches(program,blind));
        blind.Value=69;Assert.False(RetailNpcStatSpellDrain.BlindHostMatches(program,blind));blind.Value=70;
        var skill=new TimedSkill(template,npc,player,37);skill.SetWord(0);
        var protection=new UnreviewedProtectionObserver();player.GetObserveController().AddAttackCalcObserver(protection);
        var unreviewed=new Effect(skill,player);
        Assert.False(RetailNpcStatSkillCombat.TryCalculate(unreviewed,first,null,out _));
        Assert.Empty(unreviewed.GetSuccessEffects());Assert.Equal(0,protection.Calls);
        player.GetObserveController().RemoveAttackCalcObserver(protection);
        // Reproduce a shield appearing after preflight, while the primary
        // checks resist. It must not be called by the retail damage path.
        var appeared=new UnreviewedProtectionObserver();
        var installer=new InstallProtectionDuringStatus(player.GetObserveController(),appeared);
        var boostEffect=new Effect(npc,npc,new SkillTemplate {skillId=910095},1);
        npc.GetObserveController().SetOneTimeBoostSkillAttack(new OneTimeBoostSkillAttack(boostEffect,SkillType.MAGICAL,10,0,0,0,1));
        player.GetObserveController().AddAttackCalcObserver(installer);
        var raced=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(raced,first,null,out bool racedSuccess));
        Assert.False(racedSuccess);Assert.Empty(raced.GetSuccessEffects());
        Assert.Equal(1,installer.Calls);Assert.Equal(0,appeared.Calls);
        Assert.Equal(50000,player.GetLifeStats().GetCurrentHp());
        Assert.False(RetailNpcStatSpellDrain.TryApply(raced,first,()=>throw new InvalidOperationException("Unexpected hit")));
        h.Clock.Advance(TimeSpan.FromMilliseconds(10));Assert.False(boostEffect.RetailHasEnded);
        npc.GetObserveController().RemoveOneTimeBoostSkillAttack(boostEffect);
        player.GetObserveController().RemoveAttackCalcObserver(installer);
        player.GetObserveController().RemoveAttackCalcObserver(appeared);

        // Protection can also appear after damage is recorded, during the
        // normal attacked callbacks that run before the target HP commit.
        var laterShield=new UnreviewedProtectionObserver();
        var delayed=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(delayed,first,null,out bool delayedSuccess));
        Assert.True(delayedSuccess);
        blind.Calculate(delayed);Assert.True(delayed.IsInSuccessEffects(2));
        var pendingConnection=new RecordingConnection();player.SetClientConnection(pendingConnection);
        var attacked=new InstallProtectionWhenAttacked(player.GetObserveController(),laterShield,()=>
        {
            delayed.AddToEffectedController();delayed.StartEffect();player.GetEffectController().AddEffect(delayed);
            Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
            Assert.Null(player.GetEffectController().GetAbnormalEffect(template.GetStack()!));
            Assert.Empty(pendingConnection.Packets.OfType<SM_ABNORMAL_STATE>());
        });
        player.GetObserveController().AddObserver(attacked);
        delayed.ApplyEffect();
        player.SetClientConnection(null!);
        Assert.True(delayed.PreparedNpcDrainInitialAborted);
        Assert.Equal(1,attacked.Calls);Assert.Equal(0,laterShield.Calls);
        var abortedConnection=new RecordingConnection();player.SetClientConnection(abortedConnection);
        delayed.AddToEffectedController();delayed.StartEffect();player.GetEffectController().AddEffect(delayed);
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
        Assert.Null(player.GetEffectController().GetAbnormalEffect(template.GetStack()!));
        Assert.Empty(abortedConnection.Packets.OfType<SM_ABNORMAL_STATE>());player.SetClientConnection(null!);
        Assert.Equal(50000,player.GetLifeStats().GetCurrentHp());Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        player.GetObserveController().RemoveObserver(attacked);
        player.GetObserveController().RemoveAttackCalcObserver(laterShield);

        // A premature registration must not mark the effect as already added:
        // after a successful primary the normal dependent effect still starts.
        var committed=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(committed,first,null,out bool committedSuccess));
        Assert.True(committedSuccess);blind.Calculate(committed);Assert.True(committed.IsInSuccessEffects(2));
        committed.AddToEffectedController();committed.StartEffect();player.GetEffectController().AddEffect(committed);
        Assert.Null(player.GetEffectController().GetAbnormalEffect(template.GetStack()!));
        committed.ApplyEffect();
        Assert.False(committed.PreparedNpcDrainInitialAborted);
        Assert.Same(committed,player.GetEffectController().GetAbnormalEffect(template.GetStack()!));
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
        committed.EndEffect();h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);

        var effect=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(effect,first,null,out bool success));Assert.True(success);
        Assert.True(effect.IsInSuccessEffects(1));int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
        var forceResist=new ForcedMagicResist();player.GetObserveController().AddAttackCalcObserver(forceResist);
        blind.Calculate(effect);Assert.True(effect.IsInSuccessEffects(2));Assert.Equal(0,forceResist.Calls);
        player.GetObserveController().RemoveAttackCalcObserver(forceResist);
        Assert.True(RetailNpcStatSpellDrain.IsSelectedBlind(effect,blind));
        first.ApplyEffect(effect);Assert.Equal(50000-damage,player.GetLifeStats().GetCurrentHp());
        int oldCasterHp=npc.GetLifeStats().GetCurrentHp();h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Empty(h.Clock.Faults);Assert.Equal(oldCasterHp+RetailNpcStatSpellDrain.HealAmount(damage,100),npc.GetLifeStats().GetCurrentHp());
        blind.StartEffect(effect);Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
        blind.EndEffect(effect);Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.BLIND));
        effect.EndEffect();

        var noFirst=new Effect(skill,player);noFirst.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(noFirst,first,source.Drain);blind.Calculate(noFirst);Assert.False(noFirst.IsInSuccessEffects(2));
        noFirst.AddSuccessEffect(first);blind.PreEffectProb=50;
        Assert.Throws<InvalidOperationException>(()=>blind.Calculate(noFirst));blind.PreEffectProb=100;
        player.GetEffectController().SetAbnormal(AbnormalState.BLIND);
        var overlap=new Effect(skill,player);
        Assert.False(RetailNpcStatSkillCombat.TryCalculate(overlap,first,null,out _));
        player.GetEffectController().UnsetAbnormal(AbnormalState.BLIND);

        // A new life on the same host player object must not receive an old hit.
        var stale=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(stale,first,null,out bool staleSuccess));
        Assert.True(staleSuccess);
        h.World.Despawn(player);h.World.Spawn(player);
        int playerBefore=player.GetLifeStats().GetCurrentHp(),casterBefore=npc.GetLifeStats().GetCurrentHp();
        first.ApplyEffect(stale);
        Assert.True(stale.PreparedNpcDrainInitialAborted);
        Assert.Equal(playerBefore,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(casterBefore,npc.GetLifeStats().GetCurrentHp());
    }

    [SkippableFact]
    public void BoundDrainBlindRejectsChangedSourceFields()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==17900);
        byte[] raw=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        Assert.True(RetailNpcStatSkillCombat.ParseSource(raw,asset.GetProperty("sha256").GetString()!).Supported);
        foreach(var (field,value) in new[]{("effect2_reserved1",1),("effect2_reserved2",71),
            ("effect2_noresist",0),("effect2_cond_preeffect_prob2",99),("effect2_remain1",0)})
        {
            var changed=XElement.Parse(Encoding.Unicode.GetString(raw));changed.SetElementValue(field,value);
            byte[] bytes=Encoding.Unicode.GetBytes(changed.ToString(SaveOptions.DisableFormatting));
            Assert.False(RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Supported);
        }
    }

    [Theory]
    [InlineData(0,true)] [InlineData(50,true)] [InlineData(51,false)] [InlineData(100,false)]
    public void HalfChancePreEffectUsesInclusiveZeroToHundred(int roll,bool expected)
    {
        int calls=0;
        Assert.Equal(expected,RetailNpcStatSpellDrain.SecondaryPreEffectChance(50,(first,last)=>{calls++;Assert.Equal((0,100),(first,last));return roll;}));
        Assert.Equal(1,calls);
    }

    [Fact]
    public void ZeroPreEffectChanceBypassesTheRollRatherThanFailing()
    {
        int calls=0;
        Assert.True(RetailNpcStatSpellDrain.SecondaryPreEffectChance(0,(_,_)=>{calls++;return 100;}));
        Assert.Equal(0,calls);
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellDrain.SecondaryPreEffectChance(-1,(_,_)=>0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RetailNpcStatSpellDrain.SecondaryPreEffectChance(101,(_,_)=>0));
    }

    [Fact]
    public void ParalyzeCheckRequiresManaSuccessAndPreservesNativeOrder()
    {
        var calls=new List<string>();int Draw(int first,int last){Assert.Equal((0,100),(first,last));calls.Add("pre");return 50;}
        void Copy()=>calls.Add("copy");bool Magic(){calls.Add("magic");return true;}bool Status(){calls.Add("status");return true;}
        Assert.False(RetailNpcStatSpellDrain.ParalyzeChecks(false,Draw,Copy,Magic,Status));Assert.Empty(calls);
        Assert.True(RetailNpcStatSpellDrain.ParalyzeChecks(true,Draw,Copy,Magic,Status));Assert.Equal(new[]{"pre","copy","magic","status"},calls);
        calls.Clear();Assert.False(RetailNpcStatSpellDrain.ParalyzeChecks(true,Draw,Copy,()=>{calls.Add("magic");return false;},Status));
        Assert.Equal(new[]{"pre","copy","magic"},calls);
        calls.Clear();Assert.False(RetailNpcStatSpellDrain.ParalyzeChecks(true,(_,_)=>51,Copy,Magic,Status));Assert.Empty(calls);
    }

    [SkippableFact]
    public void BoundDrainManaParalyzeUsesRoundedDurationAndOneManaTick()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(300040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(215280,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,300040000);
        player.GetCommonData().SetLevel(41);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),new StatSetFunction(StatEnum.MAXMP,1000),
            new StatSetFunction(StatEnum.REGEN_HP,0),new StatSetFunction(StatEnum.REGEN_MP,0),new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.PARALYZE_RESISTANCE,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.PARALYZE_RESISTANCE_PENETRATION,0)]);
        player.GetLifeStats().SetCurrentHp(50000);player.GetLifeStats().SetCurrentMp(100);npc.GetLifeStats().SetCurrentHp(100);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,18239,41,out var source));
        Assert.NotNull(source.Drain!.Paralyze);var mpProgram=Assert.IsType<RetailNpcStatSpellDrain.MpProgram>(source.Drain.Mp);
        Assert.Equal((5,3000,3000,50),(mpProgram.Value,mpProgram.Interval,mpProgram.Duration,mpProgram.PreEffectPercent));
        var template=DataManager.SKILL_DATA.GetSkillTemplate(18239);var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var mp=Assert.IsType<MpAttackEffect>(template.GetEffectTemplate(2));var paralyze=Assert.IsType<ParalyzeEffect>(template.GetEffectTemplate(3));
        Assert.True(RetailNpcStatSpellDrain.MpHostMatches(mpProgram,mp));Assert.True(RetailNpcStatSpellDrain.ParalyzeHostMatches(paralyze));
        var skill=new TimedSkill(template,npc,player,41);skill.SetWord(0);var effect=new Effect(skill,player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(effect,first,null,out bool hit));Assert.True(hit);
        Assert.True(effect.IsInSuccessEffects(1));int damage=effect.GetReserveds(1).GetValue();Assert.True(damage>0);
        var unqualified=new Effect(skill,player);unqualified.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(unqualified,first,source.Drain);paralyze.Calculate(unqualified);Assert.False(unqualified.IsInSuccessEffects(3));
        var resisted=new Effect(skill,player);resisted.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        resisted.AddSuccessEffect(first);resisted.AddSuccessEffect(mp);resisted.SetCalculatedMagicalCritical(1,true);
        RetailNpcStatSpellDrain.Prepare(resisted,first,source.Drain);
        var forceResist=new ForcedMagicResist();player.GetObserveController().AddAttackCalcObserver(forceResist);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateParalyze(resisted,paralyze,(lo,hi)=>{Assert.Equal((0,100),(lo,hi));return 50;}));
        Assert.Equal(1,forceResist.Calls);Assert.True(resisted.IsInSuccessEffects(2));Assert.False(resisted.IsInSuccessEffects(3));
        Assert.True(resisted.IsMagicalCritical(3));player.GetObserveController().RemoveAttackCalcObserver(forceResist);
        // Select MP success, then execute the real paralyze owner with deterministic native-range draws.
        effect.AddSuccessEffect(mp);int draws=0;
        Assert.True(RetailNpcStatSpellDrain.TryCalculateParalyze(effect,paralyze,(lo,hi)=>{draws++;return hi==100?50:1000;}));
        Assert.Equal(3,draws);Assert.True(effect.IsInSuccessEffects(3));first.ApplyEffect(effect);
        Assert.Equal(50000-damage,player.GetLifeStats().GetCurrentHp());
        effect.StartEffect();Assert.Equal(4000,effect.GetDuration());
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.PARALYZE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(2999));Assert.Equal(100,player.GetLifeStats().GetCurrentMp());
        Assert.Equal(100+RetailNpcStatSpellDrain.HealAmount(damage,5),npc.GetLifeStats().GetCurrentHp());
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.PARALYZE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(95,player.GetLifeStats().GetCurrentMp());
        Assert.True(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.PARALYZE));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));Assert.Equal(95,player.GetLifeStats().GetCurrentMp());
        Assert.False(player.GetEffectController().IsInAnyAbnormalState(AbnormalState.PARALYZE));Assert.True(effect.RetailHasEnded);
        Assert.Empty(h.Clock.Faults);
        mp.PreEffectProb=49;Assert.False(RetailNpcStatSpellDrain.MpHostMatches(mpProgram,mp));mp.PreEffectProb=50;
        paralyze.PreEffectProb=49;Assert.False(RetailNpcStatSpellDrain.ParalyzeHostMatches(paralyze));paralyze.PreEffectProb=50;
    }

    [SkippableFact]
    public void PreparedVampireCurseUsesTheActiveSourceBoundPlayerBuff()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? catalogPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_PLAYER_RESISTANCE_CATALOG");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(catalogPath),
            "Requires the pinned NPC package and player resistance catalog");
        var playerCatalog=RetailNpcPlayerResistanceCapture.Catalog.LoadForOfflineFixture(
            File.ReadAllBytes(Path.Combine(catalogPath!,"catalog.json")),
            "bb44e35a26fb715b8f12a3d26b0b3cd6c53e0aecca5035e6162ba9cf90b4a990",catalogPath!);
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0)]);
        var buffTemplate=DataManager.SKILL_DATA.GetSkillTemplate(2767);
        var buffStat=Assert.IsType<StatupEffect>(buffTemplate.GetEffectTemplate(3));
        var buff=new Effect(player,player,buffTemplate,1);buff.AddSuccessEffect(buffStat);buff.ApplyEffect();
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16596,38,out var source));
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(template.GetEffectTemplate(2));
        var skill=new TimedSkill(template,npc,player,38);skill.SetWord(0);
        Effect Prepared()
        {
            var effect=new Effect(skill,player);
            effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            RetailNpcStatSpellDrain.Prepare(effect,first,new(100,false,Curse:new(source.Sha256,FixtureResistance:target=>
            {
                if(!target.GetEffectController().GetAllEffects().Any(e=>ReferenceEquals(e,buff))
                    ||!RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(target,playerCatalog,out var general))
                    throw new InvalidOperationException("Player buff witness retired");
                // Base 5 in the multi-effect case is a supplied test operand;
                // this does not establish a live native player base.
                return new((ushort)(general.Percent==0?0:5),default,general,default,default);
            })));
            effect.AddSuccessEffect(first);return effect;
        }
        var resisted=Prepared();var draws=new Queue<int>([100,1000,100]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(resisted,curse,(_,_)=>draws.Dequeue()));
        Assert.False(resisted.IsInSuccessEffects(2));Assert.Empty(draws);
        var admitted=Prepared();draws=new Queue<int>([100,1000,101]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(admitted,curse,(_,_)=>draws.Dequeue()));
        Assert.True(admitted.IsInSuccessEffects(2));Assert.Empty(draws);
        var secondTemplate=DataManager.SKILL_DATA.GetSkillTemplate(20243);
        var secondStat=Assert.IsType<StatupEffect>(secondTemplate.GetEffectTemplate(1));
        var second=new Effect(player,player,secondTemplate,1);second.AddSuccessEffect(secondStat);second.ApplyEffect();
        var bothResisted=Prepared();draws=new Queue<int>([100,1000,110]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(bothResisted,curse,(_,_)=>draws.Dequeue()));
        Assert.False(bothResisted.IsInSuccessEffects(2));Assert.Empty(draws);
        var bothAdmitted=Prepared();draws=new Queue<int>([100,1000,111]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(bothAdmitted,curse,(_,_)=>draws.Dequeue()));
        Assert.True(bothAdmitted.IsInSuccessEffects(2));Assert.Empty(draws);second.EndEffect();
        buff.EndEffect();var retired=Prepared();draws=new Queue<int>([100,1000]);
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellDrain.TryCalculateCurse(retired,curse,(_,_)=>draws.Dequeue()));
        Assert.Empty(draws);
        // Exercise the top rank through the same source-bound curse path.
        {
            buffTemplate=DataManager.SKILL_DATA.GetSkillTemplate(2778);
            buffStat=Assert.IsType<StatupEffect>(buffTemplate.GetEffectTemplate(3));
            buff=new Effect(player,player,buffTemplate,12);buff.AddSuccessEffect(buffStat);buff.ApplyEffect();
            var highResisted=Prepared();draws=new Queue<int>([100,1000,200]);
            Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(highResisted,curse,(_,_)=>draws.Dequeue()));
            Assert.False(highResisted.IsInSuccessEffects(2));Assert.Empty(draws);
            var highAdmitted=Prepared();draws=new Queue<int>([100,1000,201]);
            Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(highAdmitted,curse,(_,_)=>draws.Dequeue()));
            Assert.True(highAdmitted.IsInSuccessEffects(2));Assert.Empty(draws);buff.EndEffect();
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void PreparedVampireCurseChecksMagicThenCurseResistanceAgainstSelectedSource()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned retail source package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetLevel(38);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE,400),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16596,38,out var source));
        Assert.False(source.Supported); // Full curse package remains gated.
        var template=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(template.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(template.GetEffectTemplate(2));
        Assert.True(RetailNpcStatSpellDrain.CurseHostMatches(curse));
        foreach(int id in new[]{18459,18460})
            Assert.True(RetailNpcStatSpellDrain.CurseHostMatches(DataManager.SKILL_DATA.GetSkillTemplate(id).GetEffectTemplate(2)!));
        var skill=new TimedSkill(template,npc,player,38);skill.SetWord(0);
        Effect Prepared(bool firstSucceeded,RetailNpcCurseResourceOwner.Plan? resources=null,
            Func<Player,RetailNpcStatSpellDrain.CurseResistanceInput>? resistance=null)
        {
            var effect=new Effect(skill,player);
            effect.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
            RetailNpcStatSpellDrain.Prepare(effect,first,new(100,false,Curse:new(source.Sha256,resources,FixtureResistance:resistance)));
            if(firstSucceeded)effect.AddSuccessEffect(first);
            return effect;
        }
        var noHit=Prepared(false);int calls=0;
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(noHit,curse,(_,_)=>{calls++;return 0;}));
        Assert.Equal(0,calls);Assert.False(noHit.IsInSuccessEffects(2));
        var magicResisted=Prepared(true);var forceResist=new ForcedMagicResist();
        player.GetObserveController().AddAttackCalcObserver(forceResist);calls=0;
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(magicResisted,curse,(lo,hi)=>
        {Assert.Equal((0,100),(lo,hi));calls++;return 100;}));
        Assert.Equal(1,calls);Assert.Equal(1,forceResist.Calls);Assert.False(magicResisted.IsInSuccessEffects(2));
        player.GetObserveController().RemoveAttackCalcObserver(forceResist);
        var resisted=Prepared(true);var draws=new Queue<int>([100,1000,1]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(resisted,curse,(lo,hi)=>
        {int value=draws.Dequeue();Assert.InRange(value,lo,hi);return value;}));
        Assert.Empty(draws);Assert.False(resisted.IsInSuccessEffects(2));
        resisted.AddSuccessEffect(curse); // An external success marker cannot override retail resistance.
        Assert.False(RetailNpcStatSpellDrain.HasPreparedCurse(resisted));
        var admitted=Prepared(true);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(admitted,curse,(lo,hi)=>
        {int value=draws.Dequeue();Assert.InRange(value,lo,hi);return value;}));
        Assert.Empty(draws);Assert.True(admitted.IsInSuccessEffects(2));
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(admitted,curse,(_,_)=>throw new Exception("duplicate roll")));
        // The native input combines percentage and flat channels before the
        // status roll. This fixture deliberately differs from the host's 400.
        var combined=Prepared(true,resistance:_=>new(100,new(20,10),new(30,10),default,default));
        draws=new Queue<int>([100,1000,170]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(combined,curse,(_,_)=>draws.Dequeue()));
        Assert.Empty(draws);Assert.False(combined.IsInSuccessEffects(2));
        var combinedPass=Prepared(true,resistance:_=>new(100,new(20,10),new(30,10),default,default));
        draws=new Queue<int>([100,1000,171]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(combinedPass,curse,(_,_)=>draws.Dequeue()));
        Assert.Empty(draws);Assert.True(combinedPass.IsInSuccessEffects(2));
        var wrongSource=new Effect(skill,player);
        wrongSource.SetReserveds(new EffectReserved(1,20,EffectReserved.ResourceType.HP,true),false);
        RetailNpcStatSpellDrain.Prepare(wrongSource,first,new(100,false,Curse:new(new string('0',64))));
        wrongSource.AddSuccessEffect(first);
        Assert.Throws<InvalidOperationException>(()=>RetailNpcStatSpellDrain.TryCalculateCurse(wrongSource,curse,(_,_)=>throw new Exception("unexpected roll")));

        // Controlled source-stage values, not a live equipment projection.
        // This exercises the real player stat/life owners for start and removal.
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,1001),new StatSetFunction(StatEnum.MAXMP,1001)]);
        player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(100);player.GetLifeStats().SetCurrentMp(0);
        Assert.Throws<InvalidOperationException>(()=>player.GetLifeStats().ApplyPreparedRetailCurseResources(noHit,1001,651,1001,651,()=>true));
        Assert.Throws<InvalidOperationException>(()=>player.GetGameStats().EndPreparedRetailCurseEffectOnly(noHit,new HashSet<IStatFunction>()));
        int reducedHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,1001,1001,-35);
        int reducedMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,1001,1001,-35);
        Assert.Equal(651,reducedHp);Assert.Equal(651,reducedMp);
        var owned=new HashSet<IStatFunction>{new StatAddFunction(StatEnum.MAXHP,reducedHp-1001,true),
            new StatAddFunction(StatEnum.MAXMP,reducedMp-1001,true)};
        player.GetGameStats().AddEffectOnly(admitted,owned);
        bool guardedUnderLock=false;
        Assert.False(player.GetLifeStats().ApplyPreparedRetailCurseResources(admitted,1001,651,1001,651,()=>
        {guardedUnderLock=Monitor.IsEntered(player.GetLifeStats());return false;}));
        Assert.True(guardedUnderLock);Assert.Equal(100,player.GetLifeStats().GetCurrentHp());
        Assert.Throws<InvalidOperationException>(()=>player.GetLifeStats().ApplyPreparedRetailCurseResources(admitted,999,651,1001,651,()=>true));
        Assert.Equal(100,player.GetLifeStats().GetCurrentHp());
        Assert.True(player.GetLifeStats().ApplyPreparedRetailCurseResources(admitted,1001,651,1001,651,()=>true));
        Assert.Equal(66,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
        Assert.True(player.GetGameStats().EndPreparedRetailCurseEffectOnly(admitted,owned));
        Assert.True(player.GetLifeStats().ApplyPreparedRetailCurseResources(admitted,651,1001,651,1001,()=>true));
        Assert.Equal(101,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,player.GetLifeStats().GetCurrentMp());

        // The ordinary Effect owner now runs the prepared curse start and end;
        // the initial damage and resource inputs are still synthetic.
        Assert.Throws<InvalidOperationException>(()=>curse.StartEffect(admitted)); // no resource plan
        var plan=new RetailNpcCurseResourceOwner.Plan(1001,1001,0,1001,1001,0,_=>true);
        var lifecycle=Prepared(true,plan);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(lifecycle,curse,(_,_)=>draws.Dequeue()));
        player.GetLifeStats().SetCurrentHp(120);player.GetLifeStats().SetCurrentMp(0);
        var unrelated=new StatAddFunction(StatEnum.ATTACK_SPEED,5,true);
        player.GetGameStats().AddEffectOnly(lifecycle,[unrelated]);
        lifecycle.ApplyEffect();
        Assert.Equal(66,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        curse.EndEffect(lifecycle);
        Assert.Equal(101,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        Assert.Contains(player.GetGameStats().GetStatsSorted(StatEnum.ATTACK_SPEED),f=>ReferenceEquals(f.GetOwner(),lifecycle));
        lifecycle.EndEffect();
        Assert.DoesNotContain(player.GetGameStats().GetStatsSorted(StatEnum.ATTACK_SPEED),f=>ReferenceEquals(f.GetOwner(),lifecycle));

        // A synchronous HP observer may end the curse during its start callback.
        var interrupted=Prepared(true,plan);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(interrupted,curse,(_,_)=>draws.Dequeue()));
        player.GetLifeStats().SetCurrentHp(100);var endOnHp=new EndOnHp(interrupted);
        player.GetObserveController().AddObserver(endOnHp);
        curse.StartEffect(interrupted);
        player.GetObserveController().RemoveObserver(endOnHp);
        Assert.True(interrupted.RetailHasEnded);Assert.True(endOnHp.Calls>=1);
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        Assert.Equal(0,interrupted.GetAbnormals() & AbnormalState.CURSE.GetId());
        Assert.Equal(1001,player.GetLifeStats().GetMaxHp());Assert.Equal(101,player.GetLifeStats().GetCurrentHp());

        int snapshotChecks=0;
        var stalePlan=new RetailNpcCurseResourceOwner.Plan(1001,1001,0,1001,1001,0,_=>++snapshotChecks==1);
        var stale=Prepared(true,stalePlan);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(stale,curse,(_,_)=>draws.Dequeue()));
        Assert.Throws<InvalidOperationException>(()=>curse.StartEffect(stale));
        Assert.Equal(2,snapshotChecks);Assert.Equal(1001,player.GetLifeStats().GetMaxHp());
        Assert.Equal(101,player.GetLifeStats().GetCurrentHp());Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));

        // Another stat owner changes both maxima while the curse is active.
        // Removal must use the current cached and observed maxima, not the
        // original plan; the snapshot callback must never run under the HP lock.
        bool snapshotCurrent=true;int observedSnapshots=0;
        var changingPlan=new RetailNpcCurseResourceOwner.Plan(1001,1001,0,1001,1001,0,p=>
        {Assert.False(Monitor.IsEntered(p.GetLifeStats()));observedSnapshots++;return snapshotCurrent;});
        var changing=Prepared(true,changingPlan);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(changing,curse,(_,_)=>draws.Dequeue()));
        var connection=new RecordingConnection();player.SetClientConnection(connection);
        player.GetLifeStats().SetCurrentHp(100);player.GetLifeStats().SetCurrentMp(0);
        connection.Packets.Clear();
        curse.StartEffect(changing);Assert.Equal(66,player.GetLifeStats().GetCurrentHp());
        Assert.Single(connection.Packets.OfType<SM_STATUPDATE_HP>()); // current HP changed: no extra maximum-only packet
        var startMp=Assert.Single(connection.Packets.OfType<SM_STATUPDATE_MP>());
        Assert.Equal(0,(int)typeof(SM_STATUPDATE_MP).GetField("currentMp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(startMp)!);
        Assert.Equal(651,(int)typeof(SM_STATUPDATE_MP).GetField("maxMp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(startMp)!);
        var other=new OtherStatOwner();
        player.GetGameStats().AddEffect(other,[new StatAddFunction(StatEnum.MAXHP,200,true),
            new StatAddFunction(StatEnum.MAXMP,100,true)]);
        Assert.Equal(851,player.GetLifeStats().GetMaxHp());Assert.Equal(86,player.GetLifeStats().GetCurrentHp());
        player.GetLifeStats().SetCurrentMp(0);snapshotCurrent=false;connection.Packets.Clear();
        curse.EndEffect(changing);
        Assert.Equal(2,observedSnapshots);Assert.Equal(1201,player.GetLifeStats().GetMaxHp());
        Assert.Equal(1101,player.GetLifeStats().GetMaxMp());Assert.Equal(121,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
        var endMp=Assert.Single(connection.Packets.OfType<SM_STATUPDATE_MP>());
        Assert.Equal(0,(int)typeof(SM_STATUPDATE_MP).GetField("currentMp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(endMp)!);
        Assert.Equal(1101,(int)typeof(SM_STATUPDATE_MP).GetField("maxMp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(endMp)!);
        player.GetLifeStats().OnStatsChange(null!); // A stale cache would rescale again here.
        Assert.Equal(121,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,player.GetLifeStats().GetCurrentMp());
        changing.EndEffect();player.GetGameStats().EndEffect(other);

        var minimumHp=Prepared(true,plan);draws=new Queue<int>([100,1000,1000]);
        Assert.True(RetailNpcStatSpellDrain.TryCalculateCurse(minimumHp,curse,(_,_)=>draws.Dequeue()));
        player.GetLifeStats().SetCurrentHp(1);player.GetLifeStats().SetCurrentMp(0);connection.Packets.Clear();
        curse.StartEffect(minimumHp);
        var startHp=Assert.Single(connection.Packets.OfType<SM_STATUPDATE_HP>());
        Assert.Equal(1,(int)typeof(SM_STATUPDATE_HP).GetField("currentHp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(startHp)!);
        Assert.Equal(651,(int)typeof(SM_STATUPDATE_HP).GetField("maxHp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(startHp)!);
        connection.Packets.Clear();curse.EndEffect(minimumHp);
        var endHp=Assert.Single(connection.Packets.OfType<SM_STATUPDATE_HP>());
        Assert.Equal(1,(int)typeof(SM_STATUPDATE_HP).GetField("currentHp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(endHp)!);
        Assert.Equal(1001,(int)typeof(SM_STATUPDATE_HP).GetField("maxHp",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(endHp)!);
        minimumHp.EndEffect();player.SetClientConnection(null!);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableTheory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public void SelectedMixedEquipmentProjectionFeedsPreparedVampireCurseAndHeal(bool twoHanded,bool withPrimaryFood)
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? manifestHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        string? armorPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ARMOR_FIXTURE");
        string? authPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_AUTHORIZATION_EXPANDED_HP_FIXTURE");
        string? bonusPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_ITEM_BONUS_FIXTURE");
        string? permitPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_CLASS_PERMIT_FIXTURE");
        string? weaponPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_WEAPON_HP_FIXTURE");
        string? otherPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OTHER_HP_FIXTURE");
        string? offPath=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_OFFHAND_FIXTURE");
        string? foodPath=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_FOOD_SOURCE");
        Skip.If(string.IsNullOrEmpty(manifest)||string.IsNullOrEmpty(manifestHash)||string.IsNullOrEmpty(armorPath)
            ||string.IsNullOrEmpty(authPath)||string.IsNullOrEmpty(bonusPath)||string.IsNullOrEmpty(permitPath)
            ||string.IsNullOrEmpty(weaponPath)||string.IsNullOrEmpty(otherPath)||string.IsNullOrEmpty(offPath),"Requires pinned skill and item sources");
        Skip.If(withPrimaryFood && string.IsNullOrEmpty(foodPath),"Requires pinned retail primary food source");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest!),manifestHash!,Path.GetDirectoryName(manifest!)!);
        static string Hash(byte[] x)=>Convert.ToHexStringLower(SHA256.HashData(x));
        byte[] a=File.ReadAllBytes(armorPath!),b=File.ReadAllBytes(authPath!),c=File.ReadAllBytes(bonusPath!),
            d=File.ReadAllBytes(permitPath!),e=File.ReadAllBytes(weaponPath!),f=File.ReadAllBytes(otherPath!),g=File.ReadAllBytes(offPath!);
        var armor=RetailNpcCurseArmorSource.Parse(a,Hash(a));
        var auth=RetailNpcCurseAuthorizationHpSource.Parse(b,Hash(b));
        var bonus=RetailNpcCurseItemBonusSource.Parse(c,Hash(c));
        var permits=RetailNpcCurseClassPermitSource.Parse(d,Hash(d));
        var weapon=RetailNpcCurseWeaponHpSource.Parse(e,Hash(e));var other=RetailNpcCurseOtherEquipmentHpSource.Parse(f,Hash(f));
        var offhand=RetailNpcCurseOffhandSource.Parse(g,Hash(g));
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        player.GetCommonData().SetPlayerClass(PlayerClass.GLADIATOR);player.GetCommonData().SetLevel(33);
        player.GetGameStats().UpdateStatsTemplate();
        npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE_PENETRATION,0)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0),
            new StatSetFunction(StatEnum.CURSE_RESISTANCE,0),new StatSetFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,0)]);
        // The item is a real matched 4.8/5.8 source record. Class, level,
        // primary stats and native flags below are explicit fixture inputs.
        var template=new ItemTemplate {itemId=110100420,retailSourceName="rb_torso_n_u1_p_33a",itemGroup=ItemGroup.RB_TORSO,maxEnchant=15};
        var item=new Item(900001,template,1,true,ItemSlot.TORSO.GetSlotIdMask());item.SetEnchantLevel(15);
        var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment).GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(player.GetEquipment()));
        slots.Add(ItemSlot.TORSO.GetSlotIdMask(),item);
        long main=ItemSlot.MAIN_HAND.GetSlotIdMask(),sub=ItemSlot.SUB_HAND.GetSlotIdMask();
        var sword=twoHanded
            ?new Item(900002,new ItemTemplate {itemId=100900062,retailSourceName="2hsword_n_c2_12a",itemGroup=ItemGroup.GREATSWORD,maxEnchant=10},1,true,main|sub)
            :new Item(900002,new ItemTemplate {itemId=100001286,retailSourceName="sword_n_e1_pvp_55b",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,main);
        if(!twoHanded)typeof(Item).GetField("conditioningInfo",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(sword,new ChargeInfo(500001,sword));
        var neck=new Item(900003,new ItemTemplate {itemId=121001358,retailSourceName="necklace_authorize_test_01",itemGroup=ItemGroup.NECKLACE,maxEnchant=0},1,true,ItemSlot.NECKLACE.GetSlotIdMask());neck.SetTempering(10);
        var offSword=twoHanded?sword:new Item(900004,new ItemTemplate {itemId=100001286,retailSourceName="sword_n_e1_pvp_55b",itemGroup=ItemGroup.SWORD,maxEnchant=15},1,true,sub);
        // The real buff lifecycle reads weapon speed while notifying the player.
        // These source-identity fixture templates otherwise have no weapon data.
        sword.GetItemTemplate().weaponStats=new WeaponStats {AttackSpeed=1500};
        offSword.GetItemTemplate().weaponStats=new WeaponStats {AttackSpeed=1500};
        slots.Add(main,sword);slots.Add(ItemSlot.NECKLACE.GetSlotIdMask(),neck);slots.Add(sub,offSword);
        Effect? food=null;RetailNpcPlayerPrimaryFoodCapture.Receipt? foodReceipt=null;
        if(withPrimaryFood)
        {
            var foodTemplate=DataManager.SKILL_DATA.GetSkillTemplate(9839);
            food=new Effect(player,player,foodTemplate,1);
            food.AddSuccessEffect(Assert.IsType<StatupEffect>(foodTemplate.GetEffectTemplate(1)));food.ApplyEffect();
            Assert.True(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,food,File.ReadAllBytes(foodPath!),out foodReceipt));
            Assert.NotNull(foodReceipt);
        }
        RetailNpcCurseEquipmentObservation.Snapshot Capture(Player p)=>RetailNpcCurseEquipmentObservation.Capture(
            p.GetEquipment(),p.GetPlayerClass().GetClassId(),p.GetLevel(),armor,
            offhand:offhand,weaponHp:weapon,otherHp:other,authorizationHp:auth,itemBonus:bonus);
        var snapshot=Capture(player);
        var equipmentWitness=RetailNpcCurseEquipmentObservation.EquipmentWitness(player,snapshot,Capture);
        bool witness(Player p)=>equipmentWitness(p) && (foodReceipt is null ||foodReceipt.IsCurrentForOfflineFixture(p));
        Assert.True(witness(player));
        item.SetEnchantLevel(14);Assert.False(witness(player));item.SetEnchantLevel(15);Assert.True(witness(player));
        player.GetCommonData().SetLevel(34);Assert.False(witness(player));player.GetCommonData().SetLevel(33);Assert.True(witness(player));
        player.GetCommonData().SetPlayerClass(PlayerClass.TEMPLAR);Assert.False(witness(player));
        player.GetCommonData().SetPlayerClass(PlayerClass.GLADIATOR);Assert.True(witness(player));
        int originalHp=player.GetLifeStats().GetCurrentHp();
        var hpField=typeof(CreatureLifeStats).GetField("currentHp",BindingFlags.Instance|BindingFlags.NonPublic)!;
        hpField.SetValue(player.GetLifeStats(),0);Assert.False(witness(player));
        hpField.SetValue(player.GetLifeStats(),-1);Assert.False(witness(player));
        hpField.SetValue(player.GetLifeStats(),originalHp);Assert.True(witness(player));
        var changesDuringRead=RetailNpcCurseEquipmentObservation.EquipmentWitness(player,snapshot,p=>
        {var captured=Capture(p);p.GetCommonData().SetLevel(34);return captured;});
        Assert.False(changesDuringRead(player));player.GetCommonData().SetLevel(33);Assert.True(witness(player));
        var normal=new RetailNpcCurseItemBonusSource.RevisionContext(0,true,1,false,0x20);
        var inputs=new RetailNpcCurseEquipmentObservation.ArmorProjectionContext(0,null,false,false,normal,normal with {BonusFlags=0x200});
        Assert.True(RetailNpcCurseEquipmentObservation.TryProjectEquipment(snapshot,permits,row=>row.Status==RetailNpcCurseEquipmentObservation.Status.SkippedRetailOffhandWeapon
            ?inputs with {Normal=normal with {BonusFlags=8}}:twoHanded &&row.OccupiedSlot==main
                ?inputs with {Normal=normal with {BonusFlags=0x100}}:inputs,out var equipment,out _,out _));
        Assert.Equal((390,twoHanded?426:1025,572),(equipment.RevisedArmorHp,equipment.HpBonus.Points,equipment.MpBonus.Points));
        var primary=RetailNpcCurseStatMath.InitialClassPrimary(player.GetPlayerClass().GetClassId());
        var primaries=RetailNpcCurseStatMath.OrdinaryPrimaries(primary.Health,primary.Will,
            foodReceipt is null ? [] : [foodReceipt.Modifier]);
        ushort health=primaries.Health,will=primaries.Will;
        Assert.Equal((ushort)115,health);Assert.Equal((ushort)(withPrimaryFood?95:90),will);
        Assert.Empty(player.GetGameStats().GetStatsSorted(StatEnum.HEALTH));Assert.Empty(player.GetGameStats().GetStatsSorted(StatEnum.WILL));
        Assert.Equal(health,player.GetGameStats().GetHealth().GetCurrent());Assert.Equal(primary.Will,player.GetGameStats().GetWill().GetCurrent());
        int hpBasis=RetailNpcCurseStatMath.PercentageBase(RetailNpcCurseStatMath.Resource.Hp,
            RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,33,health,player.GetPlayerClass().GetClassId()),[equipment.RevisedArmorHp]);
        int mpBasis=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,33,will,player.GetPlayerClass().GetClassId());
        int oldHp=hpBasis+equipment.HpBonus.Points,oldMp=mpBasis+equipment.MpBonus.Points;
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,hpBasis,oldHp,-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,mpBasis,oldMp,-35);
        var hpOverride=new StatSetFunction(StatEnum.MAXHP,oldHp);
        player.GetGameStats().AddEffectOnly(null!,[hpOverride,new StatSetFunction(StatEnum.MAXMP,oldMp)]);
        player.GetLifeStats().OnStatsChange(null!);player.GetLifeStats().SetCurrentHp(oldHp);player.GetLifeStats().SetCurrentMp(600);
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out var profile));
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16596,38,out var source));Assert.False(source.Supported);
        var skillTemplate=DataManager.SKILL_DATA.GetSkillTemplate(16596);
        var first=Assert.IsType<SpellAtkDrainInstantEffect>(skillTemplate.GetEffectTemplate(1));
        var curse=Assert.IsType<CurseEffect>(skillTemplate.GetEffectTemplate(2));
        var skill=new TimedSkill(skillTemplate,npc,player,38);skill.SetWord(0);
        using var manifestDocument=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var sourceAsset=manifestDocument.RootElement.GetProperty("skill_sources").EnumerateArray()
            .Single(x=>x.GetProperty("id").GetInt32()==16596);
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(16596,
            File.ReadAllBytes(sourceAsset.GetProperty("path").GetString()!));
        var effect=new Effect(skill,player);
        var resourceWitness=RetailNpcCurseStatWitness.Create(player,effect,witness,newHp-oldHp,newMp-oldMp);
        Assert.True(resourceWitness.Before(player));Assert.False(resourceWitness.After(player));
        hpOverride.Value++;Assert.False(resourceWitness.Before(player));hpOverride.Value--;Assert.True(resourceWitness.Before(player));
        var extraOwner=new OtherStatOwner();
        player.GetGameStats().AddEffectOnly(extraOwner,[new StatAddFunction(StatEnum.MAXHP,9,true)]);
        Assert.False(resourceWitness.Before(player));
        var remove=typeof(CreatureGameStats).GetMethod("RemoveEffectFunctions",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Assert.Equal(true,remove.Invoke(player.GetGameStats(),[extraOwner]));Assert.False(resourceWitness.Before(player));
        resourceWitness=RetailNpcCurseStatWitness.Create(player,effect,witness,newHp-oldHp,newMp-oldMp);
        Assert.True(resourceWitness.Before(player));
        var conditionalOwner=new OtherStatOwner();
        player.GetGameStats().AddEffectOnly(conditionalOwner,[new StatAddFunction(StatEnum.MAXMP,3,true)
            .WithConditions(new Aion.GameServer.SkillEngine.Condition.Conditions())]);
        Assert.False(resourceWitness.Before(player));
        Assert.Equal(true,remove.Invoke(player.GetGameStats(),[conditionalOwner]));Assert.False(resourceWitness.Before(player));
        resourceWitness=RetailNpcCurseStatWitness.Create(player,effect,witness,newHp-oldHp,newMp-oldMp);
        Assert.True(resourceWitness.Before(player));
        var plan=new RetailNpcCurseResourceOwner.Plan(hpBasis,oldHp,equipment.HpBonus.Percent,
            mpBasis,oldMp,equipment.MpBonus.Percent,resourceWitness.Before,resourceWitness.After,resourceWitness.CommitStamp);
        RetailNpcVampirePrimaryProjection.AttachOfflineInitialization(effect,candidate,plan,(_,hi)=>hi);
        effect.Initialize();
        Assert.True(RetailNpcVampirePrimaryProjection.TryGetOfflineInitialization(effect,out bool handled,out bool hit));
        Assert.True(handled);Assert.True(hit);Assert.True(effect.IsInSuccessEffects(2));
        int damage=effect.GetReserveds(first.Position).GetValue();
        Assert.InRange(damage,1,oldHp-1);
        effect.ApplyEffect();
        int reducedHp=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,oldHp,newHp,oldHp-damage);
        int reducedMp=RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Mp,oldMp,newMp,600);
        Assert.Equal((newHp,newMp,reducedHp,reducedMp),(player.GetLifeStats().GetMaxHp(),player.GetLifeStats().GetMaxMp(),
            player.GetLifeStats().GetCurrentHp(),player.GetLifeStats().GetCurrentMp()));
        Assert.True(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));
        Assert.True(resourceWitness.After(player));Assert.False(resourceWitness.Before(player));
        var unexpectedCurseModifier=new StatAddFunction(StatEnum.MAXHP,1,true);
        player.GetGameStats().AddEffectOnly(effect,[unexpectedCurseModifier]);Assert.False(resourceWitness.After(player));
        Assert.True(player.GetGameStats().EndPreparedRetailCurseEffectOnly(effect,new HashSet<IStatFunction>{unexpectedCurseModifier}));
        Assert.False(resourceWitness.After(player));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),
            npc.GetLifeStats().GetCurrentHp());
        curse.EndEffect(effect);
        Assert.Equal(oldHp,player.GetLifeStats().GetMaxHp());Assert.Equal(oldMp,player.GetLifeStats().GetMaxMp());
        Assert.False(resourceWitness.Before(player));Assert.False(resourceWitness.After(player));
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Hp,newHp,oldHp,reducedHp),player.GetLifeStats().GetCurrentHp());
        Assert.Equal(RetailNpcCurseStatMath.CurrentMode2(RetailNpcCurseStatMath.Resource.Mp,newMp,oldMp,reducedMp),player.GetLifeStats().GetCurrentMp());
        Assert.False(player.GetEffectController().IsAbnormalSet(AbnormalState.CURSE));effect.EndEffect();
        if(food is not null) { Assert.True(foodReceipt!.IsCurrentForOfflineFixture(player));food.EndEffect();Assert.False(foodReceipt.IsCurrentForOfflineFixture(player)); }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void DrainAdditionalDamageUsesNativeR3R4AndBoundsStoredProbability()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==16598);
        var original=XElement.Parse(Encoding.Unicode.GetString(File.ReadAllBytes(asset.GetProperty("path").GetString()!)));
        RetailNpcStatSkillCombat.Source Parse(XElement x)
        {
            byte[] b=Encoding.Unicode.GetBytes(x.ToString(SaveOptions.DisableFormatting));
            return RetailNpcStatSkillCombat.ParseSource(b,Convert.ToHexStringLower(SHA256.HashData(b)));
        }
        var basic=Parse(original);Assert.True(basic.Supported,basic.UnsupportedReason);Assert.Null(basic.SpellBonus);
        var conditional=new XElement(original);conditional.SetElementValue("effect1_reserved3",5);
        conditional.SetElementValue("effect1_reserved4",250);conditional.SetElementValue("effect1_reserved5",1);
        conditional.SetElementValue("effect1_reserved7","_Class_Warrior");
        var source=Parse(conditional);Assert.True(source.Supported,source.UnsupportedReason);
        var bonus=Assert.IsType<RetailNpcStatSpellBonus.Program>(source.SpellBonus);
        Assert.Equal((5,250,true),(bonus.Rate,bonus.Constant,bonus.Percent));
        Assert.True(bonus.AppliesTo(PlayerClass.WARRIOR));Assert.False(bonus.AppliesTo(PlayerClass.MAGE));
        var result=RetailNpcStatSpellBonus.Apply(100,bonus.ValueAtLevel(1),bonus.Percent,true,0,1,(_,_)=>1);
        Assert.Equal(355,result.Damage);
        var explicitDefault=new XElement(conditional);explicitDefault.SetElementValue("effect1_reserved9",100);
        var sameBonus=Parse(explicitDefault);Assert.True(sameBonus.Supported,sameBonus.UnsupportedReason);
        Assert.Equal(bonus,sameBonus.SpellBonus);
        var vampireChance=new XElement(original);vampireChance.SetElementValue("effect1_reserved9",20);
        var zeroExtra=Parse(vampireChance);Assert.True(zeroExtra.Supported,zeroExtra.UnsupportedReason);
        Assert.Null(zeroExtra.SpellBonus);
        var unsafeRate=new XElement(original);unsafeRate.SetElementValue("effect1_reserved8",20);
        Assert.False(Parse(unsafeRate).Supported);
        var unsafeChance=new XElement(original);unsafeChance.SetElementValue("effect1_reserved9",21);
        Assert.False(Parse(unsafeChance).Supported);
        var unsafeBonus=new XElement(conditional);unsafeBonus.SetElementValue("effect1_reserved9",20);
        Assert.False(Parse(unsafeBonus).Supported);
        var unusedCondition=new XElement(original);unusedCondition.SetElementValue("effect1_reserved7","Paralyze");
        Assert.False(Parse(unusedCondition).Supported);
    }

    [SkippableFact]
    public void VampireDrainSourcesStillGateTheirCurse()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        foreach(int id in new[]{16596,18459,18460})
        {
            var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==id);
            var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
            Assert.False(source.Supported);Assert.Contains("sibling",source.UnsupportedReason!);
        }
    }

    [SkippableFact]
    public void SourceBoundParalyzeConditionalUsesNpcTargetStateAndRetailPercent()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==16598);
        var xml=XElement.Parse(Encoding.Unicode.GetString(File.ReadAllBytes(asset.GetProperty("path").GetString()!)));
        xml.SetElementValue("effect1_reserved4",250);xml.SetElementValue("effect1_reserved5",1);
        xml.SetElementValue("effect1_reserved7","Paralyze");
        byte[] bytes=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
        var source=RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        Assert.True(source.Supported,source.UnsupportedReason);
        var bonus=Assert.IsType<RetailNpcStatSpellBonus.Program>(source.SpellBonus);
        Assert.Equal(RetailNpcStatTargetConditions.ParalyzeMask,bonus.StatusMask);
        using var h=BossAiHarness.For(220030000).Build();var player=h.SpawnPlayer();
        player.SetFlyController(new Aion.GameServer.Controllers.FlyController(player));
        var effect=new Effect(player,player,new SkillTemplate {skillId=910019},1);var paralyze=new ParalyzeEffect();
        bool Applies()=>bonus.AppliesTo(player.GetPlayerClass(),player.GetRace(),player.GetEffectController().RetailNpcTargetConditionMask);
        Assert.False(Applies());paralyze.StartEffect(effect);Assert.True(Applies());
        Assert.Equal(350,RetailNpcStatSpellBonus.Apply(100,bonus.ValueAtLevel(1),bonus.Percent,Applies(),0,1,(_,_)=>1).Damage);
        paralyze.EndEffect(effect);Assert.False(Applies());Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void Source17860StaysGatedForItsUnreviewedSecondary()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==17860);
        var source=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(asset.GetProperty("path").GetString()!),asset.GetProperty("sha256").GetString()!);
        Assert.False(source.Supported);Assert.Contains("sibling",source.UnsupportedReason!);
    }

    [SkippableFact]
    public void BoundDrainManaParalyzeRejectsChangedSourceFields()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrEmpty(path),"Requires pinned retail source package");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(path!));
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(a=>a.GetProperty("id").GetInt32()==18239);
        byte[] raw=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        Assert.True(RetailNpcStatSkillCombat.ParseSource(raw,asset.GetProperty("sha256").GetString()!).Supported);
        foreach(var (field,value) in new[]{("effect2_checktime",2999),("effect2_cond_preeffect_prob2",49),
            ("effect3_cond_preeffect_prob2",49),("effect3_noresist",1),("effect3_remain2",4000)})
        {
            var changed=XElement.Parse(Encoding.Unicode.GetString(raw));changed.SetElementValue(field,value);
            byte[] bytes=Encoding.Unicode.GetBytes(changed.ToString(SaveOptions.DisableFormatting));
            Assert.False(RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes))).Supported);
        }
    }

    [SkippableFact]
    public void SelectedRetailDrainRecordsPreserveIndependentSpellDamageAndHostShape()
    {
        string? manifestPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hostPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HOST_SKILLS");
        Skip.If(string.IsNullOrEmpty(manifestPath)||string.IsNullOrEmpty(hostPath),"Requires pinned sources and host templates");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        var hosts=XDocument.Load(hostPath!).Root!.Elements().ToDictionary(x=>(int)x.Attribute("skill_id")!);
        int[] expected=[16598,16599,16715,16865,17063,17247,17484,17485,17518,17558,17559,17842,17948,18237,18246,18609,19214];
        var rows=new List<object>();
        foreach(var a in manifest.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            int id=a.GetProperty("id").GetInt32();if(!expected.Contains(id))continue;
            byte[] raw=File.ReadAllBytes(a.GetProperty("path").GetString()!);
            var source=RetailNpcStatSkillCombat.ParseSource(raw,a.GetProperty("sha256").GetString()!);
            Assert.True(source.Supported,source.UnsupportedReason);Assert.Equal(RetailNpcStatSkillCombat.Kind.SpellDrain,source.Family);
            if(id==19214)Assert.False(source.ApplyCritical);
            var xml=XElement.Parse(Encoding.Unicode.GetString(raw));
            if(id==19214)
            {
                var missing=new XElement(xml);missing.Element("apply_magical_skill_boost_bonus")?.Remove();
                byte[] missingBytes=Encoding.Unicode.GetBytes(missing.ToString(SaveOptions.DisableFormatting));
                var missingFlags=RetailNpcStatSkillCombat.ParseSource(missingBytes,Convert.ToHexStringLower(SHA256.HashData(missingBytes)));
                Assert.True(missingFlags.Supported,missingFlags.UnsupportedReason);
                Assert.False(missingFlags.ApplyBoost);Assert.False(missingFlags.ApplyCritical);
                var explicitZero=new XElement(missing);explicitZero.SetElementValue("apply_magical_skill_boost_bonus",0);
                explicitZero.SetElementValue("apply_magical_critical",0);
                byte[] explicitBytes=Encoding.Unicode.GetBytes(explicitZero.ToString(SaveOptions.DisableFormatting));
                var explicitFlags=RetailNpcStatSkillCombat.ParseSource(explicitBytes,Convert.ToHexStringLower(SHA256.HashData(explicitBytes)));
                Assert.Equal(explicitFlags,missingFlags with {Sha256=explicitFlags.Sha256});
                missing.SetElementValue("apply_magical_critical","unknown");
                byte[] bad=Encoding.Unicode.GetBytes(missing.ToString(SaveOptions.DisableFormatting));
                Assert.False(RetailNpcStatSkillCombat.ParseSource(bad,Convert.ToHexStringLower(SHA256.HashData(bad))).Supported);
            }
            var zeroXml=new XElement(xml);zeroXml.SetElementValue("effect1_reserved15",0);
            byte[] zeroBytes=Encoding.Unicode.GetBytes(zeroXml.ToString(SaveOptions.DisableFormatting));
            var zeroSource=RetailNpcStatSkillCombat.ParseSource(zeroBytes,Convert.ToHexStringLower(SHA256.HashData(zeroBytes)));
            Assert.True(zeroSource.Supported,zeroSource.UnsupportedReason);Assert.Equal(0,zeroSource.Drain!.HpPercent);
            // Independent control preserves every damage operand, translating
            // only the native drain-specific bonus/condition field positions.
            string? mode=xml.Element("effect1_reserved5")?.Value;
            string? condition=xml.Element("effect1_reserved7")?.Value;
            // The independent ordinary-spell parser requires explicit flags;
            // insert the constructor-proved zeros in the control only.
            if(xml.Element("apply_magical_critical") is null)xml.SetElementValue("apply_magical_critical",0);
            if(xml.Element("apply_magical_skill_boost_bonus") is null)xml.SetElementValue("apply_magical_skill_boost_bonus",0);
            xml.SetElementValue("effect1_type","SpellATK_Instant");xml.SetElementValue("effect1_reserved3",0);
            xml.Element("effect1_reserved4")?.Remove();xml.Element("effect1_reserved5")?.Remove();
            xml.SetElementValue("effect1_reserved7",mode??"0");xml.Element("effect1_reserved14")?.Remove();
            xml.Element("effect1_reserved15")?.Remove();xml.Element("effect1_reserved16")?.Remove();
            if(condition is not null)xml.SetElementValue("effect1_reserved16",condition);
            xml.Element("effect1_reserved17")?.Remove();
            byte[] controlBytes=Encoding.Unicode.GetBytes(xml.ToString(SaveOptions.DisableFormatting));
            var control=RetailNpcStatSkillCombat.ParseSource(controlBytes,Convert.ToHexStringLower(SHA256.HashData(controlBytes)));
            Assert.True(control.Supported,control.UnsupportedReason);
            Assert.Equal(control,source with {Sha256=control.Sha256,Family=RetailNpcStatSkillCombat.Kind.SpellInstant,Drain=null});
            using var reader=hosts[id].CreateReader();var host=(SkillTemplate)new XmlSerializer(typeof(SkillTemplate),new XmlRootAttribute("skill_template")).Deserialize(reader)!;
            Assert.IsType<SpellAtkDrainInstantEffect>(host.GetEffectTemplate(1));
            if(source.Drain!.Mp is { } mp)
            {
                Assert.Equal(2,host.GetEffects()!.GetEffects().Count());
                var hostMp=Assert.IsType<MpAttackEffect>(host.GetEffectTemplate(2));
                Assert.True(RetailNpcStatSpellDrain.MpHostMatches(mp,hostMp));
                Assert.Equal(Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.TARGET,host.GetProperties()!.GetFirstTarget());
                hostMp.Value++;Assert.False(RetailNpcStatSpellDrain.MpHostMatches(mp,hostMp));hostMp.Value--;
                hostMp.PreEffectProb=50;Assert.False(RetailNpcStatSpellDrain.MpHostMatches(mp,hostMp));hostMp.PreEffectProb=100;
                foreach(var (field,value) in new[]{("effect2_reserved8",1),("effect2_reserved6",1),
                    ("effect2_cond_preeffect_prob2",50),("effect2_remain2",1000)})
                {
                    var wrongSource=XElement.Parse(Encoding.Unicode.GetString(raw));wrongSource.SetElementValue(field,value);
                    byte[] wrongBytes=Encoding.Unicode.GetBytes(wrongSource.ToString(SaveOptions.DisableFormatting));
                    Assert.False(RetailNpcStatSkillCombat.ParseSource(wrongBytes,Convert.ToHexStringLower(SHA256.HashData(wrongBytes))).Supported);
                }
            }
            else if(source.Drain.Stun is { } stun)
            {
                Assert.Equal(2,host.GetEffects()!.GetEffects().Count());
                var hostStun=Assert.IsType<StunEffect>(host.GetEffectTemplate(2));
                Assert.True(RetailNpcStatSpellDrain.StunHostMatches(stun,hostStun));
                Assert.Equal(Aion.GameServer.SkillEngine.Properties.FirstTargetAttribute.TARGET,host.GetProperties()!.GetFirstTarget());
                hostStun.NoResist=!hostStun.NoResist;Assert.False(RetailNpcStatSpellDrain.StunHostMatches(stun,hostStun));
                foreach(var (field,value) in new[]{("effect2_reserved1",1),("effect2_reserved16",0),
                    ("effect2_cond_preeffect_prob2",50),("effect2_remain2",1000)})
                {
                    var wrongSource=XElement.Parse(Encoding.Unicode.GetString(raw));wrongSource.SetElementValue(field,value);
                    byte[] wrongBytes=Encoding.Unicode.GetBytes(wrongSource.ToString(SaveOptions.DisableFormatting));
                    Assert.False(RetailNpcStatSkillCombat.ParseSource(wrongBytes,Convert.ToHexStringLower(SHA256.HashData(wrongBytes))).Supported);
                }
            }
            else Assert.Single(host.GetEffects()!.GetEffects());
            Assert.Equal(source.Element,host.GetEffectTemplate(1)!.GetElement());
            Assert.Equal(source.Drain!.Area?Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.AREA:Aion.GameServer.SkillEngine.Properties.TargetRangeAttribute.ONLYONE,host.GetProperties()!.GetTargetType());
            rows.Add(new {id,percent=source.Drain!.HpPercent,area=source.Drain.Area,host=true});
        }
        Assert.Equal(expected.Length,rows.Count);
        if(Environment.GetEnvironmentVariable("AION_RETAIL_STATS_DRAIN_OUTPUT") is {Length:>0} output)
        {using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write);JsonSerializer.Serialize(stream,new {rows});}
    }
}
