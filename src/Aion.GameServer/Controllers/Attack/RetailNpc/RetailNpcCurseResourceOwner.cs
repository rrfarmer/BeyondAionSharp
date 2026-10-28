using System.Runtime.CompilerServices;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Prepared vampire curse lifecycle. Selected single-target casts
/// build a source-bound plan; unsupported equipment and effects stay gated.</summary>
internal static class RetailNpcCurseResourceOwner
{
    internal sealed class PlayerInputChangedException(string reason):InvalidOperationException(reason);
    internal sealed record Plan(int HpBasis,int HpUnscaled,int HpOtherPercent,
        int MpBasis,int MpUnscaled,int MpOtherPercent,Func<Player,bool> SnapshotCurrent,
        Func<Player,bool>? SnapshotWithCurse=null,Func<Player,long?>? StatCommitStamp=null,
        bool SelectedVampireInputs=false);

    private sealed class State(Player target,long life,HashSet<IStatFunction> functions,Plan plan,int maxHp,int maxMp)
    {
        internal readonly Player Target=target;
        internal readonly long Life=life;
        internal readonly HashSet<IStatFunction> Functions=functions;
        internal readonly Plan Plan=plan;
        internal readonly int MaxHp=maxHp,MaxMp=maxMp;
        internal bool Started,Removed,EndCommitted,Ended;
        // This is called under the HP lock. SnapshotCurrent may take an
        // equipment lock and must only run outside the HP lock.
        internal bool CurrentLife()=>Target.IsSpawned() && Target.GetController().RetailLifeEpoch==Life;
    }
    private static readonly ConditionalWeakTable<Effect,State> States=new();

