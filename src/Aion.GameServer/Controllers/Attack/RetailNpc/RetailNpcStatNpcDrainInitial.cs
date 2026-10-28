using Aion.GameServer.Model;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Source-bound first-hit projection for the prepared ordinary NPC
/// drain pair. This does not admit or execute a live cast: actor state is not
/// reserved, protection and native caster eligibility remain separate owners.</summary>
internal static class RetailNpcStatNpcDrainInitial
{
    internal sealed record Result(RetailNpcStatNpcDrainPreflight.Selection Selection,int BoostDelta,int BoostCap,
        RetailNpcStatNormalInputs.Modifiers Modifiers,int Base,int Modified,
        RetailNpcStatNpcSpellDefense.Result Defense,int CriticalRoll,int CriticalThreshold,int Damage);

    private sealed class OfflineAdmission(int roll,Func<Npc,bool> eligible)
    {
        internal readonly int Roll=roll;
        internal readonly Func<Npc,bool> Eligible=eligible;
        internal bool Attempted,Success;
        internal Result? Projection;
        internal string Reason="not-attempted";
    }
    private static readonly ConditionalWeakTable<Effect,OfflineAdmission> OfflineAdmissions=new();

    // A fixture must opt in on a single concrete effect; no production caller
    // supplies this incomplete eligibility/protection capability.
    internal static void AttachOfflineInitialization(Effect effect,int criticalRoll,Func<Npc,bool> eligible)
    {
        ArgumentNullException.ThrowIfNull(effect);ArgumentNullException.ThrowIfNull(eligible);
        if(effect.GetEffector() is not Npc || effect.GetEffected() is not Npc
            ||effect.GetEffectTemplates().FirstOrDefault() is not { } primary
            ||primary.GetType()!=typeof(SpellAtkDrainInstantEffect) ||primary.Position!=1)
            throw new InvalidOperationException("Offline NPC drain fixture requires an exact NPC primary");
        OfflineAdmissions.Add(effect,new(criticalRoll,eligible));
    }

    internal static bool TryConsumeOfflineInitialization(Effect effect,EffectTemplate primary,out bool success)
    {
        success=false;
        if(!OfflineAdmissions.TryGetValue(effect,out var admission))return false;
        if(primary.GetType()!=typeof(SpellAtkDrainInstantEffect) || primary.Position!=1
            ||!ReferenceEquals(effect.GetEffectTemplates().FirstOrDefault(),primary))
            throw new InvalidOperationException("Offline NPC drain primary changed");
        lock(admission)
        {
            if(admission.Attempted)throw new InvalidOperationException("Offline NPC drain initialization cannot repeat");
            admission.Attempted=true;
            admission.Success=TryPrepareOffline(effect,admission.Roll,admission.Eligible,
                out var result,out string reason);
            admission.Projection=admission.Success?result:null;
            admission.Reason=reason;
            success=admission.Success;
            return true; // A declined explicit fixture must not use a host fallback.
        }
    }

    internal static bool TryGetOfflineInitialization(Effect effect,out Result? result,out string reason)
    {
        result=null;reason="not-attached";
        if(!OfflineAdmissions.TryGetValue(effect,out var admission))return false;
        lock(admission){result=admission.Projection;reason=admission.Reason;return admission.Attempted;}
    }

    /// <summary>Offline fixture seam for the complete paired effect. Eligibility
    /// is deliberately supplied by the fixture: the native NPC lifecycle flag
    /// and atomic protection/lifetime admission are not mapped for gameplay.</summary>
    internal static bool TryPrepareOffline(Effect effect,int criticalRoll,Func<Npc,bool> nativeCasterEligible,
        out Result result,out string reason)
    {
        ArgumentNullException.ThrowIfNull(nativeCasterEligible);
        lock(effect)
        {
            if(!TryCalculate(effect,criticalRoll,out result,out reason))return false;
            var selected=result.Selection;
            if(!nativeCasterEligible(selected.Caster)){reason="fixture-caster-ineligible";return false;}
            if(!TryCalculate(effect,criticalRoll,out result,out reason))return false;
            selected=result.Selection;
            // The target observer owner holds its update lock from the final
            // no-shield check through the existing hit-record calculation.
            if(!selected.Target.GetObserveController().TryCalculateUnprotectedNpcDrainForOfflineFixture(
                effect,selected.Target,result.Damage,selected.Initial.Position,out var protection) ||protection is null)
            {reason="unreviewed-protection-or-observer";return false;}
            // The reserved result is what the delayed initial heal consumes.
            // Recheck after protection before accepting this prepared fixture.
            if(!RetailNpcStatNpcDrainPreflight.InitialHealFitsSnapshot(selected.CasterMaxHp,
                effect.GetReserveds(selected.Initial.Position).GetValue(),selected.Source.InitialDrain!.HpPercent!.Value))
            {reason="unreviewed-initial-npc-drain-heal-overflow";return false;}
            effect.SetCalculatedMagicalCritical(selected.Initial.Position,false);
            effect.AddSuccessEffect(selected.Initial);
            var operands=selected.Source.Parts[0].Drain!;
            RetailNpcStatSpellDrain.Prepare(effect,selected.Initial,new(selected.Source.InitialDrain!.HpPercent!.Value,false,
                NpcPeriodic:new(selected.SourceLevel,operands,nativeCasterEligible,selected.CasterLife,
                    selected.TargetLife,selected.Target.GetLifeStats(),protection,selected.Caster.GetLifeStats())));
            reason="prepared-fixture-not-enabled";
            return true;
        }
    }

