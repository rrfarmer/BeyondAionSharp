using System.Runtime.CompilerServices;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserMagicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Selected source magic bridge. Native scalar order comes from36f850/bc0a30;
/// actual4.8 effects, protections, packets and HP remain owned by their existing owners.</summary>
internal static class SkyTempleNpcSkillCombat
{
    internal readonly record struct Spell(int Percent,SkillElement Element);
    internal static Spell SelectedSpell(int id) => id switch
    {
        17039 => new(23,SkillElement.WIND), 16561 or 16723 => new(135,SkillElement.FIRE),
        16576 or 16592 => new(99,SkillElement.FIRE), 16589 => new(154,SkillElement.FIRE),
        16710 => new(77,SkillElement.FIRE), 16590 => new(92,SkillElement.WATER),
        16593 => new(34,SkillElement.WATER), 16594 => new(34,SkillElement.WIND),
        16595 => new(34,SkillElement.EARTH), 16724 => new(115,SkillElement.WATER),
        16725 => new(135,SkillElement.WIND),
        _ => throw new InvalidDataException("Not a selected SkyTemple SpellATK_Instant source: " + id)
    };

    internal static bool TryCalculate(Effect effect,EffectTemplate template,SpellStatus? spellStatus,out bool success)
    {
        success = false;
        if (effect.GetEffector() is not Npc npc || !SkyTempleNpcProfileCatalog.TryGet(npc,out var profile)) return false;
        if(IsOwnedChild(effect,17039,8217)||IsOwnedChild(effect,16794,8223))
            return CalculateOwnedChild(effect,template,spellStatus,out success);
        if (!profile.Skills.Any(s=>s.Id48==effect.GetSkillId() && s.Level==effect.GetSkillLevel()))
            throw new InvalidOperationException("SkyTemple cast is not an admitted source skill/level binding");
        if(SkyTempleNpcAbnormalEffects.TryCalculate(effect,template,spellStatus,out success))return true;
        if(SkyTempleNpcPhysicalSkills.TryCalculate(effect,template,spellStatus,out success))return true;
        if(SkyTempleNpcPeriodicEffects.TryCalculate(effect,template,spellStatus,out success))return true;
        bool selfFamily=(effect.GetSkillId()==16522&&(template is StatupEffect||template is ShieldEffect))||
            (effect.GetSkillId()==16699&&template is ReflectorEffect)||
            (effect.GetSkillId() is 16836 or 16588&&template is HealInstantEffect);
        if(selfFamily)
        {
            if(!ReferenceEquals(effect.GetEffected(),npc))throw new InvalidOperationException("Selected self effect has a different actual target");
            if(effect.GetSkillId()==16522&&template.GetPosition()==2&&!effect.IsInSuccessEffects(1))return true;
            // Source NoResist self families and Shield's constant-success CheckResist.
            // Keep their actual Buf/Heal/Shield Begin/End owners; no Java self-dodge roll.
            effect.AddSuccessEffect(template);if(spellStatus.HasValue)effect.SetSpellStatus(spellStatus.Value);
            template.CalculateDamage(effect);success=true;return true;
        }
        if (template is not SpellAttackInstantEffect)
            throw new InvalidOperationException("Unhandled selected SkyTemple effect family/slot: "+template.GetType().Name);
        if (effect.GetEffected() is not Player target)
            throw new InvalidOperationException("Selected hostile spell requires actual Player target");
        Spell selected = SelectedSpell(effect.GetSkillId());
        if (template.GetPosition()!=1 || template.GetElement()!=selected.Element)
            throw new InvalidOperationException("Selected spell source element/slot differs");

        // Target check owns resistance BEFORE base and critical. An ordinary failed RNG
        // check creates no counter deadline and does not consume the later Begin draw.
        var scope = SkyTempleCombatInputs.Scope(profile.NpcId);
        var context = RetailNpcNormalStatusMath.Context.Create(scope,RetailNpcNormalScope.MainBinarySha256,
            "current4.8 User stats mapped to selected retail spell",false);
        var boost = OneTimeBoostSkillAttackEffect.GetActiveBoost(npc,template);
        int accuracy = npc.GetGameStats().GetMAccuracy().GetCurrent();
        if (boost is not null) accuracy = boost.CalculateMagicalAccuracy(npc.GetGameStats().GetMAccuracy(),0);
        int threshold = RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(context,new(
            target.GetGameStats().GetMResist().GetCurrent(),accuracy,(uint)profile.StatRatio,profile.Level,target.GetLevel(),Pose(target)));
        if (target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST) || SkyTempleNpcCombat.Draw(1,1000)<=threshold)
        {
            effect.ResetMagicalCritical(); effect.SetAttackStatus(AttackStatus.RESIST);
            effect.SetEffectResult(EffectResult.RESIST); return true;
        }
        int boostDelta = npc.GetGameStats().GetMBoost().GetCurrent()-target.GetGameStats().GetMBResist().GetCurrent();
        int sample = SkyTempleNpcCombat.Draw(profile.MinDamage,profile.MaxDamage);
        int amount = MagicBase(sample,selected.Percent,boostDelta,1000);
        amount = F.Cvtt(npc.GetGameStats().GetStat(StatEnum.MAGICAL_ATTACK,amount).GetExactCurrent());
        if (boost is not null) { amount = F.Cvtt(boost.CalculateDamage(amount)); boost.ConsumeCharge(); }
        amount = Math.Max(1,amount); // ordinary NPC→User level helper is identity
        int mdef = SkyTempleCombatInputs.Moved(target,StatEnum.MAGICAL_DEFEND,target.GetGameStats().GetMDef().GetCurrent());
        amount = Math.Max(0,unchecked(amount-F.Cvtt(F.Div(F.FromInt(mdef),10f))));
        int element = SkyTempleCombatInputs.ElementalDefense(target,selected.Element);
        int normalized = F.Cvtt(F.Div(F.FromInt(element),F.Div(profile.StatRatio,1000f)));
        int extra = profile.Level>50 && target.GetLevel()>50 ? (Math.Min(profile.Level,target.GetLevel())-50)*10 : 0;
        float multiplier = F.Sub(1f,F.Div(Math.Max(SkyTempleCombatInputs.ElementalFloor(target.GetLevel()),normalized),F.Add(extra,1300f)));
        amount = F.Cvtt(F.Mul(amount,multiplier));
        int critical = npc.GetGameStats().GetMCritical().GetCurrent();
        int critDraw = SkyTempleNpcCombat.Draw(1,1000);
        var forced = npc.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL,true);
        bool isCritical;
        if (forced.IsResult() && !forced.IsPercent()) isCritical = critDraw<=forced.GetValue();
        else
        {
            if (forced.IsResult()) critical = F.Cvtt(F.Mul(critical,F.Add(1f,F.Div(forced.GetValue(),100f))));
            int critThreshold = Math.Min(500,unchecked(critical+(Pose(target)==5?500:0)-target.GetGameStats().GetMCR().GetCurrent()));
            isCritical = critDraw<critThreshold; // strict magic comparison, unlike normal hit<=
        }
        effect.SetCalculatedMagicalCritical(template.GetPosition(),isCritical);
        // All thirteen selected sources have zero critical-add coefficient. NPC critical
        // damage is max(1,1-reduction/1000), not the legacy spell multiplier1.5.
        if (isCritical)
        {
            int reduction = F.Cvtt(F.Div(SkyTempleCombatInputs.CriticalReduction(target,false),F.Div(profile.StatRatio,1000f)));
            amount = F.Cvtt(F.Mul(amount,Math.Max(1f,F.Sub(1f,F.Div(reduction,1000f)))));
        }
        var pve = SkyTempleCombatInputs.Pve(npc,target,false);
        amount = Math.Max(1,F.Cvtt(F.Mul(amount,RetailNpcNormalDamageStages.CalculatePveRatio(scope,pve.Attack,pve.Defense))));
        // Begin's source chance0/100 still invokes the1..100 wrapper even at100.
        _ = SkyTempleNpcCombat.Draw(1,100);
        effect.AddSuccessEffect(template);
        if (spellStatus.HasValue) effect.SetSpellStatus(spellStatus.Value);
        AttackUtil.CalculateEffectResult(effect,target,amount,isCritical?AttackStatus.CRITICAL:AttackStatus.NORMALHIT,
            HitType.MAHIT,false,template.GetPosition(),true);
        success = true; return true;
    }

    /// <summary>Complete selected NPC min/max integer-boost base, independent of host objects.
    /// Both unresolved cap-selector branches coincide below2900; higher current boosts reject.</summary>
    internal static int MagicBase(int sample,int sourceR2,int signedBoostDelta,uint defenderRatio)
    {
        if (sample is <0 or >ushort.MaxValue || sourceR2<0 || defenderRatio is <1000 or >65000)
            throw new ArgumentOutOfRangeException(nameof(sample));
        if (signedBoostDelta>=2900) throw new InvalidOperationException("Current boost reaches unresolved retail cap selector");
        int boost = Math.Max(0,signedBoostDelta);
        boost = Math.Max(0,F.Cvtt(F.Div(boost,F.Div(defenderRatio,1000f))));
        int quotient = boost/1000;
        return Math.Max(0,F.Cvtt(F.Add(F.Mul(sample,F.Mul(F.Add(F.Div(sourceR2,100f),1f),F.Add(quotient,1f))),0f)));
    }

    private sealed record ChildBinding(Effect Parent,int ParentId,int ChildId,Npc Owner,Creature Target,
        long OwnerLife,long TargetLife,string Generation);
    private static readonly ConditionalWeakTable<Effect,ChildBinding> Children=new();
    internal static bool HasOwnedChild(Effect child,int parentId,int childId)=>Children.TryGetValue(child,out var b)&&
        b.ParentId==parentId&&b.ChildId==childId&&child.GetSkillId()==childId&&child.GetSkillLevel()==1&&
        ReferenceEquals(child.GetEffector(),b.Owner)&&ReferenceEquals(child.GetEffected(),b.Target);
    internal static bool IsOwnedChild(Effect child,int parentId,int childId)
    {
        return Children.TryGetValue(child,out var b)&&b.ParentId==parentId&&b.ChildId==childId&&
            child.IsSubEffect()&&child.GetSkillId()==childId&&child.GetSkillLevel()==1&&
            ReferenceEquals(child.GetEffector(),b.Owner)&&ReferenceEquals(child.GetEffected(),b.Target)&&
            b.Owner.GetController().RetailLifeEpoch==b.OwnerLife&&b.Target.GetController().RetailLifeEpoch==b.TargetLife&&
            SkyTempleNpcProfileCatalog.TryGet(b.Owner,out var profile)&&profile.Generation==b.Generation&&b.Parent.IsInSuccessEffects(1);
    }
    internal static bool TryPrepareChild(Effect parent,EffectTemplate template,out bool proceed)
    {
        proceed=false;
        if(parent.GetEffector() is not Npc npc||!SkyTempleNpcProfileCatalog.TryGet(npc,out var p))return false;
        if(parent.GetSkillId() is not(17039 or 16794))return false;
        int child=parent.GetSkillId()==17039?8217:8223;
        if(template.GetPosition()!=1||template.GetSubEffect()?.GetSkillId()!=child||parent.GetEffected() is not Player target||
            !ReferenceEquals(parent.GetSkillTemplate().GetEffectTemplate(1),template)||
            !p.Skills.Any(x=>x.Id48==parent.GetSkillId()&&x.Level==parent.GetSkillLevel()))
            throw new InvalidOperationException("Unbound selected secondary effect producer");
        proceed=parent.IsInSuccessEffects(1)&&parent.GetReserveds(1).GetValue()>0;
        if(proceed&&parent.GetSkillId()==16794)
            proceed=SkyTempleNpcPhysicalSkills.Direction(16794,target.GetHeading(),npc.GetX(),npc.GetY(),target.GetX(),target.GetY());
        return true;
    }
    internal static void BindChild(Effect parent,Effect child,EffectTemplate template)
    {
        if(!TryPrepareChild(parent,template,out bool proceed))return;
        if(!proceed)throw new InvalidOperationException("Attempted to construct unreached selected child");
        var npc=(Npc)parent.GetEffector();SkyTempleNpcProfileCatalog.TryGet(npc,out var profile);
        if(!child.IsSubEffect()||child.GetSkillId()!=template.GetSubEffect()!.GetSkillId()||child.GetSkillLevel()!=1||
            !ReferenceEquals(child.GetEffector(),npc)||!ReferenceEquals(child.GetEffected(),parent.GetEffected()))
            throw new InvalidOperationException("Selected child actual owner/target differs");
        Children.Add(child,new(parent,parent.GetSkillId(),child.GetSkillId(),npc,parent.GetEffected(),
            npc.GetController().RetailLifeEpoch,parent.GetEffected().GetController().RetailLifeEpoch,profile.Generation));
    }
    private static bool CalculateOwnedChild(Effect effect,EffectTemplate template,SpellStatus? status,out bool success)
    {
        success=false;bool spin=IsOwnedChild(effect,16794,8223);
        if(template.GetPosition()!=1||(spin?template is not SpinEffect:template is not StaggerEffect)||effect.GetEffected() is not Player target)
            throw new InvalidOperationException("Selected child kind/current target differs");
        var resistance=spin?StatEnum.SPIN_RESISTANCE:StatEnum.STAGGER_RESISTANCE;
        var penetration=spin?StatEnum.SPIN_RESISTANCE_PENETRATION:StatEnum.STAGGER_RESISTANCE_PENETRATION;
        int resist=checked(target.GetGameStats().GetResistance(resistance).GetCurrent()+target.GetGameStats().GetAbnormalResistance().GetCurrent());
        int threshold=SkyTempleNpcAbnormalEffects.StatusThreshold(resist,effect.GetEffector().GetGameStats().GetStat(penetration,0).GetCurrent(),SkyTempleCombatInputs.Pose(target));
        // Actual native class comparison only: no extra physical/magical dodge or critical.
        if(SkyTempleNpcCombat.Draw(1,1000)<=threshold)return true;
        effect.AddSuccessEffect(template);if(status.HasValue)effect.SetSpellStatus(status.Value);
        success=true;return true;
    }

    private static byte Pose(Player player) => (player.GetState()&15)==5 ? (byte)5 : (byte)0;
}
