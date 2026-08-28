using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/CraftLearnAction.</summary>
[XmlType("CraftLearnAction")]
public class CraftLearnAction : AbstractItemAction
{
    [XmlAttribute("recipeid")] public int recipeid;

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (!player.GetInventory().DecreaseByObjectId(parentItem.GetObjectId(), 1))
            return;
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_USE_ITEM(parentItem.GetL10n()));
        // client shows the "you learned" toast from this
        if (Aion.GameServer.Services.RecipeService.AddRecipe(player, recipeid, false))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemTemplate().GetTemplateId()));
        }
    }

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        return Aion.GameServer.Services.RecipeService.ValidateNewRecipe(player, recipeid) != null;
    }

    public int GetRecipeId()
    {
        return recipeid;
    }
}
