using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Retail;
using Aion.GameServer.Model.Templates.Items;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils.Stats;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Reverse direction. Retains actual4.8 player weapons/skills/items and their
/// registered functions; applies matched NPC status/level/ratio/defense rules at the
/// damage consumer. It never changes PvP or an unmatched NPC.</summary>
internal static class SkyTemplePlayerCombat
{
    private static readonly ILogger Log=AionLog.For(nameof(SkyTemplePlayerCombat));
    private static readonly ConditionalWeakTable<Npc,HashSet<string>> IncomingWarnings=new();
    private static void IncomingCompatibility(Npc target,string reason)
    {
        var warnings=IncomingWarnings.GetValue(target,_=>[]);
        lock(warnings)
            if(warnings.Add(reason))Log.LogWarning("RETAIL_STAT_INCOMING_COMPAT npc={Npc} reason={Reason}; retained4.8 scalar recipe input",target.GetNpcId(),reason);
    }
    internal static bool Match(Creature source,Creature target,out SkyTempleNpcProfile profile)
    {
        profile=null!;
        return source is Player && target is Npc npc && source.GetWorldId()==target.GetWorldId()
            && source.GetInstanceId()==target.GetInstanceId() && RetailNpcStatCatalog.TryGetCombatProfile(npc,out profile);
    }
    private static RetailNpcNormalScope Scope(SkyTempleNpcProfile p) => RetailNpcNormalScope.Create(
        RetailNpcNormalScope.MainBinarySha256,RetailNpcNormalActorKind.Player,RetailNpcNormalActorKind.OrdinaryOwnerlessNpc,0,p.NpcId);
    private static RetailNpcNormalStatusMath.Context Context(SkyTempleNpcProfile p) => RetailNpcNormalStatusMath.Context.Create(
        Scope(p),RetailNpcNormalScope.MainBinarySha256,"retained4.8 player stats + current selected NPC construction",false);

    internal static bool TryNormal(Creature source,Creature target,ISet<CalculationType> types,
        out List<AttackResult> result,out SkillElement element)
    {
        result=null!;element=SkillElement.NONE;
        if(!Match(source,target,out var profile))return false;
        ((Npc)target).GetObserveController().PreflightSkyTempleProtection((Player)source);
        return RetailNpcPlayerNormal.TryCalculate((Player)source,(Npc)target,profile,types,out result,out element);
    }

    internal static float Defense(Player source,Npc target,SkyTempleNpcProfile p,SkillElement element,float amount)
    {
        var scope=Scope(p);
        if(element==SkillElement.NONE)
            return RetailNpcNormalDamageStages.CalculatePhysicalDefense(scope,amount,source.GetLevel(),p.Level,false,
                SkyTempleCombatInputs.Unsigned(target.GetGameStats().GetPDef().GetCurrent(),"NPC PDef"),0,(uint)p.StatRatio).Damage;
        int resistance=target.GetGameStats().GetResistance(element.GetStatForElement()!.Value).GetCurrent();
        return RetailNpcNormalDamageStages.CalculateElementalDefense(scope,amount,source.GetLevel(),p.Level,false,
            SkyTempleCombatInputs.Unsigned(target.GetGameStats().GetMDef().GetCurrent(),"NPC MDef"),0,
            SkyTempleCombatInputs.SignedWord(resistance,"NPC element"),0,1000,-1300).Damage;
    }
    internal static float ApplyPve(Player source,Npc target,SkyTempleNpcProfile p,bool physical,float amount)
    {var ratios=SkyTempleCombatInputs.Pve(source,target,physical);return amount*RetailNpcNormalDamageStages.CalculatePveRatio(Scope(p),ratios.Attack,ratios.Defense);}

    // Native trims are target-owned late fixed replacements, not attacker
    // bonuses or caps. Every nonzero value in the selected source is1. Preserve
    // misses/zero and leave outer protections and HP with their existing owners.
    internal static float TargetTrim(SkyTempleNpcProfile profile,bool physical,float amount) =>
        SkyTempleNpcCombat.TargetTrim(profile,physical,amount);

    internal static bool TryTargetTrim(Creature source,Creature target,bool physical,float amount,out float result)
    {
        result=amount;
        if(!Match(source,target,out var profile)||!profile.StatisticsOnly)return false;
        result=TargetTrim(profile,physical,amount);return true;
    }

    internal static bool TryCommonSkillMinimum(Creature source,Creature target,DamageEffect template,float amount,out float result)
    {
        result=amount;
        // Complete native common primary consumers: physical14036e8a5..ca
        // clamps its float to1; magical140370a21..2e clamps its integer to1.
        // This is after PvE and before target AI/trim/outer protection. Misses
        // never reach CalculateSkillResult. Other damage families are unproved.
        if(!IsCommonPrimary(template)
            ||!Match(source,target,out var profile)||!profile.StatisticsOnly)return false;
        result=Math.Max(1f,amount);return true;
    }

