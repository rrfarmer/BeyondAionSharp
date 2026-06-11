using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Item.Enums;

namespace Aion.GameServer.Model.Templates.Item.Actions;

/// <summary>Java parity: model/templates/item/actions/ApExtractAction.</summary>
public class ApExtractAction : AbstractItemAction
{
    [XmlAttribute("target")] protected UseTarget target;
    [XmlAttribute("rate")] protected float rate;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (targetItem == null || !targetItem.CanApExtract())
            return false;
        if (parentItem.GetItemTemplate().GetLevel() < targetItem.GetItemTemplate().GetLevel())
            return false;
        if (parentItem.GetItemTemplate().GetItemQuality() != targetItem.GetItemTemplate().GetItemQuality())
            return false;

        // TODO: ApExtractTarget.OTHER, ApExtractTarget.ALL. Find out what should go there

        UseTarget? type = null;
        switch (targetItem.GetItemTemplate().GetItemGroup())
        {
            case ItemGroup.SWORD:
            case ItemGroup.DAGGER:
            case ItemGroup.MACE:
            case ItemGroup.ORB:
            case ItemGroup.SPELLBOOK:
            case ItemGroup.BOW:
            case ItemGroup.GREATSWORD:
            case ItemGroup.POLEARM:
            case ItemGroup.STAFF:
            case ItemGroup.HARP:
            case ItemGroup.GUN:
            case ItemGroup.KEYBLADE:
            case ItemGroup.CANNON:
                type = UseTarget.WEAPON;
                break;
            case ItemGroup.RB_TORSO:
            case ItemGroup.RB_PANTS:
            case ItemGroup.RB_SHOULDER:
            case ItemGroup.RB_GLOVE:
            case ItemGroup.RB_SHOES:
            case ItemGroup.CL_TORSO:
            case ItemGroup.CL_PANTS:
            case ItemGroup.CL_SHOULDER:
            case ItemGroup.CL_GLOVE:
            case ItemGroup.CL_SHOES:
            case ItemGroup.CH_TORSO:
            case ItemGroup.CH_PANTS:
            case ItemGroup.CH_SHOULDER:
            case ItemGroup.CH_GLOVE:
            case ItemGroup.CH_SHOES:
            case ItemGroup.LT_TORSO:
            case ItemGroup.LT_PANTS:
            case ItemGroup.LT_SHOULDER:
            case ItemGroup.LT_GLOVE:
            case ItemGroup.LT_SHOES:
            case ItemGroup.PL_TORSO:
            case ItemGroup.PL_PANTS:
            case ItemGroup.PL_SHOULDER:
            case ItemGroup.PL_GLOVE:
            case ItemGroup.PL_SHOES:
            case ItemGroup.SHIELD:
                type = UseTarget.ARMOR;
                break;
            case ItemGroup.NECKLACE:
            case ItemGroup.EARRING:
            case ItemGroup.RING:
            case ItemGroup.BELT:
            case ItemGroup.HEAD:
                type = UseTarget.ACCESSORY;
                break;
            case ItemGroup.NONE:
                if (targetItem.GetItemTemplate().GetItemGroup() == ItemGroup.WING)
                {
                    type = UseTarget.WING;
                    break;
                }
                return false;
            default:
                return false;
        }
        return (target == UseTarget.EQUIPMENT || target == type);
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Model.Templates.Item.Acquisition acquisition = targetItem.GetItemTemplate().GetAcquisition();
        if (acquisition == null || acquisition.GetRequiredAp() == 0)
            return;
        int ap = (int)(acquisition.GetRequiredAp() * rate);
        Aion.GameServer.Model.Items.Storage.Storage inventory = player.GetInventory();

        if (inventory.Delete(targetItem) != null)
        {
            if (inventory.DecreaseByObjectId(parentItem.GetObjectId(), 1))
                Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, ap);
        }
        else
            Aion.GameServer.Utils.Audit.AuditLogger.Log(player, "possibly using item AP extraction hack");
    }

    public UseTarget GetTarget()
    {
        return target;
    }

    public float GetRate()
    {
        return rate;
    }
}
