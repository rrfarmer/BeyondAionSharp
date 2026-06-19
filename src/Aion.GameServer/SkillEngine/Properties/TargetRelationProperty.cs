using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Model.Templates.Npc;

namespace Aion.GameServer.SkillEngine.Properties;

/// <summary>Java parity: skillengine/properties/TargetRelationProperty (ATracer). Static set: switch ALL/ENEMY (removeIf !isEnemy unless material)/FRIEND (removeIf enemy||!buffAllowed unless material; empty→self; else firstTarget=first)/MYPARTY (iterator keep same-team buff-allowed else remove; firstTarget=first); isBuffAllowed (SiegeNpc abyss-type deny ARTIFACT/CORE/DOOR/DOORREPAIR else sameAreaType); isSameAreaType=isInsidePvPZone equality. removeIf→RemoveAll, getFirst→[0]. Properties/SiegeNpc red-tolerated.</summary>
public class TargetRelationProperty
{
    public static bool Set(Properties properties, Properties.ValidationResult result, Creature effector, SkillTemplate skillTemplate)
    {
        TargetRelationAttribute? value = properties.GetTargetRelation(); // Java parity: null when target_relation absent
        switch (value)
        {
            case TargetRelationAttribute.ALL:
                break;
            case TargetRelationAttribute.ENEMY:
                if (!DataManager.MATERIAL_DATA.IsMaterialSkill(skillTemplate.GetSkillId()))
                    result.GetTargets().RemoveAll(target => !effector.IsEnemy(target));
                break;
            case TargetRelationAttribute.FRIEND:
                if (!DataManager.MATERIAL_DATA.IsMaterialSkill(skillTemplate.GetSkillId()))
                    result.GetTargets().RemoveAll(target => effector.IsEnemy(target) || !IsBuffAllowed(effector, target));

                if (result.GetTargets().Count == 0)
                {
                    result.SetFirstTarget(effector);
                    result.GetTargets().Add(effector);
                }
                else
                {
                    result.SetFirstTarget(result.GetTargets()[0]);
                }
                break;
            case TargetRelationAttribute.MYPARTY:
                result.GetTargets().RemoveAll(target =>
                {
                    if (effector.GetMaster() is Player sourcePlayer && IsBuffAllowed(effector, target))
                    {
                        if (target.GetMaster().Equals(sourcePlayer))
                            return false;
                        if (target.GetMaster() is Player targetPlayer)
                        {
                            int teamId = sourcePlayer.GetCurrentTeamId();
                            if (teamId > 0 && teamId == targetPlayer.GetCurrentTeamId() && !sourcePlayer.IsEnemy(targetPlayer))
                                return false;
                        }
                    }
                    return true;
                });

                if (result.GetTargets().Count != 0)
                {
                    result.SetFirstTarget(result.GetTargets()[0]);
                }
                break;
        }

        return true;
    }

    /// <summary>true = allow buff, false = deny buff</summary>
    public static bool IsBuffAllowed(Creature source, Creature target)
    {
        if (source == null || target == null)
        {
            return false;
        }

        if (target is SiegeNpc)
        {
            switch (((SiegeNpc)target).GetObjectTemplate().GetAbyssNpcType())
            {
                case AbyssNpcType.ARTIFACT:
                case AbyssNpcType.ARTIFACT_EFFECT_CORE:
                case AbyssNpcType.DOOR:
                case AbyssNpcType.DOORREPAIR:
                    return false;
            }
        }

        return IsSameAreaType(source, target);
    }

    public static bool IsSameAreaType(Creature source, Creature target)
    {
        return source.IsInsidePvPZone() == target.IsInsidePvPZone();
    }
}
