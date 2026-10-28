using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Tests.Ai;

namespace Aion.GameServer.Tests;

[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerPrimaryFoodCaptureTests
{
    private sealed class OtherOwner:IStatOwner;

    [SkippableFact]
    public void ReviewedFoodRequiresItsExactActiveOwnerAndFeedsRetailMpBase()
    {
        string? path=Environment.GetEnvironmentVariable("AION_RETAIL_PRIMARY_FOOD_SOURCE");
        Skip.If(string.IsNullOrWhiteSpace(path),"Requires the pinned retail food skill");
        byte[] source=File.ReadAllBytes(path!);var bad=(byte[])source.Clone();bad[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RetailNpcPlayerPrimaryFoodCapture.VerifySourceForOfflineFixture(bad));
        using var h=BossAiHarness.For(220050000).WithAi(typeof(RetailNpcStatCatalogTests.BaseFixtureAI)).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220050000);
        var template=DataManager.SKILL_DATA.GetSkillTemplate(9839);
        var foodEffect=Assert.IsType<StatupEffect>(template.GetEffectTemplate(1));
        var effect=new Effect(player,player,template,1);effect.AddSuccessEffect(foodEffect);
        Assert.False(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,effect,source,out _));
        effect.ApplyEffect();
        Assert.True(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,effect,source,out var receipt));
        Assert.NotNull(receipt);Assert.True(receipt.IsCurrentForOfflineFixture(player));
        var initial=RetailNpcCurseStatMath.InitialClassPrimary(player.GetPlayerClass().GetClassId());
        var primary=RetailNpcCurseStatMath.OrdinaryPrimaries(initial.Health,initial.Will,[receipt.Modifier]);
        Assert.Equal(initial.Health,primary.Health);Assert.Equal(initial.Will+5,primary.Will);
        Assert.True(RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,33,primary.Will,player.GetPlayerClass().GetClassId())
            >RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,33,initial.Will,player.GetPlayerClass().GetClassId()));
        var npc=h.SpawnWithAi(214435,"retail_stat_base_fixture");
        var curse=new Effect(npc,player,DataManager.SKILL_DATA.GetSkillTemplate(16596),38);
        var witness=RetailNpcCurseStatWitness.Create(player,curse,receipt.IsCurrentForOfflineFixture,-100,-100);
        Assert.True(witness.Before(player));
        var extra=new OtherOwner();player.GetGameStats().AddEffectOnly(extra,[new StatAddFunction(StatEnum.HEALTH,1,true)]);
        Assert.False(receipt.IsCurrentForOfflineFixture(player));Assert.False(witness.Before(player));
        var remove=typeof(CreatureGameStats).GetMethod("RemoveEffectFunctions",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(true,remove.Invoke(player.GetGameStats(),[extra]));Assert.True(receipt.IsCurrentForOfflineFixture(player));
        Assert.False(witness.Before(player)); // A mutation after capture remains invalid, even when its value is restored.
        witness=RetailNpcCurseStatWitness.Create(player,curse,receipt.IsCurrentForOfflineFixture,-100,-100);
        Assert.True(witness.Before(player));effect.EndEffect();Assert.False(receipt.IsCurrentForOfflineFixture(player));
        Assert.False(witness.Before(player));
        Assert.False(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,effect,source,out _));
        var replacement=new Effect(player,player,template,1);replacement.AddSuccessEffect(foodEffect);replacement.ApplyEffect();
        Assert.False(receipt.IsCurrentForOfflineFixture(player));
        Assert.True(RetailNpcPlayerPrimaryFoodCapture.TryCaptureForOfflineFixture(player,replacement,source,out var replacementReceipt));
        Assert.True(replacementReceipt!.IsCurrentForOfflineFixture(player));replacement.EndEffect();
        Assert.Empty(h.Clock.Faults);
    }
}
