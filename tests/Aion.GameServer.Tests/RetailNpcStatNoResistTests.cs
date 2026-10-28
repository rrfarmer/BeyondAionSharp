using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatNoResistTests
{
    private const string Physical="<skill_base><id>910005</id><name>SyntheticNoResist</name>"
        +"<first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SkillATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>60</effect1_reserved2><effect1_noresist>1</effect1_noresist></skill_base>";
    private const string Spell="<skill_base><id>910006</id><name>SyntheticNoResistSpell</name>"
        +"<first_target>Target</first_target><target_range>OnlyOne</target_range><target_maxcount>1</target_maxcount><sub_type>Attack</sub_type>"
        +"<effect1_type>SpellATK_Instant</effect1_type><effect1_target_type>Target_Only</effect1_target_type>"
        +"<effect1_reserved2>100</effect1_reserved2><effect1_reserved6>0</effect1_reserved6><effect1_reserved10>Fire</effect1_reserved10><effect1_reserved11>100</effect1_reserved11>"
        +"<apply_magical_skill_boost_bonus>1</apply_magical_skill_boost_bonus><apply_magical_critical>0</apply_magical_critical>"
        +"<effect1_noresist>1</effect1_noresist><effect1_acc_mod2>500</effect1_acc_mod2></skill_base>";
    private static RetailNpcStatSkillCombat.Source Parse(string xml)
    {
        byte[] bytes=Encoding.Unicode.GetBytes(xml);
        return RetailNpcStatSkillCombat.ParseSource(bytes,Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    [Fact]
    public void SelectedNoResistAndBypassedSpellAccuracyArePreservedWithoutBroadeningOtherContexts()
    {
        var p=Parse(Physical);Assert.True(p.Supported,p.UnsupportedReason);Assert.True(p.NoResist);Assert.Equal(0,p.AccuracyFlat);
        var s=Parse(Spell);Assert.True(s.Supported,s.UnsupportedReason);Assert.True(s.NoResist);Assert.Equal(500,s.AccuracyFlat);
        Assert.False(Parse(Spell.Replace("<effect1_noresist>1</effect1_noresist>","<effect1_noresist>0</effect1_noresist>")).Supported);
        var named=Parse(Physical.Replace("</skill_base>","<effect1_acc_mod2>100</effect1_acc_mod2></skill_base>"));
        Assert.True(named.Supported);Assert.True(named.NoResist);Assert.Equal(100,named.AccuracyFlat);
        Assert.False(Parse(Physical.Replace("</skill_base>","<effect1_acc_mod1>1</effect1_acc_mod1></skill_base>")).Supported);
        foreach(int value in new[]{-1,2,int.MaxValue})
            Assert.False(Parse(Physical.Replace("<effect1_noresist>1</effect1_noresist>",$"<effect1_noresist>{value}</effect1_noresist>")).Supported);
    }

    [Fact]
    public void PreforcedRecordsAndUntranslatedCriticalOwnersRemainVisibleBeforeConsumption()
    {
        var source=Parse(Physical);Assert.True(RetailNpcStatSkillCombat.NoResistRecordSupported(source,AttackStatus.NORMALHIT));
        foreach(var status in new[]{AttackStatus.BLOCK,AttackStatus.PARRY,AttackStatus.DODGE,AttackStatus.RESIST,AttackStatus.CRITICAL})
            Assert.False(RetailNpcStatSkillCombat.NoResistRecordSupported(source,status));
        var controller=new ObserveController();var observer=new AttackerCriticalStatusObserver(AttackStatus.CRITICAL,2,100,true);
        controller.AddAttackCalcObserver(observer);Assert.False(RetailNpcStatSkillCombat.CriticalOwnerSupported(source,controller));
        Assert.Equal(2,observer.GetCount());
        Assert.True(RetailNpcStatSkillCombat.CriticalOwnerSupported(Parse(Spell),controller)); // Disabled magical critical never queries this owner.
        Assert.Equal(2,observer.GetCount());
    }

    [Fact]
    public void RangeCaptureRecordsOriginalRequestsAndRestoresNestedOrDisabledScopes()
    {
        using var outer=new RetailNpcStatSkillCombat.RangeTraceScope(true);
        Assert.Equal(7,RetailNpcStatSkillCombat.Draw(7,7)); // Equal endpoints do not advance the random engine.
        using(var off=new RetailNpcStatSkillCombat.RangeTraceScope(false))
        {Assert.Equal(8,RetailNpcStatSkillCombat.Draw(8,8));Assert.Null(off.Requests);}
        using(var child=new RetailNpcStatSkillCombat.RangeTraceScope(true))
        {Assert.Equal(9,RetailNpcStatSkillCombat.Draw(9,9));Assert.Equal(9,Assert.Single(child.Requests!).Value);}
        Assert.Equal(10,RetailNpcStatSkillCombat.Draw(10,10));
        Assert.Equal(new[]{7,10},outer.Requests!.Select(x=>x.Value));
    }

    private sealed class ForbiddenHitObserver : AttackCalcObserver
    {
        public int Queries;
        public override bool CheckStatus(AttackStatus status){Queries++;throw new Exception("NoResist queried target hit/defense observer");}
        public override bool CheckAttackerStatus(AttackStatus status){Queries++;throw new Exception("NoResist queried attacker hit observer");}
    }

    [SkippableFact]
    public void RealSourcePhysicalAndSpellBypassHitObserversAndPreservePreforcedRecords()
    {
        string? reviewPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_NORESIST_UNIT_CASES");
        Skip.If(string.IsNullOrEmpty(reviewPath),"Requires the external two-source NoResist fixture manifest");
        using var review=JsonDocument.Parse(File.ReadAllBytes(reviewPath!));var root=review.RootElement;
        string manifest=root.GetProperty("manifest").GetString()!,hash=root.GetProperty("sha256").GetString()!;
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(manifest),hash,Path.GetDirectoryName(manifest)!);
        foreach(var c in root.GetProperty("cases").EnumerateArray())
        {
            using var h=BossAiHarness.For(c.GetProperty("map").GetInt32()).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
            var npc=h.SpawnWithAi(c.GetProperty("npc").GetInt32(),"retail_stat_base_fixture");var player=h.SpawnPlayer();
            player.GetCommonData().SetExp(DataManager.PLAYER_EXPERIENCE_TABLE.GetStartExpForLevel(38));
            Assert.Equal(38,(int)player.GetLevel());
            var attacker=new ForbiddenHitObserver();var defender=new ForbiddenHitObserver();
            npc.GetObserveController().AddAttackCalcObserver(attacker);player.GetObserveController().AddAttackCalcObserver(defender);
            int id=c.GetProperty("skill").GetInt32(),level=c.GetProperty("level").GetInt32();
            var template=DataManager.SKILL_DATA.GetSkillTemplate(id);var part=template.GetEffectTemplate(1);
            var effect=new Effect(npc,player,template,level);
            Assert.Equal(AttackStatus.NORMALHIT,effect.GetAttackStatus());
            Assert.NotNull(part);Assert.True(RetailNpcStatSkillCombat.TryCalculate(effect,part,null,out bool success));Assert.True(success);
            Assert.True(effect.GetReserveds(1).GetValue()>0);Assert.Equal(0,attacker.Queries);Assert.Equal(0,defender.Queries);
            // Physical hit-state and magical periodic critical are separate native
            // caches. Only a spell primary may seed the magical cache for e2.
            Assert.Equal(part is Aion.GameServer.SkillEngine.Effects.SpellAttackInstantEffect&&template.IsApplyMagicalCritical(),
                effect.TryGetCalculatedMagicalCritical(out _));
            var forced=new Effect(npc,player,template,level);forced.SetAttackStatus(AttackStatus.BLOCK);
            using var draws=new RetailNpcStatSkillCombat.RangeTraceScope(true);
            Assert.False(RetailNpcStatSkillCombat.TryCalculate(forced,part,null,out _));
            Assert.Equal(AttackStatus.BLOCK,forced.GetAttackStatus());Assert.Empty(draws.Requests!);
            Assert.Equal(0,attacker.Queries);Assert.Equal(0,defender.Queries);
        }
    }

    [SkippableFact]
    public void CompleteSelectedSourceAdmissionMatchesTheIndependentNativeCensus()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires pinned external full09 source manifest");
        byte[] bytes=File.ReadAllBytes(path!);Assert.Equal(hash,Convert.ToHexStringLower(SHA256.HashData(bytes)));
        using var doc=JsonDocument.Parse(bytes);int physical=0,spell=0;
        foreach(var a in doc.RootElement.GetProperty("skill_sources").EnumerateArray())
        {
            var s=RetailNpcStatSkillCombat.ParseSource(File.ReadAllBytes(a.GetProperty("path").GetString()!),a.GetProperty("sha256").GetString()!);
            if(s.Supported){if(s.Family==RetailNpcStatSkillCombat.Kind.PhysicalInstant)physical++;else if(s.Family==RetailNpcStatSkillCombat.Kind.SpellInstant)spell++;}
            if(s.Id is 21508 or 21664 or 21792 or 21804)
            {Assert.True(s.Supported,s.UnsupportedReason);Assert.True(s.NoResist);Assert.Equal(s.Id==21804?2500:500,s.AccuracyFlat);}
        }
        Assert.Equal((RetailNpcStatReviewedSourceCensus.Physical,RetailNpcStatReviewedSourceCensus.Spell),(physical,spell));
    }
}
