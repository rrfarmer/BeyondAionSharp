using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/Effects (ATracer). @XmlType("Effects"); JAXB @XmlElements polymorphic list→repeated [XmlElement(name, typeof(T))] on `effects`; @XmlTransient effectTypes; afterUnmarshal→AfterUnmarshal(object) building EnumSet→HashSet&lt;EffectType&gt; from class simple name (strip "Effect", upper) via Enum.Parse, IllegalArgumentException→ArgumentException; getEffects; hasAnyEffectType(params). All effect subtypes red-tolerated until converged.</summary>
[XmlType("Effects")]
public class Effects
{
    [XmlElement("root", typeof(RootEffect))]
    [XmlElement("buf", typeof(BufEffect))]
    [XmlElement("spellatk", typeof(SpellAttackEffect))]
    [XmlElement("deform", typeof(DeformEffect))]
    [XmlElement("shapechange", typeof(ShapeChangeEffect))]
    [XmlElement("polymorph", typeof(PolymorphEffect))]
    [XmlElement("poison", typeof(PoisonEffect))]
    [XmlElement("stun", typeof(StunEffect))]
    [XmlElement("sleep", typeof(SleepEffect))]
    [XmlElement("bleed", typeof(BleedEffect))]
    [XmlElement("hide", typeof(HideEffect))]
    [XmlElement("search", typeof(SearchEffect))]
    [XmlElement("statup", typeof(StatupEffect))]
    [XmlElement("statdown", typeof(StatdownEffect))]
    [XmlElement("statboost", typeof(StatboostEffect))]
    [XmlElement("weaponstatboost", typeof(WeaponStatboostEffect))]
    [XmlElement("wpnmastery", typeof(WeaponMasteryEffect))]
    [XmlElement("snare", typeof(SnareEffect))]
    [XmlElement("absolutesnare", typeof(AbsoluteSnareEffect))]
    [XmlElement("slow", typeof(SlowEffect))]
    [XmlElement("absoluteslow", typeof(AbsoluteSlowEffect))]
    [XmlElement("stumble", typeof(StumbleEffect))]
    [XmlElement("spin", typeof(SpinEffect))]
    [XmlElement("stagger", typeof(StaggerEffect))]
    [XmlElement("openaerial", typeof(OpenAerialEffect))]
    [XmlElement("closeaerial", typeof(CloseAerialEffect))]
    [XmlElement("shield", typeof(ShieldEffect))]
    [XmlElement("mpshield", typeof(MPShieldEffect))]
    [XmlElement("bind", typeof(BindEffect))]
    [XmlElement("dispel", typeof(DispelEffect))]
    [XmlElement("skillatk", typeof(SkillAttackInstantEffect))]
    [XmlElement("spellatkinstant", typeof(SpellAttackInstantEffect))]
    [XmlElement("dash", typeof(DashEffect))]
    [XmlElement("backdash", typeof(BackDashEffect))]
    [XmlElement("delaydamage", typeof(DelayedSpellAttackInstantEffect))]
    [XmlElement("return", typeof(ReturnEffect))]
    [XmlElement("healinstant", typeof(HealInstantEffect))]
    [XmlElement("mphealinstant", typeof(MPHealInstantEffect))]
    [XmlElement("dphealinstant", typeof(DPHealInstantEffect))]
    [XmlElement("fphealinstant", typeof(FPHealInstantEffect))]
    [XmlElement("prochealinstant", typeof(ProcHealInstantEffect))]
    [XmlElement("procmphealinstant", typeof(ProcMPHealInstantEffect))]
    [XmlElement("procdphealinstant", typeof(ProcDPHealInstantEffect))]
    [XmlElement("procfphealinstant", typeof(ProcFPHealInstantEffect))]
    [XmlElement("carvesignet", typeof(CarveSignetEffect))]
    [XmlElement("signet", typeof(SignetEffect))]
    [XmlElement("signetburst", typeof(SignetBurstEffect))]
    [XmlElement("silence", typeof(SilenceEffect))]
    [XmlElement("curse", typeof(CurseEffect))]
    [XmlElement("blind", typeof(BlindEffect))]
    [XmlElement("boosthate", typeof(BoostHateEffect))]
    [XmlElement("hostileup", typeof(HostileUpEffect))]
    [XmlElement("paralyze", typeof(ParalyzeEffect))]
    [XmlElement("confuse", typeof(ConfuseEffect))]
    [XmlElement("dispeldebuffphysical", typeof(DispelDebuffPhysicalEffect))]
    [XmlElement("dispeldebuffmental", typeof(DispelDebuffMentalEffect))]
    [XmlElement("dispeldebuff", typeof(DispelDebuffEffect))]
    [XmlElement("alwaysdodge", typeof(AlwaysDodgeEffect))]
    [XmlElement("alwaysparry", typeof(AlwaysParryEffect))]
    [XmlElement("alwaysresist", typeof(AlwaysResistEffect))]
    [XmlElement("alwaysblock", typeof(AlwaysBlockEffect))]
    [XmlElement("switchhpmp", typeof(SwitchHpMpEffect))]
    [XmlElement("summon", typeof(SummonEffect))]
    [XmlElement("aura", typeof(AuraEffect))]
    [XmlElement("resurrect", typeof(ResurrectEffect))]
    [XmlElement("returnpoint", typeof(ReturnPointEffect))]
    [XmlElement("provoker", typeof(ProvokerEffect))]
    [XmlElement("reflector", typeof(ReflectorEffect))]
    [XmlElement("spellatkdraininstant", typeof(SpellAtkDrainInstantEffect))]
    [XmlElement("procatk_instant", typeof(ProcAtkInstantEffect))]
    [XmlElement("onetimeboostskillattack", typeof(OneTimeBoostSkillAttackEffect))]
    [XmlElement("armormastery", typeof(ArmorMasteryEffect))]
    [XmlElement("weaponstatup", typeof(WeaponStatupEffect))]
    [XmlElement("boostskillcastingtime", typeof(BoostSkillCastingTimeEffect))]
    [XmlElement("summontrap", typeof(SummonTrapEffect))]
    [XmlElement("summongroupgate", typeof(SummonGroupGateEffect))]
    [XmlElement("summonservant", typeof(SummonServantEffect))]
    [XmlElement("skillatkdraininstant", typeof(SkillAtkDrainInstantEffect))]
    [XmlElement("petorderuseultraskill", typeof(PetOrderUseUltraSkillEffect))]
    [XmlElement("boostheal", typeof(BoostHealEffect))]
    [XmlElement("dispelbuff", typeof(DispelBuffEffect))]
    [XmlElement("skilllauncher", typeof(SkillLauncherEffect))]
    [XmlElement("pulled", typeof(PulledEffect))]
    [XmlElement("fear", typeof(FearEffect))]
    [XmlElement("movebehind", typeof(MoveBehindEffect))]
    [XmlElement("rebirth", typeof(RebirthEffect))]
    [XmlElement("boostskillcost", typeof(BoostSkillCostEffect))]
    [XmlElement("protect", typeof(ProtectEffect))]
    [XmlElement("magiccounteratk", typeof(MagicCounterAtkEffect))]
    [XmlElement("randommoveloc", typeof(RandomMoveLocEffect))]
    [XmlElement("recallinstant", typeof(RecallInstantEffect))]
    [XmlElement("summonhoming", typeof(SummonHomingEffect))]
    [XmlElement("dispelbuffcounteratk", typeof(DispelBuffCounterAtkEffect))]
    [XmlElement("xpboost", typeof(XPBoostEffect))]
    [XmlElement("onetimeboostheal", typeof(OneTimeBoostHealEffect))]
    [XmlElement("fpatk", typeof(FpAttackEffect))]
    [XmlElement("deboostheal", typeof(DeboostHealEffect))]
    [XmlElement("fpatkinstant", typeof(FpAttackInstantEffect))]
    [XmlElement("summonskillarea", typeof(SummonSkillAreaEffect))]
    [XmlElement("mpattackinstant", typeof(MpAttackInstantEffect))]
    [XmlElement("onetimeboostskillcritical", typeof(OneTimeBoostSkillCriticalEffect))]
    [XmlElement("resurrectpos", typeof(ResurrectPositionalEffect))]
    [XmlElement("nofly", typeof(NoFlyEffect))]
    [XmlElement("healcastoronatk", typeof(HealCastorOnAttackedEffect))]
    [XmlElement("wpndual", typeof(WeaponDualEffect))]
    [XmlElement("resurrectbase", typeof(ResurrectBaseEffect))]
    [XmlElement("switchhostile", typeof(SwitchHostileEffect))]
    [XmlElement("invulnerablewing", typeof(InvulnerableWingEffect))]
    [XmlElement("shieldmastery", typeof(ShieldMasteryEffect))]
    [XmlElement("simpleroot", typeof(SimpleRootEffect))]
    [XmlElement("dptransfer", typeof(DPTransferEffect))]
    [XmlElement("mpattack", typeof(MpAttackEffect))]
    [XmlElement("boostdroprate", typeof(BoostDropRateEffect))]
    [XmlElement("spellatkdrain", typeof(SpellAtkDrainEffect))]
    [XmlElement("extendedaurarange", typeof(ExtendAuraRangeEffect))]
    [XmlElement("changehateonattacked", typeof(ChangeHateOnAttackedEffect))]
    [XmlElement("healcastorontargetdead", typeof(HealCastorOnTargetDeadEffect))]
    [XmlElement("noreducespellatk", typeof(NoReduceSpellATKInstantEffect))]
    [XmlElement("condskilllauncher", typeof(CondSkillLauncherEffect))]
    [XmlElement("fall", typeof(FallEffect))]
    [XmlElement("evade", typeof(EvadeEffect))]
    [XmlElement("buffbind", typeof(BuffBindEffect))]
    [XmlElement("buffsilence", typeof(BuffSilenceEffect))]
    [XmlElement("buffsleep", typeof(BuffSleepEffect))]
    [XmlElement("buffstun", typeof(BuffStunEffect))]
    [XmlElement("heal", typeof(HealEffect))]
    [XmlElement("mpheal", typeof(MPHealEffect))]
    [XmlElement("fpheal", typeof(FPHealEffect))]
    [XmlElement("dpheal", typeof(DPHealEffect))]
    [XmlElement("summontotem", typeof(SummonTotemEffect))]
    [XmlElement("disease", typeof(DiseaseEffect))]
    [XmlElement("boostspellattack", typeof(BoostSpellAttackEffect))]
    [XmlElement("noresurrectpenalty", typeof(NoResurrectPenaltyEffect))]
    [XmlElement("hipass", typeof(HiPassEffect))]
    [XmlElement("nodeathpenalty", typeof(NoDeathPenaltyEffect))]
    [XmlElement("caseheal", typeof(CaseHealEffect))]
    [XmlElement("summonhousegate", typeof(SummonHouseGateEffect))]
    [XmlElement("summonbindinggroupgate", typeof(SummonBindingGroupGateEffect))]
    [XmlElement("procvphealinstant", typeof(ProcVPHealInstantEffect))]
    [XmlElement("convertheal", typeof(ConvertHealEffect))]
    [XmlElement("sanctuary", typeof(SanctuaryEffect))]
    [XmlElement("subtypeboostresist", typeof(SubTypeBoostResistEffect))]
    [XmlElement("subtypeextendduration", typeof(SubTypeExtendDurationEffect))]
    [XmlElement("dispelnpcbuff", typeof(DispelNpcBuffEffect))]
    [XmlElement("dispelnpcdebuff", typeof(DispelNpcDebuffEffect))]
    [XmlElement("deathblow", typeof(DeathBlowEffect))]
    [XmlElement("delayedskill", typeof(DelayedSkillEffect))]
    [XmlElement("delayedfpatk_instant", typeof(DelayedFpAtkInstantEffect))]
    [XmlElement("drboost", typeof(DRBoostEffect))]
    [XmlElement("apboost", typeof(APBoostEffect))]
    [XmlElement("skillxpboost", typeof(SkillXPBoostEffect))]
    [XmlElement("summonfunctionalnpc", typeof(SummonFunctionalNpcEffect))]
    [XmlElement("targetteleport", typeof(TargetTeleportEffect))]
    [XmlElement("flyoff", typeof(FlyoffEffect))]
    [XmlElement("escape", typeof(EscapeEffect))]
    [XmlElement("skillcooltimereset", typeof(SkillCooltimeResetEffect))]
    [XmlElement("riderobot", typeof(RideRobotEffect))]
    [XmlElement("absstatbuff", typeof(AbsoluteStatToPCBuffEffect))]
    [XmlElement("absstatdebuff", typeof(AbsoluteStatToPCDebuffEffect))]
    [XmlElement("petrification", typeof(PetrificationEffect))]
    [XmlElement("absexppointhealinstant", typeof(AbsoluteEXPPointHealInstantEffect))]
    [XmlElement("limitedreduceDamage", typeof(LimitedReduceDamageEffect))]
    [XmlElement("activateenslave", typeof(ActivateEnslaveEffect))]
    [XmlElement("petorderunsummon", typeof(PetOrderUnSummonEffect))]
    [XmlElement("supportevent", typeof(SupportEventEffect))]
    [XmlElement("targetchange", typeof(TargetChangeEffect))]
    [XmlElement("dummy", typeof(DummyEffect))]
    [XmlElement("alwayshit", typeof(AlwaysHitEffect))]
    [XmlElement("alwaysnoresist", typeof(AlwaysNoResistEffect))]
    [XmlElement("utility", typeof(UtilityEffect))]
    public List<EffectTemplate> effects;

