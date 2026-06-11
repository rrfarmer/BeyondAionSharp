using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/TargetStatusProperty (kecimis). Static set: RI_PROTECTIONCURTAIN stack→true; removeIf targets lacking any required abnormal state (stream anyMatch→Any); return targets contains firstTarget. hasAnyAbnormalState: any state isAbnormalSet. Properties/AbnormalState red-tolerated.</summary>
[XmlType("TargetStatusProperty")]
public class TargetStatusProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result, SkillTemplate skillTemplate)
    {
        // TODO find out why skill 2504-2506 ("Protective Shell") has target_status="STUN STAGGER STUMBLE SPIN OPENAERIAL"
        if (skillTemplate.GetStack().Equals("RI_PROTECTIONCURTAIN"))
            return true;

        result.GetTargets().RemoveAll(effected => !HasAnyAbnormalState(effected, properties.GetTargetStatus()));

        // if first target was filtered out (= he had no required abnormal state), the skill cannot be cast
        return result.GetTargets().Contains(result.GetFirstTarget());
    }

    private static bool HasAnyAbnormalState(Creature creature, List<AbnormalState> states)
    {
        return states.Any(state => creature.GetEffectController().IsAbnormalSet(state));
    }
}
