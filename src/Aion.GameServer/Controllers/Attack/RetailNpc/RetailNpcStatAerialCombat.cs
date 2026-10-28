using System.Runtime.CompilerServices;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound numeric aerial finishers. Existing HP, target selection
/// and movement owners remain. Native CloseAerial queues one removal by effect
/// type55, using the same hit-delay input as the HP owner, rather than skill8224.
/// Proof: stats-only-20261025-01/close-aerial-stat-implementation-01.</summary>
internal static class RetailNpcStatAerialCombat
{
    internal sealed record CloseProgram(SkillElement Element,bool CheckStatus,int HopA,int HopB);
    internal static readonly HashSet<string> CloseFields=new(StringComparer.Ordinal)
    {
        "effect2_type","effect2_noresist","effect2_remain1","effect2_target_type",
        "effect2_cond_preeffect","effect2_cond_preeffect_prob1","effect2_cond_preeffect_prob2",
        "effect2_cond_status","effect2_cond_status_prob1","effect2_cond_status_prob2",
        "effect2_reserved10","effect2_hop_type","effect2_hop_a","effect2_hop_b"
    };
    private sealed record Selected(CloseProgram Program,EffectTemplate Host,Player Target,ushort HitWord)
    {
        internal int Calculated;
        internal readonly CloseLease Lease=new();
    }
    private static readonly ConditionalWeakTable<Effect,Selected> SelectedEffects=new();

    internal static int CloseDelay(int hitWord)
    {
        if(hitWord is <0 or >ushort.MaxValue)throw new ArgumentOutOfRangeException(nameof(hitWord));
        int delay=hitWord-50;
        return delay<1?100:delay; //51 means1ms, not100ms.
    }

    internal sealed class CloseLease
    {
        private int scheduled;
        internal bool TrySchedule(bool hasWorld,bool aerial,int hitWord,Action<Action,int> schedule,Action remove)
        {
            if(!hasWorld||!aerial)return false;
            int delay=CloseDelay(hitWord);
            if(Interlocked.CompareExchange(ref scheduled,1,0)!=0)return false;
            schedule(remove,delay);return true;
        }
    }

    // Check order follows AddSkillEffect: e1 success, inclusive0..100 preeffect
    // draw, optional native status predicate and its inclusive0..100 draw.
    internal static bool CheckSecondary(bool primarySucceeded,bool checkStatus,Func<bool> aerial,Func<int,int,int> draw)
    {
        if(!primarySucceeded)return false;
        Draw100(draw);
        if(checkStatus)
        {
            if(!aerial())return false;
            Draw100(draw);
        }
        return true;
    }
    private static void Draw100(Func<int,int,int> draw)
    {
        int value=draw(0,100);
        if(value is <0 or >100)throw new InvalidOperationException("Aerial condition draw outside0..100");
    }

    internal static bool HostMatches(CloseProgram source,EffectTemplate template)
    {
        if(template.GetType()!=typeof(CloseAerialEffect)||template.GetPosition()!=2||!template.IsNoResist()
            ||template.GetElement()!=source.Element||template.PreEffects is not [1]||template.GetPreEffectProb()!=100
            ||template.Hoptype!=HopType.SKILLLV||template.HopA!=source.HopA||template.HopB!=source.HopB
            ||template.Duration1!=0||template.Duration2!=0||template.SubEffectField is not null
            ||template.Modifiers is not null||template.change is {Count:>0}
            ||template.EffectSubConditions?.conditions is {Count:>0})return false;
        var conditions=template.GetEffectConditions()?.conditions;
        return source.CheckStatus
            ?conditions is [AbnormalStateCondition {Value:AbnormalState.OPENAERIAL}]
            :conditions is null or {Count:0};
    }

    internal static bool Preflight(Effect effect,RetailNpcStatSkillCombat.Source source,out string reason)
    {
        reason="";
        if((source.AdditionalStatusMask&RetailNpcStatTargetConditions.OpenAerialMask)==0)return true;
        var templates=effect.GetEffectTemplates();
        if(source.AerialClose is null)
        {
            if(templates.Count()!=1){reason="aerial-host-has-unreviewed-sibling";return false;}
            return true;
        }
        if(templates.Count()!=2||effect.GetSkillTemplate().GetEffectTemplate(2) is not { } host||!HostMatches(source.AerialClose,host))
        {reason="aerial-close-source-host-secondary-drift";return false;}
        if(effect.GetSkill() is not { } skill||skill.GetHitTime() is <0 or >ushort.MaxValue)
        {reason="aerial-close-missing-current-hit-delay-input";return false;}
        return true;
    }

    // Called only after the primary source/host/current-owner preflights pass.
    internal static void Prepare(Effect effect,RetailNpcStatSkillCombat.Source source)
    {
        if(source.AerialClose is not { } program)return;
        SelectedEffects.GetValue(effect,_=>new(program,effect.GetSkillTemplate().GetEffectTemplate(2)??throw new InvalidOperationException("Missing preflighted aerial secondary"),
            (Player)effect.GetEffected(),checked((ushort)effect.GetSkill().GetHitTime())));
    }

    internal static bool TryCalculateClose(Effect effect,EffectTemplate template)
    {
        if(!SelectedEffects.TryGetValue(effect,out var selected))return false;
        if(!ReferenceEquals(template,selected.Host)||!HostMatches(selected.Program,template))
            throw new InvalidOperationException("Selected NPC aerial secondary changed after primary preflight");
        if(Interlocked.Exchange(ref selected.Calculated,1)!=0)return true;
        bool Aerial()=>(selected.Target.GetEffectController().RetailNpcTargetConditionMask&RetailNpcStatTargetConditions.OpenAerialMask)!=0;
        if(!CheckSecondary(effect.IsInSuccessEffects(1),selected.Program.CheckStatus,Aerial,RetailNpcStatSkillCombat.Draw))return true;
        // Source noresist1 skips magical resist. No extra damage/critical or
        // reflector calculation belongs to this native status-only secondary.
        effect.AddSuccessEffect(template);effect.SetSpellStatus(SpellStatus.CLOSEAERIAL);
        selected.Lease.TrySchedule(selected.Target.GetWorldId()!=0,Aerial(),selected.HitWord,
            (remove,delay)=>ThreadPoolManager.GetInstance().Schedule(_=>
                {remove();return ValueTask.CompletedTask;},TimeSpan.FromMilliseconds(delay)),
            ()=>selected.Target.GetEffectController().RemoveRetailNpcAerialEffects());
        return true;
    }

    internal static bool HasSelectedClose(Effect effect)=>SelectedEffects.TryGetValue(effect,out _);
}