    [XmlIgnore]
    private HashSet<EffectType> effectTypes;

    [XmlIgnore]
    private readonly HashSet<EffectType> possibleConflictEffectTypes = new HashSet<EffectType>();

    private static readonly HashSet<EffectType> CONFLICT_TYPES = new HashSet<EffectType>
    {
        EffectType.SHIELD,
        EffectType.PROTECT,
        EffectType.REFLECTOR
    };

    public void AfterUnmarshal(object parent)
    {
        effectTypes = new HashSet<EffectType>();
        foreach (EffectTemplate et in effects)
        {
            string effectName = et.GetType().Name.Replace("Effect", "").ToUpper();
            try
            {
                EffectType effectType = Enum.Parse<EffectType>(effectName);
                effectTypes.Add(effectType);
                if (CONFLICT_TYPES.Contains(effectType))
                    possibleConflictEffectTypes.Add(effectType);
            }
            catch (Exception)
            {
                throw new ArgumentException("Missing EffectType " + effectName + " for: " + et.GetType());
            }
        }
    }

    public List<EffectTemplate> GetEffects()
    {
        return effects;
    }

    public HashSet<EffectType> GetPossibleConflictEffectTypes()
    {
        return possibleConflictEffectTypes;
    }

    public bool HasAnyEffectType(params EffectType[] types)
    {
        foreach (EffectType effectType in types)
        {
            if (effectTypes.Contains(effectType))
                return true;
        }
        return false;
    }
}
