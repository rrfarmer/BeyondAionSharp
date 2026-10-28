using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Retail;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SkillEngine.Properties;
using Aion.GameServer.Model.Stats.Container;
using Func = Aion.GameServer.SkillEngine.Change.Func;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Read-only source and host screening for paired NPC drains. A match
/// does not authorize a cast: critical RNG, protection, caster eligibility,
/// atomic lifetime checks and dispatch still need their own owners.</summary>
internal static class RetailNpcStatNpcDrainPreflight
{
    internal sealed record Selection(Npc Caster,Npc Target,SkyTempleNpcProfile CasterProfile,
        SkyTempleNpcProfile TargetProfile,int SourceLevel,RetailNpcStatPeriodicCombat.Source Source,
        SpellAtkDrainInstantEffect Initial,SpellAtkDrainEffect Periodic,int CasterMaxHp,int TargetMaxHp,long CasterLife,long TargetLife);

    private static readonly HashSet<string> InitialFields=new(StringComparer.Ordinal)
    {"type","noresist","target_type","reserved1","reserved2","reserved3","reserved4","reserved5","reserved6",
        "reserved10","reserved11","reserved12","reserved14","reserved15","reserved16","reserved17","critical_prob_mod2","hop_type"};
    private static readonly HashSet<string> PeriodicFields=new(StringComparer.Ordinal)
    {"type","noresist","remain1","remain2","effectid","basiclv","checktime","target_type","cond_preeffect",
        "cond_preeffect_prob1","cond_preeffect_prob2","reserved7","reserved8","reserved9","reserved10","reserved11",
        "reserved12","reserved14","reserved15","critical_prob_mod2"};

    private static string? Field(IEnumerable<KeyValuePair<string,string>> fields,string name)
        => fields.SingleOrDefault(x=>x.Key==name).Value;
    private static bool Clean(EffectTemplate e) => e.EffectConditions is null && e.EffectSubConditions is null
        && e.Modifiers is null && e.SubEffectField is null && e.change is not {Count:>0}
        && e.AccMod1==0 && e.AccMod2==0 && e.CritProbMod1==0 && e.CritAddDmg1==0 && e.CritAddDmg2==0
        && e.SkillEfficiency==0 && e.MaxDamageChance==0 && e.MaxDamageDelta==0
        && e.Hittype==HitType.EVERYHIT && e.HitTypeProb==100 && e.RandomTime==0;

    // The periodic HP receipt cannot exceed the target's current HP. This is
    // a conservative snapshot check, not a reservation of either actor's HP:
    // later changes must be checked atomically by the eventual cast owner.
    // Native multiplication is signed IMUL32 and Heal2 adds before capping.
    internal static bool PeriodicHealFitsSnapshot(int casterMaxHp,int targetMaxHp,int hpPercent)
        => HealFitsSnapshot(casterMaxHp,targetMaxHp,hpPercent);

    // Initial drain healing uses the reserved first-hit value, not target HP.
    // This snapshot does not reserve the later delayed heal or actor state.
    internal static bool InitialHealFitsSnapshot(int casterMaxHp,int reservedDamage,int hpPercent)
        => reservedDamage==0 ? casterMaxHp>0 && hpPercent>=0 : HealFitsSnapshot(casterMaxHp,reservedDamage,hpPercent);

    private static bool HealFitsSnapshot(int casterMaxHp,int maximumDamage,int hpPercent)
    {
        if(casterMaxHp<=0 || maximumDamage<=0 || hpPercent<0)return false;
        if((long)maximumDamage*hpPercent>int.MaxValue)return false;
        int maximumHeal=RetailNpcStatSpellDrain.HealAmount(maximumDamage,hpPercent);
        return maximumHeal>=0 && (long)casterMaxHp+maximumHeal<=int.MaxValue;
    }