    private static bool IsCommonPrimary(EffectTemplate template) =>
        template.GetType()==typeof(SkillAttackInstantEffect)||template.GetType()==typeof(SpellAttackInstantEffect);

    private static bool Dodged(Player source,Npc target,SkyTempleNpcProfile p)
    {
        if(source.GetObserveController().CheckAttackerStatus(AttackStatus.DODGE)||target.GetObserveController().CheckAttackStatus(AttackStatus.DODGE))return true;
        var c=Context(p);
        int defense=RetailNpcNormalStatusMath.AdjustOwnerlessNpcDefense(c,RetailNpcNormalStatusMath.ChanceStage.Dodge,
            SkyTempleCombatInputs.Word(target.GetGameStats().GetEvasion().GetCurrent(),"NPC evasion"),source.GetLevel(),p.Level);
        int threshold=RetailNpcNormalStatusMath.CalculatePhysicalThreshold(c,RetailNpcNormalStatusMath.ChanceStage.Dodge,
            new(defense,source.GetGameStats().GetMainHandPAccuracy().GetCurrent(),0,1000));
        return SkyTempleNpcCombat.Draw(1,1000)<=threshold;
    }
    private static bool Resisted(Player source,Npc target,SkyTempleNpcProfile p)
    {
        if(target.GetObserveController().CheckAttackStatus(AttackStatus.RESIST))return true;
        int threshold=RetailNpcNormalStatusMath.CalculateMagicalResistThreshold(Context(p),new(
            target.GetGameStats().GetMResist().GetCurrent(),source.GetGameStats().GetMAccuracy().GetCurrent(),1000,source.GetLevel(),p.Level,0));
        return SkyTempleNpcCombat.Draw(1,1000)<=threshold;
    }
    private static AttackStatus PhysicalStatus(Player source,Npc target,SkyTempleNpcProfile p,int criticalPercent,bool skill)
    {
        var c=Context(p);AttackStatus status=AttackStatus.NORMALHIT;
        int accuracy=source.GetGameStats().GetMainHandPAccuracy().GetCurrent();
        foreach(var pair in new[]{(RetailNpcNormalStatusMath.ChanceStage.Block,AttackStatus.BLOCK,target.GetGameStats().GetBlock().GetCurrent()),
            (RetailNpcNormalStatusMath.ChanceStage.Parry,AttackStatus.PARRY,target.GetGameStats().GetParry().GetCurrent())})
        {
            if(target.GetObserveController().CheckAttackStatus(pair.Item2)){status=pair.Item2;break;}
            if(pair.Item3==0)continue;
            int adjusted=RetailNpcNormalStatusMath.AdjustOwnerlessNpcDefense(c,pair.Item1,SkyTempleCombatInputs.Word(pair.Item3,"NPC active defense"),source.GetLevel(),p.Level);
            int threshold=RetailNpcNormalStatusMath.CalculatePhysicalThreshold(c,pair.Item1,new(adjusted,accuracy,0,1000));
            if(SkyTempleNpcCombat.Draw(1,1000)<=threshold){status=pair.Item2;break;}
        }
        return Critical(source,target,p,true,criticalPercent,skill)?AttackStatusExtensions.GetCriticalStatusFor(status):status;
    }
    private static bool Critical(Player source,Npc target,SkyTempleNpcProfile p,bool physical,int percent,bool skill)
    {
        int critical=physical?source.GetGameStats().GetMainHandPCritical().GetCurrent():source.GetGameStats().GetMCritical().GetCurrent();
        critical=checked((int)(critical*(percent/100f)));
        var forced=source.GetObserveController().CheckAttackerCriticalStatus(AttackStatus.CRITICAL,skill);
        int draw=SkyTempleNpcCombat.Draw(1,1000);
        if(forced.IsResult()&&!forced.IsPercent())return draw<=forced.GetValue();
        if(forced.IsResult())critical=checked((int)(critical*(1f+forced.GetValue()/100f)));
        int reduction=physical?target.GetGameStats().GetPCR().GetCurrent():target.GetGameStats().GetMCR().GetCurrent();
        int threshold=Math.Min(500,(int)((critical-reduction)/(p.StatRatio/1000f)));
        return skill?draw<threshold:draw<=threshold;
    }

