using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Point-in-time host stat witness for an explicitly prepared curse.
/// This is not an atomic snapshot or proof of native player stat equivalence.</summary>
internal static class RetailNpcCurseStatWitness
{
    private static readonly StatEnum[] Watched=[StatEnum.MAXHP,StatEnum.MAXMP,StatEnum.HEALTH,StatEnum.WILL,StatEnum.KNOWLEDGE];

    private sealed record FunctionState(IStatFunction Function,IStatOwner? Owner,IStatFunction Underlying,
        StatEnum Stat,int Value,int Priority,bool Bonus);
    private sealed record State(FunctionState[] Functions,int Health,int Will,int Knowledge,uint ExternalMutations,long Stamp);
    internal sealed record Checks(Func<Player,bool> Before,Func<Player,bool> After,Func<Player,long?> CommitStamp);

    private static bool Capture(Player player,Effect curse,CreatureGameStats stats,
        CreatureGameStats.RetailStatMutationOwner tracked,int hpDelta,int mpDelta,bool allowCurse,
        Func<IStatFunction,bool>? reviewedConditional,out State state)
    {
        state=null!;var functions=new List<FunctionState>();int ownHp=0,ownMp=0;
        if(!ReferenceEquals(player.GetGameStats(),stats)||!stats.TryCaptureRetailStatMutation(out long mutationStamp))return false;
        foreach(var stat in Watched)
        {
            foreach(var function in stats.GetStatsSorted(stat))
            {
                IStatFunction underlying=function is StatFunctionProxy proxy?proxy.GetProxiedFunction():function;
                if(ReferenceEquals(function.GetOwner(),curse))
                {
                    if(!allowCurse ||function.HasConditions() ||underlying.GetType()!=typeof(StatAddFunction)
                        ||!function.IsBonus() ||stat is not (StatEnum.MAXHP or StatEnum.MAXMP))return false;
                    if(stat==StatEnum.MAXHP &&function.GetValue()==hpDelta)ownHp++;
                    else if(stat==StatEnum.MAXMP &&function.GetValue()==mpDelta)ownMp++;
                    else return false;
                    continue;
                }
                // Conditional functions need a separately reviewed owner and
                // state witness; unknown conditions remain unsupported.
                if(function.HasConditions() && !(reviewedConditional?.Invoke(function)??false))return false;
                functions.Add(new(function,function.GetOwner(),underlying,stat,function.GetValue(),function.GetPriority(),function.IsBonus()));
            }
        }
        if(allowCurse ? ownHp!=1 ||ownMp!=1 : ownHp!=0 ||ownMp!=0)return false;
        int health=stats.GetHealth().GetCurrent(),will=stats.GetWill().GetCurrent(),knowledge=stats.GetKnowledge().GetCurrent();
        if(!stats.TryRetailExternalStatMutations(mutationStamp,tracked,out uint external))return false;
        state=new(functions.ToArray(),health,will,knowledge,external,mutationStamp);
        return true;
    }

    private static bool Same(State a,State b)
    {
        if(a.Health!=b.Health ||a.Will!=b.Will ||a.Knowledge!=b.Knowledge ||a.ExternalMutations!=b.ExternalMutations
            ||a.Functions.Length!=b.Functions.Length)return false;
        for(int i=0;i<a.Functions.Length;i++)
        {
            var x=a.Functions[i];var y=b.Functions[i];
            if(!ReferenceEquals(x.Function,y.Function)||!ReferenceEquals(x.Owner,y.Owner)
                ||!ReferenceEquals(x.Underlying,y.Underlying)||x.Stat!=y.Stat||x.Value!=y.Value
                ||x.Priority!=y.Priority||x.Bonus!=y.Bonus)return false;
        }
        return true;
    }

    /// <summary>Creates a fail-closed observer for the original host functions
    /// and the curse's two exact temporary modifiers. Equipment is checked on
    /// both sides of the stat read. Neither read reserves or locks the player.</summary>
    internal static Checks Create(Player owner,Effect curse,Func<Player,bool> equipmentWitness,int hpDelta,int mpDelta,
        Func<IStatFunction,bool>? reviewedConditional=null)
    {
        ArgumentNullException.ThrowIfNull(owner);ArgumentNullException.ThrowIfNull(curse);
        ArgumentNullException.ThrowIfNull(equipmentWitness);
        var stats=owner.GetGameStats();var tracked=stats.TrackRetailStatOwner(curse);
        if(!ReferenceEquals(curse.GetEffected(),owner)||!equipmentWitness(owner)
            ||!Capture(owner,curse,stats,tracked,hpDelta,mpDelta,false,reviewedConditional,out var initial))return new(_=>false,_=>false,_=>null);
        long afterStamp=0;int afterAvailable=0;
        bool Check(Player current,bool after)
        {
            if(after)System.Threading.Volatile.Write(ref afterAvailable,0);
            if(!ReferenceEquals(current,owner) ||!equipmentWitness(current)
                ||!Capture(current,curse,stats,tracked,hpDelta,mpDelta,after,reviewedConditional,out var observed) ||!Same(initial,observed)
                ||!equipmentWitness(current) ||!stats.RetailStatMutationUnchanged(observed.Stamp))return false;
            if(after)
            {
                System.Threading.Volatile.Write(ref afterStamp,observed.Stamp);
                System.Threading.Volatile.Write(ref afterAvailable,1);
            }
            return true;
        }
        long? CommitStamp(Player current)=>ReferenceEquals(current,owner) &&ReferenceEquals(current.GetGameStats(),stats)
            &&System.Threading.Volatile.Read(ref afterAvailable)!=0 ? System.Threading.Volatile.Read(ref afterStamp):null;
        return new(current=>Check(current,false),current=>Check(current,true),CommitStamp);
    }
}
