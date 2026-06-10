using System.Xml.Serialization;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Skill;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/WeaponDualEffect : BufEffect. instanceof Player p→is Player p; setSkillEfficiency(skillEfficiency/100f), setMaxDamageChance(maxDamageChance+lvl*maxDamageDelta), setMinDamageRatio((value+lvl*delta)/100f); endEffect resets+super; static hasDualWieldEffect: !isSpawned fallback loops skills for WEAPONDUAL effectType. PlayerSkillEntry/Effects/EffectType red-tolerated.</summary>
[XmlType("WeaponDualEffect")]
public class WeaponDualEffect : BufEffect
{
    public override void StartEffect(Effect effect)
    {
        if (effect.GetEffected() is Player p)
        {
            p.GetGameStats().SetSkillEfficiency(SkillEfficiency / 100f);
            p.GetGameStats().SetMaxDamageChance(MaxDamageChance + effect.GetSkillLevel() * MaxDamageDelta);
            p.GetGameStats().SetMinDamageRatio((Value + effect.GetSkillLevel() * Delta) / 100f);
            p.GetGameStats().UpdateStatsVisually();
        }
    }

    public override void EndEffect(Effect effect)
    {
        if (effect.GetEffected() is Player p)
        {
            p.GetGameStats().SetSkillEfficiency(0);
            p.GetGameStats().SetMaxDamageChance(0);
            p.GetGameStats().SetMinDamageRatio(0);
            p.GetGameStats().UpdateStatsVisually();
        }
        base.EndEffect(effect);
    }

    public static bool HasDualWieldEffect(Player player)
    {
        if (!player.IsSpawned())
        { // fallback for enterWorld
            foreach (PlayerSkillEntry skillEntry in player.GetSkillList().GetAllSkills())
            {
                Effects effects = DataManager.SKILL_DATA.GetSkillTemplate(skillEntry.GetSkillId()).GetEffects();
                if (effects != null && effects.HasAnyEffectType(EffectType.WEAPONDUAL))
                    return true;
            }
        }
        return player.GetGameStats().GetSkillEfficiency() != 0;
    }
}