    internal static string? RecipeMismatch(RetailNpcStatPeriodicCombat.Source source,
        SpellAtkDrainInstantEffect initial,SpellAtkDrainEffect periodic,Properties? properties)
    {
        var first=source.InitialDrain;
        if(first is null || source.Parts.Length!=1 || source.Parts[0] is not { } part
            || part.Family!=RetailNpcPeriodicSpellParameters.Family.SpellDrain
            || part.UnsupportedReason!="unreviewed-coherent-instant-periodic-drain" || part.Position!=2 || part.Drain is not { } later)
            return "missing-paired-retail-drain-source";
        if(first.OrderedPrimaryFields.IsDefault || later.OrderedFields.IsDefault
            ||first.OrderedPrimaryFields.Any(x=>!x.Key.StartsWith("effect1_",StringComparison.Ordinal)||!InitialFields.Contains(x.Key[8..]))
            ||later.OrderedFields.Any(x=>!x.Key.StartsWith("effect2_",StringComparison.Ordinal)||!PeriodicFields.Contains(x.Key[8..])))
            return "unreviewed-drain-source-field";
        if(first.Value is not (>=0 and <=100000) || first.LevelRate!=0 || first.WeaponMode!=0
            || first.HpRate!=0 || first.HpPercent is not (>=0 and <=100000) || first.MpRate.GetValueOrDefault()!=0
            || first.MpPercent.GetValueOrDefault()!=0 || first.NoResist!=1 || first.CriticalPercent!=0
            || first.TargetType!="Target_Only" || first.FirstTarget!="Target" || first.TargetRange!="OnlyOne"
            || first.TargetRelation!="Friend" || first.TargetMaxCount!=1 || first.TargetSpecies!="All"
            || first.FirstTargetValidDistance is not >=0 || first.ApplyBoost!=1 || first.ApplyCritical!=1
            ||Field(first.OrderedPrimaryFields,"effect1_reserved3")!="0" ||Field(first.OrderedPrimaryFields,"effect1_reserved4")!="0"
            ||Field(first.OrderedPrimaryFields,"effect1_reserved5")!="0" ||Field(first.OrderedPrimaryFields,"effect1_reserved11")!="0"
            ||Field(first.OrderedPrimaryFields,"effect1_reserved12")!="100" ||Field(first.OrderedPrimaryFields,"effect1_hop_type")!="damage")
            return "unreviewed-initial-drain-source";
        SkillElement element=first.Element switch {"Earth"=>SkillElement.EARTH,"Fire"=>SkillElement.FIRE,
            "Water"=>SkillElement.WATER,"Air"=>SkillElement.WIND,_=>SkillElement.NONE};
        if(element==SkillElement.NONE || initial.Position!=1 || initial.Element!=element || initial.mode!=Func.ADD || initial.shared
            || initial.Delta!=0 || !initial.NoResist || initial.hpPercent!=first.HpPercent || initial.mpPercent!=0
            || initial.CritProbMod2!=0 || initial.Duration1!=0 || initial.Duration2!=0 || initial.BasicLvl!=0 || initial.Effectid!=0
            || initial.PreEffects is not null || initial.HopA!=0 || initial.HopB!=0
            || initial.Hoptype!=HopType.DAMAGE || !Clean(initial))return "initial-host-shape-differs";
        if(part.Value is <0 or >0xffffff || part.Delta!=0 || part.Interval<=0 || !part.ApplyBoost || !part.ApplyMagicalDefense
            ||later.Radius is not (>=0 and <=46340) || later.HpRate!=0 || later.HpPercent is not (>=0 and <=100000)
            ||later.MpRate.GetValueOrDefault()!=0 || later.MpPercent.GetValueOrDefault()!=0 || later.NoResist!=1
            ||later.Remain1!=0 || later.Remain2 is not >0 || later.BasicLevel is not >=0
            ||later.TargetType!="Target_Only" || later.PreEffect!="e1" || later.PreEffectProbability1!=0 || later.PreEffectProbability2!=100
            ||Field(later.OrderedFields,"effect2_reserved11")!="100" ||Field(later.OrderedFields,"effect2_reserved12")!="100")
            return "unreviewed-periodic-drain-source";
        if(periodic.Position!=2 || periodic.Element!=part.Element || periodic.percent || periodic.shared
            ||periodic.Delta!=0 || periodic.checktime!=part.Interval || !periodic.NoResist
            ||periodic.hpPercent!=later.HpPercent || periodic.mpPercent!=0 || periodic.PreEffects is not [1] || periodic.PreEffectProb!=100
            ||periodic.CritProbMod2!=part.CriticalPercent || periodic.RandomTime!=part.DurationJitter
            ||periodic.Hoptype is not null || periodic.HopA!=0 || periodic.HopB!=0
            ||periodic.Duration1!=later.Remain1 || periodic.Duration2!=later.Remain2 || periodic.BasicLvl!=later.BasicLevel
            ||later.EffectId is null || periodic.Effectid!=later.EffectId || !Clean(periodic))return "periodic-host-shape-differs";
        if(properties?.GetFirstTarget()!=FirstTargetAttribute.TARGET || properties.GetTargetType()!=TargetRangeAttribute.ONLYONE
            ||properties.GetTargetRelation()!=TargetRelationAttribute.FRIEND ||properties.GetTargetMaxCount()!=1
            ||properties.GetTargetSpecies() is not null ||properties.IsAddWeaponRange()
            ||properties.GetFirstTargetRange()!=first.FirstTargetValidDistance)
            return "host-target-shape-differs";
        return null;
    }

