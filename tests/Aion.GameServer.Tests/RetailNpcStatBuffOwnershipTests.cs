using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcStatBuffOwnershipTests
{
    private sealed class OtherOwner:IStatOwner;
    private static RetailNpcPlayerResistanceCapture.Catalog PlayerResistanceCatalog()
    {
        string? directory=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_PLAYER_RESISTANCE_CATALOG");
        Skip.If(string.IsNullOrWhiteSpace(directory),"Requires the pinned 5.8 player resistance catalog");
        return RetailNpcPlayerResistanceCapture.Catalog.LoadForOfflineFixture(
            File.ReadAllBytes(Path.Combine(directory!,"catalog.json")),
            "bb44e35a26fb715b8f12a3d26b0b3cd6c53e0aecca5035e6162ba9cf90b4a990",directory!);
    }

    [SkippableFact]
    public void AvatarResistanceRequiresBothReviewedParentAndChild()
    {
        var catalog=PlayerResistanceCatalog();
        string? directory=Environment.GetEnvironmentVariable("AION_RETAIL_AVATAR_RESISTANCE_CATALOG");
        Skip.If(string.IsNullOrWhiteSpace(directory),"Requires the pinned avatar resistance catalog");
        var bytes=File.ReadAllBytes(Path.Combine(directory!,"catalog.json"));
        const string hash="739c439fea8ec195c5762164641d18a6137509976a1901e904dccf1affa0919e";
        var changed=(byte[])bytes.Clone();changed[0]^=1;
        Assert.Throws<InvalidDataException>(()=>catalog.WithLinkedForOfflineFixture(changed,hash,directory!));
        catalog=catalog.WithLinkedForOfflineFixture(bytes,hash,directory!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        for(int id=11885;id<=11894;id++)
        {
            int rank=(id-11885)%5+1,childId=id<11890?11912+rank-1:11907+rank-1;
            var parentTemplate=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var launch=Assert.IsType<Aion.GameServer.SkillEngine.Effects.SkillLauncherEffect>(parentTemplate.GetEffectTemplate(1));
            var shape=Assert.IsType<Aion.GameServer.SkillEngine.Effects.ShapeChangeEffect>(parentTemplate.GetEffectTemplate(2));
            var parent=new Effect(player,player,parentTemplate,rank);
            parent.AddSuccessEffect(launch);parent.AddSuccessEffect(shape);parent.ApplyEffect();
            var child=Assert.Single(player.GetEffectController().GetAllEffects(),e=>e.GetSkillId()==childId);
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var active));
            Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,100000),active);
            Assert.Equal(9999,RetailNpcStatSpellDrain.CalculateCurseResistance(new(0,default,active,default,default)));
            parent.EndEffect();
            Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
            child.EndEffect();
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var removed));
            Assert.Equal(default,removed);
        }
        var childTemplate=DataManager.SKILL_DATA.GetSkillTemplate(11912);
        var childStat=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(childTemplate.GetEffectTemplate(4));
        var orphan=new Effect(player,player,childTemplate,1);orphan.AddSuccessEffect(childStat);orphan.ApplyEffect();
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
        orphan.EndEffect();
        var parentTemplateAgain=DataManager.SKILL_DATA.GetSkillTemplate(11885);
        var launcher=Assert.IsType<Aion.GameServer.SkillEngine.Effects.SkillLauncherEffect>(parentTemplateAgain.GetEffectTemplate(1));
        var transform=Assert.IsType<Aion.GameServer.SkillEngine.Effects.ShapeChangeEffect>(parentTemplateAgain.GetEffectTemplate(2));
        var parentAgain=new Effect(player,player,parentTemplateAgain,1);
        parentAgain.AddSuccessEffect(launcher);parentAgain.AddSuccessEffect(transform);parentAgain.ApplyEffect();
        var childAgain=Assert.Single(player.GetEffectController().GetAllEffects(),e=>e.GetSkillId()==11912);
        childAgain.EndEffect();
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
        parentAgain.EndEffect();
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var final));
        Assert.Equal(default,final);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void RetailOnlyProtectionResistanceFollowsReviewedActiveEffect()
    {
        var catalog=PlayerResistanceCatalog();
        string? directory=Environment.GetEnvironmentVariable("AION_RETAIL_PROTECTION_RESISTANCE_CATALOG");
        Skip.If(string.IsNullOrWhiteSpace(directory),"Requires the pinned retail-only protection catalog");
        var bytes=File.ReadAllBytes(Path.Combine(directory!,"catalog.json"));
        const string hash="a7e2ab26a3df5b1bdbf375fc1f79d52bfdc81c72a2d0a162861b0b1540ee0e1d";
        var changed=(byte[])bytes.Clone();changed[0]^=1;
        Assert.Throws<InvalidDataException>(()=>catalog.WithRetailOnlyForOfflineFixture(changed,hash,directory!));
        catalog=catalog.WithRetailOnlyForOfflineFixture(bytes,hash,directory!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        foreach(var (id,rank,value) in new[]{(8874,8,200),(8875,9,500),(8883,8,200),(8884,9,500)})
        {
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(2));
            var effect=new Effect(player,player,skill,rank);effect.AddSuccessEffect(statup);
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var before));
            Assert.Equal(default,before);
            effect.ApplyEffect();
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var active));
            Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,value),active);
            Assert.Equal(value,RetailNpcStatSpellDrain.CalculateCurseResistance(new(0,default,active,default,default)));
            player.GetGameStats().EndEffect(effect);
            Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
            effect.EndEffect();
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var after));
            Assert.Equal(default,after);
        }
        Effect StartProtection(int id,int rank)
        {
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(2));
            var effect=new Effect(player,player,skill,rank);effect.AddSuccessEffect(statup);effect.ApplyEffect();return effect;
        }
        var old=StartProtection(8874,8);var replacement=StartProtection(8875,9);
        Assert.DoesNotContain(old,player.GetEffectController().GetAllEffects());
        Assert.Contains(replacement,player.GetEffectController().GetAllEffects());
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var replaced));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,500),replaced);
        replacement.EndEffect();
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var removed));
        Assert.Equal(default,removed);
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActivePlayerResistanceModifiersAreCombinedOnlyWhenEveryOwnerIsReviewed()
    {
        var catalog=PlayerResistanceCatalog();
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        Effect Start(int id,int position)
        {
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(position));
            var effect=new Effect(player,player,skill,1);effect.AddSuccessEffect(statup);effect.ApplyEffect();return effect;
        }
        var flat=Start(2767,3);var percent=Start(20243,1);
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var both));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(100,100),both);
        Assert.Equal(110,RetailNpcStatSpellDrain.CalculateCurseResistance(new(5,default,both,default,default)));
        var unknown=new OtherOwner();
        player.GetGameStats().AddEffectOnly(unknown,[new StatAddFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,20,true)]);
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
        player.GetGameStats().EndEffect(unknown);percent.EndEffect();
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var remaining));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,100),remaining);
        // An active source-reviewed effect without its corresponding host
        // stat function must not be silently interpreted as zero resistance.
        player.GetGameStats().EndEffect(flat);
        Assert.Contains(flat,player.GetEffectController().GetAllEffects());
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out _));
        flat.EndEffect();
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureGeneralForOfflineFixture(player,catalog,out var none));
        Assert.Equal(default,none);Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ReviewedPlayerCatalogCapturesFlatAndPercentageResistanceEffects()
    {
        var catalog=PlayerResistanceCatalog();
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        foreach(var (id,position,expected) in new[]
        {
            (3168,2,new RetailNpcStatSpellDrain.CurseModifiers(0,10000)),
            (20243,1,new RetailNpcStatSpellDrain.CurseModifiers(100,0))
        })
        {
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(position));
            var effect=new Effect(player,player,skill,1);effect.AddSuccessEffect(statup);effect.ApplyEffect();
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out var modifier));
            Assert.Equal(expected,modifier);effect.EndEffect();
            Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void AllTwelvePlayerSummonArmorRanksUseTheirMatchingRetailResistanceSource()
    {
        var catalog=PlayerResistanceCatalog();
        int[] values=[100,110,120,130,140,150,160,170,180,190,195,200];
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        for(int index=0;index<values.Length;index++)
        {
            int id=2767+index,rank=index+1;
            var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(3));
            var effect=new Effect(player,player,skill,rank);effect.AddSuccessEffect(statup);effect.ApplyEffect();
            Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out var modifier));
            Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,values[index]),modifier);
            effect.EndEffect();
            Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        }
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void ActivePlayerBuffProvidesSourceBoundGeneralResistanceOnlyWhileOwned()
    {
        var catalog=PlayerResistanceCatalog();
        string directory=Environment.GetEnvironmentVariable("AION_RETAIL_CURSE_PLAYER_RESISTANCE_CATALOG")!;
        var changedCatalog=File.ReadAllBytes(Path.Combine(directory,"catalog.json"));changedCatalog[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerResistanceCapture.Catalog.LoadForOfflineFixture(
            changedCatalog,"bb44e35a26fb715b8f12a3d26b0b3cd6c53e0aecca5035e6162ba9cf90b4a990",directory));
        var changedSource=File.ReadAllBytes(Path.Combine(directory,"skill-2767.utf16le.xml"));changedSource[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatBuffs.ParsePlayerResistance(changedSource,
            "0155bee5a323c47ad0e012e98d85fb4f2dcb195c9ad9cf206cef60dc1376f1bd"));
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var skill=DataManager.SKILL_DATA.GetSkillTemplate(2767);
        var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(skill.GetEffectTemplate(3));
        var effect=new Effect(player,player,skill,1);effect.AddSuccessEffect(statup);
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        effect.ApplyEffect();
        Assert.Contains(effect,player.GetEffectController().GetAllEffects());
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out var general));
        Assert.Equal(new RetailNpcStatSpellDrain.CurseModifiers(0,100),general);
        Assert.Equal(100,RetailNpcStatSpellDrain.CalculateCurseResistance(new(0,default,general,default,default)));
        var other=new OtherOwner();
        player.GetGameStats().AddEffectOnly(other,[new StatAddFunction(StatEnum.ABNORMAL_RESISTANCE_ALL,20,true)]);
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        player.GetGameStats().EndEffect(other);
        Assert.True(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        effect.EndEffect();
        Assert.False(RetailNpcPlayerResistanceCapture.TryCaptureForOfflineFixture(player,effect,catalog,out _));
        Assert.Empty(h.Clock.Faults);
    }

    [SkippableFact]
    public void HaramelMindControlAddsOneHpMatchingTheSelectedNativeProgram()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_SHA256");
        string? source=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HARAMEL_HP_SOURCE");
        string? sourceHash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_HARAMEL_HP_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(source)||string.IsNullOrEmpty(sourceHash),
            "Requires the pinned external cohort and exact reviewed Mind Control source");
        var parsed=RetailNpcStatBuffs.Parse(File.ReadAllBytes(source!),sourceHash!);
        Assert.Equal(19226,parsed.Id);Assert.Equal("BN_Charmed_IDNovice",parsed.Name);
        var program=Assert.Single(parsed.Programs);Assert.True(program.Supported);
        var part=Assert.Single(program.Parts);Assert.Equal(StatEnum.MAXHP,part.Stat);Assert.False(part.Percent);
        Assert.Equal(1,RetailNpcStatBuffs.Value(part,11));
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(300200000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        foreach(int id in new[]{216920,216921,217067})
        {
            var npc=h.SpawnWithAi(id,"retail_stat_base_fixture");
            Assert.Equal(1,npc.GetGameStats().StatisticsProfile!.MaxHp);
            Assert.Equal(1,npc.GetLifeStats().GetCurrentHp());Assert.Equal(1,npc.GetLifeStats().GetMaxHp());
            var template=DataManager.SKILL_DATA.GetSkillTemplate(19226);
            var statup=Assert.IsType<Aion.GameServer.SkillEngine.Effects.StatupEffect>(template.GetEffectTemplate(1));
            var effect=new Effect(npc,npc,template,11);effect.AddSuccessEffect(statup);effect.ApplyEffect();
            bool sourceBound=RetailNpcStatCatalog.TryGetSkill(npc.GetGameStats().StatisticsProfile!,19226,11,out _);
            Assert.Equal(sourceBound,RetailNpcStatBuffs.HasSelectedProgram(effect)); // Full08 compatibility and reindexed source both add exactly1.
            Assert.Contains(effect,npc.GetEffectController().GetAllEffects());
            Assert.Equal(2,npc.GetLifeStats().GetMaxHp());Assert.Equal(2,npc.GetLifeStats().GetCurrentHp());
            var modifier=Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.MAXHP));
            Assert.Equal(RetailNpcStatBuffs.Value(part,effect.GetSkillLevel()),modifier.GetValue());
            effect.EndEffect();Assert.Equal(1,npc.GetLifeStats().GetMaxHp());Assert.Equal(1,npc.GetLifeStats().GetCurrentHp());
        }
        Assert.Empty(h.Clock.Faults);
    }
    [SkippableFact]
    public void GrayManeTriggeredDefenseAlreadyMatchesTheSelectedRetailChildProgram()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_SHA256");
        string? childPath=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_GUARDIAN_CHILD_SOURCE");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(childPath),"Requires pinned retail parent/cohort and extracted child evidence");
        var childSource=RetailNpcStatBuffs.Parse(File.ReadAllBytes(childPath!),"1142a4c01ee6bcbf0e2f7f7f88d71fcc24d8da72ad54fe9e7bfc61374da1e454");
        Assert.Equal(16393,childSource.Id);Assert.Equal("NKN_GuardianR_Nr",childSource.Name);
        var program=Assert.Single(childSource.Programs);Assert.True(program.Supported);
        var part=Assert.Single(program.Parts);Assert.Equal(StatEnum.PHYSICAL_DEFENSE,part.Stat);Assert.True(part.Percent);
        Assert.Equal(70,RetailNpcStatBuffs.Value(part,1));
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220010000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(210407,"retail_stat_base_fixture");var player=h.SpawnPlayer();
        var profile=npc.GetGameStats().StatisticsProfile!;
        Assert.True(RetailNpcStatCatalog.TryGetSkill(profile,16394,1,out var bound));Assert.Equal("NKN_GuardianSS_Nr",bound.Name);
        int basis=npc.GetGameStats().GetPDef().GetCurrent();
        var parentSkill=DataManager.SKILL_DATA.GetSkillTemplate(16394);
        var trigger=Assert.IsType<Aion.GameServer.SkillEngine.Effects.ProvokerEffect>(parentSkill.GetEffectTemplate(1));
        Assert.Equal(16393,trigger.skillId);
        var parent=new Effect(npc,npc,parentSkill,1);parent.AddSuccessEffect(trigger);parent.ApplyEffect();
        npc.GetObserveController().NotifyAttackedObservers(player,0);
        var child=Assert.Single(npc.GetEffectController().GetAllEffects(),x=>x.GetSkillId()==16393);
        // Older packages lack the transitive child; full11 and later bind it.
        // Both paths must retain the same reviewed numeric result.
        bool childBound=RetailNpcStatCatalog.TryGetBuff(profile,16393,child.GetSkillLevel(),out var selectedChild,out var lineage,
            link=>RetailNpcStatTriggeredBuffs.MatchesHost(link,DataManager.SKILL_DATA.GetSkillTemplate(link.ParentId)?.GetEffectTemplate(link.Position)));
        Assert.Equal(childBound,RetailNpcStatBuffs.HasSelectedProgram(child));
        if(childBound)
        {
            Assert.Equal(childSource.Sha256,selectedChild.Sha256);
            Assert.Contains(lineage,link=>link.ParentId==16394 && link.ChildId==16393);
        }
        var modifier=Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.PHYSICAL_DEFENSE));
        Assert.Equal(RetailNpcStatBuffs.Value(part,child.GetSkillLevel()),modifier.GetValue());
        long raw=profile.ScalarInputs.Fields["physical_defend"].Value;
        Assert.Equal((int)RetailNpcStatDefenses.Calculate(raw,70,0,0,500000),npc.GetGameStats().GetPDef().GetCurrent());
        child.EndEffect();Assert.Equal(basis,npc.GetGameStats().GetPDef().GetCurrent());parent.EndEffect();Assert.Empty(h.Clock.Faults);
    }
    [SkippableTheory]
    [InlineData(256681,16405)]
    [InlineData(282996,20536)]
    public void ActiveBuffRestoresHostChannelsOnTargetRetirementAndKeepsOtherOwners(int npcId,int id)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_LEVEL_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires the full external statistics package");
        using var installed=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        var member=RetailNpcStatCatalog.Members.First(m=>m.MapId!=0&&m.Profile.NpcId==npcId);
        using var h=BossAiHarness.For(member.MapId).Build();
        var npc=h.SpawnWithAi(npcId,"aggressive");var skill=DataManager.SKILL_DATA.GetSkillTemplate(id);
        var entry=Enumerable.Range(0,npc.GetSkillList().Size()).Select(i=>npc.GetSkillList().GetSkillOnPosition(i)).First(s=>s.GetSkillId()==id);
        var effect=new Effect(npc,npc,skill,entry.GetSkillLevel());effect.AddSuccessEffect(skill.GetEffectTemplate(1));effect.ApplyEffect();
        Assert.True(RetailNpcStatBuffs.HasSelectedProgram(effect));
        Assert.NotEmpty(npc.GetGameStats().GetStatsSorted(StatEnum.LIGHT_RESISTANCE));
        var other=new OtherOwner();npc.GetGameStats().AddEffectOnly(other,[new StatAddFunction(StatEnum.PARRY,123,true)]);
        npc.SetCreatorId(42);
        Assert.Null(npc.GetGameStats().StatisticsProfile);Assert.Contains(effect,npc.GetEffectController().GetAllEffects());
        Assert.Empty(npc.GetGameStats().GetStatsSorted(StatEnum.LIGHT_RESISTANCE));
        if(id==16405)
        {
            var restored=Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.MAGICAL_RESIST));
            Assert.Equal(50,restored.GetValue());Assert.Same(effect,restored.GetOwner());
        }
        effect.EndEffect();
        Assert.Empty(npc.GetGameStats().GetStatsSorted(StatEnum.MAGICAL_RESIST));
        Assert.Same(other,Assert.Single(npc.GetGameStats().GetStatsSorted(StatEnum.PARRY)).GetOwner());
        Assert.Empty(h.Clock.Faults);
    }
}
