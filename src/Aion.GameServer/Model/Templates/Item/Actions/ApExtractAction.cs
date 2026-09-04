using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Items.Enums;

namespace Aion.GameServer.Model.Templates.Items.Actions;

/// <summary>Java parity: model/templates/item/actions/ApExtractAction.</summary>
public class ApExtractAction : AbstractItemAction
{
    private const int CASTING_DELAY = 3000;

    [XmlAttribute("target")] public UseTarget target;
    [XmlAttribute("rate")] public float rate;

    public override bool CanAct(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        if (targetItem == null || !targetItem.CanApExtract())
        {
            if (targetItem != null)
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_CANNOT(targetItem.GetL10n()));
            return false;
        }
        if (targetItem.IsEquipped())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_WRONG_EQUIPED());
            return false;
        }
        if (parentItem.GetItemTemplate().GetLevel() < targetItem.GetItemTemplate().GetLevel())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_WRONG_LEVEL(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }
        if (parentItem.GetItemTemplate().GetItemQuality().GetQualityId() < targetItem.GetItemTemplate().GetItemQuality().GetQualityId())
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player,
                Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_WRONG_QUALITY(parentItem.GetL10n(), targetItem.GetL10n()));
            return false;
        }

        UseTarget type;
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
            case ItemGroup.TORSO:
            case ItemGroup.PANTS:
            case ItemGroup.SHOULDER:
            case ItemGroup.GLOVE:
            case ItemGroup.SHOES:
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
            case ItemGroup.WING:
                type = UseTarget.WING;
                break;
            case ItemGroup.NONE:
                // e.g. non-equipment "junk" items retail still allows AP extraction on (confirmed only matched by the OTHER/ALL target types)
                type = UseTarget.OTHER;
                break;
            default:
                Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_CANNOT(targetItem.GetL10n()));
                return false;
        }
        // EQUIPMENT is a shorthand for "any of WEAPON/ARMOR/ACCESSORY/WING", confirmed on retail it does NOT also match OTHER
        if (target != UseTarget.ALL && target != type && !(target == UseTarget.EQUIPMENT && type != UseTarget.OTHER))
        {
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_CANNOT(targetItem.GetL10n()));
            return false;
        }
        return true;
    }

    public override void Act(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem, params object[] @params)
    {
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), CASTING_DELAY, 0, 0), true);

        var observer = new ApExtractObserver(player, parentItem, targetItem);
        player.GetObserveController().Attach(observer);
        player.GetController().AddTask(Aion.GameServer.Model.TaskId.ITEM_USE, Aion.GameServer.Utils.ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            player.GetObserveController().RemoveObserver(observer);
            FinishUse(player, parentItem, targetItem);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(CASTING_DELAY)));
    }

    // Java parity: anonymous ItemUseObserver in act.
    private sealed class ApExtractObserver : Aion.GameServer.Controllers.Observer.ItemUseObserver
    {
        private readonly Aion.GameServer.Model.GameObjects.Players.Player player;
        private readonly Item parentItem;
        private readonly Item targetItem;

        public ApExtractObserver(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
        {
            this.player = player;
            this.parentItem = parentItem;
            this.targetItem = targetItem;
        }

        public override void Abort()
        {
            player.GetController().CancelTask(Aion.GameServer.Model.TaskId.ITEM_USE);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_ITEM_CANCELED(targetItem.GetL10n()));
            Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
                new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, 2, 0), true);
            player.GetObserveController().RemoveObserver(this);
        }
    }

    private void FinishUse(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        bool success = ExtractAp(player, parentItem, targetItem);
        if (success)
            player.StartCooldown(parentItem);
        Aion.GameServer.Utils.PacketSendUtility.BroadcastPacket(player,
            new Aion.GameServer.Network.Aion.ServerPackets.SM_ITEM_USAGE_ANIMATION(player.GetObjectId(), parentItem.GetObjectId(), parentItem.GetItemId(), 0, success ? 1 : 2, 0), true);
    }

    private bool ExtractAp(Aion.GameServer.Model.GameObjects.Players.Player player, Item parentItem, Item targetItem)
    {
        if (!CanAct(player, parentItem, targetItem))
            return false;
        Aion.GameServer.Model.Templates.Items.Acquisition acquisition = targetItem.GetItemTemplate().GetAcquisition();
        if (acquisition == null || acquisition.GetRequiredAp() == 0)
            return false;
        Aion.GameServer.Model.Items.Storage.Storage inventory = player.GetInventory();
        if (!inventory.DecreaseByObjectId(parentItem.GetObjectId(), 1) || inventory.Delete(targetItem) == null)
        {
            Aion.GameServer.Utils.Audit.AuditLogger.Log(player, "possibly using item AP extraction hack");
            return false;
        }
        int ap = (int)(acquisition.GetRequiredAp() * rate);
        Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_ITEM_SUCCEED(targetItem.GetL10n()));
        Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, ap, Aion.GameServer.Network.Aion.ServerPackets.SM_SYSTEM_MESSAGE.STR_MSG_AP_DECOMPOSE_ITEM_SUCCEED_AP);
        return true;
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
