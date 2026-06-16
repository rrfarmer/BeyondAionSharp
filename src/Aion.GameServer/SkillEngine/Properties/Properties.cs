using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/Properties (ATracer). @XmlAttribute fields (first_target/range/awr/target_relation/type/distance/maxcount/status[List&lt;AbnormalState&gt; space-sep]/revision_distance/effective_*/direction/target_species/ineffective_range); validate→FirstTargetProperty+FirstTargetRangeProperty+validateEffectedList; endCastValidate→clear+CAST_END range; validateEffectedList orchestrates TargetRange/TargetRelation/TargetStatus/TargetSpecies/MaxCount property helpers; nested CastState enum + ValidationResult (outer sets private `valid`, allowed in C#). Helper enums/TargetSpeciesProperty red-tolerated.</summary>
[XmlType("Properties")]
public class Properties
{
    [XmlAttribute("first_target")]
    public FirstTargetAttribute firstTarget;

    [XmlAttribute("first_target_range")]
    public int firstTargetRange;

    [XmlAttribute("awr")]
    public bool addWeaponRange;

    [XmlAttribute("target_relation")]
    public TargetRelationAttribute targetRelation;

    [XmlAttribute("target_type")]
    public TargetRangeAttribute targetType;

    [XmlAttribute("target_distance")]
    public int targetDistance;

    [XmlAttribute("target_maxcount")]
    public int targetMaxCount;

    // Java parity: @XmlAttribute("target_status") @XmlList List<AbnormalState> (space-separated enum tokens).
    // XmlSerializer cannot bind a List<T> as an attribute, so round-trip via a string proxy that splits on
    // whitespace and parses each token (null when the attribute is absent).
    [XmlIgnore]
    private List<AbnormalState> targetStatus;

    [XmlAttribute("target_status")]
    public string TargetStatusRaw
    {
        get => targetStatus == null ? null : string.Join(" ", targetStatus);
        set
        {
            if (value == null)
            {
                targetStatus = null;
                return;
            }
            targetStatus = new List<AbnormalState>();
            foreach (var tok in value.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
                if (System.Enum.TryParse<AbnormalState>(tok, out var v))
                    targetStatus.Add(v);
        }
    }

    [XmlAttribute("revision_distance")]
    public int revisionDistance;

    [XmlAttribute("effective_range")]
    public int effectiveRange;

    [XmlAttribute("effective_altitude")]
    public int effectiveAltitude;

    [XmlAttribute("effective_angle")]
    public int effectiveAngle;

    [XmlAttribute("effective_dist")]
    public int effectiveDist;

    [XmlAttribute("direction")]
    public AreaDirections direction = AreaDirections.NONE;

    [XmlAttribute("target_species")]
    public TargetSpeciesAttribute targetSpecies;

    [XmlAttribute("ineffective_range")]
    public int ineffectiveRange;

    public bool Validate(Skill skill, CastState castState)
    {
        if (firstTarget != null)
        {
            if (!FirstTargetProperty.Set(skill, this))
            {
                return false;
            }
        }
        if (firstTargetRange != 0 || addWeaponRange)
        {
            if (!FirstTargetRangeProperty.Set(skill, this, castState))
            {
                return false;
            }
        }
        return ValidateEffectedList(skill);
    }

    public bool EndCastValidate(Skill skill)
    {
        Creature firstTarget = skill.GetFirstTarget();
        skill.GetEffectedList().Clear();
        skill.GetEffectedList().Add(firstTarget);

        if (firstTargetRange != 0)
        {
            if (!FirstTargetRangeProperty.Set(skill, this, CastState.CAST_END))
            {
                return false;
            }
        }
        return ValidateEffectedList(skill);
    }

    private bool ValidateEffectedList(Skill skill)
    {
        ValidationResult result = ValidateEffectedList(skill.GetEffectedList(), skill.GetFirstTarget(), skill.GetEffector(), skill.GetSkillTemplate(), skill.GetX(), skill.GetY(), skill.GetZ());
        skill.SetFirstTarget(result.GetFirstTarget());
        return result.IsValid();
    }

    public ValidationResult ValidateEffectedList(List<Creature> targets, Creature firstTarget, Creature effector, SkillTemplate skillTemplate, float x,
        float y, float z)
    {
        ValidationResult result = new ValidationResult(targets, firstTarget);
        if (targetType != null && !TargetRangeProperty.Set(this, result, effector, skillTemplate, x, y, z))
            return result;
        if (targetRelation != null && !TargetRelationProperty.Set(this, result, effector, skillTemplate))
            return result;
        if (targetStatus != null && !TargetStatusProperty.Set(this, result, skillTemplate))
            return result;
        if (targetSpecies != null && !TargetSpeciesProperty.Set(this, result))
            return result;
        if (targetType != null && !MaxCountProperty.Set(this, result))
            return result;
        result.valid = true;
        return result;
    }

    public FirstTargetAttribute GetFirstTarget()
    {
        return firstTarget;
    }

    public int GetFirstTargetRange()
    {
        return firstTargetRange;
    }

    public bool IsAddWeaponRange()
    {
        return addWeaponRange;
    }

    public TargetRelationAttribute GetTargetRelation()
    {
        return targetRelation;
    }

    public TargetRangeAttribute GetTargetType()
    {
        return targetType;
    }

    public int GetTargetDistance()
    {
        return targetDistance;
    }

    public int GetTargetMaxCount()
    {
        return targetMaxCount;
    }

    public List<AbnormalState> GetTargetStatus()
    {
        return targetStatus;
    }

    public int GetRevisionDistance()
    {
        return revisionDistance;
    }

    public int GetEffectiveRange()
    {
        return effectiveRange;
    }

    public int GetEffectiveAltitude()
    {
        return effectiveAltitude;
    }

    public int GetEffectiveDist()
    {
        return effectiveDist;
    }

    public int GetEffectiveAngle()
    {
        return effectiveAngle;
    }

    public AreaDirections GetDirection()
    {
        return direction;
    }

    public TargetSpeciesAttribute GetTargetSpecies()
    {
        return targetSpecies;
    }

    public enum CastState
    {
        CAST_START,
        CAST_END,
    }

    public int GetIneffectiveRange()
    {
        return ineffectiveRange;
    }

    public class ValidationResult
    {
        private readonly List<Creature> targets;
        private Creature firstTarget;
        internal bool valid;

        public ValidationResult(List<Creature> targets, Creature firstTarget)
        {
            this.targets = targets;
            this.firstTarget = firstTarget;
        }

        public List<Creature> GetTargets()
        {
            return targets;
        }

        public Creature GetFirstTarget()
        {
            return firstTarget;
        }

        public void SetFirstTarget(Creature firstTarget)
        {
            this.firstTarget = firstTarget;
        }

        public bool IsValid()
        {
            return valid;
        }
    }
}
