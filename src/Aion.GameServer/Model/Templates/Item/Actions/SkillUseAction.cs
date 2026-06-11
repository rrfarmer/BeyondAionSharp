using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/SkillUseAction.</summary>
[XmlType("SkillUseAction")]
public class SkillUseAction : AbstractItemAction
{
    [XmlAttribute("skillid")] protected int skillid;
    [XmlAttribute("level")] protected int level;
    [XmlAttribute("mapid")] private int mapid;

    public int GetSkillId()
    {
        return skillid;
    }

    public int GetLevel()
    {
        return level;
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (mapid != 0 && player.GetWorldId() != mapid)
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_CAN_NOT_USE_ITEM_IN_CURRENT_POSITION());
            return false;
        }
        Aion.GameServer.SkillEngine.Model.Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(player, skillid, level, player.GetTarget(), parentItem.GetItemTemplate());
        if (skill == null)
            return false;
        List<Aion.GameServer.SkillEngine.Effect.EffectTemplate> effects = skill.GetSkillTemplate().GetEffects().GetEffects();
        if (effects.Count != 0)
        {
            foreach (Aion.GameServer.SkillEngine.Effect.EffectTemplate template in effects)
            {
                if (player.IsTransformed() && template is Aion.GameServer.SkillEngine.Effect.TransformEffect) // Cant use transform items while already transformed
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_CAN_NOT_CAST_IN_SHAPECHANGE());
                    return false;
                }
                if (player.GetSummon() != null && template is Aion.GameServer.SkillEngine.Effect.SummonEffect)
                {
                    Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_SKILL_SUMMON_ALREADY_HAVE_A_FOLLOWER());
                    return false;
                }
            }
        }
        if (!skill.CanUseSkill(Aion.GameServer.SkillEngine.Properties.Properties.CastState.CAST_START)) // also initializes effectedList for isIneffectiveHealSkill check
            return false;
        if (Aion.GameServer.Configs.Main.CustomConfig.IGNORE_POTIONS_AT_FULL_HEALTH && IsIneffectiveHealSkill(effects, skill.GetEffectedList()))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_NOTHING_HAPPEN());
            return false;
        }
        return true;
    }

    private static bool IsIneffectiveHealSkill(List<Aion.GameServer.SkillEngine.Effect.EffectTemplate> effects, List<Creature> effectedList)
    {
        int hpHealEffects = 0, mpHealEffects = 0;
        foreach (Aion.GameServer.SkillEngine.Effect.EffectTemplate template in effects)
        {
            if (template.GetValue() < 0) // negative heal value means damage
                return false;
            if (template is Aion.GameServer.SkillEngine.Effect.HealEffect || template is Aion.GameServer.SkillEngine.Effect.HealInstantEffect || template is Aion.GameServer.SkillEngine.Effect.ProcHealInstantEffect)
            {
                hpHealEffects++;
            }
            else if (template is Aion.GameServer.SkillEngine.Effect.MPHealEffect || template is Aion.GameServer.SkillEngine.Effect.MPHealInstantEffect || template is Aion.GameServer.SkillEngine.Effect.ProcMPHealInstantEffect)
            {
                mpHealEffects++;
            }
            else
            {
                return false;
            }
        }
        return (hpHealEffects == 0 || effectedList.All(effected => effected.GetLifeStats().IsFullyRestoredHp()))
            && (mpHealEffects == 0 || effectedList.All(effected => effected.GetLifeStats().IsFullyRestoredMp()));
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.SkillEngine.Model.Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(player, skillid, level, player.GetTarget(), parentItem.GetItemTemplate());
        if (skill != null)
        {
            player.GetController().CancelUseItem();
            skill.SetItemObjectId(parentItem.GetObjectId());
            skill.UseSkill();
        }
    }

    public int GetMapId()
    {
        return mapid;
    }
}