    // The retail same-skill/slot replacement branch removes the previous
    // curse before starting the next one. Only an owned, source-checked plan
    // can witness the state that will be removed; an arbitrary fixture plan
    // or a different active curse is not evidence of its future resources.
    internal static bool CanReplaceSelectedForOfflineFixture(Player target,Effect replacement)
    {
        if(!ReferenceEquals(replacement.GetEffected(),target)||!target.IsSpawned()||target.IsDead()
            ||replacement.RetailHasEnded ||!target.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE))return false;
        var active=target.GetEffectController().GetAllEffects();
        var curses=active.Where(e=>(e.GetAbnormals()&AbnormalState.CURSE.GetId())!=0).ToArray();
        if(curses.Length!=1)return false;
        var previous=curses[0];
        if(ReferenceEquals(previous,replacement)||previous.GetSkillId()!=replacement.GetSkillId()
            ||previous.GetSkillLevel()!=replacement.GetSkillLevel()
            ||!string.Equals(previous.GetStack(),replacement.GetStack(),StringComparison.Ordinal))return false;
        lock(previous)
        {
            if(!States.TryGetValue(previous,out var state)||!state.Started||state.Removed||state.Ended
                ||!state.Plan.SelectedVampireInputs||state.Plan.SnapshotWithCurse is not { } current
                ||!ReferenceEquals(state.Target,target)||!state.CurrentLife()||previous.RetailHasEnded
                ||!RetailNpcStatSpellDrain.HasPreparedCurse(previous)
                ||target.GetLifeStats().GetMaxHp()!=state.MaxHp||target.GetLifeStats().GetMaxMp()!=state.MaxMp
                ||!current(target))return false;
            var stillActive=target.GetEffectController().GetAllEffects();
            return target.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE)
                && stillActive.Count(e=>(e.GetAbnormals()&AbnormalState.CURSE.GetId())!=0)==1
                && stillActive.Count(e=>ReferenceEquals(e,previous))==1
                && !previous.RetailHasEnded && state.CurrentLife()
                && target.GetLifeStats().GetMaxHp()==state.MaxHp && target.GetLifeStats().GetMaxMp()==state.MaxMp;
        }
    }

    internal static bool TryStart(Effect effect,CurseEffect template)
    {
        if(!RetailNpcStatSpellDrain.TryGetCurseProgram(effect,out var program))return false;
        lock(effect)
        {
            if(States.TryGetValue(effect,out var existing))
            {
                if(existing.Started && !existing.Ended)return true;
                throw new InvalidOperationException("Prepared vampire curse cannot restart");
            }
            if(!RetailNpcStatSpellDrain.HasPreparedCurse(effect) ||effect.RetailHasEnded || effect.GetEffected() is not Player target
                ||!RetailNpcStatSpellDrain.CurseHostMatches(template) || effect.GetEffectTemplates() is not {Count:2} templates
                ||templates[0].GetType()!=typeof(SpellAtkDrainInstantEffect) ||!ReferenceEquals(templates[1],template)
                ||!target.IsSpawned() ||target.IsDead() ||target.GetEffectController().IsInAnyAbnormalState(AbnormalState.CURSE))
                throw new InvalidOperationException("Incomplete prepared vampire curse lifecycle");
            var plan=program.Resources??program.FixturePlan?.Invoke(target,effect);
            Exception InputChanged(string reason)=>program.AdmissionCheck is null
                ?new InvalidOperationException(reason):new PlayerInputChangedException(reason);
            if(plan is null)throw InputChanged("Prepared vampire curse resource plan unavailable");
            if(plan.SnapshotCurrent is null
                ||plan.HpOtherPercent is <-10000 or >10000 ||plan.MpOtherPercent is <-10000 or >10000)
                throw new InvalidOperationException("Incomplete prepared vampire curse resource plan");
            int oldHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,plan.HpBasis,plan.HpUnscaled,plan.HpOtherPercent);
            int newHp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Hp,plan.HpBasis,plan.HpUnscaled,plan.HpOtherPercent-35);
            int oldMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,plan.MpBasis,plan.MpUnscaled,plan.MpOtherPercent);
            int newMp=RetailNpcCurseStatMath.Maximum(RetailNpcCurseStatMath.Resource.Mp,plan.MpBasis,plan.MpUnscaled,plan.MpOtherPercent-35);
            if(target.GetLifeStats().GetMaxHp()!=oldHp ||target.GetLifeStats().GetMaxMp()!=oldMp ||!plan.SnapshotCurrent(target))
                throw InputChanged("Prepared vampire curse snapshot changed");
            var functions=new HashSet<IStatFunction>(ReferenceEqualityComparer.Instance)
            {new StatAddFunction(StatEnum.MAXHP,newHp-oldHp,true),new StatAddFunction(StatEnum.MAXMP,newMp-oldMp,true)};
            var state=new State(target,target.GetController().RetailLifeEpoch,functions,plan,newHp,newMp);
            States.Add(effect,state);bool abnormal=false;
            try
            {
                target.GetEffectController().SetAbnormal(AbnormalState.CURSE);abnormal=true;
                target.GetGameStats().AddPreparedRetailCurseEffectOnly(effect,functions);
                if(!(plan.SnapshotWithCurse??plan.SnapshotCurrent)(target))
                    throw InputChanged("Prepared vampire curse snapshot retired");
                var stats=target.GetGameStats();long? statStamp=plan.StatCommitStamp?.Invoke(target);
                if(plan.StatCommitStamp is not null &&statStamp is null)
                    throw InputChanged("Prepared vampire curse stat stamp unavailable");
                if(!target.GetLifeStats().ApplyPreparedRetailCurseResources(effect,oldHp,newHp,oldMp,newMp,
                    state.CurrentLife,()=>state.Started=true,
                    statStamp is { } stamp ? ()=>ReferenceEquals(target.GetGameStats(),stats) &&stats.TryReserveRetailStatCommit(stamp):null,
                    statStamp is { } releaseStamp ? ()=>stats.ReleaseRetailStatCommit(releaseStamp):null))
                    throw InputChanged("Prepared vampire curse target retired");
                // An HP observer can synchronously end this effect after the
                // commit. Do not re-mark the effect after that cleanup.
                if(!state.Ended && !effect.RetailHasEnded)effect.SetAbnormal(AbnormalState.CURSE);
                return true;
            }
            catch
            {
                // If a post-commit observer throws, retain ownership so End can
                // clean up without replaying the committed resource update.
                if(!state.Started)
                {
                    target.GetGameStats().EndPreparedRetailCurseEffectOnly(effect,functions);
                    if(abnormal)target.GetEffectController().UnsetAbnormal(AbnormalState.CURSE);
                    States.Remove(effect);
                }
                throw;
            }
        }
    }

    internal static bool TryEnd(Effect effect,CurseEffect template)
    {
        if(!RetailNpcStatSpellDrain.TryGetCurseProgram(effect,out _))return false;
        lock(effect)
        {
            if(!States.TryGetValue(effect,out var state) || state.Ended)return true;
            if(!RetailNpcStatSpellDrain.HasPreparedCurse(effect) ||!ReferenceEquals(effect.GetEffected(),state.Target)
                ||!RetailNpcStatSpellDrain.CurseHostMatches(template))
                throw new InvalidOperationException("Prepared vampire curse changed before removal");
            state.Target.GetEffectController().UnsetAbnormal(AbnormalState.CURSE);
            if(!state.Removed)
            {
                state.Target.GetGameStats().EndPreparedRetailCurseEffectOnly(effect,state.Functions);
                state.Removed=true;
            }
            if(state.Started && !state.EndCommitted)
            {
                if(!state.Target.GetLifeStats().ReconcilePreparedRetailCurseRemoval(effect,state.CurrentLife,()=>state.EndCommitted=true))
                    state.EndCommitted=true; // Dead or retired targets do not receive an update.
            }
            state.Ended=true;
            return true;
        }
    }
}
