using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Templates.Item.Enums;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/WeaponCondition (ATracer).
/// </summary>
public class WeaponCondition : Condition
{
    [XmlAttribute("weapon")]
    public List<ItemGroup>? itemGroups;

    public override bool Validate(Skill env)
    {
        if (env.GetSkillMethod() != Skill.SkillMethod.CAST)
            return true;

        return IsValidWeapon(env.GetEffector());
    }

    public override bool Validate(Stat2 stat, IStatFunction statFunction)
    {
        return IsValidWeapon(stat.GetOwner());
    }

    private bool IsValidWeapon(Creature creature)
    {
        if (creature is Player)
        {
            Player player = (Player)creature;
            return itemGroups!.Contains(player.GetEquipment().GetMainHandWeaponType());
        }
        // for npcs we don't validate weapon, though in templates they are present
        return true;
    }
}
