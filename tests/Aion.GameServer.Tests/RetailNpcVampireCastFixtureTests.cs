using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcVampireCastFixtureTests
{
    private sealed class FixturePlayerController : PlayerController
    {
        internal bool DeathReported;
        public override void OnDie(Creature lastAttacker) => DeathReported = true;
    }

    [SkippableTheory]
    [InlineData(16596,214435,38,100,491,25,false,"vampire-ordered-impact-01/motion-16596.json")]
    [InlineData(18459,214583,41,130,700,0,false,"vampire-ordered-impact-01/motion-18459.json")]
    [InlineData(18460,214583,41,100,700,25,true,"vampire-ordered-impact-01/motion-18460.json")]
    public void NpcSkillStartCarriesSelectedMarkerIntoDamageCurseAndHeal(int skillId,int npcId,int level,int healPercent,
        int word,int hostAmmo,bool lethal,string markerFile)
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        string? hash=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_SHA256");
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
        Assert.True(RetailNpcCursePlayerPrimarySnapshot.TryCapture(player,out var primary));Assert.NotNull(primary);
        var equipment=RetailNpcCurseEquipmentObservation.Capture(player);Assert.Empty(equipment.Items);
        var equipmentWitness=RetailNpcCurseEquipmentObservation.EquipmentWitness(player,equipment,RetailNpcCurseEquipmentObservation.Capture);
        int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,primary.HpBasis,primary.HpBasis,-35);
        int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,primary.MpBasis,primary.MpBasis,-35);
        var skill=Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(npc,skillId,level,player);Assert.NotNull(skill);
        Assert.Equal(hostAmmo,skill.GetSkillTemplate().GetAmmoSpeed());skill.GetEffectedList().Add(player);
        RetailNpcCurseResourceOwner.Plan? Plan(Player p,Effect e)
        {
            var witness=RetailNpcCurseStatWitness.Create(p,e,q=>equipmentWitness(q)&&primary.HasEquivalentInputs(q),
                newHp-primary.HpBasis,newMp-primary.MpBasis);
            return new(primary.HpBasis,primary.HpBasis,0,primary.MpBasis,primary.MpBasis,0,
                witness.Before,witness.After,witness.CommitStamp);
        }
        var altered=JsonNode.Parse(markerBytes)!;altered["markers"]![0]!["hit_word_ms"]=word+1;
        byte[] alteredBytes=System.Text.Encoding.UTF8.GetBytes(altered.ToJsonString());
        Assert.Throws<InvalidDataException>(()=>RetailNpcVampireCastFixture.AttachForOfflineFixture(skill,candidate,alteredBytes,
            Convert.ToHexStringLower(SHA256.HashData(alteredBytes)),Plan,(_,hi)=>hi));
        RetailNpcVampireCastFixture.AttachForOfflineFixture(skill,candidate,markerBytes,
            Convert.ToHexStringLower(SHA256.HashData(markerBytes)),Plan,(_,hi)=>hi);
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
        Assert.Equal(!lethal,player.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE));
        if(lethal)Assert.True(controller!.DeathReported);else Assert.Equal(newHp,player.GetLifeStats().GetMaxHp());
        effect.EndEffect();h.Clock.Advance(TimeSpan.FromMilliseconds(999));Assert.Equal(100,npc.GetLifeStats().GetCurrentHp());
        h.Clock.Advance(TimeSpan.FromMilliseconds(1));Assert.Equal(Math.Min(npc.GetLifeStats().GetMaxHp(),100+RetailNpcStatSpellDrain.HealAmount(damage,healPercent)),npc.GetLifeStats().GetCurrentHp());
        Assert.Empty(h.Clock.Faults);
    }
}
