using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Observed 4.8 player primary inputs for the reviewed 5.8 initial
/// HP/MP formulas. Equipment, other resource modifiers, effect admission and
/// a stable lifetime after this optimistic read remain separate requirements.</summary>
internal static class RetailNpcCursePlayerPrimarySnapshot
{
    private static readonly StatEnum[] PrimaryStats=[StatEnum.HEALTH,StatEnum.WILL,StatEnum.ALLPARA,StatEnum.STRVIT,StatEnum.KNOWIL];
    private static bool NoPrimaryFunctions(CreatureGameStats stats)=>PrimaryStats.All(stat=>stats.GetStatsSorted(stat).Count==0);

    internal sealed class Receipt(Player owner,CreatureGameStats stats,long stamp,long life,
        Race race,PlayerClass playerClass,int level,int health,int will,int hpBasis,int mpBasis,bool requireUnmodifiedHost,
        IRetailNpcPlayerPrimaryReceipt? food)
    {
        internal int HpBasis=>hpBasis;
        internal int MpBasis=>mpBasis;

        // Adding the prepared curse changes the stat mutation stamp, but must
        // not change the observed class, level or primary inputs themselves.
        internal bool HasEquivalentInputs(Player current)
        {
            if(!ReferenceEquals(current,owner)||!ReferenceEquals(current.GetGameStats(),stats)
                ||!current.IsSpawned()||current.GetLifeStats().GetCurrentHp()<=0
                ||current.GetController().RetailLifeEpoch!=life||current.GetRace()!=race
                ||current.GetPlayerClass()!=playerClass||current.GetLevel()!=level
                ||!stats.TryCaptureRetailStatMutation(out long now))return false;
            int currentHealth=stats.GetHealth().GetCurrent(),currentWill=stats.GetWill().GetCurrent();
            return currentHealth==health&&currentWill==will&&stats.RetailStatMutationUnchanged(now)
                &&(!requireUnmodifiedHost || ((food is null
                    ?currentHealth==playerClass.GetHealth() &&currentWill==playerClass.GetWill() &&NoPrimaryFunctions(stats)
                    :food.MatchesHostPrimaries(current,currentHealth,currentWill) &&food.IsCurrentForOfflineFixture(current))
                    &&stats.RetailStatMutationUnchanged(now)))
                &&current.IsSpawned()&&current.GetLifeStats().GetCurrentHp()>0
                &&current.GetController().RetailLifeEpoch==life&&current.GetRace()==race
                &&current.GetPlayerClass()==playerClass&&current.GetLevel()==level;
        }

        internal bool IsCurrent(Player current)=>stats.RetailStatMutationUnchanged(stamp)
            &&HasEquivalentInputs(current)&&stats.RetailStatMutationUnchanged(stamp);
    }

    internal static bool TryCapture(Player player,out Receipt? receipt)
        =>TryCaptureCore(player,false,null,out receipt);

    // A 4.8 primary value is not always the 5.8 class initial value. This
    // restricted fixture path uses the reviewed native initials only while
    // host HEALTH/WILL have no functions and still equal the host class base.
    // It does not establish retail-only effects, eligibility or live state.
    internal static bool TryCaptureRetailInitialForUnmodifiedHost(Player player,out Receipt? receipt)
        =>TryCaptureCore(player,true,null,out receipt);

    // The selected food receipt owns the only reviewed primary modifier in
    // this explicit fixture. It does not establish all possible food sources.
    internal static bool TryCaptureRetailWithFoodForOfflineFixture(Player player,
        IRetailNpcPlayerPrimaryReceipt food,out Receipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(food);
        if(food is not (RetailNpcPlayerPrimaryFoodCapture.Receipt or RetailNpcPlayerPrimaryStatdownCapture.Receipt
            or RetailNpcPlayerPrimaryAllStatdownCapture.Receipt or RetailNpcPlayerPrimaryFlightCapture.Receipt))
        {receipt=null;return false;}
        return TryCaptureCore(player,true,food,out receipt);
    }

    private static bool TryCaptureCore(Player player,bool requireUnmodifiedHost,
        IRetailNpcPlayerPrimaryReceipt? food,out Receipt? receipt)
    {
        ArgumentNullException.ThrowIfNull(player);receipt=null;
        if(!player.IsSpawned()||player.GetLifeStats().GetCurrentHp()<=0)return false;
        var stats=player.GetGameStats();long life=player.GetController().RetailLifeEpoch;
        Race race=player.GetRace();PlayerClass playerClass=player.GetPlayerClass();int level=player.GetLevel();
        if(race is not (Race.ELYOS or Race.ASMODIANS)||!Enum.IsDefined(playerClass)||level is <1 or >65
            ||!stats.TryCaptureRetailStatMutation(out long stamp))return false;
        int health=stats.GetHealth().GetCurrent(),will=stats.GetWill().GetCurrent();
        if(health is <80 or >999||will is <80 or >999)return false;
        ushort sourceHealth=(ushort)health,sourceWill=(ushort)will;
        if(requireUnmodifiedHost)
        {
            if(!(food is null
                ?health==playerClass.GetHealth() &&will==playerClass.GetWill() &&NoPrimaryFunctions(stats)
                :food.MatchesHostPrimaries(player,health,will) &&food.IsCurrentForOfflineFixture(player))
                ||!stats.RetailStatMutationUnchanged(stamp))return false;
            (sourceHealth,sourceWill)=RetailNpcCurseStatMath.InitialClassPrimary(playerClass.GetClassId());
            if(food is not null)
                (sourceHealth,sourceWill)=RetailNpcCurseStatMath.OrdinaryPrimaries(sourceHealth,sourceWill,[food.Modifier]);
        }
        int hp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Hp,(ushort)level,sourceHealth,playerClass.GetClassId());
        int mp=RetailNpcCurseStatMath.InitialBase(RetailNpcCurseStatMath.Resource.Mp,(ushort)level,sourceWill,playerClass.GetClassId());
        var result=new Receipt(player,stats,stamp,life,race,playerClass,level,health,will,hp,mp,requireUnmodifiedHost,food);
        if(!result.IsCurrent(player))return false;
        receipt=result;return true;
    }
}
