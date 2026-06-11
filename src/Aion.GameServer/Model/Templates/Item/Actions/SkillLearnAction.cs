using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/SkillLearnAction.</summary>
[XmlType("SkillLearnAction")]
public class SkillLearnAction : AbstractItemAction
{
    [XmlAttribute("skillid")] protected int skillid;
    [XmlAttribute("level")] protected int level;
    [XmlAttribute("class")] protected PlayerClass? playerClass;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // 1. check player level
        if (player.GetCommonData().GetLevel() < level)
            return false;

        PlayerClass pc = player.GetCommonData().GetPlayerClass();
        if (!ValidateClass(pc))
            return false;

        // 4. check player race and Race.PC_ALL
        Race race = parentItem.GetItemTemplate().GetRace();
        if (player.GetRace() != race && race != Race.PC_ALL)
            return false;
        // 5. check whether this skill is already learned
        if (player.GetSkillList().IsSkillPresent(skillid))
            return false;

        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        // item animation and message
        Aion.GameServer.Model.Templates.Items.ItemTemplate itemTemplate = parentItem.GetItemTemplate();
        player.GetController().CancelUseItem();
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SmItemUsageAnimation(player.GetObjectId(), parentItem.GetObjectId(), itemTemplate.GetTemplateId()), true);

        // add skill
        Aion.GameServer.Services.SkillLearnService.LearnSkillBook(player, skillid);

        // remove book from inventory (assuming it's not stackable)
        Item item = player.GetInventory().GetItemByObjId(parentItem.GetObjectId());
        player.GetInventory().Delete(item);
    }

    private bool ValidateClass(PlayerClass pc)
    {
        return playerClass == null || playerClass == pc || playerClass == pc.GetStartingClass();
    }
}
