using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;
using System.Security.Cryptography;
using System.Text;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerShieldTests
{
    private sealed class UnknownShield : AttackCalcObserver
    {
        internal int Calls;
        public override void CheckShield(List<AttackResult> attacks,Effect effect,Creature attacker)=>Calls++;
    }

    [SkippableFact]
    public void SourceMatchedStoneSkinFamilyProtectsAllTwelveRanks()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_FAMILY_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_FAMILY_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked shield family package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        string manifest=File.ReadAllText(path!);const string marker="\"records\": [";
        int index=manifest.LastIndexOf(marker,StringComparison.Ordinal);Assert.True(index>=0);
        byte[] ambiguous=Encoding.UTF8.GetBytes(manifest[..index]+"\"records\": [], \"records\": ["+manifest[(index+marker.Length)..]);
        string ambiguousHash=Convert.ToHexStringLower(SHA256.HashData(ambiguous));
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatCatalog.InstallForTests(ambiguous,ambiguousHash,Path.GetDirectoryName(path!)!));
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213747,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000)]);
        player.GetLifeStats().SetCurrentHp(50000);
        var catalog=Assert.IsType<RetailNpcPlayerShieldCatalog>(RetailNpcStatCatalog.PlayerShieldSources);
        int[] pools=[709,857,1005,1153,1301,1449,1597,1745,1893,2041,2189,2277];
        var drainTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
        Effect NewHit()=>new(new Skill(drainTemplate,npc,37,player,null!),player);
        for(int i=0;i<pools.Length;i++)
        {
            int id=1155+i;var source=Assert.IsType<RetailNpcPlayerShieldSource>(catalog.Find(id));
            Assert.Equal((id,1,pools[i],pools[i],1000),(source.Id,source.Level,source.PerHit,source.InitialPool,source.Chance));
            var pending=NewHit();
            Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(pending,player,3000,AttackStatus.NORMALHIT,out var receipt));
            Assert.NotNull(receipt);Assert.Equal(3000,pending.GetReserveds(1).GetValue());
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var shield=Assert.IsType<ShieldEffect>(template.GetEffectTemplate(1));
            var effect=new Effect(player,player,template,1);effect.AddSuccessEffect(shield);shield.StartEffect(effect);
            Assert.True(player.GetObserveController().CanCalculateRetailPlayerDrain(player));
            lock(player.GetLifeStats())
            {
                using var permit=player.GetObserveController().EnterPlayerDrainHp(receipt,player);
            }
            Assert.False(effect.RetailHasEnded);
            var next=NewHit();
            Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(next,player,3000,AttackStatus.NORMALHIT,out _));
            Assert.Equal(3000-pools[i],next.GetReserveds(1).GetValue());
            Assert.Equal(ShieldType.NORMAL.GetId(),next.GetShieldDefense());Assert.True(effect.RetailHasEnded);
        }
        Assert.Null(catalog.Find(1154));Assert.Null(catalog.Find(1167));
        Assert.Throws<InvalidDataException>(()=>new RetailNpcPlayerShieldCatalog([catalog.Find(1155)!,catalog.Find(1155)!]));

        player.GetCommonData().SetLevel(37);npc.GetLifeStats().SetCurrentHp(100);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        var firstTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1155);
        var firstShield=Assert.IsType<ShieldEffect>(firstTemplate.GetEffectTemplate(1));
        var firstEffect=new Effect(player,player,firstTemplate,1);firstEffect.AddSuccessEffect(firstShield);firstShield.StartEffect(firstEffect);
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(drainTemplate.GetEffectTemplate(1));var incoming=NewHit();
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(incoming,primary,null,out bool success));Assert.True(success);
        int damage=incoming.GetReserveds(1).GetValue();Assert.True(damage>0);
        primary.ApplyEffect(incoming);Assert.False(incoming.PreparedNpcDrainInitialAborted);
        Assert.Equal(50000-damage,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.True(firstEffect.RetailHasEnded);

        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        var lateHit=NewHit();Assert.True(RetailNpcStatSkillCombat.TryCalculate(lateHit,primary,null,out bool lateSuccess));Assert.True(lateSuccess);
        int lateDamage=lateHit.GetReserveds(1).GetValue();Assert.True(lateDamage>0);
        var lastTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1166);
        var lastShield=Assert.IsType<ShieldEffect>(lastTemplate.GetEffectTemplate(1));
        var lateEffect=new Effect(player,player,lastTemplate,1);lateEffect.AddSuccessEffect(lastShield);lastShield.StartEffect(lateEffect);
        primary.ApplyEffect(lateHit);Assert.False(lateHit.PreparedNpcDrainInitialAborted);
        Assert.Equal(50000-lateDamage,player.GetLifeStats().GetCurrentHp());
        var probe=NewHit();Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,2277,AttackStatus.NORMALHIT,out _));
        Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.True(lateEffect.RetailHasEnded);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(lateDamage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void LateReviewedStoneSkinDoesNotCancelOrAbsorbAlreadyCalculatedNpcDrain()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213747,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        player.GetCommonData().SetLevel(37);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        var drainTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(drainTemplate.GetEffectTemplate(1));
        Effect NewHit()=>new(new Skill(drainTemplate,npc,37,player,null!),player);
        var incoming=NewHit();
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(incoming,primary,null,out bool success));Assert.True(success);
        int damage=incoming.GetReserveds(1).GetValue();Assert.True(damage>0);
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shield=Assert.IsType<ShieldEffect>(shieldTemplate.GetEffectTemplate(1));
        var lateShield=new Effect(player,player,shieldTemplate,1);lateShield.AddSuccessEffect(shield);shield.StartEffect(lateShield);
        primary.ApplyEffect(incoming);Assert.False(incoming.PreparedNpcDrainInitialAborted);
        Assert.Equal(50000-damage,player.GetLifeStats().GetCurrentHp());Assert.False(lateShield.RetailHasEnded);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        var probe=NewHit();
        Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(probe,player,2189,AttackStatus.NORMALHIT,out _));
        Assert.Equal(0,probe.GetReserveds(1).GetValue());Assert.True(lateShield.RetailHasEnded);

        var unknownHit=NewHit();Assert.True(RetailNpcStatSkillCombat.TryCalculate(unknownHit,primary,null,out bool next));Assert.True(next);
        int hp=player.GetLifeStats().GetCurrentHp();var unknown=new UnknownShield();
        player.GetObserveController().AddAttackCalcObserver(unknown);
        primary.ApplyEffect(unknownHit);Assert.True(unknownHit.PreparedNpcDrainInitialAborted);
        Assert.Equal(hp,player.GetLifeStats().GetCurrentHp());Assert.Equal(0,unknown.Calls);
        player.GetObserveController().RemoveAttackCalcObserver(unknown);Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void SourceBoundNpcLifeStealUsesProtectedDamageForHpAndHeal()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213747,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        player.GetCommonData().SetLevel(37);
        npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000),
            new StatSetFunction(StatEnum.MAGICAL_RESIST,0),new StatSetFunction(StatEnum.REGEN_HP,0)]);
        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shield=Assert.IsType<ShieldEffect>(shieldTemplate.GetEffectTemplate(1));
        var shieldEffect=new Effect(player,player,shieldTemplate,1);shieldEffect.AddSuccessEffect(shield);shield.StartEffect(shieldEffect);
        var drainTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
        var primary=Assert.IsType<SpellAtkDrainInstantEffect>(drainTemplate.GetEffectTemplate(1));
        var incoming=new Effect(new Skill(drainTemplate,npc,37,player,null!),player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(incoming,primary,null,out bool success));Assert.True(success);
        int damage=incoming.GetReserveds(1).GetValue();Assert.True(damage>0);
        Assert.Equal(ShieldType.NORMAL.GetId(),incoming.GetShieldDefense());Assert.True(shieldEffect.RetailHasEnded);
        primary.ApplyEffect(incoming);Assert.False(incoming.PreparedNpcDrainInitialAborted);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(50000-damage,player.GetLifeStats().GetCurrentHp());
        Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,100)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);

        player.GetLifeStats().SetCurrentHp(50000);npc.GetLifeStats().SetCurrentHp(100);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_DEFEND,30000)]);
        var fullShield=new Effect(player,player,shieldTemplate,1);fullShield.AddSuccessEffect(shield);shield.StartEffect(fullShield);
        var absorbed=new Effect(new Skill(drainTemplate,npc,37,player,null!),player);
        Assert.True(RetailNpcStatSkillCombat.TryCalculate(absorbed,primary,null,out bool absorbedSuccess));Assert.True(absorbedSuccess);
        Assert.Equal(0,absorbed.GetReserveds(1).GetValue());Assert.Equal(ShieldType.NORMAL.GetId(),absorbed.GetShieldDefense());
        primary.ApplyEffect(absorbed);Assert.False(absorbed.PreparedNpcDrainInitialAborted);
        h.Clock.Advance(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(50000,player.GetLifeStats().GetCurrentHp());Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);fullShield.EndEffect();
    }

    [SkippableFact]
    public void ReviewedStoneSkinConsumesItsRetailPoolForNpcDrainOnly()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_SHIELD_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires source-checked shield package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(213747,"retail_stat_base_fixture");
        var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        player.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAXHP,50000)]);
        player.GetLifeStats().SetCurrentHp(50000);
        var source=Assert.IsType<RetailNpcPlayerShieldSource>(RetailNpcStatCatalog.PlayerShieldSource);
        Assert.Equal((1165,1,2189,2189,1000),(source.Id,source.Level,source.PerHit,source.InitialPool,source.Chance));
        byte[] raw=File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path!)!,"skill-1165.utf16le.xml"));
        raw[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerShieldSource.Parse(raw,
            "724c891e2019445e316b24fd6af705e7e2f7e6d6fe345380886560cb99a2eb33"));
        var shieldTemplate=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shield=Assert.IsType<ShieldEffect>(shieldTemplate.GetEffectTemplate(1));
        var shieldEffect=new Effect(player,player,shieldTemplate,1);shieldEffect.AddSuccessEffect(shield);
        shield.StartEffect(shieldEffect);
        Assert.True(player.GetObserveController().CanCalculateRetailPlayerDrain(player));
        var unknown=new UnknownShield();player.GetObserveController().AddAttackCalcObserver(unknown);
        Assert.False(player.GetObserveController().CanCalculateRetailPlayerDrain(player));
        Assert.Equal(0,unknown.Calls);player.GetObserveController().RemoveAttackCalcObserver(unknown);
        var drainTemplate=DataManager.SKILL_DATA.GetSkillTemplate(17900);
        Effect NewHit()=>new(new Skill(drainTemplate,npc,37,player,null!),player);
        var first=NewHit();
        Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(first,player,1000,AttackStatus.NORMALHIT,out var receipt));
        Assert.NotNull(receipt);Assert.Equal(0,first.GetReserveds(1).GetValue());
        Assert.Equal(ShieldType.NORMAL.GetId(),first.GetShieldDefense());Assert.False(shieldEffect.RetailHasEnded);
        var second=NewHit();
        Assert.True(player.GetObserveController().TryCalculatePlayerRetailNpcDrain(second,player,1500,AttackStatus.NORMALHIT,out var secondReceipt));
        Assert.NotNull(secondReceipt);Assert.Equal(311,second.GetReserveds(1).GetValue());
        Assert.True(shieldEffect.RetailHasEnded);Assert.False(player.GetObserveController().HasDrainProtectionObserver);
        Assert.Equal(50000,player.GetLifeStats().GetCurrentHp());
        var wrongLevel=new Effect(player,player,shieldTemplate,2);wrongLevel.AddSuccessEffect(shield);shield.StartEffect(wrongLevel);
        Assert.False(player.GetObserveController().CanCalculateRetailPlayerDrain(player));wrongLevel.EndEffect();
    }

    [SkippableFact]
    public void MissingSourceDoesNotAdmitTheSameShield()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires prior retail package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220040000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220040000);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(1165);
        var shield=Assert.IsType<ShieldEffect>(template.GetEffectTemplate(1));
        var effect=new Effect(player,player,template,1);effect.AddSuccessEffect(shield);shield.StartEffect(effect);
        Assert.Null(RetailNpcStatCatalog.PlayerShieldSource);
        Assert.False(player.GetObserveController().CanCalculateRetailPlayerDrain(player));
        effect.EndEffect();
    }
}
