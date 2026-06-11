using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/MaxCountProperty (MrPoke, Neon). Static set(properties, result): maxCount==0 || targets&lt;=maxCount→true; switch AREA/PARTY/PARTY_WITHPET (fallthrough): null firstTarget→false; nearest maxCount by distance (stream sorted/limit/collect→OrderBy/Take/ToHashSet); PARTY_WITHPET→add summons; retainAll→RemoveAll(!contains). Properties/enums red-tolerated.</summary>
public class MaxCountProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result)
    {
        TargetRangeAttribute value = properties.GetTargetType();
        int maxCount = properties.GetTargetMaxCount();
        if (maxCount == 0 || result.GetTargets().Count <= maxCount)
            return true;

        switch (value)
        {
            case TargetRangeAttribute.AREA:
            case TargetRangeAttribute.PARTY:
            case TargetRangeAttribute.PARTY_WITHPET:
                if (result.GetFirstTarget() == null)
                    return false;

                HashSet<Creature> nearestCreatures = result.GetTargets()
                    .OrderBy(c => PositionUtil.GetDistance(result.GetFirstTarget(), c))
                    .Take(maxCount)
                    .ToHashSet();

                // rebuild effected list with correct number of creatures and their summons
                if (value == TargetRangeAttribute.PARTY_WITHPET)
                {
                    foreach (Creature creature in nearestCreatures.ToArray())
                    {
                        Creature summon = creature is Player ? ((Player)creature).GetSummon() : null;
                        if (summon != null && result.GetTargets().Contains(summon))
                            nearestCreatures.Add(summon);
                    }
                }
                result.GetTargets().RemoveAll(t => !nearestCreatures.Contains(t));
                break;
        }
        return true;
    }
}