    internal static bool TryCalculate(Effect effect,int criticalRoll,out Result result,out string reason)
    {
        result=null!;
        if(criticalRoll is <1 or >1000){reason="critical-roll-outside-native-range";return false;}
        if(!RetailNpcStatNpcDrainPreflight.TryMatchInputs(effect,out var selected,out reason))return false;
        var caster=selected.Caster;var target=selected.Target;
        if(selected.TargetProfile.MagicalDamageTrim!=0 || SkyTempleCombatInputs.Pose(target)!=0
            ||target.GetGameStats().GetMCR().GetCurrent()<0)
        {reason="unreviewed-initial-npc-fixed-damage-or-critical-context";return false;}
        var element=selected.Initial.Element.GetStatForElement();
        if(element is null){reason="unreviewed-initial-npc-element";return false;}
        int mdef=target.GetGameStats().GetMDef().GetCurrent();
        int elemental=target.GetGameStats().GetResistance(element.Value).GetCurrent();
        if(elemental is <short.MinValue or >short.MaxValue
            ||SkyTempleCombatInputs.Moved(target,StatEnum.MAGICAL_DEFEND,mdef)!=mdef
            ||SkyTempleCombatInputs.Moved(target,element.Value,elemental)!=elemental)
        {reason="unreviewed-initial-npc-defense-or-movement";return false;}
        int boost=unchecked(caster.GetGameStats().GetMBoost().GetCurrent()-target.GetGameStats().GetMBResist().GetCurrent());
        int? selectedCap=Aion.GameServer.Retail.RetailNpcStatCatalog.MagicBoostCap;
        if(selectedCap is null && boost>=2900){reason="missing-initial-npc-boost-cap";return false;}
        RetailNpcStatNormalInputs.Modifiers modifiers;
        try { modifiers=RetailNpcStatNormalInputs.CurrentModifiers(caster,StatEnum.BOOST_SPELL_ATTACK); }
        catch(InvalidOperationException){reason="unreviewed-initial-npc-spell-modifier";return false;}
        int cap=selectedCap??2900;
        int baseDamage=RetailNpcStatSkillCombat.AbsoluteSpellBase(selected.Source.InitialDrain!.Value!.Value,
            boost,(uint)selected.TargetProfile.StatRatio,true,cap);
        int modified=Math.Max(1,RetailNpcStatSkillCombat.ApplySpellAttackModifiers(baseDamage,modifiers));
        var scope=RetailNpcNormalScope.Create(RetailNpcNormalScope.MainBinarySha256,
            RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,
            selected.CasterProfile.NpcId,selected.TargetProfile.NpcId);
        var defense=RetailNpcStatNpcSpellDefense.Calculate(scope,new(modified,caster.GetLevel(),target.GetLevel(),
            (uint)selected.CasterProfile.StatRatio,mdef,0,elemental,0));
        int threshold=RetailNpcStatSkillCombat.CriticalThreshold(0,target.GetGameStats().GetMCR().GetCurrent(),0,
            (uint)selected.TargetProfile.StatRatio);
        if(criticalRoll<threshold){reason="unreviewed-initial-npc-critical-damage";return false;}
        if(effect.RetailHasEnded || caster.GetController().RetailLifeEpoch!=selected.CasterLife
            ||target.GetController().RetailLifeEpoch!=selected.TargetLife
            ||target.GetObserveController().HasDrainProtectionObserver ||caster.GetObserveController().HasSkillCriticalObserver)
        {reason="initial-npc-snapshot-retired";return false;}
        int damage=Math.Max(1,defense.AfterElement);
        if(!RetailNpcStatNpcDrainPreflight.InitialHealFitsSnapshot(selected.CasterMaxHp,damage,
            selected.Source.InitialDrain!.HpPercent!.Value))
        {reason="unreviewed-initial-npc-drain-heal-overflow";return false;}
        result=new(selected,boost,cap,modifiers,baseDamage,modified,defense,criticalRoll,threshold,damage);
        reason="calculated-inputs-not-enabled";
        return true;
    }
}