    internal static bool TryTargetCheck(Effect effect,EffectTemplate template,SpellStatus? spellStatus,out bool success)
    {
        success=false;
        if(template is not DamageEffect damage || !Match(effect.GetEffector(),effect.GetEffected(),out var p))return false;
        var source=(Player)effect.GetEffector();var target=(Npc)effect.GetEffected();
        bool physical=template.GetElement()==SkillElement.NONE;
        bool failed=!template.IsNoResist()&&(physical?Dodged(source,target,p):Resisted(source,target,p));
        if(failed){effect.ResetMagicalCritical();effect.SetAttackStatus(physical?AttackStatus.DODGE:AttackStatus.RESIST);return true;}
        if(!physical)
        {
            if(effect.TryGetCalculatedMagicalCritical(out _))effect.ReuseMagicalCritical(template.GetPosition());
            else effect.SetCalculatedMagicalCritical(template.GetPosition(),Critical(source,target,p,false,template.CalculateCritProbMod(effect),true));
        }
        effect.AddSuccessEffect(template);if(spellStatus.HasValue)effect.SetSpellStatus(spellStatus.Value);
        AttackUtil.CalculateSkillResult(effect,unchecked(template.GetValue()+template.GetDelta()*effect.GetSkillLevel()),damage,false);
        success=true;return true;
    }
    internal static bool TryPhysicalSkillStatus(Effect effect,DamageEffect template,out AttackStatus status)
    {
        status=default;
        if(template.GetElement()!=SkillElement.NONE||!Match(effect.GetEffector(),effect.GetEffected(),out var p))return false;
        status=PhysicalStatus((Player)effect.GetEffector(),(Npc)effect.GetEffected(),p,template.CalculateCritProbMod(effect),true);return true;
    }
    internal static bool TryPhysicalSkillDefense(Creature source,Creature target,float amount,out float result)
    {
        result=amount;
        if(!Match(source,target,out var p))return false;
        result=Defense((Player)source,(Npc)target,p,SkillElement.NONE,amount);return true;
    }
    internal static bool TryMagicalSkillBase(Creature source,Creature target,float baseDamage,int bonus,EffectTemplate template,
        bool boost,bool knowledge,bool spellBoost,out float result)
    {
        result=0;if(!Match(source,target,out var p))return false;
        int delta=boost?unchecked(source.GetGameStats().GetMBoost().GetCurrent()-target.GetGameStats().GetMBResist().GetCurrent()):0;
        int magicBoost;bool statisticsPrimary=p.StatisticsOnly&&IsCommonPrimary(template);
        if(statisticsPrimary)
        {
            int? cap=RetailNpcStatCatalog.MagicBoostCap;
            if(boost&&cap is null&&delta>=2900)
                throw new InvalidDataException("Selected NPC incoming spell requires its source-bound native boost-cap context");
            // Below2900 the two proved selectors agree. The source-bound
            // ordinary package selects3400; an unlimited Java stat upper cap
            // cannot replace the native clamp BEFORE ratio normalization.
            magicBoost=boost?RetailNpcStatSkillCombat.NormalizeSpellBoost(delta,(uint)p.StatRatio,cap??2900):0;
        }
        else
            magicBoost=Math.Max(0,StatCapUtil.ClampStatValue(StatEnum.BOOST_MAGICAL_SKILL,source,(int)(Math.Max(0,delta)/(p.StatRatio/1000f))));
        int kno=knowledge?source.GetGameStats().GetKnowledge().GetCurrent():100;
        bool nativeScalar=statisticsPrimary&&template is DamageEffect primary
            &&primary.GetMode()==Aion.GameServer.SkillEngine.Change.Func.ADD&&baseDamage==MathF.Truncate(baseDamage);
        if(statisticsPrimary&&!nativeScalar)IncomingCompatibility((Npc)target,"non-absolute-integer-player-skill-base");
        if(statisticsPrimary&&bonus!=0)IncomingCompatibility((Npc)target,"unverified-conditional-bonus-order");
        float amount=nativeScalar?RetailNpcStatSkillCombat.PlayerSpellBase(baseDamage,kno,magicBoost)
            :baseDamage*(magicBoost/1000f+kno/100f);
        if(spellBoost)amount=source.GetGameStats().GetStat(StatEnum.BOOST_SPELL_ATTACK,(int)amount).GetCurrent();
        amount+=bonus;
        result=Defense((Player)source,(Npc)target,p,template.GetElement(),amount);return true;
    }
    internal static bool TrySkillPve(Creature source,Creature target,bool physical,float amount,out float result,bool useTemplateDamage=false)
    {result=amount;if(useTemplateDamage||!Match(source,target,out var p))return false;result=ApplyPve((Player)source,(Npc)target,p,physical,amount);return true;}
}