    internal static bool TryMatchInputs(Effect effect,out Selection selection,out string reason)
    {
        selection=null!;reason="retired-effect";
        if(effect.RetailHasEnded)return false;
        reason="already-calculated-effect";
        if(effect.GetSuccessEffects().Count!=0)return false;
        reason="actor-context";
        if(effect.IsForcedEffect() || effect.GetEffector() is not Npc caster || effect.GetEffected() is not Npc target
            ||ReferenceEquals(caster,target) || !caster.IsSpawned() || !target.IsSpawned() || caster.IsDead() || target.IsDead()
            ||caster.GetLifeStats().GetCurrentHp()<=0 ||target.GetLifeStats().GetCurrentHp()<=0
            ||!ReferenceEquals(caster.GetWorldMapInstance(),target.GetWorldMapInstance()))return false;
        if(!RetailNpcStatCatalog.TryGet(caster,out var cp) || !RetailNpcStatCatalog.TryGet(target,out var tp))return false;
        bool Materialized(Npc n,SkyTempleNpcProfile p,out int maximum)
        {
            maximum=0;
            if(n.GetGameStats().StatisticsProfile is not { } stats ||stats.Generation!=p.Generation
                ||stats.SourceRecordSha256!=p.SourceRecordSha256)return false;
            // The exact-integer marker survives the reviewed retail HP scalar
            // and its supported modifiers, but is invalidated by a later AI
            // override or an unreviewed host calculation. Do not require the
            // current maximum to equal the unbuffed source template.
            var hp=n.GetGameStats().GetMaxHp();
            if(!hp.HasUnchangedExactIntegerCurrent())return false;
            maximum=hp.GetCurrent();return maximum>0;
        }
        if(!Materialized(caster,cp,out int casterMaxHp)||!Materialized(target,tp,out int targetMaxHp))
        {reason="unreviewed-actor-stats-or-hp-override";return false;}
        if(RetailNpcStatEquipmentResponses.Required(cp) || target.GetObserveController().HasDrainProtectionObserver
            ||caster.GetObserveController().HasSkillCriticalObserver){reason="unreviewed-protection-or-observer";return false;}
        int? level=RetailNpcStatSkillLevels.GetDirectLevel(caster,effect.GetSkillId());
        if(level is not (>=0 and <=short.MaxValue) || !RetailNpcStatCatalog.TryGetPeriodic(cp,effect.GetSkillId(),level.Value,out var source))
        {reason="missing-source-level-or-binding";return false;}
        var effects=effect.GetEffectTemplates().ToArray();
        if(effects.Length!=2 || effects[0] is not SpellAtkDrainInstantEffect initial || effects[1] is not SpellAtkDrainEffect periodic
            ||effect.GetSkill() is not { } skill || skill.GetHitTime() is <0 or >ushort.MaxValue
            ||effect.GetSkillTemplate().GetActivationAttribute()!=ActivationAttribute.ACTIVE
            ||!effect.GetSkillTemplate().IsApplyMagicalSkillBoostBonus() || !effect.GetSkillTemplate().IsApplyMagicalCritical())
        {reason="host-skill-shape-differs";return false;}
        if(!ReferenceEquals(skill.GetFirstTarget(),target)){reason="host-first-target-differs";return false;}
        reason=RecipeMismatch(source,initial,periodic,effect.GetSkillTemplate().GetProperties()) ?? "matched-inputs-not-enabled";
        if(reason!="matched-inputs-not-enabled")return false;
        if(OneTimeBoostSkillAttackEffect.GetActiveBoost(caster,initial) is not null)
        {reason="unreviewed-one-time-boost";return false;}
        if(cp.StatRatio is <1000 or >65000 || tp.StatRatio is <1000 or >65000 ||tp.MagicalDamageTrim<0
            ||target.GetGameStats().GetMDef().GetCurrent()<0)
        {reason="unreviewed-numeric-domain";return false;}
        if(caster.GetLifeStats().GetCurrentHp()>casterMaxHp || target.GetLifeStats().GetCurrentHp()>targetMaxHp
            || source.Parts[0].Drain?.HpPercent is not { } periodicPercent
            || !PeriodicHealFitsSnapshot(casterMaxHp,targetMaxHp,periodicPercent))
        {reason="unreviewed-periodic-drain-heal-overflow";return false;}
        selection=new(caster,target,cp,tp,level.Value,source,initial,periodic,casterMaxHp,targetMaxHp,
            caster.GetController().RetailLifeEpoch,target.GetController().RetailLifeEpoch);
        return true;
    }
}
