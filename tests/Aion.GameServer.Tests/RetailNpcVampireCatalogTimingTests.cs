using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Xml.Linq;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.Model.Templates.Items.Enums;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireCatalogTimingTests
{
    private sealed class StatOwner : IStatOwner { }
    private sealed class FixturePlayerController : PlayerController
    {
        internal bool DeathReported;
        public override void OnDie(Creature lastAttacker) => DeathReported = true;
    }

    [Fact]
    public void TimingContextRejectsDuplicateJsonFields()
    {
        byte[] duplicate=System.Text.Encoding.UTF8.GetBytes("{\"schema_version\":1,\"schema_version\":1}");
        Assert.Throws<InvalidDataException>(()=>RetailNpcStatSkillTimingSources.Parse(duplicate,
            Convert.ToHexStringLower(SHA256.HashData(duplicate)),new Dictionary<(int,int),RetailNpcStatCatalog.Member>(),
            new Dictionary<int,RetailNpcStatSkillCombat.Source>(),_=>[]));
    }

    [SkippableFact]
    public void ExistingPackageDoesNotInferUnconfiguredTiming()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash),"Requires original retail package");
        using var active=RetailNpcStatCatalog.InstallForTests(File.ReadAllBytes(path!),hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");
        Assert.True(RetailNpcStatCatalog.TryGet(npc,out _));
        Assert.False(RetailNpcStatCatalog.TryGetSkillTiming(npc,16596,38,out _));
        Assert.False(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,16596,38,out _));
    }

    [SkippableFact]
    public void AlteredTimingReportPinRejectsTheWholeCandidatePackage()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_MANIFEST");
        string? expected=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_SHA256");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(expected),"Requires candidate timing package");
        byte[] raw=File.ReadAllBytes(path!);
        Assert.Equal(expected,Convert.ToHexStringLower(SHA256.HashData(raw)));
        var manifest=JsonNode.Parse(raw)!;
        string contextPath=manifest["skill_timing_context"]!["path"]!.GetValue<string>();
        var context=JsonNode.Parse(File.ReadAllBytes(contextPath))!;
        context["records"]![0]!["report"]!["sha256"]=new string('0',64);
        byte[] altered=System.Text.Encoding.UTF8.GetBytes(context.ToJsonString());
        string temp=Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp,altered);
            manifest["skill_timing_context"]!["path"]=temp;
            manifest["skill_timing_context"]!["sha256"]=Convert.ToHexStringLower(SHA256.HashData(altered));
            byte[] candidate=System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
            Assert.Throws<InvalidDataException>(()=>RetailNpcStatCatalog.InstallForTests(candidate,
                Convert.ToHexStringLower(SHA256.HashData(candidate)),Path.GetDirectoryName(path!)!));
        }
        finally {File.Delete(temp);}
    }

    [SkippableTheory]
    [InlineData(16596,214435,38,100,491,25,false,"vampire-ordered-impact-01/motion-16596.json",false)]
    [InlineData(18459,214583,41,130,700,0,false,"vampire-ordered-impact-01/motion-18459.json",false)]
    [InlineData(18460,214583,41,100,700,25,true,"vampire-ordered-impact-01/motion-18460.json",false)]
    [InlineData(16596,214435,38,100,491,25,false,"vampire-ordered-impact-01/motion-16596.json",true)]
    [InlineData(18459,214583,41,130,700,0,false,"vampire-ordered-impact-01/motion-18459.json",true)]
    [InlineData(18460,214583,41,100,700,25,true,"vampire-ordered-impact-01/motion-18460.json",true)]
    public void CatalogSelectedMarkerDrivesNpcSkillStart(int skillId,int npcId,int level,int healPercent,
        int word,int hostAmmo,bool lethal,string markerFile,bool joinedInputs)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_SHA256");
        string? root=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TIMING_TEST_ROOT");
        Skip.If(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(hash)||string.IsNullOrEmpty(root),"Requires pinned retail package and marker reports");
        byte[] manifestBytes=File.ReadAllBytes(path!);using var manifest=JsonDocument.Parse(manifestBytes);
        var asset=manifest.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==skillId);
        byte[] skillSource=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        var native=XDocument.Parse(System.Text.Encoding.Unicode.GetString(skillSource)).Root!;
        Assert.Null(native.Element("ammo_speed"));Assert.Null(native.Element("pre_fx_delay"));
        var candidate=RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId,skillSource);
        byte[] markerBytes=File.ReadAllBytes(Path.Combine(root!,markerFile));
        using var marker=JsonDocument.Parse(markerBytes);
        Assert.Equal(word,marker.RootElement.GetProperty("markers").EnumerateArray().Single().GetProperty("hit_word_ms").GetInt32());
        using var active=RetailNpcStatCatalog.InstallForTests(manifestBytes,hash!,Path.GetDirectoryName(path!)!);
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var npc=h.SpawnWithAi(npcId,"retail_stat_base_fixture");var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        FixturePlayerController? controller=null;
        if(lethal){controller=new();controller.SetOwner(player);typeof(VisibleObject).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(player,controller);}
        player.GetCommonData().SetPlayerClass(PlayerClass.PRIEST);player.GetCommonData().SetLevel(lethal?38:65);
        player.GetGameStats().UpdateStatsTemplate();PlayerStatFunctions.AddPredefinedStatFunctions(player);player.GetLifeStats().OnStatsChange(null!);
        player.GetLifeStats().SetCurrentHp(player.GetLifeStats().GetMaxHp());player.GetLifeStats().SetCurrentMp(player.GetLifeStats().GetMaxMp());
        npc.GetLifeStats().SetCurrentHp(100);npc.GetGameStats().AddEffectOnly(null!,[new StatSetFunction(StatEnum.MAGICAL_ACCURACY,10000)]);
        BossAiHarness.MakeMutuallyKnown(npc,player);
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCaptureRetailInitialForUnmodifiedHost(player,out var primary));Assert.NotNull(primary);
        var equipment=RetailNpcCurseEquipmentObservation.Capture(player);Assert.Empty(equipment.Items);
        int hostHp=player.GetLifeStats().GetMaxHp(),hostMp=player.GetLifeStats().GetMaxMp();
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,primary.HpBasis,hostHp,-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,hostMp,-35);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,skillId,level,player);Assert.NotNull(skill);
        Assert.Equal(hostAmmo,skill.GetSkillTemplate().GetAmmoSpeed());skill.GetEffectedList().Add(player);
        var probe=new Effect(skill,player);
        Assert.True(RetailNpcCurseResourcePlanBuilder.HasUnmodifiedResourceInputs(player,probe));
        var unknown=new StatOwner();
        player.GetGameStats().AddEffectOnly(unknown,[new StatRateFunction(StatEnum.MAXHP,10,true)]);
        Assert.False(RetailNpcCurseResourcePlanBuilder.HasUnmodifiedResourceInputs(player,probe));
        player.GetGameStats().EndEffect(unknown);
        Assert.True(RetailNpcCurseResourcePlanBuilder.HasUnmodifiedResourceInputs(player,probe));
        if(skillId==16596)
        {
            var slots=Assert.IsType<SortedDictionary<long,Item>>(typeof(Equipment)
                .GetField("equipment",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(player.GetEquipment()));
            long main=ItemSlot.MAIN_HAND.GetSlotIdMask();
            slots.Add(main,new Item(900999,new ItemTemplate {itemId=999999,itemGroup=ItemGroup.SWORD},1,true,main));
            Assert.False(RetailNpcCurseResourcePlanBuilder.TryBuildPlainFromCatalog(player,probe,out _,out var reason));
            Assert.Equal(RetailNpcCurseResourcePlanBuilder.Failure.EquipmentUnsupported,reason);
            slots.Remove(main);
        }
        RetailNpcCurseResourceOwner.Plan? Plan(Player p,Effect e)
        {
            if(!RetailNpcCurseResourcePlanBuilder.TryBuildPlainFromCatalog(p,e,out var plan,out var failure))
                throw new InvalidOperationException($"Plain resource plan rejected: {failure}");
            return plan;
        }
        Assert.True(RetailNpcStatCatalog.TryGetSkillTiming(npc,skillId,level,out var selected));
        Assert.Equal(word,selected.Word);
        Assert.False(RetailNpcStatCatalog.TryGetSkillTiming(npc,skillId,level+1,out _));
        Assert.False(RetailNpcStatCatalog.TryGetSkillTiming(npc,1,level,out _));
        RetailNpcVampirePlayerInputs? joined=null;
        if(joinedInputs)
        {
            Assert.True(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,skillId,level,out var selectedSource));
            Assert.Equal(candidate.FullSourceSha256,selectedSource.FullSourceSha256);
            Assert.False(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,skillId,level+1,out _));
            Assert.False(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,1,level,out _));
            int otherSkill=skillId==16596?18459:16596,otherLevel=skillId==16596?41:38;
            Assert.False(RetailNpcStatCatalog.TryGetReviewedVampireProjection(npc,otherSkill,otherLevel,out _));
            var changedSource=(byte[])skillSource.Clone();changedSource[0]^=1;
            Assert.Throws<InvalidDataException>(()=>RetailNpcVampirePrimaryProjection.ReadForOfflineFixture(skillId,changedSource));
            joined=RetailNpcVampireCastFixture.AttachSelectedInputsForOfflineFixture(skill,(_,hi)=>hi);
        }
        else RetailNpcVampireCastFixture.AttachFromCatalogForOfflineFixture(skill,candidate,Plan,(_,hi)=>hi);
        int castMs=(int)MathF.Floor(skill.GetSkillTemplate().GetDuration()*(npc.GetGameStats().GetCastSpeed()/1000f)+0.5f);
        Assert.True(castMs>0);int initialHp=player.GetLifeStats().GetCurrentHp();
        Assert.True(skill.UseWithoutPropSkill());Assert.Equal(word,skill.GetHitTime());
        h.Clock.Advance(TimeSpan.FromMilliseconds(castMs-1));Assert.Null(RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill));
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));var effect=RetailNpcVampireCastFixture.ObservedEffectForOfflineFixture(skill);Assert.NotNull(effect);
        Assert.True(effect.IsInSuccessEffects(2));int damage=effect.GetReserveds(1).GetValue();Assert.Equal(lethal,damage>=initialHp);
        Assert.Equal(initialHp,player.GetLifeStats().GetCurrentHp());int impact=Math.Max(word-50,100);
        h.Clock.Advance(TimeSpan.FromMilliseconds(impact-1));Assert.Equal(initialHp,player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(Math.Max(0,initialHp-damage),player.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(word-impact));
        if(joined is not null&&!lethal)Assert.NotNull(joined.PreparedPlan);
        Assert.Equal(!lethal,player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        if(lethal)Assert.True(controller!.DeathReported);
        else {Assert.Equal(newHp,player.GetLifeStats().GetMaxHp());Assert.Equal(newMp,player.GetLifeStats().GetMaxMp());}
        effect.EndEffect();
        if(!lethal){Assert.Equal(hostHp,player.GetLifeStats().GetMaxHp());Assert.Equal(hostMp,player.GetLifeStats().GetMaxMp());}
        h.Clock.Advance(TimeSpan.FromMilliseconds(999));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,healPercent)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}
