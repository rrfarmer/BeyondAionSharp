using System.Text.Json;
using Aion.GameServer.Controllers.Attack.RetailNpc;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Tests.Ai;
using Aion.GameServer.Utils.Stats;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

/// <summary>Compares constructed, unequipped host players with the reviewed
/// ordinary 5.8 source calculation; it does not admit a live curse.</summary>
[Collection("GoldenDataManager")]
public sealed class RetailNpcPlayerCurseBaselineComparisonTests(ITestOutputHelper output)
{
    private sealed class ModifierOwner : IStatOwner;

    [Fact]
    public void CompareUnequippedPlayerBaseAndPrimaryChangesAcrossClassesAndLevels()
    {
        using var h=BossAiHarness.For(220010000).Build();
        var player=RetailNpcStatPeriodicTests.Player(h,220010000);
        Assert.Empty(player.GetEquipment().GetEquippedItemIds());
        PlayerStatFunctions.AddPredefinedStatFunctions(player);
        var rows=new List<object>();
        foreach(var race in new[]{Race.ELYOS,Race.ASMODIANS})
        foreach(var pc in Enum.GetValues<PlayerClass>())
        foreach(int level in new[]{1,20,38,65})
        foreach(int adjustment in new[]{-10,0,10})
        {
            player.GetCommonData().SetRace(race);player.GetCommonData().SetPlayerClass(pc);
            player.GetCommonData().SetLevel(level);player.GetGameStats().UpdateStatsTemplate();
            var statOwner=new ModifierOwner();
            if(adjustment!=0)
                player.GetGameStats().AddEffectOnly(statOwner,[new StatAddFunction(StatEnum.HEALTH,adjustment,true),
                    new StatAddFunction(StatEnum.WILL,adjustment,true)]);
            var (nativeHealth,nativeWill)=RetailNpcCurseStatMath.InitialClassPrimary(pc.GetClassId());
            var primary=RetailNpcCurseStatMath.OrdinaryPrimaries(nativeHealth,nativeWill,
                adjustment==0?[]:[new(RetailNpcCurseStatMath.PrimaryAttribute.Health,0,adjustment),
                    new(RetailNpcCurseStatMath.PrimaryAttribute.Will,0,adjustment)]);
            int hostHealth=player.GetGameStats().GetHealth().GetCurrent();
            int hostWill=player.GetGameStats().GetWill().GetCurrent();
            int hostHp=player.GetGameStats().GetMaxHp().GetCurrent();
            int hostMp=player.GetGameStats().GetMaxMp().GetCurrent();
            int nativeHp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,(ushort)level,primary.Health,pc.GetClassId());
            int nativeMp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,(ushort)level,primary.Will,pc.GetClassId());
            // NPC-facing combat reads the current 4.8 player's primaries; it
            // must not silently substitute the 5.8 character's class defaults.
            Assert.InRange(hostHealth,80,999);Assert.InRange(hostWill,80,999);
            int retailFromHostHp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,(ushort)level,(ushort)hostHealth,pc.GetClassId());
            int retailFromHostMp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,(ushort)level,(ushort)hostWill,pc.GetClassId());
            Assert.True(hostHp>0&&hostMp>0&&nativeHp>0&&nativeMp>0);
            var curseOwner=new ModifierOwner();
            player.GetGameStats().AddEffectOnly(curseOwner,[new StatRateFunction(StatEnum.MAXHP,-35,true),
                new StatRateFunction(StatEnum.MAXMP,-35,true)]);
            int hostCurseHp=player.GetGameStats().GetMaxHp().GetCurrent();
            int hostCurseMp=player.GetGameStats().GetMaxMp().GetCurrent();
            int nativeCurseHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,nativeHp,nativeHp,-35);
            int nativeCurseMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,nativeMp,nativeMp,-35);
            if(race==Race.ASMODIANS&&pc==PlayerClass.PRIEST&&level==38&&adjustment==0)
            {
                Assert.Equal((100,110,4210,4270),(hostWill,(int)primary.Will,hostMp,nativeMp));
                Assert.Equal(4210,retailFromHostMp);
                // A primary-stat/version difference belongs to the scaled
                // basis; it must not be assumed to be an invariant offset.
                Assert.NotEqual(hostMp-nativeMp,hostCurseMp-nativeCurseMp);
            }
            rows.Add(new{race=race.ToString(),playerClass=pc.ToString(),level,adjustment,
                hostHealth,hostWill,nativeHealth=primary.Health,nativeWill=primary.Will,
                hostHp,nativeHp,hpDifference=hostHp-nativeHp,hostMp,nativeMp,mpDifference=hostMp-nativeMp,
                hostCurseHp,nativeCurseHp,cursedHpDifference=hostCurseHp-nativeCurseHp,
                hostCurseMp,nativeCurseMp,cursedMpDifference=hostCurseMp-nativeCurseMp,
                retailFromHostHp,retailFromHostMp,hostInputHpDifference=hostHp-retailFromHostHp,
                hostInputMpDifference=hostMp-retailFromHostMp});
            player.GetGameStats().EndEffect(curseOwner);
            if(adjustment!=0)player.GetGameStats().EndEffect(statOwner);
        }
        Assert.Equal(2*17*4*3,rows.Count);
        output.WriteLine("BASELINE_PROBE="+JsonSerializer.Serialize(rows));
    }
}
