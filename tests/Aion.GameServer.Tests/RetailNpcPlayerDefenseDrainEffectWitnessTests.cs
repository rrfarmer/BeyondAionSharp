using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Change=Aion.GameServer.SkillEngine.Change.Change;
using ChangeFunc=Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Tests;

public sealed class RetailNpcPlayerDefenseDrainEffectWitnessTests
{
    [SkippableFact]
    public void CapturedHostOwnerRetiresOnReplacementAndEffectCleanup()
    {
        string? manifest=Environment.GetEnvironmentVariable("AION_RETAIL_STATS_TEST_MANIFEST");
        Skip.If(string.IsNullOrWhiteSpace(manifest),"Requires selected 5.8 17860 source");
        using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest!));
        var asset=doc.RootElement.GetProperty("skill_sources").EnumerateArray().Single(x=>x.GetProperty("id").GetInt32()==17860);
        byte[] source=File.ReadAllBytes(asset.GetProperty("path").GetString()!);
        // Isolated host objects only: no world, server, journey or SIM is started.
        var player=(Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        var stats=(PlayerGameStats)RuntimeHelpers.GetUninitializedObject(typeof(PlayerGameStats));
        typeof(CreatureGameStats).GetField("owner",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(stats,player);
        typeof(CreatureGameStats).GetField("stats",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(stats,new ConcurrentDictionary<StatEnum,List<IStatFunction>>());
        player.SetGameStats(stats);
        player.SetLifeStats((CreatureLifeStats)RuntimeHelpers.GetUninitializedObject(typeof(NpcLifeStats)));
        var controller=new PlayerEffectController(player);player.SetEffectController(controller);
        var primary=new SpellAtkDrainInstantEffect {Position=1};
        var statdown=new StatdownEffect {Position=2,NoResist=true,Duration2=60100,Effectid=10178603,
            Element=SkillElement.WATER,PreEffects=[1],Hoptype=HopType.SKILLLV,HopA=60,HopB=60,
            change=[new Change {Stat=StatEnum.PHYSICAL_DEFENSE,Func=ChangeFunc.PERCENT,Value=-10},
                new Change {Stat=StatEnum.MAGICAL_RESIST,Func=ChangeFunc.PERCENT,Value=-10}]};
        var template=new SkillTemplate {skillId=17860,stack="BNEL_SPELLDRAINMPATTSDDEFTA10_CH1",
            effects=new Effects {effects=[primary,statdown]}};
        var effect=new Effect(null!,player,template,1);effect.AddSuccessEffect(primary);effect.AddSuccessEffect(statdown);
        var map=Assert.IsType<Dictionary<string,Effect>>(typeof(EffectController).GetField("abnormalEffectMap",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller));
        map.Add(effect.GetStack(),effect);
        stats.AddEffectOnly(effect,[new StatRateFunction(StatEnum.PHYSICAL_DEFENSE,-10,true),new StatRateFunction(StatEnum.MAGICAL_RESIST,-10,true)]);
        Assert.True(RetailNpcPlayerDefenseDrainEffectWitness.TryCaptureForOfflineFixture(player,effect,source,out var receipt));
        Assert.NotNull(receipt);Assert.False(receipt!.GameplayReady);Assert.True(receipt.IsCurrentForOfflineFixture(player));
        var other=new RetailNpcPlayerDefenseDrainLifecycleProjection.Vector(0,0,0,0,0,0,0,25);
        Assert.True(receipt.TryProjectForOfflineFixture(player,other,out var projected));
        Assert.Equal(-10,projected.Physical);Assert.Equal(-10,projected.Fire);Assert.Equal(25,projected.MagicalResistance);
        var replacement=new Effect(null!,player,template,1);map[effect.GetStack()]=replacement;
        Assert.False(receipt.IsCurrentForOfflineFixture(player));Assert.False(receipt.TryProjectForOfflineFixture(player,other,out _));
        map[effect.GetStack()]=effect;
        player.SetEffectController(new PlayerEffectController(player));
        Assert.False(receipt.IsCurrentForOfflineFixture(player));
        player.SetEffectController(controller);
        effect.EndEffects(); // Existing host owner removes exactly its own two stat functions.
        Assert.Empty(stats.GetStatsSorted(StatEnum.PHYSICAL_DEFENSE));Assert.Empty(stats.GetStatsSorted(StatEnum.MAGICAL_RESIST));
        Assert.False(receipt.IsCurrentForOfflineFixture(player));
    }
}
